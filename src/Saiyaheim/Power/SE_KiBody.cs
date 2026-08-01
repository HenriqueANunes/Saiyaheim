using Saiyaheim.Ki;
using UnityEngine;

namespace Saiyaheim.Power
{
    /// <summary>
    /// O corpo de ki: o <c>StatusEffect</c> ativo enquanto o ki está ligado.
    ///
    /// Faz as duas coisas que o [[Power Level]] entrega ao combate:
    /// <list type="number">
    /// <item><b>soma dano cru no soco</b>, via <c>ModifyAttack</c>, que recebe o <c>HitData</c>
    /// por referência antes do golpe sair. É o aditivo que o <c>SE_Stats.m_damageModifier</c>,
    /// só multiplicativo, não dava;</item>
    /// <item><b>substitui a armadura do equipamento</b>, via <c>ModifyArmorMods</c>.
    /// <c>Player.GetBodyArmor()</c> soma as peças e termina chamando
    /// <c>m_seman.ApplyArmorMods(ref armor)</c> — quem escreve por último manda.</item>
    /// </list>
    ///
    /// <b>As duas no mesmo efeito de propósito.</b> <c>SEMan.ModifyAttack</c> percorre os status
    /// effects na ordem em que foram adicionados; se o poder somasse aqui e a transformação
    /// multiplicasse noutro SE, o resultado dependeria dessa ordem. Quando a etapa 5 chegar, o
    /// multiplicador da forma entra <b>neste</b> arquivo, depois da soma.
    ///
    /// Herda de <c>SE_Stats</c> para que a etapa 5 possa usar os campos dele (velocidade,
    /// regeneração, <c>m_modifyAttackSkill</c>) sem trocar a hierarquia.
    /// </summary>
    internal class SE_KiBody : SE_Stats
    {
        /// <summary>
        /// Nome do objeto, não o campo <c>m_name</c>: <c>StatusEffect.NameHash()</c> usa
        /// <c>UnityEngine.Object.name</c>. É por ele que o SEMan identifica o efeito.
        /// </summary>
        internal const string ObjectName = "SE_SaiyaheimKiBody";

        internal static readonly int NameHashValue = ObjectName.GetStableHashCode();

        /// <summary>
        /// Frame em que o ki já foi cobrado. <c>SEMan.ModifyAttack</c> roda <b>dentro do laço de
        /// alvos atingidos</b>: um golpe que pega três inimigos dispara três vezes. O bônus de
        /// dano vale por alvo, a cobrança de ki não.
        /// </summary>
        private int _chargedFrame = -1;

        /// <summary>
        /// Bônus já pago neste frame, reusado pelos alvos seguintes do mesmo golpe. Zero significa
        /// golpe sem bônus — ki insuficiente, ki desligado ou poder zerado.
        /// </summary>
        private float _damageBonusThisFrame;

        internal static SE_KiBody CreateTemplate()
        {
            var effect = CreateInstance<SE_KiBody>();
            effect.name = ObjectName;
            effect.m_name = "Ki Body";
            effect.m_tooltip = "Damage and armor come from your battle power.";

            // Sem ícone: SEMan.GetHUDStatusEffects filtra por m_icon, então o efeito não ocupa
            // espaço na barra de status. O ícone de estado do toggle é outra coisa, e vem depois.
            effect.m_icon = null;

            // m_ttl = 0 é permanente. Quem tira é o toggle, não o tempo.
            effect.m_ttl = 0f;

            return effect;
        }

        public override void ModifyAttack(Skills.SkillType skill, ref HitData hitData)
        {
            if (IsPunch(skill))
            {
                // Contusão pura: é o soco base. Cada forma vai misturar uma fração de outro tipo
                // de dano (raio, fogo) na etapa 5 — é onde o sabor por forma sai quase de graça.
                hitData.m_damage.m_blunt += ResolveBonusForThisFrame();
            }

            // Base por último: o SE_Stats multiplica, e queremos (base + poder) * mult,
            // não base * mult + poder.
            base.ModifyAttack(skill, ref hitData);
        }

