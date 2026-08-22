using System.Collections.Generic;
using Saiyaheim.Net;
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
    /// <b>✅ O multiplayer entrou na etapa 8, e o mecanismo previsto não foi o usado.</b> A ideia
    /// era o próprio projétil ser o sinal: ele replica, então um cliente que visse nascer uma bola
    /// de ki com aquele dono chamaria a pose. Caiu por duas razões — o <c>Setup</c> só roda em quem
    /// atirou, e distinguir a nossa bola da do Dvergr do outro lado do fio exigiria marcar a ZDO do
    /// projétil. Como o carregamento já ia precisar de um canal de estado, um <b>contador</b> no
    /// mesmo inteiro custou um campo e resolveu os dois casos.
    ///
    /// E resolveu justamente o que o desenho antigo temia: o disparo é instante, não estado, e um
    /// <c>SE_</c> de 200 ms por tiro seria status effect usado como variável. Contador não tem esse
    /// problema — ele nem sabe quanto tempo a pose dura. Ver <see cref="NetState"/>.
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
        /// Último contador de disparo visto em cada jogador. É a memória que transforma o número
        /// que só sobe do <see cref="NetState"/> num evento.
        ///
        /// <b>Separado do <see cref="States"/> de propósito</b>: aquele é descartado quando a pose
        /// termina de descer, e este precisa sobreviver ao intervalo entre dois tiros. Fundir os
        /// dois faria cada tiro parecer o primeiro.
        ///
        /// <b>Ausente quer dizer "nunca vi este jogador"</b>, e é o que impede um falso disparo
        /// quando alguém entra no alcance já com o contador em 7: anota-se o 7 e espera-se o 8.
        /// </summary>
        private static readonly Dictionary<Character, int> LastSeenBlast =
            new Dictionary<Character, int>();

        /// <summary>
        /// O tiro saiu: levanta a pose.
        ///
        /// <b>Ninguém chama isto diretamente desde a etapa 8</b>, nem quem atirou. O disparo é
        /// anunciado por <c>NetState.PublishBlast</c> e é o <see cref="ObserveBlast"/> que traz o
        /// anúncio de volta para cá — em toda máquina, inclusive na de quem apertou a tecla. O
        /// caminho curto existia e foi removido pelo mesmo motivo que o do carregamento: duas
        /// fontes para o mesmo evento escondem a quebra do canal justamente de quem poderia vê-la.
        /// </summary>
        private static void Trigger(Player player)
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
            ObserveBlast(player);

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

            // Para onde a mira aponta, na horizontal, e quem paga por ela. O tronco leva a fatia
            // que o braço não leva — nunca as duas coisas, senão o gesto gira duas vezes.
            float aimYaw = AimYaw(player);
            float yawToTorso = Mathf.Clamp01(SaiyaheimConfig.BlastPoseAimYawTorsoShare.Value);

            // ---------- O braço ----------
            float arm = weight * SaiyaheimConfig.BlastPoseArmWeight.Value;
            if (arm > 0f)
            {
                // Altura do ombro é alvo ABSOLUTO no espaço de músculo, como o ArmDown da recarga e
                // o HoverArmSpread do voo: 0 é T-pose, ou seja braço na horizontal — que é
                // exatamente a altura de quem aponta para frente. Não há nome de intenção honesto
                // para "onde fica o braço".
                float height = SaiyaheimConfig.BlastPoseArmHeight.Value + AimPitchOffset(player);
                HumanMuscles.Blend(muscles, MuscleArmSpread, Mathf.Clamp(height, -1f, 1f), arm);

                // A que faz o gesto. Da T-pose, girar o braço para frente é o que o aponta para
                // onde o jogador olha.
                //
                // A mira horizontal entra aqui, **subtraindo**: com o braço direito, olhar para a
                // direita é abrir o braço de volta na direção do lado (menos "para frente"), e
                // olhar para a esquerda é atravessá-lo no peito (mais). Sem clamp de propósito —
                // atravessar o peito passa de 1, e é uma posição que o braço de verdade alcança.
                // Quem limita quanto isso anda é o AimFollowYaw.
                float forward = SaiyaheimConfig.BlastPoseArmForward.Value
                                - aimYaw * (1f - yawToTorso);
                HumanMuscles.Blend(muscles, MuscleArmSwing, forward * ForwardSign, arm);

                // A rotação do úmero decide para onde a palma aponta. Com o cotovelo esticado ela
                // quase não muda a silhueta — mas é ela que separa "mão espalmada para frente" de
                // "mão de lado", e a bola nasce na mão.
                HumanMuscles.Blend(muscles, MuscleArmTwist,
                    SaiyaheimConfig.BlastPoseArmTwist.Value, arm);

                // Alvo absoluto, e o músculo se chama "Stretch" por um motivo: +1 é o braço reto
                // e -1 a dobra máxima — 0, que parecia o valor óbvio para "cotovelo esticado", é o
                // MEIO da faixa, ou seja um braço dobrado. A descrição desta chave dizia o
                // contrário até 2026-08-21, e foi o que fez o braço reto parecer inalcançável.
                HumanMuscles.Blend(muscles, MuscleElbow,
                    SaiyaheimConfig.BlastPoseElbowStretch.Value, arm);
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
                // A fatia da mira que o braço não levou. Mesmo sinal do braço: girar o tronco para
                // a direita afasta o ombro direito, que é o contrário da intenção positiva daqui.
                float twist = (SaiyaheimConfig.BlastPoseTorsoTwist.Value - aimYaw * yawToTorso)
                              * TwistSign;

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
            LastSeenBlast.Remove(character);
        }

        /// <summary>
        /// Traduz o contador de disparos do <see cref="NetState"/> em chamadas ao
        /// <see cref="Trigger"/>. Roda para todo jogador carregado, todo frame, e por isso a saída
        /// barata é o caminho comum: uma leitura de ZDO e uma comparação de inteiros.
        ///
        /// <b>Dispara uma pose por tiro, mesmo que dois cheguem no mesmo frame.</b> Um jogador com
        /// lag pode aparecer com o contador dois à frente, e a pose não tem o que fazer com isso —
        /// o gesto é o mesmo. O que importa é não perder o <i>último</i>, e o carimbo de tempo
        /// cuida disso sozinho.
        /// </summary>
        private static void ObserveBlast(Player player)
        {
            int count = NetState.GetBlastCount(player);

            if (!LastSeenBlast.TryGetValue(player, out int seen))
            {
                // Primeira vez que este cliente olha para este jogador: anota e não dispara nada.
                LastSeenBlast[player] = count;
                return;
            }

            if (count == seen)
            {
                return;
            }

            LastSeenBlast[player] = count;

            // Menor que o visto significa que o jogador reentrou no mundo e o contador reiniciou.
            // Não é disparo, é ZDO nova.
            if (count > seen)
            {
                Trigger(player);
            }
        }

        /// <summary>
        /// Quanto o braço sobe ou desce para acompanhar a mira.
        ///
        /// Existe porque a bola nasce <b>na mão</b> (<c>KiProjectile.GetOrigin</c>) e voa na
        /// direção do olhar. Com o braço travado na horizontal, mirar no céu produz um tiro saindo
        /// da mão para cima com o braço apontando para o horizonte — e é o tipo de erro que só
        /// aparece quando alguém atira num Draugr numa torre.
        /// </summary>
        private static float AimPitchOffset(Player player)
        {
            float follow = SaiyaheimConfig.BlastPoseAimFollowPitch.Value;
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
        /// Quanto o braço gira na horizontal para acompanhar a mira. Positivo é a câmera olhando à
        /// <b>direita</b> de para onde o corpo aponta.
        ///
        /// <b>Por que não bastava o pitch.</b> A pose é escrita no espaço do <i>corpo</i>, e o
        /// corpo em geral já está virado para a câmera — daí o braço parecer acompanhar a mira
        /// sozinho na horizontal e o irmão vertical ter sido o único a nascer. Mas "em geral" não é
        /// "sempre": correndo para um lado e olhando para outro, o corpo segue o movimento e a
        /// câmera não, e é justamente aí que o tiro sai numa direção com a mão apontando para
        /// outra. É a mesma falha do pitch, no eixo que ninguém tinha olhado.
        ///
        /// <b>O denominador é 90°</b> porque é o que uma unidade de músculo vale neste swing: do
        /// braço aberto de lado (0) ao braço apontado para frente (1) vai exatamente um quarto de
        /// volta. Passar disso é o braço atravessando o peito, e o clamp aqui existe só para o
        /// jogador olhando para trás — onde não há gesto possível e o certo é parar no limite em
        /// vez de o braço dar a volta.
        /// </summary>
        private static float AimYaw(Player player)
        {
            float follow = SaiyaheimConfig.BlastPoseAimFollowYaw.Value;
            if (follow <= 0f)
            {
                return 0f;
            }

            Vector3 look = player.GetLookDir();
            Vector3 body = player.transform.forward;

            // Só o plano do chão: a parte vertical do olhar já é problema do AimPitchOffset, e
            // deixá-la aqui faria mirar no céu contar como girar para o lado.
            look.y = 0f;
            body.y = 0f;

            if (look.sqrMagnitude < 0.0001f || body.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            // SignedAngle em torno do "para cima" dá positivo quando o alvo está à direita da
            // referência. Se na tela o braço for para o lado errado, é este sinal que troca.
            float degrees = Vector3.SignedAngle(body, look, Vector3.up);

            return Mathf.Clamp(degrees / 90f, -1f, 1f) * follow;
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
