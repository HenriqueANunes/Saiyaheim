using System.Collections.Generic;
using Saiyaheim.Transformations;
using Saiyaheim.Util;
using UnityEngine;
using Valheim.UI;

namespace Saiyaheim.Radial
{
    /// <summary>
    /// O anel das formas: um item por degrau da escada, mais a volta à forma base.
    ///
    /// <b>A forma atual não aparece.</b> Todo item da roda é um lugar para onde ir, e o lugar onde
    /// o jogador já está não é um deles — clicar nele não teria o que fazer. Vale dos dois lados:
    /// transformado, o degrau ativo sai da roda; na forma base, é o "Base form" que sai.
    ///
    /// <b>Degrau travado não aparece.</b> A roda mostra o que dá para fazer agora, e nada mais —
    /// decidido em 2026-09-20, no lugar de mostrar o degrau travado com o motivo no subtítulo.
    /// Quem quer saber o que falta para o próximo degrau tem a mensagem do <c>TryStart</c>, que
    /// continua respondendo com o que falta em vez de ficar em silêncio.
    ///
    /// Com tudo travado e na forma base a roda fica sem item nenhum, e aí o grupo inteiro some do
    /// anel principal — ver <see cref="HasContent"/>.
    /// </summary>
    internal sealed class FormsRadialConfig : IRadialConfig
    {
        public string LocalizedName => "Forms";

        public Sprite Sprite => IconLoader.LoadOptional("ssj");

        public void InitRadialConfig(RadialBase radial)
        {
            Player player = Player.m_localPlayer;
            List<RadialMenuElement> elements = new List<RadialMenuElement>();
            Transformation active = TransformationRegistry.GetActive(player);

            foreach (Transformation form in TransformationRegistry.All)
            {
                if (form == active || !form.IsUnlocked(player))
                {
                    continue;
                }

                // Cópia local: o foreach entrega a mesma variável a todas as closures.
                Transformation target = form;

                // O jogador é lido no clique, não capturado aqui: o anel principal da vanilla
                // guarda o último item usado e o devolve à roda na abertura seguinte, então um
                // elemento do mod pode sobreviver a uma morte ou a uma troca de personagem.
                elements.Add(SaiyaRadial.Leaf(
                    target.DisplayName,
                    Subtitle(player, target),
                    IconLoader.LoadOptional(target.Id),
                    () => TransformationManager.TransformTo(Player.m_localPlayer, target)));
            }

            if (active != null)
            {
                // O ícone da skill de poder: a forma base é o poder do jogador sem multiplicador
                // nenhum por cima, que é exatamente o que aquela skill mede.
                elements.Add(SaiyaRadial.Leaf(
                    "Base form",
                    $"Leave {active.DisplayName}",
                    IconLoader.LoadOptional("power_level"),
                    () => TransformationManager.PowerDownNow(Player.m_localPlayer)));
            }

            radial.ConstructRadial(elements);
        }

        /// <summary>
        /// A linha de baixo do item: o que o jogador precisa saber <b>antes</b> de clicar. É o
        /// dreno, único custo que a forma tem e o número que a maestria melhora. Não há caso de
        /// forma travada aqui — ela nem chega a virar item.
        /// </summary>
        private static string Subtitle(Player player, Transformation form)
        {
            if (player == null)
            {
                return null;
            }

            return $"{form.GetKiDrainPerSecond(player):0.#} ki/s";
        }

        /// <summary>
        /// Este anel tem alguma coisa para mostrar? Chamado pelo <see cref="RadialMainMenuPatch"/>
        /// antes de pendurar o grupo: um grupo que abre um anel vazio é um item morto na roda
        /// principal, do mesmo tipo que o <c>EmptyElement</c> que o patch remove.
        /// </summary>
        internal static bool HasContent(Player player)
        {
            if (player == null)
            {
                return false;
            }

            // Transformado, sempre há para onde ir: a volta à forma base.
            if (TransformationRegistry.GetActive(player) != null)
            {
                return true;
            }

            foreach (Transformation form in TransformationRegistry.All)
            {
                if (form.IsUnlocked(player))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
