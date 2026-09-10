using System;
using System.Collections.Generic;
using Saiyaheim.Ki;
using Saiyaheim.Power;
using Saiyaheim.Transformations;
using Saiyaheim.Util;
using UnityEngine;

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
    /// saiya_form hair             lista os penteados do jogo, com o nome legivel de cada um
    /// saiya_form hair Hair6       experimenta um penteado sem transformar
    /// saiya_form hair spiked      experimenta a versão espetada do penteado do personagem
    /// saiya_form hair off         devolve o penteado do personagem
    /// </code>
    ///
    /// <b>Por que o <c>hair</c> mora aqui.</b> A chave <c>HairItem</c> de cada forma pede o nome
    /// de um item de customização do jogo, e eles são <b>numerados</b>: <c>Hair1</c> a
    /// <c>Hair37</c>. Escolher o cabelo de uma forma lendo <c>.cfg</c> é escolher no escuro, e
    /// entrar no barbeiro para ver cada um é caro. Aqui a lista sai com o nome legível ao lado, e
    /// vestir um é um comando.
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
            "Usage: saiya_form [<form>] [gate | unlock | lock | skill <level> | xp <amount> | " +
            "hair [<name> | spiked | off]]";

        /// <summary>
        /// Os nomes das formas entram no autocomplete junto dos subcomandos. É a escada que muda
        /// entre versões, não a lista de ações — montar a partir do registry evita a lista aqui
        /// envelhecer quando o SSJ2 entrar.
        /// </summary>
        public override List<string> CommandOptionList()
        {
            List<string> options = new List<string> { "gate", "unlock", "lock", "skill", "xp", "hair" };

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

                case "hair":
                    HandleHair(player, args, actionAt + 1);
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

            // O multiplicador de boss e' invisivel em jogo — a barra de XP nao existe e o nivel
            // sobe devagar demais para se notar a diferenca olhando. Sem esta linha nao ha como
            // saber se a chave ligou, muito menos calibrar o passo dela.
            float bossXp = form.GetBossXpMultiplier();
            float xpRate = form.Config.MasteryXpPerSecond.Value * bossXp;

            Print($"{form.DisplayName} mastery: level {form.GetSkillLevel(player):0.#}   " +
                  $"gaining {xpRate:0.##} xp/s" +
                  $"{(bossXp > 1f ? $" (base {form.Config.MasteryXpPerSecond.Value:0.##} x{bossXp:0.##} " +
                                    $"from {BossGate.DefeatedCount()} bosses down)" : "")}");
            PrintMasteryEta(player, form, xpRate);
            Print($"Power multiplier: x{form.GetPowerMultiplier():0.##}");
            PrintCarryWeight(player, form, active);
            Print($"Ki drain: {drain:0.##}/s " +
                  $"(base {form.Config.KiDrainPerSecond.Value:0.##}, " +
                  $"mastery cuts {(1f - SafeRatio(drain, form.Config.KiDrainPerSecond.Value)) * 100f:0}%)");
            Print($"Ki: {KiManager.Current:0.#}/{KiManager.Max:0.#} " +
                  $"— {SecondsOfForm(drain):0} s in form" +
                  $"{(active == null ? " if you transformed now" : " left")}");

            // O ponto inteiro da mecanica e' o salto de poder. Imprimir os dois lados evita ter que
            // transformar, rodar saiya_power, destransformar e rodar de novo para comparar.
            float combat = BattlePower.GetCombatRaw(player);
            float multiplier = form.GetPowerMultiplier();
            float outOfForm = active == null ? combat : combat / multiplier;

            float inForm = outOfForm * multiplier;

            Print($"Combat power: {outOfForm:0.#} base → {inForm:0.#} in form");
            Print($"  armor {BattlePower.ArmorFor(outOfForm):0} → {BattlePower.ArmorFor(inForm):0}, " +
                  $"punch bonus {BattlePower.PunchBonusFor(outOfForm):0.#} → " +
                  $"{BattlePower.PunchBonusFor(inForm):0.#}");

            PrintDamageSplit(form, BattlePower.PunchBonusFor(inForm));

            // A maestria DESTA forma, e nao a da ativa: o saiya_form fala de um degrau por vez, e
            // "saiya_form ssj2" rodado em SSJ tem que responder o que o SSJ2 custaria, nao o que o
            // SSJ custa agora. Fora de forma o lado esquerdo e' devolucao zero por definicao.
            float payback = BattlePower.FormCostPayback(multiplier, form.GetSkillLevel(player));
            PrintPunchEconomy(outOfForm, inForm, payback);
        }

        /// <summary>
        /// O que a forma faz com o limite de peso, com os dois lados na mesma linha.
        ///
        /// Sem isto o número não existe em lugar nenhum: o inventário mostra o limite <b>já
        /// somado</b>, sem dizer quanto dele veio da forma, e fora de forma o bônus não aparece de
        /// jeito nenhum. A carga atual entra junto porque é ela que diz se o bônus está fazendo
        /// alguma diferença agora ou se é só um número maior na tela.
        ///
        /// Omitida quando a forma não soma peso, como a linha de repartição de dano: forma sem
        /// bônus não tem o que dizer.
        /// </summary>
        private void PrintCarryWeight(Player player, Transformation form, Transformation active)
        {
            float bonus = form.GetCarryWeightBonus();
            if (bonus <= 0f)
            {
                return;
            }

            // O limite que o jogo devolve JA inclui o bonus da forma ativa, se houver uma — e ela
            // pode nao ser a forma sendo mostrada. Descontar a ativa (e nao a mostrada) e' o que
            // faz "saiya_form ssj2" em SSJ imprimir os dois lados certos.
            float outOfForm = player.GetMaxCarryWeight() - (active == null ? 0f : active.GetCarryWeightBonus());
            Inventory inventory = player.GetInventory();

            Print($"Carry weight: {outOfForm:0} → {outOfForm + bonus:0} " +
                  $"(carrying {(inventory == null ? 0f : inventory.GetTotalWeight()):0})");
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
        private void PrintPunchEconomy(float outOfForm, float inForm, float payback)
        {
            float costOut = PunchCostFor(outOfForm, 0f);
            float costIn = PunchCostFor(inForm, payback);
            float max = KiManager.Max;

            Print($"  punch cost {costOut:0.#} → {costIn:0.#} ki" +
                  $"{DescribeDiscount(inForm, payback)}");

            if (costOut <= 0f || costIn <= 0f || max <= 0f)
            {
                return;
            }

            // Dano por barra e' o teste de fogo da forma: dobrar o dano do soco e' inutil se a
            // barra passar a comprar metade dos socos.
            Print($"  punches per full bar ({max:0} ki): {max / costOut:0.#} → {max / costIn:0.#}" +
                  $"   bonus damage per bar: {max / costOut * BattlePower.PunchBonusFor(outOfForm):0} → " +
                  $"{max / costIn * BattlePower.PunchBonusFor(inForm):0}");
        }

        /// <summary>
        /// O custo de ki de um soco a um poder de combate e um nível de maestria hipotéticos.
        /// </summary>
        private static float PunchCostFor(float combatPower, float payback)
        {
            return BattlePower.PunchBonusFor(combatPower)
                   * SaiyaheimConfig.PunchKiCostPerDamage.Value
                   * BattlePower.KiCostFactorFor(combatPower, payback);
        }

        /// <summary>
        /// O desconto no soco em forma, ou string vazia se está desligado.
        ///
        /// <b>As duas parcelas aparecem separadas</b> quando a maestria está pagando alguma: são
        /// duas chaves diferentes do <c>.cfg</c>, e sem separá-las não dá para saber qual delas
        /// mexer quando o número na tela estiver errado. É a mesma razão pela qual esta linha
        /// existe desde 2026-08-04 — o custo do soco não aparece em lugar nenhum do jogo.
        /// </summary>
        private static string DescribeDiscount(float inForm, float payback)
        {
            float factor = BattlePower.KiCostFactorFor(inForm, payback);
            if (factor >= 1f)
            {
                return "";
            }

            string parts = $"   (discount in form: x{factor:0.###}, {(1f - factor) * 100f:0}% off";

            // Sem maestria treinada a segunda parcela vale zero, e imprimir "+0" so' polui a linha
            // que ja' existia. Com ela treinada, o que interessa e' quanto de cada lado.
            if (payback > 0f)
            {
                float fromPower = SaiyaheimConfig.KiCostPowerReduction.Value * Math.Max(0f, inForm);

                parts += $" — power {fromPower:0.##} + mastery payback {payback:0.##}";
            }

            return parts + ")";
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
            form.GetPunchSplit(out float slash, out float lightning);

            if (slash <= 0f && lightning <= 0f)
            {
                return;
            }

            float blunt = 1f - slash - lightning;

            // As três partes na mesma linha, e as duas de zero omitidas: o que a linha existe para
            // conferir é que a soma continua sendo o bônus inteiro, e um "0% slash" no meio só
            // atrapalha essa leitura. As frações vêm normalizadas do GetPunchSplit, então o que
            // está impresso aqui é o que o golpe faz — não o que o .cfg pediu.
            string parts = $"{blunt * 100f:0}% blunt";
            string amounts = $"{punchBonus * blunt:0.#}";
            int types = 1;

            if (slash > 0f)
            {
                parts += $" / {slash * 100f:0}% slash";
                amounts += $" + {punchBonus * slash:0.#}";
                types++;
            }

            if (lightning > 0f)
            {
                parts += $" / {lightning * 100f:0}% lightning";
                amounts += $" + {punchBonus * lightning:0.#}";
                types++;
            }

            Print($"  punch damage split: {parts} ({amounts} of the bonus) " +
                  $"— same total, spread over {types} types");
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

        /// <summary>
        /// O <c>hair</c>: lista os penteados, veste um, ou devolve o do personagem.
        ///
        /// <b>Listar é livre; vestir é cheat.</b> Ler a lista não muda nada em jogo, e é
        /// justamente o que se quer fazer com o jogo aberto para preencher o <c>HairItem</c> de
        /// uma forma. Vestir muda a aparência do personagem na sessão, então passa pelo
        /// <c>devcommands</c> como o resto.
        ///
        /// <b>Não persiste nada.</b> O penteado experimentado vive na ZDO, igual ao das formas —
        /// cai no próximo <c>power down</c>, no <c>off</c>, ou sozinho ao fechar o jogo. O
        /// penteado de verdade do personagem não é tocado em nenhum caminho.
        /// </summary>
        private void HandleHair(Player player, string[] args, int at)
        {
            string name = args.Length > at ? args[at] : null;

            if (name == null)
            {
                PrintHairList(player);
                return;
            }

            if (!RequireCheats("hair"))
            {
                return;
            }

            if (string.Equals(name, "off", StringComparison.OrdinalIgnoreCase))
            {
                TransformationEffects.RestoreHairStyle(player);
                Print("Hair back to the character's own.");
                return;
            }

            // A mesma tradução que a forma faz com "Spiked" no HairItem, para conferir a malha
            // espetada do próprio cabelo sem precisar de ki nem de forma destravada.
            if (string.Equals(name, CustomHair.SpikedKeyword, StringComparison.OrdinalIgnoreCase))
            {
                name = TransformationEffects.ResolveHairItem(player, CustomHair.SpikedKeyword);
                if (string.IsNullOrEmpty(name))
                {
                    Print($"{player.GetHair()} has no spiked version yet.");
                    return;
                }
            }

            if (!TransformationEffects.ApplyHairStyle(player, name))
            {
                Print($"No hair item named '{name}'. Run 'saiya_form hair' for the list.");
                return;
            }

            Print($"Wearing {name} ({HairLabel(name)}). Preview only: it lasts until you power " +
                  "down, wear another one, or run 'saiya_form hair off'.");
        }

        /// <summary>
        /// Os penteados do jogo, com o nome legível ao lado, o do personagem marcado e a forma que
        /// usa cada um.
        ///
        /// A lista sai do próprio <c>ObjectDB</c> e pelo mesmo caminho que o barbeiro do jogo usa
        /// (<c>GetAllItems(Customization, "Hair")</c>), inclusive descartando os nomes com
        /// <c>_</c> — são variantes internas, não opções. Uma lista escrita à mão aqui
        /// envelheceria na primeira atualização do Valheim que acrescentasse um cabelo.
        ///
        /// Os nossos <c>SaiyaHairN</c> não entram nesse filtro — o <c>GetAllItems</c> casa pelo
        /// <b>começo</b> do nome, e é isso que os mantém fora do barbeiro. Por isso aparecem ao
        /// lado do cabelo do jogo de que nasceram, em vez de numa linha própria.
        /// </summary>
        private void PrintHairList(Player player)
        {
            if (ObjectDB.instance == null)
            {
                Print("The item database is not loaded yet.");
                return;
            }

            List<ItemDrop> hairs =
                ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Customization, "Hair");
            hairs.RemoveAll(hair => hair.name.Contains("_"));

            if (hairs.Count == 0)
            {
                Print("No hair items found.");
                return;
            }

            string worn = player.GetHair();

            Print($"Hair items ({hairs.Count}). Wear one with 'saiya_form hair <name>':");

            foreach (ItemDrop hair in hairs)
            {
                string spiked = CustomHair.GetSpikedVariant(hair.name);
                string spikedMark = spiked == null ? "" : $"  (spiked: {spiked})";
                string mark = hair.name == worn ? " <- worn" : "";
                string used = FormsUsing(hair.name, spiked, hair.name == worn);

                Print($"  {hair.name,-10} {HairLabel(hair.name)}{spikedMark}{used}{mark}");
            }
        }

        /// <summary>O nome legível de um penteado, ou o próprio nome se o item não existe.</summary>
        private static string HairLabel(string name)
        {
            GameObject prefab = ObjectDB.instance == null ? null : ObjectDB.instance.GetItemPrefab(name);
            ItemDrop item = prefab == null ? null : prefab.GetComponent<ItemDrop>();

            return item == null
                ? name
                : Localization.instance.Localize(item.m_itemData.m_shared.m_name);
        }

        /// <summary>
        /// As formas que vestem este penteado, entre colchetes, ou vazio. Existe para a lista
        /// responder "este já é o cabelo do SSJ3" sem obrigar a abrir o <c>.cfg</c> ao lado.
        ///
        /// Conta três jeitos de uma forma chegar a ele: pelo nome do jogo, pelo nome da versão
        /// espetada (<paramref name="spiked"/>), e pelo <c>Spiked</c> quando este é o penteado que
        /// o personagem usa — esse último só vale na linha do <paramref name="worn"/>, porque é o
        /// cabelo de cada personagem que decide.
        /// </summary>
        private static string FormsUsing(string name, string spiked, bool worn)
        {
            List<string> forms = new List<string>();

            foreach (Transformation form in TransformationRegistry.All)
            {
                string item = form.Config.HairItem.Value;

                if (string.Equals(item, name, StringComparison.OrdinalIgnoreCase))
                {
                    forms.Add(form.DisplayName);
                }
                else if (spiked != null &&
                         (string.Equals(item, spiked, StringComparison.OrdinalIgnoreCase) ||
                          (worn && string.Equals(item, CustomHair.SpikedKeyword, StringComparison.OrdinalIgnoreCase))))
                {
                    forms.Add(form.DisplayName + " spiked");
                }
            }

            return forms.Count == 0 ? "" : "  [" + string.Join(", ", forms.ToArray()) + "]";
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
                case "hair":
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

        /// <summary>
        /// Quanto tempo de forma ainda falta para a maestria bater 100 — e para o próximo nível.
        ///
        /// <b>Existe porque "0,5 xp/s" não responde a pergunta que se está fazendo ao ler a linha
        /// de cima.</b> A curva de XP do Valheim custa <c>(nível + 1)^1,5 / 2 + 0,5</c> por degrau,
        /// então o topo vale umas trinta vezes o primeiro nível: a mesma taxa que parece rápida no
        /// começo pode significar vinte horas de forma no fim, e não havia como saber disso sem
        /// esperar o playtest chegar lá. É o mesmo motivo do <c>saiya_form skill 100</c> — ver o
        /// fim da curva sem ter que fazer o grind dela.
        ///
        /// <b>A conta imita o pagamento real, e não a integral da curva.</b> O XP é creditado uma
        /// vez por segundo (<c>SE_Transformation.FlushXp</c>) e o <c>Skill.Raise</c> do jogo
        /// <b>zera o acumulador ao subir de nível</b>, jogando fora o excesso da chamada que subiu.
        /// Daí o arredondamento para cima em cada degrau: com XP por segundo alto, esse desperdício
        /// é justamente a diferença entre a estimativa e o que o jogador vive.
        ///
        /// Assume a taxa de agora e a forma segurada sem parar, e as duas coisas mentem um pouco: o
        /// multiplicador de boss sobe quando o mundo anda, e ki nenhum segura uma forma por horas
        /// seguidas. É número de calibragem, não previsão.
        /// </summary>
        private void PrintMasteryEta(Player player, Transformation form, float xpPerSecond)
        {
            Skills.Skill skill = FindSkill(player, form);
            float level = skill == null ? 0f : skill.m_level;

            if (level >= 100f)
            {
                Print("  mastery maxed — the drain above is as cheap as this form gets");
                return;
            }

            // Os dois multiplicadores que o Skill.Raise aplica por fora do que o mod paga: o
            // increaseStep da skill e o skillGainRate do mundo. O segundo é global key — um servidor
            // pode ter mexido nele, e a estimativa feita só com o número do .cfg erraria por um
            // fator inteiro sem nada na tela denunciando.
            float step = skill == null || skill.m_info == null ? 1f : skill.m_info.m_increseStep;
            float gain = xpPerSecond * step * Game.m_skillGainRate;

            if (gain <= 0f)
            {
                Print("  mastery gains nothing at this rate — level 100 is unreachable");
                return;
            }

            float accumulator = skill == null ? 0f : skill.m_accumulator;

            Print($"  next level in {DescribeDuration(SecondsToLevel(level, level + 1f, accumulator, gain))}, " +
                  $"level 100 in {DescribeDuration(SecondsToLevel(level, 100f, accumulator, gain))} " +
                  "holding this form (or any above it), at the rate above");
        }

        /// <summary>
        /// Segundos de forma para ir do nível <paramref name="from"/> ao <paramref name="to"/>, no
        /// ritmo de um pagamento por segundo — com o excesso perdido em cada subida de nível.
        /// </summary>
        private static float SecondsToLevel(float from, float to, float accumulator, float gainPerSecond)
        {
            float start = (float)Math.Floor(from);
            float seconds = 0f;

            for (float level = start; level < to; level += 1f)
            {
                float required = (float)Math.Pow(Math.Floor(level + 1f), 1.5) * 0.5f + 0.5f;

                // O acumulador só desconta do degrau em que o jogador está: os seguintes começam do
                // zero, porque o Raise zera o acumulador ao subir.
                float missing = required - (level == start ? accumulator : 0f);

                seconds += (float)Math.Ceiling(Math.Max(0f, missing) / gainPerSecond);
            }

            return seconds;
        }

        /// <summary>
        /// Duração em unidade legível. "15130 s" é exatamente o número que esta linha existe para
        /// traduzir — em segundos ninguém lê horas.
        /// </summary>
        private static string DescribeDuration(float seconds)
        {
            if (seconds < 60f)
            {
                return $"{seconds:0} s";
            }

            if (seconds < 3600f)
            {
                return $"{seconds / 60f:0} min";
            }

            return $"{Math.Floor(seconds / 3600f):0} h {seconds % 3600f / 60f:0} min";
        }

        /// <summary>
        /// A entrada de skill desta forma, ou null se ela nunca subiu — uma skill nunca usada não
        /// aparece em <c>GetSkillList()</c>.
        ///
        /// O nível cru daqui não é o mesmo do <c>Player.GetSkillLevel</c>: aquele passa pelo
        /// <c>SEMan.ModifySkillLevel</c> e arredonda para baixo. Para estimar tempo é o cru que
        /// serve, e o acumulador só faz sentido junto do nível a que ele pertence.
        /// </summary>
        private static Skills.Skill FindSkill(Player player, Transformation form)
        {
            Skills skills = player == null ? null : player.GetSkills();
            if (skills == null || form == null || !form.IsRegistered)
            {
                return null;
            }

            foreach (Skills.Skill skill in skills.GetSkillList())
            {
                if (skill.m_info != null && skill.m_info.m_skill == form.SkillType)
                {
                    return skill;
                }
            }

            return null;
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
            player.RaiseSkill(form.SkillType, 0.0001f);

            Skills.Skill skill = FindSkill(player, form);
            if (skill == null)
            {
                return false;
            }

            skill.m_level = level;
            skill.m_accumulator = 0f;
            return true;
        }
    }
}
