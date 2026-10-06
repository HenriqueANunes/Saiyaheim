using System.Collections.Generic;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Kaioken
{
    /// <summary>
    /// Armadura com brilho vermelho enquanto o Kaioken está ligado, na cor do tier (2026-10-05,
    /// pedido do Henrique).
    ///
    /// <b>Por emissão, e não por <c>_Color</c>.</b> As peças de armadura usam o shader
    /// <c>Custom/Creature</c>, que tem <c>_Color</c> (multiplica o albedo) e <c>_EmissionColor</c>
    /// (HDR, soma luz). Multiplicar escurece — foi o defeito da primeira versão da pele —, e somar
    /// avermelha sem tirar luz nenhuma. O <c>_EmissionMap</c> vem branco por padrão, então em peça
    /// sem mapa próprio a emissão cobre a peça inteira.
    ///
    /// <b>Só as malhas de armadura</b> (elmo, peito, pernas, ombro), lidas dos campos privados do
    /// <c>VisEquipment</c> pelo <c>GameAccess</c>. Arma, cabelo e os efeitos do mod pendurados no
    /// jogador ficam de fora. A parte da armadura que o jogo pinta como <b>textura no corpo</b>
    /// (<c>_ChestTex</c>/<c>_LegsTex</c> do shader <c>Custom/Player</c>) não tem cor nem emissão
    /// para mexer, e não muda.
    ///
    /// <b>Pelo <c>MaterialPropertyBlock</c> de cada renderer, lido antes de escrever</b>, como o
    /// <c>VisEquipment.SetupHairRenderer</c> faz com o cabelo: assim o <c>_SnowCover</c> que o
    /// <c>MaterialMan</c> põe no mesmo bloco continua lá. Quando o <c>MaterialMan</c> reescreve o
    /// bloco inteiro (neve mudando), a emissão some por um frame e o tique seguinte a repõe.
    ///
    /// <b>Roda para todo jogador carregado</b>, chamada pelo <c>TransformationEffects.Observe</c>
    /// com o tier do <c>NetState</c>: é renderização local, e cada um pinta os amigos na própria
    /// tela.
    /// </summary>
    internal static class KaiokenArmorTint
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// A emissão de cada renderer antes da nossa, por jogador. É o que volta ao desligar, e é
        /// também o que impede somar o vermelho por cima do vermelho a cada frame.
        /// </summary>
        private static readonly Dictionary<Player, Dictionary<Renderer, Color>> Originals =
            new Dictionary<Player, Dictionary<Renderer, Color>>();

        private static readonly List<GameObject> Pieces = new List<GameObject>();
        private static readonly List<Renderer> Renderers = new List<Renderer>();
        private static readonly List<Renderer> Gone = new List<Renderer>();
        private static MaterialPropertyBlock _block;

        internal static void Tick(Player player, KaiokenTier tier)
        {
            if (player == null)
            {
                return;
            }

            float strength = Mathf.Max(0f, SaiyaheimConfig.KaiokenArmorGlow);
            bool wanted = tier != null && strength > 0f;

            Originals.TryGetValue(player, out Dictionary<Renderer, Color> originals);

            if (!wanted)
            {
                if (originals != null)
                {
                    Restore(originals);
                    Originals.Remove(player);
                }

                return;
            }

            VisEquipment vis = player.GetVisEquipment();
            if (vis == null || !GameAccess.TryGetArmorInstances(vis, Pieces))
            {
                return;
            }

            if (originals == null)
            {
                originals = new Dictionary<Renderer, Color>();
                Originals[player] = originals;
            }

            Color color = ColorUtility.TryParseHtmlString(tier.GetAuraColor(), out Color parsed)
                ? parsed
                : Color.red;
            Color add = color * strength;

            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            foreach (GameObject piece in Pieces)
            {
                if (piece == null)
                {
                    continue;
                }

                piece.GetComponentsInChildren(true, Renderers);

                foreach (Renderer renderer in Renderers)
                {
                    if (!originals.TryGetValue(renderer, out Color original))
                    {
                        original = ReadEmission(renderer);
                        originals[renderer] = original;
                    }

                    renderer.GetPropertyBlock(_block);
                    _block.SetColor(EmissionId, original + add);
                    renderer.SetPropertyBlock(_block);
                }
            }

            // Peça desequipada: o renderer morreu junto e a entrada só ocupa espaço.
            Gone.Clear();
            foreach (Renderer renderer in originals.Keys)
            {
                if (renderer == null)
                {
                    Gone.Add(renderer);
                }
            }

            foreach (Renderer renderer in Gone)
            {
                originals.Remove(renderer);
            }
        }

        /// <summary>Este jogador deixou de existir: a armadura morreu junto, só a lembrança fica.</summary>
        internal static void Forget(Player player)
        {
            if (player != null)
            {
                Originals.Remove(player);
            }
        }

        /// <summary>Devolve a emissão de todo mundo e esquece. Caminho de erro dos efeitos.</summary>
        internal static void Reset()
        {
            foreach (Dictionary<Renderer, Color> originals in Originals.Values)
            {
                Restore(originals);
            }

            Originals.Clear();
        }

        private static void Restore(Dictionary<Renderer, Color> originals)
        {
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            foreach (KeyValuePair<Renderer, Color> pair in originals)
            {
                if (pair.Key == null)
                {
                    continue;
                }

                pair.Key.GetPropertyBlock(_block);
                _block.SetColor(EmissionId, pair.Value);
                pair.Key.SetPropertyBlock(_block);
            }
        }

        /// <summary>
        /// A emissão que o renderer tem agora: a do bloco, se alguém já pôs uma, senão a do
        /// material. Peça sem emissão nenhuma lê preto, que é o "sem brilho" do shader.
        /// </summary>
        private static Color ReadEmission(Renderer renderer)
        {
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            renderer.GetPropertyBlock(_block);
            if (_block.HasColor(EmissionId))
            {
                return _block.GetColor(EmissionId);
            }

            Material material = renderer.sharedMaterial;

            return material != null && material.HasProperty(EmissionId)
                ? material.GetColor(EmissionId)
                : Color.black;
        }
    }
}
