using Jotunn.Managers;
using Saiyaheim.Ki;
using UnityEngine;

namespace Saiyaheim.Power
{
    /// <summary>
    /// A skill "Battle Power": o eixo de progressão de quem joga com o ki ligado.
    ///
    /// Existe porque, no modo ki, o jogador abandona o caminho vanilla — soca em vez de usar
    /// arma, e a armadura dele passa a vir do power level. Isso apaga dois dos quatro termos da
    /// fórmula antiga e deixa só HP e ki; e o componente de ki não progride, porque a maestria
    /// só existe transformado (etapa 6). Sem esta skill, o poder de quem usa ki seria
    /// <b>constante do primeiro ao último boss</b>.
    ///
    /// Skill nativa via Jotunn, não float próprio: persistência no save, UI no menu de skills e
    /// curva de ganho decrescente até 100 saem de graça — mesma decisão já tomada para a maestria.
    ///
    /// <b>Não</b> reusa a Unarmed nativa: ela sobe para todo mundo que lute desarmado, inclusive
    /// com o ki desligado, e o mod não consegue impedir. Seria ler um número contaminado.
    /// </summary>
    internal static class PowerSkill
    {
        /// <summary>
        /// Identificador único da skill. Vira o hash que o save usa — <b>não mudar depois de jogar.</b>
        /// Trocar este identificador cria uma skill nova: a antiga fica órfã no save e o nível
        /// volta a zero. Foi renomeado de "saiyaheim.poder_de_luta" ainda em fase de teste,
        /// justamente para não ter que fazer isso depois que houver progresso de verdade.
        /// </summary>
        private const string Identifier = "saiyaheim.battle_power";

        /// <summary>Nível máximo de qualquer skill no Valheim.</summary>
        internal const float MaxLevel = 100f;

        internal static Skills.SkillType Type { get; private set; } = Skills.SkillType.None;

        internal static bool IsRegistered => Type != Skills.SkillType.None;

        internal static void Register()
        {
            // Ícone null é aceito pelo Jotunn: a skill aparece no menu sem arte própria.
            // Trocar por um ícone do jogo é polimento da etapa 11, não bloqueia nada.
            Type = SkillManager.Instance.AddSkill(
                Identifier,
                "Battle Power",
                "Grows by fighting with ki turned on: landing blows and taking damage. " +
                "Determines punch damage, armor and the ki cap.",
                increaseStep: 1f);

            SaiyaheimPlugin.Log.LogInfo($"Skill 'Battle Power' registered ({Type}).");
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

        /// <summary>
        /// XP por dano aplicado num inimigo. O <paramref name="applied"/> já vem limitado ao HP
        /// que o alvo realmente tinha — é isso que impede farmar bicho fraco: por mais forte que
        /// o jogador fique, matar um Boar nunca conta mais que os 10 de HP dele.
        /// </summary>
        internal static void RaiseFromDamageDealt(Player player, float applied)
        {
            Raise(player, applied * SaiyaheimConfig.SkillXpPerDamageDealt.Value);
        }

        /// <summary>
        /// XP por dano recebido. O <paramref name="applied"/> já vem <b>depois</b> da armadura e
        /// das resistências, o que mata o exploit de apanhar de propósito de um inimigo fraco:
        /// apanhar pouco rende pouco.
        /// </summary>
        internal static void RaiseFromDamageTaken(Player player, float applied)
        {
            Raise(player, applied * SaiyaheimConfig.SkillXpPerDamageTaken.Value);
        }

        private static void Raise(Player player, float xp)
        {
            // Ki desligado não acumula progressão do mod — é a regra do toggle.
            if (player == null || !IsRegistered || !KiManager.IsEnabled || xp <= 0f)
            {
                return;
            }

            xp *= GetWeightMultiplier(player);
            xp = Mathf.Min(xp, SaiyaheimConfig.SkillXpMaxPerEvent.Value);

            player.RaiseSkill(Type, xp);
        }

        /// <summary>
        /// Treino com roupa pesada: quanto mais peso carregado, mais XP.
        ///
        /// Auto-limitante por construção — peso alto deixa lento, come stamina e (a partir da
        /// etapa 4) derruba a velocidade de voo. O jogador paga mobilidade por progressão, o que
        /// é uma troca legítima e não um exploit.
        /// </summary>
        private static float GetWeightMultiplier(Player player)
        {
            float maxWeight = player.GetMaxCarryWeight();
            if (maxWeight <= 0f)
            {
                return 1f;
            }

            Inventory inventory = player.GetInventory();
            if (inventory == null)
            {
                return 1f;
            }

            float load = Mathf.Clamp01(inventory.GetTotalWeight() / maxWeight);
            return 1f + SaiyaheimConfig.SkillXpWeightBonus.Value * load;
        }
    }
}
