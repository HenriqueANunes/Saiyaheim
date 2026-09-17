using HarmonyLib;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Attacks
{
    /// <summary>
    /// Veste de ki o projétil de outro jogador quando ele aparece nesta máquina.
    ///
    /// <b>O bug (multiplayer, 2026-09-16).</b> Cada jogador via o próprio ki blast certo e o dos
    /// outros com a cara do prefab do jogo. O <see cref="KiProjectile.Fire"/> instancia o clone e
    /// o pinta, escala e ajusta — mas tudo isso é escrita local no <c>GameObject</c>. O que
    /// atravessa a rede é a ZDO, e a ZDO só diz "este prefab, nesta posição". O cliente do amigo
    /// cria a cópia pelo <c>ZNetScene.CreateObject</c>, a partir do prefab cru, e ninguém a
    /// vestia.
    ///
    /// <b>Por que patch Harmony, contra a regra do projeto.</b> O jogo não tem evento de "objeto
    /// de rede apareceu". As alternativas eram varrer por frame todos os projéteis carregados
    /// atrás de ZDO com a nossa chave, ou pendurar no <c>Projectile.Awake</c> — que roda também
    /// para toda flecha do jogo e, pior, pode rodar antes do <c>ZNetView.Awake</c> dar a ZDO.
    /// O <c>CreateObject</c> é o ponto exato: só roda para cópia vinda da rede, e no postfix todos
    /// os <c>Awake</c> já aconteceram.
    ///
    /// <b>Também veste o estouro de impacto</b> (2026-09-17), que tinha o mesmo bug. Ver
    /// <see cref="StrippedEffect.NetTag"/>.
    ///
    /// <b>Não pega o projétil de quem atirou.</b> Esse nasce por <c>Instantiate</c> direto, e o
    /// <c>Fire</c> aplica o visual ali mesmo — pelo mesmo <see cref="KiProjectile.ApplyVisuals"/>.
    /// </summary>
    [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
    internal static class KiProjectileSyncPatch
    {
        private static void Postfix(ZDO zdo, GameObject __result)
        {
            if (__result == null || zdo == null)
            {
                return;
            }

            // Estouro de impacto montado pelo StrippedEffect. Vem antes do projétil porque é o
            // outro tipo de objeto nosso que chega por aqui; ver StrippedEffect.NetTag.
            string effectKey = zdo.GetString(StrippedEffect.NetKeyHash);
            if (!string.IsNullOrEmpty(effectKey))
            {
                StrippedEffect.ApplyRemote(__result, effectKey);
                return;
            }

            // Barato para o caso comum: um objeto que não é nosso sai no primeiro GetString, sem
            // procurar componente nenhum.
            string attackId = zdo.GetString(KiProjectile.AttackIdHash);
            if (string.IsNullOrEmpty(attackId))
            {
                return;
            }

            KiAttack attack = KiAttackRegistry.Find(attackId);
            Projectile projectile = __result.GetComponent<Projectile>()
                                    ?? __result.GetComponentInChildren<Projectile>();
            if (attack == null || projectile == null)
            {
                SaiyaheimPlugin.LogVerbose(
                    $"Remote ki projectile '{__result.name}': unknown attack '{attackId}' " +
                    "or no Projectile component. Leaving the prefab's look.");
                return;
            }

            // O ZNetView.Awake já aplicou a escala gravada na ZDO pelo dono, e o EffectScale mede
            // a escala base do transform na primeira chamada. Sem voltar ao tamanho do prefab, a
            // base medida seria a já escalada e o fator entraria duas vezes.
            GameObject prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
            if (prefab != null)
            {
                __result.transform.localScale = prefab.transform.localScale;
            }

            KiProjectile.ApplyVisuals(projectile, attack, zdo.GetFloat(KiProjectile.ChargeRatioHash, 1f));
        }
    }
}
