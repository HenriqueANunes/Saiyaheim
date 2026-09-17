using HarmonyLib;

namespace Saiyaheim.Attacks
{
    /// <summary>
    /// Kamehameha carregando não bloqueia. Segurar o botão de bloqueio no meio da carga não ergue a
    /// guarda, não faz a animação e não barra dano. Pedido pelo Henrique em 2026-09-17, junto com
    /// o <see cref="KiBeamCharge.HoldStill"/>: carregar é se expor.
    ///
    /// <b>Por que patch Harmony, contra a regra do projeto.</b> O bloqueio não passa pelo
    /// <c>m_moveDir</c>, então o <c>HoldStill</c> não o alcança. O estado é o
    /// <c>Character.m_blocking</c>, que é <b>protected</b> e é reescrito pelo
    /// <c>Player.SetControls</c> todo frame. Zerar o campo por reflexão de dentro do <c>SEMan</c>
    /// não serve: o <c>Humanoid.CustomFixedUpdate</c> roda o <c>UpdateBlock</c> <b>antes</b> do
    /// <c>SEMan.Update</c>, e o dano pode chegar entre um frame e outro com o campo já religado.
    ///
    /// O <c>IsBlocking</c> é o ponto único que todo mundo consulta: a animação e a ZDO (no
    /// <c>UpdateBlock</c>), o dano (no <c>Character.RPC_Damage</c>, antes do <c>BlockAttack</c>) e
    /// a stamina. É público e de assinatura estável.
    ///
    /// Esquiva (bloqueio + pulo) continua passando, porque o <c>SetControls</c> lê o campo cru, e
    /// ela cancela a carga no <see cref="KiAttackManager"/>.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.IsBlocking))]
    internal static class KiBeamChargeBlockPatch
    {
        private static void Postfix(Humanoid __instance, ref bool __result)
        {
            // Barato para o caso comum: sem bloqueio não há o que desfazer, e o IsCharging já
            // descarta todo personagem que não é o jogador local.
            if (__result && __instance is Player player && KiBeamCharge.IsCharging(player))
            {
                __result = false;
            }
        }
    }
}