        /// <summary>
        /// Se este golpe é um soco — o que <b>não</b> é a mesma pergunta que "o parâmetro
        /// <paramref name="skill"/> vale <c>Unarmed</c>".
        ///
        /// ⚠️ <c>Attack.DoMeleeAttack</c> troca a skill do golpe por <c>m_specialHitSkill</c>
        /// quando o alvo casa com <c>m_specialHitType</c> — é o mecanismo que faz machado virar
        /// Woodcutting em árvore, e ele escreve tanto no parâmetro quanto no <c>hitData.m_skill</c>.
        /// Nos golpes em que isso acontece, o parâmetro mente sobre o que o jogador está fazendo.
        ///
        /// A arma equipada não mente: <c>GetCurrentWeapon()</c> nunca devolve null — sem nada
        /// equipado ele entrega o <c>m_unarmedWeapon</c>, cujo <c>m_skillType</c> é <c>Unarmed</c>.
        /// Vale para o ataque primário, o secundário e o combo, porque os três saem do mesmo item
        /// (<c>Humanoid.StartAttack</c> só escolhe entre <c>m_attack</c> e <c>m_secondaryAttack</c>
        /// do <b>mesmo</b> <c>m_shared</c>).
        ///
        /// Cobre também o clone das Flesh Rippers da etapa 5, que é `Unarmed` igual.
        /// </summary>
        private bool IsPunch(Skills.SkillType skill)
        {
            if (skill == Skills.SkillType.Unarmed)
            {
                return true;
            }

            ItemDrop.ItemData weapon = (m_character as Player)?.GetCurrentWeapon();

            return weapon != null && weapon.m_shared.m_skillType == Skills.SkillType.Unarmed;
        }

        public override void ModifyArmorMods(ref float armor)
        {
            // O cast falhando zeraria a armadura do alvo, então sai sem tocar em nada.
            // Nada tira `armor` do valor vanilla a não ser uma substituição deliberada.
            if (!KiManager.IsEnabled || !(m_character is Player player))
            {
                return;
            }

            // Atribuição, não soma: a armadura do equipamento é descartada. Quem usa ki abre mão
            // da build vanilla inteira. Desligar o toggle remove este efeito e devolve a armadura
            // das peças na hora.
            armor = PowerLevel.GetArmor(player);
        }

        /// <summary>
        /// Cobra ki por apanhar, proporcional ao dano que a armadura de ki <b>absorveu</b>.
        /// Fecha a economia da barra: sustentar o corpo de ki custa, defender também.
        ///
        /// <b>Por que o absorvido e não o dano bruto nem o aplicado.</b> É o único dos três que
        /// mede o serviço que a armadura de fato prestou — e por causa disso o filtro de fontes de
        /// dano sai de graça. Veneno, queda e afogamento chegam como dano genérico
        /// (<c>m_damage</c>), que o <c>DamageTypes.ApplyArmor</c> do jogo nem soma: a armadura não
        /// os toca no vanilla, então absorvem zero e custam zero. Nenhuma lista de exceções para
        /// manter atualizada a cada golpe novo que o Valheim inventar.
        ///
        /// <b>Nenhum patch Harmony.</b> <c>StatusEffect.OnDamaged</c> é virtual e o
        /// <c>SEMan.OnDamaged</c> chama todo efeito ativo dentro do <c>RPC_Damage</c>.
        ///
        /// ⚠️ O lugar que <b>não</b> serve é o <c>ModifyArmorMods</c>: ele roda também para
        /// exibição, via <c>GetBodyArmor()</c>, e cobrar ali drenaria ki por abrir o inventário.
        /// </summary>
        public override void OnDamaged(HitData hit, Character attacker)
        {
            base.OnDamaged(hit, attacker);

            float rate = SaiyaheimConfig.DamageTakenKiCost.Value;
            if (rate <= 0f || hit == null || !KiManager.IsEnabled || !(m_character is Player player))
            {
                return;
            }

            float absorbed = EstimateArmorAbsorption(hit, player);
            if (absorbed <= 0f)
            {
                return;
            }

            // Drain e não TryConsume: a barra vazia não impede o golpe de acontecer, e o
            // que sobrar do custo simplesmente não é cobrado.
            KiManager.Drain(absorbed * rate);

            // Sem isto a calibração do DamageTakenKiCost é às cegas: na tela o jogador só vê a
            // barra andar, não quanto do golpe a armadura barrou.
            SaiyaheimPlugin.LogVerbose(
                $"Hit for {hit.GetTotalDamage():0.#} raw, ki armor absorbed {absorbed:0.#} " +
                $"→ {absorbed * rate:0.#} ki ({KiManager.Current:0.#} left).");
        }

