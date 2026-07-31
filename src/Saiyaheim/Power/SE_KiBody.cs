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

        /// <summary>Resultado da cobrança do frame, reusado pelos alvos seguintes do mesmo golpe.</summary>
        private bool _bonusActive;

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
            if (skill == Skills.SkillType.Unarmed && ResolveBonusForThisFrame())
            {
                // Contusão pura: é o soco base. Cada forma vai misturar uma fração de outro tipo
                // de dano (raio, fogo) na etapa 5 — é onde o sabor por forma sai quase de graça.
                hitData.m_damage.m_blunt += PowerLevel.GetPunchDamageBonus(m_character as Player);
            }

            // Base por último: o SE_Stats multiplica, e queremos (base + poder) * mult,
            // não base * mult + poder.
            base.ModifyAttack(skill, ref hitData);
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
        /// Cobra o ki do golpe uma única vez por frame e devolve se o bônus se aplica.
        ///
        /// Ki insuficiente <b>não cancela o golpe</b> — o soco sai com o dano vanilla cru. Cancelar
        /// seria o pior padrão de UX possível (botão de atacar que não responde) e nem é possível
        /// aqui: quando <c>ModifyAttack</c> roda, o golpe já saiu.
        ///
        /// Golpe que erra não cobra nada, porque este método só é alcançado quando há alvo.
        /// </summary>
        private bool ResolveBonusForThisFrame()
        {
            if (!KiManager.IsEnabled)
            {
                return false;
            }

            if (_chargedFrame == Time.frameCount)
            {
                return _bonusActive;
            }

            _chargedFrame = Time.frameCount;

            float cost = SaiyaheimConfig.PunchKiCost.Value;
            _bonusActive = cost <= 0f || KiManager.TryConsume(cost);

            return _bonusActive;
        }
    }
}
