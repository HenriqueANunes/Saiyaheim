using Saiyaheim.Power;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Ki
{
    /// <summary>
    /// Dono do ki do jogador local: regeneração, carregamento ativo, gasto e persistência.
    ///
    /// Zero patch Harmony. O tick sai do <c>Update</c> do próprio plugin lendo
    /// <c>Player.m_localPlayer</c>, e a persistência é o dicionário que o jogo já serializa.
    /// Menos superfície para quebrar quando o Valheim atualizar.
    ///
    /// Etapa 2 do roadmap. Multiplayer (etapa 8) ainda não existe: isto é estado local.
    /// </summary>
    internal static class KiManager
    {
        private static Player _trackedPlayer;
        private static KiState _state;
        private static float _tickAccumulator;

        /// <summary>Estado do jogador local. Null antes de entrar no mundo.</summary>
        internal static KiState State => _state;

        /// <summary>Verdadeiro enquanto a tecla de carregar está sendo segurada e o carregamento é válido.</summary>
        internal static bool IsCharging { get; private set; }

        /// <summary>Ki atual, ou 0 se não há jogador. Ki desligado lê como zero — é a regra do toggle.</summary>
        internal static float Current => _state == null || !_state.Enabled ? 0f : _state.Current;

        /// <summary>
        /// Teto de ki. Cresce com o nível de Battle Power: com um teto fixo a barra teria o
        /// mesmo tamanho do primeiro ao último boss e a progressão não apareceria em lugar nenhum
        /// da HUD.
        /// </summary>
        internal static float Max => MaxFor(Player.m_localPlayer);

        /// <inheritdoc cref="Max"/>
        internal static float MaxFor(Player player)
        {
            return SaiyaheimConfig.MaxKi.Value
                   + SaiyaheimConfig.MaxKiPerPowerLevel.Value * PowerSkill.GetLevel(player);
        }

        internal static bool IsEnabled => _state != null && _state.Enabled;

        /// <summary>
        /// Regeneração passiva por segundo, já escalada pelo poder e pelo descanso.
        ///
        /// Escala com o poder porque o teto não é fixo: <see cref="MaxFor"/> cresce com o nível de
        /// Battle Power, e uma torneira plena enchendo um reservatório cada vez maior significa que
        /// ficar forte deixa o jogador proporcionalmente <b>mais lento</b> para recuperar ki —
        /// o contrário do que o mod quer dizer.
        ///
        /// Aditiva no poder, como todo o resto do mod. Lê o power level derivado, a mesma base do
        /// <c>FlightSpeedFromPower</c>: se comer melhor faz voar mais rápido, faz recarregar
        /// mais rápido também.
        ///
        /// O bônus de <see cref="IsRested"/> entra por último e é <b>multiplicativo</b>: aditivo
        /// ele seria decisivo no começo do jogo e ruído no fim, quando a torneira já é grande.
        /// </summary>
        internal static float RegenPerSecondFor(Player player)
        {
            float perSecond = SaiyaheimConfig.KiRegenPerSecond.Value
                              + SaiyaheimConfig.KiRegenFromPower.Value * PowerLevel.GetRaw(player);

            return IsRested(player)
                ? perSecond * SaiyaheimConfig.KiRegenRestedMultiplier.Value
                : perSecond;
        }

        /// <summary>
        /// Segundos de bloqueio da regeneração depois de gastar ki, já com o desconto do descanso.
        ///
        /// É aqui que o Rested aparece de verdade. Multiplicar uma torneira de poucos ki por
        /// segundo some numa barra de centenas; o que o jogador cronometra no meio da luta é esta
        /// pausa, e encurtá-la muda o ritmo do combate.
        /// </summary>
        internal static float RegenDelayFor(Player player)
        {
            float delay = SaiyaheimConfig.KiRegenDelay.Value;

            return IsRested(player)
                ? delay * SaiyaheimConfig.KiRegenDelayRestedMultiplier.Value
                : delay;
        }

        /// <summary>
        /// Verdadeiro enquanto o jogador está com o buff <c>Rested</c> do jogo base — fogueira sob
        /// abrigo, ou acordar numa cama.
        ///
        /// O buff dura <c>300s + 60s por nível de conforto</c> e vai junto com o jogador para o
        /// campo, então o bônus não é "recarrega mais rápido em casa" (carregar já é mais rápido
        /// que esperar), é sair de casa aguentando mais tempo de luta lá fora.
        ///
        /// Status effect nativo consultado pela API pública do <c>SEMan</c>: sincroniza sozinho no
        /// multiplayer e não precisa de patch nenhum.
        /// </summary>
        internal static bool IsRested(Player player)
        {
            if (player == null)
            {
                return false;
            }

            SEMan seman = player.GetSEMan();
            return seman != null && seman.HaveStatusEffect(SEMan.s_statusEffectRested);
        }

        /// <summary>Carregamento ativo por segundo, já escalado pelo poder. Ver <see cref="RegenPerSecondFor"/>.</summary>
        internal static float ChargePerSecondFor(Player player)
        {
            return SaiyaheimConfig.ChargeKiPerSecond.Value
                   + SaiyaheimConfig.ChargeKiFromPower.Value * PowerLevel.GetRaw(player);
        }

        /// <summary>
        /// Segundos para encher a barra do zero. <b>É este o número a calibrar</b>, não o ki por
        /// segundo: o absoluto sozinho não diz nada, porque o teto também se move.
        /// </summary>
        internal static float SecondsToFill(float perSecond)
        {
            return perSecond <= 0f ? float.PositiveInfinity : MaxFor(Player.m_localPlayer) / perSecond;
        }

        internal static void Update(float dt)
        {
            Player player = Player.m_localPlayer;

            if (player == null)
            {
                // Morte ou saída do mundo: os efeitos morrem junto com o jogador (são filhos do
                // transform dele), mas o estado interno precisa acompanhar.
                //
                // Quem larga os efeitos é o RemoteEffects, desde a etapa 8: eles deixaram de ser
                // do jogador local para serem de cada jogador, e um Reset daqui apagaria também os
                // dos amigos.
                _trackedPlayer = null;
                _state = null;
                IsCharging = false;
                return;
            }

            // Trocou de personagem (ou entrou no mundo agora): recarrega do save.
            if (!ReferenceEquals(player, _trackedPlayer))
            {
                _trackedPlayer = player;
                _state = KiState.Load(player);
                _tickAccumulator = 0f;
                SaiyaheimPlugin.Log.LogInfo(
                    $"Ki loaded: {_state.Current:0.#}/{Max:0.#}, {(_state.Enabled ? "on" : "off")}.");
            }

            HandleInput(player);

            // Tick fixo, não por frame: custo previsível e independente de framerate.
            float interval = SaiyaheimConfig.KiTickInterval.Value;
            _tickAccumulator += dt;
            while (_tickAccumulator >= interval)
            {
                _tickAccumulator -= interval;
                Tick(player, interval);
            }
        }

        private static void HandleInput(Player player)
        {
            // Abrir inventário no meio do carregamento não pode deixar efeito preso na tela.
            if (!AcceptsInput())
            {
                // Abaixar a bandeira basta: o efeito segue o canal, e o canal segue isto.
                IsCharging = false;
                return;
            }

            if (Hotkey.IsDown(SaiyaheimConfig.ToggleKiKey))
            {
                _state.Enabled = !_state.Enabled;
                _state.Save(player);
                SaiyaheimPlugin.Log.LogInfo($"Ki {(_state.Enabled ? "on" : "off")}.");

                // O toggle não tem efeito visual imediato quando o ki está cheio ou vazio; sem
                // aviso na tela dá para apertar a tecla e não saber em que estado ficou.
                player.Message(MessageHud.MessageType.Center, _state.Enabled ? "Ki on" : "Ki off");
            }

            IsCharging = _state.Enabled
                         && Hotkey.IsPressed(SaiyaheimConfig.ChargeKiKey)
                         && CanCharge(player);
        }

        private static bool CanCharge(Player player)
        {
            if (player.IsDead() || player.IsSleeping() || player.IsTeleporting() || player.InCutscene())
            {
                return false;
            }

            if (_state.Current >= Max)
            {
                return false;
            }

            // Carregar parado é o gesto clássico de Dragon Ball, mas atrapalha em combate.
            // Fica em config para descobrir no playtest qual dos dois é mais divertido.
            if (SaiyaheimConfig.ChargeRequiresStandingStill.Value)
            {
                Vector3 velocity = player.GetVelocity();
                velocity.y = 0f;
                if (velocity.sqrMagnitude > 0.25f)
                {
                    return false;
                }
            }

            return true;
        }

        private static void Tick(Player player, float dt)
        {
            if (!_state.Enabled)
            {
                return;
            }

            if (IsCharging)
            {
                // Carregar ignora o delay pós-gasto de propósito: é uma ação deliberada,
                // não a regeneração de fundo.
                Add(ChargePerSecondFor(player) * dt);
            }
            else if (Time.time >= _state.RegenBlockedUntil)
            {
                Add(RegenPerSecondFor(player) * dt);
            }

            _state.Save(player);
        }

        private static void Add(float amount)
        {
            _state.Current = Mathf.Clamp(_state.Current + amount, 0f, Max);
        }

        /// <summary>
        /// Gasta ki se houver o suficiente. Base para transformação, voo e ataques de ki.
        /// Retorna false sem gastar nada se faltar — quem chama decide o que fazer.
        /// </summary>
        internal static bool TryConsume(float amount)
        {
            if (_state == null || !_state.Enabled || _state.Current < amount)
            {
                return false;
            }

            Spend(amount);
            return true;
        }

        /// <summary>
        /// Gasta ki até zerar, sem exigir o valor cheio. É o dreno contínuo —
        /// chegar a zero é o gatilho de destransformação e de queda no voo.
        ///
        /// <b>Bloqueia a regeneração como qualquer outro gasto, e isso é regra, não descuido.</b>
        /// O dreno é cobrado a cada tick, então cada tick reagenda o bloqueio e a regeneração
        /// passiva não corre enquanto houver forma no ar ou voo ativo. Separar manutenção de
        /// combate foi tentado em 2026-08-16 e revertido no mesmo dia: com a regeneração correndo
        /// por baixo, o que a barra sente é o dreno líquido, e segurar a forma e voar ficaram
        /// fáceis demais. Ver Decisões Tomadas, "Manutenção também desliga a regeneração".
        /// </summary>
        internal static void Drain(float amount)
        {
            if (_state == null || !_state.Enabled)
            {
                return;
            }

            Spend(Mathf.Min(amount, _state.Current));
        }

        private static void Spend(float amount)
        {
            _state.Current = Mathf.Max(0f, _state.Current - amount);
            _state.RegenBlockedUntil = Time.time + RegenDelayFor(_trackedPlayer);
        }

        /// <summary>Uso de debug e console. Não bloqueia a regeneração.</summary>
        internal static void SetCurrent(float value)
        {
            if (_state == null)
            {
                return;
            }

            _state.Current = Mathf.Clamp(value, 0f, Max);
            _state.Save(_trackedPlayer);
        }

        /// <summary>
        /// Ignora input quando o jogador está digitando ou com uma janela aberta —
        /// senão apertar a tecla no chat carrega ki sem querer.
        /// </summary>
        private static bool AcceptsInput() => InputGuard.AcceptsInput();
    }
}
