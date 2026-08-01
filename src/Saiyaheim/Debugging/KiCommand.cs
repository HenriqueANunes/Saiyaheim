using System;
using System.Collections.Generic;
using System.Globalization;
using Jotunn.Entities;
using Saiyaheim.Ki;
using Saiyaheim.Power;

namespace Saiyaheim.Debugging
{
    /// <summary>
    /// Comando de teste do ki. Existe para exercitar a barra e a persistência **antes** de
    /// existir transformação, voo ou qualquer coisa que gaste ki de verdade — senão não há
    /// como testar a etapa 2 isoladamente.
    ///
    /// <code>
    /// saiya_ki              estado atual
    /// saiya_ki set 50       define o ki
    /// saiya_ki drain 20     gasta (aciona o delay de regeneração)
    /// saiya_ki full         enche
    /// saiya_ki empty        zera
    /// saiya_ki toggle       liga/desliga o ki
    /// </code>
    /// </summary>
    internal class KiCommand : ConsoleCommand
    {
        public override string Name => "saiya_ki";

        public override string Help =>
            "Tests ki. Usage: saiya_ki [set <n> | drain <n> | full | empty | toggle]";

        public override List<string> CommandOptionList() =>
            new List<string> { "set", "drain", "full", "empty", "toggle" };

        public override void Run(string[] args)
        {
            if (KiManager.State == null)
            {
                Print("No player. Join a world first.");
                return;
            }

            string action = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : null;

            switch (action)
            {
                case null:
                    break;

                case "set":
                case "drain":
                    if (!TryParseAmount(args, out float amount))
                    {
                        Print($"Usage: saiya_ki {action} <number>");
                        return;
                    }

                    if (action == "set")
                    {
                        KiManager.SetCurrent(amount);
                    }
                    else
                    {
                        KiManager.Drain(amount);
                    }
                    break;

                case "full":
                    KiManager.SetCurrent(KiManager.Max);
                    break;

                case "empty":
                    KiManager.SetCurrent(0f);
                    break;

                case "toggle":
                    KiManager.State.Enabled = !KiManager.State.Enabled;
                    break;

                default:
                    Print($"Unknown action: '{action}'. {Help}");
                    return;
            }

            Print($"Ki: {KiManager.State.Current:0.#}/{KiManager.Max:0.#} " +
                  $"({(KiManager.State.Enabled ? "on" : "off")}" +
                  $"{(KiManager.IsCharging ? ", charging" : "")})");

            Player player = Player.m_localPlayer;
            float regen = KiManager.RegenPerSecondFor(player);
            float charge = KiManager.ChargePerSecondFor(player);

            Print($"Power level: {PowerLevel.GetRaw(player):0.#} raw");
            Print($"Regen: {regen:0.##}/s  ({SecondsToFill(regen)} to fill)");
            Print($"Charge: {charge:0.##}/s  ({SecondsToFill(charge)} to fill)");
        }

        /// <summary>
        /// Segundos para encher, formatado. <b>É este o número que a calibração da recarga
        /// persegue</b> — o ki por segundo sozinho engana, porque o teto cresce junto com ele.
        /// Compare o mesmo valor em nível de skill baixo e alto (<c>saiya_power skill 100</c>).
        /// </summary>
        private static string SecondsToFill(float perSecond)
        {
            float seconds = KiManager.SecondsToFill(perSecond);
            return float.IsInfinity(seconds) ? "never" : $"{seconds:0} s";
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
