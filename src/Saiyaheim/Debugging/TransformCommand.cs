using System;
using System.Collections.Generic;
using Saiyaheim.Ki;
using Saiyaheim.Power;
using Saiyaheim.Transformations;
using Saiyaheim.Util;

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
    /// saiya_form                  os números da forma ativa (ou do primeiro degrau)
    /// saiya_form ssj              os números daquela forma, esteja ela ativa ou não
    /// saiya_form gate             a escada inteira: o que está destravado e o que falta
    /// saiya_form ssj unlock       ignora a trava daquela forma nesta sessão
    /// saiya_form ssj lock         devolve a trava
    /// saiya_form ssj skill 50     define o nível de maestria daquela forma
    /// saiya_form ssj xp 100       joga XP na skill de maestria daquela forma
    /// </code>
    ///
    /// <b>O nome da forma é opcional em toda linha.</b> Sem ele, o alvo é a forma ativa — e fora de
    /// forma, o primeiro degrau da escada. Com uma forma só isso é indiferente; com cinco, digitar
    /// <c>saiya_form ssj2 skill 100</c> sem ter que entrar no SSJ2 antes é a diferença entre olhar
    /// o topo da curva e ter que fazer o grind para vê-lo.
    ///
    /// <c>unlock</c> e <c>lock</c> sem forma valem para a escada inteira.
    ///
    /// Como nos outros: ler é livre, o resto pede <c>devcommands</c>.
    /// </summary>
    internal class TransformCommand : SaiyaheimCommand
    {
        public override string Name => "saiya_form";

        public override string Help =>
            "Inspects transformations. " +
            "Usage: saiya_form [<form>] [gate | unlock | lock | skill <level> | xp <amount>]";

        /// <summary>
        /// Os nomes das formas entram no autocomplete junto dos subcomandos. É a escada que muda
        /// entre versões, não a lista de ações — montar a partir do registry evita a lista aqui
        /// envelhecer quando o SSJ2 entrar.
        /// </summary>
        public override List<string> CommandOptionList()
        {
            List<string> options = new List<string> { "gate", "unlock", "lock", "skill", "xp" };

            foreach (Transformation form in TransformationRegistry.All)
            {
                options.Add(form.Id);
            }

            return options;
        }

        protected override void Execute(string[] args)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Print("No player. Join a world first.");
                return;
            }

            // O primeiro argumento PODE ser o nome de uma forma. Quando é, tudo desliza uma casa
            // para a direita — é o que faz "saiya_form skill 50" e "saiya_form ssj skill 50"
            // conviverem sem dois comandos separados.
            Transformation named = args.Length > 0 ? TransformationRegistry.Find(args[0]) : null;
            int actionAt = named == null ? 0 : 1;

            // Sem forma nomeada, o alvo é a ATIVA, para que mexer na skill enquanto transformado
            // afete o que está na tela. Fora de forma sobra o primeiro degrau da escada.
            Transformation form = named
                                  ?? TransformationRegistry.GetActive(player)
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

            string action = args.Length > actionAt ? args[actionAt].ToLowerInvariant() : null;

            // Um argumento que não é forma nem ação quase sempre é um nome de forma digitado
            // errado, e o silêncio faria o comando parecer que obedeceu.
            if (named == null && action != null && !IsKnownAction(action))
            {
                Print($"Unknown form or action: '{args[0]}'.");
                PrintKnownForms();
                return;
            }

            switch (action)
            {
                case null:
                    break;

                case "gate":
                    PrintGate(player);
                    return;

                case "unlock":
                case "lock":
                    if (!RequireCheats(action))
                    {
                        return;
                    }

                    bool open = action == "unlock";

                    // Sem forma nomeada, vale para a escada inteira: "saiya_form unlock" é o gesto
                    // de quem quer olhar tudo, e obrigar a nomear cada degrau seria imposto.
                    if (named == null)
                    {
                        foreach (Transformation step in TransformationRegistry.All)
                        {
                            step.IgnoreLocks = open;
                        }
                    }
                    else
                    {
                        named.IgnoreLocks = open;
                    }

                    PrintGate(player);
                    return;

                case "skill":
                    if (!RequireCheats("skill"))
                    {
                        return;
                    }

                    if (!TryParseAmount(args, actionAt + 1, out float level))
                    {
                        Print($"Usage: saiya_form [<form>] skill <level 0-100>");
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

                    if (!TryParseAmount(args, actionAt + 1, out float xp))
                    {
                        Print($"Usage: saiya_form [<form>] xp <amount>");
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

            PrintUnlockWarning();

            // "Showing" e "Form" são coisas diferentes desde que o nome da forma virou argumento:
            // dá para pedir os números do SSJ2 estando em SSJ, ou fora de forma nenhuma. Confundir
            // os dois faria ler o multiplicador de uma forma como se fosse o da outra.
            Print($"Showing: {form.DisplayName}   " +
                  $"Active form: {(active == null ? "none" : active.DisplayName)}   " +
                  $"(ki {(KiManager.IsEnabled ? "on" : "off")})");

            // A trava vem antes dos números: se ela está fechada, os números abaixo descrevem uma
            // forma em que o jogador não consegue entrar, e saber disso muda a leitura de tudo.
            string lockReason = form.GetLockReason(player);
            Print($"{form.DisplayName}: {(lockReason == null ? "unlocked" : "LOCKED — " + lockReason)}" +
                  "   (saiya_form gate for the whole ladder)");

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

            PrintDamageSplit(form, PowerLevel.PunchBonusFor(inForm));
            PrintPunchEconomy(outOfForm, inForm);
        }

        /// <summary>
        /// O que a forma faz com a <b>economia</b> do soco, e não só com o dano dele.
        ///
        /// Existe por causa do playtest de 2026-08-04, em que o SSJ parecia bugado — o primeiro
        /// soco saía com dano cheio e os seguintes com o dano vanilla cru. Não era bug: a forma
        /// dobrava o custo do soco sem dobrar a barra, e o segundo soco não cabia. Nenhuma tela do
        /// mod mostrava isso, e o diagnóstico só saiu lendo o <c>LogOutput.log</c>.
        ///
        /// <b>Socos por barra é a linha que importa</b>, mais do que o custo em si: é ela que diz
        /// se transformar melhora ou piora a luta. Se o valor em forma for menor que fora dela, a
        /// forma está cobrando mais do que entrega.
        /// </summary>
        private void PrintPunchEconomy(float outOfForm, float inForm)
        {
            float costOut = PunchCostFor(outOfForm);
            float costIn = PunchCostFor(inForm);
            float max = KiManager.Max;

            Print($"  punch cost {costOut:0.#} → {costIn:0.#} ki" +
                  $"{DescribeDiscount(inForm)}");

            if (costOut <= 0f || costIn <= 0f || max <= 0f)
            {
                return;
            }

            // Dano por barra e' o teste de fogo da forma: dobrar o dano do soco e' inutil se a
            // barra passar a comprar metade dos socos.
            Print($"  punches per full bar ({max:0} ki): {max / costOut:0.#} → {max / costIn:0.#}" +
                  $"   bonus damage per bar: {max / costOut * PowerLevel.PunchBonusFor(outOfForm):0} → " +
                  $"{max / costIn * PowerLevel.PunchBonusFor(inForm):0}");
        }

        /// <summary>O custo de ki de um soco a um poder de combate hipotético.</summary>
        private static float PunchCostFor(float combatPower)
        {
            return PowerLevel.PunchBonusFor(combatPower)
                   * SaiyaheimConfig.PunchKiCostPerDamage.Value
                   * PowerLevel.KiCostFactorFor(combatPower);
        }

        /// <summary>O desconto por poder no soco em forma, ou string vazia se está desligado.</summary>
        private static string DescribeDiscount(float inForm)
        {
            float factor = PowerLevel.KiCostFactorFor(inForm);

            return factor >= 1f
                ? ""
                : $"   (power discount in form: x{factor:0.###}, {(1f - factor) * 100f:0}% off)";
        }

        /// <summary>
        /// Como o soco desta forma se reparte entre tipos de dano.
        ///
        /// Impresso em cima do bônus de poder porque é o número que domina o golpe; o dano
        /// desarmado vanilla é repartido junto, na mesma proporção, e é pequeno demais para mudar
        /// a leitura. Uma linha só, e omitida quando a forma não reparte nada — sem repartição não
        /// há o que conferir, e a tela do <c>saiya_form</c> já é longa.
        /// </summary>
        private void PrintDamageSplit(Transformation form, float punchBonus)
        {
            float slash = form.GetPunchSlashFraction();
            if (slash <= 0f)
            {
                return;
            }

            Print($"  punch damage split: {(1f - slash) * 100f:0}% blunt / {slash * 100f:0}% slash " +
                  $"({punchBonus * (1f - slash):0.#} + {punchBonus * slash:0.#} of the bonus) " +
                  "— same total, spread over two types");
        }

        /// <summary>
        /// A escada inteira e as travas dela.
        ///
        /// Três coisas de uma vez, e cada uma responde uma pergunta que só o jogo em execução
        /// responde: qual forma está destravada agora, quais bosses já caíram <b>neste mundo</b>, e
        /// que global keys o mundo tem — esta última é como se descobre a chave da Rainha e a do
        /// Fader, que não existem como string na assembly e portanto não podem ser chutadas no
        /// <c>.cfg</c>.
        /// </summary>
        private void PrintGate(Player player)
        {
            PrintUnlockWarning();

            Print("Ladder:");
            foreach (Transformation form in TransformationRegistry.All)
            {
                string reason = form.GetLockReason(player);
                string key = form.Config.RequiredGlobalKey.Value;
                string gate = string.IsNullOrEmpty(key) ? "no gate" : key;

                // O "(forced)" evita a leitura mais cara possível desta tela: ver UNLOCKED e
                // concluir que o boss caiu, quando quem abriu foi o comando de debug.
                Print($"  {form.Id}: {(reason == null ? "UNLOCKED" : "locked — " + reason)}" +
                      $"  [{gate}]{(form.IgnoreLocks ? "  (forced by saiya_form unlock)" : "")}");
            }

            Print("Bosses:");
            foreach (KeyValuePair<string, string> boss in BossGate.Known)
            {
                Print($"  {(BossGate.IsOpen(boss.Key) ? "x" : " ")} {boss.Value}  ({boss.Key})");
            }

            // Cru, sem filtro: o ponto é justamente ver o que existe e o mod não conhece.
            List<string> keys = BossGate.WorldKeys();
            keys.Sort();
            Print($"World global keys ({keys.Count}): " +
                  (keys.Count == 0 ? "none" : string.Join(", ", keys.ToArray())));
        }

        /// <summary>
        /// O aviso de que as travas estão desligadas, na <b>primeira linha</b> de qualquer saída do
        /// comando.
        ///
        /// É o preço de existir um atalho de destravar: sem isso, ligar e esquecer produz um
        /// playtest que mente em silêncio — a forma entra, tudo parece certo, e a conclusão sobre a
        /// trava não vale nada. O aviso aparece justamente onde se vai olhar.
        /// </summary>
        private void PrintUnlockWarning()
        {
            List<string> forced = new List<string>();
            foreach (Transformation form in TransformationRegistry.Unlocked())
            {
                forced.Add(form.Id);
            }

            if (forced.Count == 0)
            {
                return;
            }

            Print($"*** LOCKS OFF for {string.Join(", ", forced.ToArray())} — debug only, " +
                  "this session only. 'saiya_form lock' undoes it. ***");
        }

        /// <summary>Os nomes que o comando aceita, para quando o jogador erra um.</summary>
        private void PrintKnownForms()
        {
            List<string> names = new List<string>();
            foreach (Transformation form in TransformationRegistry.All)
            {
                names.Add(form.Id);
            }

            Print($"Forms: {(names.Count == 0 ? "none" : string.Join(", ", names.ToArray()))}");
            Print(Help);
        }

        private static bool IsKnownAction(string action)
        {
            switch (action)
            {
                case "gate":
                case "unlock":
                case "lock":
                case "skill":
                case "xp":
                    return true;
                default:
                    return false;
            }
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
