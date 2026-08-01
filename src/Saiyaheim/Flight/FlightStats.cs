using UnityEngine;

namespace Saiyaheim.Flight
{
    /// <summary>
    /// As contas do voo, separadas do <see cref="SE_Flight"/> para que o comando de debug possa
    /// mostrar exatamente os mesmos números que o efeito usa — sem duplicar fórmula.
    ///
    /// Tudo aqui é função pura do estado atual do jogador (skill, peso, config). Nada é cacheado:
    /// o peso muda a cada item pego e a config pode ser editada com o jogo aberto.
    /// </summary>
    internal static class FlightStats
    {
        /// <summary>
        /// Carga do inventário em 0–1. É o mesmo dado que o <c>SkillXpWeightBonus</c> usa na
        /// etapa 3, e de propósito: peso paga XP de Battle Power e cobra velocidade de voo.
        /// A roupa pesada do Goku é exatamente essa troca.
        /// </summary>
        internal static float GetWeightLoad(Player player)
        {
            if (player == null)
            {
                return 0f;
            }

            float maxWeight = player.GetMaxCarryWeight();
            Inventory inventory = player.GetInventory();
            if (maxWeight <= 0f || inventory == null)
            {
                return 0f;
            }

            return Mathf.Clamp01(inventory.GetTotalWeight() / maxWeight);
        }

        /// <summary>
        /// Velocidade somada pelo power level.
        ///
        /// <b>Aditiva, não multiplicativa</b>, pelo mesmo motivo do dano do soco: o poder cresce
        /// sem teto (HP acima da base + nível de Battle Power) e multiplicá-lo faria a velocidade
        /// explodir contra o teto do <c>MaxSpeed</c> cedo demais.
        ///
        /// Efeito colateral desejado: comer melhor faz voar mais rápido, porque HP entra na
        /// fórmula do poder. É a leitura correta de "ficar mais forte" no gênero.
        /// </summary>
        internal static float GetSpeedFromPower(Player player)
        {
            return Power.PowerLevel.GetRaw(player) * SaiyaheimConfig.FlightSpeedFromPower.Value;
        }

        /// <summary>
        /// Velocidade base, já com poder, skill e peso. É o valor que vai para
        /// <c>Character.m_flySlowSpeed</c>.
        ///
        /// O peso multiplica <b>tudo</b>, inclusive a parcela do poder: carregar meio inventário
        /// deve doer no jogador forte tanto quanto no fraco.
        /// </summary>
        internal static float GetSlowSpeed(Player player)
        {
            float skillFactor = 1f + SaiyaheimConfig.FlightSpeedSkillBonus.Value * FlightSkill.GetLevelFactor(player);
            float weightFactor = 1f - SaiyaheimConfig.FlightWeightPenalty.Value * GetWeightLoad(player);

            float baseSpeed = SaiyaheimConfig.FlightBaseSpeed.Value + GetSpeedFromPower(player);
            float speed = baseSpeed * skillFactor * weightFactor;

            // Piso baixo, não zero: com WeightPenalty em 1 e peso máximo o jogador ficaria parado
            // no ar sem entender por quê.
            return Mathf.Clamp(speed, 1f, SaiyaheimConfig.FlightMaxSpeed.Value);
        }

        /// <summary>Velocidade com o botão de correr segurado. Vai para <c>m_flyFastSpeed</c>.</summary>
        internal static float GetFastSpeed(Player player)
        {
            float speed = GetSlowSpeed(player) * SaiyaheimConfig.FlightFastSpeedMultiplier.Value;

            // O teto vale aqui também: ele é limite do streaming de zonas, não balanceamento,
            // e o modo rápido é justamente onde ele seria estourado.
            return Mathf.Clamp(speed, 1f, SaiyaheimConfig.FlightMaxSpeed.Value);
        }

        /// <summary>
        /// Ki por segundo. O <paramref name="fast"/> vem do mesmo <c>m_run</c> que o
        /// <c>UpdateFlying</c> vanilla lê para escolher a velocidade — os dois andam juntos.
        /// </summary>
        internal static float GetKiCostPerSecond(Player player, bool fast)
        {
            float cost = SaiyaheimConfig.FlightKiPerSecond.Value;

            if (fast)
            {
                cost *= SaiyaheimConfig.FlightFastKiMultiplier.Value;
            }

            float reduction = SaiyaheimConfig.FlightKiSkillReduction.Value * FlightSkill.GetLevelFactor(player);

            return Mathf.Max(0f, cost * (1f - reduction));
        }
    }
}
