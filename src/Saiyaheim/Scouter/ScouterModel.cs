using UnityEngine;

namespace Saiyaheim.Scouter
{
    /// <summary>
    /// O modelo do scouter dentro do item: troca a malha do capacete de couro pela do scouter e
    /// aplica a posição dele na cabeça. Etapa 15, fase 3.
    ///
    /// <b>A carcaça usa o material do próprio item do jogo</b>: na Meadows a pederneira (flint),
    /// depois as barras de metal (bronze, iron, silverbar, blackmetal, flametal, BloodGoldBar_mat)
    /// e, na Mistlands, a Yggdrasil Wood (Shoot_Stack_mat), tirado do prefab do item em tempo de execução. Fica igual ao jogo porque <i>é</i> o material do jogo, e nada dele viaja no bundle.
    /// Lente, botão, acabamento e grade vêm do <c>saiyaheim_fx</c>, um prefab por cor de lente.
    /// </summary>
    internal static class ScouterModel
    {
        private const string ModelPrefix = "SaiyaheimScouterModel_";
        private const string FramePlaceholder = "Scouter_FramePlaceholder";
        internal const string ChildName = "scouter";

        /// <summary>
        /// Monta o modelo no <c>attach</c> do item clonado. Falso se faltar alguma peça: o item fica
        /// com a cara do capacete de couro, que é feio mas funciona.
        /// </summary>
        internal static bool Build(GameObject itemPrefab, ScouterTier tier, string lens)
        {
            Transform attach = itemPrefab.transform.Find("attach");
            GameObject source = Util.CustomEffects.GetPrefab(ModelPrefix + lens);
            if (attach == null || source == null)
            {
                SaiyaheimPlugin.Log.LogWarning(
                    $"Scouter model '{ModelPrefix + lens}' or the helmet's 'attach' is missing; " +
                    $"the {tier.Metal} scouter keeps the leather helmet look.");
                return false;
            }

            // Tira a malha do capacete de couro. O collider fica: é o que deixa apanhar o item do
            // chão, e o formato do couro serve.
            for (int i = attach.childCount - 1; i >= 0; i--)
            {
                Transform child = attach.GetChild(i);
                if (child.GetComponent<Renderer>() != null)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            GameObject model = Object.Instantiate(source, attach, false);
            model.name = ChildName;
            model.AddComponent<Pose>();

            Material metal = MetalMaterial(tier);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (metal != null && materials[i] != null && materials[i].name.StartsWith(FramePlaceholder))
                    {
                        materials[i] = metal;
                    }
                }

                renderer.sharedMaterials = materials;
            }

            return true;
        }

        /// <summary>
        /// O material da barra de metal do tier: o primeiro <c>MeshRenderer</c> do prefab da barra.
        /// Pelo prefab e não pelo nome do material, que mudou de padrão entre atualizações
        /// (<c>bronze</c>, <c>silverbar</c>, <c>BloodGoldBar_mat</c>).
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<ScouterTier, Material> MaterialCache =
            new System.Collections.Generic.Dictionary<ScouterTier, Material>();

        internal static Material MetalMaterial(ScouterTier tier)
        {
            // Um material por tier, compartilhado pelas quatro lentes: a cópia do material de
            // cenário não precisa existir quatro vezes.
            if (MaterialCache.TryGetValue(tier, out Material cached) && cached != null)
            {
                return cached;
            }

            Material material = LoadMetalMaterial(tier);
            MaterialCache[tier] = material;
            return material;
        }

        private static Material LoadMetalMaterial(ScouterTier tier)
        {
            GameObject bar = Jotunn.Managers.PrefabManager.Instance.GetPrefab(tier.MetalPrefab);
            MeshRenderer renderer = bar == null ? null : bar.GetComponentInChildren<MeshRenderer>(true);
            if (renderer == null || renderer.sharedMaterial == null)
            {
                SaiyaheimPlugin.Log.LogWarning(
                    $"No material on '{tier.MetalPrefab}'; the {tier.Metal} scouter keeps a plain frame.");
                return null;
            }

            Material material = renderer.sharedMaterial;

            // ⚠️ Material de cenário, como o da Yggdrasil Wood (Shoot_Stack_mat), não serve direto:
            // o shader dele calcula musgo, ruído, chuva, neve e balanço de vegetação pela posição
            // do MUNDO, e na cabeça, que anda, a carcaça piscava (visto no jogo em 2026-10-09).
            // Ligar o _MoveableObject não bastou. Então só as texturas dele vão para uma cópia do
            // material da barra de ferro, cujo shader é o mesmo das outras carcaças e não tem nada
            // disso. A faixa de musgo continua, porque está desenhada na própria textura.
            if (material.HasProperty(MoveableObject))
            {
                material = FromBarShader(material);
            }

            return material;
        }

