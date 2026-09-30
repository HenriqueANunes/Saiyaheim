using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Saiyaheim.Util
{
    /// <summary>
    /// Nome sem espaço para as skills do mod nos comandos <c>raiseskill</c> e <c>resetskill</c>:
    /// <c>raiseskill PowerLevel 10</c>, <c>raiseskill SSJGod 10</c>. Issue #4 do GitHub.
    ///
    /// <b>O problema.</b> O console separa os argumentos por espaço, então <c>raiseskill Power Level
    /// 10</c> chega como skill "Power" e quantidade "Level". O Jotunn só reconhece uma skill custom
    /// pelo nome de exibição ou pelo <c>Identifier</c>, e o nome de exibição tem espaço. A vanilla
    /// não tem esse problema porque o nome no console é o do enum (<c>WoodCutting</c>).
    ///
    /// <b>Por que não tirar o espaço do nome.</b> O nome é o que aparece na tela de skills. E o
    /// <c>Identifier</c> não muda: é dele que o Jotunn deriva o <c>SkillType</c>, e trocá-lo zeraria
    /// a skill de quem já joga.
    ///
    /// <b>Por que patch Harmony.</b> Não há API: a busca pelo nome mora no prefix que o Jotunn põe no
    /// <c>CheatRaiseSkill</c>. Este prefix roda antes dele (<c>Priority.First</c>) e só troca o
    /// apelido pelo nome de exibição; daí em diante é o caminho do Jotunn. O autocompletar (Tab)
    /// também passa a sugerir o apelido, que é o que o comando aceita.
    /// </summary>
    internal static class SkillCommandAliases
    {
        /// <summary>Apelido sem espaço, em minúsculas, para o nome de exibição.</summary>
        private static readonly Dictionary<string, string> DisplayNameByAlias =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Nome de exibição para o apelido, na grafia que o Tab vai sugerir.</summary>
        private static readonly Dictionary<string, string> AliasByDisplayName =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Chamado junto do <c>AddSkill</c>. Nome sem espaço não precisa de apelido.</summary>
        internal static void Register(string displayName)
        {
            if (string.IsNullOrEmpty(displayName) || !displayName.Contains(" "))
            {
                return;
            }

            string alias = displayName.Replace(" ", "");
            DisplayNameByAlias[alias] = displayName;
            AliasByDisplayName[displayName] = alias;
        }

        private static bool IsSkillCommand(string command)
        {
            return command == "raiseskill" || command == "resetskill";
        }

        [HarmonyPatch(typeof(Skills), nameof(Skills.CheatRaiseSkill))]
        private static class RaisePatch
        {
            [HarmonyPriority(Priority.First)]
            private static void Prefix(ref string name)
            {
                if (name != null && DisplayNameByAlias.TryGetValue(name, out string displayName))
                {
                    name = displayName;
                }
            }
        }

        [HarmonyPatch(typeof(Skills), nameof(Skills.CheatResetSkill))]
        private static class ResetPatch
        {
            [HarmonyPriority(Priority.First)]
            private static void Prefix(ref string name)
            {
                if (name != null && DisplayNameByAlias.TryGetValue(name, out string displayName))
                {
                    name = displayName;
                }
            }
        }

        /// <summary>
        /// O Jotunn põe o nome de exibição na lista do Tab, e completar com ele reproduz o bug. A
        /// lista volta em cache do <c>GetTabOptions</c>, então trocar no lugar é idempotente.
        /// </summary>
        [HarmonyPatch(typeof(Terminal.ConsoleCommand), nameof(Terminal.ConsoleCommand.GetTabOptions))]
        private static class TabOptionsPatch
        {
            private static void Postfix(Terminal.ConsoleCommand __instance, List<string> __result)
            {
                if (__result == null || !IsSkillCommand(__instance.Command))
                {
                    return;
                }

                for (int i = 0; i < __result.Count; i++)
                {
                    if (AliasByDisplayName.TryGetValue(__result[i], out string alias))
                    {
                        __result[i] = alias;
                    }
                }
            }
        }
    }
}
