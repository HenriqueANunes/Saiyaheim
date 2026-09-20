using Jotunn.Configs;
using Jotunn.Managers;
using Saiyaheim.Ki;
using Saiyaheim.Util;

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
            Type = SkillManager.Instance.AddSkill(new SkillConfig
            {
                Identifier = Identifier,
                Name = "Flight",
                Description = "Grows while you are airborne. Higher levels fly faster and burn less ki.",
                IncreaseStep = 1f,
                Icon = IconLoader.Load("flight"),
            });

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
        /// XP por distância percorrida no ar. O chamador acumula os metros e passa de uma vez —
        /// <c>RaiseSkill</c> a cada passo de física seriam ~50 chamadas por segundo por nada.
        ///
        /// <b>Por distância e não por tempo</b> desde 2026-09-20: por tempo, pairar parado pagava o
        /// mesmo que atravessar o mapa, e mais barato, porque o <c>HoverKiMultiplier</c> corta o
        /// custo justamente quando nada se move. Por metro, o XP acompanha o que custa ki — XP por
        /// ki gasto fica constante entre o voo normal e o rápido — e a única forma de farmar é
        /// voar de verdade. Ver <see cref="SE_Flight"/> para o odômetro.
        /// </summary>
        internal static void RaiseFromFlightDistance(Player player, float meters)
        {
            // Ki desligado não acumula progressão do mod — é a regra do toggle. Na prática não dá
            // para chegar aqui com ele desligado (o voo cai junto), mas a regra vale igual.
            if (player == null || !IsRegistered || !KiManager.IsEnabled || meters <= 0f)
            {
                return;
            }

            float xp = meters * SaiyaheimConfig.FlightXpPerMeter.Value;
            if (xp <= 0f)
            {
                return;
            }

            player.RaiseSkill(Type, xp);
        }
    }
}
