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
        ///
        /// <b>Limites</b> (vida e DPS da criatura imaginária, ver <see cref="ScouterTier"/>) testados
        /// no jogo em 2026-10-09, pela tabela de Criaturas do vault (sem dano de construção, DPS
        /// somando os ataques e veneno pelo tick). Saíram do <c>.cfg</c> no mesmo dia. Com os pesos
        /// de 2026-09-06 o limite é <c>0,8 × vida + 10 × dps</c>.
        ///
        /// Critério: o comum de cada bioma só é lido a partir do tier do bioma dele. Flint lê o
        /// Greyling (41), com folga até 90 a pedido do Henrique; Bronze, o Greydwarf (114) e não o Draugr (220); Iron, o Swamp e não o Wolf
        /// (484); Silver, o Wolf e não o Fuling (518); Black Metal, o Fuling e não o Seeker (1.325);
        /// Yggdrasil, o Seeker e não o Charred Warrior (2.017); Flametal, o Charred e não o Krigen
        /// (2.295); Bloodgold, o Deep North. As criaturas grandes de cada bioma ficam para o tier
        /// seguinte.
        /// </summary>
        internal static ScouterTier[] All
        {
            get
            {
                if (_all == null)
                {
                    _all = new[]
                    {
                        // Meadows: não tem metal, então pederneira na workbench, a única bancada
                        // do bioma (2026-10-09). É o tier 0 para o Bronze continuar sendo o 1.
                        new ScouterTier(0, "Flint", "Flint", ScouterTier.Workbench, 75f, 3f),
                        new ScouterTier(1, "Bronze", "Bronze", ScouterTier.Forge, 120f, 10.4f),
                        new ScouterTier(2, "Iron", "Iron", ScouterTier.Forge, 250f, 22f),
                        new ScouterTier(3, "Silver", "Silver", ScouterTier.Forge, 300f, 26f),
                        new ScouterTier(4, "Black Metal", "BlackMetal", ScouterTier.Forge, 500f, 60f),
                        // Mistlands: Yggdrasil Wood e não eitr refinado (2026-10-08). O material do
                        // eitr é liso com brilho próprio e no jogo saiu verde-claro; a madeira tem
                        // textura, como os metais.
                        new ScouterTier(5, "Yggdrasil", "YggdrasilWood", ScouterTier.BlackForge, 950f, 84f),
                        new ScouterTier(6, "Flametal", "FlametalNew", ScouterTier.BlackForge, 1250f, 110f),
                        new ScouterTier(7, "Bloodgold", "Gold", ScouterTier.BlackForge, 2000f, 300f),
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
        /// Os registrados nesta sessão. Servem para a dica (<see cref="ScouterTooltip"/>).
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
                    new RequirementConfig(tier.MetalPrefab, SaiyaheimConfig.ScouterMetalAmount),
                    new RequirementConfig(lens.ColorPrefab, SaiyaheimConfig.ScouterColorAmount),
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
    }
}
