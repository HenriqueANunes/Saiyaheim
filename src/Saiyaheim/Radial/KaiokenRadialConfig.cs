using System.Collections.Generic;
using Saiyaheim.Kaioken;
using Saiyaheim.Util;
using UnityEngine;
using Valheim.UI;

namespace Saiyaheim.Radial
{
    /// <summary>
    /// O anel do Kaioken: um item por tier aprendido, mais o desligar. Pedido do Henrique em
    /// 2026-10-06, como grupo próprio ao lado das formas — o Kaioken liga por cima de qualquer
    /// forma, então misturar os dois num anel só confundiria o que cada clique troca.
    ///
    /// Mesmas regras do <see cref="FormsRadialConfig"/>: o tier ativo não aparece, tier não
    /// aprendido não aparece, e o desligar só aparece com o Kaioken ligado.
    ///
    /// <b>Clicar vai direto ao tier</b>, para cima ou para baixo, ao contrário da tecla, que sobe
    /// um de cada vez. A roda é para escolher; subir degrau a degrau é o papel da tecla.
    ///
    /// Todos os itens usam o ícone do Kaioken (o do Power Level tingido), aprovado pelo Henrique
    /// no mesmo dia. O rótulo do tier já diz qual é qual.
    /// </summary>
    internal sealed class KaiokenRadialConfig : IRadialConfig
    {
        private const string IconName = "kaioken";

        public string LocalizedName => "Kaioken";

        public Sprite Sprite => IconLoader.LoadOptional(IconName);

        public void InitRadialConfig(RadialBase radial)
        {
            Player player = Player.m_localPlayer;
            List<RadialMenuElement> elements = new List<RadialMenuElement>();
            KaiokenTier active = KaiokenRegistry.GetActive(player);

            foreach (KaiokenTier tier in KaiokenRegistry.All)
            {
                if (tier == active || !tier.IsLearned(player))
                {
                    continue;
                }

                // Cópia local: o foreach entrega a mesma variável a todas as closures.
                KaiokenTier target = tier;

                // O jogador é lido no clique, não capturado aqui — mesmo motivo do anel das formas.
                elements.Add(SaiyaRadial.Leaf(
                    target.DisplayName,
                    Subtitle(player, target),
                    IconLoader.LoadOptional(IconName),
                    () => KaiokenManager.StartTier(Player.m_localPlayer, target)));
            }

            if (active != null)
            {
                // O ícone da skill de poder, como o "Base form": sem multiplicador por cima.
                elements.Add(SaiyaRadial.Leaf(
                    "Kaioken off",
                    $"Leave {active.DisplayName}",
                    IconLoader.LoadOptional("power_level"),
                    () => KaiokenManager.StopNow(Player.m_localPlayer)));
            }

            radial.ConstructRadial(elements);
        }

        /// <summary>
        /// O que o tier custa agora, na forma ativa: stamina sempre, e vida quando a margem está
        /// negativa. É o número que decide se vale ligar aquele tier. Vem antes o multiplicador de
        /// poder, porque o rótulo da lore ("x20") não é a força de verdade.
        /// </summary>
        private static string Subtitle(Player player, KaiokenTier tier)
        {
            if (player == null)
            {
                return null;
            }

            float margin = tier.GetMargin(player);
            string text = $"x{tier.GetPowerMultiplier():0.##} power, {tier.GetStaminaPerSecond(margin):0.#} stamina/s";

            float health = tier.GetHealthPerSecond(margin);
            if (health > 0.05f)
            {
                text += $", {health:0.#} hp/s";
            }

            return text;
        }

        /// <summary>
        /// Este anel tem alguma coisa para mostrar? Ligado, sempre há o desligar; desligado, só com
        /// algum tier aprendido. Ver <see cref="FormsRadialConfig.HasContent"/>.
        /// </summary>
        internal static bool HasContent(Player player)
        {
            if (player == null)
            {
                return false;
            }

            return KaiokenRegistry.GetActive(player) != null || KaiokenRegistry.HighestLearned(player) != null;
        }
    }
}