        /// <summary>
        /// Quanto a armadura de ki vai barrar deste golpe.
        ///
        /// <b>Estimativa, e de propósito.</b> O <c>SEMan.OnDamaged</c> roda cedo no
        /// <c>RPC_Damage</c> — antes da resistência e antes da armadura — então o dano final ainda
        /// não existe quando somos chamados. A alternativa seria parear este hook com o postfix de
        /// <c>Character.ApplyDamage</c>, e ela é pior do que parece: quando o <c>ApplyDamage</c>
        /// roda, o jogo já zerou veneno, fogo e espírito do <c>HitData</c> para aplicá-los ao longo
        /// do tempo, então nem lá o número está inteiro.
        ///
        /// Refazemos as duas etapas numa cópia, com as funções públicas do próprio jogo — nada de
        /// fórmula duplicada à mão, que sairia de sincronia na primeira atualização:
        /// <c>ApplyResistance</c> com os modificadores do jogador e o <c>HitData.ApplyArmor</c>
        /// estático.
        ///
        /// A cópia diverge do real num caso só: golpe <b>bloqueado</b>, que o jogo reduz depois
        /// deste ponto. O erro é para cima e o custo é subestimar o bloqueio — cobra-se um pouco a
        /// mais de ki de quem bloqueou. Aceitável enquanto a build do modo ki for punho nu.
        /// </summary>
        private static float EstimateArmorAbsorption(HitData hit, Player player)
        {
            float armor = PowerLevel.GetArmor(player);
            if (armor <= 0f)
            {
                return 0f;
            }

            // Clone antes de qualquer coisa: ApplyResistance e ApplyArmor escrevem no HitData, e
            // este é o golpe de verdade, ainda a caminho da vida do jogador.
            HitData copy = hit.Clone();
            copy.ApplyResistance(player.GetDamageModifiers(), out _);

            // Medir a diferença em vez de recalcular a fórmula: qual dano a armadura reduz é
            // decisão do jogo, e o DamageTypes.ApplyArmor dele nem soma o dano genérico
            // (m_damage — queda, afogamento, veneno ao longo do tempo), nem m_chop, nem m_pickaxe.
            // Fazer a conta aqui seria copiar essa lista e perdê-la de vista na próxima
            // atualização. Assim, se o Valheim mudar o que a armadura cobre, o custo de ki
            // acompanha sozinho.
            float before = copy.GetTotalDamage();
            copy.ApplyArmor(armor);

            return Mathf.Max(0f, before - copy.GetTotalDamage());
        }

        /// <summary>
        /// Cobra o ki do golpe uma única vez por frame e devolve o bônus de dano que ele comprou.
        ///
        /// <b>O custo é proporcional ao bônus</b>, espelhando o <c>OnDamaged</c>: lá se cobra pelo
        /// dano que a armadura de ki barrou, aqui pelo dano que o poder somou. Os dois medem o
        /// serviço que o ki prestou, e por isso envelhecem bem — um custo fixo faria o soco ficar
        /// cada vez mais barato em relação ao que entrega, já que o bônus cresce com o poder.
        ///
        /// <b>O dano desarmado vanilla sai de graça</b>, e é a leitura certa: o ki não o produziu.
        /// É também o único número disponível aqui — quanto dano o golpe de fato aplica só se sabe
        /// depois da armadura e das resistências do alvo, do outro lado do <c>RPC_Damage</c> e
        /// possivelmente noutra máquina.
        ///
        /// Ki insuficiente <b>não cancela o golpe</b> — o soco sai com o dano vanilla cru. Cancelar
        /// seria o pior padrão de UX possível (botão de atacar que não responde) e nem é possível
        /// aqui: quando <c>ModifyAttack</c> roda, o golpe já saiu. Por isso <c>TryConsume</c> e não
        /// <c>Drain</c>: é tudo ou nada, meia barra não compra meio bônus.
        ///
        /// Golpe que erra não cobra nada, porque este método só é alcançado quando há alvo.
        /// </summary>
        private float ResolveBonusForThisFrame()
        {
            if (_chargedFrame == Time.frameCount)
            {
                return _damageBonusThisFrame;
            }

            _chargedFrame = Time.frameCount;
            _damageBonusThisFrame = 0f;

            if (!KiManager.IsEnabled || !(m_character is Player player))
            {
                return 0f;
            }

            float bonus = PowerLevel.GetPunchDamageBonus(player);
            if (bonus <= 0f)
            {
                return 0f;
            }

            float cost = bonus * SaiyaheimConfig.PunchKiCostPerDamage.Value;
            if (cost > 0f && !KiManager.TryConsume(cost))
            {
                return 0f;
            }

            _damageBonusThisFrame = bonus;

            // Simétrico ao log do OnDamaged, e pelo mesmo motivo: sem ele a calibração do
            // PunchKiCostPerDamage é às cegas — na tela só se vê a barra andar.
            SaiyaheimPlugin.LogVerbose(
                $"Punch bonus {bonus:0.#} blunt → {cost:0.#} ki ({KiManager.Current:0.#} left).");

            return bonus;
        }
    }
}
