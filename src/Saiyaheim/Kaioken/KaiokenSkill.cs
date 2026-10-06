using Jotunn.Configs;
using Jotunn.Managers;
using Saiyaheim.Ki;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Kaioken
{
    /// <summary>
    /// A skill Kaioken (etapa 14): uma só para todos os tiers, registrada do mesmo jeito que a
    /// <c>PowerSkill</c> — skill nativa via Jotunn, com persistência no save, entrada no menu de
    /// skills e a curva de ganho decrescente do Valheim de graça.
    ///
    /// <b>É o que a margem lê.</b> Ela não deixa o Kaioken mais forte; deixa mais seguro e mais
    /// barato. Sobe <b>por dano causado</b> com o Kaioken ligado, igual ao Power Level e lenta como
    /// ele, e paga mais com a margem negativa: treinar além do limite rende mais.
    /// </summary>
    internal static class KaiokenSkill
    {
        // Vira o hash do save: não mudar depois de jogar.
        private const string Identifier = "saiyaheim.kaioken";

        internal static Skills.SkillType Type { get; private set; } = Skills.SkillType.None;

        internal static bool IsRegistered => Type != Skills.SkillType.None;

        internal static void Register()
        {
            Type = SkillManager.Instance.AddSkill(new SkillConfig
            {
                Identifier = Identifier,
                Name = "Kaioken",
                Description = "Grows by landing blows with Kaioken on, and faster when the tier is beyond " +
                              "your limit. Each tier is safe from a certain level: below it Kaioken burns " +
                              "health, above it the stamina cost shrinks. It does not make Kaioken stronger.",
                IncreaseStep = 1f,
                Icon = IconLoader.Load("kaioken"),
            });

            SkillCommandAliases.Register("Kaioken");

            SaiyaheimPlugin.Log.LogInfo($"Skill 'Kaioken' registered ({Type}).");
        }

        internal static float GetLevel(Player player)
        {
            if (player == null || !IsRegistered)
            {
                return 0f;
            }

            return player.GetSkillLevel(Type);
        }

        /// <summary>
        /// XP por um golpe causado. Não paga nada com o Kaioken desligado — a skill treina usando o
        /// Kaioken, não lutando em geral, que já é o Power Level.
        ///
        /// <code>min(dano × taxa, grampo) × (1 + bônus × min(|margem| / cheio, 1))</code>
        ///
        /// O bônus só existe com margem negativa e cresce com a distância até o nível seguro,
        /// inteiro a <c>XpNegativeMarginFullAt</c> níveis abaixo (2026-10-05).
        ///
        /// O bônus entra depois do grampo pelo mesmo motivo da maestria das formas: um grampo que
        /// comesse o bônus anularia o incentivo justamente nos golpes grandes.
        /// </summary>
        internal static void RaiseFromDamageDealt(Player player, float applied)
        {
            if (player == null || !IsRegistered || !KiManager.IsEnabled || applied <= 0f)
            {
                return;
            }

            KaiokenTier active = KaiokenRegistry.GetActive(player);
            if (active == null)
            {
                return;
            }

            float rate = SaiyaheimConfig.KaiokenXpPerDamageDealt.Value;
            if (rate <= 0f)
            {
                return;
            }

            float xp = Mathf.Min(applied * rate, SaiyaheimConfig.KaiokenXpMaxPerEvent.Value);

            float margin = active.GetMargin(player);
            if (margin < 0f)
            {
                float fullAt = Mathf.Max(1f, SaiyaheimConfig.KaiokenXpNegativeMarginFullAt.Value);
                float share = Mathf.Min(-margin / fullAt, 1f);
                xp *= 1f + Mathf.Max(0f, SaiyaheimConfig.KaiokenXpNegativeMarginBonus.Value) * share;
            }

            if (xp > 0f)
            {
                player.RaiseSkill(Type, xp);
            }
        }
    }
}
