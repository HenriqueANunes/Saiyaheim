using System;
using System.Collections.Generic;
using System.Globalization;
using Jotunn.Entities;
using Saiyaheim.Ki;
using Saiyaheim.Power;

namespace Saiyaheim.Debugging
{
    /// <summary>
    /// Inspeção e teste do power level.
    ///
    /// Existe porque o poder é invisível: sem HUD ainda (etapa 10), a única forma de saber se o
    /// dano do soco e a armadura estão saindo dos números certos seria inferir pela sensação de
    /// jogo — que é exatamente o tipo de iteração cega que o projeto tenta evitar.
    ///
    /// <code>
    /// saiya_power              mostra os números: fórmula em uso, poder, dano e armadura
    /// saiya_power skill 50     define o nível de Battle Power (testa o topo da curva sem grind)
    /// saiya_power xp 10        joga XP na skill
    /// </code>
    /// </summary>
    internal class PowerCommand : ConsoleCommand
    {
        public override string Name => "saiya_power";

        public override string Help =>
            "Inspects the power level. Usage: saiya_power [skill <level> | xp <amount>]";

        public override List<string> CommandOptionList() => new List<string> { "skill", "xp" };

        public override void Run(string[] args)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Print("No player. Join a world first.");
                return;
            }

            if (!PowerSkill.IsRegistered)
            {
                Print("The 'Battle Power' skill was not registered. Check the BepInEx log.");
                return;
            }

            string action = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : null;

            switch (action)
            {
                case null:
                    break;

                case "skill":
                    if (!TryParseAmount(args, out float level))
                    {
                        Print("Usage: saiya_power skill <level 0-100>");
                        return;
                    }

                    if (!TrySetLevel(player, Math.Max(0f, Math.Min(level, PowerSkill.MaxLevel))))
                    {
                        Print("Could not change the skill level.");
                        return;
                    }
                    break;

                case "xp":
                    if (!TryParseAmount(args, out float xp))
                    {
                        Print("Usage: saiya_power xp <amount>");
                        return;
                    }

                    player.RaiseSkill(PowerSkill.Type, xp);
                    break;

                default:
                    Print($"Unknown action: '{action}'. {Help}");
                    return;
            }

            bool kiOn = KiManager.IsEnabled;

            Print($"Ki: {(kiOn ? "on" : "off")} — {(kiOn ? "ki formula (HP + skill)" : "vanilla formula (HP + weapon + armor)")}");
            Print($"Battle Power: level {PowerSkill.GetLevel(player):0.#}");
            Print($"Raw power level: {PowerLevel.GetRaw(player):0.#}  (displayed: {PowerLevel.GetDisplayValue(player):0})");
            Print($"Damage added to punch: {(kiOn ? PowerLevel.GetPunchDamageBonus(player).ToString("0.#") : "0 (ki off)")}");
            Print($"Armor: {player.GetBodyArmor():0.#} {(kiOn ? "(from power, equipment ignored)" : "(from equipment)")}");
            Print($"Ki: {KiManager.State?.Current ?? 0f:0.#}/{KiManager.Max:0.#}");
        }

        /// <summary>
        /// Define o nível direto, para testar o topo da curva sem horas de grind.
        ///
        /// <c>CheatRaiseSkill</c> não serve: ele casa a skill pelo <c>ToString()</c> do enum, e
        /// uma skill custom do Jotunn não tem nome de enum — o <c>ToString()</c> dela sai como o
        /// número do hash. O caminho que funciona é mexer no <c>Skill.m_level</c>, que é público.
        ///
        /// O <c>RaiseSkill</c> de valor mínimo antes existe para forçar a criação da entrada:
        /// uma skill nunca usada não aparece em <c>GetSkillList()</c>.
        /// </summary>
        private static bool TrySetLevel(Player player, float level)
        {
            Skills skills = player.GetSkills();
            if (skills == null)
            {
                return false;
            }

            player.RaiseSkill(PowerSkill.Type, 0.0001f);

            foreach (Skills.Skill skill in skills.GetSkillList())
            {
                if (skill.m_info == null || skill.m_info.m_skill != PowerSkill.Type)
                {
                    continue;
                }

                skill.m_level = level;
                skill.m_accumulator = 0f;
                return true;
            }

            return false;
        }

        private static bool TryParseAmount(string[] args, out float amount)
        {
            amount = 0f;
            return args.Length > 1 &&
                   float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out amount);
        }

        private static void Print(string message)
        {
            SaiyaheimPlugin.Log.LogInfo(message);
            if (Console.instance != null)
            {
                Console.instance.Print($"[Saiyaheim] {message}");
            }
        }
    }
}
