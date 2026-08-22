using System.Collections.Generic;
using Saiyaheim.Ki;
using Saiyaheim.Net;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Flight
{
    /// <summary>
    /// A pose de voo, construída em código — sem clipe, sem AssetBundle, sem Blender.
    ///
    /// <b>O problema.</b> O <see cref="FlightPosePatch"/> estaciona o animator na pose idle, que já
    /// deixa o voo jogável, mas é literalmente um personagem <i>de pé</i> transladando pelo ar:
    /// pernas afastadas na postura de andar, braços caídos, nada muda quando ele acelera.
    ///
    /// <b>A saída.</b> Voar parado e voar para frente não são duas performances, são duas
    /// <i>poses</i> — e pose é um punhado de rotações. Esta classe lê a pose que o animator acabou
    /// de produzir e sobrescreve os controles que interessam; quem faz a leitura e a devolução é o
    /// <see cref="PoseDriver"/>, que é o dono do hook e do handler.
    ///
    /// <b>Espaço de músculos, não rotação de osso.</b> Escrever <c>localRotation</c> osso a osso
    /// exigiria saber qual eixo local de cada osso é "afastar o braço" — e isso depende da bind pose
    /// do rig. O sistema humanoide da Unity já resolve: ver <see cref="HumanMuscles"/>.
    ///
    /// <b>Multiplayer.</b> Nada disto é replicado, e não precisa: quem está voando vem do
    /// <see cref="NetState"/>, e cada cliente aplica a pose localmente. Por isso o driver roda em
    /// <b>todo</b> <c>Player</c>, não só no local.
    ///
    /// ⚠️ <b>Até a etapa 8 esta pose era local, e o comentário aqui dizia o contrário</b> — a
    /// premissa era que o <c>SE_Flight</c> sincronizava por ZDO, e status effect não sincroniza.
    /// O que os outros viam do voo era só efeito colateral: o <see cref="FlightPosePatch"/>
    /// escreve <c>onGround</c> e <c>falling</c> pelo <c>ZSyncAnimation</c>, e <b>isso</b> replica.
    /// Boneco em pé deslizando pelo ar. Ver o cabeçalho do <see cref="NetState"/>.
    /// </summary>
    internal sealed class FlightPose : IPoseContributor
    {
        internal static readonly FlightPose Instance = new FlightPose();

        private FlightPose()
        {
        }

        // ---------- A pose, fixa no código ----------
        //
        // Ficou em config enquanto era chute, para o playtest poder mexer sem recompilar. Depois de
        // calibrada em 2026-07-31 a pose é uma decisão fechada, não balanceamento: ninguém vai
        // querer outro número, e vinte e cinco chaves no .cfg só atrapalham quem for instalar.
        //
        // Toda a metade de músculos é escala normalizada [-1, 1] — não é grau nem radiano: os
        // extremos são os limites que o próprio avatar declara, e por isso os mesmos números
        // funcionariam em qualquer rig humanoide. As de inclinação são graus de verdade.
        //
        // Dois conjuntos, Hover e Forward, interpolados pela velocidade horizontal.

        /// <summary>Lombar parado. Pequena de propósito: no rig do Valheim ela arrasta o quadril.</summary>
        private const float HoverSpine = 0.3f;

        /// <summary>Peito parado. Dobra o tronco sem levar o quadril junto.</summary>
        private const float HoverChest = 0f;

        /// <summary>Braços parado. Alvo absoluto: 0 é T-pose, ~-0,65 é braço caído.</summary>
        private const float HoverArmSpread = -0.45f;
        private const float HoverArmSwing = 0.05f;

        private const float ForwardSpine = 0.15f;

        /// <summary>Peito em cruzeiro — a inclinação principal do voo lento.</summary>
        private const float ForwardChest = 0.4f;

        /// <summary>Braços colados no corpo em velocidade máxima: é o que parece aerodinâmico.</summary>
        private const float ForwardArmSpread = -0.6f;
        private const float ForwardArmSwing = 0.4f;

        /// <summary>Graus de barriga para baixo em velocidade de corrida. 90 seria Superman.</summary>
        private const float FastPitch = 55f;

        /// <summary>A versão suave do <see cref="FastPitch"/>, em velocidade de cruzeiro.</summary>
        private const float CruisePitch = 12f;

        /// <summary>Graus de nariz para cima subindo, e para baixo descendo.</summary>
        private const float ClimbPitch = 25f;

        /// <summary>
        /// Braço quase reto. O músculo se chama "Forearm Stretch" e +1 é o braço estendido, não
        /// dobrado — a constante se chamava <c>ElbowBend</c> e mentia sobre a própria direção até
        /// 2026-08-21. O valor não mudou: 0.8 é o que foi calibrado na tela, e voar de braço reto
        /// é justamente o que se queria.
        /// </summary>
        private const float ElbowStretch = 0.8f;

        /// <summary>Pé esticado. Barato, e vende voo melhor que quase tudo aqui.</summary>
        private const float ToePoint = 0.4f;

        // Perna esquerda e direita separadas: a pose do gênero é assimétrica — uma recolhida, a
        // outra estendida — e o espelhamento automático saiu para o lado errado no playtest.
        private const float LegBendLeft = 0.7f;
        private const float LegBendRight = 0.3f;
        private const float LegSpreadLeft = 0f;
        private const float LegSpreadRight = 0.1f;
        private const float LegSwingLeft = 0.4f;
        private const float LegSwingRight = 0.3f;

        /// <summary>
        /// Quanto o corpo é girado para ficar de frente para a direção do voo. Ver
        /// <see cref="SquareToHeading"/>.
        /// </summary>
        private const float SquareToHeadingAmount = 1f;

        /// <summary>Quanto da pose das pernas sobrevive a um golpe. 1 segura tudo.</summary>
        private const float ActionLegHold = 1f;

        /// <summary>Segundos para entregar o corpo à animação de ação, e para retomá-lo.</summary>
        private const float ActionBlendSeconds = 0.12f;

        /// <summary>Segundos para a pose entrar na decolagem e sair no pouso.</summary>
        private const float BlendSeconds = 0.35f;

        private const string MuscleSpine = "Spine Front-Back";
        private const string MuscleChest = "Chest Front-Back";
        private const string MuscleSpineTwist = "Spine Twist Left-Right";
        private const string MuscleChestTwist = "Chest Twist Left-Right";
        private const string MuscleUpperChestTwist = "UpperChest Twist Left-Right";
        private const string MuscleArmSpreadL = "Left Arm Down-Up";
        private const string MuscleArmSpreadR = "Right Arm Down-Up";
        private const string MuscleArmSwingL = "Left Arm Front-Back";
        private const string MuscleArmSwingR = "Right Arm Front-Back";
        private const string MuscleElbowL = "Left Forearm Stretch";
        private const string MuscleElbowR = "Right Forearm Stretch";
        private const string MuscleLegSwingL = "Left Upper Leg Front-Back";
        private const string MuscleLegSwingR = "Right Upper Leg Front-Back";
        private const string MuscleLegSpreadL = "Left Upper Leg In-Out";
        private const string MuscleLegSpreadR = "Right Upper Leg In-Out";
        private const string MuscleKneeL = "Left Lower Leg Stretch";
        private const string MuscleKneeR = "Right Lower Leg Stretch";
        private const string MuscleFootL = "Left Foot Up-Down";
        private const string MuscleFootR = "Right Foot Up-Down";

        private static bool _warnedMissing;

        private sealed class PoseState
        {
            /// <summary>0 a 1. Faz a pose entrar e sair sem estalo.</summary>
            internal float Weight;

            /// <summary>
            /// Cai a zero durante ataque, defesa e emote, tirando a pose inteira do caminho.
            /// Ver <see cref="ActionTarget"/>.
            /// </summary>
            internal float ActionWeight = 1f;
        }

        private static readonly Dictionary<Character, PoseState> States =
            new Dictionary<Character, PoseState>();

        public float Step(Player player, float deltaTime)
        {
            // NetState e não FlightManager: o segundo pergunta ao SEMan, que só responde pelo dono.
            bool flying = NetState.IsFlying(player);

            if (!flying && !States.ContainsKey(player))
            {
                return 0f;
            }

            WarnMissingOnce();

            PoseState state = GetOrCreateState(player);

            state.ActionWeight = Mathf.MoveTowards(
                state.ActionWeight,
                ActionTarget(player),
                StepPerSecond(ActionBlendSeconds) * deltaTime);

            // A ação multiplica o peso da pose inteira, e não só o da metade de cima. Ver ActionTarget.
            state.Weight = StepWeight(state.Weight, flying, deltaTime);

            if (state.Weight <= 0f)
            {
                States.Remove(player);
                return 0f;
            }

            return state.Weight;
        }

        public void Apply(Player player, ref HumanPose pose)
        {
            if (States.TryGetValue(player, out PoseState state))
            {
                ApplyPose(player, state, ref pose);
            }
        }

        public void Forget(Character character)
        {
            States.Remove(character);
        }

        /// <summary>
        /// Interpola entre a pose parada e a de velocidade máxima pela velocidade horizontal atual.
        ///
        /// É isto que dispensa o segundo clipe: em vez de dois estados que se alternam, uma pose só
        /// que se inclina continuamente conforme o jogador acelera. O componente vertical fica de
        /// fora — subir na vertical não deve deitar o corpo para frente.
        /// </summary>
        private static void ApplyPose(Player player, PoseState state, ref HumanPose pose)
        {
            float[] muscles = pose.muscles;

            // Dois pesos, e a divisão importa. As **pernas** ficam na pose durante um golpe: são
            // elas que dizem "isto é voo", e nenhuma animação de ataque do Valheim depende delas
            // (o chute é a exceção, e o ActionLegHold existe para esse caso). Todo o resto —
            // tronco, braços e, principalmente, a rotação do quadril — volta para a animação.
            //
            // A primeira tentativa fez o corte no lugar errado: soltou os braços e manteve o
            // quadril, e como o soco gira o quadril o resultado foi uma torção forçada. A segunda
            // soltou tudo, e aí bloquear parecia estar de pé no ar. O corte certo é este.
            float weight = state.Weight;
            float action = weight * state.ActionWeight;
            float legs = weight * Mathf.Lerp(state.ActionWeight, 1f, ActionLegHold);

            if (weight > 0f)
            {
                Vector3 velocity = player.GetVelocity();
                float verticalSpeed = velocity.y;
                velocity.y = 0f;
                float speed = velocity.magnitude;

                // Duas faixas, e não uma. Antes havia só uma normalização contra a velocidade
                // rápida, e ela tinha um defeito silencioso: voando devagar o fator parava em
                // ~0,55 (a razão entre lenta e rápida), então a pose de frente nunca chegava
                // inteira em velocidade de cruzeiro. Foi o que obrigou o playtest a inflar o
                // ForwardSpine para compensar.
                //
                // Agora a faixa de caminhada satura na velocidade lenta, e a de corrida só começa
                // onde a outra terminou. Uma pega a pose de músculo, a outra a inclinação do corpo.
                //
                // Sai da velocidade real, não do botão de correr: o m_run é do dono e não replica,
                // e velocidade todo mundo enxerga — os amigos veem a mesma inclinação.
                float slow = Mathf.Max(player.m_flySlowSpeed, 0.01f);
                float fast = Mathf.Max(player.m_flyFastSpeed, slow + 0.01f);

                float speed01 = Mathf.Clamp01(speed / slow);
                float fast01 = Mathf.Clamp01((speed - slow) / (fast - slow));

                // Assinado: positivo subindo, negativo descendo. É o que faltava para o corpo
                // levantar o nariz ao subir e baixar ao descer.
                float vertical01 = Mathf.Clamp(verticalSpeed / slow, -1f, 1f);

                // Guinada e inclinação são orientação do quadril: saem junto com o resto do corpo,
                // senão volta a torção do primeiro playtest.
                SquareToHeading(ref pose, muscles, action);
                PitchForward(ref pose, speed01, fast01, vertical01, action);
                ApplyMuscles(muscles, speed01, fast01, action, legs);
            }
        }

        /// <summary>
        /// Põe o corpo de frente para a direção do voo.
        ///
        /// <b>O sintoma.</b> No playtest a metade de baixo do corpo aparecia torcida para a direita,
        /// e não acompanhava a direção do voo. A causa é a pose idle do Valheim: ela não é simétrica
        /// — o personagem para de lado, com o quadril angulado e a coluna torcida no sentido oposto
        /// para o tronco continuar de frente. Andando isso passa despercebido; parado no ar, não.
        ///
        /// <b>Por que não sai nos músculos.</b> A orientação do quadril não é músculo nenhum: é o
        /// <c>bodyRotation</c> do <see cref="HumanPose"/>, a raiz do corpo humanoide, e todos os
        /// músculos são relativos a ela. Mexer em músculo giraria membro por membro em cima de uma
        /// base que continua torta.
        ///
        /// Então cancela-se a guinada da raiz — e junto a torção de coluna que o idle usava para
        /// compensar, senão o tronco passaria a apontar para o outro lado pelo mesmo ângulo.
        /// </summary>
        private static void SquareToHeading(ref HumanPose pose, float[] muscles, float weight)
        {
            float amount = SquareToHeadingAmount * weight;
            if (amount <= 0f)
            {
                return;
            }

            // O bodyRotation é relativo à raiz do avatar, cujo +Z é a frente do personagem — então
            // a guinada é o ângulo entre o Z do corpo e o Z da raiz, no plano horizontal.
            Vector3 forward = pose.bodyRotation * Vector3.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.0001f)
            {
                float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                pose.bodyRotation = Quaternion.Euler(0f, -yaw * amount, 0f) * pose.bodyRotation;
            }

            Blend(muscles, MuscleSpineTwist, 0f, amount);
            Blend(muscles, MuscleChestTwist, 0f, amount);
            Blend(muscles, MuscleUpperChestTwist, 0f, amount);
        }

        /// <summary>
        /// Deita o corpo inteiro de barriga para baixo em velocidade de corrida.
        ///
        /// <b>Corpo inteiro, e não coluna.</b> Dobrar a coluna curva o tronco e deixa as pernas
        /// para trás, que é a pose de quem se inclina para frente <i>em pé</i>. O que o voo rápido
        /// pede é o corpo todo girando em torno do quadril — e isso é o <c>bodyRotation</c>, a raiz
        /// do humanoide, não músculo nenhum.
        ///
        /// <b>Só o desenho.</b> O <c>transform</c> continua nivelado pelo
        /// <see cref="FlightPosePatch"/>, então mira, colisão e direção de voo seguem horizontais
        /// enquanto o personagem <i>parece</i> deitado. É a separação que deixa a inclinação ser
        /// escolha visual em vez de mudar o jogo.
        ///
        /// Positivo é barriga para baixo. Se sair de barriga para cima, é o sinal.
        ///
        /// <b>⚠️ Precisa ser idempotente, e a primeira versão não era.</b> Este método roda por
        /// <i>frame</i>, mas o Animator do jogador avalia em passo de <i>física</i>. Nos frames em
        /// que o animator não reavaliou, o <c>GetHumanPose</c> devolve a pose que nós mesmos já
        /// inclinamos — e somar o ângulo de novo dobra a inclinação. O sintoma no playtest de
        /// 2026-07-31 foi exatamente esse: dois ângulos alternando muito rápido, conforme o frame
        /// tivesse caído depois de um passo de física ou não.
        ///
        /// Por isso o ângulo é <b>absoluto</b>, e não somado: mede-se a inclinação atual e aplica-se
        /// só a diferença até o alvo. Rodar duas vezes no mesmo frame dá o mesmo resultado que rodar
        /// uma. O <see cref="SquareToHeading"/> nunca sofreu disso porque cancelar guinada já é
        /// idempotente por construção — zerar duas vezes é zerar.
        /// </summary>
        private static void PitchForward(
            ref HumanPose pose, float speed01, float fast01, float vertical01, float weight)
        {
            // Cruzeiro e corrida se revezam: a inclinação leve da velocidade lenta some conforme a
            // forte entra, em vez de as duas se somarem.
            float basePitch =
                CruisePitch * speed01 * (1f - fast01) +
                FastPitch * fast01;

            // Subir levanta o nariz, descer abaixa. Sem este termo a inclinação vertical só
            // aparecia por acidente, como queda do fator horizontal — e por isso subir e descer
            // davam no mesmo, que foi o que o playtest de 2026-07-31 pegou.
            //
            // Multiplicado pela velocidade horizontal porque inclinar é coisa de quem se desloca:
            // subir na vertical, parado, é o Goku subindo em pé, não um avião cabrando.
            float climb = ClimbPitch * vertical01 * speed01;

            float target = (basePitch - climb) * weight;

            // Inclinação atual do corpo, no referencial da raiz. Depois do SquareToHeading a frente
            // do corpo é a frente da raiz, então o Y da frente é seno da inclinação: negativo é
            // nariz para baixo, e o sinal é invertido para "positivo = barriga para baixo".
            Vector3 forward = pose.bodyRotation * Vector3.forward;
            float current = -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;

            float delta = target - current;
            if (Mathf.Abs(delta) < 0.01f)
            {
                return;
            }

            pose.bodyRotation = Quaternion.AngleAxis(delta, Vector3.right) * pose.bodyRotation;
        }

        /// <param name="weight">Tronco e braços. Cai a zero durante ataque, defesa e emote.</param>
        /// <param name="legs">Pernas e pés, que seguram a leitura de voo durante o golpe.</param>
        private static void ApplyMuscles(
            float[] muscles, float speed01, float fast01, float weight, float legs)
        {
            // A curvatura de tronco é da caminhada e some conforme a inclinação do corpo entra:
            // as duas juntas dobrariam o personagem em dois. Foi o que o playtest de 2026-07-31
            // descreveu como "fica bom devagar, mas apertando para correr fica muito ruim".
            float walk = 1f - fast01;

            float spine = Mathf.Lerp(HoverSpine, ForwardSpine, speed01) * walk;

            // Lombar e peito separados porque não são a mesma articulação: no rig do Valheim a
            // lombar arrasta o quadril junto, e foi ela que o playtest viu "mexendo as pernas".
            float chest = Mathf.Lerp(HoverChest, ForwardChest, speed01) * walk;

            float armSpread = Mathf.Lerp(HoverArmSpread, ForwardArmSpread, speed01);
            float armSwing = Mathf.Lerp(HoverArmSwing, ForwardArmSwing, speed01);

            Blend(muscles, MuscleSpine, spine, weight);
            Blend(muscles, MuscleChest, chest, weight);

            Blend(muscles, MuscleArmSpreadL, armSpread, weight);
            Blend(muscles, MuscleArmSpreadR, armSpread, weight);
            Blend(muscles, MuscleArmSwingL, armSwing, weight);
            Blend(muscles, MuscleArmSwingR, armSwing, weight);

            Blend(muscles, MuscleElbowL, ElbowStretch, weight);
            Blend(muscles, MuscleElbowR, ElbowStretch, weight);

            // Perna esquerda e direita separadas: a pose clássica do gênero tem uma perna recolhida
            // e a outra estendida, e no primeiro playtest o valor único ainda espelhava errado.
            Blend(muscles, MuscleLegSwingL, LegSwingLeft, legs);
            Blend(muscles, MuscleLegSwingR, LegSwingRight, legs);

            Blend(muscles, MuscleLegSpreadL, LegSpreadLeft, legs);
            Blend(muscles, MuscleLegSpreadR, LegSpreadRight, legs);

            Blend(muscles, MuscleKneeL, LegBendLeft, legs);
            Blend(muscles, MuscleKneeR, LegBendRight, legs);

            Blend(muscles, MuscleFootL, ToePoint, legs);
            Blend(muscles, MuscleFootR, ToePoint, legs);
        }

        private static void Blend(float[] muscles, string name, float target, float weight) =>
            HumanMuscles.Blend(muscles, name, target, weight);

        private static float StepWeight(float weight, bool flying, float deltaTime)
        {
            float target = flying ? 1f : 0f;
            return Mathf.MoveTowards(
                weight, target, StepPerSecond(BlendSeconds) * deltaTime);
        }

        /// <summary>Blend em segundos vira passo por segundo; 0 vira "instantâneo".</summary>
        private static float StepPerSecond(float blendSeconds) =>
            blendSeconds <= 0f ? float.PositiveInfinity : 1f / blendSeconds;

        /// <summary>
        /// Zero enquanto o jogador faz alguma coisa com o corpo; um no resto do tempo.
        ///
        /// <b>Por que precisa existir.</b> A pose reescreve os músculos <i>todo frame</i>, depois de
        /// o animator ter rodado. Um soco toca normalmente e é apagado antes de aparecer — o
        /// playtest de 2026-07-31 descreveu como "faz a animação mas fica todo torto".
        ///
        /// <b>⚠️ Soltar só a metade de cima não bastou.</b> A primeira tentativa liberou braços,
        /// cotovelos e tronco mas manteve o <c>bodyRotation</c> no peso cheio, argumentando que
        /// orientação não é animação de braço. Estava errado: o soco <b>gira o quadril</b>, e o
        /// quadril é exatamente o <c>bodyRotation</c>. Braço solto com quadril travado é uma torção
        /// forçada — melhorou o sintoma sem tirar a causa, que foi o relato do segundo playtest.
        ///
        /// Agora a ação zera o peso da pose <b>inteira</b>: músculos, guinada e inclinação. Durante
        /// o golpe o mod some do caminho e a animação do jogo roda como no chão, sobre a pose idle
        /// que o <see cref="FlightPosePatch"/> já mantém. Cai fora a categoria de bug em vez de um
        /// caso dela — o chute do golpe secundário, que mexe nas pernas, vem junto de graça.
        ///
        /// <c>InEmote</c> entra na lista para a pose não comer a animação de um emote que o jogador
        /// tenha pedido. O <see cref="KiChargePose.IsPosing"/> vem junto e é o que resolve carregar
        /// ki no ar: são duas poses procedurais disputando o mesmo corpo, e quem cede é o voo.
        /// (Até 2026-08-07 o carregamento era um emote e caía no <c>InEmote</c>; hoje não é mais,
        /// e sem esta segunda checagem as duas brigariam.)
        /// </summary>
        private static float ActionTarget(Player player)
        {
            bool busy = player.InAttack()
                        || player.IsBlocking()
                        || player.InMinorAction()
                        || player.InDodge()
                        || player.InEmote()
                        || KiChargePose.IsPosing(player);

            return busy ? 0f : 1f;
        }

        private static PoseState GetOrCreateState(Player player)
        {
            if (States.TryGetValue(player, out PoseState existing))
            {
                return existing;
            }

            PoseState state = new PoseState { Weight = 0f };
            States[player] = state;
            return state;
        }

        private static void WarnMissingOnce()
        {
            if (_warnedMissing)
            {
                return;
            }

            _warnedMissing = true;

            HumanMuscles.WarnMissing(
                "The flight pose",
                MuscleSpine, MuscleChest, MuscleSpineTwist, MuscleChestTwist,
                MuscleUpperChestTwist, MuscleArmSpreadL, MuscleArmSpreadR,
                MuscleArmSwingL, MuscleArmSwingR, MuscleElbowL, MuscleElbowR,
                MuscleLegSwingL, MuscleLegSwingR, MuscleLegSpreadL, MuscleLegSpreadR,
                MuscleKneeL, MuscleKneeR, MuscleFootL, MuscleFootR);
        }
    }
}
