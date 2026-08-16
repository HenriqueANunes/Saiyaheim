using System.Collections.Generic;
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
    /// saiya_ki pose         segura a pose de carregamento, para calibrar
    /// </code>
    ///
    /// Como no <see cref="PowerCommand"/>: ler o estado é livre, mas tudo que escreve no ki é
    /// trapaça e exige <c>devcommands</c>. Ver <see cref="SaiyaheimCommand"/>.
    /// </summary>
    internal class KiCommand : SaiyaheimCommand
    {
        public override string Name => "saiya_ki";

        public override string Help =>
            "Tests ki. Usage: saiya_ki [set <n> | drain <n> | full | empty | toggle | pose]";

        public override List<string> CommandOptionList() =>
            new List<string> { "set", "drain", "full", "empty", "toggle", "pose" };

        protected override void Execute(string[] args)
        {
            if (KiManager.State == null)
            {
                Print("No player. Join a world first.");
                return;
            }

            string action = args.Length > 0 ? args[0].ToLowerInvariant() : null;

            // Sem argumento é leitura pura; quase todo subcomando daqui escreve no ki, então passam
            // pelo porteiro. O typo é rejeitado antes, senão "isso é cheat" sai para quem só errou
            // de digitar.
            //
            // O 'pose' é a exceção e sai antes: ele não toca no ki, só segura um desenho na tela
            // para calibrar os números da pose no ConfigurationManager sem a barra encher e o
            // carregamento parar sozinho no meio do ajuste. Exigir devcommands para olhar o próprio
            // personagem seria porteiro sem porta.
            if (action == "pose")
            {
                KiChargePose.DebugHold = !KiChargePose.DebugHold;
                Print($"Charging pose held: {(KiChargePose.DebugHold ? "on" : "off")}" +
                      $"{(SaiyaheimConfig.ChargePoseEnabled.Value ? "" : " (but ChargePose.Enabled is off)")}");
                return;
            }

            if (action != null)
            {
                if (!CommandOptionList().Contains(action))
                {
                    Print($"Unknown action: '{action}'. {Help}");
                    return;
                }

                if (!RequireCheats(action))
                {
                    return;
                }
            }

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

            // O Rested mexe na linha de regen acima e na pausa pos-gasto, e nao ha como olhar a
            // barra e saber se o buff esta ativo. Sem isto, calibrar os dois multiplicadores seria
            // adivinhacao.
            Print($"Rested: {(KiManager.IsRested(player) ? "yes" : "no")}  " +
                  $"(regen blocked for {KiManager.RegenDelayFor(player):0.#} s after spending)");
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
    }
}
