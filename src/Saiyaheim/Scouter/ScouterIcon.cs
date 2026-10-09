using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Rendering;

namespace Saiyaheim.Scouter
{
    /// <summary>
    /// O ícone de inventário de cada scouter, renderizado dentro do jogo pelo <c>RenderManager</c>
    /// do Jotunn. Etapa 15, fase 4.
    ///
    /// <b>Por que renderizar e não um PNG:</b> o ícone sai do próprio modelo, com os materiais e o
    /// shader reais do Valheim, coisa que nem o Blender nem a Unity reproduzem. Mudou o material ou
    /// a malha, o ícone acompanha sozinho. Decidido em 2026-10-08.
    ///
    /// Duas diferenças em relação ao scouter vestido, as duas vistas na simulação do
    /// <c>Saiyaheim-Preview</c>:
    /// <list type="bullet">
    /// <item>A lente fica <b>opaca</b>: transparente ela some contra o fundo do inventário.</item>
    /// <item>A <b>emissão vai a zero</b>, na lente e na carcaça: a câmera do Jotunn não tem o
    /// pós-processamento do jogo, e a emissão do flametal estourava em branco.</item>
    /// </list>
    /// </summary>
    internal static class ScouterIcon
    {
        private const int Size = 128;

        private const string LensPrefix = "Scouter_Lens_";

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        /// <summary>Material do modelo → a versão de ícone dele. Uma por material, não por item.</summary>
        private static readonly Dictionary<Material, Material> IconMaterials = new Dictionary<Material, Material>();

        /// <summary>Os sprites que este mod criou, para soltar a textura quando o ícone é refeito.</summary>
        private static readonly Dictionary<ScouterRegistry.Entry, Sprite> Rendered =
            new Dictionary<ScouterRegistry.Entry, Sprite>();

        /// <summary>Renderiza os ícones de todos os scouters registrados.</summary>
        internal static void RenderAll()
        {
            foreach (ScouterRegistry.Entry entry in ScouterRegistry.Entries)
            {
                try
                {
                    Render(entry);
                }
                catch (System.Exception e)
                {
                    // Sem ícone o item continua funcionando, com o do capacete de couro.
                    SaiyaheimPlugin.Log.LogError($"Scouter icon for '{entry.Item?.ItemPrefab?.name}' failed: {e}");
                }
            }
        }

        private static void Render(ScouterRegistry.Entry entry)
        {
            Transform model = entry.Item.ItemPrefab.transform.Find("attach/" + ScouterModel.ChildName);
            if (model == null)
            {
                // ScouterModel.Build falhou e já avisou; fica o ícone do capacete de couro.
                return;
            }

            // Montado num pai inativo para nada rodar enquanto os materiais são trocados.
            var holder = new GameObject("SaiyaheimScouterIcon");
            holder.SetActive(false);
            GameObject icon = Object.Instantiate(model.gameObject, holder.transform, false);
            try
            {
                foreach (ScouterModel.Pose pose in icon.GetComponentsInChildren<ScouterModel.Pose>(true))
                {
                    Object.DestroyImmediate(pose);
                }

                icon.transform.localPosition = Vector3.zero;
                icon.transform.localRotation = Quaternion.identity;
                icon.transform.localScale = Vector3.one;

                Material metal = ScouterModel.MetalMaterial(entry.Tier);
                foreach (Renderer renderer in icon.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material m = materials[i];
                        if (m == null)
                        {
                            continue;
                        }

                        if (m.name.StartsWith(LensPrefix))
                        {
                            materials[i] = IconMaterial(m, OpaqueLens);
                        }
                        else if (m == metal)
                        {
                            materials[i] = IconMaterial(m, NoEmission);
                        }
                    }

                    renderer.sharedMaterials = materials;
                }

                // O RenderManager só aceita alvo ativo (procura componente visual só em objeto
                // ativo). Fora do pai, o clone fica ativo no mundo, mas é destruído aqui mesmo,
                // antes de qualquer frame ser desenhado.
                icon.transform.SetParent(null, false);

                var request = new RenderManager.RenderRequest(icon)
                {
                    Width = Size,
                    Height = Size,
                    Rotation = Quaternion.Euler(SaiyaheimConfig.ScouterIconRotation),
                    DistanceMultiplier = SaiyaheimConfig.ScouterIconDistance,
                };

                Sprite sprite = RenderManager.Instance.Render(request);
                if (sprite == null)
                {
                    SaiyaheimPlugin.Log.LogWarning($"No icon rendered for '{entry.Item.ItemPrefab.name}'.");
                    return;
                }

                sprite.name = entry.Item.ItemPrefab.name + "_icon";
                entry.Shared.m_icons = new[] { sprite };

                if (Rendered.TryGetValue(entry, out Sprite old) && old != null)
                {
                    Object.Destroy(old.texture);
                    Object.Destroy(old);
                }

                Rendered[entry] = sprite;
            }
            finally
            {
                Object.DestroyImmediate(icon);
                Object.DestroyImmediate(holder);
            }
        }

        private static Material IconMaterial(Material source, System.Action<Material> adjust)
        {
            if (IconMaterials.TryGetValue(source, out Material cached) && cached != null)
            {
                return cached;
            }

            var material = new Material(source) { name = source.name + " (icon)" };
            adjust(material);
            IconMaterials[source] = material;
            return material;
        }

        /// <summary>
        /// Standard em modo Opaque, sem emissão. Os dois keywords saem juntos de propósito: a
        /// variante sem keyword nenhum é a do acabamento e da grade, e com certeza está no bundle;
        /// só <c>_EMISSION</c>, sem o alfa, talvez não esteja.
        /// </summary>
        private static void OpaqueLens(Material material)
        {
            material.SetFloat("_Mode", 0f);
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_EMISSION");
            material.renderQueue = -1;
            NoEmission(material);
        }

        /// <summary>
        /// Emissão zerada pela cor, e não pelo keyword: o material da carcaça é do jogo, e a
        /// variante sem <c>_EMISSION</c> pode não existir para o shader dele.
        /// </summary>
        private static void NoEmission(Material material)
        {
            if (material.HasProperty(EmissionColor))
            {
                material.SetColor(EmissionColor, Color.black);
            }
        }
    }
}
