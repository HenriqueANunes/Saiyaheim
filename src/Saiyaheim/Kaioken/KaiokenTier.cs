using Saiyaheim.Runes;
using Saiyaheim.Transformations;
using UnityEngine;

namespace Saiyaheim.Kaioken
{
    /// <summary>
    /// Um tier de Kaioken (etapa 14): os números dele, a identidade do <see cref="SE_Kaioken"/> que
    /// o representa em jogo e a conta da margem.
    ///
    /// <b>Tudo sai de uma variável só, a margem</b>:
    /// <code>
    /// necessário = seguro do tier + penalidade da forma ativa
    /// margem     = nível da skill Kaioken − necessário
    /// stamina/s  = base × max(piso, 1 − corte × max(0, margem))
    /// vida/s     = vida por nível × max(0, −margem)
    /// </code>
    ///
    /// <b>Tier é dado, não código</b>, como a forma: os cinco da lore são este objeto instanciado
    /// cinco vezes no <see cref="KaiokenRegistry"/>. O rótulo ("x20") é nome; a força de verdade é
    /// o <c>PowerMultiplier</c> do config.
    ///
    /// <b>Ensinado por runestone</b>, no mesmo sorteio dos ataques de ki, e em ordem estrita: o tier
    /// só é candidato com o anterior aprendido (<see cref="PrerequisiteMet"/>). Ver [[Kaioken]].
    /// </summary>
    internal class KaiokenTier : IRuneLesson
    {
        /// <summary>Posição na escada de tiers, de 0 (x2) a 4 (x20).</summary>
        internal int Index { get; }

        /// <summary>O rótulo da lore: "x2", "x20".</summary>
        internal string Name { get; }

        /// <summary>
        /// Id estável. Divide a lista de aprendidos com os ataques de ki, e mudar depois de jogar
        /// faz o personagem esquecer o tier.
        /// </summary>
        internal string Id { get; }

        internal string DisplayName { get; }

        internal SaiyaheimConfig.KaiokenTierConfig Config { get; }

        /// <summary>Nome do objeto do status effect. É por ele que o <c>SEMan</c> acha o tier.</summary>
        internal string ObjectName { get; }

        internal int NameHashValue { get; }

        internal KaiokenTier(int index, string name, SaiyaheimConfig.KaiokenTierConfig config)
        {
            Index = index;
            Name = name;
            Id = "kaioken_" + name;
            DisplayName = "Kaioken " + name;
            Config = config;

            ObjectName = "SE_SaiyaheimKaioken_" + name;
            NameHashValue = ObjectName.GetStableHashCode();
        }

        // O lado runestone do tier. Explícito porque membro de interface é público e o resto da
        // classe é internal.
        string IRuneLesson.Id => Id;
        string IRuneLesson.DisplayName => DisplayName;
        Heightmap.Biome IRuneLesson.LearnBiome => Config.LearnBiome.Value;
        float IRuneLesson.LearnWeight => Config.LearnWeight.Value;

        /// <summary>
        /// Ordem estrita: o x2 sempre pode, os outros só com o anterior aprendido. Quem chega à
        /// Floresta Negra sem o x2 não aprende nada de Kaioken lá — o x2 só existe nos Prados.
        /// </summary>
        public bool PrerequisiteMet(Player player)
        {
            return Index == 0 || KaiokenRegistry.All[Index - 1].IsLearned(player);
        }

        internal bool IsLearned(Player player)
        {
            return RuneKnowledge.HasLearned(player, this);
        }

        /// <summary>
        /// Multiplicador do battle power de combate, por cima da forma. Piso em 1, pela mesma regra
        /// das formas: o <c>.cfg</c> não pode inverter o sentido da mecânica.
        ///
        /// ⚠️ Lido de dentro do <c>BattlePower.GetKiCombatRaw</c>: não pode ler battle power.
        /// </summary>
        internal float GetPowerMultiplier()
        {
            return Mathf.Max(1f, Config.PowerMultiplier.Value);
        }

        /// <summary>Nível da skill Kaioken que este tier pede para não custar vida, na forma dada.</summary>
        internal float GetNeededLevel(Player player, Transformation form)
        {
            float penalty = form == null ? 0f : form.GetKaiokenPenalty(player);

            return Mathf.Max(0f, Config.SafeLevel.Value) + penalty;
        }

        /// <summary>A margem agora, com a forma ativa do jogador. Negativa = além do limite.</summary>
        internal float GetMargin(Player player)
        {
            return KaiokenSkill.GetLevel(player) - GetNeededLevel(player, TransformationRegistry.GetActive(player));
        }

        /// <summary>
        /// Stamina por segundo nesta margem. Cheia até a margem 0; acima, cai em linha reta até o
        /// piso, que nunca é zero — um Kaioken de graça ficaria ligado para sempre.
        /// </summary>
        internal float GetStaminaPerSecond(float margin)
        {
            float floor = Mathf.Clamp01(SaiyaheimConfig.KaiokenStaminaFloor.Value);
            float cut = Mathf.Max(0f, SaiyaheimConfig.KaiokenStaminaCutPerLevel.Value);
            float factor = Mathf.Max(floor, Mathf.Min(1f, 1f - cut * Mathf.Max(0f, margin)));

            return Mathf.Max(0f, Config.StaminaPerSecond.Value) * factor;
        }

        /// <summary>
        /// Vida por segundo nesta margem: zero com margem positiva, e proporcional à distância com
        /// margem negativa. Sem teto, de propósito — ver [[Decisões Tomadas]].
        /// </summary>
        internal float GetHealthPerSecond(float margin)
        {
            return Mathf.Max(0f, Config.HealthPerSecondPerLevel.Value) * Mathf.Max(0f, -margin);
        }

        internal float GetMoveSpeedBonus()
        {
            return Mathf.Max(0f, Config.MoveSpeedBonus.Value);
        }

        /// <summary>
        /// Multiplicador da animação de ataque corpo a corpo, 1 ou mais. Quem aplica é o
        /// <see cref="KaiokenAttackSpeedPatch"/>.
        /// </summary>
        internal float GetAttackSpeedMultiplier()
        {
            return 1f + Mathf.Max(0f, Config.AttackSpeedBonus.Value);
        }

        /// <summary>Cor da aura deste tier, em #RRGGBB. Vazio cai no vermelho puro.</summary>
        internal string GetAuraColor()
        {
            string color = Config.AuraColor;

            return string.IsNullOrEmpty(color) ? "#FF0000" : color;
        }

        internal float GetGlowIntensity()
        {
            return Mathf.Max(0f, Config.GlowIntensity);
        }

        /// <summary>O tier nesta posição, ou null. É a volta do índice que atravessa a rede.</summary>
        internal static KaiokenTier At(int index)
        {
            return index >= 0 && index < KaiokenRegistry.All.Length ? KaiokenRegistry.All[index] : null;
        }

        internal float GetMaxKiBonus()
        {
            return Mathf.Max(0f, Config.MaxKiBonus.Value);
        }
    }
}
