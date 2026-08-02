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
