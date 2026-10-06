using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Kaioken
{
    /// <summary>
    /// A Exaustão (etapa 14): o que sobra de ficar sem stamina com o Kaioken ligado. Tempo fixo,
    /// sem regeneração de stamina, mais lento, e Kaioken bloqueado até acabar — quem bloqueia é o
    /// <see cref="KaiokenManager"/>, que só pergunta se este efeito existe.
    ///
    /// <b>Por que existe</b>: sem ela, zerar a stamina só desligaria o Kaioken, e o jogador religaria
    /// no segundo seguinte com a stamina regenerada pela metade. A janela curta do Kaioken precisa
    /// de um depois que custe.
    /// </summary>
    internal class SE_KaiokenExhaustion : SE_Stats
    {
        internal const string ObjectNameValue = "SE_SaiyaheimKaiokenExhaustion";

        internal static readonly int NameHashValue = ObjectNameValue.GetStableHashCode();

        internal static SE_KaiokenExhaustion CreateTemplate()
        {
            var effect = CreateInstance<SE_KaiokenExhaustion>();

            effect.name = ObjectNameValue;
            effect.m_name = "Exhausted";
            effect.m_tooltip = "You pushed Kaioken until your stamina gave out. No stamina regeneration, " +
                               "slower movement and no Kaioken until it wears off.";

            // O ícone do Encumbered, emprestado da vanilla: é o sinal que o jogador já lê como
            // "estou pesado e lento". Sem ícone a barra de status esconde o efeito, e o tempo que
            // falta (que o jogo desenha sozinho com m_ttl) é justamente o que importa aqui.
            StatusEffect encumbered = ObjectDB.instance == null
                ? null
                : ObjectDB.instance.GetStatusEffect(SEMan.s_statusEffectEncumbered);
            effect.m_icon = encumbered == null ? null : encumbered.m_icon;

            // O tempo é escrito pelo manager na instância ativa, e não aqui: o template é criado
            // uma vez, e o Clone() congelaria o .cfg lido nessa hora.
            effect.m_ttl = 1f;

            return effect;
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);

            if (m_character is Player player && player == Player.m_localPlayer)
            {
                // Um pouco além do próximo tick: segura a regeneração enquanto o efeito viver, e
                // solta logo depois de ele acabar.
                GameAccess.HoldStaminaRegen(player, 0.1f);
            }
        }

        /// <summary>Rede de segurança caso o campo do atraso suma numa atualização do jogo.</summary>
        public override void ModifyStaminaRegen(ref float staminaRegen)
        {
            base.ModifyStaminaRegen(ref staminaRegen);
            staminaRegen = 0f;
        }

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            base.ModifySpeed(baseSpeed, ref speed, character, dir);
            speed -= baseSpeed * Mathf.Clamp01(SaiyaheimConfig.KaiokenExhaustionSlow.Value);
        }
    }
}
