using BepInEx.Configuration;
using Jotunn.Configs;
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
    /// graça. Ela é a maestria — sobe <b>lutando dentro da forma</b> (desde 2026-09-20; antes era
    /// por tempo segurando) e paga em dreno menor e, principalmente, em custo de ki por golpe mais
    /// barato dentro daquela forma (<c>BattlePower.FormCostPayback</c>).
    ///
    /// <b>Não confundir com Power Level.</b> Power Level é uma só, global, e mede quanto o
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
        /// Ignora a trava <b>desta forma</b>: a global key do boss.
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
            SkillType = SkillManager.Instance.AddSkill(new SkillConfig
            {
                Identifier = "saiyaheim.mastery." + Id,
                Name = DisplayName,
                Description = $"Mastery of the {DisplayName} form. Grows while you hold it — or any form " +
                              "above it — and every level makes holding it cost less ki. It does not make " +
                              "the form stronger — that is Power Level's job.",
                IncreaseStep = 1f,
                Icon = IconLoader.Load(Id),
            });

            SkillCommandAliases.Register(DisplayName);

            SaiyaheimPlugin.Log.LogInfo($"Skill '{DisplayName}' (mastery) registered ({SkillType}).");
        }

        /// <summary>
        /// O jogador já destravou esta forma?
        ///
        /// A única trava é o boss (<c>RequiredGlobalKey</c>). Existiu uma segunda, por nível de
        /// Power Level (<c>MinPowerLevel</c>), que nunca saiu de 0 e foi removida em 2026-10-03:
        /// a escada é ritmada por bosses, e exigir grind por cima ritmaria duas vezes a mesma
        /// progressão.
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
        /// Existe separado do <see cref="IsUnlocked"/> porque o jogador precisa saber o que fazer
        /// ("mata o Eikthyr"), não só que está travado. A regra de desbloqueio continua morando num
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
        /// O multiplicador que a forma aplica sobre o battle power de combate.
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
        internal bool HasLightning => Config.LightningEnabled;

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
            string own = Config.LightningColor;

            return string.IsNullOrEmpty(own) ? Config.AuraColor : own;
        }

        /// <summary>
        /// Quanto esta forma brilha, como multiplicador da regulagem compartilhada
        /// (<c>FormGlowIntensity</c>). 0 apaga o brilho só nesta forma.
        ///
        /// <b>É intensidade por forma e não só um booleano</b>, ao contrário do raio, porque aqui
        /// a escada tem um degrau contínuo a subir: o raio ou estala ou não estala, mas o brilho
        /// de um degrau alto é o mesmo brilho <i>mais forte</i>. Deixar a diferença em uma chave
        /// numérica é o que permite ver de longe em que forma alguém está sem contar raios.
        ///
        /// Piso em zero: brilho negativo não existe, e uma luz de intensidade negativa <b>escurece
        /// o cenário</b> em vez de não fazer nada.
        /// </summary>
        internal float GetGlowIntensity()
        {
            return Mathf.Max(0f, Config.GlowIntensity);
        }

        /// <summary>
        /// A cor do brilho desta forma. Vazio na chave própria cai na cor da aura, que é o caso
        /// normal — mesma regra do <see cref="GetLightningColor"/>, e pelo mesmo motivo: a forma
        /// tem uma cor só, e repeti-la em três chaves seria mais coisa a manter em sincronia.
        ///
        /// A diferença é o que acontece quando as <b>duas</b> estão vazias: lá isso vira "não
        /// tinja o prefab", aqui não há prefab a preservar. Quem decide é o <c>FormGlow</c>.
        /// </summary>
        internal string GetGlowColor()
        {
            string own = Config.GlowColor;

            return string.IsNullOrEmpty(own) ? Config.AuraColor : own;
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
        /// Quanto da sobretaxa de combate compartilhada (<c>CombatFormKiShare</c>) esta forma
        /// cobra. 1 cobra a sobretaxa inteira; o SSJ God cobra menos, que é o que ele compra no
        /// lugar de um golpe maior. Ver <c>BattlePower.FormKiCostMultiplier</c>.
        /// </summary>
        /// <summary>
        /// Quantas vezes mais rápido o relógio da cura passiva anda nesta forma. 1 deixa o
        /// relógio da vanilla em paz, que é o caso de toda forma menos o SSJ God — as outras nem
        /// têm a chave, e a entrada chega null.
        /// </summary>
        internal float GetHealthRegenSpeed()
        {
            ConfigEntry<float> entry = Config.HealthRegenSpeed;

            return entry == null ? 1f : Mathf.Max(1f, entry.Value);
        }

        /// <summary>
        /// Esta forma cura através de Molhado, Frio e Congelando? False em toda forma que não tem
        /// a chave — hoje todas menos o SSJ God.
        /// </summary>
        internal bool GetHealthRegenIgnoresBlockers()
        {
            ConfigEntry<bool> entry = Config.HealthRegenIgnoresBlockers;

            return entry != null && entry.Value;
        }

        /// <summary>
        /// Piso da cura passiva desta forma, em vida por segundo. Zero em toda forma que não tem a
        /// chave — hoje todas menos o SSJ God.
        /// </summary>
        internal float GetHealthRegenMinimum()
        {
            ConfigEntry<float> entry = Config.HealthRegenMinimum;

            return entry == null ? 0f : Mathf.Max(0f, entry.Value);
        }

        /// <summary>
        /// Resistência a contusão desta forma. Normal em toda forma que não tem a chave — hoje
        /// todas menos o SSJ God. Só os degraus de resistência passam: o <c>.cfg</c> aceita
        /// qualquer valor do enum, e Weak ou Immune inverteriam ou quebrariam a mecânica.
        /// </summary>
        internal HitData.DamageModifier GetBluntResistance()
        {
            ConfigEntry<HitData.DamageModifier> entry = Config.BluntResistance;

            if (entry == null)
            {
                return HitData.DamageModifier.Normal;
            }

            switch (entry.Value)
            {
                case HitData.DamageModifier.SlightlyResistant:
                case HitData.DamageModifier.Resistant:
                case HitData.DamageModifier.VeryResistant:
                    return entry.Value;
                default:
                    return HitData.DamageModifier.Normal;
            }
        }

        /// <summary>
        /// Níveis de skill Kaioken que esta forma soma ao seguro de todo tier, agora (etapa 14).
        ///
        /// <code>penalidade = inicial × (1 − maestria / maestria em que zera)</code>, com piso em 0.
        ///
        /// É a única ponte entre forma e Kaioken. A maestria só tira o que a forma soma: com ela
        /// no topo, o Kaioken nesta forma custa o mesmo que na base, nunca menos.
        /// </summary>
        internal float GetKaiokenPenalty(Player player)
        {
            float initial = Mathf.Max(0f, Config.KaiokenPenalty.Value);
            float zeroAt = Mathf.Max(1f, Config.KaiokenPenaltyZeroAtMastery.Value);

            return initial * Mathf.Max(0f, 1f - GetSkillLevel(player) / zeroAt);
        }

        internal float GetCombatKiCostScale()
        {
            return Mathf.Max(0f, Config.CombatKiCostScale.Value);
        }

        /// <summary>
        /// O dreno agora, já com a maestria descontada.
        ///
        /// <code>dreno = base * (1 - nivel/100 * reducao_no_100)</code>
        ///
        /// É a curva inteira da progressão da forma: no começo o jogador mal segura, depois vai
        /// dominando. A redução é linear porque a entrada é limitada — o fator de skill vive em
        /// 0–1 e o config em 0–1, então a conta é fechada nas duas pontas. (O voo precisa de uma
        /// forma hiperbólica para a redução vinda do poder justamente porque lá a entrada não tem
        /// teto; aqui tem.)
        ///
        /// <b>Com o config em 1, o nível 100 devolve exatamente zero</b> — a forma para de custar
        /// ki e a regeneração passiva volta a correr por baixo dela (o <c>KiManager.Drain</c>
        /// ignora dreno zero de propósito). É o default e o ponto de chegada da maestria: maxar
        /// uma forma é passar a vestir ela de graça, não a pagar menos por ela.
        /// </summary>
        internal float GetKiDrainPerSecond(Player player)
        {
            float reduction = Config.MasteryDrainReduction.Value * GetSkillFactor(player);

            return Mathf.Max(0f, Config.KiDrainPerSecond.Value * (1f - reduction));
        }

        /// <summary>
        /// XP de maestria por um golpe trocado dentro da forma — causado
        /// (<paramref name="dealt"/>) ou sofrido.
        ///
        /// <b>Por dano e não por tempo</b>, desde 2026-09-20. Segurar a forma parado era o segundo
        /// grind que o feedback público relatou, e o rework que derrubou o dreno o tornaria
        /// <i>mais</i> rentável, não menos: forma barata de manter + XP por segundo é um convite a
        /// ficar parado em SSJ. Agora a forma só treina quando está sendo usada para o que ela
        /// serve. Voar transformado também não paga nada — quem quer baratear a forma luta dentro
        /// dela.
        ///
        /// <code>min(dano × taxa, grampo) × compensação de curva × multiplicador de boss</code>
        ///
        /// <b>O grampo entra antes dos dois multiplicadores</b>, e não depois como o do Power
        /// Level: a compensação corrige o fim da curva (<see cref="GetCurveXpMultiplier"/>) e o
        /// bônus de boss corrige o degrau velho que ficou para trás
        /// (<see cref="GetBossXpMultiplier"/>). Deixar o grampo comê-los anularia a correção
        /// justamente onde ela existe para fazer efeito.
        ///
        /// As chaves de XP são as mesmas para todas as formas, desde 2026-09-28. O que é desta
        /// forma é o <b>nível</b> que entra na compensação de curva.
        ///
        /// <b>Isto é o pagamento de uma forma só.</b> O golpe treina também os degraus abaixo, mas
        /// essa regra é da escada e mora no
        /// <see cref="TransformationRegistry.RaiseMasteryFromDamage"/> — o normal é chamar por lá.
        /// </summary>
        internal void RaiseMasteryFromDamage(Player player, float applied, bool dealt)
        {
            // Ki desligado não acumula progressão do mod — é a regra do toggle. Na prática não dá
            // para chegar aqui com ele desligado (a forma cai junto), mas a regra vale igual.
            if (player == null || !IsRegistered || !Ki.KiManager.IsEnabled || applied <= 0f)
            {
                return;
            }

            float rate = dealt
                ? SaiyaheimConfig.MasteryXpPerDamageDealt.Value
                : SaiyaheimConfig.MasteryXpPerDamageTaken.Value;

            if (rate <= 0f)
            {
                return;
            }

            float xp = Mathf.Min(applied * rate, SaiyaheimConfig.MasteryXpMaxPerEvent.Value)
                       * GetCurveXpMultiplier(player)
                       * GetBossXpMultiplier();

            if (xp <= 0f)
            {
                return;
            }

            player.RaiseSkill(SkillType, xp);
        }

        /// <summary>
        /// Quanto do custo crescente dos níveis do Valheim o XP de maestria devolve, no nível em
        /// que <b>esta</b> forma está.
        ///
        /// <code>custo do próximo nível ^ MasteryXpCurveCompensation</code>
        ///
        /// <b>Por que existe.</b> O XP pago é linear no dano, e o custo de cada nível cresce como
        /// <c>(nível+1)^1,5</c>. Os três cortes da taxa até 2026-09-21 acertaram o começo e
        /// afundaram o fim: o SSJ no nível 66 pedia 399 socos por nível. Com o expoente em 0,8
        /// (playtest de 2026-09-28) o custo em golpes continua subindo, só que como
        /// <c>req^0,2</c> em vez da curva inteira. Ver [[Melhorias]], "Maestria das formas trava no meio da curva".
        ///
        /// <b>O nível é desta forma</b>, não o da ativa: um golpe em SSJ2 paga o SSJ também, e o
        /// SSJ no 80 tem de receber a compensação do 80. É o <c>GetSkillLevel</c>, que já vem
        /// sem fração — a mesma conta do <c>Skills.Skill.GetNextLevelRequirement</c>, que é
        /// privado e por isso foi copiada aqui.
        /// </summary>
        internal float GetCurveXpMultiplier(Player player)
        {
            // Mesmo guarda do GetBossXpMultiplier, pelo mesmo motivo: perder a compensacao e'
            // invisivel e recuperavel, travar o personagem nao.
            if (SaiyaheimConfig.MasteryXpCurveCompensation == null)
            {
                return 1f;
            }

            float exponent = SaiyaheimConfig.MasteryXpCurveCompensation.Value;
            if (exponent <= 0f)
            {
                return 1f;
            }

            return Mathf.Pow(NextLevelRequirement(GetSkillLevel(player)), exponent);
        }

        /// <summary>
        /// XP que o nível <paramref name="level"/> custa para subir, copiado do
        /// <c>Skills.Skill.GetNextLevelRequirement</c> (privado). Muda se o Valheim mudar a curva.
        /// </summary>
        internal static float NextLevelRequirement(float level)
        {
            return Mathf.Pow(Mathf.Floor(level + 1f), 1.5f) * 0.5f + 0.5f;
        }

        /// <summary>
        /// Quanto o XP de maestria desta forma está acelerado <b>agora</b>, pelo que o mundo já
        /// derrubou depois do boss que a destravou.
        ///
        /// <code>min(teto, 1 + passo × (bosses derrotados − degrau desta forma))</code>
        ///
        /// <b>Por que existe.</b> A curva de XP do Valheim é a mesma para todo degrau, então a
        /// forma destravada primeiro é sempre a que está mais longe na ponta cara da curva — e ela
        /// engatinha exatamente quando uma forma mais forte acabou de fazê-la parecer inútil.
        /// Aqui o degrau velho treina mais rápido quanto mais o mundo andou além dele, que também
        /// é a leitura que faz sentido na ficção: aquela forma é trivial para você agora.
        ///
        /// <b>Sem estado novo.</b> É função pura do mundo (as global keys) mais config, recalculada
        /// a cada pagamento. Não há evento de "boss morreu" para escutar, nada para serializar e
        /// nada que se perca por estar offline na hora — e o servidor sincroniza a entrada de
        /// graça, que é a mesma razão pela qual a trava de desbloqueio usa global key.
        ///
        /// <b>Piso em 1</b>: enquanto o boss desta forma é a morte mais recente, ela paga a taxa
        /// cheia e nada mais. Chave desconhecida (Rainha, Fader) também fica em 1, e não no bônus
        /// máximo — errar para menos numa chave que este build não sabe posicionar é o lado seguro.
        /// </summary>
        internal float GetBossXpMultiplier()
        {
            // ⚠️ Entrada não ligada devolve 1 em vez de estourar, e isto não é paranoia: quem chama
            // é o RaiseMastery, que roda dentro do SEMan.Update a cada FixedUpdate. Uma exceção
            // aqui aborta o Humanoid.CustomFixedUpdate inteiro e o jogador para de andar, de voar e
            // de trocar de forma — foi exatamente o que aconteceu em 2026-09-06, quando a
            // propriedade existia mas o config.Bind dela tinha ficado de fora. O compilador não
            // pega isso: ConfigEntry nulo só falha em runtime.
            //
            // Perder o multiplicador é invisível e recuperável; travar o personagem, não. Quando as
            // duas falhas são desse tamanho, a silenciosa é a certa — e o saiya_form imprime o
            // multiplicador justamente para que ela não fique escondida.
            if (SaiyaheimConfig.MasteryXpPerBossBonus == null || Config.RequiredGlobalKey == null)
            {
                return 1f;
            }

            float step = SaiyaheimConfig.MasteryXpPerBossBonus.Value;
            if (step <= 0f)
            {
                return 1f;
            }

            int rung = BossGate.LadderIndex(Config.RequiredGlobalKey.Value);
            if (rung < 0)
            {
                return 1f;
            }

            // O degrau é índice base zero e a contagem é base um: a forma do primeiro boss está no
            // índice 0 e vale x1 com um boss morto, x2 com dois. Daí o "rung + 1".
            int ahead = BossGate.DefeatedCount() - (rung + 1);

            if (ahead <= 0)
            {
                return 1f;
            }

            // Teto: o bônus corrige o atraso do degrau velho, não acelera sem limite. Entrada não
            // ligada cai no comportamento sem teto, pelo mesmo motivo do guarda lá em cima.
            float multiplier = 1f + step * ahead;
            return SaiyaheimConfig.MasteryXpBossMultiplierMax == null
                ? multiplier
                : Mathf.Min(multiplier, Mathf.Max(1f, SaiyaheimConfig.MasteryXpBossMultiplierMax.Value));
        }
    }
}
