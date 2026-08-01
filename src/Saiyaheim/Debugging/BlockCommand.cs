using System.Collections.Generic;
using System.Text;
using Saiyaheim.Ki;
using Saiyaheim.Power;
using UnityEngine;

namespace Saiyaheim.Debugging
{
    /// <summary>
    /// Despeja os números do bloqueio e do stagger, e simula um golpe contra eles.
    ///
    /// <code>
    /// saiya_block           números do bloqueador atual, do stagger e da armadura de ki
    /// saiya_block 80        idem, mais a simulação de um golpe de 80 de contusão
    /// </code>
    ///
    /// <b>Por que este comando existe.</b> A fórmula do bloqueio está toda na assembly e já foi
    /// lida, mas os valores que ela consome — <c>m_blockPower</c>, <c>m_timedBlockBonus</c>,
    /// <c>m_staggerDamageFactor</c> — são dados de <b>prefab</b>, não de código. Só existem com o
    /// jogo aberto. Sem eles não dá para saber se o bloqueio desarmado está fraco de um jeito que
    /// um multiplicador resolve ou fraco de um jeito que exige reescrever a mecânica.
    ///
    /// <b>Leitura pura, sem guard de cheat</b>: nada aqui escreve no personagem. Ver
    /// <see cref="SaiyaheimCommand"/>.
    ///
    /// Este comando é <b>descartável</b>. Ele responde a perguntas de calibração; respondidas,
    /// pode sair — como saíram o <c>saiya_dumpemotes</c> e o <c>saiya_dumpprefabs</c>.
    ///
    /// Já saiu um pedaço dele: o subcomando <c>shields</c>, que despejava o block power de todos os
    /// escudos do <c>ObjectDB</c> para servir de alvo de calibração. Uso único — a tabela que ele
    /// gerou está congelada em <c>Técnico/Prefabs do Jogo.md</c> e não muda até o Valheim
    /// atualizar. Removido em 2026-08-01, no mesmo dia em que nasceu.
    /// </summary>
    internal class BlockCommand : SaiyaheimCommand
    {
        public override string Name => "saiya_block";

        public override string Help =>
            "Dumps block/parry/stagger numbers. Usage: saiya_block [damage to simulate]";

        protected override void Execute(string[] args)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Print("No player. Join a world first.");
                return;
            }

            // Cópia do Humanoid.GetCurrentBlocker(), que é privado: item da mão esquerda ou, na
            // falta dele, a arma atual — que sem nada equipado é o m_unarmedWeapon (PlayerUnarmed).
            // Reimplementado em vez de refletido porque as duas peças são públicas e a regra tem
            // duas linhas; não vale um delegate no GameAccess.
            ItemDrop.ItemData blocker = player.LeftItem ?? player.GetCurrentWeapon();
            if (blocker == null)
            {
                Print("No blocker. (Not even the unarmed weapon — something is off.)");
                return;
            }

            PrintBlocker(player, blocker);
            PrintStagger(player);

