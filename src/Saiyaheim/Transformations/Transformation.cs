using Jotunn.Managers;
using Saiyaheim.Power;
using UnityEngine;

namespace Saiyaheim.Transformations
{
    /// <summary>
    /// Uma forma: os números dela, a skill de maestria dela e a identidade do
    /// <see cref="SE_Transformation"/> que a representa em jogo.
    ///
    /// <b>Forma é dado, não código.</b> A escada de cinco degraus prevista em
    /// [[Progressão por Bosses]] é este objeto instanciado cinco vezes no
    /// <see cref="TransformationRegistry"/> — nenhuma das outras classes do mod sabe quantas
    /// formas existem nem qual está ativa por nome.
    ///
    /// <b>Cada forma tem a skill dela</b>, registrada do mesmo jeito que <c>PowerSkill</c> e
    /// <c>FlightSkill</c>: skill nativa via <c>SkillManager.AddSkill</c> do Jotunn, com
    /// persistência no save, entrada no menu de skills e curva de ganho decrescente até 100 de
    /// graça. Ela é a maestria — sobe segurando a forma e paga em <b>dreno menor</b>, que é a
    /// única moeda dela.
    ///
    /// <b>Não confundir com Battle Power.</b> Battle Power é uma só, global, e mede quanto o
    /// jogador treinou lutando; maestria é uma por forma e mede quanto ele domina <i>aquela</i>
    /// forma. As duas sobem por caminhos diferentes e pagam em coisas diferentes.
    /// </summary>
    internal class Transformation
    {
        /// <summary>
        /// Identificador estável da forma. Entra no identificador da skill, que <b>vira o hash do
        /// save</b> — mudar depois de jogar cria uma skill nova e zera o nível de quem já treinou.
        /// </summary>
        internal string Id { get; }

        /// <summary>Nome que o jogador lê: na skill, na mensagem de tela e no comando de debug.</summary>
        internal string DisplayName { get; }

        /// <summary>Os números desta forma, ligados à seção própria dela no <c>.cfg</c>.</summary>
        internal SaiyaheimConfig.TransformationConfig Config { get; }

        /// <summary>
        /// Nome do objeto do status effect desta forma. É o nome do <c>UnityEngine.Object</c>, não
        /// o <c>m_name</c>: <c>StatusEffect.NameHash()</c> usa aquele.
        /// </summary>
        internal string ObjectName { get; }

        /// <summary>Hash pelo qual o <c>SEMan</c> identifica a forma. Cacheado: é o custo do lookup.</summary>
        internal int NameHashValue { get; }

        internal Skills.SkillType SkillType { get; private set; } = Skills.SkillType.None;

        internal bool IsRegistered => SkillType != Skills.SkillType.None;

        internal Transformation(string id, string displayName, SaiyaheimConfig.TransformationConfig config)
        {
            Id = id;
            DisplayName = displayName;
            Config = config;

            ObjectName = "SE_SaiyaheimForm_" + id;
            NameHashValue = ObjectName.GetStableHashCode();
        }

        /// <summary>
        /// Registra a skill de maestria desta forma. Chamado uma vez, do <c>Awake</c> do plugin,
        /// pelo <see cref="TransformationRegistry"/>.
        /// </summary>
        internal void Register()
        {
            // Ícone null é aceito pelo Jotunn: a skill aparece no menu sem arte própria.
            // Arte é polimento da etapa 11 e não bloqueia nada.
            SkillType = SkillManager.Instance.AddSkill(
                "saiyaheim.mastery." + Id,
                DisplayName,
                $"Mastery of the {DisplayName} form. Grows while you hold it, and every level " +
                "makes holding it cost less ki. It does not make the form stronger — that is " +
                "Battle Power's job.",
                increaseStep: 1f);

            SaiyaheimPlugin.Log.LogInfo($"Skill '{DisplayName}' (mastery) registered ({SkillType}).");
        }

        /// <summary>
        /// O jogador já destravou esta forma?
        ///
        /// Destravar é só Battle Power: a forma existe desde sempre e o que falta é o jogador
        /// chegar no nível. Ki e estado (morto, dormindo) <b>não</b> entram aqui — aquilo é
        /// "não posso agora", isto é "não posso ainda", e a tecla de ir direto ao topo precisa
        /// justamente da segunda pergunta.
        /// </summary>
        internal bool IsUnlocked(Player player)
        {
            if (player == null || !IsRegistered)
            {
                return false;
            }

            float required = Config.MinBattlePower.Value;

            return required <= 0f || PowerSkill.GetLevel(player) >= required;
        }

        /// <summary>Nível de maestria, 0–100.</summary>
        internal float GetSkillLevel(Player player)
        {
            if (player == null || !IsRegistered)
            {
                return 0f;
            }

            return player.GetSkillLevel(SkillType);
        }

        /// <summary>Nível normalizado em 0–1, que é como as fórmulas usam.</summary>
        internal float GetSkillFactor(Player player)
        {
            return GetSkillLevel(player) / 100f;
        }

        /// <summary>
        /// O multiplicador que a forma aplica sobre o power level de combate.
        ///
        /// Piso em 1: um multiplicador abaixo de 1 seria uma transformação que <b>enfraquece</b>,
        /// e o <c>.cfg</c> de um jogador não deve conseguir inverter o sentido da mecânica.
        /// </summary>
        internal float GetPowerMultiplier()
        {
            return Mathf.Max(1f, Config.PowerMultiplier.Value);
        }

        /// <summary>
        /// O dreno agora, já com a maestria descontada.
        ///
        /// <code>dreno = base * (1 - nivel/100 * reducao_no_100)</code>
        ///
        /// É a curva inteira da progressão da forma: no começo o jogador mal segura, depois vai
        /// dominando. A redução é linear porque a entrada é limitada — o fator de skill vive em
        /// 0–1 e o config em 0–0,95, então o resultado nunca chega a zero sozinho. (O voo precisa
        /// de uma forma hiperbólica para a redução vinda do poder justamente porque lá a entrada
        /// não tem teto; aqui tem.)
        /// </summary>
        internal float GetKiDrainPerSecond(Player player)
        {
            float reduction = Config.MasteryDrainReduction.Value * GetSkillFactor(player);

            return Mathf.Max(0f, Config.KiDrainPerSecond.Value * (1f - reduction));
        }

        /// <summary>
        /// XP de maestria por tempo segurando a forma. O chamador acumula os segundos e passa de
        /// uma vez — <c>RaiseSkill</c> a cada passo de física seriam ~50 chamadas por segundo pelo
        /// mesmo efeito.
        /// </summary>
        internal void RaiseMastery(Player player, float seconds)
        {
            // Ki desligado não acumula progressão do mod — é a regra do toggle. Na prática não dá
            // para chegar aqui com ele desligado (a forma cai junto), mas a regra vale igual.
            if (player == null || !IsRegistered || !Ki.KiManager.IsEnabled || seconds <= 0f)
            {
                return;
            }

            float xp = seconds * Config.MasteryXpPerSecond.Value;
            if (xp <= 0f)
            {
                return;
            }

            player.RaiseSkill(SkillType, xp);
        }
    }
}
