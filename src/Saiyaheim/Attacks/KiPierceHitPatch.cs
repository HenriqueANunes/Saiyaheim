using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Saiyaheim.Attacks
{
    /// <summary>
    /// Um ataque que atravessa acerta cada <b>personagem</b> uma vez só, e não cada collider.
    ///
    /// <b>O bug (playtest de 2026-09-28).</b> O <c>m_onlyStopOnTerrain</c> do jogo guarda o que já
    /// acertou num <c>HashSet&lt;Collider&gt;</c>. Um boss tem mais de um collider — corpo, cabeça,
    /// caixas de acerto —, e o Kienzan passava por dois deles e cobrava o golpe duas vezes.
    ///
    /// <b>Por que patch Harmony, contra a regra do projeto.</b> O dano acontece dentro do
    /// <c>OnHit</c>, e o jogo não expõe nada antes dele: o <c>m_onHit</c> é chamado depois do
    /// golpe, e o <c>IHitProjectile</c> é do alvo, não do projétil. O prefixo é o único ponto.
    ///
    /// <b>Só personagem.</b> Árvore e minério continuam por collider, de propósito: num
    /// <c>MineRock5</c> cada collider é um pedaço da rocha com vida própria, e cortar três pedaços
    /// no caminho é o disco fazendo o que deve.
    ///
    /// Barato para todo projétil que não é nosso: um <c>GetComponent</c> por impacto, e impacto é
    /// evento raro. O <see cref="KiPierceMemory"/> só existe em projétil do mod que atravessa, e só
    /// na máquina do dono — que é a única onde o <c>OnHit</c> roda.
    /// </summary>
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
    internal static class KiPierceHitPatch
    {
        private static bool Prefix(Projectile __instance, Collider collider)
        {
            if (collider == null)
            {
                return true;
            }

            KiPierceMemory memory = __instance.GetComponent<KiPierceMemory>();
            if (memory == null)
            {
                return true;
            }

            Character character = collider.GetComponentInParent<Character>();
            if (character == null)
            {
                return true;
            }

            // Add devolve false quando já estava: segundo collider do mesmo bicho, sem golpe.
            return memory.Hit.Add(character);
        }
    }

    /// <summary>
    /// Os personagens que este projétil já cortou. Posto pelo <c>KiProjectile.Defuse</c> no
    /// projétil de um ataque que atravessa; morre com ele.
    /// </summary>
    internal sealed class KiPierceMemory : MonoBehaviour
    {
        internal readonly HashSet<Character> Hit = new HashSet<Character>();
    }
}
