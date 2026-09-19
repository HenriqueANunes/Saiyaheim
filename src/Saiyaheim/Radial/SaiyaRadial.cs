using System;
using System.Collections.Generic;
using Saiyaheim.Util;
using UnityEngine;
using Valheim.UI;

namespace Saiyaheim.Radial
{
    /// <summary>
    /// A fábrica de elementos de anel do mod, usada pelas páginas que o
    /// <see cref="RadialMainMenuPatch"/> pendura no anel principal da vanilla.
    ///
    /// <b>Nada aqui é asset.</b> O framework de menu radial que o Valheim ganhou na
    /// <c>l-1.0.7</c> é público e <c>IRadialConfig</c> é uma interface pura, então uma página do
    /// mod é uma classe C# comum — sem <c>ScriptableObject</c>, sem prefab próprio, sem passar
    /// pelo Unity. Os prefabs de elemento (<c>EmptyElement</c>, <c>GroupElement</c>) saem do
    /// <c>RadialData.SO</c> do próprio jogo. Ver [[Menu Radial]].
    /// </summary>
    internal static class SaiyaRadial
    {
        /// <summary>
        /// Um item folha: nome, subtítulo, ícone e o que ele faz.
        ///
        /// <b>O contrato do <paramref name="action"/> é "deu certo?"</b>, e ele decide sozinho se
        /// o menu fecha: transformar fecha, esbarrar numa forma travada não fecha — o menu se
        /// redesenha com a mensagem da trava na tela e o jogador escolhe outra coisa. É por isso
        /// que o <c>Interact</c> daqui devolve sempre true: no <c>RadialBase</c>, um
        /// <c>Interact</c> falso é "elemento morto" e fecha o menu de qualquer jeito, sem
        /// consultar o <c>CloseOnInteract</c>. Quem responde a pergunta de fechar é ele.
        /// </summary>
        internal static RadialMenuElement Leaf(string name, string subTitle, Sprite icon, Func<bool> action)
        {
            EmptyElement element = UnityEngine.Object.Instantiate(RadialData.SO.EmptyElement);

            // Init() do EmptyElement escreve Name = "Empty" e um Interact que devolve false. Os
            // dois são sobrescritos logo abaixo; ele é chamado mesmo assim porque é o único lugar
            // que prepara o elemento, e o que ele faz além disso pode crescer numa atualização.
            element.Init();

            element.gameObject.AddComponent<SaiyaRadialMark>();

            GameAccess.SetElementName(element, name);
            GameAccess.SetElementSubTitle(element, subTitle);

            bool worked = false;

            element.Interact = () =>
            {
                worked = action != null && action();
                return true;
            };
            element.CloseOnInteract = () => worked;

            SetIcon(element, icon);

            return element;
        }

        /// <summary>
        /// Um item que abre outro anel. <c>GroupElement.Init</c> é público e recebe a
        /// <c>IRadialConfig</c>, então ele já amarra o <c>Interact</c> no
        /// <c>QueuedOpen(config, back)</c> — voltar para o anel de origem sai de graça, e o nome e
        /// o ícone vêm do <c>LocalizedName</c> e do <c>Sprite</c> da própria config.
        /// </summary>
        internal static GroupElement Group(RadialBase radial, IRadialConfig config, IRadialConfig back)
        {
            GroupElement group = UnityEngine.Object.Instantiate(RadialData.SO.GroupElement);
            group.Init(config, back, radial);

            return group;
        }

        /// <summary>Este elemento nasceu do <see cref="Leaf"/>?</summary>
        internal static bool IsOurs(RadialMenuElement element)
        {
            return element != null && element.GetComponent<SaiyaRadialMark>() != null;
        }

        private static void SetIcon(RadialMenuElement element, Sprite icon)
        {
            if (element.Icon == null)
            {
                return;
            }

            element.Icon.sprite = icon;

            // Mesmo caminho do GroupElement.Init da vanilla: sem sprite, o objeto do ícone sai de
            // cena em vez de desenhar um quadrado branco.
            element.Icon.gameObject.SetActive(icon != null);
        }
    }

    /// <summary>
    /// Etiqueta vazia: marca um elemento de anel como sendo do mod, para o
    /// <see cref="RadialMainMenuPatch"/> reconhecê-lo de volta.
    ///
    /// Um componente e não uma lista de instâncias porque o jogo destrói e recria elementos por
    /// conta própria, e a etiqueta morre junto com o objeto — uma lista viraria um cemitério de
    /// referências mortas.
    /// </summary>
    internal sealed class SaiyaRadialMark : MonoBehaviour
    {
    }
}
