using Jotunn.Configs;

namespace Saiyaheim.Scouter
{
    /// <summary>
    /// Um tier de scouter: o metal que entra na receita, a bancada e o limite de leitura. Etapa 15,
    /// ver [[Scouter]].
    ///
    /// <b>O limite não é um número de poder: é uma criatura imaginária.</b> O tier guarda vida e
    /// dano por segundo, e o limite é o poder de luta que uma criatura assim teria pela fórmula de
    /// sempre (<c>k1 × vida + k2 × dps</c>, sem armadura, como toda criatura). Mudou um peso do
    /// poder de luta, o limite anda junto com os bichos. Ideia do Henrique, 2026-10-08. Vida e DPS
    /// são constantes desde 2026-10-09; ver <see cref="ScouterRegistry.All"/>.
    /// </summary>
    internal class ScouterTier
    {
        internal int Index { get; }

        /// <summary>Nome do metal em inglês, como aparece no nome do item.</summary>
        internal string Metal { get; }

        /// <summary>Prefab do material que a receita pede (<c>Flint</c>, <c>Bronze</c>, <c>YggdrasilWood</c>, <c>Gold</c>...).</summary>
        internal string MetalPrefab { get; }

        /// <summary>Prefab da bancada onde se faz.</summary>
        internal string Station { get; }

        /// <summary>Vida da criatura imaginária que define o limite.</summary>
        internal float LimitHealth { get; }

        /// <summary>Dano por segundo da criatura imaginária que define o limite.</summary>
        internal float LimitDps { get; }

        internal ScouterTier(int index, string metal, string metalPrefab, string station,
            float limitHealth, float limitDps)
        {
            Index = index;
            Metal = metal;
            MetalPrefab = metalPrefab;
            Station = station;
            LimitHealth = limitHealth;
            LimitDps = limitDps;
        }

        /// <summary>
        /// Limite de leitura em poder de luta <b>cru</b>, na mesma escala do <c>PowerRating.GetRaw</c>.
        /// Calculado toda vez: é uma multiplicação, e assim mudar um peso do poder de luta no
        /// <c>.cfg</c> vale na hora, sem cache para invalidar.
        /// </summary>
        internal float GetLimitRaw()
        {
            return SaiyaheimConfig.RatingK1Health.Value * LimitHealth
                   + SaiyaheimConfig.RatingK2Damage.Value * LimitDps;
        }

        internal static string Workbench => CraftingStations.Workbench;

        internal static string Forge => CraftingStations.Forge;

        internal static string BlackForge => CraftingStations.BlackForge;
    }
}
