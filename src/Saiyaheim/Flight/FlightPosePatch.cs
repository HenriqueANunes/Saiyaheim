using HarmonyLib;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Flight
{
    /// <summary>
    /// A pose em pé: voar como o Goku e não como um saco de batatas caindo.
    ///
    /// <b>Por que existe.</b> O <c>Player_animator</c> vanilla <b>não tem</b> parâmetro
    /// <c>flying</c> — os ~130 parâmetros dele foram listados e não há nenhum com "fly". O
    /// <c>SetBool("flying", true)</c> que o <c>UpdateFlying</c> faz não tem para onde blendar.
    /// Pior: o <c>UpdateMotion</c> marca <c>falling = true</c> antes de desviar para o voo, porque
    /// <c>IsOnGround()</c> continua false no ar. Sem intervenção o jogador voa em queda livre.
    ///
    /// A saída é convencer o animator de que ele está de pé e parado: <c>falling = false</c>,
    /// <c>onGround = true</c> (só visual — <c>Character.IsOnGround()</c> continua false e nada de
    /// física muda) e as velocidades de blend em zero. O resultado é a pose idle transladando pelo
    /// ar. Custa três escritas por passo de física e nenhum asset novo.
    ///
    /// <b>Por que é patch Harmony, contra a regra do projeto.</b> É uma questão de ordem, não de
    /// preferência. Dentro de <c>Character.CustomFixedUpdate</c> a ordem é
    /// <c>SEMan.Update</c> → <c>UpdateMotion</c> → <c>UpdateFlying</c>, e é o <c>UpdateFlying</c>
    /// quem escreve esses parâmetros por último. Escrever do <c>SE_Flight</c> seria sobrescrito no
    /// mesmo passo; escrever do <c>Update</c> do plugin só funcionaria se o Animator do jogador
    /// estivesse em modo Normal, e nada garante que não esteja em AnimatePhysics — o
    /// <c>ZSyncAnimation</c> aplica os valores remotos com <c>fixedDeltaTime</c>, o que sugere
    /// justamente AnimatePhysics. Um postfix aqui é o único ponto correto nos dois modos.
    ///
    /// <c>Character.CustomFixedUpdate</c> é public e virtual, e o <c>Player</c> não a sobrescreve —
    /// é o alvo mais estável disponível. A física do voo continua sem patch nenhum.
    ///
    /// <b>Não validado na tela.</b> Riscos conhecidos: forçar <c>onGround</c> pode mexer em som de
    /// passos ou em alguma transição não mapeada. Por isso está atrás de
    /// <c>FlightForceIdlePose</c> — desligar devolve o comportamento vanilla na hora.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.CustomFixedUpdate))]
    internal static class FlightPosePatch
    {
        private static readonly int FallingHash = ZSyncAnimation.GetHash("falling");
        private static readonly int OnGroundHash = ZSyncAnimation.GetHash("onGround");
        private static readonly int ForwardSpeedHash = ZSyncAnimation.GetHash("forward_speed");
        private static readonly int SidewaySpeedHash = ZSyncAnimation.GetHash("sideway_speed");

        /// <summary>
        /// Guarda a guinada de antes do <c>UpdateFlying</c>, para o <see cref="LevelBody"/> poder
        /// devolvê-la quando o jogo a tiver destruído. <c>NaN</c> quer dizer "não estava voando".
        /// </summary>
        private static void Prefix(Character __instance, out float __state)
        {
            __state = __instance != null && __instance.IsFlying()
                ? __instance.transform.eulerAngles.y
                : float.NaN;
        }

        private static void Postfix(Character __instance, float __state)
        {
            // m_flying primeiro: é um campo bool, e para todo mundo que não está voando o postfix
            // custa isso e mais nada.
            if (__instance == null || !__instance.IsFlying() || !(__instance is Player player))
            {
                return;
            }

            // Voo de debug do jogo (tecla Z com cheats) não passa por aqui: quem não voa pelo mod
            // continua com a animação vanilla.
            if (!FlightManager.IsFlying(player))
            {
                return;
            }

            LevelBody(player, __state);

            if (!SaiyaheimConfig.FlightForceIdlePose.Value)
            {
                return;
            }

            ZSyncAnimation zanim = player.GetZAnim();
            if (zanim == null)
            {
                return;
            }

            // Pelo ZSyncAnimation, não pelo Animator direto: o SetBool/SetFloat dele escreve na
            // ZDO (438569 + hash) quando somos o dono, e é assim que os outros jogadores veem a
            // mesma pose sem nenhum RPC nosso — o mesmo mecanismo dos emotes de carregar ki.
            zanim.SetBool(FallingHash, false);
            zanim.SetBool(OnGroundHash, true);
            zanim.SetFloat(ForwardSpeedHash, 0f);
            zanim.SetFloat(SidewaySpeedHash, 0f);

            // E agora direto no Animator, sem passar pelo ZSyncAnimation de novo.
            //
            // Motivo: o SetFloat dele amortece forward_speed/sideway_speed em 0.2s
            // (m_smoothCharacterSpeeds). Com o UpdateFlying puxando para a velocidade real e nós
            // puxando para zero no mesmo passo, o valor local estabilizaria no meio do caminho e o
            // blend tree tocaria corrida no ar. A ZDO recebe o zero (ela guarda o alvo, não o
            // amortecido), então os outros jogadores já veriam a pose certa — quem veria errado é
            // justamente quem está pilotando.
            //
            // A ordem importa: primeiro o ZSyncAnimation, que compara com o valor amortecido e por
            // isso ainda escreve a ZDO; só depois o zero cru.
            Animator animator = GameAccess.GetAnimator(player);
            if (animator != null)
            {
                animator.SetFloat(ForwardSpeedHash, 0f);
                animator.SetFloat(SidewaySpeedHash, 0f);
            }
        }

        /// <summary>
        /// Tira a inclinação vertical do corpo, deixando só a guinada.
        ///
        /// <b>De onde vinha.</b> O <c>UpdateFlying</c> mira a rotação em
        /// <c>Quaternion.LookRotation(m_moveDir)</c> — e o <c>m_moveDir</c> carrega o componente Y
        /// que o <see cref="SE_Flight"/> escreve para subir e descer. Resultado: apertar para subir
        /// empinava o personagem de barriga para cima, e descer o punha de barriga para baixo.
        /// Não é bug do jogo; é o alvo de rotação sendo tridimensional enquanto o design pede que
        /// o corpo só gire na horizontal.
        ///
        /// <b>Por que aqui e não na pose.</b> Isto é a rotação do <c>transform</c>, que a física e
        /// o alvo de mira usam. Zerar em <c>LateUpdate</c> consertaria só o desenho e deixaria o
        /// personagem mirando para um lado e sendo desenhado para outro. O postfix de
        /// <c>CustomFixedUpdate</c> roda depois do <c>UpdateFlying</c> ter escrito a rotação, que é
        /// o momento certo.
        ///
        /// A inclinação que o design <i>quer</i> — barriga para baixo em velocidade — é puramente
        /// visual e mora em <see cref="FlightPose"/>, sobre o <c>bodyRotation</c>. Separar as duas
        /// é o que permite o corpo parecer deitado sem que a mira e a colisão deitem junto.
        /// </summary>
        private static void LevelBody(Player player, float previousYaw)
        {
            if (!SaiyaheimConfig.FlightLevelBody.Value)
            {
                return;
            }

            Vector3 moveDir = player.GetMoveDir();
            moveDir.y = 0f;

            // Subindo ou descendo sem nenhuma intenção horizontal, o alvo de rotação do jogo é
            // lixo: LookRotation de um vetor puramente vertical tem guinada **indefinida**, e a
            // Unity devolve zero — o norte do mapa. Achatar isso preservaria o norte, que foi
            // exatamente o que o playtest de 2026-07-31 viu.
            //
            // O limiar é o mesmo 0.1 que o UpdateFlying usa para decidir se rotaciona, de propósito:
            // abaixo dele o jogo não deveria ter mirado em nada.
            if (moveDir.sqrMagnitude < 0.01f)
            {
                if (!float.IsNaN(previousYaw))
                {
                    player.transform.rotation = Quaternion.Euler(0f, previousYaw, 0f);
                }

                return;
            }

            Vector3 forward = player.transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
            {
                return;
            }

            player.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }
}
