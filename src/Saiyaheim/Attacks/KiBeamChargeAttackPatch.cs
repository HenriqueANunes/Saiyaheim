using HarmonyLib;

namespace Saiyaheim.Attacks
{
    /// <summary>
    /// Kamehameha carregando não ataca com a arma nem com o soco. Pedido pelo Henrique em
    /// 2026-09-17, depois do <see cref="KiBeamChargeBlockPatch"/>: as mãos estão ocupadas com a
    /// carga.
    ///
    /// <b>Por que patch Harmony.</b> O ataque sai de <c>m_attack</c>/<c>m_attackHold</c>, campos
    /// protected que o <c>Player.SetControls</c> reescreve todo frame, e o
    /// <c>Humanoid.CustomFixedUpdate</c> os consome <b>antes</b> do <c>SEMan.Update</c> — a mesma
    /// armadilha de ordem do bloqueio.
    ///
    /// <b>Por que o <c>PlayerAttackInput</c>, e não o <c>StartAttack</c>.</b> Ele é o laço inteiro
    /// de ataque do jogador: primário, secundário, a fila de meio segundo e o puxar do arco. Barrar
    /// só o <c>StartAttack</c> deixaria o arco retesar durante a carga, e a fila guardaria o clique
    /// para disparar o golpe no instante em que o Kamehameha sai. É privado, mas Harmony patcheia
    /// pelo nome sem problema; se sumir numa atualização, o <c>PatchAll</c> reclama no log.
    ///
    /// ⚠️ Pular o método inteiro também pula o <c>UpdateWeaponLoading</c> (besta) e o fim de ataque
    /// em laço. Durante a carga não há ataque em curso para encerrar, e a recarga da besta só
    /// espera a carga acabar.
    /// </summary>
    [HarmonyPatch(typeof(Player), "PlayerAttackInput")]
    internal static class KiBeamChargeAttackPatch
    {
        private static bool Prefix(Player __instance)
        {
            return !KiBeamCharge.IsCharging(__instance);
        }
    }
}
