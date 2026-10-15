using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Saiyaheim.Transformations
{
    /// <summary>
    /// Pelo pintado no corpo enquanto a forma está ativa — hoje só o SSJ4 (2026-10-15).
    ///
    /// <b>O pelo é textura, não malha</b> (ver [[Decisões Tomadas]], "Pelo do SSJ4 é textura do
    /// corpo, não malha"). O material do corpo (<c>Custom/Player</c>) tem duas camadas por cima da
    /// pele, <c>_ChestTex</c> e <c>_LegsTex</c>, e é nelas que o jogo pinta armadura de pano e
    /// couro (<c>VisEquipment.SetChestEquipped</c>). O pelo entra na <c>_ChestTex</c> e na
    /// <c>_ChestBumpMap</c>, e a <c>_LegsTex</c> não é tocada: a cueca fica por cima do pelo, como
    /// o Henrique pediu.
    ///
    /// <b>Direto no material, reaplicado por tique, como o jogo faz.</b> O <c>VisEquipment</c> só
    /// escreve nessas camadas quando o peitoral muda. Então, enquanto a forma dura, cada tique
    /// confere a textura: se não é o pelo, o jogo acabou de trocar a armadura, e o que ele
    /// escreveu é guardado como o que volta ao desligar a forma. Assim o desligar devolve a
    /// armadura que está vestida <b>agora</b>, e não a de quando a forma ligou. Sem reflexão: o
    /// estado do jogo (<c>m_currentChestItemHash</c>, <c>m_emptyBodyTexture</c>) é privado, e lido
    /// do próprio material ele não precisa ser.
    ///
    /// <b>O pelo cobre a textura do peitoral</b>, não a malha. Armadura com volume (o filho
    /// <c>attach_skin</c>) continua aparecendo por cima. Esconder a armadura durante a forma é
    /// uma decisão que ficou para depois de ver no jogo — ver [[Melhorias]], SSJ4.
    ///
    /// <b>Uma textura por corpo.</b> O pelo é assado na UV da malha, e o corpo feminino
    /// (<c>bodyfem.asset</c>) tem malha e UV próprias: o par dele é <c>&lt;nome&gt;_fem</c>. Ele
    /// também carrega o <b>sutiã já composto por cima do pelo</b>, porque no material feminino o
    /// sutiã mora na própria <c>_ChestTex</c> (<c>PlayerCharacter_FemUnderwearBra</c>), que o pelo
    /// substitui. Corpo que não é nenhum dos dois modelos do jogo fica sem pelo.
    ///
    /// <b>Roda para todo jogador carregado</b>, chamada pelo <c>TransformationEffects.Observe</c>
    /// com a forma do <c>NetState</c>: é renderização local, como o <c>KaiokenArmorTint</c>.
    /// </summary>
    internal static class FormBodyFur
    {
        private static readonly int ChestTexId = Shader.PropertyToID("_ChestTex");
        private static readonly int ChestBumpId = Shader.PropertyToID("_ChestBumpMap");
        private static readonly int ChestMetalId = Shader.PropertyToID("_ChestMetal");

        /// <summary>O que estava no material antes do pelo, por jogador.</summary>
        private sealed class Saved
        {
            internal Material Material;
            internal Texture Chest;
            internal Texture Bump;
            internal Texture Metal;
        }

        private sealed class Fur
        {
            internal Texture2D Color;
            internal Texture2D Normal;
        }

        private static readonly Dictionary<Player, Saved> Applied = new Dictionary<Player, Saved>();

        /// <summary>Texturas carregadas, por nome. Null guardado quer dizer "falhou, não tente de novo".</summary>
        private static readonly Dictionary<string, Fur> Loaded =
            new Dictionary<string, Fur>(StringComparer.OrdinalIgnoreCase);


        internal static void Tick(Player player, Transformation form)
        {
            if (player == null)
            {
                return;
            }

            Applied.TryGetValue(player, out Saved saved);

            VisEquipment vis = player.GetVisEquipment();
            SkinnedMeshRenderer body = vis == null ? null : vis.m_bodyModel;
            string name = form == null ? null : form.Config.BodyFur;

            string variant = string.IsNullOrEmpty(name) || body == null ? null : BodyVariant(vis, body, name);
            Fur fur = variant == null ? null : Get(variant);

            if (fur == null)
            {
                if (saved != null)
                {
                    Restore(saved);
                    Applied.Remove(player);
                }

                return;
            }

            Material material = body.material;
            if (material == null || !material.HasProperty(ChestTexId))
            {
                return;
            }

            if (saved != null && saved.Material != material)
            {
                // Material trocado por baixo (não acontece na vanilla): o guardado é de outro.
                saved = null;
            }

            Texture current = material.GetTexture(ChestTexId);
            if (saved != null && current == fur.Color)
            {
                return;
            }

            // Primeira vez, ou o jogo acabou de escrever a armadura por cima do pelo: o que está
            // lá agora é o que volta quando a forma acabar.
            if (!IsOurs(current))
            {
                saved = new Saved
                {
                    Material = material,
                    Chest = current,
                    Bump = material.GetTexture(ChestBumpId),
                    Metal = material.HasProperty(ChestMetalId) ? material.GetTexture(ChestMetalId) : null,
                };
                Applied[player] = saved;
            }
            else if (saved == null)
            {
                // Pelo de outra forma sem registro guardado: sem o original não há o que devolver
                // depois, então não pinta.
                return;
            }

            material.SetTexture(ChestTexId, fur.Color);
            material.SetTexture(ChestBumpId, fur.Normal);
            if (material.HasProperty(ChestMetalId))
            {
                // O metal da armadura por baixo deixaria o pelo brilhando como placa.
                material.SetTexture(ChestMetalId, null);
            }
        }

        /// <summary>Este jogador deixou de existir: o material morreu junto, só a lembrança fica.</summary>
        internal static void Forget(Player player)
        {
            if (player != null)
            {
                Applied.Remove(player);
            }
        }

        /// <summary>Devolve o corpo de todo mundo e esquece. Caminho de erro dos efeitos.</summary>
        internal static void Reset()
        {
            foreach (Saved saved in Applied.Values)
            {
                Restore(saved);
            }

            Applied.Clear();
        }

        private static void Restore(Saved saved)
        {
            if (saved.Material == null)
            {
                return;
            }

            // Só devolve se o pelo ainda está lá. Se o jogo já escreveu outra coisa por cima, a
            // dele é a mais nova e fica.
            if (IsOurs(saved.Material.GetTexture(ChestTexId)))
            {
                saved.Material.SetTexture(ChestTexId, saved.Chest);
                saved.Material.SetTexture(ChestBumpId, saved.Bump);
                if (saved.Material.HasProperty(ChestMetalId))
                {
                    saved.Material.SetTexture(ChestMetalId, saved.Metal);
                }
            }
        }

        /// <summary>
        /// O nome da textura para a malha que este corpo veste agora: <paramref name="name"/> no
        /// masculino (modelo 0 do <c>VisEquipment</c>), <c>&lt;nome&gt;_fem</c> no feminino (modelo
        /// 1). Pela malha e não pelo índice do ZDO, porque é a malha que define a UV, e o
        /// <c>UpdateBaseModel</c> troca as duas juntas.
        /// </summary>
        private static string BodyVariant(VisEquipment vis, SkinnedMeshRenderer body, string name)
        {
            if (vis.m_models.Length > 0 && body.sharedMesh == vis.m_models[0].m_mesh)
            {
                return name;
            }

            if (vis.m_models.Length > 1 && body.sharedMesh == vis.m_models[1].m_mesh)
            {
                return name + "_fem";
            }

            return null;
        }

        private static bool IsOurs(Texture texture)
        {
            if (texture == null)
            {
                return false;
            }

            foreach (Fur fur in Loaded.Values)
            {
                if (fur != null && (texture == fur.Color || texture == fur.Normal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// As texturas do pelo, para quem precisa delas fora do corpo — o rabo do SSJ4
        /// (<see cref="FormTail"/>) usa as mesmas. False se não carregaram.
        /// </summary>
        internal static bool TryGetTextures(string name, out Texture2D color, out Texture2D normal)
        {
            Fur fur = Get(name);
            color = fur?.Color;
            normal = fur?.Normal;
            return fur != null;
        }

        private static Fur Get(string name)
        {
            if (Loaded.TryGetValue(name, out Fur cached))
            {
                return cached;
            }

            Texture2D color = LoadTexture(name, linear: false);
            Texture2D normal = color == null ? null : LoadTexture(name + "_n", linear: true);

            Fur fur = color != null && normal != null ? new Fur { Color = color, Normal = normal } : null;
            Loaded[name] = fur;

            return fur;
        }

        /// <summary>
        /// O <c>ImageConversion.LoadImage</c>, por reflexão pelo mesmo motivo do
        /// <c>IconLoader</c>: referenciar o módulo direto trava o build com CS1705. Aqui não dá para
        /// usar o <c>AssetUtils.LoadImage</c> do Jotunn, que cria a textura em sRGB: o normal map
        /// tem que ser <b>linear</b>, senão o relevo sai torto.
        /// </summary>
        private static readonly MethodInfo LoadImageMethod =
            Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")
                ?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });

        private static Texture2D LoadTexture(string name, bool linear)
        {
            Assembly assembly = typeof(FormBodyFur).Assembly;
            string suffix = $"body.{name}.png";
            string resource = assembly.GetManifestResourceNames()
                                      .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.Ordinal));
            if (resource == null)
            {
                SaiyaheimPlugin.Log.LogError($"Body texture '{name}' not found in the DLL as '{suffix}'.");
                return null;
            }

            if (LoadImageMethod == null)
            {
                SaiyaheimPlugin.Log.LogError("ImageConversion.LoadImage not found; form body fur is off.");
                return null;
            }

            byte[] data;
            using (Stream stream = assembly.GetManifestResourceStream(resource))
            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                data = buffer.ToArray();
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear)
            {
                name = "saiyaheim_" + name,
                wrapMode = TextureWrapMode.Clamp,
            };

            // markNonReadable: a cópia na RAM só serviria para ler pixel, e ninguém lê.
            if (!(bool)LoadImageMethod.Invoke(null, new object[] { texture, data, true }))
            {
                SaiyaheimPlugin.Log.LogError($"'{resource}' did not decode as an image.");
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            SaiyaheimPlugin.LogVerbose($"Loaded body texture '{name}' ({texture.width}x{texture.height}, linear {linear}).");
            return texture;
        }
    }
}
