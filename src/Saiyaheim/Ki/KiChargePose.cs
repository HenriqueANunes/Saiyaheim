using System.Collections.Generic;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Ki
{
    /// <summary>
    /// A pose de carregamento de ki: punhos no quadril, cotovelos dobrados, ombros erguidos,
    /// meio agachamento. O power up clássico do gênero, construído em código.
    ///
    /// <b>O que ela substitui.</b> Carregar tocava o emote <c>roar</c>. Ele saiu inteiro em
    /// 2026-08-07, depois do primeiro teste na tela: um grito por cima de uma pose escrita músculo
    /// a músculo é uma animação brigando com a outra, e a mistura ficou pior que qualquer uma das
    /// duas sozinha.
    ///
    /// <b>⚠️ E isso custou o multiplayer, de propósito.</b> Enquanto o emote existia, ele
    /// replicava a bandeira de graça: <c>StartEmote(nome, oneshot: false)</c> escreve na ZDO e o
    /// <c>Player.UpdateEmote</c> copia o nome para o <c>m_emoteState</c> de <b>toda</b> cópia do
    /// jogador, em toda máquina — então qualquer cliente sabia quem estava carregando. Sem o
    /// emote não há canal, e <b>a pose é local</b>: cada um vê a sua.
    ///
    /// O conserto é da etapa 8 e é conhecido: um <c>SE_</c> de carregamento, no molde do
    /// <see cref="SE_Flight"/>, que já sincroniza por ZDO e do qual o <c>FlightPose</c> já
    /// depende para descobrir quem está voando. Não foi feito aqui porque o resto da etapa 2
    /// também é estado local, e adiantar meia sincronização não deixa nada jogável antes.
    ///
    /// <b>Construir a pose por grupos.</b> Cada grupo de músculos tem um <b>peso</b> além do alvo,
    /// porque zero num alvo de músculo <b>não</b> quer dizer "não mexe": <c>Left Arm Down-Up = 0</c>
    /// é T-pose e <c>Left Upper Leg In-Out = 0</c> é pernas juntas. O único jeito honesto de dizer
    /// "deixa como a animação deixou" é não escrever aquele músculo, e é isso que peso zero faz.
    /// A pose se acende um grupo por vez, na calibragem.
    ///
    /// <b>E o tronco são três grupos, não um.</b> Peito, peito alto e lombar têm peso e alvo
    /// separados porque no rig do Valheim eles não fazem a mesma coisa — ver o comentário nas
    /// constantes de músculo. A primeira versão tinha uma inclinação só, repartida entre lombar e
    /// peito por uma proporção fixa no código, e o playtest de 2026-08-07 reprovou na tela: a
    /// proporção obrigava a lombar a entrar toda vez que o peito entrava, e é a lombar que leva o
    /// quadril junto.
    ///
    /// <b>⚠️ Só músculo, nada de <c>bodyRotation</c>.</b> Ao contrário do
    /// <see cref="Flight.FlightPose"/>, esta pose não toca na orientação do corpo. Escrever músculo
    /// é idempotente por construção (interpola para um alvo absoluto), e é isso que a poupa da
    /// armadilha que o <c>PitchForward</c> teve de resolver: o animator avalia em passo de física,
    /// e num frame sem passo o <c>GetHumanPose</c> devolve o que nós mesmos escrevemos. A única
    /// exceção aqui é o <see cref="PoseState.BaseHipY"/>, e ela é absoluta pelo mesmo motivo.
    /// </summary>
    internal sealed class KiChargePose : IPoseContributor
    {
        internal static readonly KiChargePose Instance = new KiChargePose();

        private KiChargePose()
        {
        }

        /// <summary>
        /// Segura a pose sem carregar de verdade, para calibrar os números no
        /// ConfigurationManager sem a barra encher e o carregamento parar sozinho.
        /// Ligado pelo <c>saiya_ki pose</c>; vale só para o jogador local.
        /// </summary>
        internal static bool DebugHold;

        // ---------- Sinais ----------
        //
        // O nome do músculo é "direção negativa - direção positiva" (ver HumanMuscles). As chaves
        // de config são em espaço de **intenção** — positivo é sempre "mais do que o nome diz" —
        // e a tradução para o espaço de músculo mora nestas três constantes. Se alguma coisa sair
        // para o lado contrário na tela, é uma delas que troca de sinal, e não a config do
        // Henrique.

        /// <summary>"Front-Back": +1 é para trás, então inclinar para frente é negativo.</summary>
        private const float LeanSign = -1f;

        /// <summary>"Nod Down-Up": +1 é queixo para cima.</summary>
        private const float HeadTiltSign = -1f;

        /// <summary>"Shoulder Down-Up": +1 é ombro erguido — este não inverte.</summary>
        private const float ShrugSign = 1f;

        /// <summary>Velocidade horizontal (ao quadrado) acima da qual a pose sai do caminho.</summary>
        private const float MovingSpeedSqr = 0.25f;

        /// <summary>Segundos para entregar o corpo à animação de ação, e para retomá-lo.</summary>
        private const float ActionBlendSeconds = 0.12f;

        // Três articulações, e **não** uma "coluna". No humanoide da Unity o tronco é uma escada de
        // três, e no rig do Valheim elas não são intercambiáveis: a lombar arrasta o quadril junto
        // (é ela que o playtest do voo, em 2026-07-31, viu "mexendo as pernas"), o peito dobra o
        // tronco sem levar o quadril, e o peito alto é o mais isolado dos três.
        //
        // Por isso cada uma tem peso e alvo próprios em config, em vez de uma inclinação repartida
        // entre elas por uma proporção fixa no código: a proporção era justamente o que obrigava a
        // lombar a entrar sempre que o peito entrava.
        private const string MuscleSpine = "Spine Front-Back";
        private const string MuscleChest = "Chest Front-Back";
        private const string MuscleUpperChest = "UpperChest Front-Back";
        private const string MuscleNeck = "Neck Nod Down-Up";
        private const string MuscleHead = "Head Nod Down-Up";
        private const string MuscleShrugL = "Left Shoulder Down-Up";
        private const string MuscleShrugR = "Right Shoulder Down-Up";
        private const string MuscleArmSpreadL = "Left Arm Down-Up";
        private const string MuscleArmSpreadR = "Right Arm Down-Up";
        private const string MuscleArmSwingL = "Left Arm Front-Back";
        private const string MuscleArmSwingR = "Right Arm Front-Back";
        private const string MuscleArmTwistL = "Left Arm Twist In-Out";
        private const string MuscleArmTwistR = "Right Arm Twist In-Out";
        private const string MuscleElbowL = "Left Forearm Stretch";
        private const string MuscleElbowR = "Right Forearm Stretch";
        private const string MuscleLegSpreadL = "Left Upper Leg In-Out";
        private const string MuscleLegSpreadR = "Right Upper Leg In-Out";
        private const string MuscleKneeL = "Left Lower Leg Stretch";
        private const string MuscleKneeR = "Right Lower Leg Stretch";

        private static bool _warnedMissing;

        private sealed class PoseState
        {
            /// <summary>0 a 1. Faz a pose entrar e sair sem estalo.</summary>
            internal float Weight;

            /// <summary>Cai a zero durante ataque, defesa e deslocamento. Ver <see cref="ActionTarget"/>.</summary>
            internal float ActionWeight = 1f;

            /// <summary>
            /// Desloca as senóides de tremor por jogador. Sem isto dois amigos carregando lado a
            /// lado tremem em sincronia perfeita, que é a leitura oposta de "esforço".
            /// </summary>
            internal float Phase;

            /// <summary>
            /// Altura do quadril na pose limpa, capturada no frame em que a pose entra.
            ///
            /// ⚠️ <b>Tem de ser capturada uma vez, e não medida todo frame.</b> Depois do primeiro
            /// <c>SetHumanPose</c> a leitura pode devolver o que nós mesmos escrevemos, e subtrair
            /// o agachamento de novo afundaria o personagem no chão um pouco mais a cada frame.
            /// Guardar a base e escrever <c>base − agachamento</c> é absoluto: rodar duas vezes dá
            /// o mesmo resultado que rodar uma.
            /// </summary>
            internal float BaseHipY;

            internal bool HasBaseHipY;
        }

        private static readonly Dictionary<Character, PoseState> States =
            new Dictionary<Character, PoseState>();

        /// <summary>
        /// Verdadeiro enquanto esta pose está escrevendo neste jogador. É o que faz a pose de voo
        /// sair do caminho quando se carrega ki no ar — ver <c>FlightPose.ActionTarget</c>.
        /// </summary>
        internal static bool IsPosing(Player player)
        {
            return player != null
                   && States.TryGetValue(player, out PoseState state)
                   && state.Weight > 0f;
        }

        public float Step(Player player, float deltaTime)
        {
            bool charging = IsCharging(player);

            if (!charging && !States.ContainsKey(player))
            {
                return 0f;
            }

            WarnMissingOnce();

            PoseState state = GetOrCreateState(player);

            state.ActionWeight = Mathf.MoveTowards(
                state.ActionWeight,
                ActionTarget(player),
                StepPerSecond(ActionBlendSeconds) * deltaTime);

            state.Weight = Mathf.MoveTowards(
                state.Weight,
                charging ? 1f : 0f,
                StepPerSecond(SaiyaheimConfig.ChargePoseBlendSeconds.Value) * deltaTime);

            if (state.Weight <= 0f)
            {
                States.Remove(player);
                return 0f;
            }

            // Devolve o peso de entrada, **sem** multiplicar pelo da ação. A diferença não é
            // estética: o que o driver faz com este número é decidir se mantém o handler nativo
            // vivo, e o peso de ação zera a cada soco. Multiplicando aqui, um golpe de meio segundo
            // viraria dezenas de HumanPoseHandler criados e destruídos. Quem consulta o peso de
            // ação é o Apply, que sabe que "não escrever nada neste frame" e "acabei" são coisas
            // diferentes.
            return state.Weight;
        }

        public void Apply(Player player, ref HumanPose pose)
        {
            if (!States.TryGetValue(player, out PoseState state))
            {
                return;
            }

            float[] muscles = pose.muscles;
            float weight = state.Weight * state.ActionWeight;
            if (weight <= 0f)
            {
                return;
            }

            // A base do agachamento tem de ser lida antes de a pose escrever qualquer coisa. Ela é
            // capturada aqui, e não no Step, porque é aqui que a pose do frame existe.
            if (!state.HasBaseHipY)
            {
                state.BaseHipY = pose.bodyPosition.y;
                state.HasBaseHipY = true;
            }

            // ---------- Vida ----------
            //
            // Uma pose parada é um boneco de vitrine, e era esse o risco que [[Melhorias]] apontava
            // nesta ideia: no voo o personagem translada pelo ar, então há movimento na cena mesmo
            // com o corpo parado; carregando no chão não há.
            //
            // Duas senóides, e a divisão de trabalho importa. O **esforço** é lento e de amplitude
            // grande — o corpo inteiro se afunda e volta, como quem respira puxando. O **tremor** é
            // rápido e minúsculo, e é o que vende tensão. Uma sozinha não dá: só a lenta parece
            // respiração de personagem dormindo, só a rápida parece bug de animação.
            float time = Time.time + state.Phase;

            float strain = Mathf.Sin(time * SaiyaheimConfig.ChargePoseStrainSpeed.Value)
                           * SaiyaheimConfig.ChargePoseStrain.Value;

            float tremorAmount = SaiyaheimConfig.ChargePoseTremor.Value;
            float tremorSpeed = SaiyaheimConfig.ChargePoseTremorSpeed.Value;

            // Frequências ligeiramente diferentes entre os lados. Em sincronia o tremor lê como
            // vibração mecânica; fora de fase lê como músculo.
            float tremorL = Mathf.Sin(time * tremorSpeed) * tremorAmount;
            float tremorR = Mathf.Sin(time * tremorSpeed * 1.27f + 1.9f) * tremorAmount;

            // ---------- Tronco, uma articulação de cada vez ----------
            Lean(muscles, MuscleChest,
                SaiyaheimConfig.ChargePoseChestWeight.Value,
                SaiyaheimConfig.ChargePoseChestLean.Value, strain, weight);

            Lean(muscles, MuscleUpperChest,
                SaiyaheimConfig.ChargePoseUpperChestWeight.Value,
                SaiyaheimConfig.ChargePoseUpperChestLean.Value, strain, weight);

            Lean(muscles, MuscleSpine,
                SaiyaheimConfig.ChargePoseSpineWeight.Value,
                SaiyaheimConfig.ChargePoseSpineLean.Value, strain, weight);

            float shoulders = weight * SaiyaheimConfig.ChargePoseShoulderWeight.Value;
            if (shoulders > 0f)
            {
                float shrug = (SaiyaheimConfig.ChargePoseShoulderShrug.Value + strain * 0.5f) * ShrugSign;
                HumanMuscles.Blend(muscles, MuscleShrugL, shrug + tremorL, shoulders);
                HumanMuscles.Blend(muscles, MuscleShrugR, shrug + tremorR, shoulders);
            }

            float head = weight * SaiyaheimConfig.ChargePoseHeadWeight.Value;
            if (head > 0f)
            {
                float tilt = (SaiyaheimConfig.ChargePoseHeadTilt.Value + strain * 0.4f) * HeadTiltSign;
                HumanMuscles.Blend(muscles, MuscleNeck, tilt * 0.6f, head);
                HumanMuscles.Blend(muscles, MuscleHead, tilt * 0.4f, head);
            }

            // ---------- Braços ----------
            float arms = weight * SaiyaheimConfig.ChargePoseArmWeight.Value;
            if (arms > 0f)
            {
                // Alvo absoluto no espaço de músculo, como o HoverArmSpread do voo: 0 é T-pose e
                // ~-0,65 é braço caído ao lado do corpo. Os punhos ficam no quadril, então o braço
                // está quase caído e o que os traz para a frente é o cotovelo.
                float armDown = SaiyaheimConfig.ChargePoseArmDown.Value + strain * 0.2f;
                HumanMuscles.Blend(muscles, MuscleArmSpreadL, armDown, arms);
                HumanMuscles.Blend(muscles, MuscleArmSpreadR, armDown, arms);

                float armBack = SaiyaheimConfig.ChargePoseArmBack.Value;
                HumanMuscles.Blend(muscles, MuscleArmSwingL, armBack + tremorL * 0.5f, arms);
                HumanMuscles.Blend(muscles, MuscleArmSwingR, armBack + tremorR * 0.5f, arms);

                // A rotação do úmero decide para ONDE o cotovelo dobrado aponta o antebraço. Sem
                // ela a dobra sai para um lado qualquer que depende da pose de baixo.
                float armTwist = SaiyaheimConfig.ChargePoseArmTwist.Value;
                HumanMuscles.Blend(muscles, MuscleArmTwistL, armTwist, arms);
                HumanMuscles.Blend(muscles, MuscleArmTwistR, armTwist, arms);

                float elbow = SaiyaheimConfig.ChargePoseElbowBend.Value;
                HumanMuscles.Blend(muscles, MuscleElbowL, elbow + tremorL, arms);
                HumanMuscles.Blend(muscles, MuscleElbowR, elbow + tremorR, arms);
            }

            // Fora do grupo dos braços de propósito: o punho cerrado é o único pedaço da pose que
            // faz sentido sozinho — mão fechada com o braço da animação continua lendo como tensão.
            ApplyFists(muscles, weight);

            // ---------- Pernas ----------
            float legs = weight * SaiyaheimConfig.ChargePoseLegWeight.Value;
            if (legs > 0f)
            {
                float stance = SaiyaheimConfig.ChargePoseStanceWidth.Value;
                HumanMuscles.Blend(muscles, MuscleLegSpreadL, stance, legs);
                HumanMuscles.Blend(muscles, MuscleLegSpreadR, stance, legs);

                float knee = SaiyaheimConfig.ChargePoseKneeBend.Value + strain * 0.5f;
                HumanMuscles.Blend(muscles, MuscleKneeL, knee + tremorL * 0.5f, legs);
                HumanMuscles.Blend(muscles, MuscleKneeR, knee + tremorR * 0.5f, legs);

                ApplyCrouch(ref pose, state, knee, strain, legs);
            }
        }

        public void Forget(Character character)
        {
            States.Remove(character);
        }

        /// <summary>
        /// Inclina uma articulação do tronco para frente. Peso zero não escreve nada — que é
        /// diferente de alvo zero, e é a distinção inteira dos pesos por grupo.
        ///
        /// O esforço é somado ao alvo e não multiplicado por ele, para uma articulação parada em
        /// zero ainda respirar se o jogador quiser só a respiração.
        /// </summary>
        private static void Lean(
            float[] muscles, string muscle, float groupWeight, float target, float strain, float weight)
        {
            float w = weight * groupWeight;
            if (w <= 0f)
            {
                return;
            }

            HumanMuscles.Blend(muscles, muscle, (target + strain) * LeanSign, w);
        }

        /// <summary>
        /// Baixa o quadril junto com o joelho dobrado.
        ///
        /// <b>Por que é obrigatório, e não enfeite.</b> O foot IK do jogo roda em
        /// <c>CharacterAnimEvent.OnAnimatorIK</c>, que é a passada de IK do animator — ou seja
        /// <b>antes</b> do <see cref="PoseDriver"/>. Ele não corrige o que escrevemos depois.
        /// Então dobrar o joelho com o quadril na mesma altura não agacha o personagem: **levanta
        /// os pés do chão**, porque a perna encurta e a pelve não desce para compensar.
        ///
        /// O quanto descer é geometria, não gosto — mas o rig é o que é, então a proporção fica em
        /// config e a calibragem é na tela. <c>ChargePoseHipDrop</c> em zero devolve o
        /// comportamento sem agachamento, e é a saída se o personagem afundar no chão.
        ///
        /// A unidade é a do <c>bodyPosition</c> do humanoide: escala do avatar, que para um rig do
        /// tamanho do jogador dá aproximadamente metro.
        /// </summary>
        private static void ApplyCrouch(
            ref HumanPose pose, PoseState state, float knee, float strain, float weight)
        {
            float drop = SaiyaheimConfig.ChargePoseHipDrop.Value;
            if (drop <= 0f || !state.HasBaseHipY)
            {
                return;
            }

            // Proporcional ao joelho de fato dobrado — inclusive ao pedaço que veio do esforço, para
            // o corpo subir e descer junto em vez de a pelve ficar parada enquanto a perna pulsa.
            float amount = drop * Mathf.Max(0f, knee + strain * 0.5f) * weight;

            Vector3 body = pose.bodyPosition;
            body.y = state.BaseHipY - amount;
            pose.bodyPosition = body;
        }

        /// <summary>
        /// Fecha as duas mãos.
        ///
        /// Punho cerrado é metade do gesto — "braços para baixo, punhos fechados, corpo tenso" é a
        /// descrição inteira que o vault dá da pose. E é barato: os dedos são músculos como
        /// qualquer outro, então não há nada de novo aqui além de trinta nomes.
        ///
        /// Os índices são resolvidos uma vez: são 40 músculos por frame, e resolver por nome nos
        /// 40 seria a única parte cara desta pose.
        /// </summary>
        private static void ApplyFists(float[] muscles, float weight)
        {
            if (_fistIndex == null)
            {
                BuildFists();
            }

            float clench = Mathf.Clamp01(SaiyaheimConfig.ChargePoseFistClench.Value);
            if (clench <= 0f)
            {
                return;
            }

            for (int i = 0; i < _fistIndex.Length; i++)
            {
                HumanMuscles.Blend(muscles, _fistIndex[i], _fistTarget[i], clench * weight);
            }
        }

        private static int[] _fistIndex;
        private static float[] _fistTarget;

        private static void BuildFists()
        {
            List<int> indices = new List<int>();
            List<float> targets = new List<float>();

            foreach (string side in new[] { "Left", "Right" })
            {
                foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                {
                    // O polegar fecha por cima dos outros dedos e curva bem menos que eles; no
                    // valor cheio ele atravessa a mão.
                    float curl = finger == "Thumb" ? -0.5f : -1f;

                    for (int joint = 1; joint <= 3; joint++)
                    {
                        Add($"{side} {finger} {joint} Stretched", curl);
                    }

                    // Dedos juntos: punho é mão fechada, não mão em garra.
                    Add($"{side} {finger} Spread", 0f);
                }
            }

            _fistIndex = indices.ToArray();
            _fistTarget = targets.ToArray();

            void Add(string name, float target)
            {
                int index = HumanMuscles.IndexOf(name);
                if (index < 0)
                {
                    return;
                }

                indices.Add(index);
                targets.Add(target);
            }
        }

        /// <summary>
        /// Quem está carregando, na perspectiva desta máquina — e hoje a resposta só pode ser o
        /// jogador local.
        ///
        /// O <see cref="KiManager"/> é estado local (etapa 2), e desde que o emote saiu não há
        /// canal que conte a um cliente que <i>outro</i> jogador está carregando. Ver o aviso de
        /// multiplayer no topo da classe: o conserto é um <c>SE_</c>, e é da etapa 8.
        ///
        /// O driver continua rodando em todo <c>Player</c> — é aqui que os outros são recusados,
        /// e é aqui que a linha some quando a sincronização existir.
        /// </summary>
        private static bool IsCharging(Player player)
        {
            if (!SaiyaheimConfig.ChargePoseEnabled.Value)
            {
                return false;
            }

            if (player != Player.m_localPlayer)
            {
                return false;
            }

            return DebugHold || KiManager.IsCharging;
        }

        /// <summary>
        /// Zero enquanto o jogador faz outra coisa com o corpo; um no resto do tempo.
        ///
        /// Mesma razão do <c>FlightPose.ActionTarget</c>: a pose reescreve os músculos todo frame,
        /// depois de o animator ter rodado, então um golpe tocaria e seria apagado antes de
        /// aparecer.
        ///
        /// <b><c>InEmote</c> fica fora desta lista</b>, ao contrário da pose de voo — aqui o emote
        /// é justamente o que sinaliza o carregamento, e incluí-lo desligaria a pose sempre.
        ///
        /// <b>Deslocamento entra.</b> Com <c>ChargeRequiresStandingStill</c> ligado andar já
        /// interrompe o carregamento e isto nunca dispara; desligado, é o que evita o personagem
        /// correndo em posição de power up.
        /// </summary>
        private static float ActionTarget(Player player)
        {
            if (player.InAttack()
                || player.IsBlocking()
                || player.InMinorAction()
                || player.InDodge())
            {
                return 0f;
            }

            Vector3 velocity = player.GetVelocity();
            velocity.y = 0f;

            return velocity.sqrMagnitude > MovingSpeedSqr ? 0f : 1f;
        }

        private static PoseState GetOrCreateState(Player player)
        {
            if (States.TryGetValue(player, out PoseState existing))
            {
                return existing;
            }

            PoseState state = new PoseState
            {
                // Determinístico por jogador: dois personagens diferentes tremem fora de fase, e o
                // mesmo personagem treme igual entre um carregamento e o seguinte.
                Phase = Mathf.Abs(player.GetInstanceID() % 1000) * 0.01f,
            };

            States[player] = state;
            return state;
        }

        /// <summary>Blend em segundos vira passo por segundo; 0 vira "instantâneo".</summary>
        private static float StepPerSecond(float blendSeconds) =>
            blendSeconds <= 0f ? float.PositiveInfinity : 1f / blendSeconds;

        private static void WarnMissingOnce()
        {
            if (_warnedMissing)
            {
                return;
            }

            _warnedMissing = true;

            HumanMuscles.WarnMissing(
                "The ki charging pose",
                MuscleSpine, MuscleChest, MuscleUpperChest, MuscleNeck, MuscleHead,
                MuscleShrugL, MuscleShrugR, MuscleArmSpreadL, MuscleArmSpreadR,
                MuscleArmSwingL, MuscleArmSwingR, MuscleArmTwistL, MuscleArmTwistR,
                MuscleElbowL, MuscleElbowR, MuscleLegSpreadL, MuscleLegSpreadR,
                MuscleKneeL, MuscleKneeR);
        }
    }
}