        private const string BarShaderSource = "Iron";

        private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        private static readonly int Metallic = Shader.PropertyToID("_Metallic");
        private static readonly int Glossiness = Shader.PropertyToID("_Glossiness");

        private static Material FromBarShader(Material scenery)
        {
            GameObject bar = Jotunn.Managers.PrefabManager.Instance.GetPrefab(BarShaderSource);
            Material barMaterial = bar == null ? null : bar.GetComponentInChildren<MeshRenderer>(true)?.sharedMaterial;
            if (barMaterial == null)
            {
                SaiyaheimPlugin.Log.LogWarning($"No '{BarShaderSource}' bar material to rebuild '{scenery.name}' on.");
                return scenery;
            }

            var material = new Material(barMaterial) { name = scenery.name + " (scouter)" };
            material.mainTexture = scenery.mainTexture;
            material.mainTextureScale = scenery.mainTextureScale;
            material.mainTextureOffset = scenery.mainTextureOffset;
            material.color = scenery.HasProperty("_Color") ? scenery.color : Color.white;
            if (scenery.HasProperty(BumpMap) && material.HasProperty(BumpMap))
            {
                material.SetTexture(BumpMap, scenery.GetTexture(BumpMap));
            }

            if (material.HasProperty(Metallic))
            {
                material.SetFloat(Metallic, scenery.HasProperty(Metallic) ? scenery.GetFloat(Metallic) : 0f);
            }

            if (material.HasProperty(Glossiness))
            {
                material.SetFloat(Glossiness, scenery.HasProperty(Glossiness) ? scenery.GetFloat(Glossiness) : 0.1f);
            }

            return material;
        }

        private static readonly int MoveableObject = Shader.PropertyToID("_MoveableObject");

        /// <summary>
        /// Aplica posição, rotação e escala do scouter a cada frame. Vive no modelo, então vale para
        /// o scouter na cabeça de qualquer jogador e para o item caído no chão. Até 2026-10-09 os
        /// três vinham do <c>.cfg</c>, para o ajuste valer com o jogo aberto; hoje são constantes,
        /// e o frame a frame fica pela escala do chão, que acompanha o osso do jogador.
        ///
        /// <b>No chão, a escala copia a do osso da cabeça.</b> Vestido, o <c>attach</c> fica
        /// pendurado no <c>Helmet_attach</c> do jogador, que tem escala própria; no chão, na raiz
        /// do item, com escala 1. Com a mesma escala local o scouter caído parecia menor que o
        /// vestido (visto pelo Henrique em 2026-10-09). Em vez de um número fixo, mede o osso do
        /// jogador local: vale para qualquer mudança de esqueleto numa atualização do jogo.
        /// </summary>
        internal class Pose : MonoBehaviour
        {
            private bool _checked;
            private bool _onCharacter;

            private void LateUpdate()
            {
                if (!_checked)
                {
                    _onCharacter = GetComponentInParent<VisEquipment>() != null;
                    _checked = true;
                }

                float scale = SaiyaheimConfig.ScouterModelScale;
                if (!_onCharacter)
                {
                    scale *= GroundScale(transform.parent);
                }

                transform.localPosition = SaiyaheimConfig.ScouterModelOffset;
                transform.localRotation = Quaternion.Euler(SaiyaheimConfig.ScouterModelRotation);
                transform.localScale = Vector3.one * scale;
            }
        }

        private static VisEquipment _localVis;
        private static bool _groundScaleLogged;

        /// <summary>
        /// Quanto o scouter no chão precisa crescer para ficar do tamanho do vestido: a escala de
        /// mundo do <c>Helmet_attach</c> do jogador local sobre a do pai do modelo no chão. 1 sem
        /// jogador (menu) ou sem osso.
        /// </summary>
        private static float GroundScale(Transform parent)
        {
            Player player = Player.m_localPlayer;
            if (player == null || parent == null)
            {
                return 1f;
            }

            if (_localVis == null || _localVis.gameObject != player.gameObject)
            {
                _localVis = player.GetComponent<VisEquipment>();
            }

            Transform helmet = _localVis == null ? null : _localVis.m_helmet;
            float parentScale = parent.lossyScale.x;
            if (helmet == null || parentScale <= 0f)
            {
                return 1f;
            }

            float ratio = helmet.lossyScale.x / parentScale;
            if (!_groundScaleLogged)
            {
                _groundScaleLogged = true;
                SaiyaheimPlugin.Log.LogInfo(
                    $"Scouter on the ground: Helmet_attach scale {helmet.lossyScale}, item scale " +
                    $"{parent.lossyScale}, ground model scaled by {ratio:F3}.");
            }

            return ratio;
        }
    }
}
