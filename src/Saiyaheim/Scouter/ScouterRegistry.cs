using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Saiyaheim.Scouter
{
    /// <summary>
    /// Os oito tiers e as quatro lentes do scouter, e o registro dos 32 itens. Etapa 15.
    ///
    /// <b>Um item por cor de lente, e não variante do item</b> (decisão de 2026-10-08): o jogo veste
    /// capacete sempre com a variante 0 e só manda o hash do item pela rede, então com variantes os
    /// outros jogadores veriam a lente errada. Com um item por cor, o hash já é a cor.
    ///
    /// Os itens são clones do <see cref="PlaceholderSource"/>, que empresta a estrutura de capacete
    /// (o <c>attach</c> que o jogo prende na cabeça, o collider); a malha é
    /// trocada pela do scouter no <see cref="ScouterModel"/>.
    /// </summary>
    internal static class ScouterRegistry
    {
        /// <summary>Capacete que empresta a estrutura de capacete. O ícone é o do <see cref="ScouterIcon"/>.</summary>
        private const string PlaceholderSource = "HelmetLeather";

        private const string PrefabPrefix = "SaiyaheimScouter_";

        internal struct Lens
        {
            internal string Name;
            internal string ColorPrefab;
        }

        /// <summary>
        /// As quatro lentes e o material de cor de cada uma. Fixo por cor e todo da Meadows/Black
        /// Forest: qualquer lente sai já no primeiro tier, e quem gosta de uma cor a mantém ao subir.
        /// </summary>
        internal static readonly Lens[] Lenses =
        {
            new Lens { Name = "Red", ColorPrefab = "Raspberry" },
            new Lens { Name = "Green", ColorPrefab = "GreydwarfEye" },
            new Lens { Name = "Blue", ColorPrefab = "Blueberries" },
            new Lens { Name = "Purple", ColorPrefab = "Thistle" },
        };

        private static ScouterTier[] _all;

        /// <summary>
        /// Em ordem de progressão. O Bloodgold vai para a Black Forge porque a bancada do Deep North,
        /// a Frost Foundry, é forno de fundir e não aceita receita (conferido no prefab em 2026-10-08).
        /// </summary>
        internal static ScouterTier[] All
        {
            get
            {
                if (_all == null)
                {
                    var c = SaiyaheimConfig.ScouterTiers;
                    _all = new[]
                    {
                        // Meadows: não tem metal, então pederneira na workbench, a única bancada
                        // do bioma (2026-10-09). É o tier 0 para o Bronze continuar sendo o 1.
                        new ScouterTier(0, "Flint", "Flint", ScouterTier.Workbench, c[0]),
                        new ScouterTier(1, "Bronze", "Bronze", ScouterTier.Forge, c[1]),
                        new ScouterTier(2, "Iron", "Iron", ScouterTier.Forge, c[2]),
                        new ScouterTier(3, "Silver", "Silver", ScouterTier.Forge, c[3]),
                        new ScouterTier(4, "Black Metal", "BlackMetal", ScouterTier.Forge, c[4]),
                        // Mistlands: Yggdrasil Wood e não eitr refinado (2026-10-08). O material do
                        // eitr é liso com brilho próprio e no jogo saiu verde-claro; a madeira tem
                        // textura, como os metais.
                        new ScouterTier(5, "Yggdrasil", "YggdrasilWood", ScouterTier.BlackForge, c[5]),
                        new ScouterTier(6, "Flametal", "FlametalNew", ScouterTier.BlackForge, c[6]),
                        new ScouterTier(7, "Bloodgold", "Gold", ScouterTier.BlackForge, c[7]),
                    };
                }

                return _all;
            }
        }

        /// <summary>Nome do prefab de cada scouter → tier dele. É por aqui que o leitor reconhece o capacete.</summary>
        private static readonly Dictionary<string, ScouterTier> TierByPrefab = new Dictionary<string, ScouterTier>();

        /// <summary>Um scouter registrado: o item do Jotunn, o tier e os dados do prefab.</summary>
        internal class Entry
        {
            internal CustomItem Item;
            internal ScouterTier Tier;

            /// <summary>
            /// Os dados do <b>prefab</b>. Item feito na bancada ou carregado do save aponta para
            /// estes; item que nasceu no mundo (spawn, caído no chão) e foi apanhado tem cópia
            /// própria — ver <see cref="ScouterTooltip"/>.
            /// </summary>
            internal ItemDrop.ItemData.SharedData Shared;
        }

        private static readonly Dictionary<string, Entry> EntryByPrefab = new Dictionary<string, Entry>();

        /// <summary>O registro de um item, ou null se ele não for scouter.</summary>
        internal static Entry EntryOf(ItemDrop.ItemData item)
        {
            GameObject prefab = item?.m_dropPrefab;
            if (prefab == null)
            {
                return null;
            }

            return EntryByPrefab.TryGetValue(prefab.name, out Entry entry) ? entry : null;
        }

        /// <summary>
        /// Os registrados nesta sessão. Servem para reescrever as quantidades da receita quando o
        /// config muda (inclusive o do servidor) e para a dica (<see cref="ScouterTooltip"/>).
        /// </summary>
        internal static readonly List<Entry> Entries = new List<Entry>();

        internal static void Register()
        {
            foreach (ScouterTier tier in All)
            {
                foreach (Lens lens in Lenses)
                {
                    TierByPrefab[PrefabName(tier, lens)] = tier;
                }
            }

            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabsAvailable;
            SaiyaheimConfig.ScouterMetalAmount.SettingChanged += (_, __) => ApplyAmounts();
            SaiyaheimConfig.ScouterColorAmount.SettingChanged += (_, __) => ApplyAmounts();
        }

        internal static string PrefabName(ScouterTier tier, Lens lens)
        {
            return PrefabPrefix + tier.Metal.Replace(" ", string.Empty) + "_" + lens.Name;
        }

        /// <summary>O tier de um item, ou null se ele não for scouter.</summary>
        internal static ScouterTier TierOf(ItemDrop.ItemData item)
        {
            GameObject prefab = item?.m_dropPrefab;
            if (prefab == null)
            {
                return null;
            }

            return TierByPrefab.TryGetValue(prefab.name, out ScouterTier tier) ? tier : null;
        }

        private static void OnVanillaPrefabsAvailable()
        {
            foreach (ScouterTier tier in All)
            {
                foreach (Lens lens in Lenses)
                {
                    RegisterItem(tier, lens);
                }
            }

            ScouterIcon.RenderAll();
        }

        private static void RegisterItem(ScouterTier tier, Lens lens)
        {
            string name = PrefabName(tier, lens);

            // O evento pode disparar mais de uma vez numa sessão (voltar ao menu e entrar de novo).
            if (PrefabManager.Instance.GetPrefab(name) != null)
            {
                return;
            }

            var config = new ItemConfig
            {
                Name = $"{tier.Metal} Scouter ({lens.Name.ToLowerInvariant()} lens)",
                // Curta de propósito (pedido do Henrique, 2026-10-08): a dica completa, com o limite
                // e os números, é o ScouterTooltip que escreve; esta só aparece antes da primeira
                // reescrita.
                Description = "Reads battle power.",
                CraftingStation = tier.Station,
                MinStationLevel = 1,
                Requirements = new[]
                {
                    new RequirementConfig(tier.MetalPrefab, SaiyaheimConfig.ScouterMetalAmount.Value),
                    new RequirementConfig(lens.ColorPrefab, SaiyaheimConfig.ScouterColorAmount.Value),
                },
            };

            var item = new CustomItem(name, PlaceholderSource, config);
            if (item.ItemPrefab == null)
            {
                SaiyaheimPlugin.Log.LogError($"Could not clone '{PlaceholderSource}'. Scouter '{name}' is off this session.");
                return;
            }

            ScouterModel.Build(item.ItemPrefab, tier, lens.Name);

            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;

            // Aparelho, não armadura: sem defesa, sem desgaste, sem upgrade e sem o efeito de set
            // do capacete de couro que ele imita.
            shared.m_armor = 0f;
            shared.m_armorPerLevel = 0f;
            shared.m_maxQuality = 1;
            shared.m_useDurability = false;
            shared.m_weight = 0.5f;
            shared.m_movementModifier = 0f;
            shared.m_setName = string.Empty;
            shared.m_setSize = 0;
            shared.m_setStatusEffect = null;
            shared.m_equipStatusEffect = null;

            // Não esconde cabelo nem barba: o cabelo da forma é a metade do visual das transformações.
            shared.m_helmetHideHair = ItemDrop.ItemData.HelmetHairType.Default;
            shared.m_helmetHideBeard = ItemDrop.ItemData.HelmetHairType.Default;

            ItemManager.Instance.AddItem(item);
            var entry = new Entry { Item = item, Tier = tier, Shared = shared };
            Entries.Add(entry);
            EntryByPrefab[name] = entry;
        }

        /// <summary>
        /// Reescreve as quantidades das receitas já registradas. O config <c>AdminOnly</c> do
        /// servidor chega depois do registro, e sem isto a receita ficaria com o número local.
        /// </summary>
        private static void ApplyAmounts()
        {
            foreach (Entry entry in Entries)
            {
                Piece.Requirement[] resources = entry.Item.Recipe?.Recipe?.m_resources;
                if (resources == null || resources.Length < 2)
                {
                    continue;
                }

                resources[0].m_amount = SaiyaheimConfig.ScouterMetalAmount.Value;
                resources[1].m_amount = SaiyaheimConfig.ScouterColorAmount.Value;
            }
        }
    }
}