            if (args.Length > 0)
            {
                if (!float.TryParse(args[0], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float damage) ||
                    damage <= 0f)
                {
                    Print($"Usage: {Name} [damage to simulate]");
                    return;
                }

                Simulate(player, blocker, damage);
            }
            else
            {
                Print($"Run '{Name} <damage>' to simulate a hit of that size.");
            }
        }

        private void PrintBlocker(Player player, ItemDrop.ItemData blocker)
        {
            float skillFactor = player.GetSkillFactor(Skills.SkillType.Blocking);
            float baseBlockPower = blocker.GetBaseBlockPower();
            float blockPower = blocker.GetBlockPower(skillFactor);
            ItemDrop.ItemData.SharedData shared = blocker.m_shared;

            Print($"--- Blocker: {shared.m_name} (prefab {blocker.m_dropPrefab?.name ?? "?"}, " +
                  $"quality {blocker.m_quality}) ---");

            // Os dois termos separados porque a skill nativa é o único crescimento que o bloqueio
            // vanilla tem, e ela satura em +50%. Ver a fórmula em ItemDrop.ItemData.GetBlockPower.
            Print($"Item block power: {blockPower:0.##}  " +
                  $"= base {baseBlockPower:0.##} × (1 + {skillFactor:0.##} × 0.5), " +
                  $"Blocking skill {player.GetSkillLevel(Skills.SkillType.Blocking):0}");
            Print($"  m_blockPower {shared.m_blockPower:0.##}, " +
                  $"m_blockPowerPerLevel {shared.m_blockPowerPerLevel:0.##}");

            // A linha que vale com o ki ligado. O GetBlockPower do item, chamado aqui, devolve o
            // valor vanilla: o BlockPowerPatch só troca o resultado dentro do BlockAttack.
            Print(KiManager.IsEnabled
                ? $"→ IN USE: {EffectiveBlockPower(player, blocker):0.##} (ki, replaces the item)"
                : $"→ IN USE: {blockPower:0.##} (ki is off, the item's own value)");

            // A pergunta que decide se o ModifyTimedBlockBonus serve de alavanca: o Humanoid só
            // considera parry se m_timedBlockBonus > 1.
            Print(shared.m_timedBlockBonus > 1f
                ? $"Parry: YES, {shared.m_timedBlockBonus:0.##}x within 0.25 s of raising guard"
                : $"Parry: NONE (m_timedBlockBonus {shared.m_timedBlockBonus:0.##} <= 1)");

            float equipmentModifier = player.GetEquipmentBlockStaminaModifier();
            Print($"Stamina per block: {player.m_blockStaminaDrain:0.##} base " +
                  $"× used fraction, equipment modifier {equipmentModifier:+0.##;-0.##;0}");
            Print($"  perfect block drain {player.m_perfectBlockStaminaDrain:0.##}, " +
                  $"item regen {shared.m_perfectBlockStaminaRegen:0.##}");
            Print($"Deflection force: {blocker.GetDeflectionForce():0.##}, " +
                  $"durability used: {shared.m_useDurability}");
            Print($"Item damage modifiers: {DescribeModifiers(shared.m_damageModifiers)}");
        }

        private void PrintStagger(Player player)
        {
            Print("--- Stagger ---");

            // O fator zerado desliga o AddStaggerDamage inteiro (early return), então o bloqueio
            // nunca falharia por stagger. Vale a pena ver na tela antes de assumir o contrário.
            if (player.m_staggerDamageFactor <= 0f)
            {
                Print("m_staggerDamageFactor is 0 — the player never staggers from damage. " +
                      "Blocks can only fail from lack of stamina.");
                return;
            }

            float threshold = player.GetMaxHealth() * player.m_staggerDamageFactor;
            float current = player.GetStaggerPercentage() * threshold;

            Print($"Threshold: {threshold:0.##}  " +
                  $"= max health {player.GetMaxHealth():0.##} × factor {player.m_staggerDamageFactor:0.###}");
            Print($"Current: {current:0.##} ({player.GetStaggerPercentage() * 100f:0}%), " +
                  $"decays {threshold / 5f:0.##}/s (full bar in 5 s)");
            Print("Counts toward stagger: blunt + slash + pierce + lightning only.");
        }

        /// <summary>
        /// Roda o mesmo golpe pelos dois caminhos — bloqueando e não bloqueando — <b>até o fim do
        /// pipeline</b>, e imprime os dois lado a lado.
        ///
        /// ⚠️ <b>Até o fim do pipeline é o detalhe que decide se a comparação vale.</b> O bloqueio
        /// não é um destino, é uma etapa: o que ele deixa passar <b>ainda atravessa a resistência e
        /// a armadura de ki</b>, que entram depois no <c>RPC_Damage</c>. Comparar o dano
        /// pós-bloqueio com o dano pós-armadura mede estágios diferentes e faz o bloqueio parecer
        /// muito pior do que é no dano — o que importa aqui é o número final dos dois lados.
        ///
        /// <b>O que a comparação revela de verdade é o stagger.</b> O <c>BlockAttack</c> roda
        /// <b>antes</b> da armadura, então o stagger de quem bloqueia é calculado sobre o golpe
        /// reduzido só pelo block power do item, enquanto o de quem não bloqueia é calculado depois
        /// da armadura inteira. Com a armadura de ki muito maior que o block power do punho,
        /// bloquear leva ao stagger <b>mais rápido</b> do que não bloquear.
        ///
        /// A conta é reproduzida com as funções públicas do próprio jogo (<c>ApplyResistance</c>,
        /// <c>ApplyArmor</c>), pelo mesmo motivo do <c>SE_KiBody.EstimateArmorAbsorption</c>: uma
        /// fórmula reescrita à mão sai de sincronia na primeira atualização.
        ///
        /// Simula contusão pura porque é o dano do soco e porque é um dos tipos que contam para o
        /// stagger. Um golpe de fogo daria outro resultado — não bloqueia diferente, mas não
        /// staggera.
        /// </summary>
        private void Simulate(Player player, ItemDrop.ItemData blocker, float damage)
        {
            float blockPower = EffectiveBlockPower(player, blocker);
            float kiArmor = PowerLevel.GetArmor(player);
            float threshold = player.m_staggerDamageFactor > 0f
                ? player.GetMaxHealth() * player.m_staggerDamageFactor
                : 0f;

            Print($"--- Simulating a {damage:0.##} blunt hit from the front ---");
            Print($"Ki armor {kiArmor:0.##}, block power {blockPower:0.##} " +
                  $"(ratio {blockPower / Mathf.Max(kiArmor, 0.01f) * 100f:0}%)");

            SimulateBlock(player, blocker, damage, blockPower, kiArmor, threshold, parry: false);

            if (blocker.m_shared.m_timedBlockBonus > 1f)
            {
                SimulateBlock(player, blocker, damage,
                    blockPower * blocker.m_shared.m_timedBlockBonus, kiArmor, threshold, parry: true);
            }

            // O outro lado da comparação: nem toca no BlockAttack, cai direto na resistência e na
            // armadura de ki dentro do RPC_Damage. Um único AddStaggerDamage, no ApplyDamage.
            HitData unblocked = MakeHit(damage);
            unblocked.ApplyResistance(player.GetDamageModifiers(), out _);
            unblocked.ApplyArmor(kiArmor);

            float stagger = unblocked.m_damage.GetTotalStaggerDamage();
            player.GetSEMan().ModifyStagger(stagger, ref stagger);

            Print("NOT blocking:");
            Print($"  final: {unblocked.GetTotalDamage():0.##} damage " +
                  $"({(damage - unblocked.GetTotalDamage()) / damage * 100f:0}% absorbed), " +
                  $"stagger +{stagger:0.##} {PercentOfThreshold(stagger, threshold)}");
        }

        /// <summary>
        /// Um golpe pelo caminho do bloqueio, do <c>BlockAttack</c> até a vida.
        ///
        /// Duas somas de stagger, e é de propósito: o <c>BlockAttack</c> chama
        /// <c>AddStaggerDamage</c> com o residual do block power, e o <c>ApplyDamage</c> chama de
        /// novo com o que sobrou depois da armadura. Um golpe bloqueado enche a barra pelos dois
        /// lados.
        /// </summary>
        private void SimulateBlock(Player player, ItemDrop.ItemData blocker, float damage,
            float blockPower, float kiArmor, float threshold, bool parry)
        {
            string label = parry ? "PARRY" : "Blocking";

            // Passo a passo do Humanoid.BlockAttack, na mesma ordem: modificadores do item,
            // ApplyArmor(blockPower) numa cópia, e a diferença é o que foi barrado.
            HitData hit = MakeHit(damage);
            if (blocker.m_shared.m_damageModifiers.Count > 0)
            {
                HitData.DamageModifiers modifiers = default(HitData.DamageModifiers);
                modifiers.Apply(blocker.m_shared.m_damageModifiers);
                hit.ApplyResistance(modifiers, out _);
            }

            HitData.DamageTypes afterBlock = hit.m_damage.Clone();
            afterBlock.ApplyArmor(blockPower);

            float blockable = hit.GetTotalBlockableDamage();
            float blocked = blockable - afterBlock.GetTotalBlockableDamage();

            // A fração do block power que o golpe consumiu: é ela que escala o custo de stamina, e
            // ela satura em 1 — golpe grande cobra a stamina cheia mesmo barrando quase nada.
            float usedFraction = Mathf.Clamp01(blocked / blockPower);
            float stamina = parry ? player.m_perfectBlockStaminaDrain
                                  : player.m_blockStaminaDrain * usedFraction;
            stamina += stamina * player.GetEquipmentBlockStaminaModifier();
            player.GetSEMan().ModifyBlockStaminaUsage(stamina, ref stamina, minZero: false);

            float blockStagger = afterBlock.GetTotalStaggerDamage();
            player.GetSEMan().ModifyStagger(blockStagger, ref blockStagger);

            bool wouldStagger = threshold > 0f &&
                                player.GetStaggerPercentage() * threshold + blockStagger >= threshold;
            bool haveStamina = player.HaveStamina(stamina);
            bool holds = haveStamina && !wouldStagger;

            // A redução só é aplicada ao HitData de verdade se as duas condições passarem. Falhando
            // qualquer uma, o golpe segue inteiro para a armadura — e o stagger acima já foi somado.
            if (holds)
            {
                hit.BlockDamage(blocked);
            }

            // Daqui em diante é o mesmo caminho de quem não bloqueou: resistência e armadura de ki.
            hit.ApplyResistance(player.GetDamageModifiers(), out _);
            hit.ApplyArmor(kiArmor);

            float damageStagger = hit.m_damage.GetTotalStaggerDamage();
            player.GetSEMan().ModifyStagger(damageStagger, ref damageStagger);

            float finalDamage = hit.GetTotalDamage();
            float totalStagger = blockStagger + damageStagger;

            Print($"{label} (power {blockPower:0.##}):");
            Print($"  block: {blocked:0.##} blocked ({blocked / blockable * 100f:0}%), " +
                  $"stagger +{blockStagger:0.##} {PercentOfThreshold(blockStagger, threshold)}" +
                  $" → {(holds ? "holds" : haveStamina ? "FAILS, this hit staggers you" : "FAILS, not enough stamina")}");
            Print($"  stamina {stamina:0.##} of {player.GetStamina():0.##} " +
                  $"(used fraction {usedFraction:0.##})");
            Print($"  final: {finalDamage:0.##} damage " +
                  $"({(damage - finalDamage) / damage * 100f:0}% absorbed), " +
                  $"stagger +{totalStagger:0.##} {PercentOfThreshold(totalStagger, threshold)} " +
                  $"({blockStagger:0.##} on block + {damageStagger:0.##} on damage)");
        }

        /// <summary>
        /// O block power que o golpe vai de fato encontrar: o do poder com o ki ligado, o do item
        /// com ele desligado. Reproduz a decisão do <c>BlockPowerPatch</c>, que não dá para chamar
        /// daqui — ele só troca o valor <b>dentro</b> do <c>Humanoid.BlockAttack</c>, e o comando
        /// nunca entra lá.
        /// </summary>
        private static float EffectiveBlockPower(Player player, ItemDrop.ItemData blocker)
        {
            return KiManager.IsEnabled
                ? PowerLevel.GetBlockPower(player)
                : blocker.GetBlockPower(player.GetSkillFactor(Skills.SkillType.Blocking));
        }

        /// <summary>
        /// <c>HitData</c> descartável de contusão pura, sem atacante. Nunca chega perto de um
        /// <c>RPC_Damage</c> — só serve de entrada para as funções de cálculo do jogo.
        /// </summary>
        private static HitData MakeHit(float damage)
        {
            var hit = new HitData();
            hit.m_damage.m_blunt = damage;
            return hit;
        }

        private static string PercentOfThreshold(float staggerDamage, float threshold)
        {
            return threshold > 0f ? $"({staggerDamage / threshold * 100f:0}% of the bar)" : "(no stagger bar)";
        }

        private static string DescribeModifiers(List<HitData.DamageModPair> modifiers)
        {
            if (modifiers == null || modifiers.Count == 0)
            {
                return "none";
            }

            var text = new StringBuilder();
            foreach (HitData.DamageModPair pair in modifiers)
            {
                if (text.Length > 0)
                {
                    text.Append(", ");
                }

                text.Append(pair.m_type).Append(' ').Append(pair.m_modifier);
            }

            return text.ToString();
        }
    }
}
