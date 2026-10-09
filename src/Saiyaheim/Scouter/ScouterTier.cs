using Jotunn.Configs;

namespace Saiyaheim.Scouter
{
    /// <summary>
    /// Um tier de scouter: o metal que entra na receita, a bancada e o limite de leitura. Etapa 15,
    /// ver [[Scouter]].
    ///
    /// <b>O limite não é um número no código nem no <c>.cfg</c>: é uma criatura imaginária.</b> O
    /// config guarda vida e dano por segundo, e o limite é o poder de luta que uma criatura assim
    /// teria pela fórmula de sempre (<c>k1 × vida + k2 × dps</c>, sem armadura, como toda
    /// criatura). Mudou um peso do poder de luta, o limite anda junto com os bichos. Ideia do
    /// Henrique, 2026-10-08.
    /// </summary>
    internal class ScouterTier
    {
        internal int Index { get; }

        /// <summary>Nome do metal em inglês, como aparece no nome do item e na seção do <c>.cfg</c>.</summary>
        internal string Metal { get; }

        /// <summary>Prefab do material que a receita pede (<c>Flint</c>, <c>Bronze</c>, <c>YggdrasilWood</c>, <c>Gold</c>...).</summary>
        internal string MetalPrefab { get; }

        /// <summary>Prefab da bancada onde se faz.</summary>
        internal string Station { get; }

        internal SaiyaheimConfig.ScouterTierConfig Config { get; }

        internal ScouterTier(int index, string metal, string metalPrefab, string station,
            SaiyaheimConfig.ScouterTierConfig config)
        {
            Index = index;
            Metal = metal;
            MetalPrefab = metalPrefab;
            Station = station;
            Config = config;
        }

        /// <summary>
        /// Limite de leitura em poder de luta <b>cru</b>, na mesma escala do <c>PowerRating.GetRaw</c>.
        /// Lido do config toda vez: é uma multiplicação, e assim mudar peso ou limite no <c>.cfg</c>
        /// vale na hora, sem cache para invalidar.
        /// </summary>
        internal float GetLimitRaw()
        {
            return SaiyaheimConfig.RatingK1Health.Value * System.Math.Max(0f, Config.LimitHealth.Value)
                   + SaiyaheimConfig.RatingK2Damage.Value * System.Math.Max(0f, Config.LimitDps.Value);
        }

        internal static string Workbench => CraftingStations.Workbench;

        internal static string Forge => CraftingStations.Forge;

        internal static string BlackForge => CraftingStations.BlackForge;
    }
}
