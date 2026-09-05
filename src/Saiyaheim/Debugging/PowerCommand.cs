using System;
using System.Collections.Generic;
using Saiyaheim.Ki;
using Saiyaheim.Power;
using Saiyaheim.Transformations;

namespace Saiyaheim.Debugging
{
    /// <summary>
    /// Inspeção e teste do battle power.
    ///
    /// Existe porque o poder é invisível: sem HUD ainda (etapa 10), a única forma de saber se o
    /// dano do soco e a armadura estão saindo dos números certos seria inferir pela sensação de
    /// jogo — que é exatamente o tipo de iteração cega que o projeto tenta evitar.
    ///
    /// <code>
    /// saiya_power              mostra os números: fórmula em uso, poder, dano e armadura
    /// saiya_power scan         poder de luta de todo bicho carregado, ordenado — a ferramenta de calibragem
    /// saiya_power skill 50     define o nível da skill Power Level (testa o topo da curva sem grind)
    /// saiya_power xp 10        joga XP na skill
    /// </code>
    ///
    /// Ler o estado é livre; <c>skill</c> e <c>xp</c> mexem no personagem e por isso pedem
    /// <c>devcommands</c>, igual ao <c>spawn</c> do jogo base. Ver <see cref="SaiyaheimCommand"/>.
    /// </summary>
    internal class PowerCommand : SaiyaheimCommand
    {
        public override string Name => "saiya_power";

        public override string Help =>
            "Inspects the battle power. Usage: saiya_power [skill <level> | xp <amount>]";

        public override List<string> CommandOptionList() => new List<string> { "scan", "skill", "xp" };

        protected override void Execute(string[] args)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Print("No player. Join a world first.");
                return;
            }

            if (!PowerSkill.IsRegistered)
            {
                Print("The 'Power Level' skill was not registered. Check the BepInEx log.");
                return;
            }

            string action = args.Length > 0 ? args[0].ToLowerInvariant() : null;

            switch (action)
            {
                case null:
                    break;

                case "scan":
                    PrintScan(player);
                    return;

                case "skill":
                    if (!RequireCheats("skill"))
                    {
                        return;
                    }

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
                    if (!RequireCheats("xp"))
                    {
                        return;
                    }

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
            Print($"Power Level: level {PowerSkill.GetLevel(player):0.#}");

            // Os dois numeros aparecem separados de proposito: e a unica forma de ver, no jogo, se
            // o termo de fim de jogo ja acordou — e a diferenca entre eles explica por que o soco
            // cresce sem o voo crescer junto.
            float linear = BattlePower.GetRaw(player);
            float combat = BattlePower.GetCombatRaw(player);
            float late = BattlePower.GetLateGameBonus(player);

            Print($"Battle power (combat): {combat:0.#}  — internal stat, feeds punch and armor");
            Print($"  linear part: {linear:0.#}  — feeds flight speed, ki cap and ki regen");
            Print($"  late-game term: +{late:0.#}  — feeds punch, armor and block only");

            // Sem esta linha as duas de cima passam a mentir enquanto o jogador esta transformado:
            // elas nao somam o combat, porque a forma multiplica o total depois.
            Transformation form = TransformationRegistry.GetActive(player);
            if (form != null)
            {
                Print($"  {form.DisplayName}: x{form.GetPowerMultiplier():0.##} over the sum above " +
                      "— flight speed included, ki cap and regen excluded");
            }
            Print($"Damage added to punch: {(kiOn ? BattlePower.GetPunchDamageBonus(player).ToString("0.#") : "0 (ki off)")}");
            Print($"Armor: {player.GetBodyArmor():0.#} {(kiOn ? "(from power, equipment ignored)" : "(from equipment)")}");
            Print($"Ki: {KiManager.State?.Current ?? 0f:0.#}/{KiManager.Max:0.#}");

            // O numero escaneavel, que NAO e o de cima: sai de HP e dano, na mesma escala dos
            // bichos. E a linha para comparar com o 'saiya_power scan' — se o seu numero e o do
            // troll nao contam a mesma historia que a luta conta, os pesos estao errados.
            float ratingRaw = PowerRating.GetRaw(player);
            Print($"Power rating (scannable): {PowerRating.ToDisplay(ratingRaw):0}  (raw {ratingRaw:0.#})");
            // A vida efetiva e o HP cru saem juntos porque a diferenca entre eles E o termo que a
            // transformacao move do lado defensivo — sem os dois, a linha esconde o que ela existe
            // para mostrar.
            Print($"  = {SaiyaheimConfig.RatingK1Health.Value:0.##} x {PowerRating.GetEffectiveHp(player):0.#} ehp" +
                  $" (hp {player.GetMaxHealth():0.#} x armor {player.GetBodyArmor():0.#})" +
                  $" + {SaiyaheimConfig.RatingK2Damage.Value:0.##} x {PowerRating.GetDps(player):0.#} dps");
        }

        /// <summary>
        /// Poder de luta de todo <c>Character</c> carregado, ordenado do mais forte para o mais
        /// fraco, com o jogador marcado no meio da lista.
        ///
        /// <b>É a ferramenta que calibra os pesos, e por isso ela é uma lista e não uma leitura
        /// avulsa.</b> A pergunta do playtest não é "qual o poder do troll?", é <i>"a ordem que
        /// esta lista imprime é a mesma ordem em que estes bichos me matam?"</i> — e essa só se
        /// responde com todo mundo lado a lado. Uma leitura por vez obrigaria a anotar num papel.
        ///
        /// Os componentes saem separados de propósito: quando a ordem sair errada, é a coluna de
        /// HP contra a de dano que diz qual dos dois pesos está mentindo.
        /// </summary>
        private void PrintScan(Player player)
        {
            var rows = new List<KeyValuePair<float, string>>();

            foreach (Character character in Character.GetAllCharacters())
            {
                if (character == null || character.IsDead())
                {
                    continue;
                }

                float raw = PowerRating.GetRaw(character);
                float distance = UnityEngine.Vector3.Distance(
                    player.transform.position, character.transform.position);

                // O nome cru do prefab, nao o localizado: 'Troll' e 'Draugr_Elite' identificam a
                // criatura sem ambiguidade, e e por ele que se procura no dump de prefabs.
                string name = character.name.Replace("(Clone)", string.Empty);
                string stars = character.GetLevel() > 1 ? $" {character.GetLevel() - 1}*" : string.Empty;
                string self = character == player ? "  <-- you" : string.Empty;

                rows.Add(new KeyValuePair<float, string>(raw,
                    $"{PowerRating.ToDisplay(raw),8:0}  {name}{stars}  " +
                    $"(ehp {PowerRating.GetEffectiveHp(character):0} | dps {PowerRating.GetDps(character):0.#}" +
                    $" | raw {raw:0.#} | {distance:0}m){self}"));
            }

            if (rows.Count == 0)
            {
                Print("No characters loaded.");
                return;
            }

            rows.Sort((a, b) => b.Key.CompareTo(a.Key));

            Print($"Power rating — {SaiyaheimConfig.RatingK1Health.Value:0.##} x ehp" +
                  $" + {SaiyaheimConfig.RatingK2Damage.Value:0.##} x dps," +
                  $" displayed x{SaiyaheimConfig.PowerDisplayScale.Value:0.##}");

            foreach (KeyValuePair<float, string> row in rows)
            {
                Print(row.Value);
            }
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
    }
}
