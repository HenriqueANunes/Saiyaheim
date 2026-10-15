using System.Collections.Generic;
using System.Globalization;
using Saiyaheim.Ki;
using Saiyaheim.Power;
using Saiyaheim.Runes;
using Saiyaheim.Transmission;

namespace Saiyaheim.Debugging
{
    /// <summary>
    /// Inspeciona e testa o Instant Transmission (etapa 16). Sem argumento é leitura: aprendido ou
    /// não, o ki por 100 m no Power Level atual e até onde o ki atual e a barra cheia levam. É a
    /// tabela que a calibragem dos três números de custo pede.
    /// </summary>
    internal class TransmissionCommand : SaiyaheimCommand
    {
        public override string Name => "saiya_it";

        public override string Help =>
            "Instant Transmission. Usage: saiya_it [learn | forget | cost <meters>].";

        public override List<string> CommandOptionList() =>
            new List<string> { "learn", "forget", "cost" };

        protected override void Execute(string[] args)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Print("No player. Join a world first.");
                return;
            }

            string action = args.Length > 0 ? args[0].ToLowerInvariant() : null;

            if (action != null && !CommandOptionList().Contains(action))
            {
                Print($"Unknown action: '{action}'. {Help}");
                return;
            }

            switch (action)
            {
                case "learn":
                case "forget":
                    if (!RequireCheats(action))
                    {
                        return;
                    }

                    bool learn = action == "learn";
                    bool changed = learn
                        ? RuneKnowledge.Learn(player, InstantTransmission.Lesson)
                        : RuneKnowledge.Forget(player, InstantTransmission.Lesson);

                    Print($"{InstantTransmission.DisplayName}: " +
                          (learn
                              ? changed ? "learned." : "already known."
                              : changed ? "forgotten." : "was not known."));
                    break;

                case "cost":
                    if (args.Length < 2 ||
                        !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float meters))
                    {
                        Print("Usage: saiya_it cost <meters>");
                        return;
                    }

                    Print($"{InstantTransmission.FormatDistance(meters)} costs " +
                          $"{InstantTransmission.GetKiCost(player, meters):0.#} ki.");
                    break;
            }

            PrintStatus(player);
        }

        private void PrintStatus(Player player)
        {
            float current = KiManager.Current;
            float max = KiManager.MaxFor(player);

            Print($"{InstantTransmission.DisplayName}: " +
                  $"{(InstantTransmission.IsLearned(player) ? "learned" : "not learned")}, " +
                  $"taught in {SaiyaheimConfig.TransmissionLearnBiome.Value}. " +
                  $"Hold {SaiyaheimConfig.TransmissionMapKey.Value} and click the map, or press " +
                  $"{SaiyaheimConfig.TransmissionBlinkKey.Value} for up to " +
                  $"{SaiyaheimConfig.TransmissionBlinkMaxDistance.Value:0} m where you aim.");

            Print($"Power Level {PowerSkill.GetLevel(player):0.#}: " +
                  $"{SaiyaheimConfig.TransmissionKiCost.Value:0.#} ki + " +
                  $"{InstantTransmission.GetKiPer100m(player):0.##} ki per 100 m. " +
                  $"Range with current ki ({current:0}): " +
                  $"{InstantTransmission.FormatDistance(InstantTransmission.GetRange(player, current))}, " +
                  $"with a full bar ({max:0}): " +
                  $"{InstantTransmission.FormatDistance(InstantTransmission.GetRange(player, max))}.");
        }
    }
}
