using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Jotunn.Utils;
using UnityEngine;

namespace Saiyaheim.Util
{
    /// <summary>
    /// Carrega os ícones de skill embutidos na DLL (<c>Assets/icons/*.jpg</c>) como
    /// <see cref="Sprite"/>.
    ///
    /// Mesmo motivo do bundle de cabelo em <c>CustomHair.LoadBundle</c>: ler o recurso inteiro
    /// para um <c>byte[]</c> antes de decodificar, para não depender de um stream que pode já
    /// ter sido fechado. Aqui o arquivo é pequeno (dezenas de KB), então o cache por nome evita
    /// só o custo de decodificar JPEG de novo, não estoura memória.
    /// </summary>
    internal static class IconLoader
    {
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> Cache =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>
        /// Carrega o ícone <paramref name="name"/> (sem extensão, ex. "ssj") de
        /// <c>Assets/icons/{name}.jpg</c>. Devolve null e loga erro se o recurso não existir ou
        /// não decodificar — chamador trata como "sem ícone", igual ao comportamento anterior.
        /// </summary>
        internal static Sprite Load(string name)
        {
            return Load(name, required: true);
        }

        /// <summary>
        /// Igual ao <see cref="Load"/>, mas <b>ícone ausente não é erro</b>: devolve null em
        /// silêncio.
        ///
        /// Existe para o menu radial, onde o ícone é enfeite e o rótulo é que carrega a
        /// informação. Uma skill sem ícone é bug — o jogo desenha um quadrado vazio na aba de
        /// skills; um elemento de anel sem ícone é só um elemento com o nome, e vai ganhar arte
        /// quando houver. Logar erro por frame de menu aberto seria pior que o buraco.
        /// </summary>
        internal static Sprite LoadOptional(string name)
        {
            return Load(name, required: false);
        }

        private static Sprite Load(string name, bool required)
        {
            if (Cache.TryGetValue(name, out Sprite cached))
            {
                return cached;
            }

            Assembly assembly = typeof(IconLoader).Assembly;
            string suffix = $"icons.{name}.jpg";
            string resource = assembly.GetManifestResourceNames()
                                      .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.Ordinal));
            if (resource == null)
            {
                if (required)
                {
                    SaiyaheimPlugin.Log.LogError($"Icon '{name}' not found in the DLL as '{suffix}'.");
                }
                else
                {
                    // Cacheado como ausente: sem isto, todo Refresh do menu varreria os recursos
                    // da DLL de novo atrás de um arquivo que não existe.
                    Cache[name] = null;
                }

                return null;
            }

            byte[] data;
            using (Stream stream = assembly.GetManifestResourceStream(resource))
            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                data = buffer.ToArray();
            }

            // AssetUtils.LoadImage decodifica via reflection sobre UnityEngine.ImageConversion:
            // referenciar aquele módulo direto (UnityEngine.ImageConversion.LoadImage) trava o
            // build com CS1705 — a versão de netstandard que ele carrega é maior que a
            // referenciada pelo projeto net462. O Jotunn já paga esse custo por nós.
            Texture2D texture = AssetUtils.LoadImage(data);
            if (texture == null)
            {
                SaiyaheimPlugin.Log.LogError($"'{resource}' did not decode as an image.");
                return null;
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            Cache[name] = sprite;
            return sprite;
        }
    }
}
