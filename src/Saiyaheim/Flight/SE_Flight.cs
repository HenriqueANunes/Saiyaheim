using Saiyaheim.Ki;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Flight
{
    /// <summary>
    /// O voo: um <c>StatusEffect</c> que liga o <c>Character.m_flying</c> e deixa o motor do
    /// jogo fazer o resto.
    ///
    /// <b>Zero patch Harmony na física.</b> <c>Character.UpdateMotion</c> desvia para o
    /// <c>UpdateFlying()</c> nativo quando <c>m_flying</c> é true, e de lá saem prontos: gravidade
    /// desligada, aceleração suave com clamp de força, giro com <c>m_flyTurnSpeed</c>, dois modos
    /// de velocidade escolhidos pelo botão de correr e — o mais importante —
    /// <c>m_maxAirAltitude</c> reposto todo tick, o que torna dano de queda impossível <i>enquanto
    /// se voa</i>. Parar de voar no ar devolve a queda inteira, que é a tensão que o design pede.
    ///
    /// Ser <c>StatusEffect</c> em vez de patch dá três coisas de graça: sincroniza no multiplayer
    /// (etapa 8), pode aparecer na barra de status da HUD e some sozinho na morte.
    ///
    /// <b>Por que o gasto de ki e o input vertical moram aqui e a pose não:</b>
    /// <c>SEMan.Update</c> é chamado dentro de <c>Character.CustomFixedUpdate</c> <i>logo antes</i>
    /// de <c>UpdateMotion</c>. É a única janela em que dá para escrever no <c>m_moveDir</c> depois
    /// do <c>PlayerController</c> e antes do <c>UpdateFlying</c> lê-lo. Pela mesma razão de ordem,
    /// forçar animator daqui não funcionaria — o <c>UpdateFlying</c> sobrescreveria em seguida.
    /// Isso está em <see cref="FlightPosePatch"/>.
    ///
    /// Herda de <c>SE_Stats</c> pelo mesmo motivo do <c>SE_KiBody</c>: os campos de modificador
    /// já estão lá quando a etapa 5 precisar deles.
    /// </summary>
    internal class SE_Flight : SE_Stats
    {
        /// <summary>
        /// Nome do objeto, não o campo <c>m_name</c>: <c>StatusEffect.NameHash()</c> usa
        /// <c>UnityEngine.Object.name</c>.
        /// </summary>
        internal const string ObjectName = "SE_SaiyaheimFlight";

        internal static readonly int NameHashValue = ObjectName.GetStableHashCode();

        /// <summary>Valores originais do prefab, restaurados no <see cref="Stop"/>.</summary>
        private float _originalSlowSpeed;
        private float _originalFastSpeed;
        private float _originalTurnSpeed;
        private bool _speedsSaved;

        /// <summary>Metros percorridos no ar e ainda não convertidos em XP. Ver <see cref="FlushXp"/>.</summary>
        private float _pendingXpMeters;

        /// <summary>Posição no tique anterior, que é de onde sai a distância deste tique.</summary>
        private Vector3 _lastPosition;

        /// <summary>
        /// Já houve um tique para comparar. Sem isto, o primeiro tique de um voo mediria a
        /// distância desde a última vez que este efeito rodou — a pé, no chão, possivelmente
        /// quilômetros atrás.
        /// </summary>
        private bool _hasLastPosition;

        /// <summary>
        /// Metros acumulados antes de pagar XP de uma vez. Mesmo papel do "um segundo" de antes:
        /// evitar ~50 <c>RaiseSkill</c> por segundo sem atrasar o pagamento a ponto de um voo
        /// curto não render nada (o <see cref="Stop"/> força a descarga do resto).
        /// </summary>
        private const float XpFlushMeters = 5f;

        internal static SE_Flight CreateTemplate()
        {
            var effect = CreateInstance<SE_Flight>();
            effect.name = ObjectName;
            effect.m_name = "Flight";
            effect.m_tooltip = "You are flying. Ki drains while airborne.";

            // Sem ícone por enquanto: SEMan.GetHUDStatusEffects filtra por m_icon, então o efeito
            // não ocupa espaço na barra de status. Arte é polimento da etapa 11.
            effect.m_icon = null;

            // m_ttl = 0 é permanente. Quem tira é o FlightManager: tecla, ki no zero ou o jogador
            // entrar num estado incompatível.
            effect.m_ttl = 0f;

            return effect;
        }

        public override void Setup(Character character)
        {
            base.Setup(character);

            if (character == null)
            {
                return;
            }

            _originalSlowSpeed = character.m_flySlowSpeed;
            _originalFastSpeed = character.m_flyFastSpeed;
            _originalTurnSpeed = character.m_flyTurnSpeed;
            _speedsSaved = true;

            ApplySpeeds(character as Player);

            // TakeOff() liga m_flying, dispara o m_jumpEffects (o baque de poeira da decolagem) e
            // pede o trigger "fly_takeoff". Esse trigger não existe no animator do jogador, mas o
            // ZSyncAnimation.Awake já desliga o logWarnings do Animator — não vira spam no console.
            character.TakeOff();

            // Âncora do odômetro. Sem isto o primeiro tique cobraria o caminho andado desde a
            // aterrissagem anterior, que é XP de graça por ter caminhado.
            _lastPosition = character.transform.position;
            _hasLastPosition = true;
            _pendingXpMeters = 0f;
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);

            if (!(m_character is Player player))
            {
                return;
            }

            // Recalculado todo tick de propósito: pegar um item muda o peso, e a config pode ser
            // editada com o jogo aberto.
            ApplySpeeds(player);

            // Tela de carregamento do portal ou da dungeon: não é jogo, não cobra ki nem mede
            // caminho. O primeiro tique depois dela vê o salto até o destino, e o MeasureStep
            // descarta.
            if (player.IsTeleporting())
            {
                return;
            }

            ApplyVerticalInput(player);
            DrainKi(player, dt);
            FlushXp(player, dt);

        }

        public override void Stop()
        {
            if (m_character != null)
            {
                // Land() desliga m_flying. A partir do próximo passo de física a gravidade volta e
                // m_maxAirAltitude para de ser reposto — cair de alto machuca, como deve.
                m_character.Land();

                if (_speedsSaved)
                {
                    m_character.m_flySlowSpeed = _originalSlowSpeed;
                    m_character.m_flyFastSpeed = _originalFastSpeed;
                    m_character.m_flyTurnSpeed = _originalTurnSpeed;
                }

                // Sem isto, o resto do odômetro abaixo do XpFlushMeters se perderia — um voo
                // curto nunca pagaria XP nenhum. dt zero: aqui só se descarrega o que já foi
                // medido, não se mede mais nada.
                FlushXp(m_character as Player, 0f, force: true);
            }

            base.Stop();
        }

        private static void ApplySpeeds(Player player)
        {
            if (player == null)
            {
                return;
            }

            player.m_flySlowSpeed = FlightStats.GetSlowSpeed(player);
            player.m_flyFastSpeed = FlightStats.GetFastSpeed(player);
            player.m_flyTurnSpeed = SaiyaheimConfig.FlightTurnSpeed.Value;
        }

        /// <summary>
        /// Subir e descer.
        ///
        /// <c>Character.SetMoveDir</c> aceita componente Y e o <c>UpdateFlying</c> multiplica o
        /// vetor inteiro pela velocidade — ou seja, altitude sai de graça, sem tocar no rigidbody.
        /// O <c>PlayerController</c> zera o Y ao montar o <c>m_moveDir</c> (ele projeta o olhar no
        /// plano), então o componente vertical é inteiramente nosso.
        ///
        /// <b>ZInput, não <c>UnityEngine.Input</c>:</b> respeita rebind e gamepad. São os mesmos
        /// botões que o voo de debug do próprio jogo usa.
        /// </summary>
        private static void ApplyVerticalInput(Player player)
        {
            // Um jogador remoto não roda SEMan.Update (só o dono roda), mas a guarda é barata e
            // deixa explícito que ler input aqui só faz sentido para quem está no teclado.
            if (player != Player.m_localPlayer)
            {
                return;
            }

            // Kamehameha carregando: pairar parado, sem subir nem descer. O SE_KiBody também zera,
            // mas se ele rodar antes deste efeito o componente vertical voltaria aqui.
            if (Attacks.KiBeamCharge.IsCharging(player))
            {
                Attacks.KiBeamCharge.HoldStill(player);
                return;
            }

            float vertical = 0f;

            // Character.TakeInput() seria o teste completo, mas é protected: compila por causa da
            // assembly publicizada e estoura MethodAccessException em runtime.
            if (InputGuard.AcceptsInput())
            {
                if (ZInput.GetButton("Jump") || ZInput.GetButton("JoyJump"))
                {
                    vertical = 1f;
                }
                else if (ZInput.GetButton("Crouch") || ZInput.GetButtonPressedTimer("JoyCrouch") > 0.33f)
                {
                    vertical = -1f;
                }
            }

            Vector3 dir = player.GetMoveDir();
            dir.y = vertical * SaiyaheimConfig.FlightVerticalSpeedFactor.Value;

            // Normalizar só acima de 1: subir enquanto anda para frente daria magnitude 1.25 e o
            // jogador voaria mais rápido na diagonal do que na reta.
            if (dir.sqrMagnitude > 1f)
            {
                dir.Normalize();
            }

            player.SetMoveDir(dir);
        }

        /// <summary>
        /// Cobra o ki do voo. <c>UpdateStatusEffect</c> vem do <c>FixedUpdate</c>, então isto já é
        /// tick fixo — a regra do projeto de nunca cobrar recurso por frame está atendida.
        ///
        /// Quem percebe o ki zerado e derruba o jogador é o <see cref="FlightManager"/>: remover um
        /// status effect de dentro do <c>SEMan.Update</c> corromperia o laço dele, que cacheia o
        /// <c>Count</c> antes de iterar.
        /// </summary>
        private static void DrainKi(Player player, float dt)
        {
            bool fast = GameAccess.IsRunPressed(player);

            // Depois do ApplyVerticalInput de propósito: é ele que escreve o componente Y no
            // m_moveDir, e sem isso subir com o Jump passaria por "parado no ar".
            bool hovering = FlightStats.IsHovering(player);

            KiManager.Drain(FlightStats.GetKiCostPerSecond(player, fast, hovering) * dt);
        }

        /// <summary>
        /// Odômetro: mede o quanto o jogador andou neste tique e paga XP a cada
        /// <see cref="XpFlushMeters"/> metros. Chamar <c>RaiseSkill</c> a cada passo de física
        /// seriam ~50 chamadas por segundo para o mesmo efeito.
        ///
        /// <b>É caminho percorrido, não distância até a decolagem.</b> Ir e voltar paga as duas
        /// pernas, porque as duas custaram ki. Medir contra o ponto de partida faria a volta para
        /// casa pagar negativo e o círculo pagar zero gastando ki.
        ///
        /// <paramref name="dt"/> zero significa "só descarregue o que já foi medido": é como o
        /// <see cref="Stop"/> chama, e é o que impede a última posição de ser reescrita quando não
        /// houve tique de física nenhum.
        /// </summary>
        private void FlushXp(Player player, float dt, bool force = false)
        {
            if (player == null)
            {
                return;
            }

            if (dt > 0f)
            {
                Vector3 position = player.transform.position;

                if (_hasLastPosition)
                {
                    _pendingXpMeters += MeasureStep(player, position, dt);
                }

                _lastPosition = position;
                _hasLastPosition = true;
            }

            if (_pendingXpMeters <= 0f || (!force && _pendingXpMeters < XpFlushMeters))
            {
                return;
            }

            FlightSkill.RaiseFromFlightDistance(player, _pendingXpMeters);
            _pendingXpMeters = 0f;
        }

        /// <summary>
        /// Quanto contar do salto de posição deste tique.
        ///
        /// <b>O clamp não é paranoia.</b> Posição do jogador também muda sem ele voar: portal,
        /// respawn, knockback de um golpe pesado e o reposicionamento que o jogo faz ao carregar
        /// uma zona. Qualquer um deles vira centenas de metros num tique, e o portal é o caso que
        /// um jogador acha sozinho. Salto acima do que a velocidade máxima deste voo permite é
        /// descartado inteiro — perder alguns metros legítimos é invisível, e XP de graça por
        /// atravessar o mapa num portal não é.
        ///
        /// A margem de 2x cobre um tique mais longo que o normal (engasgo, carregamento de zona)
        /// sem abrir espaço para um teleporte curto.
        /// </summary>
        private float MeasureStep(Player player, Vector3 position, float dt)
        {
            float step = Vector3.Distance(position, _lastPosition);

            // O +1 é um piso: com dt pequeno e o jogador quase parado, um limite proporcional puro
            // ficaria tão apertado que descartaria movimento real.
            float limit = (FlightStats.GetFastSpeed(player) + 1f) * dt * 2f;

            if (step <= limit)
            {
                return step;
            }

            SaiyaheimPlugin.LogVerbose(
                $"Flight odometer: ignored a {step:0.#} m jump in {dt:0.###} s (limit {limit:0.#} m) " +
                "— teleport, respawn or knockback.");

            return 0f;
        }
    }
}
