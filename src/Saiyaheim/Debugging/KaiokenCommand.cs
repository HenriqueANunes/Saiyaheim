using System.Collections.Generic;
using Saiyaheim.Kaioken;
using Saiyaheim.Power;
using Saiyaheim.Runes;
using Saiyaheim.Transformations;

namespace Saiyaheim.Debugging
{
    /// <summary>
    /// Inspeciona e testa o Kaioken (etapa 14). Sem argumento é leitura: a skill, os tiers
    /// aprendidos e, para cada um, o que ele pede e cobra na forma atual — a mesma tabela da aba
    /// Kaioken da calculadora, lida do jogo. É por aqui que a calibragem sai do papel.
    ///
    /// O nível da skill sobe pelo comando vanilla (<c>raiseskill Kaioken 30</c>), como as outras.
    /// </summary>
    internal class KaiokenCommand : SaiyaheimCommand
    {
        public override string Name => "saiya_kaioken";

        public override string Help =>
            "Kaioken. Usage: saiya_kaioken [learn <tier|all> | forget <tier|all> | off]. " +
            "Raise the skill with 'raiseskill Kaioken <n>'.";

        public override List<string> CommandOptionList() =>
            new List<string> { "learn", "forget", "off" };

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

                    LearnOrForget(player, action == "learn", args.Length > 1 ? args[1] : "all");
                    break;

                case "off":
                    Print(KaiokenManager.StopNow(player) ? "Kaioken off." : "Kaioken was not on.");
                    break;
            }

            PrintStatus(player);
        }

        private void LearnOrForget(Player player, bool learn, string which)
        {
            IEnumerable<KaiokenTier> tiers;

            if (which == "all")
            {
                tiers = KaiokenRegistry.All;
            }
            else
            {
                KaiokenTier tier = KaiokenRegistry.Find(which);
                if (tier == null)
                {
                    Print($"No tier '{which}'. Tiers: x2, x3, x4, x10, x20, or all.");
                    return;
                }

                tiers = new[] { tier };
            }

            foreach (KaiokenTier tier in tiers)
            {
                bool changed = learn ? RuneKnowledge.Learn(player, tier) : RuneKnowledge.Forget(player, tier);

                Print($"{tier.DisplayName}: " +
                      (learn
                          ? changed ? "learned." : "already known."
                          : changed ? "forgotten." : "was not known."));
            }
        }

        private void PrintStatus(Player player)
        {
            Transformation form = TransformationRegistry.GetActive(player);
            KaiokenTier active = KaiokenRegistry.GetActive(player);

            Print($"Kaioken skill {KaiokenSkill.GetLevel(player):0.#}, in {(form == null ? "base form" : form.DisplayName)}" +
                  (form == null ? "" : $" (mastery {form.GetSkillLevel(player):0.#}, adds {form.GetKaiokenPenalty(player):0.#} levels)") +
                  $". Active: {(active == null ? "none" : active.DisplayName)}" +
                  (KaiokenManager.IsExhausted(player) ? ", EXHAUSTED" : "") + ".");

            foreach (KaiokenTier tier in KaiokenRegistry.All)
            {
                float margin = tier.GetMargin(player);
                float health = tier.GetHealthPerSecond(margin);

                Print($"  {tier.Name}: {(tier.IsLearned(player) ? "learned" : "not learned")}, " +
                      $"x{tier.GetPowerMultiplier():0.##} power, needs {tier.GetNeededLevel(player, form):0.#} " +
                      $"(margin {(margin > 0f ? "+" : "")}{margin:0.#}), " +
                      $"{tier.GetStaminaPerSecond(margin):0.##} stamina/s, " +
                      (health > 0f ? $"{health:0.##} hp/s" : "no health cost") +
                      $", taught in {tier.Config.LearnBiome.Value}.");
            }

            Print($"Combat power x{BattlePower.GetCombatMultiplier(player):0.##} " +
                  $"(form x{TransformationRegistry.GetPowerMultiplier(player):0.##}, " +
                  $"Kaioken x{KaiokenRegistry.GetPowerMultiplier(player):0.##}).");
        }
    }
}
