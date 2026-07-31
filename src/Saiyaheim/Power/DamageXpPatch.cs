using HarmonyLib;
using UnityEngine;

namespace Saiyaheim.Power
{
    /// <summary>
    /// Alimenta o XP de <see cref="PowerSkill"/> a partir do dano que passa pela luta.
    ///
    /// <b>O primeiro e único patch Harmony do mod</b>, e a exceção à regra do projeto. O motivo:
    /// não existe API nativa que entregue "quanto dano eu de fato causei". Os candidatos
    /// avaliados e por que não servem:
    /// <list type="bullet">
    /// <item><c>StatusEffect.ModifyAttack</c> — roda no atacante, mas não sabe o alvo nem quanto
    /// dano sobreviveu à armadura dele;</item>
    /// <item><c>StatusEffect.OnDamaged</c> — roda no alvo, e <b>antes</b> da armadura e das
    /// resistências, que é justamente o que precisa ser descontado;</item>
    /// <item><c>Character.m_onDamaged</c> — é público e traz o valor certo, mas obrigaria a
    /// assinar o evento de toda criatura carregada e a gerenciar o ciclo de vida dessas
    /// assinaturas a cada tick. Mais frágil que o patch, não menos.</item>
    /// </list>
    ///
    /// <c>ApplyDamage</c> é público e tem assinatura estável, então a superfície de quebra numa
    /// atualização do Valheim é pequena.
    ///
    /// ⚠️ <b>Multiplayer (etapa 8):</b> <c>ApplyDamage</c> roda no <b>dono</b> do alvo. Em
    /// singleplayer isso é sempre o jogador local; num servidor, acertar um bicho que outro
    /// cliente possui não passa por aqui. Resolver junto do resto da sincronização.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    internal static class DamageXpPatch
    {
        /// <summary>HP antes do golpe, para descontar o overkill.</summary>
        private static void Prefix(Character __instance, out float __state)
        {
            __state = __instance == null ? 0f : __instance.GetHealth();
        }

        private static void Postfix(Character __instance, HitData hit, float __state)
        {
            Player local = Player.m_localPlayer;
            if (local == null || __instance == null || hit == null)
            {
                return;
            }

            // HP realmente perdido, não o dano bruto do hit. É o que desconta o overkill de graça:
            // um soco de 5000 num Boar de 10 de HP conta 10, e conteúdo de tier baixo para de
            // pagar sozinho conforme o jogador cresce.
            float applied = Mathf.Clamp(__state - __instance.GetHealth(), 0f, __state);
            if (applied <= 0f)
            {
                return;
            }

            if (__instance == local)
            {
                // Dano recebido: já depois da armadura e das resistências, porque ApplyDamage é
                // chamado depois delas. É o que torna inútil apanhar de propósito de bicho fraco.
                PowerSkill.RaiseFromDamageTaken(local, applied);
                SaiyaheimPlugin.LogVerbose($"Battle Power XP: took {applied:0.#} damage.");
            }
            else if (hit.GetAttacker() == local)
            {
                PowerSkill.RaiseFromDamageDealt(local, applied);
                SaiyaheimPlugin.LogVerbose($"Battle Power XP: dealt {applied:0.#} damage.");
            }
        }
    }
}
