using System;
using System.Collections.Generic;
using Saiyaheim.Ki;
using Saiyaheim.Power;
using Saiyaheim.Transformations;

namespace Saiyaheim.Debugging
{
    /// <summary>
    /// Inspeção e teste das transformações.
    ///
    /// Existe pelo mesmo motivo do <see cref="FlightCommand"/>: os números da forma — quanto ela
    /// multiplica, quanto drena agora, quantos segundos de barra isso dá — não aparecem em lugar
    /// nenhum da tela, e a curva de maestria leva horas para subir sozinha. Sem
    /// <c>saiya_form skill 100</c> não há como olhar o topo da curva antes de o playtest chegar lá.
    ///
    /// <code>
    /// saiya_form              mostra os números da forma agora
    /// saiya_form skill 50     define o nível de maestria da forma ativa (ou da primeira)
    /// saiya_form xp 100       joga XP na skill de maestria
    /// </code>
    ///
    /// Como nos outros: ler é livre, <c>skill</c> e <c>xp</c> pedem <c>devcommands</c>.
    /// </summary>
    internal class TransformCommand : SaiyaheimCommand
    {
        public override string Name => "saiya_form";

        public override string Help =>
            "Inspects transformations. Usage: saiya_form [skill <level> | xp <amount>]";

        public override List<string> CommandOptionList() => new List<string> { "skill", "xp" };

        protected override void Execute(string[] args)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Print("No player. Join a world first.");
                return;
            }

            // O comando opera sobre a forma ATIVA quando há uma, para que mexer na skill enquanto
            // transformado afete o que está na tela. Fora da forma sobra o primeiro degrau da
            // escada, que hoje é o único.
            Transformation form = TransformationRegistry.GetActive(player)
                                  ?? TransformationRegistry.Next(null);

            if (form == null)
            {
                Print("No transformations are registered.");
                return;
            }

            if (!form.IsRegistered)
            {
                Print($"The '{form.DisplayName}' skill was not registered. Check the BepInEx log.");
                return;
            }

            string action = args.Length > 0 ? args[0].ToLowerInvariant() : null;

            switch (action)
            {
                case null:
                    break;

                case "skill":
                    if (!RequireCheats("skill"))
                    {
                        return;
                    }

                    if (!TryParseAmount(args, out float level))
                    {
                        Print("Usage: saiya_form skill <level 0-100>");
                        return;
                    }

                    if (!TrySetLevel(player, form, Math.Max(0f, Math.Min(level, 100f))))
                    {
                        Print("Could not change the skill level.");
                        return;
                    }
                    break;

                case "xp":
                    if (!RequireCheats("xp"))
                    {
                        return;
                    }

                    if (!TryParseAmount(args, out float xp))
                    {
                        Print("Usage: saiya_form xp <amount>");
                        return;
                    }

                    player.RaiseSkill(form.SkillType, xp);
                    break;

                default:
                    Print($"Unknown action: '{action}'. {Help}");
                    return;
            }

            Transformation active = TransformationRegistry.GetActive(player);
            float drain = form.GetKiDrainPerSecond(player);

            Print($"Form: {(active == null ? "none" : active.DisplayName)}  " +
                  $"(ki {(KiManager.IsEnabled ? "on" : "off")})");
            Print($"{form.DisplayName} mastery: level {form.GetSkillLevel(player):0.#}");
            Print($"Power multiplier: x{form.GetPowerMultiplier():0.##}");
            Print($"Ki drain: {drain:0.##}/s " +
                  $"(base {form.Config.KiDrainPerSecond.Value:0.##}, " +
                  $"mastery cuts {(1f - SafeRatio(drain, form.Config.KiDrainPerSecond.Value)) * 100f:0}%)");
            Print($"Ki: {KiManager.Current:0.#}/{KiManager.Max:0.#} " +
                  $"— {SecondsOfForm(drain):0} s in form" +
                  $"{(active == null ? " if you transformed now" : " left")}");

            // O ponto inteiro da mecanica e' o salto de poder. Imprimir os dois lados evita ter que
            // transformar, rodar saiya_power, destransformar e rodar de novo para comparar.
            float combat = PowerLevel.GetCombatRaw(player);
            float multiplier = form.GetPowerMultiplier();
            float outOfForm = active == null ? combat : combat / multiplier;

            float inForm = outOfForm * multiplier;

            Print($"Combat power: {outOfForm:0.#} base → {inForm:0.#} in form");
            Print($"  armor {PowerLevel.ArmorFor(outOfForm):0} → {PowerLevel.ArmorFor(inForm):0}, " +
                  $"punch bonus {PowerLevel.PunchBonusFor(outOfForm):0.#} → " +
                  $"{PowerLevel.PunchBonusFor(inForm):0.#}");
        }

        /// <summary>Autonomia da forma. É o número que diz se dá para entrar nela nesta luta.</summary>
        private static float SecondsOfForm(float drainPerSecond)
        {
            return drainPerSecond <= 0f ? float.PositiveInfinity : KiManager.Current / drainPerSecond;
        }

        private static float SafeRatio(float value, float reference)
        {
            return reference <= 0f ? 1f : value / reference;
        }

        /// <summary>
        /// Mesmo caminho do <see cref="PowerCommand"/> e do <see cref="FlightCommand"/>: o
        /// <c>CheatRaiseSkill</c> do jogo casa a skill pelo <c>ToString()</c> do enum, e uma skill
        /// custom do Jotunn não tem nome de enum. Sobra mexer no <c>Skill.m_level</c>, que é
        /// público — com um <c>RaiseSkill</c> mínimo antes, para forçar a criação da entrada de uma
        /// skill nunca usada.
        /// </summary>
        private static bool TrySetLevel(Player player, Transformation form, float level)
        {
            Skills skills = player.GetSkills();
            if (skills == null)
            {
                return false;
            }

            player.RaiseSkill(form.SkillType, 0.0001f);

            foreach (Skills.Skill skill in skills.GetSkillList())
            {
                if (skill.m_info == null || skill.m_info.m_skill != form.SkillType)
                {
                    continue;
                }

                skill.m_level = level;
                skill.m_accumulator = 0f;
                return true;
            }

            return false;
        }
    }
}
