using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Saiyaheim.Util
{
    /// <summary>
    /// Prefabs de efeito próprios do mod, vindos do AssetBundle <c>saiyaheim_fx</c> embutido na
    /// DLL. Hoje é a aura de carregar ki (pacote "Goku aura" do Hovl Studio), montada no projeto
    /// Unity vizinho em <c>Assets/Aura/</c>.
    ///
    /// <b>O <see cref="AttachedEffect"/> pergunta aqui quando o <c>ZNetScene</c> não conhece o
    /// nome.</b> Assim um efeito nosso entra no mesmo lugar de um <c>fx_</c> do jogo — nome no
    /// config — e herda o resto: preso ao jogador, sem ZDO, loop forçado, tingido.
    ///
    /// ⚠️ <b>Este bundle leva shader</b>, ao contrário do <c>saiyaheim_hair</c>, que por regra leva
    /// só malha: em 2026-09-08 um bundle com shader derrubou o jogo na tela de carregamento (ver
    /// [[Malhas Custom]]). Aqui não há como fugir — o visual da aura <i>é</i> o shader. Duas
    /// defesas:
    /// <list type="bullet">
    /// <item>Bundle separado, carregado <b>só na primeira vez que um efeito dele é pedido</b>, e
    /// não no boot. Se voltar a derrubar, o jogo ainda abre, e o crash aparece no primeiro
    /// carregamento de ki — fácil de ligar à causa.</item>
    /// <item>Um bundle por plataforma. Shader compilado é específico do pipeline gráfico:
    /// Vulkan/GLCore no Linux, D3D11 no Windows. O de cabelo vai só o Linux porque malha não liga
    /// para isso; shader liga.</item>
    /// </list>
    /// Carga por nome, nunca <c>LoadAllAssets</c>, e por <c>LoadFromMemory</c> pelo mesmo motivo
    /// do <see cref="Saiyaheim.Transformations.CustomHair"/>.
    /// </summary>
    internal static class CustomEffects
    {
        private const string BundleName = "saiyaheim_fx";

        private static AssetBundle _bundle;
        private static bool _loadFailed;

        private static readonly Dictionary<string, GameObject> Cache =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// O prefab com esse nome de arquivo (sem extensão, sem diferenciar maiúsculas), ou null
        /// se o bundle não tem — ou não existe nesta DLL.
        /// </summary>
        internal static GameObject GetPrefab(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (Cache.TryGetValue(name, out GameObject cached))
            {
                return cached;
            }

            AssetBundle bundle = Bundle();
            if (bundle == null)
            {
                return null;
            }

            // Os caminhos no bundle vêm inteiros e em minúsculas:
            // "assets/aura/aura base.prefab".
            string assetName = bundle.GetAllAssetNames()
                                     .FirstOrDefault(n => string.Equals(
                                         Path.GetFileNameWithoutExtension(n), name,
                                         StringComparison.OrdinalIgnoreCase));

            GameObject prefab = assetName == null ? null : bundle.LoadAsset<GameObject>(assetName);

            // Cacheia também a ausência: o AttachedEffect cai aqui para todo nome que o jogo não
            // conhece, e varrer o bundle a cada carregamento seria desperdício.
            Cache[name] = prefab;
            return prefab;
        }

        private static AssetBundle Bundle()
        {
            if (_bundle == null && !_loadFailed)
            {
                _bundle = LoadBundle();
                _loadFailed = _bundle == null;
            }

            return _bundle;
        }

        private static AssetBundle LoadBundle()
        {
            string platform = Application.platform == RuntimePlatform.WindowsPlayer ? "win" : "linux";
            string resourceName = $"{BundleName}.{platform}";

            Assembly assembly = typeof(CustomEffects).Assembly;
            string resource = assembly.GetManifestResourceNames()
                                      .FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.Ordinal));
            if (resource == null)
            {
                SaiyaheimPlugin.Log.LogWarning(
                    $"Asset bundle '{resourceName}' not found in the DLL; custom effects are off.");
                return null;
            }

            byte[] data;
            using (Stream stream = assembly.GetManifestResourceStream(resource))
            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                data = buffer.ToArray();
            }

            AssetBundle bundle = AssetBundle.LoadFromMemory(data);
            if (bundle == null)
            {
                SaiyaheimPlugin.Log.LogError($"'{resource}' did not load as an asset bundle.");
            }
            else
            {
                SaiyaheimPlugin.Log.LogInfo(
                    $"Loaded '{resource}': {string.Join(", ", bundle.GetAllAssetNames())}");
            }

            return bundle;
        }
    }
}
