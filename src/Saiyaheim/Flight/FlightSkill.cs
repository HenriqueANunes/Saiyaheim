using Jotunn.Managers;
using Saiyaheim.Ki;

namespace Saiyaheim.Flight
{
    /// <summary>
    /// A skill "Flight": o eixo de progressão do voo.
    ///
    /// Sobe pelo único treino que existe para ela — ficar no ar — e paga em duas moedas:
    /// mais velocidade (<c>FlightSpeedSkillBonus</c>) e menos ki por segundo
    /// (<c>FlightKiSkillReduction</c>). As duas juntas significam "mais distância por ponto de ki",
    /// que é a progressão que o design pede.
    ///
    /// Skill nativa via Jotunn pelos mesmos motivos da <see cref="Power.PowerSkill"/>:
    /// persistência no save, entrada no menu de skills e curva de ganho decrescente até 100
    /// saem de graça.
    /// </summary>
    internal static class FlightSkill
    {
        /// <summary>
        /// Identificador único da skill. Vira o hash que o save usa — <b>não mudar depois de jogar.</b>
        /// Trocar cria uma skill nova e o nível volta a zero.
        /// </summary>
        private const string Identifier = "saiyaheim.flight";

        /// <summary>Nível máximo de qualquer skill no Valheim.</summary>
        internal const float MaxLevel = 100f;

        internal static Skills.SkillType Type { get; private set; } = Skills.SkillType.None;

        internal static bool IsRegistered => Type != Skills.SkillType.None;

        internal static void Register()
        {
            // Ícone null é aceito pelo Jotunn: a skill aparece no menu sem arte própria.
            Type = SkillManager.Instance.AddSkill(
                Identifier,
                "Flight",
                "Grows while you are airborne. Higher levels fly faster and burn less ki.",
                increaseStep: 1f);

            SaiyaheimPlugin.Log.LogInfo($"Skill 'Flight' registered ({Type}).");
        }

        /// <summary>Nível atual, 0–100. Zero se a skill não existe ou não há jogador.</summary>
        internal static float GetLevel(Player player)
        {
            if (player == null || !IsRegistered)
            {
                return 0f;
            }

            return player.GetSkillLevel(Type);
        }

        /// <summary>Nível normalizado em 0–1, que é a forma como as fórmulas usam.</summary>
        internal static float GetLevelFactor(Player player)
        {
            return GetLevel(player) / MaxLevel;
        }

        /// <summary>
        /// XP por tempo de voo. O chamador acumula os segundos e passa de uma vez —
        /// <c>RaiseSkill</c> a cada passo de física seriam ~50 chamadas por segundo por nada.
        /// </summary>
        internal static void RaiseFromFlightTime(Player player, float seconds)
        {
            // Ki desligado não acumula progressão do mod — é a regra do toggle. Na prática não dá
            // para chegar aqui com ele desligado (o voo cai junto), mas a regra vale igual.
            if (player == null || !IsRegistered || !KiManager.IsEnabled || seconds <= 0f)
            {
                return;
            }

            float xp = seconds * SaiyaheimConfig.FlightXpPerSecond.Value;
            if (xp <= 0f)
            {
                return;
            }

            player.RaiseSkill(Type, xp);
        }
    }
}
