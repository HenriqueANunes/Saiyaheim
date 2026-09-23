using HarmonyLib;
using UnityEngine;

namespace Saiyaheim.Flight
{
    /// <summary>
    /// Decolar nadando: um empurrão para cima até o jogador sair da água, e só então o voo.
    ///
    /// <b>Por que existe.</b> O <c>Character.UpdateMotion</c> testa <c>IsSwimming()</c> antes de
    /// <c>m_flying</c>: nadando, o <c>UpdateFlying</c> nunca roda, com ou sem o
    /// <see cref="SE_Flight"/> ativo. E o <c>IsSwimming()</c> só fica falso 0.5s depois de o corpo
    /// sair da profundidade de nado — o <c>UpdateWater</c> zera o <c>m_swimTimer</c> a cada passo
    /// de física enquanto está fundo. Então o jogador precisa sair da água por conta própria
    /// antes de o voo assumir, e o <c>UpdateSwimming</c> sozinho o prende na superfície.
    ///
    /// <b>O que faz.</b> Durante a decolagem na água (<see cref="FlightManager.IsTakingOffFromWater"/>)
    /// o nado vanilla roda inteiro — o controle horizontal continua dele — e este postfix
    /// sobrescreve só a velocidade vertical. Postfix e não prefix: o <c>UpdateSwimming</c> puxa a
    /// velocidade vertical de volta para a linha d'água, e escrever antes dele seria desfeito no
    /// mesmo passo. Assim que o <c>IsSwimming()</c> vira falso o <c>UpdateMotion</c> desvia para
    /// o <c>UpdateFlying</c>, que desliga a gravidade e freia a subida: o jogador fica pairando
    /// alguns metros acima da água.
    ///
    /// <b>Voar debaixo d'água continua proibido</b>, por decisão de design: fora da decolagem,
    /// nadar derruba o voo em <c>FlightManager.GetStopReason</c>.
    ///
    /// <b>Por que é patch Harmony.</b> Mesma questão de ordem do <see cref="FlightPosePatch"/>:
    /// quem escreve a velocidade por último no passo de física é o <c>UpdateSwimming</c>, dentro
    /// do <c>UpdateMotion</c> privado. Escrever de qualquer outro lugar perde para ele.
    /// </summary>
    [HarmonyPatch(typeof(Character), "UpdateSwimming")]
    internal static class WaterTakeOffPatch
    {
        /// <summary>
        /// Velocidade vertical do empurrão, em m/s. Calibrada no playtest de 2026-09-23 (o
        /// primeiro chute, 6, jogava o jogador alto demais); saiu da config no mesmo dia por ser
        /// sensação, não balanceamento. O jogador para pairando um pouco acima da água.
        /// </summary>
        private const float TakeOffSpeed = 3f;

        // ___m_body: o campo é protected na assembly real. Ler __instance.m_body compilaria
        // contra a publicizada e estouraria FieldAccessException em runtime.
        private static void Postfix(Character __instance, Rigidbody ___m_body)
        {
            if (!FlightManager.IsTakingOffFromWater || ___m_body == null
                || !(__instance is Player player) || player != Player.m_localPlayer
                || !FlightManager.IsFlying(player))
            {
                return;
            }

            Vector3 velocity = ___m_body.linearVelocity;
            velocity.y = TakeOffSpeed;
            ___m_body.linearVelocity = velocity;
        }
    }
}
