using System.Collections.Generic;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Attacks
{
    /// <summary>
    /// A pose de disparo do ki blast: braço direito estendido para frente, cotovelo esticado, o
    /// tronco acompanhando com uma torção. Nasce quando o tiro sai, segura um instante e volta.
    ///
    /// <b>É a terceira pose, e a primeira que é um instante.</b> O voo pergunta "este jogador está
    /// voando?" e a recarga pergunta "está carregando?" — as duas escrevem enquanto a resposta for
    /// sim. Disparar não tem "enquanto": não há estado a consultar, há um evento que aconteceu.
    /// Daí a única peça de fato nova aqui, o <see cref="Trigger"/> e o
    /// <see cref="PoseState.HoldUntil"/>: um carimbo de tempo no futuro, e o peso subindo enquanto
    /// o relógio não passa dele.
    ///
    /// O envelope é deliberadamente assimétrico — <c>Rise</c> curto, <c>Fall</c> longo. Subir
    /// devagar transformaria o gesto num alongamento; descer rápido faria o braço voltar como se
    /// tivesse sido cortado. Sobe estalando, volta relaxando.
    ///
    /// <b>Sem oscilação e sem tremor</b>, ao contrário da recarga, e por um motivo e não por
    /// preguiça: as senóides existem lá porque a recarga <i>dura</i>, e uma pose parada por dez
    /// segundos é um boneco de vitrine. Esta dura meio segundo. É a mesma lição do
    /// <c>ForceLoop</c> da aura — exagerar num efeito curto o transforma em outra coisa.
    ///
    /// <b>Ordem no <see cref="PoseDriver"/>: por último, de propósito.</b> Atirar voando e atirar
    /// carregando são situações normais, e nas duas o braço direito é disputado. Quem escreve
    /// depois ganha, e o disparo é o gesto que o jogador acabou de pedir. As outras duas continuam
    /// donas de tudo que esta não toca — as pernas do voo, o braço esquerdo da recarga —, que é
    /// justamente o que um único driver com uma pose lida e devolvida uma vez permite.
    ///
    /// <b>⚠️ E aqui o multiplayer não vem de graça</b>, ao contrário do voo. Lá o <c>SE_Flight</c>
    /// sincroniza por ZDO e cada cliente descobre sozinho quem está voando. Um disparo não tem
    /// status effect onde pendurar a bandeira, então <b>a pose é local</b>: cada um vê o próprio
    /// braço esticar. O conserto é da etapa 8 e o caminho barato já está desenhado em
    /// [[Melhorias#Pose procedural de disparo do ki blast]] — o projétil já replica, então um
    /// cliente que vê nascer uma bola de ki com aquele dono pode chamar o <see cref="Trigger"/>
    /// daquele jogador. Não foi feito agora porque a pose de recarga tem o mesmo furo e os dois
    /// entram juntos na etapa 8 — mas <b>não pelo mesmo mecanismo</b>: carregar é estado e um
    /// <c>SE_</c> responde bem a "quem está carregando agora?"; disparar é instante, e um
    /// <c>SE_</c> de 200 ms por tiro seria status effect usado como variável.
    ///
    /// <b>Não tem <c>ActionTarget</c></b>, ao contrário das outras duas. Elas precisam sair do
    /// caminho porque um estado que dura pisa em cima de qualquer golpe que o jogador tente no
    /// meio; aqui a pose <i>é</i> a reação ao golpe, e sair do caminho de si mesma não quer dizer
    /// nada. Se no playtest atirar durante um soco ficar estranho, é aqui que a checagem entra.
    /// </summary>
    internal sealed class KiBlastPose : IPoseContributor
    {
        internal static readonly KiBlastPose Instance = new KiBlastPose();

        private KiBlastPose()
        {
        }

        /// <summary>
        /// Segura a pose indefinidamente, para calibrar os números no ConfigurationManager.
        ///
        /// Não é conforto: uma pose de meio segundo é <b>impossível</b> de ajustar sem isto — o
        /// tempo entre mexer no slider e olhar o personagem já é maior que a pose inteira. Ligado
        /// pelo <c>saiya_blast pose</c>; vale só para o jogador local.
        /// </summary>
        internal static bool DebugHold;

        // ---------- Sinais ----------
        //
        // O nome do músculo é "direção negativa - direção positiva" (ver HumanMuscles). As chaves
        // de config são em espaço de **intenção** — positivo é sempre "mais do que o nome diz" —
        // e a tradução para o espaço de músculo mora nestas constantes. Se alguma coisa sair para
        // o lado contrário na tela, é uma delas que troca de sinal, e não a config do Henrique.

        /// <summary>"Front-Back": +1 é para trás, então estender para frente é negativo.</summary>
        private const float ForwardSign = -1f;

        /// <summary>
        /// "Twist Left-Right": +1 é girar para a direita. Trazer o ombro <i>direito</i> à frente
        /// é girar o tronco para a esquerda, então a intenção positiva é negativa no músculo.
        /// </summary>
        private const float TwistSign = -1f;

        // Só o lado direito. O braço esquerdo fica de fora de propósito: o pedido é "apontar a mão
        // direita para frente", e cada grupo a mais aqui é um grupo a mais para calibrar na tela.
        // Se o disparo pedir a mão esquerda recolhida no quadril depois, é um grupo novo — não uma
        // reescrita.
        private const string MuscleArmSpread = "Right Arm Down-Up";
        private const string MuscleArmSwing = "Right Arm Front-Back";
        private const string MuscleArmTwist = "Right Arm Twist In-Out";
        private const string MuscleElbow = "Right Forearm Stretch";
        private const string MuscleShoulderUp = "Right Shoulder Down-Up";
        private const string MuscleShoulderSwing = "Right Shoulder Front-Back";
        private const string MuscleWrist = "Right Hand Down-Up";
        private const string MuscleSpineTwist = "Spine Twist Left-Right";
        private const string MuscleChestTwist = "Chest Twist Left-Right";
        private const string MuscleUpperChestTwist = "UpperChest Twist Left-Right";

        private static bool _warnedMissing;

        private sealed class PoseState
        {
            /// <summary>0 a 1. Sobe pelo <c>Rise</c> e desce pelo <c>Fall</c>.</summary>
            internal float Weight;

            /// <summary>
            /// Instante (<c>Time.time</c>) até o qual a pose fica levantada. É a peça que
            /// transforma um evento em algo que o driver, que só sabe perguntar "e agora?", possa
            /// consultar todo frame.
            ///
            /// Atirar de novo antes do fim só empurra este número para a frente — o peso não
            /// reinicia, então uma rajada não faz o braço piscar entre um tiro e o seguinte.
            /// </summary>
            internal float HoldUntil;
        }

        private static readonly Dictionary<Character, PoseState> States =
            new Dictionary<Character, PoseState>();

        /// <summary>
        /// O tiro saiu: levanta a pose.
        ///
        /// Chamado pelo <see cref="KiAttackManager"/> <b>depois</b> de o projétil existir de fato,
        /// e não quando a tecla é apertada. A diferença aparece na tela: um prefab errado no
        /// <c>.cfg</c>, ou a barra vazia, fariam o braço esticar sem nada sair da mão.
        /// </summary>
        internal static void Trigger(Player player)
        {
            if (player == null || !SaiyaheimConfig.BlastPoseEnabled.Value)
            {
                return;
            }

            GetOrCreateState(player).HoldUntil =
                Time.time + Mathf.Max(0f, SaiyaheimConfig.BlastPoseHoldSeconds.Value);
        }

        public float Step(Player player, float deltaTime)
        {
            States.TryGetValue(player, out PoseState state);

            bool up = IsUp(player, state);

            if (state == null)
            {
                // O caminho comum, e o barato: ninguém atirou, não há nada para lembrar.
                if (!up)
                {
                    return 0f;
                }

                state = GetOrCreateState(player);
            }

            WarnMissingOnce();

            float blend = up
                ? SaiyaheimConfig.BlastPoseRiseSeconds.Value
                : SaiyaheimConfig.BlastPoseFallSeconds.Value;

            state.Weight = Mathf.MoveTowards(
                state.Weight, up ? 1f : 0f, StepPerSecond(blend) * deltaTime);

            if (!up && state.Weight <= 0f)
            {
                States.Remove(player);
                return 0f;
            }

            return state.Weight;
        }

        public void Apply(Player player, ref HumanPose pose)
        {
            if (!States.TryGetValue(player, out PoseState state) || state.Weight <= 0f)
            {
                return;
            }

            float[] muscles = pose.muscles;
            float weight = state.Weight;

            // ---------- O braço ----------
            float arm = weight * SaiyaheimConfig.BlastPoseArmWeight.Value;
            if (arm > 0f)
            {
                // Altura do ombro é alvo ABSOLUTO no espaço de músculo, como o ArmDown da recarga e
                // o HoverArmSpread do voo: 0 é T-pose, ou seja braço na horizontal — que é
                // exatamente a altura de quem aponta para frente. Não há nome de intenção honesto
                // para "onde fica o braço".
                float height = SaiyaheimConfig.BlastPoseArmHeight.Value + AimOffset(player);
                HumanMuscles.Blend(muscles, MuscleArmSpread, Mathf.Clamp(height, -1f, 1f), arm);

                // A que faz o gesto. Da T-pose, girar o braço para frente é o que o aponta para
                // onde o jogador olha.
                HumanMuscles.Blend(muscles, MuscleArmSwing,
                    SaiyaheimConfig.BlastPoseArmForward.Value * ForwardSign, arm);

                // A rotação do úmero decide para onde a palma aponta. Com o cotovelo esticado ela
                // quase não muda a silhueta — mas é ela que separa "mão espalmada para frente" de
                // "mão de lado", e a bola nasce na mão.
                HumanMuscles.Blend(muscles, MuscleArmTwist,
                    SaiyaheimConfig.BlastPoseArmTwist.Value, arm);

                // Alvo absoluto: 0 é braço reto, positivo dobra. O pedido é cotovelo esticado, e o
                // default é o que a config diz.
                HumanMuscles.Blend(muscles, MuscleElbow,
                    SaiyaheimConfig.BlastPoseElbowBend.Value, arm);
            }

            // ---------- O ombro ----------
            //
            // Grupo separado do braço, e não enfeite: o ombro é o que transforma "braço levantado"
            // em "braço estendido". Sem ele o alcance do gesto para no encaixe do úmero, e o
            // personagem parece apontar em vez de empurrar.
            float shoulder = weight * SaiyaheimConfig.BlastPoseShoulderWeight.Value;
            if (shoulder > 0f)
            {
                HumanMuscles.Blend(muscles, MuscleShoulderSwing,
                    SaiyaheimConfig.BlastPoseShoulderPush.Value * ForwardSign, shoulder);
                HumanMuscles.Blend(muscles, MuscleShoulderUp,
                    SaiyaheimConfig.BlastPoseShoulderLift.Value, shoulder);
            }

            // ---------- O tronco ----------
            //
            // Três articulações e não uma "coluna", pela mesma razão da recarga: no rig do Valheim
            // a lombar arrasta o quadril e as outras duas não. Aqui elas dividem UM alvo em vez de
            // ter um cada — a torção é um gesto só, distribuído — mas os pesos continuam
            // separados, e é por isso que a lombar pode ficar fora.
            float torso = weight * SaiyaheimConfig.BlastPoseTorsoWeight.Value;
            if (torso > 0f)
            {
                float twist = SaiyaheimConfig.BlastPoseTorsoTwist.Value * TwistSign;

                // Escalonado de baixo para cima: a lombar mal se mexe, o peito alto leva o ombro.
                // Torcer as três igualmente aponta o quadril para o lado junto, e aí o personagem
                // deixa de encarar para onde está mirando.
                HumanMuscles.Blend(muscles, MuscleSpineTwist, twist * 0.25f,
                    torso * SaiyaheimConfig.BlastPoseSpineTwistWeight.Value);
                HumanMuscles.Blend(muscles, MuscleChestTwist, twist * 0.75f, torso);
                HumanMuscles.Blend(muscles, MuscleUpperChestTwist, twist, torso);
            }

            // ---------- A mão ----------
            ApplyOpenPalm(muscles, weight);
        }

        public void Forget(Character character)
        {
            States.Remove(character);
        }

        /// <summary>
        /// Quanto o braço sobe ou desce para acompanhar a mira.
        ///
        /// Existe porque a bola nasce <b>na mão</b> (<c>KiProjectile.GetOrigin</c>) e voa na
        /// direção do olhar. Com o braço travado na horizontal, mirar no céu produz um tiro saindo
        /// da mão para cima com o braço apontando para o horizonte — e é o tipo de erro que só
        /// aparece quando alguém atira num Draugr numa torre.
        /// </summary>
        private static float AimOffset(Player player)
        {
            float follow = SaiyaheimConfig.BlastPoseAimFollow.Value;
            if (follow <= 0f)
            {
                return 0f;
            }

            // A componente vertical do olhar já é o seno do ângulo de mira, em [-1, 1]. Não precisa
            // virar grau: o espaço de músculo também é normalizado, e o que se quer aqui é
            // proporção e não ângulo exato.
            return player.GetLookDir().normalized.y * follow;
        }

        /// <summary>
        /// Abre a mão direita.
        ///
        /// O oposto exato do <c>KiChargePose.ApplyFists</c>, e o contraste é o ponto: carregar é
        /// punho fechado, disparar é palma aberta. Alvo 0 em todos os dedos é a mão neutra do
        /// humanoide — plana —, então "abrir" aqui é só puxar os dedos para o neutro com um peso.
        ///
        /// Fora do grupo do braço pela mesma razão que o punho está fora lá: uma palma aberta lê
        /// como disparo mesmo com o braço que a animação já tinha.
        ///
        /// Os índices são resolvidos uma vez — são 15 músculos, e resolver por nome todo frame
        /// seria a única parte cara desta pose.
        /// </summary>
        private static void ApplyOpenPalm(float[] muscles, float weight)
        {
            float open = Mathf.Clamp01(SaiyaheimConfig.BlastPoseHandOpen.Value) * weight;
            if (open <= 0f)
            {
                return;
            }

            if (_palmIndex == null)
            {
                BuildPalm();
            }

            for (int i = 0; i < _palmIndex.Length; i++)
            {
                HumanMuscles.Blend(muscles, _palmIndex[i], _palmTarget[i], open);
            }

            // O pulso vai junto do resto da mão: palma empurrada para frente é pulso estendido, e
            // separá-lo daria uma chave a mais para calibrar um gesto só.
            HumanMuscles.Blend(muscles, MuscleWrist, SaiyaheimConfig.BlastPoseWristBend.Value, open);
        }

        private static int[] _palmIndex;
        private static float[] _palmTarget;

        private static void BuildPalm()
        {
            List<int> indices = new List<int>();
            List<float> targets = new List<float>();

            foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
            {
                for (int joint = 1; joint <= 3; joint++)
                {
                    Add($"Right {finger} {joint} Stretched", 0f);
                }

                // Dedos ligeiramente separados. Mão aberta com os dedos colados lê como um golpe de
                // caratê, que é outro gesto.
                Add($"Right {finger} Spread", 0.3f);
            }

            _palmIndex = indices.ToArray();
            _palmTarget = targets.ToArray();

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

        /// <summary>A pose deve estar levantada agora?</summary>
        private static bool IsUp(Player player, PoseState state)
        {
            if (!SaiyaheimConfig.BlastPoseEnabled.Value)
            {
                return false;
            }

            if (DebugHold && player == Player.m_localPlayer)
            {
                return true;
            }

            return state != null && Time.time < state.HoldUntil;
        }

        private static PoseState GetOrCreateState(Player player)
        {
            if (States.TryGetValue(player, out PoseState existing))
            {
                return existing;
            }

            PoseState state = new PoseState();
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
                "The ki blast pose",
                MuscleArmSpread, MuscleArmSwing, MuscleArmTwist, MuscleElbow,
                MuscleShoulderUp, MuscleShoulderSwing, MuscleWrist,
                MuscleSpineTwist, MuscleChestTwist, MuscleUpperChestTwist);
        }
    }
}
