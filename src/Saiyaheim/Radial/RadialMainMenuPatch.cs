using System.Collections.Generic;
using HarmonyLib;
using Valheim.UI;

namespace Saiyaheim.Radial
{
    /// <summary>
    /// Os dois grupos do mod — formas e ataques de ki — entram no <b>anel principal da vanilla</b>,
    /// o que abre na tecla do jogo, ao lado de consumíveis, armas, emotes e companhia.
    ///
    /// <b>Dentro do anel do jogo e não num anel próprio</b>, decidido em 2026-09-18 depois de
    /// jogar com os dois: o mod não gasta tecla nenhuma, e quem já abre a roda para trocar de arma
    /// acha a forma no mesmo lugar. O anel próprio custava uma tecla a mais e um segundo patch.
    ///
    /// <b>Prefixo e não postfixo.</b> O <c>ValheimRadialConfig.InitRadialConfig</c> monta a lista
    /// dos oito itens e a entrega ao <c>ConstructRadial</c>; interceptar a lista <i>antes</i> é o
    /// que permite acrescentar sem copiar nada da vanilla. Um postfixo chegaria tarde demais: o
    /// <c>ConstructRadial</c> começa com um <c>ClearElements</c> que dá <c>Destroy</c> em todo
    /// filho do container, então reconstruir depois mataria os oito e obrigaria o mod a
    /// reimplementar o anel principal inteiro.
    ///
    /// <b>Cabe sem ajuste.</b> O <c>MaxElementsRange</c> do jogo é <c>[8, 12]</c>: a vanilla
    /// entrega 8 e enche a camada; com os dois do mod são 10, o <c>SetElementsPerLayer</c> passa
    /// para 12 por camada, e continua sendo uma camada só — sem paginação e sem item cortado.
    ///
    /// É o <b>único</b> patch Harmony do menu radial; todo o resto é API pública.
    /// Ver [[Menu Radial]].
    /// </summary>
    [HarmonyPatch(typeof(RadialBase), nameof(RadialBase.ConstructRadial))]
    internal static class RadialMainMenuPatch
    {
        private static void Prefix(RadialBase __instance, List<RadialMenuElement> elements)
        {
            // CurrentConfig já é a config que está montando: o Open escreve nele antes de chamar
            // o InitRadialConfig. Qualquer outra página — grupos de item, emotes, os anéis do
            // próprio mod — cai fora por aqui.
            if (elements == null || !(__instance.CurrentConfig is ValheimRadialConfig))
            {
                return;
            }

            if (Player.m_localPlayer == null)
            {
                return;
            }

            // O anel principal reserva o último lugar para o "último item usado", e quem escreve
            // ali é o próprio jogo, em todo clique que não seja num grupo. Sem esta linha, clicar
            // no SSJ2 faz o SSJ2 reaparecer na roda principal na abertura seguinte — um item que
            // só diz em que forma o jogador está, e que não faz nada ao ser clicado, porque entrar
            // na forma em que já se está é recusado.
            //
            // O elemento continua sendo o LastUsed do jogo depois de sair da lista; ele é
            // destruído pelo próprio RadialBase assim que o jogador usar qualquer item da vanilla.
            elements.RemoveAll(SaiyaRadial.IsOurs);

            // O anel principal tapa com EmptyElement os dois slots que pode não ter o que
            // preencher: o do martelo, quando não há martelo no inventário, e o do último item
            // usado, quando ainda não se usou nada. É um item sem nome, sem ícone e sem ação —
            // clicar nele não faz nada. Ao entrar no mundo o LastUsed é sempre nulo, então a
            // primeira abertura da roda sempre mostrava um desses; ele sumia depois porque
            // qualquer clique preenche o LastUsed.
            //
            // Os dois itens do mod entram no lugar, então a roda não fica com buraco.
            elements.RemoveAll(element =>
            {
                if (!(element is EmptyElement) || SaiyaRadial.IsOurs(element))
                {
                    return false;
                }

                // Destruir aqui porque o elemento sai da lista antes de virar filho do
                // container, e o ClearElements do ConstructRadial só varre os filhos de lá —
                // sem isto, cada abertura da roda deixaria um GameObject órfão na cena.
                // O LastUsed é exceção: quem manda nele é o RadialBase.
                if (element != __instance.LastUsed)
                {
                    UnityEngine.Object.Destroy(element.gameObject);
                }

                return true;
            });

            // back = a config que está montando: voltar de dentro de um grupo cai no anel
            // principal, como em qualquer outro grupo da roda.
            elements.Add(SaiyaRadial.Group(__instance, new FormsRadialConfig(), __instance.CurrentConfig));
            elements.Add(SaiyaRadial.Group(__instance, new KiAttacksRadialConfig(), __instance.CurrentConfig));
        }
    }
}
