using Jotunn.Managers;
using Saiyaheim.Power;
using Saiyaheim.Util;
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

        /// <summary>
        /// Ignora as travas <b>desta forma</b>: a global key do boss e o <c>MinBattlePower</c>.
        /// Ligado só pelo <c>saiya_form &lt;forma&gt; unlock</c>, e só com <c>devcommands</c>.
        ///
        /// <b>Por que existe em vez de mandar usar o <c>setglobalkey</c> do jogo.</b> Aquele
        /// comando funciona, e testa o caminho real — mas escreve <c>defeated_eikthyr</c> no save
        /// do <b>mundo</b>, e a chave não é só do mod: ela controla raids e spawns do Valheim.
        /// Testar a trava sujaria o mundo de jogar. Este atalho não toca em nada de fora do mod.
        ///
        /// <b>É por forma, e não um interruptor geral</b>, porque a pergunta do playtest é sobre um
        /// degrau: "como é o SSJ2 antes do Bonemass" quer o SSJ2 aberto e o resto da escada como
        /// está. Um interruptor geral só sabe responder "tudo aberto", que é outra pergunta.
        ///
        /// <b>Não é persistido, de propósito.</b> Vive na memória e morre com o processo. Uma
        /// trava desligada que sobrevivesse ao restart seria um playtest mentindo em silêncio, e a
        /// mentira só apareceria muito depois. Pelo mesmo motivo o <c>saiya_form</c> avisa na
        /// primeira linha enquanto houver qualquer forma assim.
        /// </summary>
        internal bool IgnoreLocks { get; set; }

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
                $"Mastery of the {DisplayName} form. Grows while you hold it — or any form above " +
                "it — and every level makes holding it cost less ki. It does not make the form " +
                "stronger — that is Battle Power's job.",
                increaseStep: 1f);

            SaiyaheimPlugin.Log.LogInfo($"Skill '{DisplayName}' (mastery) registered ({SkillType}).");
        }

        /// <summary>
        /// O jogador já destravou esta forma?
        ///
        /// Destravar tem <b>duas</b> travas independentes, e as duas precisam estar abertas:
        /// o boss (<c>RequiredGlobalKey</c>, a trava da escada) e o nível de Battle Power
        /// (<c>MinBattlePower</c>, treino). Hoje só a primeira está em uso — a escada é ritmada por
        /// bosses, e exigir grind por cima ritmaria duas vezes a mesma progressão.
        ///
        /// Ki e estado (morto, dormindo) <b>não</b> entram aqui: aquilo é "não posso agora", isto é
        /// "não posso <i>ainda</i>", e a tecla de ir direto ao topo precisa justamente da segunda
        /// pergunta para saber a que forma ir.
        /// </summary>
        internal bool IsUnlocked(Player player)
        {
            return GetLockReason(player) == null;
        }

        /// <summary>
        /// O que falta para esta forma destravar, em uma frase, ou null se ela já está destravada.
        ///
        /// Existe separado do <see cref="IsUnlocked"/> porque as duas travas falham por motivos
        /// diferentes e o jogador precisa saber <b>qual</b>: "mata o Eikthyr" e "treina até o nível
        /// 20" mandam fazer coisas que não se parecem. A regra de desbloqueio continua morando num
        /// lugar só — o booleano é derivado daqui, e não o contrário.
        /// </summary>
        internal string GetLockReason(Player player)
        {
            if (player == null || !IsRegistered)
            {
                // Skill não registrada não é trava a burlar: é o mod carregado errado, e o
                // IgnoreLocks abaixo não deve esconder isso.
                return $"{DisplayName} is not available.";
            }

            if (IgnoreLocks)
            {
                return null;
            }

            // O boss primeiro: é a trava que o jogo inteiro usa para marcar progresso, e é a que
            // vai estar fechada na esmagadora maioria das vezes em que esta mensagem aparecer.
            string bossLock = BossGate.DescribeLock(Config.RequiredGlobalKey.Value);
            if (bossLock != null)
            {
                return bossLock;
            }

            float required = Config.MinBattlePower.Value;
            if (required > 0f && PowerSkill.GetLevel(player) < required)
            {
                return $"Battle Power {required:0} required for {DisplayName}.";
            }

            return null;
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
        /// Como esta forma reparte o soco: quanto da contusão vira corte e quanto vira raio, os
        /// dois em 0–1.
        ///
        /// <b>Converte, não soma.</b> O total do golpe é o mesmo — quem define a força da forma
        /// continua sendo o <see cref="GetPowerMultiplier"/> sozinho. O que muda é contra o que
        /// esse total bate: a armadura do Valheim é por tipo de dano, então um golpe partido em
        /// dois tipos é menos punido por um inimigo que resiste a um deles.
        ///
        /// <b>As duas frações saem daqui já somando no máximo 1</b>, e é por isso que elas são
        /// respondidas juntas em vez de uma por método. Cada chave sozinha respeita o intervalo
        /// 0–1, mas nada impede um <c>.cfg</c> de pedir 0,8 de corte e 0,8 de raio — e servir as
        /// duas ao pé da letra deixaria a contusão negativa, que <b>cura o alvo</b>. Quando a soma
        /// passa de 1, as duas encolhem na mesma proporção: a intenção de "muito corte, pouco
        /// raio" é preservada, e o que se perde é só o excesso.
        ///
        /// Clamp por cima do <c>AcceptableValueRange</c> pelo mesmo motivo: o intervalo é dica de
        /// UI, não garantia — um <c>.cfg</c> editado à mão passa por ele.
        /// </summary>
        internal void GetPunchSplit(out float slash, out float lightning)
        {
            slash = Mathf.Clamp01(Config.PunchSlashFraction.Value);
            lightning = Mathf.Clamp01(Config.PunchLightningFraction.Value);

            float total = slash + lightning;
            if (total > 1f)
            {
                slash /= total;
                lightning /= total;
            }
        }

        /// <summary>Esta forma estala raios em volta do corpo enquanto está ativa?</summary>
        internal bool HasLightning => Config.LightningEnabled.Value;

        /// <summary>
        /// A cor dos raios desta forma. Vazio na chave própria cai na cor da aura — a forma tem
        /// uma cor só, e repeti-la em duas chaves seria mais uma coisa a manter em sincronia.
        ///
        /// Vazio nas <b>duas</b> devolve vazio, que o <c>AttachedEffect</c> lê como <i>não tinja</i>
        /// e deixa o prefab com a cor original. Isso é diferente de cair numa cor padrão: quem
        /// apaga as duas chaves está pedindo o raio cru do jogo.
        /// </summary>
        internal string GetLightningColor()
        {
            string own = Config.LightningColor.Value;

            return string.IsNullOrEmpty(own) ? Config.AuraColor.Value : own;
        }

        /// <summary>
        /// Quanto peso a mais o inventário aguenta enquanto esta forma está ativa.
        ///
        /// <b>Não passa pelo <see cref="GetPowerMultiplier"/>, e não escala com maestria.</b> A
        /// força da forma é uma coisa; quanto ela carrega é outra, e as duas não têm por que andar
        /// juntas — o limite de peso é logística, não combate. A maestria também fica de fora: a
        /// moeda dela é o dreno, e só ela (ver <see cref="GetKiDrainPerSecond"/>).
        ///
        /// Piso em zero: um bônus negativo seria uma forma que <b>reduz</b> a mochila, e o
        /// <c>.cfg</c> de um jogador não deve conseguir inverter o sentido da mecânica — mesma
        /// regra do multiplicador de poder.
        /// </summary>
        internal float GetCarryWeightBonus()
        {
            return Mathf.Max(0f, Config.CarryWeightBonus.Value);
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
        ///
        /// <b>Isto é o pagamento de uma forma só.</b> Segurar um degrau treina também os degraus
        /// abaixo dele, mas essa regra é da escada e mora no
        /// <see cref="TransformationRegistry.RaiseMastery"/> — o normal é chamar por lá, não aqui.
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
