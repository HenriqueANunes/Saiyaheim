using System.Collections.Generic;
using Saiyaheim.Attacks;
using Saiyaheim.Ki;
using Saiyaheim.Power;

namespace Saiyaheim.Debugging
{
    /// <summary>
    /// Inspeção e teste dos ataques de ki.
    ///
    /// Existe pelo mesmo motivo do <see cref="TransformCommand"/>: os números do ataque — quanto
    /// ele bate, quantos tiros cabem na barra, quanto dano por ki ele entrega comparado ao soco —
    /// não aparecem em lugar nenhum da tela, e a pergunta central do playtest (<i>vale mais atirar
    /// ou socar?</i>) é uma divisão que ninguém faz de cabeça no meio de uma luta.
    ///
    /// <code>
    /// saiya_blast                 os números do ataque selecionado
    /// saiya_blast blast           os números daquele ataque, selecionado ou não
    /// saiya_blast blast select    seleciona aquele ataque
    /// saiya_blast blast unlock    ignora a trava daquele ataque nesta sessão
    /// saiya_blast blast lock      devolve a trava
    /// saiya_blast pose            segura a pose de disparo, para calibrar
    /// </code>
    ///
    /// <b>O nome do ataque é opcional em toda linha</b>, como no <c>saiya_form</c>: sem ele o alvo é
    /// o selecionado. <c>unlock</c> e <c>lock</c> sem nome valem para a escada inteira.
    ///
    /// Ler é livre; destravar pede <c>devcommands</c>. O <c>pose</c> é a exceção que não pede nada:
    /// ele não toca em ataque nenhum, só segura um desenho na tela — e sem ele a pose de disparo é
    /// <b>impossível</b> de calibrar, porque ela dura menos que o tempo de arrastar um slider no
    /// ConfigurationManager e olhar o personagem. Mesmo papel do <c>saiya_ki pose</c>.
    /// </summary>
    internal class AttackCommand : SaiyaheimCommand
    {
        public override string Name => "saiya_blast";

        public override string Help =>
            "Inspects ki attacks. Usage: saiya_blast [<attack>] [select | unlock | lock] | pose";

