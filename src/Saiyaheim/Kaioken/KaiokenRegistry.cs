using System.Collections.Generic;

namespace Saiyaheim.Kaioken
{
    /// <summary>
    /// Os cinco tiers de Kaioken e as perguntas que o resto do mod faz sobre eles (etapa 14).
    ///
    /// <b>O tier ativo mora no <c>SEMan</c></b>, como a forma: um <see cref="SE_Kaioken"/> por tier,
    /// identificado pelo hash do nome. Nada aqui guarda estado próprio.
    /// </summary>
    internal static class KaiokenRegistry
    {
        /// <summary>
        /// Na ordem da escada. A posição é a ordem estrita de aprender e a de subir com a tecla.
        /// Os nomes entram no id do tier, que vai para o save do personagem: não renomear.
        /// </summary>
        internal static readonly KaiokenTier[] All = BuildTiers();

        private static KaiokenTier[] BuildTiers()
        {
            string[] names = { "x2", "x3", "x4", "x10", "x20" };
            var tiers = new KaiokenTier[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                tiers[i] = new KaiokenTier(i, names[i], SaiyaheimConfig.KaiokenTiers[i]);
            }

            return tiers;
        }

        /// <summary>
        /// O tier cujo nome ou id casa com <paramref name="name"/>, ou null. Aceita "x3", "3" e
        /// "kaioken_x3", sem diferença de caixa — é o que o console digita.
        /// </summary>
        internal static KaiokenTier Find(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (KaiokenTier tier in All)
            {
                if (string.Equals(tier.Name, name, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tier.Name, "x" + name, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tier.Id, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return tier;
                }
            }

            return null;
        }

        /// <summary>O tier ligado neste jogador, ou null.</summary>
        internal static KaiokenTier GetActive(Player player)
        {
            SEMan seman = player == null ? null : player.GetSEMan();
            if (seman == null)
            {
                return null;
            }

            foreach (KaiokenTier tier in All)
            {
                if (seman.HaveStatusEffect(tier.NameHashValue))
                {
                    return tier;
                }
            }

            return null;
        }

        /// <summary>
        /// Quanto o Kaioken multiplica o poder de combate. 1 desligado.
        ///
        /// ⚠️ <b>Não pode ler battle power nenhum</b>: é o <c>BattlePower.GetKiCombatRaw</c> quem
        /// chama, pela mesma regra do <c>TransformationRegistry.GetPowerMultiplier</c>.
        /// </summary>
        internal static float GetPowerMultiplier(Player player)
        {
            KaiokenTier active = GetActive(player);

            return active == null ? 1f : active.GetPowerMultiplier();
        }

        /// <summary>Quanto o teto de ki cresce com o Kaioken ligado. 1 desligado.</summary>
        internal static float GetMaxKiMultiplier(Player player)
        {
            KaiokenTier active = GetActive(player);

            return active == null ? 1f : 1f + active.GetMaxKiBonus();
        }

        /// <summary>O tier aprendido logo acima de <paramref name="current"/>, ou null se não há.</summary>
        internal static KaiokenTier NextLearned(Player player, KaiokenTier current)
        {
            int start = current == null ? 0 : current.Index + 1;

            for (int i = start; i < All.Length; i++)
            {
                if (All[i].IsLearned(player))
                {
                    return All[i];
                }
            }

            return null;
        }

        /// <summary>O tier mais alto aprendido, ou null se nenhum.</summary>
        internal static KaiokenTier HighestLearned(Player player)
        {
            KaiokenTier highest = null;

            foreach (KaiokenTier tier in All)
            {
                if (tier.IsLearned(player))
                {
                    highest = tier;
                }
            }

            return highest;
        }

        internal static IEnumerable<int> AllNameHashes()
        {
            foreach (KaiokenTier tier in All)
            {
                yield return tier.NameHashValue;
            }
        }
    }
}
