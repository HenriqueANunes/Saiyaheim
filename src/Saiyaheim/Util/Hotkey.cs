using BepInEx.Configuration;
using UnityEngine;

namespace Saiyaheim.Util
{
    /// <summary>
    /// Teste de atalho que separa <c>T</c> de <c>Shift+T</c> <b>sem</b> quebrar quem está andando.
    ///
    /// <b>Por que não usar o <c>KeyboardShortcut.IsDown</c> do BepInEx.</b> Ele exige exclusividade
    /// total: varre todos os KeyCodes suportados e devolve false se qualquer tecla fora do combo
    /// estiver pressionada. Segurando W, <c>T</c> simplesmente não dispara — e transformar correndo
    /// é justamente o caso de uso. (Vale para toda tecla do mod que use <c>IsDown</c> direto.)
    ///
    /// Aqui a exclusividade fica <b>só entre modificadores</b>: o combo exige os modificadores dele
    /// segurados e os outros soltos, e ignora o resto do teclado. É o mínimo para que <c>T</c> e
    /// <c>Shift+T</c> nunca disparem juntos.
    ///
    /// <b>Toda tecla do mod passa por aqui</b>, tanto de toque quanto de segurar (carregar ki
    /// andando cai no mesmo problema: <c>IsPressed</c> do BepInEx recusa o R com W pressionado).
    ///
    /// Esquerda e direita continuam distintas, como no BepInEx: quem bindar <c>LeftShift+T</c> não
    /// aciona com o Shift direito.
    /// </summary>
    internal static class Hotkey
    {
        private static readonly KeyCode[] ModifierKeys =
        {
            KeyCode.LeftShift, KeyCode.RightShift,
            KeyCode.LeftControl, KeyCode.RightControl,
            KeyCode.LeftAlt, KeyCode.RightAlt
        };

        /// <summary>Tecla principal pressionada neste frame, com os modificadores certos.</summary>
        internal static bool IsDown(ConfigEntry<KeyboardShortcut> entry)
        {
            if (entry == null)
            {
                return false;
            }

            KeyboardShortcut shortcut = entry.Value;

            return shortcut.MainKey != KeyCode.None
                   && Input.GetKeyDown(shortcut.MainKey)
                   && ModifiersMatch(shortcut);
        }

        /// <summary>Tecla principal segurada agora, com os modificadores certos.</summary>
        internal static bool IsPressed(ConfigEntry<KeyboardShortcut> entry)
        {
            if (entry == null)
            {
                return false;
            }

            KeyboardShortcut shortcut = entry.Value;

            return shortcut.MainKey != KeyCode.None
                   && Input.GetKey(shortcut.MainKey)
                   && ModifiersMatch(shortcut);
        }

        /// <summary>
        /// Tecla principal ainda segurada, <b>ignorando os modificadores</b>.
        ///
        /// Existe para soltar um gesto que já começou, e não para começá-lo: quem decide se o
        /// gesto vale é o <see cref="IsDown"/>, com os modificadores exigidos. Depois disso a
        /// pergunta muda — não é mais "o jogador quer isto?", é "ele ainda está segurando?" — e
        /// aí exigir os modificadores é um bug: encostar no Shift no meio de um Kamehameha
        /// carregado faria o <see cref="IsPressed"/> virar false e o tiro sair sozinho.
        /// </summary>
        internal static bool IsMainKeyHeld(ConfigEntry<KeyboardShortcut> entry)
        {
            if (entry == null)
            {
                return false;
            }

            KeyCode main = entry.Value.MainKey;

            return main != KeyCode.None && Input.GetKey(main);
        }

        private static bool ModifiersMatch(KeyboardShortcut shortcut)
        {
            foreach (KeyCode modifier in ModifierKeys)
            {
                // A tecla principal entra na conta: bindar Shift sozinho não pode se auto-bloquear.
                bool partOfCombo = modifier == shortcut.MainKey || Declares(shortcut, modifier);

                if (Input.GetKey(modifier) != partOfCombo)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Declares(KeyboardShortcut shortcut, KeyCode key)
        {
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (modifier == key)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