        public override List<string> CommandOptionList()
        {
            List<string> options = new List<string> { "select", "unlock", "lock", "pose" };

            foreach (KiAttack attack in KiAttackRegistry.All)
            {
                options.Add(attack.Id);
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

            // O 'pose' sai antes de tudo: ele não fala de ataque nenhum, então não passa pelo
            // desdobramento abaixo nem precisa de um ataque registrado para funcionar.
            if (args.Length > 0 && args[0].ToLowerInvariant() == "pose")
            {
                KiBlastPose.DebugHold = !KiBlastPose.DebugHold;
                Print($"Blast pose held: {(KiBlastPose.DebugHold ? "on" : "off")}" +
                      $"{(SaiyaheimConfig.BlastPoseEnabled.Value ? "" : " (but BlastPose.Enabled is off)")}");
                return;
            }

            // Mesmo desdobramento do saiya_form: o primeiro argumento PODE ser o nome do ataque, e
            // quando é, a ação desliza uma casa para a direita.
            KiAttack named = args.Length > 0 ? KiAttackRegistry.Find(args[0]) : null;
            int actionAt = named == null ? 0 : 1;

            KiAttack attack = named
                              ?? KiAttackRegistry.Current(player)
                              ?? FirstOrNull();

            if (attack == null)
            {
                Print("No ki attacks are registered.");
                return;
            }

            string action = args.Length > actionAt ? args[actionAt].ToLowerInvariant() : null;

            if (named == null && action != null && !IsKnownAction(action))
            {
                Print($"Unknown attack or action: '{args[0]}'.");
                PrintKnownAttacks();
                return;
            }

            switch (action)
            {
                case null:
                    break;

                case "select":
                    // Sem passar pelo SelectNext: aqui o alvo é explícito, e recusar por trava
                    // fechada tiraria justamente a utilidade de olhar um ataque travado de perto.
                    Print(attack.IsUnlocked(player)
                        ? $"Selected: {attack.DisplayName}."
                        : $"Selected: {attack.DisplayName} — but it is LOCKED, so firing will refuse it.");
                    SelectByCycling(player, attack);
                    break;

                case "unlock":
                case "lock":
                    if (!RequireCheats(action))
                    {
                        return;
                    }

                    bool open = action == "unlock";

                    if (named == null)
                    {
                        foreach (KiAttack step in KiAttackRegistry.All)
                        {
                            step.IgnoreLocks = open;
                        }
                    }
                    else
                    {
                        named.IgnoreLocks = open;
                    }
                    break;

                default:
                    Print($"Unknown action: '{action}'. {Help}");
                    return;
            }

            PrintUnlockWarning();
            PrintAttack(player, attack);
        }

        private void PrintAttack(Player player, KiAttack attack)
        {
            KiAttack selected = KiAttackRegistry.Current(player);

            Print($"Showing: {attack.DisplayName}   " +
                  $"Selected: {(selected == null ? "none" : selected.DisplayName)}   " +
                  $"(ki {(KiManager.IsEnabled ? "on" : "off")})");

            // A trava vem antes dos números, como no saiya_form: se está fechada, tudo abaixo
            // descreve um ataque que o jogador não consegue usar.
            string lockReason = attack.GetLockReason(player);
            string key = attack.Config.RequiredGlobalKey.Value;
            Print($"{attack.DisplayName}: " +
                  $"{(lockReason == null ? "unlocked" : "LOCKED — " + lockReason)}   " +
                  $"[{(string.IsNullOrEmpty(key) ? "no gate" : key)}]" +
                  $"{(attack.IgnoreLocks ? "  (forced by saiya_blast unlock)" : "")}");

            float combat = BattlePower.GetCombatRaw(player);
            float damage = attack.DamageFor(combat);
            int beam = attack.GetBeamCount();
            float total = damage * beam;
            float cost = attack.GetKiCost();

            Print($"Damage: {damage:0.#} slash " +
                  $"(base {attack.Config.DamageBase.Value:0.#} + " +
                  $"{attack.Config.DamageFromPower.Value:0.###} x {combat:0.#} combat power)" +
                  $"{(beam > 1 ? " per projectile" : "")}");

            // O total sai numa linha propria e so' quando ha' feixe. Sem isso o numero acima
            // mente por um fator de doze — e' o dano de um projetil de um feixe, e nao o que o
            // jogador ve acontecer ao apertar a tecla, que e' o numero que se calibra.
            if (beam > 1)
            {
                Print($"  beam of {beam} = {total:0.#} slash total, if all of it connects" +
                      $"{(attack.IsCharged ? " (at a full charge)" : "")}");
            }

            PrintEconomy(player, attack, total, cost);
            PrintCharge(player, attack, damage);
            PrintCadence(attack, cost);
            PrintProjectile(attack);
        }

        /// <summary>
        /// A pergunta central do playtest, em duas linhas: <b>quantos tiros cabem na barra</b> e
        /// <b>vale mais atirar ou socar</b>.
        ///
        /// Dano por ki é a comparação honesta entre os dois, e não o dano bruto: o soco bate menos
        /// por golpe mas custa proporcionalmente ao que soma, enquanto o tiro tem preço fixo. Se o
        /// tiro ganhar em dano por ki <i>e</i> for à distância, não há razão para encostar em nada.
        /// </summary>
        private void PrintEconomy(Player player, KiAttack attack, float damage, float cost)
        {
            float max = KiManager.Max;

            if (cost <= 0f)
            {
                Print($"Ki cost: {cost:0.#} — free. Nothing limits the rate of fire but the cooldown.");
                return;
            }

            int beam = attack.GetBeamCount();
            string breakdown = beam > 1
                ? $" ({attack.GetKiCostPerProjectile():0.#} x {beam} projectiles)"
                : " fixed";

            if (attack.IsCharged)
            {
                Print($"Ki cost: {cost:0.#} for a full charge{breakdown}, spent AS IT GROWS   " +
                      $"full charges per bar ({max:0} ki): {max / cost:0.#}   " +
                      $"current bar ({KiManager.Current:0} ki): {KiManager.Current / cost:0.#}");
            }
            else
            {
                Print($"Ki cost: {cost:0.#}{breakdown}   " +
                      $"shots per full bar ({max:0} ki): {max / cost:0.#}   " +
                      $"current bar ({KiManager.Current:0} ki): {KiManager.Current / cost:0.#}");
            }

            float punchBonus = BattlePower.GetPunchDamageBonus(player);
            float punchCost = BattlePower.GetPunchKiCost(player, punchBonus);

            if (punchCost <= 0f)
            {
                return;
            }

            Print($"  per ki: blast {damage / cost:0.##} dmg/ki   " +
                  $"punch {punchBonus / punchCost:0.##} dmg/ki (bonus only, {punchBonus:0.#} for " +
                  $"{punchCost:0.##} ki)");
        }

        /// <summary>
        /// O carregamento nas duas pontas: o que sai da carga mínima e o que sai da cheia.
        ///
        /// As duas juntas, e não só a cheia, porque a pergunta do playtest é <b>se a carga curta
        /// vale a pena</b> — se ela não valer, o ataque tem um só modo e o carregamento vira um
        /// atraso obrigatório em vez de uma escolha. É a mesma pergunta que a linha de dano por ki
        /// faz entre o tiro e o soco, uma escala abaixo.
        /// </summary>
        private void PrintCharge(Player player, KiAttack attack, float perProjectile)
        {
            if (!attack.IsCharged)
            {
                Print("Charge: none — this attack fires on the key press.");
                return;
            }

            float minRatio = attack.GetMinChargeRatio();
            int minBeam = attack.GetBeamCount(minRatio);
            int fullBeam = attack.GetBeamCount();

            float perSecond = attack.GetChargeKiPerSecond();

            Print($"Charge: {attack.GetChargeTime():0.##} s to full, " +
                  $"fires from {minRatio:0.##} of it ({minRatio * attack.GetChargeTime():0.##} s)");

            // O ki/s e' o numero que importa desde 2026-09-07, quando o gasto passou para DENTRO do
            // carregamento: e' a velocidade com que a barra desce enquanto se segura, e nao esta'
            // em chave nenhuma — sai de KiCost x BeamCount / ChargeTime.
            Print($"  {perSecond:0.#} ki/s while the charge GROWS — free once full, " +
                  "nothing on release   " +
                  $"bar lasts {(perSecond > 0f ? KiManager.Current / perSecond : 0f):0.##} s " +
                  $"of charging ({KiManager.Current:0} ki now)");

            Print($"  shortest: {minBeam} projectiles, {perProjectile * minBeam:0.#} slash, " +
                  $"{attack.GetKiCost(minBeam):0.#} ki" +
                  $"   full: {fullBeam} projectiles, {perProjectile * fullBeam:0.#} slash, " +
                  $"{attack.GetKiCost(fullBeam):0.#} ki");

            // O que a carga NAO faz e' tao importante quanto o que ela faz: o dano por projetil e'
            // o mesmo nas duas pontas, de proposito, e sem dizer isso a linha acima parece mostrar
            // um ataque que fica mais forte por acerto — que e' exatamente a curva quadratica que
            // a decisao de 2026-09-07 recusou.
            Print($"  each projectile hits for {perProjectile:0.#} at any charge; " +
                  $"the charge buys length and thickness only " +
                  $"({attack.Config.ChargeMinScale.Value:0.##}x to 1x thickness)");

            if (KiBeamCharge.Current == attack)
            {
                Print($"  *** charging now: {KiBeamCharge.Ratio:0.00} " +
                      $"= {attack.GetBeamCount(KiBeamCharge.Ratio)} projectiles ***");
            }
        }

        /// <summary>
        /// Cadência e o que ela custa por segundo se o jogador segurar o gatilho. O ki por segundo
        /// sustentado é o número que diz se o ataque esvazia a barra em um feixe.
        /// </summary>
        private void PrintCadence(KiAttack attack, float cost)
        {
            float floor = SaiyaheimConfig.KiAttackMinimumInterval.Value;
            float interval = attack.Config.Cooldown.Value > floor ? attack.Config.Cooldown.Value : floor;

            if (interval <= 0f)
            {
                Print("Cooldown: none, and no shared floor either — rate of fire is the frame rate.");
                return;
            }

            Print($"Cooldown: {attack.Config.Cooldown.Value:0.##} s " +
                  $"(shared floor {floor:0.##} s) → {1f / interval:0.#} shots/s, " +
                  $"{cost / interval:0.#} ki/s sustained" +
                  $"{DescribeRemaining(attack)}");
        }

        private void PrintProjectile(KiAttack attack)
        {
            float speed = attack.Config.ProjectileSpeed.Value;
            float life = attack.Config.ProjectileLifetime.Value;

            string prefabName = attack.Config.ProjectilePrefab.Value;
            bool exists = ZNetScene.instance != null && ZNetScene.instance.GetPrefab(prefabName) != null;

            Print($"Projectile: {prefabName}{(exists ? "" : "  *** DOES NOT EXIST — nothing will fire ***")}");
            Print($"  {speed:0.#} m/s for {life:0.##} s = {speed * life:0} m range, " +
                  $"gravity {attack.Config.ProjectileGravity.Value:0.##}, " +
                  $"knockback {attack.Config.Knockback.Value:0}");

            int beam = attack.GetBeamCount();
            if (beam <= 1)
            {
                return;
            }

            // O espacamento e' o numero que decide se sai feixe ou fila de bolinhas, e e' o unico
            // dos tres que nao esta' em nenhuma chave: ele e' velocidade x intervalo. Imprimi-lo
            // aqui e' o que permite calibrar o feixe sem fazer a conta de cabeca entre um playtest
            // e o seguinte.
            float interval = attack.GetBeamInterval();
            Print($"  Beam: {beam} projectiles every {interval:0.###} s = " +
                  $"{attack.GetBeamDuration():0.##} s of beam, " +
                  $"{speed * interval:0.#} m apart in the air" +
                  $"{(attack.IsCharged ? "  (at a full charge)" : "")}");
        }

        private static string DescribeRemaining(KiAttack attack)
        {
            float remaining = attack.GetRemainingCooldown();

            return remaining <= 0f ? "   [ready]" : $"   [{remaining:0.##} s left]";
        }

        /// <summary>
        /// Seleciona um ataque específico usando só o que o registry expõe.
        ///
        /// O registry não tem um <c>Select(ataque)</c> de propósito: em jogo a seleção só muda pela
        /// tecla de ciclar, e uma segunda porta de entrada seria estado do jogador podendo ser
        /// escrito de dois lugares. Aqui a volta custa no máximo uma passada pela escada.
        /// </summary>
        private static void SelectByCycling(Player player, KiAttack target)
        {
            for (int i = 0; i < KiAttackRegistry.All.Length; i++)
            {
                if (KiAttackRegistry.Current(player) == target)
                {
                    return;
                }

                if (KiAttackRegistry.SelectNext(player) == null)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// O aviso de travas desligadas, na primeira linha. Mesmo motivo do <c>saiya_form</c>:
        /// esquecer o <c>unlock</c> ligado produz um playtest que mente em silêncio.
        /// </summary>
        private void PrintUnlockWarning()
        {
            List<string> forced = new List<string>();
            foreach (KiAttack attack in KiAttackRegistry.Forced())
            {
                forced.Add(attack.Id);
            }

            if (forced.Count == 0)
            {
                return;
            }

            Print($"*** LOCKS OFF for {string.Join(", ", forced.ToArray())} — debug only, " +
                  "this session only. 'saiya_blast lock' undoes it. ***");
        }

        private void PrintKnownAttacks()
        {
            List<string> names = new List<string>();
            foreach (KiAttack attack in KiAttackRegistry.All)
            {
                names.Add(attack.Id);
            }

            Print($"Attacks: {(names.Count == 0 ? "none" : string.Join(", ", names.ToArray()))}");
            Print(Help);
        }

        private static KiAttack FirstOrNull()
        {
            return KiAttackRegistry.All.Length == 0 ? null : KiAttackRegistry.All[0];
        }

        private static bool IsKnownAction(string action)
        {
            switch (action)
            {
                case "select":
                case "unlock":
                case "lock":
                case "pose":
                    return true;
                default:
                    return false;
            }
        }
    }
}
