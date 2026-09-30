using System.Collections.Generic;
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
    /// Aqui a exclusividade fica <b>só entre modificadores</b>, e só onde ela resolve alguma coisa:
    /// o combo exige os modificadores dele segurados, e um modificador <b>não declarado</b> só
    /// bloqueia quando outro atalho do mod usa a mesma tecla principal com ele. É o mínimo para que
    /// <c>T</c> e <c>Shift+T</c> nunca disparem juntos.
    ///
    /// <b>Por que não exigir todos os outros modificadores soltos.</b> Shift é também o correr do
    /// Valheim. Com a regra estrita, <c>F</c> não decolava correndo — o gesto natural para sair do
    /// chão em velocidade —, e o mesmo valia para toda tecla sem par Shift (<c>K</c>, <c>R</c>).
    /// Quem não tem irmão com Shift não tem nada a separar, então o Shift segurado não conta.
    /// Esquerda e direita contam como o mesmo modificador nesse bloqueio: com <c>LeftShift+T</c>
    /// bindado, <c>T</c> não dispara com nenhum dos dois Shifts.
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
                   && ModifiersMatch(entry, shortcut);
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
                   && ModifiersMatch(entry, shortcut);
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

        private static bool ModifiersMatch(ConfigEntry<KeyboardShortcut> entry, KeyboardShortcut shortcut)
        {
            foreach (KeyCode modifier in ModifierKeys)
            {
                // A tecla principal entra na conta: bindar Shift sozinho não pode se auto-bloquear.
                bool partOfCombo = modifier == shortcut.MainKey || Declares(shortcut, modifier);
                bool held = Input.GetKey(modifier);

                if (partOfCombo && !held)
                {
                    return false;
                }

                if (!partOfCombo && held && SiblingClaims(entry, shortcut.MainKey, modifier))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Outro atalho do mod usa a mesma tecla principal com este modificador (ou o do outro
        /// lado)? Então o modificador segurado é dele, e este atalho não dispara.
        /// </summary>
        private static bool SiblingClaims(ConfigEntry<KeyboardShortcut> entry, KeyCode mainKey, KeyCode modifier)
        {
            KeyCode twin = Twin(modifier);

            foreach (ConfigEntry<KeyboardShortcut> other in ShortcutsOf(entry.ConfigFile))
            {
                if (other == entry)
                {
                    continue;
                }

                // Valor lido na hora: rebindar pelo .cfg ou pelo ConfigurationManager vale sem
                // reiniciar.
                KeyboardShortcut shortcut = other.Value;

                if (shortcut.MainKey == mainKey
                    && (Declares(shortcut, modifier) || Declares(shortcut, twin)))
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly Dictionary<ConfigFile, List<ConfigEntry<KeyboardShortcut>>> ShortcutCache =
            new Dictionary<ConfigFile, List<ConfigEntry<KeyboardShortcut>>>();

        /// <summary>
        /// Todos os atalhos do arquivo de config, lidos do próprio arquivo e não de uma lista
        /// mantida à mão: tecla nova no <c>SaiyaheimConfig</c> entra no bloqueio sem ninguém
        /// lembrar. As entradas só são criadas no bind, então a lista é montada uma vez.
        /// </summary>
        private static List<ConfigEntry<KeyboardShortcut>> ShortcutsOf(ConfigFile file)
        {
            if (ShortcutCache.TryGetValue(file, out List<ConfigEntry<KeyboardShortcut>> list))
            {
                return list;
            }

            list = new List<ConfigEntry<KeyboardShortcut>>();

            foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> pair in file)
            {
                if (pair.Value is ConfigEntry<KeyboardShortcut> shortcut)
                {
                    list.Add(shortcut);
                }
            }

            ShortcutCache[file] = list;
            return list;
        }

        private static KeyCode Twin(KeyCode modifier)
        {
            switch (modifier)
            {
                case KeyCode.LeftShift: return KeyCode.RightShift;
                case KeyCode.RightShift: return KeyCode.LeftShift;
                case KeyCode.LeftControl: return KeyCode.RightControl;
                case KeyCode.RightControl: return KeyCode.LeftControl;
                case KeyCode.LeftAlt: return KeyCode.RightAlt;
                case KeyCode.RightAlt: return KeyCode.LeftAlt;
                default: return modifier;
            }
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
