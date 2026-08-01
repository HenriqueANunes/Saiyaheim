using System;
using System.Collections.Generic;
using Saiyaheim.Flight;
using Saiyaheim.Ki;
using Saiyaheim.Power;

namespace Saiyaheim.Debugging
{
    /// <summary>
    /// Inspeção e teste do voo.
    ///
    /// Existe pelo mesmo motivo do <see cref="PowerCommand"/>: os números do voo (velocidade
    /// efetiva, custo de ki por segundo, penalidade de peso) não aparecem em lugar nenhum da tela,
    /// e calibrar no escuro é exatamente o que o projeto tenta evitar.
    ///
    /// <code>
    /// saiya_fly              mostra os números do voo agora
    /// saiya_fly skill 50     define o nível da skill de voo (testa o topo da curva sem grind)
    /// saiya_fly xp 10        joga XP na skill de voo
    /// </code>
    ///
    /// Como no <see cref="PowerCommand"/>: ler é livre, <c>skill</c> e <c>xp</c> pedem
    /// <c>devcommands</c>. Ver <see cref="SaiyaheimCommand"/>.
    /// </summary>
    internal class FlightCommand : SaiyaheimCommand
    {
        public override string Name => "saiya_fly";

        public override string Help =>
            "Inspects flight. Usage: saiya_fly [skill <level> | xp <amount>]";

        public override List<string> CommandOptionList() => new List<string> { "skill", "xp" };

        protected override void Execute(string[] args)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Print("No player. Join a world first.");
                return;
            }

            if (!FlightSkill.IsRegistered)
            {
                Print("The 'Flight' skill was not registered. Check the BepInEx log.");
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
                        Print("Usage: saiya_fly skill <level 0-100>");
                        return;
                    }

                    if (!TrySetLevel(player, Math.Max(0f, Math.Min(level, FlightSkill.MaxLevel))))
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
                        Print("Usage: saiya_fly xp <amount>");
                        return;
                    }

                    player.RaiseSkill(FlightSkill.Type, xp);
                    break;

                default:
                    Print($"Unknown action: '{action}'. {Help}");
                    return;
            }

            float slow = FlightStats.GetSlowSpeed(player);
            float fast = FlightStats.GetFastSpeed(player);
            float slowCost = FlightStats.GetKiCostPerSecond(player, fast: false);
            float fastCost = FlightStats.GetKiCostPerSecond(player, fast: true);

            Print($"Flying: {(FlightManager.IsFlying(player) ? "yes" : "no")}  (ki {(KiManager.IsEnabled ? "on" : "off")})");
            Print($"Flight skill: level {FlightSkill.GetLevel(player):0.#}");
            Print($"Carry load: {FlightStats.GetWeightLoad(player) * 100f:0}% of max weight");
            Print($"Speed floor: {SaiyaheimConfig.FlightBaseSpeed.Value:0.#} " +
                  $"+ {FlightStats.GetSpeedFromPower(player):0.#} from power level " +
                  $"(raw {PowerLevel.GetRaw(player):0.#})");
            Print($"Speed: {slow:0.#} normal / {fast:0.#} running  (cap {SaiyaheimConfig.FlightMaxSpeed.Value:0.#})");
            Print($"Ki cost: {slowCost:0.##}/s normal, {fastCost:0.##}/s running");

            // A economia do fim de jogo e invisivel no custo final — sem imprimir o fator nao da
            // para saber se o KiPowerReduction esta fazendo alguma coisa ou se o termo ainda dorme.
            float costFactor = FlightStats.GetPowerCostFactor(player);
            if (costFactor < 1f)
            {
                Print($"  late-game discount: x{costFactor:0.###} " +
                      $"({(1f - costFactor) * 100f:0}% cheaper, from +{PowerLevel.GetLateGameBonus(player):0.#} power)");
            }
            Print($"Ki: {KiManager.State?.Current ?? 0f:0.#}/{KiManager.Max:0.#} " +
                  $"— {SecondsOfFlight(slowCost):0} s of normal flight left");
        }

        /// <summary>Autonomia restante. É o número que decide se dá para atravessar aquele vale.</summary>
        private static float SecondsOfFlight(float costPerSecond)
        {
            return costPerSecond <= 0f ? float.PositiveInfinity : KiManager.Current / costPerSecond;
        }

        /// <summary>
        /// Mesmo caminho do <see cref="PowerCommand"/>: <c>CheatRaiseSkill</c> casa a skill pelo
        /// <c>ToString()</c> do enum e uma skill custom do Jotunn não tem nome de enum. Sobra
        /// mexer no <c>Skill.m_level</c>, que é público — e um <c>RaiseSkill</c> mínimo antes, para
        /// forçar a criação da entrada de uma skill nunca usada.
        /// </summary>
        private static bool TrySetLevel(Player player, float level)
        {
            Skills skills = player.GetSkills();
            if (skills == null)
            {
                return false;
            }

            player.RaiseSkill(FlightSkill.Type, 0.0001f);

            foreach (Skills.Skill skill in skills.GetSkillList())
            {
                if (skill.m_info == null || skill.m_info.m_skill != FlightSkill.Type)
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
