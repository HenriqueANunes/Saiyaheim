using System.Collections.Generic;
using Saiyaheim.Net;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Attacks
{
    /// <summary>
    /// A pose de carga do Kienzan: braço direito esticado para cima, palma aberta virada para o
    /// céu, com o disco girando sobre ela. É o gesto do Kuririn.
    ///
    /// <b>Só a carga.</b> O arremesso é a pose de disparo do ki blast (<see cref="KiBlastPose"/>),
    /// que o mesmo contador de disparo levanta: braço à frente e palma aberta, que é o fim de um
    /// arremesso. Escrever um segundo gesto para isso seria mais uma pose a calibrar para o mesmo
    /// meio segundo.
    ///
    /// <b>Estado, como a recarga e o voo</b>, e não instante: escreve enquanto a ZDO disser que o
    /// jogador carrega um ataque de <see cref="ChargePose.OverheadDisc"/>, e sai quando parar. Por
    /// ler a ZDO, vale para todo jogador carregado, o local inclusive — mesma regra de todas as
    /// outras poses: uma fonte de verdade só.
    ///
    /// <b>No <see cref="PoseDriver"/>, antes do disparo.</b> Os dois disputam o braço direito, e no
    /// instante da soltura esta desce enquanto a do disparo sobe: escrevendo depois, o disparo ganha
    /// e o braço vai de cima para a frente sem passar pela pose neutra.
    ///
    /// <b>Os números são constantes</b> em <c>SaiyaheimConfig.KienzanPose</c>. Moraram no
    /// <c>.cfg</c> enquanto eram chute e fecharam na tela em 2026-09-28, como as três poses
    /// anteriores em 2026-09-13. Ajustar exige recompilar.
    /// </summary>
    internal sealed class KiDiscPose : IPoseContributor
    {
        internal static readonly KiDiscPose Instance = new KiDiscPose();

        private KiDiscPose()
        {
        }

        /// <summary>
        /// Segura a pose indefinidamente, para olhar de perto. Ligado pelo
        /// <c>saiya_blast pose disc</c>; vale só para o jogador local.
        /// </summary>
        internal static bool DebugHold;

        /// <summary>"Front-Back": +1 é para trás, então levar para frente é negativo.</summary>
        private const float ForwardSign = -1f;

        /// <summary>Segundos para entregar o corpo à animação de um golpe, e para retomá-lo.</summary>
        private const float ActionBlendSeconds = 0.12f;

        private const string MuscleArmSpread = "Right Arm Down-Up";
        private const string MuscleArmSwing = "Right Arm Front-Back";
        private const string MuscleArmTwist = "Right Arm Twist In-Out";
        private const string MuscleElbow = "Right Forearm Stretch";
        private const string MuscleShoulderUp = "Right Shoulder Down-Up";
        private const string MuscleWrist = "Right Hand Down-Up";
        private const string MuscleForearmTwist = "Right Forearm Twist In-Out";
        private const string MuscleShoulderSwing = "Right Shoulder Front-Back";
        private const string MuscleWristSide = "Right Hand In-Out";

        private static bool _warnedMissing;

        private sealed class PoseState
        {
            /// <summary>0 a 1: quanto do braço esta pose é dona.</summary>
            internal float Weight;

            /// <summary>Zero enquanto o jogador faz outra coisa com o corpo. Ver <see cref="ActionTarget"/>.</summary>
            internal float ActionWeight = 1f;
        }

        private static readonly Dictionary<Character, PoseState> States =
            new Dictionary<Character, PoseState>();

        public float Step(Player player, float deltaTime)
        {
            bool up = IsUp(player);

            States.TryGetValue(player, out PoseState state);

            if (state == null)
            {
                // O caminho comum e barato: ninguém carrega um Kienzan.
                if (!up)
                {
                    return 0f;
                }

                state = new PoseState();
                States[player] = state;
                WarnMissingOnce();
            }

            SaiyaheimConfig.OverheadPoseConfig config = Config();
            float blend = config == null ? 0.25f : config.BlendSeconds;

            state.Weight = Mathf.MoveTowards(
                state.Weight, up ? 1f : 0f, StepPerSecond(blend) * deltaTime);

            state.ActionWeight = Mathf.MoveTowards(
                state.ActionWeight, ActionTarget(player),
                StepPerSecond(ActionBlendSeconds) * deltaTime);

            if (!up && state.Weight <= 0f)
            {
                States.Remove(player);
                return 0f;
            }

            return state.Weight;
        }

        public void Apply(Player player, ref HumanPose pose)
        {
            SaiyaheimConfig.OverheadPoseConfig config = Config();

            if (config == null || !States.TryGetValue(player, out PoseState state))
            {
                return;
            }

            float weight = state.Weight * state.ActionWeight;
            if (weight <= 0f)
            {
                return;
            }

            float[] muscles = pose.muscles;

            // Alvos absolutos no espaço de músculo, como nas outras poses: 0 na altura é a T-pose,
            // e 1 é o braço erguido até onde o rig deixa.
            HumanMuscles.Blend(muscles, MuscleArmSpread, config.ArmHeight, weight);
            HumanMuscles.Blend(muscles, MuscleArmSwing, config.ArmForward * ForwardSign, weight);
            HumanMuscles.Blend(muscles, MuscleArmTwist, config.ArmTwist, weight);

            // +1 é o braço reto; 0 é o MEIO da faixa, um braço dobrado. Ver KiBlastPose.
            HumanMuscles.Blend(muscles, MuscleElbow, config.ElbowStretch, weight);
            HumanMuscles.Blend(muscles, MuscleShoulderUp, config.ShoulderLift, weight);

            // ⚠️ Toda articulação do braço direito recebe alvo, inclusive as que o gesto não pede.
            // Playtest de 2026-09-28: com PoseArmTwist fora de zero, a mão alternava entre duas
            // posições. O animator só avalia em passo de física, então nos frames sem passo o
            // GetHumanPose relê a pose que o mod mesmo escreveu. Músculo com alvo absoluto sai
            // igual nos dois casos; músculo deixado em paz vem uma vez da animação e outra da
            // releitura — e com o braço girado a Unity redistribui a torção entre braço e
            // antebraço, então as duas leituras não batem. Pinar tudo tira a segunda fonte.
            HumanMuscles.Blend(muscles, MuscleForearmTwist, config.ForearmTwist, weight);

            // Neutros e não chaves: só estão aqui para a releitura não ter o que mudar.
            HumanMuscles.Blend(muscles, MuscleShoulderSwing, 0f, weight);
            HumanMuscles.Blend(muscles, MuscleWristSide, 0f, weight);

            ApplyOpenPalm(muscles, weight * Mathf.Clamp01(config.HandOpen));

            // O pulso fora da mão aberta, ao contrário do disparo: lá o pulso e os dedos são um
            // gesto só, aqui é o pulso que vira a palma para o céu, e é ele que se calibra.
            HumanMuscles.Blend(muscles, MuscleWrist, config.WristBend, weight);
        }

        public void Forget(Character character)
        {
            States.Remove(character);
        }

        /// <summary>A pose deve estar levantada agora?</summary>
        private static bool IsUp(Player player)
        {
            if (DebugHold && player == Player.m_localPlayer)
            {
                return true;
            }

            if (!NetState.IsChargingBeam(player))
            {
                return false;
            }

            KiAttack attack = KiAttackRegistry.Charging(player);

            return attack != null && attack.Config.ChargePose == ChargePose.OverheadDisc;
        }

        /// <summary>
        /// A config da pose: a do primeiro ataque que a usa. Hoje é só o Kienzan; um segundo ataque
        /// de braço erguido herdaria esta, e só então valeria a pena ler por jogador.
        /// </summary>
        private static SaiyaheimConfig.OverheadPoseConfig Config()
        {
            foreach (KiAttack attack in KiAttackRegistry.All)
            {
                if (attack.Config.OverheadPose != null)
                {
                    return attack.Config.OverheadPose;
                }
            }

            return null;
        }

        /// <summary>
        /// Zero durante golpe, bloqueio, esquiva e ação curta: a pose reescreve os músculos depois
        /// do animator, e um soco tocaria e seria apagado antes de aparecer. Mesma regra do
        /// Kamehameha.
        /// </summary>
        private static float ActionTarget(Player player)
        {
            return player.InAttack() || player.IsBlocking() || player.InMinorAction()
                   || player.InDodge()
                ? 0f
                : 1f;
        }

        /// <summary>
        /// Abre a mão direita, dedos um pouco separados. Os mesmos alvos da mão do disparo — ver
        /// <c>KiBlastPose.ApplyOpenPalm</c> —, resolvidos uma vez só.
        /// </summary>
        private static void ApplyOpenPalm(float[] muscles, float weight)
        {
            if (weight <= 0f)
            {
                return;
            }

            if (_palmIndex == null)
            {
                BuildPalm();
            }

            for (int i = 0; i < _palmIndex.Length; i++)
            {
                HumanMuscles.Blend(muscles, _palmIndex[i], _palmTarget[i], weight);
            }
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
                "The Kienzan pose",
                MuscleArmSpread, MuscleArmSwing, MuscleArmTwist, MuscleElbow,
                MuscleShoulderUp, MuscleWrist, MuscleForearmTwist, MuscleShoulderSwing,
                MuscleWristSide);
        }
    }
}
