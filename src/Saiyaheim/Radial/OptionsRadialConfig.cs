using System.Collections.Generic;
using Saiyaheim.Ki;
using Saiyaheim.Transformations;
using Saiyaheim.Util;
using UnityEngine;
using Valheim.UI;

namespace Saiyaheim.Radial
{
    /// <summary>
    /// O anel de opções: o que o jogador liga e desliga do próprio personagem, num lugar só em vez
    /// de uma tecla para cada coisa. Começou em 2026-10-16 com o rabo solto ou enrolado na cintura;
    /// é a página de toggles anotada em [[Melhorias]], "Menu de toggles no radial", e os próximos
    /// entram aqui. No mesmo dia entraram a aura sempre acesa e o personagem sem rabo.
    ///
    /// <b>Um item que alterna</b>, e não os dois estados lado a lado: mesma regra dos outros anéis,
    /// o estado em que o jogador já está não aparece. O rótulo diz para onde o clique leva.
    ///
    /// <b>Ícones no estilo dos de grupo do radial do jogo</b> (silhueta branca, recortes, halo
    /// fraco), desenhados por script em <c>tools/icon_radial.py</c> no projeto Unity: a engrenagem
    /// no grupo, e nos itens um ícone que mostra para onde o clique leva (rabo solto, enrolado ou
    /// riscado; a aura).
    /// </summary>
    internal sealed class OptionsRadialConfig : IRadialConfig
    {
        public string LocalizedName => "Options";

        public Sprite Sprite => IconLoader.LoadOptional("options");

        public void InitRadialConfig(RadialBase radial)
        {
            Player player = Player.m_localPlayer;
            List<RadialMenuElement> elements = new List<RadialMenuElement>();

            // O jogador é lido no clique, não capturado aqui — mesmo motivo do anel das formas.
            bool auraOn = KiChargeEffects.IsAlwaysOn(player);
            elements.Add(SaiyaRadial.Leaf(
                auraOn ? "Aura when charging" : "Aura always on",
                auraOn ? "Show the aura only while charging ki" : "Keep the aura on while ki is on",
                IconLoader.LoadOptional("aura"),
                () =>
                {
                    KiChargeEffects.SetAlwaysOn(Player.m_localPlayer, !auraOn);
                    return true;
                }));

            bool hidden = FormTail.IsHidden(player);
            elements.Add(SaiyaRadial.Leaf(
                hidden ? "Show tail" : "Hide tail",
                hidden ? "Give your character a tail again" : "Your character goes without a tail, SSJ4 included",
                IconLoader.LoadOptional(hidden ? "tail" : "tail_hidden"),
                () =>
                {
                    FormTail.SetHidden(Player.m_localPlayer, !hidden);
                    return true;
                }));

            // Enrolar um rabo escondido não teria o que mostrar.
            if (!hidden)
            {
                bool wrapped = FormTail.IsWrapped(player);
                elements.Add(SaiyaRadial.Leaf(
                    wrapped ? "Loose tail" : "Wrap tail",
                    wrapped ? "Let the tail hang free" : "Wrap the tail around your waist",
                    IconLoader.LoadOptional(wrapped ? "tail" : "tail_wrapped"),
                    () =>
                    {
                        FormTail.SetWrapped(Player.m_localPlayer, !wrapped);
                        return true;
                    }));
            }

            radial.ConstructRadial(elements);
        }

        /// <summary>
        /// Este anel tem alguma coisa para mostrar? Sempre: a aura vale para qualquer personagem.
        /// Ver <see cref="FormsRadialConfig.HasContent"/>.
        /// </summary>
        internal static bool HasContent(Player player)
        {
            return player != null;
        }
    }
}
