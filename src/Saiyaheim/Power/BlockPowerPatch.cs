using System;
using HarmonyLib;
using Saiyaheim.Ki;

namespace Saiyaheim.Power
{
    /// <summary>
    /// Faz o bloqueio escalar com o <see cref="PowerLevel"/>, substituindo o block power do item —
    /// mesma regra da armadura.
    ///
    /// <b>Por que é patch Harmony, e não hook nativo.</b> A lista de <c>Modify*</c> do
    /// <c>StatusEffect</c> tem <c>ModifyArmorMods</c>, que o mod já usa, mas <b>nada equivalente
    /// para bloqueio</b>. O único parente é o <c>ModifyTimedBlockBonus</c>, que só entra no parry
    /// (janela de 0,25 s) e multiplica um valor que continuaria sendo 2. Não há caminho nativo;
    /// esta é a diferença que obrigou o patch, e ela foi verificada na decompilação antes.
    ///
    /// <b>Por que são dois alvos e não um.</b> O <c>GetBlockPower</c> é método do <b>item</b> e não
    /// recebe personagem nenhum — não dá para saber de quem é o bloqueio olhando só para ele. E o
    /// <c>PlayerUnarmed</c> é um <c>ItemData</c> de <b>prefab</b>, compartilhado: um humanoide
    /// inimigo desarmado que bloqueasse enquanto o ki do jogador está ligado bloquearia com o poder
    /// do jogador. É o mesmo vazamento contra o qual a doc alerta ao falar de escrever no
    /// <c>m_shared</c>, só que pela porta da leitura. O patch no <c>BlockAttack</c> existe só para
    /// marcar <i>quem</i> está bloqueando, e de quebra é onde o custo de ki é medido.
    ///
    /// <b>Os dois alvos são públicos e de assinatura estável</b>, então a superfície de quebra numa
    /// atualização do Valheim é pequena — mesmo critério que aprovou o <see cref="DamageXpPatch"/>.
    ///
    /// ⚠️ O tooltip do item continua vanilla. O <c>GetBlockPowerTooltip</c> passa pelo mesmo
    /// <c>GetBlockPower</c>, mas fora do <c>BlockAttack</c>, então o marcador está desligado e o
    /// escudo na mochila mostra o número dele. É a leitura certa: o número do item é o que valeria
    /// com o ki desligado.
    /// </summary>
    internal static class BlockPowerPatch
    {
        /// <summary>
        /// Ligado apenas durante o <c>Humanoid.BlockAttack</c> do jogador local, com o ki ligado.
        /// Fora dessa janela o <c>GetBlockPower</c> devolve o valor do item, intocado.
        ///
        /// Estático simples e não pilha: o <c>BlockAttack</c> não é reentrante — roda dentro do
        /// <c>RPC_Damage</c>, na thread principal, um golpe de cada vez — e o <c>Finalizer</c>
        /// garante o desligamento até se o método estourar.
        /// </summary>
        private static bool _localPlayerIsBlocking;

        /// <summary>
        /// Marca a janela em que o bloqueio é do jogador local, e mede quanto o bloqueio barrou.
        ///
        /// A medida é a diferença do <c>HitData</c> antes e depois: o <c>BlockAttack</c> chama
        /// <c>hit.BlockDamage(...)</c> no <b>mesmo objeto</b> que recebemos. Não é estimativa como
        /// a do <see cref="SE_KiBody"/> — aqui o número é exato, e um bloqueio que falha (por
        /// stagger ou falta de stamina) não chama o <c>BlockDamage</c>, então a diferença é zero e
        /// nada é cobrado, sem precisar de teste algum.
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), "BlockAttack", typeof(HitData), typeof(Character))]
        internal static class BlockAttackPatch
        {
            private static void Prefix(Humanoid __instance, HitData hit, out float __state)
            {
                __state = -1f;

                if (__instance == null || hit == null ||
                    __instance != Player.m_localPlayer || !KiManager.IsEnabled)
                {
                    return;
                }

                _localPlayerIsBlocking = true;
                __state = hit.GetTotalBlockableDamage();
            }

            /// <summary>
            /// <c>Finalizer</c> e não <c>Postfix</c> porque ele roda <b>também quando o método
            /// estoura</b>. Um marcador estático que vaza por causa de uma exceção ficaria ligado
            /// para sempre, e o efeito seria dar o poder do jogador a todo humanoide do mundo —
            /// falha silenciosa, do tipo que só aparece semanas depois.
            ///
            /// O <c>void</c> é deliberado: um finalizer que devolve <c>null</c> engole a exceção.
            /// Este só limpa e sai, deixando o erro subir.
            /// </summary>
            private static void Finalizer(HitData hit, float __state, Exception __exception)
            {
                if (__state < 0f)
                {
                    return;
                }

                _localPlayerIsBlocking = false;

                if (__exception != null || hit == null)
                {
                    return;
                }

                float rate = SaiyaheimConfig.BlockKiCost.Value;
                if (rate <= 0f)
                {
                    return;
                }

                float blocked = __state - hit.GetTotalBlockableDamage();
                if (blocked <= 0f)
                {
                    return;
                }

                // Drain e não TryConsume: quando chegamos aqui o bloqueio já aconteceu e o dano já
                // foi reduzido. Não há o que cancelar, então a barra vazia simplesmente deixa de
                // pagar o que falta — igual ao custo por dano recebido, e ao contrário do soco.
                KiManager.Drain(blocked * rate);

                SaiyaheimPlugin.LogVerbose(
                    $"Ki block stopped {blocked:0.#} damage → {blocked * rate:0.#} ki " +
                    $"({KiManager.Current:0.#} left).");
            }
        }

        /// <summary>
        /// Troca o block power do item pelo do poder, e só dentro da janela do
        /// <see cref="BlockAttackPatch"/>.
        ///
        /// <b>Substitui, não soma</b> — mesma decisão do <c>ModifyArmorMods</c>: quem usa ki abre
        /// mão da build vanilla inteira. Segurar o melhor escudo do jogo não muda nada enquanto o
        /// toggle está ligado, e desligar devolve o escudo na hora.
        ///
        /// Patchar a sobrecarga <c>(int, float)</c> cobre as duas: a de um argumento só delega
        /// para esta.
        /// </summary>
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetBlockPower),
            typeof(int), typeof(float))]
        internal static class GetBlockPowerPatch
        {
            private static void Postfix(ref float __result)
            {
                if (!_localPlayerIsBlocking)
                {
                    return;
                }

                float blockPower = PowerLevel.GetBlockPower(Player.m_localPlayer);

                // ⚠️ Rede de segurança contra zero, não checagem defensiva de rotina. O
                // BlockAttack divide pelo block power sem checar, e 0 vira NaN, que vira stamina
                // NaN permanente — ver o aviso no PowerLevel.GetBlockPower. O GetBlockPower já
                // garante o piso, mas o BlockPowerBase é config: alguém que ponha 0 lá reabriria
                // o mesmo bug. Zero aqui significa "não substitui", e o item devolve o valor dele.
                if (blockPower > 0f)
                {
                    __result = blockPower;
                }
            }
        }
    }
}
