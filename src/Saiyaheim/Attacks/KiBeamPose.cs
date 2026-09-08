using System.Collections.Generic;
using Saiyaheim.Net;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Attacks
{
    /// <summary>
    /// A pose do Kamehameha: mãos em concha ao lado do quadril enquanto carrega, e as duas
    /// empurradas para frente ao soltar.
    ///
    /// <b>É a quarta pose, e a primeira com duas fases no mesmo gesto.</b> As três que existem são
    /// de um tipo só cada uma — o voo e a recarga perguntam um <b>estado</b> (<i>está voando? está
    /// carregando?</i>) e escrevem enquanto a resposta for sim; o disparo do ki blast é um
    /// <b>instante</b>, com carimbo de tempo no futuro. Esta precisa das duas, encadeadas: o estado
    /// termina exatamente onde o instante começa.
    ///
    /// <b>A costura é um número, e é o único desenho novo aqui.</b> O peso
    /// (<see cref="PoseState.Weight"/>) diz <i>quanto do corpo a pose é dona</i>, e vale para as
    /// duas fases — ela entra uma vez, quando a carga começa, e sai uma vez, quando o feixe acaba.
    /// A fase (<see cref="PoseState.Release"/>) diz <i>qual dos dois gestos</i>, e cada alvo de
    /// músculo é uma interpolação entre o valor da concha e o do empurrão. Duas poses separadas,
    /// cada uma com o próprio envelope, produziriam um buraco entre elas: uma descendo enquanto a
    /// outra sobe deixa o boneco passar pelo meio do caminho, que é a pose neutra.
    ///
    /// <b>A fase só sobe.</b> Uma vez solto, não se volta a carregar sem largar a pose inteira, e é
    /// isso que impede o gesto de piscar de volta para a concha entre um projétil e o seguinte.
    ///
    /// <b>Nada de rede novo.</b> A carga é o <c>NetState.IsChargingBeam</c>, que os efeitos da mão
    /// já leem; o disparo é o mesmo <b>contador</b> que levanta a pose do ki blast — um por
    /// projétil, o que mantém o empurrão de pé do primeiro ao último em vez de piscar no meio.
    /// A única peça que este arquivo acrescenta é distinguir <i>soltou</i> de <i>cancelou</i>, e
    /// isso é uma janela de tolerância (<see cref="ReleaseGraceSeconds"/>): quem carregava e
    /// disparou vira empurrão, quem carregava e sumiu sem disparar volta para o repouso.
    ///
    /// A janela existe por causa da <b>ordem de chegada</b>: a bandeira de carga e o contador são
    /// duas chaves de ZDO diferentes, e num cliente remoto elas podem chegar em frames diferentes.
    /// Na máquina de quem atirou as duas mudam no mesmo frame.
    ///
    /// <b>Sobre qual lado do quadril</b>: o mesmo que os efeitos de carregamento usam
    /// (<c>ChargeEffectAnchor</c>). Não é config nova de propósito — a bola tem que nascer entre as
    /// mãos, e duas chaves para a mesma pergunta só criariam a chance de elas saírem de sincronia.
    ///
    /// <b>Por último no <see cref="PoseDriver"/>.</b> Carregar um Kamehameha voando é normal, e o
    /// gesto é do corpo inteiro; escrevendo depois, esta pose ganha onde disputa e as outras
    /// continuam donas do que ela não toca. A saída é suave <i>porque</i> as outras duas são
    /// estados contínuos: enquanto esta desce, o que aparece por baixo é a pose de voo ou de
    /// recarga escrita neste mesmo frame, e não um salto.
    ///
    /// <b>A exceção é a pose de disparo do ki blast</b>, que é um instante com envelope próprio e
    /// reagiria ao mesmo contador de projéteis — o braço direito subiria sozinho por baixo desta,
    /// e apareceria com tudo no instante em que ela terminasse de descer. Ela não é levantada
    /// enquanto esta estiver de pé; ver <c>KiBlastPose.ObserveBlast</c>.
    /// </summary>
    internal sealed class KiBeamPose : IPoseContributor
    {
        internal static readonly KiBeamPose Instance = new KiBeamPose();

        private KiBeamPose()
        {
        }

        /// <summary>Qual fase o <c>saiya_blast pose</c> está segurando para calibragem.</summary>
        internal enum DebugPhase
        {
            None,
            Charge,
            Release,
        }

        /// <summary>
        /// Segura uma das duas fases indefinidamente, para calibrar os números no
        /// ConfigurationManager.
        ///
        /// Não é conforto, é a única forma de calibrar: a carga dura dois segundos e o empurrão
        /// menos de um, e o tempo entre arrastar um slider e olhar o personagem já é maior que os
        /// dois juntos. Vale só para o jogador local.
        /// </summary>
        internal static DebugPhase DebugHold;

        // ---------- Sinais ----------
        //
        // O nome do músculo é "direção negativa - direção positiva" (ver HumanMuscles). As chaves
        // de config são em espaço de **intenção** — positivo é sempre "mais do que o nome diz" — e
        // a tradução para o espaço de músculo mora nestas constantes. Se alguma coisa sair para o
        // lado contrário na tela, é uma delas que troca de sinal, e não a config do Henrique.

        /// <summary>"Front-Back": +1 é para trás, então levar para frente é negativo.</summary>
        private const float ForwardSign = -1f;

        /// <summary>"Front-Back" do tronco: +1 é arquear para trás, inclinar é negativo.</summary>
        private const float LeanSign = -1f;

        /// <summary>
        /// Quanto tempo depois de a carga sumir ainda se aceita um disparo como sendo dela.
        ///
        /// <b>Não é chave de config</b> porque não é escolha visual nem número de balanceamento: é
        /// tolerância a duas chaves de ZDO chegarem em frames diferentes num cliente remoto. Na
        /// máquina de quem atirou as duas mudam juntas e esta janela nunca é usada.
        /// </summary>
        private const float ReleaseGraceSeconds = 0.4f;

        /// <summary>Segundos para entregar o corpo à animação de ação, e para retomá-lo.</summary>
        private const float ActionBlendSeconds = 0.12f;

        /// <summary>Velocidade horizontal (ao quadrado) acima da qual as pernas saem do caminho.</summary>
        private const float MovingSpeedSqr = 0.25f;

        // Os dois lados, e desta vez de verdade: ao contrário das três poses anteriores, aqui os
        // braços NÃO fazem a mesma coisa. Um fica do lado do quadril e o outro atravessa o corpo
        // para encontrá-lo, e qual é qual depende de onde a bola nasce.
        private const string MuscleArmSpreadL = "Left Arm Down-Up";
        private const string MuscleArmSpreadR = "Right Arm Down-Up";
        private const string MuscleArmSwingL = "Left Arm Front-Back";
        private const string MuscleArmSwingR = "Right Arm Front-Back";
        private const string MuscleArmTwistL = "Left Arm Twist In-Out";
        private const string MuscleArmTwistR = "Right Arm Twist In-Out";
        private const string MuscleElbowL = "Left Forearm Stretch";
        private const string MuscleElbowR = "Right Forearm Stretch";

        // O giro do ANTEBRAÇO, e não o do úmero. São duas articulações diferentes e o rig humanoide
        // as separa: "Arm Twist In-Out" gira o braço inteiro a partir do ombro e leva o cotovelo
        // junto; este gira só do cotovelo para baixo, e é o único que vira a palma sem mexer em
        // mais nada. Com o cotovelo dobrado — que é a concha inteira — a diferença entre os dois é
        // a diferença entre apontar o antebraço para outro lugar e girar a mão no lugar.
        private const string MuscleForearmTwistL = "Left Forearm Twist In-Out";
        private const string MuscleForearmTwistR = "Right Forearm Twist In-Out";
        private const string MuscleShoulderUpL = "Left Shoulder Down-Up";
        private const string MuscleShoulderUpR = "Right Shoulder Down-Up";
        private const string MuscleShoulderSwingL = "Left Shoulder Front-Back";
        private const string MuscleShoulderSwingR = "Right Shoulder Front-Back";
        // O pulso tem DOIS eixos e o rig humanoide os separa: "Down-Up" é a palma subindo e
        // descendo, "In-Out" é a mão desviando para o lado do polegar ou do mindinho — o mesmo
        // movimento de quem acena. Com os dois, os nove músculos que o humanoide dá a um braço
        // estão todos em uso: ombro (2), braço (3), antebraço (2), pulso (2).
        private const string MuscleWristL = "Left Hand Down-Up";
        private const string MuscleWristR = "Right Hand Down-Up";
        private const string MuscleWristSideL = "Left Hand In-Out";
        private const string MuscleWristSideR = "Right Hand In-Out";

        // Três articulações e não uma "coluna", pelo mesmo motivo das outras poses: no rig do
        // Valheim a lombar arrasta o quadril e as outras duas não. Aqui elas dividem UM alvo de
        // torção e UM de inclinação, escalonados de baixo para cima, mas o peso da lombar é
        // separado — e é por isso que ela pode ficar fora.
        private const string MuscleSpineTwist = "Spine Twist Left-Right";
        private const string MuscleChestTwist = "Chest Twist Left-Right";
        private const string MuscleUpperChestTwist = "UpperChest Twist Left-Right";
        private const string MuscleSpineLean = "Spine Front-Back";
        private const string MuscleChestLean = "Chest Front-Back";
        private const string MuscleUpperChestLean = "UpperChest Front-Back";

        private const string MuscleLegSpreadL = "Left Upper Leg In-Out";
        private const string MuscleLegSpreadR = "Right Upper Leg In-Out";
        private const string MuscleKneeL = "Left Lower Leg Stretch";
        private const string MuscleKneeR = "Right Lower Leg Stretch";

        private static bool _warnedMissing;

        private sealed class PoseState
        {
            /// <summary>0 a 1. Quanto do corpo a pose é dona. Vale para as duas fases.</summary>
            internal float Weight;

            /// <summary>
            /// 0 é a concha, 1 é o empurrão.
            ///
            /// <b>Só desce quando há uma carga nova em curso</b>, e nunca no intervalo entre dois
            /// projéteis do mesmo feixe — o <see cref="HoldUntil"/> cobre o intervalo inteiro, e é
            /// isso que impede o gesto de piscar de volta para a concha no meio do feixe. Descer
            /// numa carga nova só importa se o cooldown do ataque for curto o bastante para o
            /// jogador recomeçar antes de a pose anterior ter saído.
            /// </summary>
            internal float Release;

            /// <summary>Cai a zero durante ataque, defesa e esquiva. Ver <see cref="ActionTarget"/>.</summary>
            internal float ActionWeight = 1f;

            /// <summary>
            /// Cai a zero quando o jogador anda ou está no ar. <b>Só as pernas</b> saem do caminho:
            /// carregar andando é permitido, e um agachamento por cima da animação de corrida briga
            /// com ela — mas a concha na mão continua fazendo sentido.
            /// </summary>
            internal float GroundWeight = 1f;

            /// <summary>
            /// Instante (<c>Time.time</c>) até o qual o empurrão fica de pé. Cada projétil do feixe
            /// empurra este número para a frente, então a pose acompanha o feixe inteiro sem
            /// precisar saber quantos projéteis ele tem.
            /// </summary>
            internal float HoldUntil;

            /// <summary>
            /// Este jogador estava carregando. É o que transforma um disparo qualquer no
            /// <b>desfecho desta carga</b> — sem isto, o ki blast do vizinho levantaria um
            /// Kamehameha.
            /// </summary>
            internal bool Armed;

            /// <summary>Até quando a <see cref="ReleaseGraceSeconds"/> ainda aceita o disparo.</summary>
            internal float ArmedUntil;

            /// <summary>
            /// Último contador de disparo visto neste jogador.
            ///
            /// <b>Mora no estado da pose</b>, e não num dicionário à parte como no
            /// <c>KiBlastPose</c>: lá a memória precisa sobreviver ao intervalo entre dois tiros
            /// avulsos; aqui ela só vale entre o começo da carga e o fim do feixe, que é
            /// exatamente o tempo de vida deste objeto. Anotado no instante em que a pose nasce —
            /// quem chega perto de alguém que já carrega não vê um disparo que não houve.
            /// </summary>
            internal int LastSeenBlast;

            /// <summary>
            /// Desloca as senóides por jogador. Sem isto dois amigos carregando lado a lado tremem
            /// em sincronia perfeita, que é a leitura oposta de "esforço".
            /// </summary>
            internal float Phase;

            /// <summary>
            /// Altura do quadril na pose limpa, capturada no frame em que a pose entra.
            ///
            /// ⚠️ <b>Capturada uma vez, e não medida todo frame</b>, pelo mesmo motivo do
            /// <c>KiChargePose</c>: depois do primeiro <c>SetHumanPose</c> a leitura devolve o que
            /// nós mesmos escrevemos, e subtrair o agachamento de novo afundaria o personagem no
            /// chão um pouco mais a cada frame.
            ///
            /// ⚠️ Se outra pose já tiver baixado o quadril <b>neste mesmo frame</b>, a base entra
            /// baixa. Hoje não acontece: a única outra que escreve <c>bodyPosition</c> é a de
            /// recarga, e o grupo de pernas dela vem desligado de fábrica.
            /// </summary>
            internal float BaseHipY;

            internal bool HasBaseHipY;

            /// <summary>
            /// Guinada da raiz na pose limpa, em graus, capturada no frame em que a pose entra.
            ///
            /// Mesma armadilha e mesma saída do <see cref="BaseHipY"/>, e o mesmo motivo para
            /// existir do lado da rotação: sem uma base, "girar zero grau" significaria endireitar
            /// o personagem, porque a pose idle do Valheim não é simétrica.
            /// </summary>
            internal float BaseBodyYaw;

            internal bool HasBaseBodyYaw;
        }

        private static readonly Dictionary<Character, PoseState> States =
            new Dictionary<Character, PoseState>();

        /// <summary>
        /// Esta pose está escrevendo neste jogador? Quem pergunta é a pose de disparo do ki blast,
        /// para não levantar o braço direito por baixo desta — ver o cabeçalho.
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

            States.TryGetValue(player, out PoseState state);

            if (state == null)
            {
                // O caminho comum, e o barato: ninguém carrega, não há nada para lembrar. Uma
                // leitura de ZDO e uma comparação de bit.
                if (!charging && !IsDebugReleasing(player))
                {
                    return 0f;
                }

                state = GetOrCreateState(player);
                WarnMissingOnce();
            }

            if (charging)
            {
                state.Armed = true;
                state.ArmedUntil = Time.time + ReleaseGraceSeconds;
            }

            // O contador só é lido enquanto há uma carga para desfechar. É o que mantém barato o
            // caso comum e, de quebra, o que impede um ki blast avulso de virar Kamehameha.
            if (state.Armed && ObserveBlast(player, state))
            {
                state.HoldUntil = Time.time + Mathf.Max(0f, SaiyaheimConfig.BeamPoseHoldSeconds.Value);
            }

            bool releasing = Time.time < state.HoldUntil || IsDebugReleasing(player);

            // Carregava e sumiu sem disparar: foi cancelamento. Desarma, e a pose volta ao repouso
            // pelo mesmo caminho por onde entrou.
            if (state.Armed && !charging && !releasing && Time.time >= state.ArmedUntil)
            {
                state.Armed = false;
            }

            bool up = SaiyaheimConfig.BeamPoseEnabled.Value && (charging || releasing);

            state.ActionWeight = Mathf.MoveTowards(
                state.ActionWeight, ActionTarget(player),
                StepPerSecond(ActionBlendSeconds) * deltaTime);

            state.GroundWeight = Mathf.MoveTowards(
                state.GroundWeight, GroundTarget(player),
                StepPerSecond(ActionBlendSeconds) * deltaTime);

            // A fase só se move quando há uma das duas coisas acontecendo. Largada no meio — o
            // jogador cancelou, ou o feixe acabou — ela fica onde está, e a pose sai com o gesto
            // que tinha em vez de trocar de gesto na saída.
            if (releasing || charging)
            {
                state.Release = Mathf.MoveTowards(
                    state.Release, releasing ? 1f : 0f,
                    StepPerSecond(SaiyaheimConfig.BeamPoseReleaseRiseSeconds.Value) * deltaTime);
            }

            float blend = up
                ? SaiyaheimConfig.BeamPoseRiseSeconds.Value
                : SaiyaheimConfig.BeamPoseFallSeconds.Value;

            state.Weight = Mathf.MoveTowards(
                state.Weight, up ? 1f : 0f, StepPerSecond(blend) * deltaTime);

            if (!up && state.Weight <= 0f)
            {
                States.Remove(player);
                return 0f;
            }

            // O peso de entrada, **sem** multiplicar pelo da ação — mesmo motivo do KiChargePose: o
            // driver usa este número para decidir se mantém o handler nativo vivo, e o peso de ação
            // zera a cada soco. Quem consulta o peso de ação é o Apply.
            return state.Weight;
        }

        public void Apply(Player player, ref HumanPose pose)
        {
            if (!States.TryGetValue(player, out PoseState state))
            {
                return;
            }

            float weight = state.Weight * state.ActionWeight;
            if (weight <= 0f)
            {
                return;
            }

            float[] muscles = pose.muscles;
            float release = Mathf.Clamp01(state.Release);

            if (!state.HasBaseHipY)
            {
                state.BaseHipY = pose.bodyPosition.y;
                state.HasBaseHipY = true;
            }

            // ---------- Vida ----------
            //
            // As mesmas duas senóides da recarga, e desligadas pela fase: a concha **dura** e um
            // corpo parado é boneco de vitrine, mas o empurrão é curto e a oscilação nele viraria
            // um tremor de braço estendido — outra leitura. Some junto com a concha.
            float life = 1f - release;
            float time = Time.time + state.Phase;

            float strain = Mathf.Sin(time * SaiyaheimConfig.BeamPoseStrainSpeed.Value)
                           * SaiyaheimConfig.BeamPoseStrain.Value * life;

            float tremorAmount = SaiyaheimConfig.BeamPoseTremor.Value * life;
            float tremorSpeed = SaiyaheimConfig.BeamPoseTremorSpeed.Value;

            // Frequências ligeiramente diferentes entre os lados: em sincronia o tremor lê como
            // vibração mecânica, fora de fase lê como músculo.
            float tremorL = Mathf.Sin(time * tremorSpeed) * tremorAmount;
            float tremorR = Mathf.Sin(time * tremorSpeed * 1.27f + 1.9f) * tremorAmount;

            // Onde a bola nasce decide de que lado do quadril as mãos se juntam, e por consequência
            // qual braço atravessa o corpo para encontrar o outro.
            bool cupRight = IsCupRight();
            float cupSign = cupRight ? 1f : -1f;

            // ---------- Para onde o gesto aponta ----------
            //
            // Só na fase de empurrão: durante a concha as mãos estão no quadril e não há para onde
            // apontar.
            //
            // ⚠️ **A mira inteira é do TRONCO, e os braços não participam dela.** Até 2026-09-07
            // o empurrão mirava pelos músculos do braço, como o ki blast faz, e na tela isso
            // estava quebrado dos dois lados: olhando para cima ou para baixo cada braço ia para
            // um lado, e olhando para o lado um braço girava e o outro não. São duas falhas
            // independentes, e as duas nascem de mirar com um par de membros espelhados em vez de
            // com o eixo do corpo:
            //
            // 1. **Gimbal.** "Arm Down-Up" só é <i>levantar o braço</i> enquanto o braço está ao
            //    lado do corpo. No empurrão ele aponta para frente, e aí esse mesmo músculo gira
            //    em torno de um eixo que também aponta para frente — ou seja, move o braço
            //    LATERALMENTE. Como os dois lados são espelhados, o mesmo delta manda um braço
            //    para cada lado.
            // 2. **Saturação assimétrica.** A mira horizontal entrava somando num braço e
            //    subtraindo no outro, que é geometricamente correto, mas o músculo satura em 1.
            //    Com o braço já quase estendido, olhar para o lado levava um braço ao teto (trava)
            //    enquanto o outro ainda tinha faixa (gira). Um gira, o outro não.
            //
            // O tronco não tem nenhum dos dois problemas: ele leva os dois braços juntos, mantém a
            // forma do gesto, e saturar só faz a mira parar de acompanhar — nunca desmontar. O
            // preço é alcance: o tronco cobre menos ângulo que um braço solto, e mirar no zênite
            // vira "o quanto o peito alcança" em vez do ângulo exato. É o preço certo, porque o
            // ki blast continua mirando pelo braço e funciona: **um** braço não tem lado para
            // divergir.
            float aimPitch = AimFollow(player, SaiyaheimConfig.BeamPoseAimFollowPitch.Value, true)
                             * release;
            float aimYaw = AimFollow(player, SaiyaheimConfig.BeamPoseAimFollowYaw.Value, false)
                           * release;

            // O corpo girado leva os braços junto, e eles são escritos no referencial dele — então
            // apontar para o alvo custa desfazer o giro. Sem isto, pôr o personagem de lado manda o
            // empurrão para o lado também, e o feixe sai de mãos que apontam para outro lugar.
            //
            // ⚠️ **Não passa pelo AimFollowYaw de propósito.** Aquele é o quanto o jogador quer que
            // o gesto siga a câmera; este é a pose desfazendo uma rotação que ela mesma aplicou, e
            // desligá-lo junto deixaria o gesto torto sem que nenhuma chave explicasse por quê.
            // Multiplicado pelo peso das pernas junto com o do corpo: girar a raiz é girar o
            // personagem inteiro, e por cima da animação de corrida isso é o mesmo problema que
            // tira o agachamento do caminho. Andando, o corpo se endireita sozinho.
            //
            // A compensação vai para a torção do tronco pela mesma razão que a mira: nos braços
            // ela entrava com sinal oposto em cada lado e saturava um deles primeiro. O
            // <c>weight</c> fica DE FORA dela — quem escreve músculo já multiplica pelo peso, e
            // contá-lo aqui de novo faria a compensação entrar ao quadrado enquanto a pose sobe.
            float bodyYawDesign = BodyYawDegrees(release) * cupSign * state.GroundWeight;
            float bodyYawToTorso = -bodyYawDesign / 90f
                                   * SaiyaheimConfig.BeamPoseBodyYawCompensation.Value;

            ApplyBodyYaw(ref pose, state, bodyYawDesign * weight);

            ResolveSides(cupRight, release, out SideTargets right, out SideTargets left);

            ApplyArms(muscles, weight, right, left, strain, tremorL, tremorR);
            ApplyForearms(muscles, weight, right, left);
            ApplyShoulders(muscles, weight, right, left, strain, tremorL, tremorR);
            ApplyTorso(muscles, weight, release, cupSign,
                aimYaw + bodyYawToTorso, aimPitch, strain);
            ApplyHands(muscles, weight, release, right, left);
            ApplyLegs(ref pose, state, weight, tremorL, tremorR);
        }

        /// <summary>
        /// Quantos graus o corpo inteiro gira, na fase atual do gesto.
        ///
        /// Positivo afasta o lado da concha do alvo. O <c>cupSign</c> de quem chama traduz isso
        /// para o lado que a bola de fato usa.
        /// </summary>
        private static float BodyYawDegrees(float release)
        {
            return Mathf.Lerp(
                SaiyaheimConfig.BeamPoseChargeBodyYaw.Value,
                SaiyaheimConfig.BeamPoseReleaseBodyYaw.Value, release);
        }

        /// <summary>
        /// Gira o corpo inteiro em torno do próprio eixo — ombros, quadril e pernas juntos.
        ///
        /// <b>Isto não é torção de coluna, e é a diferença que a pose clássica precisa.</b> O
        /// <c>ChargeTorsoTwist</c> é músculo: dobra o tronco e deixa o quadril de frente. Pôr o
        /// personagem <i>de lado</i> é a raiz do humanoide, o <c>bodyRotation</c>, ao qual todos os
        /// músculos são relativos — não há músculo nenhum que faça isso.
        ///
        /// <b>Só o desenho.</b> O <c>transform</c> não é tocado, então mira, colisão e a direção em
        /// que o feixe sai continuam iguais. É a mesma separação que deixa a inclinação do voo ser
        /// escolha visual em vez de mudar o jogo.
        ///
        /// ⚠️ <b>Precisa ser idempotente, e é a única parte desta pose que não é de graça.</b>
        /// Escrever músculo interpola para um alvo absoluto; somar um ângulo à raiz <b>acumula</b>,
        /// e acumula de forma intermitente, porque o animator avalia em passo de física — nos
        /// frames sem passo o <c>GetHumanPose</c> devolve o que nós mesmos escrevemos. Foi o bug de
        /// 2026-07-31 no <c>FlightPose.PitchForward</c>, dois ângulos alternando muito rápido.
        ///
        /// A saída é a mesma de lá: <b>mede-se a guinada atual e aplica-se só a diferença</b> até o
        /// alvo. Rodar duas vezes no mesmo frame dá o mesmo que rodar uma.
        ///
        /// <b>E o alvo é medido a partir da guinada limpa</b>, capturada uma vez, e não do zero da
        /// raiz — ao contrário do <c>FlightPose.SquareToHeading</c>, que zera de vez. A pose idle
        /// do Valheim <b>não</b> é simétrica: o personagem para de lado, com o quadril angulado.
        /// Zerando, a chave em 0 já giraria o personagem, e "desligado" deixaria de significar
        /// "como estava". Somando à base, 0 grau é exatamente a animação e nada é escrito.
        /// </summary>
        private static void ApplyBodyYaw(ref HumanPose pose, PoseState state, float yaw)
        {
            // O bodyRotation é relativo à raiz do avatar, cujo +Z é a frente do personagem — então
            // a guinada é o ângulo entre o Z do corpo e o Z da raiz, no plano horizontal.
            Vector3 forward = pose.bodyRotation * Vector3.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float current = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

            if (!state.HasBaseBodyYaw)
            {
                state.BaseBodyYaw = current;
                state.HasBaseBodyYaw = true;
            }

            float delta = state.BaseBodyYaw + yaw - current;

            if (Mathf.Abs(delta) < 0.01f)
            {
                return;
            }

            pose.bodyRotation = Quaternion.AngleAxis(delta, Vector3.up) * pose.bodyRotation;
        }

        public void Forget(Character character)
        {
            States.Remove(character);
        }

        /// <summary>
        /// Os alvos de <b>um</b> lado do corpo, já resolvidos entre as duas fases.
        ///
        /// <b>Todo alvo lateral vem em par</b>, e este objeto é o par resolvido: o mesmo cálculo
        /// roda duas vezes por frame, uma para o braço da concha e outra para o que atravessa. É
        /// aqui que a pose deixa de ser simétrica, e é o único lugar onde isso é decidido — quem
        /// escreve na pose recebe números prontos e não sabe qual lado é qual.
        ///
        /// A primeira versão compartilhava altura, torção, ombros e pulso entre os dois braços, e
        /// não durou um playtest: dois braços que se encontram num ponto do corpo não fazem a mesma
        /// coisa em eixo nenhum, e cada alvo compartilhado era um lado certo e um lado errado.
        /// </summary>
        private struct SideTargets
        {
            internal float Height;
            internal float Forward;
            internal float Twist;
            internal float ForearmTwist;
            internal float Elbow;
            internal float ShoulderPush;
            internal float ShoulderLift;
            internal float Wrist;
            internal float WristSide;
        }

        /// <summary>
        /// Interpola os alvos de um lado entre a concha e o empurrão.
        /// </summary>
        /// <param name="cup">
        /// Este é o braço do lado onde a bola nasce. O outro é o que atravessa o corpo.
        /// </param>
        private static SideTargets Resolve(bool cup, float release)
        {
            return new SideTargets
            {
                // Altura é alvo ABSOLUTO no espaço de músculo, como o ArmDown da recarga e o
                // ArmHeight do disparo: 0 é T-pose. Não há nome de intenção honesto para "onde
                // fica o braço".
                Height = Mathf.Lerp(
                    cup
                        ? SaiyaheimConfig.BeamPoseChargeCupArmHeight.Value
                        : SaiyaheimConfig.BeamPoseChargeCrossArmHeight.Value,
                    cup
                        ? SaiyaheimConfig.BeamPoseReleaseCupArmHeight.Value
                        : SaiyaheimConfig.BeamPoseReleaseCrossArmHeight.Value,
                    release),

                Forward = Mathf.Lerp(
                    cup
                        ? SaiyaheimConfig.BeamPoseChargeCupArmForward.Value
                        : SaiyaheimConfig.BeamPoseChargeCrossArmForward.Value,
                    cup
                        ? SaiyaheimConfig.BeamPoseReleaseCupArmForward.Value
                        : SaiyaheimConfig.BeamPoseReleaseCrossArmForward.Value,
                    release),

                // O mesmo número não é um espelho: "Twist In-Out" já é nomeado em relação ao corpo,
                // então "para dentro" é para dentro dos dois lados. Que os dois lados tenham chave
                // própria é outra coisa — é que eles querem valores diferentes, não sinais.
                Twist = Mathf.Lerp(
                    cup
                        ? SaiyaheimConfig.BeamPoseChargeCupArmTwist.Value
                        : SaiyaheimConfig.BeamPoseChargeCrossArmTwist.Value,
                    cup
                        ? SaiyaheimConfig.BeamPoseReleaseCupArmTwist.Value
                        : SaiyaheimConfig.BeamPoseReleaseCrossArmTwist.Value,
                    release),

                // Mesma convenção do twist do úmero: o mesmo número não é espelho, é "para dentro"
                // dos dois lados.
                ForearmTwist = Mathf.Lerp(
                    cup
                        ? SaiyaheimConfig.BeamPoseChargeCupForearmTwist.Value
                        : SaiyaheimConfig.BeamPoseChargeCrossForearmTwist.Value,
                    cup
                        ? SaiyaheimConfig.BeamPoseReleaseCupForearmTwist.Value
                        : SaiyaheimConfig.BeamPoseReleaseCrossForearmTwist.Value,
                    release),

                Elbow = Mathf.Lerp(
                    cup
                        ? SaiyaheimConfig.BeamPoseChargeCupElbowBend.Value
                        : SaiyaheimConfig.BeamPoseChargeCrossElbowBend.Value,
                    cup
                        ? SaiyaheimConfig.BeamPoseReleaseCupElbowStretch.Value
                        : SaiyaheimConfig.BeamPoseReleaseCrossElbowStretch.Value,
                    release),

                ShoulderPush = Mathf.Lerp(
                    cup
                        ? SaiyaheimConfig.BeamPoseChargeCupShoulderPush.Value
                        : SaiyaheimConfig.BeamPoseChargeCrossShoulderPush.Value,
                    cup
                        ? SaiyaheimConfig.BeamPoseReleaseCupShoulderPush.Value
                        : SaiyaheimConfig.BeamPoseReleaseCrossShoulderPush.Value,
                    release),

                ShoulderLift = Mathf.Lerp(
                    cup
                        ? SaiyaheimConfig.BeamPoseChargeCupShoulderLift.Value
                        : SaiyaheimConfig.BeamPoseChargeCrossShoulderLift.Value,
                    cup
                        ? SaiyaheimConfig.BeamPoseReleaseCupShoulderLift.Value
                        : SaiyaheimConfig.BeamPoseReleaseCrossShoulderLift.Value,
                    release),

                Wrist = Mathf.Lerp(
                    cup
                        ? SaiyaheimConfig.BeamPoseChargeCupWristBend.Value
                        : SaiyaheimConfig.BeamPoseChargeCrossWristBend.Value,
                    cup
                        ? SaiyaheimConfig.BeamPoseReleaseCupWristBend.Value
                        : SaiyaheimConfig.BeamPoseReleaseCrossWristBend.Value,
                    release),

                WristSide = Mathf.Lerp(
                    cup
                        ? SaiyaheimConfig.BeamPoseChargeCupWristSide.Value
                        : SaiyaheimConfig.BeamPoseChargeCrossWristSide.Value,
                    cup
                        ? SaiyaheimConfig.BeamPoseReleaseCupWristSide.Value
                        : SaiyaheimConfig.BeamPoseReleaseCrossWristSide.Value,
                    release),
            };
        }

        /// <summary>Os alvos do braço direito e do esquerdo, nessa ordem.</summary>
        private static void ResolveSides(bool cupRight, float release,
            out SideTargets right, out SideTargets left)
        {
            SideTargets cup = Resolve(true, release);
            SideTargets cross = Resolve(false, release);

            right = cupRight ? cup : cross;
            left = cupRight ? cross : cup;
        }

        /// <summary>
        /// Os dois braços, e é aqui que esta pose difere de todas as anteriores: eles <b>não</b>
        /// fazem a mesma coisa em eixo nenhum.
        ///
        /// Na concha, um braço fica do lado do quadril e o outro atravessa o corpo para encontrá-lo.
        /// No empurrão os dois convergem para o mesmo gesto — mas continuam vindo de lugares
        /// diferentes, e é por isso que o par sobrevive às duas fases.
        ///
        /// <b>Os braços não miram.</b> Os alvos daqui são a forma do gesto e nada mais; para onde
        /// ele aponta é decidido no tronco, em <see cref="ApplyTorso"/>. Um par de membros
        /// espelhados é o pior lugar possível para pôr uma mira — ver o comentário longo em
        /// <see cref="Apply"/>.
        /// </summary>
        private static void ApplyArms(
            float[] muscles, float weight, SideTargets right, SideTargets left,
            float strain, float tremorL, float tremorR)
        {
            float arm = weight * SaiyaheimConfig.BeamPoseArmWeight.Value;
            if (arm <= 0f)
            {
                return;
            }

            float rise = strain * 0.2f;

            WriteArm(muscles, MuscleArmSpreadR, MuscleArmSwingR, MuscleArmTwistR, MuscleElbowR,
                right.Height + rise, right.Forward, right.Twist,
                right.Elbow + tremorR, arm);

            WriteArm(muscles, MuscleArmSpreadL, MuscleArmSwingL, MuscleArmTwistL, MuscleElbowL,
                left.Height + rise, left.Forward, left.Twist,
                left.Elbow + tremorL, arm);
        }

        /// <summary>
        /// O giro dos antebraços.
        ///
        /// <b>Grupo próprio, e não parte do braço</b>, pela mesma razão que a mão é grupo próprio:
        /// ele é o único alvo da pose que muda para onde a <i>palma</i> aponta sem mudar onde o
        /// braço está. Girar a mão em volta da bola é um ajuste que faz sentido sozinho, por cima
        /// do braço que a animação ou que o resto da pose já colocou.
        ///
        /// E tem um custo de calibragem que os outros não têm, o que é o outro motivo do peso
        /// separado: alvo zero aqui <b>não</b> é "não mexe", é o antebraço no neutro do rig — que
        /// já é uma escolha, e não necessariamente a que estava na tela. <c>ForearmWeight</c> em
        /// zero devolve exatamente o que havia antes de este grupo existir.
        /// </summary>
        private static void ApplyForearms(
            float[] muscles, float weight, SideTargets right, SideTargets left)
        {
            float forearm = weight * SaiyaheimConfig.BeamPoseForearmWeight.Value;
            if (forearm <= 0f)
            {
                return;
            }

            HumanMuscles.Blend(muscles, MuscleForearmTwistR, right.ForearmTwist, forearm);
            HumanMuscles.Blend(muscles, MuscleForearmTwistL, left.ForearmTwist, forearm);
        }

        private static void WriteArm(
            float[] muscles, string spread, string swing, string twistMuscle, string elbow,
            float height, float forward, float twist, float stretch, float weight)
        {
            HumanMuscles.Blend(muscles, spread, Mathf.Clamp(height, -1f, 1f), weight);
            HumanMuscles.Blend(muscles, swing, forward * ForwardSign, weight);
            HumanMuscles.Blend(muscles, twistMuscle, twist, weight);

            // O músculo se chama "Stretch": +1 é o braço reto e -1 a dobra máxima, então 0 é o MEIO
            // da faixa e não "cotovelo esticado". Foi a descrição errada desta chave no ki blast
            // que fez o braço reto parecer inalcançável até 2026-08-21.
            HumanMuscles.Blend(muscles, elbow, Mathf.Clamp(stretch, -1f, 1f), weight);
        }

        /// <summary>
        /// Os ombros, um alvo por lado.
        ///
        /// Grupo separado do braço pelo mesmo motivo do ki blast: o ombro é o que transforma "braço
        /// levantado" em "braço estendido". Sem ele o alcance do empurrão para no encaixe do úmero,
        /// e o personagem parece apontar em vez de empurrar.
        ///
        /// <b>É um joelho a mais do que a torção do tronco, e não o mesmo giro duas vezes.</b> A
        /// torção roda a caixa torácica inteira; isto move uma articulação. Na concha os dois
        /// costumam andar juntos — o ombro que atravessa vem à frente enquanto o tronco gira —, mas
        /// o quanto de cada um é pergunta da tela.
        /// </summary>
        private static void ApplyShoulders(
            float[] muscles, float weight, SideTargets right, SideTargets left,
            float strain, float tremorL, float tremorR)
        {
            float shoulder = weight * SaiyaheimConfig.BeamPoseShoulderWeight.Value;
            if (shoulder <= 0f)
            {
                return;
            }

            float lift = strain * 0.5f;

            HumanMuscles.Blend(muscles, MuscleShoulderSwingR, right.ShoulderPush * ForwardSign, shoulder);
            HumanMuscles.Blend(muscles, MuscleShoulderSwingL, left.ShoulderPush * ForwardSign, shoulder);
            HumanMuscles.Blend(muscles, MuscleShoulderUpR, right.ShoulderLift + lift + tremorR, shoulder);
            HumanMuscles.Blend(muscles, MuscleShoulderUpL, left.ShoulderLift + lift + tremorL, shoulder);
        }

        /// <summary>
        /// O tronco: uma torção e uma inclinação, cada uma repartida pelas três articulações.
        ///
        /// <b>A torção é metade do gesto de carregar.</b> É ela que leva o ombro do lado da concha
        /// para trás e traz o outro à frente — sem ela as duas mãos se encontram no quadril com o
        /// peito de frente, que é uma posição que o corpo não faz.
        ///
        /// Escalonada de baixo para cima: a lombar mal se mexe, o peito alto leva o ombro. Torcer
        /// as três igualmente aponta o quadril para o lado junto, e aí o personagem deixa de
        /// encarar para onde está mirando.
        ///
        /// <b>E é aqui que o empurrão mira, nos dois eixos.</b> A torção acompanha o olhar
        /// horizontal e a inclinação acompanha o vertical, as duas somadas por cima do desenho do
        /// gesto. Os braços não participam: ver <see cref="Apply"/>.
        ///
        /// ⚠️ <b>Com <c>TorsoWeight</c> em zero o empurrão deixa de mirar</b>, e é a única forma
        /// de o gesto voltar a apontar para um lugar fixo. Não é bug: é o preço de a mira morar
        /// num grupo que também pode ser desligado.
        /// </summary>
        private static void ApplyTorso(
            float[] muscles, float weight, float release, float cupSign, float torsoYaw,
            float aimPitch, float strain)
        {
            float torso = weight * SaiyaheimConfig.BeamPoseTorsoWeight.Value;
            if (torso <= 0f)
            {
                return;
            }

            // Positivo na config é "o ombro do lado da concha vai para trás", e o cupSign traduz
            // isso para o lado que a bola de fato usa. O músculo é "Twist Left-Right", onde +1 é
            // girar para a direita — que é o que manda o ombro direito para trás.
            float twist = Mathf.Lerp(
                              SaiyaheimConfig.BeamPoseChargeTorsoTwist.Value,
                              SaiyaheimConfig.BeamPoseReleaseTorsoTwist.Value, release) * cupSign
                          + torsoYaw;

            // O lean positivo é o tronco indo PARA FRENTE, então olhar para cima tem de tirar
            // lean: o peito arqueia para trás e leva os dois braços com ele. Daí o sinal
            // invertido do aimPitch, que vem positivo quando o jogador olha para o alto.
            float lean = Mathf.Lerp(
                             SaiyaheimConfig.BeamPoseChargeTorsoLean.Value,
                             SaiyaheimConfig.BeamPoseReleaseTorsoLean.Value, release)
                         - aimPitch + strain;

            float spine = torso * SaiyaheimConfig.BeamPoseSpineWeight.Value;

            // ⚠️ **Clamp por articulação, e ele passou a ser obrigatório quando a mira entrou
            // aqui.** Enquanto o tronco só carregava o desenho do gesto, as somas eram pequenas e
            // nunca saíam da faixa. Somar a mira estoura: olhar para os pés com o lean já em 0,25
            // pede 1,25 de um músculo que vai até 1. Fora da faixa o valor não é uma pose mais
            // extrema, é uma pose indefinida — e o certo é a mira PARAR no limite do peito, que é
            // exatamente o pedaço de alcance que se aceitou ao tirar a mira dos braços.
            HumanMuscles.Blend(muscles, MuscleSpineTwist, Clamp(twist * 0.25f), spine);
            HumanMuscles.Blend(muscles, MuscleChestTwist, Clamp(twist * 0.75f), torso);
            HumanMuscles.Blend(muscles, MuscleUpperChestTwist, Clamp(twist), torso);

            HumanMuscles.Blend(muscles, MuscleSpineLean, Clamp(lean * 0.25f * LeanSign), spine);
            HumanMuscles.Blend(muscles, MuscleChestLean, Clamp(lean * 0.75f * LeanSign), torso);
            HumanMuscles.Blend(muscles, MuscleUpperChestLean, Clamp(lean * LeanSign), torso);
        }

        /// <summary>Espaço de músculo vai de -1 a 1; fora disso a pose é indefinida.</summary>
        private static float Clamp(float value)
        {
            return Mathf.Clamp(value, -1f, 1f);
        }

        /// <summary>
        /// As pernas: base aberta e meio agachamento, iguais nas duas fases — é a mesma posição que
        /// segura a carga e absorve o empurrão.
        ///
        /// <b>O agachamento é obrigatório, e não enfeite</b>, pelo mesmo motivo do
        /// <c>KiChargePose.ApplyCrouch</c>: o foot IK do jogo roda <b>antes</b> desta pose e não
        /// corrige o que ela escreve, então dobrar o joelho sem baixar o quadril levanta os pés do
        /// chão.
        ///
        /// <b>Mas aqui o quadril é independente do joelho</b>, e lá é proporcional a ele. A relação
        /// de lá nasceu calibrada e foi mantida; repeti-la aqui exigiria repetir também o sinal
        /// invertido que ela carrega ("Lower Leg Stretch" é 1 para a perna reta). Dois números
        /// independentes se calibram um contra o outro na tela em dois passos, e nenhum deles mente
        /// sobre o que faz.
        /// </summary>
        private static void ApplyLegs(
            ref HumanPose pose, PoseState state, float weight, float tremorL, float tremorR)
        {
            float legs = weight * SaiyaheimConfig.BeamPoseLegWeight.Value * state.GroundWeight;
            if (legs <= 0f)
            {
                return;
            }

            float[] muscles = pose.muscles;

            float stance = SaiyaheimConfig.BeamPoseStanceWidth.Value;
            HumanMuscles.Blend(muscles, MuscleLegSpreadL, stance, legs);
            HumanMuscles.Blend(muscles, MuscleLegSpreadR, stance, legs);

            float knee = SaiyaheimConfig.BeamPoseKneeStretch.Value;
            HumanMuscles.Blend(muscles, MuscleKneeL, knee + tremorL * 0.5f, legs);
            HumanMuscles.Blend(muscles, MuscleKneeR, knee + tremorR * 0.5f, legs);

            float drop = SaiyaheimConfig.BeamPoseHipDrop.Value;
            if (drop <= 0f || !state.HasBaseHipY)
            {
                return;
            }

            // Absoluto: base menos agachamento. Rodar duas vezes dá o mesmo resultado que rodar uma
            // — o que NÃO valeria para um deslocamento somado. Ver HumanMuscles.
            Vector3 body = pose.bodyPosition;
            body.y = state.BaseHipY - drop * legs;
            pose.bodyPosition = body;
        }

        /// <summary>
        /// As mãos: em concha durante a carga, palmas abertas no empurrão.
        ///
        /// <b>Os dedos são o único lugar onde a fase troca de forma e não só de valor</b>, então os
        /// dois conjuntos de alvos são construídos uma vez e interpolados um no outro. São 20
        /// músculos por mão, e resolvê-los por nome todo frame seria a única parte cara da pose.
        ///
        /// A concha não é punho: os dedos curvam pela metade e ficam ligeiramente juntos, que é a
        /// mão que segura uma esfera. Punho fechado é a pose de recarga, e é outro gesto.
        /// </summary>
        private static void ApplyHands(
            float[] muscles, float weight, float release, SideTargets right, SideTargets left)
        {
            float hand = weight * SaiyaheimConfig.BeamPoseHandWeight.Value;
            if (hand <= 0f)
            {
                return;
            }

            // ⚠️ **Os pulsos vêm ANTES dos dedos, e com peso próprio.** Até 2026-09-07 eles
            // pegavam carona na intensidade do gesto dos dedos, e o resultado era um bug silencioso
            // que só aparecia na calibragem: com o ChargeHandCup em zero — "deixa os dedos como a
            // animação deixou" — os dois WristBend paravam de fazer qualquer coisa, sem que nada
            // dissesse por quê. São dois gestos com chaves separadas, então são dois pesos.
            //
            // Um alvo por lado, porque as duas mãos se encaram em volta da bola: o mesmo ângulo nas
            // duas põe uma delas de costas para ela.
            HumanMuscles.Blend(muscles, MuscleWristR, right.Wrist, hand);
            HumanMuscles.Blend(muscles, MuscleWristL, left.Wrist, hand);
            HumanMuscles.Blend(muscles, MuscleWristSideR, right.WristSide, hand);
            HumanMuscles.Blend(muscles, MuscleWristSideL, left.WristSide, hand);

            float fingers = hand * Mathf.Lerp(
                Mathf.Clamp01(SaiyaheimConfig.BeamPoseChargeHandCup.Value),
                Mathf.Clamp01(SaiyaheimConfig.BeamPoseReleaseHandOpen.Value),
                release);

            if (fingers <= 0f)
            {
                return;
            }

            if (_fingerIndex == null)
            {
                BuildFingers();
            }

            for (int i = 0; i < _fingerIndex.Length; i++)
            {
                HumanMuscles.Blend(
                    muscles, _fingerIndex[i],
                    Mathf.Lerp(_cupTarget[i], _openTarget[i], release), fingers);
            }
        }

        private static int[] _fingerIndex;
        private static float[] _cupTarget;
        private static float[] _openTarget;

        private static void BuildFingers()
        {
            List<int> indices = new List<int>();
            List<float> cup = new List<float>();
            List<float> open = new List<float>();

            foreach (string side in new[] { "Left", "Right" })
            {
                foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                {
                    // O polegar curva bem menos que os outros: no valor cheio ele atravessa a mão.
                    float curl = finger == "Thumb" ? -0.3f : -0.55f;

                    for (int joint = 1; joint <= 3; joint++)
                    {
                        // Alvo 0 é a mão neutra do humanoide — plana. "Abrir" é puxar para o
                        // neutro, exatamente como o ApplyOpenPalm do ki blast faz.
                        Add($"{side} {finger} {joint} Stretched", curl, 0f);
                    }

                    // A concha mantém os dedos quase juntos, a palma aberta os separa. Mão aberta
                    // com dedos colados lê como golpe de caratê, que é outro gesto.
                    Add($"{side} {finger} Spread", 0.1f, 0.3f);
                }
            }

            _fingerIndex = indices.ToArray();
            _cupTarget = cup.ToArray();
            _openTarget = open.ToArray();

            void Add(string name, float cupped, float opened)
            {
                int index = HumanMuscles.IndexOf(name);
                if (index < 0)
                {
                    return;
                }

                indices.Add(index);
                cup.Add(cupped);
                open.Add(opened);
            }
        }

        /// <summary>
        /// Traduz o contador de disparos em "saiu um projétil agora".
        ///
        /// Menor que o visto significa que o jogador reentrou no mundo e o contador reiniciou. Não
        /// é disparo, é ZDO nova.
        /// </summary>
        private static bool ObserveBlast(Player player, PoseState state)
        {
            int count = NetState.GetBlastCount(player);

            if (count == state.LastSeenBlast)
            {
                return false;
            }

            bool fired = count > state.LastSeenBlast;
            state.LastSeenBlast = count;

            return fired;
        }

        /// <summary>
        /// Quanto do olhar o gesto acompanha. Zero de seguimento sai cedo: a conta pede um
        /// <c>normalize</c> ou um <c>SignedAngle</c>, e ela roda para todo jogador que estiver
        /// posando.
        /// </summary>
        private static float AimFollow(Player player, float follow, bool pitch)
        {
            if (follow == 0f)
            {
                return 0f;
            }

            return (pitch ? AimPose.Pitch(player) : AimPose.Yaw(player)) * follow;
        }

        /// <summary>
        /// De que lado do quadril as mãos se juntam.
        ///
        /// <b>Sai do mesmo lugar que a bola de carregamento</b> (<c>ChargeEffectAnchor</c>), e não
        /// de uma chave própria: a esfera nasce entre as mãos, e duas chaves para a mesma pergunta
        /// só criariam a chance de as mãos se juntarem de um lado e a bola nascer do outro.
        ///
        /// Sempre o primeiro ataque carregável da escada, como no <c>KiBeamChargeEffects</c>: a ZDO
        /// diz que o jogador carrega, não O QUE ele carrega. <c>Body</c> não é lado nenhum e cai na
        /// direita, que é a mão de onde o projétil sai.
        /// </summary>
        private static bool IsCupRight()
        {
            foreach (KiAttack attack in KiAttackRegistry.All)
            {
                if (attack.IsCharged)
                {
                    return attack.Config.ChargeEffectAnchor.Value != EffectAnchor.LeftHand;
                }
            }

            return true;
        }

        /// <summary>
        /// Quem está carregando um ataque de ki, na perspectiva desta máquina — e a resposta vale
        /// para qualquer jogador, não só o local.
        ///
        /// <b>O <c>KiBeamCharge</c> não é consultado aqui, nem para o jogador local</b>, pelo mesmo
        /// motivo que o <c>KiChargePose</c> não consulta o <c>KiManager</c>: duas fontes de verdade
        /// para a mesma pergunta escondem a quebra do canal justamente de quem poderia vê-la. Lendo
        /// todo mundo do mesmo lugar, o bug aparece na tela de quem está desenvolvendo.
        /// </summary>
        private static bool IsCharging(Player player)
        {
            if (!SaiyaheimConfig.BeamPoseEnabled.Value)
            {
                return false;
            }

            if (DebugHold == DebugPhase.Charge && player == Player.m_localPlayer)
            {
                return true;
            }

            return NetState.IsChargingBeam(player);
        }

        private static bool IsDebugReleasing(Player player)
        {
            return SaiyaheimConfig.BeamPoseEnabled.Value
                   && DebugHold == DebugPhase.Release
                   && player == Player.m_localPlayer;
        }

        /// <summary>
        /// Zero enquanto o jogador faz outra coisa com o corpo; um no resto do tempo.
        ///
        /// Mesma razão do <c>KiChargePose.ActionTarget</c>: a pose reescreve os músculos depois de
        /// o animator ter rodado, então um golpe tocaria e seria apagado antes de aparecer.
        ///
        /// <b>Andar não entra nesta lista</b>, ao contrário da recarga: carregar um Kamehameha
        /// andando é permitido pela mecânica, e desligar o gesto inteiro deixaria a bola acesa na
        /// mão de um personagem em pose neutra. Quem sai do caminho ao andar são só as pernas —
        /// ver <see cref="GroundTarget"/>.
        /// </summary>
        private static float ActionTarget(Player player)
        {
            return player.InAttack() || player.IsBlocking() || player.InMinorAction()
                   || player.InDodge()
                ? 0f
                : 1f;
        }

        /// <summary>
        /// Um enquanto o jogador está parado no chão; zero andando ou no ar.
        ///
        /// Só o grupo das pernas depende disto. Um agachamento por cima da animação de corrida
        /// briga com ela, e no ar ele briga com a pose de voo — que continua dona das pernas
        /// justamente porque esta as solta.
        /// </summary>
        private static float GroundTarget(Player player)
        {
            if (NetState.IsFlying(player) || !player.IsOnGround())
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
                // mesmo personagem treme igual entre uma carga e a seguinte.
                Phase = Mathf.Abs(player.GetInstanceID() % 1000) * 0.01f,

                // Anotado agora, e não no primeiro disparo: quem chega perto de alguém que já está
                // carregando encontra o contador em 7, anota o 7 e espera o 8.
                LastSeenBlast = NetState.GetBlastCount(player),
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
                "The Kamehameha pose",
                MuscleArmSpreadL, MuscleArmSpreadR, MuscleArmSwingL, MuscleArmSwingR,
                MuscleArmTwistL, MuscleArmTwistR, MuscleElbowL, MuscleElbowR,
                MuscleForearmTwistL, MuscleForearmTwistR,
                MuscleShoulderUpL, MuscleShoulderUpR, MuscleShoulderSwingL, MuscleShoulderSwingR,
                MuscleWristL, MuscleWristR, MuscleWristSideL, MuscleWristSideR,
                MuscleSpineTwist, MuscleChestTwist, MuscleUpperChestTwist,
                MuscleSpineLean, MuscleChestLean, MuscleUpperChestLean,
                MuscleLegSpreadL, MuscleLegSpreadR, MuscleKneeL, MuscleKneeR);
        }
    }
}
