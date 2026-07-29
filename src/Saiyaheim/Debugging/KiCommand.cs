using System;
using System.Collections.Generic;
using System.Globalization;
using Jotunn.Entities;
using Saiyaheim.Ki;

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
            "Testa o ki. Uso: saiya_ki [set <n> | drain <n> | full | empty | toggle]";

        public override List<string> CommandOptionList() =>
            new List<string> { "set", "drain", "full", "empty", "toggle" };

        public override void Run(string[] args)
        {
            if (KiManager.State == null)
            {
                Print("Sem jogador. Entre em um mundo primeiro.");
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
                        Print($"Uso: saiya_ki {action} <numero>");
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
                    Print($"Ação desconhecida: '{action}'. {Help}");
                    return;
            }

            Print($"Ki: {KiManager.State.Current:0.#}/{KiManager.Max:0.#} " +
                  $"({(KiManager.State.Enabled ? "ligado" : "desligado")}" +
                  $"{(KiManager.IsCharging ? ", carregando" : "")})");
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
