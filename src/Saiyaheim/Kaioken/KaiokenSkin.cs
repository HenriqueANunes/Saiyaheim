using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Kaioken
{
    /// <summary>
    /// Pele avermelhada enquanto o Kaioken está ligado, na cor do tier (2026-10-05, pedido do
    /// Henrique).
    ///
    /// <b>Pela mesma porta da cor do cabelo</b> (ver <c>TransformationEffects.SetHairColor</c>):
    /// <c>VisEquipment.SetSkinColor</c> escreve só na ZDO, que é estado de sessão, e o
    /// <c>VisEquipment.UpdateColors</c> lê a ZDO todo frame. A troca aparece na hora, replica para
    /// os outros jogadores sem RPC e some sozinha se o jogo fechar com o Kaioken ligado. O
    /// <c>Player.SetSkinColor</c> ficaria proibido pelo mesmo motivo do cabelo: o
    /// <c>m_skinColor</c> do <c>Player</c> vai para o perfil do personagem.
    ///
    /// <b>Reaplicado todo frame, e não por patch.</b> O <c>Player.SetupVisEquipment</c> devolve a
    /// pele original a cada troca de equipamento, como faz com o cabelo. Aqui não precisa do
    /// postfix do <c>HairColorPatch</c>: o <c>VisEquipment.SetSkinColor</c> compara com o valor que
    /// já tem e só escreve na ZDO quando mudou, então pedir a mesma cor todo frame não custa nada,
    /// e a troca de equipamento custa no máximo um frame de pele normal.
    ///
    /// Só roda na máquina do dono: a escrita na ZDO só vale ali, e é ela que leva a cor aos outros.
    /// </summary>
    internal static class KaiokenSkin
    {
        private static Player _tintedPlayer;

        internal static void Tick(Player player, KaiokenTier active)
        {
            if (player == null)
            {
                _tintedPlayer = null;
                return;
            }

            if (!ReferenceEquals(player, _tintedPlayer))
            {
                // Personagem novo (respawn, troca de personagem): a pele do anterior morreu com a
                // ZDO dele.
                _tintedPlayer = null;
            }

            float strength = Mathf.Clamp01(SaiyaheimConfig.KaiokenSkinTint);
            bool wanted = active != null && strength > 0f;

            if (!wanted && _tintedPlayer == null)
            {
                return;
            }

            if (!GameAccess.TryGetSkinColor(player, out Vector3 original))
            {
                return;
            }

            VisEquipment vis = player.GetVisEquipment();
            if (vis == null)
            {
                return;
            }

            if (!wanted)
            {
                vis.SetSkinColor(original);
                _tintedPlayer = null;
                return;
            }

            Color tier = ColorUtility.TryParseHtmlString(active.GetAuraColor(), out Color parsed)
                ? parsed
                : Color.red;

            // A pele é um multiplicador sobre a textura. Só puxar G e B para baixo avermelhava, mas
            // escurecia junto (playtest de 2026-10-05): a luminância caía. Dividir pela luminância
            // do próprio tom devolve o brilho — o R passa de 1 e compensa o G e o B. Multiplicar
            // pela cor original preserva o tom de pele do personagem.
            Color tint = Color.Lerp(Color.white, tier, strength);
            float luminance = 0.2126f * tint.r + 0.7152f * tint.g + 0.0722f * tint.b;
            if (luminance > 0.01f)
            {
                tint /= luminance;
            }

            vis.SetSkinColor(new Vector3(original.x * tint.r, original.y * tint.g, original.z * tint.b));
            _tintedPlayer = player;
        }
    }
}
