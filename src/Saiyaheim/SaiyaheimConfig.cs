using BepInEx.Configuration;
using UnityEngine;

namespace Saiyaheim
{
    /// <summary>
    /// Alinhamento horizontal de um texto da HUD. Três valores em vez do
    /// <c>HorizontalAlignmentOptions</c> do TMP, que traz junto <c>Justified</c>, <c>Flush</c> e
    /// <c>Geometry Center</c> — opções que não querem dizer nada para uma linha de texto curta e
    /// que só sujariam a lista de valores aceitos no <c>.cfg</c>.
    /// </summary>
    public enum HudTextAlign
    {
        Left,
        Center,
        Right,
    }

    /// <summary>
    /// Quanto de um efeito de impacto a cor do ataque pinta.
    ///
    /// <b>São duas leituras diferentes do mesmo pedido</b>, e só quem está olhando a tela decide
    /// qual é a certa: a luz sozinha conserta o vazamento de cor do prefab — o estouro do xamã
    /// goblin acende rosa no terreno em volta — sem tocar no desenho do estouro, enquanto pintar
    /// tudo faz o impacto inteiro virar cor de ki, o que combina mais mas apaga a variação de tom
    /// que o efeito trazia de fábrica.
    /// </summary>
    public enum ImpactTintTarget
    {
        /// <summary>Só a luz dinâmica. É o que tira o rosa do chão sem mexer no resto.</summary>
        Light,

        /// <summary>Luz, partículas, rastros e materiais — o estouro inteiro na cor do ataque.</summary>
        Everything,
    }

    /// <summary>
    /// Onde no corpo do jogador um efeito se prende. Ver <c>Util/BodyAnchor.cs</c>.
    /// </summary>
    public enum EffectAnchor
    {
        /// <summary>O transform do jogador. O offset é medido a partir dos pés.</summary>
        Body,

        /// <summary>O osso da mão direita. O offset é medido a partir da palma.</summary>
        RightHand,

        /// <summary>O osso da mão esquerda.</summary>
        LeftHand,
    }

    /// <summary>
    /// Toda a configuração do mod em um lugar só.
    ///
    /// Regra do projeto: <b>nenhum número de balanceamento hardcoded no código.</b> Sem isso,
    /// cada ajuste vira recompilação e o ciclo de playtest morre — e playtest é a única
    /// fonte de verdade sobre esses valores.
    ///
    /// Os defaults abaixo são chutes iniciais para o jogo abrir, não valores calibrados.
    ///
    /// Seções numeradas para o ConfigurationManager exibir na ordem certa.
    ///
    /// Texto voltado ao jogador (nomes de seção, descrições) fica em inglês; comentários e
    /// documentação de código continuam em português.
    /// </summary>
    public static class SaiyaheimConfig
    {
        private const string SecGeral = "1 - General";
        private const string SecKi = "2 - Ki";
        private const string SecCombat = "2.1 - Combat";

        /// <summary>
        /// Uma seção por forma. Não existe seção "3 - Transformations" genérica de propósito:
        /// <b>não há número compartilhado entre formas</b>. Multiplicador, dreno e maestria são
        /// da forma, e uma escada de cinco formas ([[Progressão por Bosses]]) precisa que cada
        /// degrau seja calibrável sozinho. Adicionar a segunda forma é copiar este bloco.
        /// </summary>
        private const string SecSsj = "3.1 - SSJ";

        /// <summary>
        /// O segundo degrau. Seção própria e não uma variante da anterior pelo mesmo motivo:
        /// <b>nenhum número é compartilhado entre formas</b>. Ver <see cref="SecSsj"/>.
        /// </summary>
        private const string SecSsj2 = "3.2 - SSJ2";

        /// <summary>
        /// O terceiro degrau. Seção própria pelo mesmo motivo dos anteriores. Ver
        /// <see cref="SecSsj"/>.
        /// </summary>
        private const string SecSsj3 = "3.3 - SSJ3";

        /// <summary>
        /// O quarto degrau. Seção própria pelo mesmo motivo dos anteriores. Ver
        /// <see cref="SecSsj"/>.
        /// </summary>
        private const string SecSsjGod = "3.4 - SSJ God";

        // A seção "4 - Ki Attacks", que valia para todos os ataques, ficou vazia em 2026-09-13:
        // o piso entre disparos e as três chaves de mira eram tudo o que ela tinha, e nenhuma era
        // balanceamento. As duas teclas seguem na seção 1, e o que é de um ataque só segue nas
        // 4.1 e 4.2.

        /// <summary>
        /// Uma seção por ataque, pelo mesmo motivo das formas: <b>não há número compartilhado
        /// entre ataques</b>. Dano, custo, cooldown e projétil são do ataque, e uma escada de
        /// ataques ([[Ataques de Ki]]) precisa que cada um seja calibrável sozinho.
        /// </summary>
        private const string SecKiBlast = "4.1 - Ki Blast";

        /// <summary>
        /// O segundo degrau da escada, atrás do Bonemass. Seção própria pelo mesmo motivo do
        /// <see cref="SecKiBlast"/>: nenhum número é compartilhado entre ataques.
        /// </summary>
        private const string SecKamehameha = "4.2 - Kamehameha";

        private const string SecFlight = "5 - Flight";
        private const string SecPower = "6 - Battle Power";
        private const string SecPowerSkill = "6.1 - Power Level";
        private const string SecHud = "7 - HUD";

        // As seções "8 - Effects" e "10 - Multiplayer" deixaram de existir em 2026-09-15: tudo o
        // que estava nelas era visual já calibrado e virou constante. Ver a seção 8 mais abaixo.
        private const string SecDebug = "9 - Debug";

        // ---------- 1 - General ----------

        /// <summary>Tecla que liga/desliga o ki. Client-side: cada um usa a que quiser.</summary>
        public static ConfigEntry<KeyboardShortcut> ToggleKiKey { get; private set; }

        /// <summary>Estado do ki para um personagem novo, antes de qualquer toggle.</summary>
        public static ConfigEntry<bool> KiEnabledByDefault { get; private set; }

        /// <summary>Tecla segurada para carregar ki ativamente.</summary>
        public static ConfigEntry<KeyboardShortcut> ChargeKiKey { get; private set; }

        /// <summary>Tecla que decola e pousa. Client-side, como as outras.</summary>
        public static ConfigEntry<KeyboardShortcut> ToggleFlightKey { get; private set; }

        /// <summary>Tecla que vai direto à forma mais alta já destravada.</summary>
        public static ConfigEntry<KeyboardShortcut> TransformKey { get; private set; }

        /// <summary>Tecla que volta direto à forma base, de qualquer degrau.</summary>
        public static ConfigEntry<KeyboardShortcut> PowerDownKey { get; private set; }

        /// <summary>Tecla que sobe um degrau na escada de formas.</summary>
        public static ConfigEntry<KeyboardShortcut> TransformStepUpKey { get; private set; }

        /// <summary>Tecla que desce um degrau. Do primeiro, volta à base.</summary>
        public static ConfigEntry<KeyboardShortcut> TransformStepDownKey { get; private set; }

        /// <summary>Tecla que dispara o ataque de ki selecionado.</summary>
        public static ConfigEntry<KeyboardShortcut> FireKiAttackKey { get; private set; }

        /// <summary>Tecla que troca de ataque, entre os destravados.</summary>
        public static ConfigEntry<KeyboardShortcut> CycleKiAttackKey { get; private set; }

        /// <summary>Bater duas vezes no botão de pulo decola.</summary>
        public static ConfigEntry<bool> FlightTakeOffOnDoubleJump { get; private set; }

        /// <summary>Segundos entre os dois toques para contar como toque duplo.</summary>
        public static ConfigEntry<float> FlightDoubleJumpWindow { get; private set; }

        // ---------- 2 - Ki ----------

        /// <summary>Ki máximo no nível 0 da skill de poder. O teto cresce a partir daqui.</summary>
        public static ConfigEntry<float> MaxKi { get; private set; }

        /// <summary>Ki máximo somado por nível da skill Power Level.</summary>
        public static ConfigEntry<float> MaxKiPerPowerLevel { get; private set; }

        public static ConfigEntry<float> KiRegenPerSecond { get; private set; }

        /// <summary>Regeneração passiva somada por ponto de battle power. Mantém a torneira crescendo junto com a barra.</summary>
        public static ConfigEntry<float> KiRegenFromPower { get; private set; }

        /// <summary>Segundos sem regenerar depois de gastar ki.</summary>
        public static ConfigEntry<float> KiRegenDelay { get; private set; }

        /// <summary>Multiplicador da regeneração passiva enquanto o jogador está com o buff Rested do jogo.</summary>
        public static ConfigEntry<float> KiRegenRestedMultiplier { get; private set; }

        /// <summary>Multiplicador do <see cref="KiRegenDelay"/> enquanto o jogador está com o buff Rested.</summary>
        public static ConfigEntry<float> KiRegenDelayRestedMultiplier { get; private set; }

        /// <summary>Ki por segundo enquanto a tecla de carregar está segurada.</summary>
        public static ConfigEntry<float> ChargeKiPerSecond { get; private set; }

        /// <summary>Carregamento ativo somado por ponto de battle power.</summary>
        public static ConfigEntry<float> ChargeKiFromPower { get; private set; }

        /// <summary>Se true, andar interrompe o carregamento.</summary>
        public static ConfigEntry<bool> ChargeRequiresStandingStill { get; private set; }

        /// <summary>Ki ganho num parry, em socos: múltiplos do custo de ki de um soco agora.</summary>
        public static ConfigEntry<float> KiOnParryPunches { get; private set; }

        /// <summary>Ki ganho ao matar, em socos: múltiplos do custo de ki de um soco agora.</summary>
        public static ConfigEntry<float> KiOnKillPunches { get; private set; }

        // ---------- 2.1 - Combat ----------

        /// <summary>Ki gasto por ponto de dano que o poder somou ao soco. Ki insuficiente não cancela o golpe, só tira o bônus.</summary>
        public static ConfigEntry<float> PunchKiCostPerDamage { get; private set; }

        /// <summary>
        /// Taxa do desconto hiperbólico que o poder de combate dá no custo de ki do <b>soco</b>.
        /// 0 desliga. Ver <c>BattlePower.KiCostFactorFor</c>.
        ///
        /// Valia para os três custos do combate até 2026-09-20; apanhar e bloquear passaram a ter
        /// a chave própria abaixo.
        /// </summary>
        public static ConfigEntry<float> KiCostPowerReduction { get; private set; }

        /// <summary>
        /// Desconto de poder nos custos de ki da <b>defesa</b> (apanhar e bloquear). 0 = sem
        /// desconto, que é o default. Ver <c>BattlePower.GetDefenseKiCostFactor</c>.
        /// </summary>
        public static ConfigEntry<float> DefenseKiCostPowerReduction { get; private set; }

        /// <summary>
        /// Quanto do ganho de poder da forma vira custo de ki a mais nos três custos de combate.
        /// 1 = proporcional; 0,2 (default) cobra um quinto da sobretaxa.
        /// Ver <c>BattlePower.FormKiCostMultiplier</c>.
        /// </summary>
        public static ConfigEntry<float> CombatFormKiShare { get; private set; }

        /// <summary>
        /// Quanto do <b>acréscimo</b> de custo de combate que a forma cobra é devolvido pela
        /// maestria dela. Em 1, a forma maxada cobra pelo soco exatamente o que a base cobra.
        /// 0 desliga. Ver <c>BattlePower.KiCostFactorFor</c>.
        /// </summary>
        public static ConfigEntry<float> MasteryFormCostReduction { get; private set; }

        /// <summary>Fração do battle power somada ao dano do soco.</summary>
        public static ConfigEntry<float> PunchDamageFromPower { get; private set; }

        /// <summary>Armadura garantida com o ki ligado, antes da parcela vinda do poder.</summary>
        public static ConfigEntry<float> ArmorBase { get; private set; }

        /// <summary>Fração da armadura de ki que sobra com a barra zerada. Degrau, não degradê.</summary>
        public static ConfigEntry<float> ArmorFractionWithoutKi { get; private set; }

        /// <summary>Ki gasto por ponto de dano que a armadura de ki absorveu.</summary>
        public static ConfigEntry<float> DamageTakenKiCost { get; private set; }

        /// <summary>Fração do battle power convertida em armadura.</summary>
        public static ConfigEntry<float> ArmorFromPower { get; private set; }

        /// <summary>Block power garantido com o ki ligado, antes da parcela vinda do poder.</summary>
        public static ConfigEntry<float> BlockPowerBase { get; private set; }

        /// <summary>Fração do battle power convertida em block power. Substitui o do item equipado.</summary>
        public static ConfigEntry<float> BlockPowerFromPower { get; private set; }

        /// <summary>Ki gasto por ponto de dano que o bloqueio de ki barrou.</summary>
        public static ConfigEntry<float> BlockKiCost { get; private set; }

        // ---------- 7 - HUD ----------

        // Não há chave para esconder a barra de ki nem o poder de luta, e não é esquecimento:
        // saíram em 2026-09-13. Ki é o recurso central do mod e não tem outro leitor na tela, e
        // quem quer a tela limpa desliga o ki com o ToggleKiKey — os dois somem junto, que é a
        // regra do toggle. Quebra de runtime já tem desligamento automático nas duas classes.
        // O que sobrou em config é posição pura: offset e tamanho de fonte dependem da resolução
        // e da escala de UI da máquina de quem joga, então continuam sendo decisão de cada um.
        // Cor, rótulo e alinhamento saíram em 2026-09-15, junto com o resto do visual calibrado:
        // viraram as constantes logo abaixo. Ver [[Limpeza do .cfg]].
        public static ConfigEntry<float> KiBarOffsetX { get; private set; }
        public static ConfigEntry<float> KiBarOffsetY { get; private set; }

        /// <summary>Cor da barra de ki. Calibrada no playtest de 2026-07-28.</summary>
        public const string KiBarColor = "#4FC3F7";

        /// <summary>Deslocamento X do texto, relativo ao rótulo do bioma no minimapa.</summary>
        public static ConfigEntry<float> PowerHudOffsetX { get; private set; }

        /// <summary>Deslocamento Y do texto, relativo ao rótulo do bioma no minimapa.</summary>
        public static ConfigEntry<float> PowerHudOffsetY { get; private set; }

        /// <summary>Tamanho da fonte, em unidades de canvas.</summary>
        public static ConfigEntry<float> PowerHudFontSize { get; private set; }

        /// <summary>Texto antes do número. Fixado em 2026-09-15.</summary>
        public const string PowerHudLabel = "BP:";

        /// <summary>Cor do texto — o branco do rótulo do bioma, de onde o texto é clonado.</summary>
        public const string PowerHudColor = "#FFFFFF";

        /// <summary>Deslocamento X do texto, relativo ao nome do inimigo.</summary>
        public static ConfigEntry<float> EnemyPowerOffsetX { get; private set; }

        /// <summary>Deslocamento Y do texto, relativo ao nome do inimigo.</summary>
        public static ConfigEntry<float> EnemyPowerOffsetY { get; private set; }

        /// <summary>Tamanho da fonte do texto do inimigo, em unidades de canvas.</summary>
        public static ConfigEntry<float> EnemyPowerFontSize { get; private set; }

        /// <summary>Texto antes do numero, no rotulo do inimigo. Fixado em 2026-09-15.</summary>
        public const string EnemyPowerLabel = "BP:";

        /// <summary>Cor do texto do inimigo — o branco do nome que fica logo acima dele.</summary>
        public const string EnemyPowerColor = "#FFFFFF";

        /// <summary>
        /// Alinhamento horizontal do texto do inimigo dentro da largura do hud. Centralizado por
        /// playtest (2026-09-06): empilhado sob o nome, os dois leem como um rótulo só.
        /// </summary>
        public const HudTextAlign EnemyPowerAlign = HudTextAlign.Center;

        // ---------- 3.x - Transformations ----------

        /// <summary>
        /// Os números de <b>uma</b> forma. Uma instância por transformação, cada uma na sua seção
        /// do <c>.cfg</c> — ver <see cref="BindTransformation"/>.
        ///
        /// A maestria mora aqui dentro e não numa seção própria porque ela é <b>por forma</b>: não
        /// existe "a skill de maestria", existe a skill de Super Saiyan. Ver <c>Transformation</c>.
        /// </summary>
        public class TransformationConfig
        {
            /// <summary>Multiplicador do battle power de combate enquanto a forma está ativa.</summary>
            public ConfigEntry<float> PowerMultiplier { get; internal set; }

            /// <summary>Dreno base por segundo, antes da redução por maestria.</summary>
            public ConfigEntry<float> KiDrainPerSecond { get; internal set; }

            /// <summary>
            /// Fração do dano de contusão do soco convertida em corte enquanto a forma está ativa.
            /// Converte, não soma: o total do golpe não muda.
            /// </summary>
            public ConfigEntry<float> PunchSlashFraction { get; internal set; }

            /// <summary>
            /// Fração do dano de contusão do soco convertida em <b>raio</b> enquanto a forma está
            /// ativa. Mesma regra do corte acima: move, não soma. As duas frações repartem o mesmo
            /// total, e a soma delas é normalizada se passar de 1.
            /// </summary>
            public ConfigEntry<float> PunchLightningFraction { get; internal set; }

            /// <summary>
            /// Peso máximo somado ao limite do inventário enquanto a forma está ativa. Soma ao
            /// limite base do jogador, e ao Megingjord se ele estiver equipado.
            /// </summary>
            public ConfigEntry<float> CarryWeightBonus { get; internal set; }

            /// <summary>
            /// Quantas vezes mais rápido o relógio da cura passiva anda enquanto a forma está
            /// ativa. <b>Null nas formas que não têm a chave</b>, como a acima. Ver
            /// <c>Transformations.HealthRegenPatch</c>.
            /// </summary>
            public ConfigEntry<float> HealthRegenSpeed { get; internal set; }

            /// <summary>
            /// Se a cura desta forma ignora o que corta ou zera a regeneração de vida — Molhado,
            /// Frio e Congelando. <b>Null nas formas que não têm a chave.</b> Ver
            /// <c>Transformations.HealthRegenFloorPatch</c>.
            /// </summary>
            public ConfigEntry<bool> HealthRegenIgnoresBlockers { get; internal set; }

            /// <summary>
            /// Fração da sobretaxa de combate compartilhada (<see cref="CombatFormKiShare"/>) que
            /// esta forma cobra. 1 cobra inteira. Ver <c>BattlePower.FormKiCostMultiplier</c>.
            /// </summary>
            public ConfigEntry<float> CombatKiCostScale { get; internal set; }

            /// <summary>Fração do dreno removida no nível 100 da skill desta forma.</summary>
            public ConfigEntry<float> MasteryDrainReduction { get; internal set; }

            /// <summary>XP da skill desta forma por ponto de dano causado dentro dela.</summary>
            public ConfigEntry<float> MasteryXpPerDamageDealt { get; internal set; }

            /// <summary>XP da skill desta forma por ponto de dano sofrido dentro dela.</summary>
            public ConfigEntry<float> MasteryXpPerDamageTaken { get; internal set; }

            /// <summary>Teto de XP de maestria de um único golpe, antes do multiplicador de boss.</summary>
            public ConfigEntry<float> MasteryXpMaxPerEvent { get; internal set; }

            /// <summary>
            /// Quanto o ganho de XP desta forma sobe por boss derrotado <b>depois</b> do boss que
            /// a destravou. 0 desliga. Ver <c>Transformation.GetBossXpMultiplier</c>.
            /// </summary>
            public ConfigEntry<float> MasteryXpPerBossBonus { get; internal set; }

            /// <summary>
            /// Teto do multiplicador de XP por boss. 1 desliga o bônus. Ver
            /// <c>Transformation.GetBossXpMultiplier</c>.
            /// </summary>
            public ConfigEntry<float> MasteryXpBossMultiplierMax { get; internal set; }

            /// <summary>Nível mínimo de Power Level para entrar na forma. 0 desliga a trava.</summary>
            public ConfigEntry<float> MinPowerLevel { get; internal set; }

            /// <summary>
            /// Global key do boss que destrava a forma. Vazio desliga a trava. Ver
            /// <c>Util.BossGate</c>.
            /// </summary>
            public ConfigEntry<string> RequiredGlobalKey { get; internal set; }

            /// <summary>
            /// Penteado usado enquanto a forma está ativa: <c>Spiked</c> para a versão espetada do
            /// cabelo do personagem, ou o nome de um item de customização — do jogo
            /// (<c>Hair1</c>..<c>Hair37</c>, <c>HairNone</c>) ou nosso (<c>SaiyaHair6</c>...).
            /// Vazio mantém o do personagem.
            /// </summary>
            public string HairItem { get; internal set; }

            /// <summary>Cor do cabelo enquanto a forma está ativa, em #RRGGBB. Vazio não pinta.</summary>
            public string HairColor { get; internal set; }

            /// <summary>Multiplicador de brilho da cor acima. Acima de 1 estoura e queima.</summary>
            public float HairColorIntensity { get; internal set; }

            /// <summary>Cor da aura desta forma, em #RRGGBB. Vazio mantém a cor do prefab.</summary>
            public string AuraColor { get; internal set; }

            /// <summary>
            /// Se esta forma estala raios em volta do corpo enquanto está ativa. É a única chave
            /// que decide <b>quais</b> formas crepitam; a regulagem do efeito é compartilhada, na
            /// seção 8 (<c>FormLightning*</c>).
            /// </summary>
            public bool LightningEnabled { get; internal set; }

            /// <summary>
            /// Cor dos raios desta forma, em #RRGGBB. Vazio cai na <see cref="AuraColor"/>, que é
            /// o caso normal — a forma tem uma cor só.
            /// </summary>
            public string LightningColor { get; internal set; }

            /// <summary>
            /// Quanto esta forma brilha, como multiplicador da regulagem compartilhada da seção 8
            /// (<c>FormGlow*</c>). 0 apaga o brilho só nesta forma.
            /// </summary>
            public float GlowIntensity { get; internal set; }

            /// <summary>
            /// Cor do brilho desta forma, em #RRGGBB. Vazio cai na <see cref="AuraColor"/>, que é
            /// o caso normal — mesma regra da <see cref="LightningColor"/>.
            /// </summary>
            public string GlowColor { get; internal set; }
        }

        /// <summary>
        /// O primeiro degrau da escada. Cada degrau novo é outra propriedade como esta, com seção
        /// própria — ver <see cref="BindTransformation"/>.
        /// </summary>
        public static TransformationConfig Ssj { get; private set; }

        /// <summary>O segundo degrau. Ver <see cref="Ssj"/>.</summary>
        public static TransformationConfig Ssj2 { get; private set; }

        /// <summary>O terceiro degrau. Ver <see cref="Ssj"/>.</summary>
        public static TransformationConfig Ssj3 { get; private set; }

        /// <summary>O quarto degrau. Ver <see cref="Ssj"/>.</summary>
        public static TransformationConfig SsjGod { get; private set; }

        // ---------- 4.x - Ki Attacks ----------

        // A mira assistida não tem config: a convergência, o alcance do raio e a trava de
        // correção viraram constantes em KiAim em 2026-09-13, e o piso entre disparos no
        // KiAttackRegistry. Nenhum dos quatro é balanceamento — são o conserto do paralelismo
        // entre a câmera e a mão, e a trava que o mantém honesto.

        /// <summary>
        /// Os números de <b>um</b> ataque de ki. Uma instância por ataque, cada uma na sua seção do
        /// <c>.cfg</c> — ver <see cref="BindKiAttack"/>.
        ///
        /// <b>Não há chave de tipo de dano.</b> O ki blast é contusão pura, por decisão de design
        /// de 2026-08-06: contusão staggera (fogo e gelo não), é o mesmo tipo do soco, e corte —
        /// que foi a primeira escolha — deixaria o ataque à distância fraco justamente contra
        /// morto-vivo, que é contra quem mais se quer atirar. Ver [[Ataques de Ki]].
        /// </summary>
        public class KiAttackConfig
        {
            /// <summary>Dano no battle power zero. O piso do ataque, antes da parcela do poder.</summary>
            public ConfigEntry<float> DamageBase { get; internal set; }

            /// <summary>Fração do battle power de combate somada ao dano.</summary>
            public ConfigEntry<float> DamageFromPower { get; internal set; }

            /// <summary>Ki gasto por disparo. Fixo: não escala com nada, de propósito.</summary>
            public ConfigEntry<float> KiCost { get; internal set; }

            /// <summary>Segundos até este ataque poder ser disparado de novo.</summary>
            public ConfigEntry<float> Cooldown { get; internal set; }

            /// <summary>Empurrão no alvo atingido.</summary>
            public ConfigEntry<float> Knockback { get; internal set; }

            /// <summary>Raio da explosão em metros. Zero = só o alvo acertado. Ver <c>KiProjectile.Defuse</c>.</summary>
            public ConfigEntry<float> ImpactRadius { get; internal set; }

            /// <summary>Prefab do projétil, do <c>ZNetScene</c>. Ver [[Prefabs do Jogo]].</summary>
            public string ProjectilePrefab { get; internal set; }

            /// <summary>
            /// O que toca onde o projétil bate. Vazio mantém o do prefab, <c>none</c> tira, um nome
            /// de prefab substitui. Ver [[Prefabs do Jogo]].
            /// </summary>
            public string ImpactEffect { get; internal set; }

            /// <summary>
            /// Nomes de emissores de partícula a tirar do efeito de impacto — a fumaça, tipicamente.
            /// É o corte fino que o <see cref="ImpactEffect"/> não faz. Ver
            /// <c>Util/StrippedEffect.cs</c>.
            /// </summary>
            public string ImpactEffectStrip { get; internal set; }

            /// <summary>
            /// Cor do efeito de impacto. Vazio segue o <c>ProjectileColor</c>, <c>none</c> mantém a
            /// do prefab, um hex manda. Ver <c>KiProjectile.ResolveImpactColor</c>.
            /// </summary>
            public string ImpactColor { get; internal set; }

            /// <summary>Quanto do efeito de impacto a <see cref="ImpactColor"/> pinta.</summary>
            public ImpactTintTarget ImpactColorTarget { get; internal set; }

            /// <summary>
            /// Deixa o projétil sobreviver ao próprio impacto, como o prefab queria. Desligado, ele
            /// some no acerto e não sobra rastro depois.
            /// </summary>
            public bool ProjectileLingerOnHit { get; internal set; }

            /// <summary>Velocidade do projétil em m/s.</summary>
            public ConfigEntry<float> ProjectileSpeed { get; internal set; }

            /// <summary>Segundos de vida do projétil. Alcance = velocidade x isto.</summary>
            public ConfigEntry<float> ProjectileLifetime { get; internal set; }

            /// <summary>Escala do projétil. 1 é o tamanho do prefab.</summary>
            public float ProjectileScale { get; internal set; }

            /// <summary>Cor do projétil, em #RRGGBB. Vazio mantém a cor do prefab.</summary>
            public string ProjectileColor { get; internal set; }

            /// <summary>
            /// Quantos projéteis um disparo solta. 1 é o tiro único do ki blast.
            ///
            /// É o que faz um feixe existir sem o jogo ter feixe: o próprio Yagluth encadeia
            /// <c>projectile_beam</c> em sequência, e o que se lê como raio contínuo é a fila de
            /// projéteis próximos demais para o olho separar. Ver <c>Attacks/KiBeam.cs</c>.
            /// </summary>
            public ConfigEntry<int> BeamCount { get; internal set; }

            /// <summary>Segundos entre um projétil e o seguinte do mesmo feixe.</summary>
            public ConfigEntry<float> BeamInterval { get; internal set; }

            /// <summary>
            /// Segundos de tecla segurada até a carga cheia. 0 desliga o carregamento: o ataque
            /// dispara no toque, com o feixe inteiro, que é o ki blast.
            /// </summary>
            public ConfigEntry<float> ChargeTime { get; internal set; }

            /// <summary>Fração mínima de carga que dispara. Abaixo dela, soltar cancela de graça.</summary>
            public ConfigEntry<float> MinChargeRatio { get; internal set; }

            /// <summary>Escala do projétil na carga mínima, como fração da escala na carga cheia.</summary>
            public float ChargeMinScale { get; internal set; }

            /// <summary>Efeito preso ao jogador enquanto ele carrega. Vazio não mostra nada.</summary>
            public string ChargeEffectPrefab { get; internal set; }

            /// <summary>Cor do efeito de carregamento. Vazio segue o <see cref="ProjectileColor"/>.</summary>
            public string ChargeEffectColor { get; internal set; }

            /// <summary>Escala do efeito de carregamento na carga cheia.</summary>
            public float ChargeEffectScale { get; internal set; }

            /// <summary>Altura do efeito de carregamento, a partir dos pés.</summary>
            public float ChargeEffectHeight { get; internal set; }

            /// <summary>Deslocamento lateral do efeito. Positivo é para a direita do jogador.</summary>
            public float ChargeEffectSide { get; internal set; }

            /// <summary>Deslocamento para frente do efeito.</summary>
            public float ChargeEffectForward { get; internal set; }

            /// <summary>
            /// Onde os efeitos de carregamento se prendem. Preso na mão, a animação os carrega.
            /// </summary>
            public EffectAnchor ChargeEffectAnchor { get; internal set; }

            /// <summary>
            /// A bola que junta na mão. Prefab de <b>projétil</b>, parado — ver
            /// <c>Util/StaticProp.cs</c>. Vazio não mostra nada.
            /// </summary>
            public string ChargeBallPrefab { get; internal set; }

            /// <summary>Cor da bola. Vazio segue o <see cref="ProjectileColor"/>.</summary>
            public string ChargeBallColor { get; internal set; }

            /// <summary>Tamanho da bola na carga cheia. Ela cresce da <see cref="ChargeMinScale"/> até aqui.</summary>
            public float ChargeBallScale { get; internal set; }

            /// <summary>Emissores a tirar da bola — o rastro que o projétil deixava ao voar.</summary>
            public string ChargeBallStrip { get; internal set; }

            /// <summary>Efeito que marca a carga cheia. Vazio não mostra nada.</summary>
            public string ChargeFullEffectPrefab { get; internal set; }

            /// <summary>Cor do efeito de carga cheia. Vazio segue o <see cref="ProjectileColor"/>.</summary>
            public string ChargeFullEffectColor { get; internal set; }

            /// <summary>Escala do efeito de carga cheia.</summary>
            public float ChargeFullEffectScale { get; internal set; }

            /// <summary>
            /// Segura o efeito de carga cheia enquanto o jogador continuar segurando, em vez de
            /// tocá-lo uma vez no instante em que a carga enche.
            /// </summary>
            public bool ChargeFullEffectLoop { get; internal set; }

            /// <summary>
            /// A carga cheia <b>substitui</b> o efeito de carregamento em vez de somar-se a ele.
            /// </summary>
            public bool ChargeFullEffectReplaces { get; internal set; }

            /// <summary>Nível mínimo de Power Level para usar o ataque. 0 desliga a trava.</summary>
            public ConfigEntry<float> MinPowerLevel { get; internal set; }

            /// <summary>Global key do boss que destrava o ataque. Vazio desliga a trava.</summary>
            public ConfigEntry<string> RequiredGlobalKey { get; internal set; }
        }

        /// <summary>
        /// O primeiro ataque da escada. O segundo é outra propriedade como esta, com seção própria
        /// — ver <see cref="BindKiAttack"/>.
        /// </summary>
        public static KiAttackConfig KiBlast { get; private set; }

        /// <summary>O segundo ataque da escada. Mesma forma do <see cref="KiBlast"/>, em feixe.</summary>
        public static KiAttackConfig Kamehameha { get; private set; }

        // ---------- 5 - Flight ----------

        public static ConfigEntry<float> FlightKiPerSecond { get; private set; }

        /// <summary>Multiplicador do custo de ki com o botão de correr segurado.</summary>
        public static ConfigEntry<float> FlightFastKiMultiplier { get; private set; }

        /// <summary>Multiplicador do custo de ki parado no ar, sem nenhum input de movimento.</summary>
        public static ConfigEntry<float> FlightHoverKiMultiplier { get; private set; }

        /// <summary>
        /// Quanto pairar parado custa a mais com inimigo alertado por perto. 1 desliga.
        /// Ver <c>FlightStats.GetKiCostPerSecond</c>.
        /// </summary>
        public static ConfigEntry<float> FlightCombatHoverMultiplier { get; private set; }

        /// <summary>Raio, em metros, em que um inimigo alertado conta para a sobretaxa acima.</summary>
        public static ConfigEntry<float> FlightCombatHoverRange { get; private set; }

        public static ConfigEntry<float> FlightBaseSpeed { get; private set; }

        /// <summary>Velocidade somada por ponto de battle power bruto.</summary>
        public static ConfigEntry<float> FlightSpeedFromPower { get; private set; }

        /// <summary>
        /// Quanto do ganho de poder da forma vira velocidade de voo: o fator aplicado é
        /// <c>1 + (PowerMultiplier - 1) × este valor</c>.
        /// </summary>
        public static ConfigEntry<float> FlightFormSpeedShare { get; private set; }

        /// <summary>Multiplicador da velocidade com o botão de correr segurado.</summary>
        public static ConfigEntry<float> FlightFastSpeedMultiplier { get; private set; }

        /// <summary>Componente vertical do movimento, como fração da velocidade horizontal.</summary>
        public static ConfigEntry<float> FlightVerticalSpeedFactor { get; private set; }

        /// <summary>Velocidade de giro no ar. Vai direto para <c>Character.m_flyTurnSpeed</c>.</summary>
        public static ConfigEntry<float> FlightTurnSpeed { get; private set; }

        /// <summary>Bônus de velocidade no nível 100 da skill de voo. 0.5 = +50%.</summary>
        public static ConfigEntry<float> FlightSpeedSkillBonus { get; private set; }

        /// <summary>Fração do custo de ki removida no nível 100 da skill de voo.</summary>
        public static ConfigEntry<float> FlightKiSkillReduction { get; private set; }

        /// <summary>
        /// Expoente da curva com que a redução da skill de voo chega ao valor do
        /// <see cref="FlightKiSkillReduction"/>. 1 = linear; acima disso a economia se concentra
        /// no topo da skill.
        /// </summary>
        public static ConfigEntry<float> FlightKiSkillCurve { get; private set; }

        /// <summary>Fração do dano de queda removida no nível 100 da skill de voo, com o ki ligado. Linear.</summary>
        public static ConfigEntry<float> FlightFallDamageSkillReduction { get; private set; }

        /// <summary>
        /// Barateamento hiperbólico do voo vindo do termo de fim de jogo. É a única coisa do voo
        /// que esse termo toca — velocidade fica de fora.
        /// </summary>
        public static ConfigEntry<float> FlightKiPowerReduction { get; private set; }

        /// <summary>XP da skill de voo por metro percorrido no ar.</summary>
        public static ConfigEntry<float> FlightXpPerMeter { get; private set; }

        /// <summary>
        /// Quanto a forma ativa encarece o voo, como fração do ganho de poder dela. A maestria da
        /// forma devolve isso até zerar no nível 100.
        /// </summary>
        public static ConfigEntry<float> FlightFormKiShare { get; private set; }

        /// <summary>Fração da velocidade perdida com o inventário no peso máximo.</summary>
        public static ConfigEntry<float> FlightWeightPenalty { get; private set; }

        /// <summary>
        /// Expoente com que a carga chega à penalidade do <see cref="FlightWeightPenalty"/>.
        /// 1 = linear; acima disso o peso quase não pesa até o inventário encher.
        /// </summary>
        public static ConfigEntry<float> FlightWeightCurve { get; private set; }

        /// <summary>Nível mínimo de Power Level para decolar. 0 desliga a trava.</summary>
        public static ConfigEntry<float> FlightMinPowerLevel { get; private set; }

        /// <summary>
        /// Teto duro de velocidade. Não é balanceamento: acima de certa velocidade o
        /// streaming de zonas do Valheim não acompanha e o mundo carrega em pedaços
        /// (ou o jogador cai pelo chão). Limite do motor, não do mod.
        /// </summary>
        public static ConfigEntry<float> FlightMaxSpeed { get; private set; }

        // O voo não tem config de pose nenhuma. Os números da pose procedural viraram constantes
        // em <c>FlightPose</c> no playtest de 2026-07-31; o corpo na horizontal e a pose em pé
        // forçada no animator seguiram o mesmo caminho em 2026-09-13, direto no
        // <c>FlightPosePatch</c>. São decisão de arte fechada, não balanceamento — não há motivo
        // para outro jogador querer diferente.

        // ---------- 6 - Battle Power ----------

        public static ConfigEntry<float> PowerK1Health { get; private set; }

        /// <summary>Só entra na fórmula do ki desligado — com ki não há arma equipada.</summary>
        public static ConfigEntry<float> PowerK2WeaponDamage { get; private set; }

        /// <summary>Só entra na fórmula do ki desligado — com ki a armadura é saída, não entrada.</summary>
        public static ConfigEntry<float> PowerK3Armor { get; private set; }

        /// <summary>Peso do nível da skill Power Level. Só entra na fórmula do ki ligado.</summary>
        public static ConfigEntry<float> PowerK4PowerSkill { get; private set; }

        /// <summary>
        /// Poder que o termo de fim de jogo entrega no nível 100. 0 desliga o termo e devolve a
        /// fórmula linear original. Só afeta combate — ver <see cref="Power.BattlePower"/>.
        /// </summary>
        public static ConfigEntry<float> PowerK5LateGame { get; private set; }

        /// <summary>Quão tarde o termo de fim de jogo acorda. Maior = mais concentrado no topo.</summary>
        public static ConfigEntry<float> PowerLateGameExponent { get; private set; }

        // ---------- 6.1 - Power Level ----------

        public static ConfigEntry<float> SkillXpPerDamageDealt { get; private set; }
        public static ConfigEntry<float> SkillXpPerDamageTaken { get; private set; }

        /// <summary>XP extra no peso máximo de inventário. 1.0 = dobra.</summary>
        public static ConfigEntry<float> SkillXpWeightBonus { get; private set; }

        /// <summary>Trava de segurança: XP máximo de um único golpe.</summary>
        public static ConfigEntry<float> SkillXpMaxPerEvent { get; private set; }

        /// <summary>
        /// Fração do dano de arma que conta como XP. 0 = arma não treina Power Level.
        /// Soco e ataque de ki contam sempre inteiros.
        /// </summary>
        public static ConfigEntry<float> SkillXpWeaponFactor { get; private set; }

        /// <summary>Peso da vida efetiva no poder de luta escaneável. Vale para jogador e inimigo.</summary>
        public static ConfigEntry<float> RatingK1Health { get; private set; }

        /// <summary>Quanta armadura dobra a vida efetiva. Inimigo tem 0, então não é afetado.</summary>
        public static ConfigEntry<float> RatingArmorScale { get; private set; }

        /// <summary>Peso do dano por segundo no poder de luta. Vale para jogador e inimigo.</summary>
        public static ConfigEntry<float> RatingK2Damage { get; private set; }

        /// <summary>Quanto do multiplicador da forma chega ao poder de luta do jogador.</summary>
        public static ConfigEntry<float> RatingFormShare { get; private set; }

        // Aqui morava o PowerCompressionExponent, removido no playtest da etapa 10: um expoente
        // sobre o valor vira o mesmo expoente sobre a razao, e ele achatava justamente as
        // diferencas que o numero existe para mostrar. Ver PowerRating.GetDisplay.

        /// <summary>
        /// Multiplicador linear do número exibido. Só escolhe o tamanho, não distorce razão —
        /// por isso 1 mostra o valor cru, que é o que o poder de luta já foi desenhado para ser
        /// legível (javali ~55, troll ~800, Fader ~25000). Fixado em 2026-09-15: era escolha de
        /// gosto, e o gosto ficou em "número honesto".
        /// </summary>
        public const float PowerDisplayScale = 1f;

        // ---------- 8 - Effects ----------

        // A seção 8 do .cfg não existe mais: em 2026-09-15, antes da primeira publicação no
        // Thunderstore, tudo o que ela tinha virou constante. Eram vinte e oito chaves de prefab,
        // cor, escala e tempo — nenhuma delas balanceamento, todas já calibradas na tela, e todas
        // no caminho de quem abre o arquivo procurando dano. O que cada número quer dizer, e de
        // onde ele veio, está no comentário de cada um.
        //
        // Antes delas, aqui morava o ChargeEmote, removido em 2026-08-07: o emote de carregamento
        // saiu inteiro quando a pose procedural entrou. Ver KiChargePose.

        /// <summary>Efeito preso ao jogador enquanto ele carrega ki.</summary>
        public const string ChargeEffectPrefab = "fx_DvergerMage_Support_start";

        /// <summary>Som em loop enquanto carrega.</summary>
        public const string ChargeSoundPrefab = "sfx_charred_mage_attack_charge";

        /// <summary>
        /// Cor do efeito de carregamento. Azul de ki, o mesmo da barra. Ignorada transformado: ali
        /// o brilho sai na <c>AuraColor</c> da forma, para os dois lerem como uma coisa só.
        /// </summary>
        public const string ChargeEffectColor = "#4FC3F7";

        /// <summary>
        /// Escala do efeito. O suporte do Dverger nasce pequeno demais na escala do jogador e
        /// precisa dobrar. Calibrado no playtest de 2026-07-28.
        /// </summary>
        public const float ChargeEffectScale = 2f;

        /// <summary>
        /// Força partículas e áudio a repetir. Prefab do jogo é feito para um estouro curto; sem
        /// isto o efeito some sozinho depois de um segundo.
        /// </summary>
        public const bool ChargeEffectForceLoop = true;

        /// <summary>
        /// Emote de disparo único tocado ao transformar. "roar" foi o escolhido no playtest: é o
        /// que lê como power up. Não toca ao DESCER de forma — descer é alívio, não estouro.
        /// </summary>
        public const string TransformEmote = "roar";

        /// <summary>
        /// Estouro da transformação. Mesmo prefab do carregamento de propósito: ele já se provou
        /// legível preso ao jogador, e quem separa os dois estados é a cor — azul carregando, a
        /// cor da forma transformado. A cor não mora aqui: é por forma.
        /// </summary>
        public const string TransformAuraPrefab = "fx_DvergerMage_Support_start";

        /// <summary>
        /// Escala do estouro. Um pouco maior que o carregamento de propósito: transformar tem que
        /// ler maior que carregar até lá.
        /// </summary>
        public const float TransformAuraScale = 2.5f;

        /// <summary>
        /// Segundos que o estouro dura, impostos por nós e não herdados do prefab — o
        /// <c>TimedDestruction</c> dele só dispara sozinho se marcou <c>m_triggerOnAwake</c>, e
        /// confiar nisso foi o que deixou o efeito aceso a forma inteira (2026-08-02).
        /// </summary>
        public const float TransformAuraDuration = 2f;

        /// <summary>
        /// Manter o estouro aceso enquanto a forma durar. <b>false por playtest</b> (2026-08-02):
        /// prefab de estouro em loop não dura mais tempo, vira nuvem colada no personagem, porque
        /// as partículas nunca chegam a dispersar.
        /// </summary>
        public const bool TransformAuraForceLoop = false;

        /// <summary>
        /// Multiplicador da luz dinâmica do estouro. 1 é o prefab como veio, que é o certo para um
        /// flash de meio segundo — o baque é a luz.
        /// </summary>
        public const float TransformAuraLightIntensity = 1f;

        // Os raios das formas altas. Quais formas crepitam e de que cor continua sendo por forma
        // (LightningEnabled/LightningColor); a regulagem do estalo é a mesma em qualquer uma e
        // mora aqui.
        //
        // Estalos REPETIDOS, e não um efeito sustentado, pela mesma razão da TransformAuraForceLoop
        // acima. Raio não tem o problema da nuvem porque ele JÁ é intermitente por natureza — a
        // leitura certa é um estalo curto aqui, outro ali, que é literalmente como o SSJ2 se
        // apresenta.

        /// <summary>Prefab de um estalo de raio.</summary>
        public const string FormLightningPrefab = "fx_Lightning";

        /// <summary>Segundos médios entre dois estalos.</summary>
        public const float FormLightningInterval = 0.55f;

        /// <summary>
        /// Quanto o intervalo varia, em fração dele: cada intervalo é sorteado entre 40% e 160%
        /// do acima. Sem sorteio os estalos batem em compasso e leem como máquina, não como
        /// energia instável — é a mesma razão pela qual o jogo randomiza som de passo.
        /// </summary>
        public const float FormLightningIntervalJitter = 0.6f;

        /// <summary>Quantos raios saem por estalo.</summary>
        public const int FormLightningCount = 1;

        /// <summary>
        /// Escala de cada raio. Bem abaixo do estouro da transformação de propósito: são faíscas
        /// em volta do corpo, não uma explosão.
        /// </summary>
        public const float FormLightningScale = 0.5f;

        /// <summary>
        /// Raio do cilindro em volta do corpo onde os estalos nascem, em metros. O jogador tem uns
        /// 0,4 m de largura: abaixo disso o raio nasce dentro do personagem, muito acima vira
        /// clima em vez de aura.
        /// </summary>
        public const float FormLightningRadius = 0.6f;

        /// <summary>Altura do centro do cilindro acima dos pés — a altura do peito.</summary>
        public const float FormLightningHeight = 1.1f;

        /// <summary>Altura total do cilindro: 1,8 m cobre o corpo inteiro, dos pés ao topo.</summary>
        public const float FormLightningSpread = 1.8f;

        /// <summary>
        /// Segundos que cada estalo dura. Curto de propósito: estalo que demora deixa de ler como
        /// raio. Imposto por nós pelo mesmo motivo da <see cref="TransformAuraDuration"/>, e aqui
        /// importa mais — são dezenas de objetos por minuto.
        /// </summary>
        public const float FormLightningDuration = 0.35f;

        /// <summary>
        /// Multiplicador da luz dinâmica de cada estalo. <b>0 de propósito</b>: a luz do
        /// <c>fx_Lightning</c> ilumina o terreno, e piscando a cada meio segundo vira
        /// estroboscópio para quem joga à noite. As partículas têm brilho próprio.
        /// </summary>
        public const float FormLightningLightIntensity = 0f;

        // O brilho das formas. Mesma divisão de sempre — quanto cada forma brilha e de que cor é
        // por forma (GlowIntensity/GlowColor), a regulagem é compartilhada e mora aqui.
        //
        // Uma luz nua presa ao jogador, sem prefab e sem partícula nenhuma. É o contrário exato da
        // TransformAuraLightIntensity: lá a luz é herdada de um prefab de ESTOURO e o cuidado é
        // não deixá-la acesa por engano; aqui ela é o pedido. Ver a cabeça do FormGlow.

        /// <summary>
        /// Intensidade da luz da forma, antes do multiplicador de cada uma. Bem abaixo de uma
        /// tocha (~1,5) de propósito: é para se notar à noite e quase não aparecer ao meio-dia,
        /// não para iluminar o caminho.
        /// </summary>
        public const float FormGlowIntensity = 0.8f;

        /// <summary>
        /// Alcance da luz, em metros. Pequeno de propósito: o pedido é uma poça de luz em volta do
        /// personagem. Alcance grande ilumina meia clareira e deixa de ler como vindo do corpo.
        /// </summary>
        public const float FormGlowRange = 6f;

        /// <summary>Altura da luz acima dos pés — peito, que ilumina corpo e chão de uma vez.</summary>
        public const float FormGlowHeight = 1.1f;

        /// <summary>
        /// Amplitude da respiração da luz, em fração da intensidade: ela oscila entre 85% e 115%,
        /// com a intensidade como MÉDIA e não como teto. Sem pulso a luz lê como lâmpada
        /// aparafusada no personagem, não como energia que ele mal segura.
        /// </summary>
        public const float FormGlowPulseAmount = 0.15f;

        /// <summary>
        /// Velocidade da respiração, em ciclos por segundo. Calibrada no playtest de 2026-08-27, o
        /// primeiro do brilho: saiu em 1,2 — "um ciclo por segundo é o ritmo de uma respiração" —
        /// e desceu para 0,3, quatro vezes mais lento. A analogia estava errada: cadência de
        /// respiração literal, vista de fora, lê como a luz piscando.
        /// </summary>
        public const float FormGlowPulseSpeed = 0.3f;

        /// <summary>
        /// Segundos que a luz leva para acender e apagar. 0 estala — principalmente na saída, em
        /// que não há mais nada acontecendo na tela para cobrir.
        /// </summary>
        public const float FormGlowFade = 0.4f;

        /// <summary>
        /// A luz projeta sombras. <b>Desligada de propósito</b>: luz pontual com sombra custa SEIS
        /// mapas de sombra por quadro, esta luz vive minutos e há uma por jogador transformado na
        /// cena. Ligar é bonito e custa quadros de verdade.
        /// </summary>
        public const bool FormGlowShadows = false;

        // ---------- 10 - Multiplayer ----------

        // As duas chaves da seção 10 — ShowRemotePoses e ShowRemoteEffects — saíram em 2026-09-15.
        // Ver os outros jogadores voando e transformados não é preferência, é o mod funcionando;
        // quem as desligasse veria os amigos correndo no ar. Eram interruptor de depuração com
        // nome de config.

        // ---------- 9 - Debug ----------

        public static ConfigEntry<bool> VerboseLogging { get; private set; }

        // ---------- Escrituração do arquivo ----------

        /// <summary>
        /// Qual rodada de migração este <c>.cfg</c> já recebeu. Ver <see cref="Util.ConfigMigration"/>.
        ///
        /// Escondida da UI e <b>não</b> AdminOnly: é estado do arquivo de cada máquina, não
        /// balanceamento. Se o servidor impusesse a versão dele, o cliente registraria ter recebido
        /// uma migração que nunca rodou no arquivo dele.
        /// </summary>
        public static ConfigEntry<int> ConfigVersion { get; private set; }

        public static void Init(ConfigFile config)
        {
            // Primeiro de todos: a migração no fim do Init precisa saber de onde este arquivo vem,
            // e 0 é "de antes de existir migração". Instalação limpa também entra como 0 e sai
            // migrada sem nada a fazer, porque o arquivo já nasce com os defaults novos.
            ConfigVersion = config.Bind(SecDebug, "ConfigVersion", 0,
                new ConfigDescription(
                    "Bookkeeping: which round of balance migrations this file has already " +
                    "received. Do not edit — lowering it makes the mod redo a migration and " +
                    "overwrite balance keys you may have tuned.",
                    null,
                    new global::ConfigurationManagerAttributes
                    {
                        IsAdminOnly = false, Browsable = false, Order = 0,
                    }));

            // Client-side: preferência de cada jogador, servidor não impõe.
            ToggleKiKey = config.Bind(SecGeral, "ToggleKiKey",
                new KeyboardShortcut(KeyCode.K),
                new ConfigDescription(
                    "Key that toggles ki on and off. Ki turned off behaves like zero ki: " +
                    "no damage bonus, no mastery accumulating, no ki term in the battle power.",
                    null, ClientSide(100)));

            KiEnabledByDefault = config.Bind(SecGeral, "KiEnabledByDefault", true,
                new ConfigDescription("Starting ki state on a brand new character.",
                    null, ClientSide(90)));

            ChargeKiKey = config.Bind(SecGeral, "ChargeKiKey",
                new KeyboardShortcut(KeyCode.R),
                new ConfigDescription(
                    "Key HELD DOWN to actively charge ki, far faster than passive regeneration.",
                    null, ClientSide(95)));

            ToggleFlightKey = config.Bind(SecGeral, "ToggleFlightKey",
                new KeyboardShortcut(KeyCode.F),
                new ConfigDescription(
                    "Key that takes off and lands. Once airborne, movement is the usual one: " +
                    "the game's Jump button climbs, Crouch descends and Run flies fast.",
                    null, ClientSide(85)));

            // Quatro teclas, em dois pares: T/G resolvem o caso comum de um toque so — "poder
            // maximo agora" e "sai da forma agora" — e Shift+T/Shift+G percorrem a escada degrau a
            // degrau, para quem quer uma forma intermediaria. Sem o par direto, entrar em SSJ3 no
            // meio de uma luta custaria tres toques; sem o par de degraus, formas intermediarias
            // seriam inalcancaveis. T e G sao vizinhas verticais no teclado e ambas livres no
            // Valheim.
            //
            // Nao ha custo de ativacao — o que a forma cobra e o dreno continuo — entao subir e
            // descer e de graca em qualquer combinacao.
            TransformKey = config.Bind(SecGeral, "TransformKey",
                new KeyboardShortcut(KeyCode.T),
                new ConfigDescription(
                    "Key that transforms straight into the HIGHEST form you have unlocked, " +
                    "skipping everything below it. Does nothing when you are already there.",
                    null, ClientSide(84)));

            TransformStepUpKey = config.Bind(SecGeral, "TransformStepUpKey",
                new KeyboardShortcut(KeyCode.T, KeyCode.LeftShift),
                new ConfigDescription(
                    "Key that goes UP one step on the ladder: base form to SSJ, SSJ to SSJ2, and " +
                    "so on. Use it to stop at an intermediate form instead of jumping to the top. " +
                    "Does nothing at the top of what you have unlocked.",
                    null, ClientSide(83)));

            // Z e Shift+Z, e nao G: a l-1.0.7 do Valheim passou a abrir o menu radial em G, e as
            // duas coisas na mesma tecla brigam. Calibrado no playtest de 2026-09-20.
            PowerDownKey = config.Bind(SecGeral, "PowerDownKey",
                new KeyboardShortcut(KeyCode.Z),
                new ConfigDescription(
                    "Key that drops you straight back to base form, from whatever step you are " +
                    "on — no walking back down the ladder. Running out of ki does the same thing " +
                    "on its own: at zero there is nothing to hold any form with.",
                    null, ClientSide(82)));

            TransformStepDownKey = config.Bind(SecGeral, "TransformStepDownKey",
                new KeyboardShortcut(KeyCode.Z, KeyCode.LeftShift),
                new ConfigDescription(
                    "Key that goes DOWN one step, to trade power for a smaller ki drain without " +
                    "leaving the ladder entirely. From the first form it returns to base.",
                    null, ClientSide(81)));

            // V e Shift+V pelo mesmo desenho de T/Z: a acao comum num toque, a troca no Shift. Nao
            // sao Z nem vizinha dele porque Z ja e' o power down — e disparar e destransformar sao
            // as duas teclas que mais se aperta com pressa, entao vizinhas seria pedir engano.
            //
            // Passam pelo Hotkey e nao pelo KeyboardShortcut.IsDown cru: atirar parado e' justamente
            // o que nao se quer ensinar, e o IsDown do BepInEx recusa o atalho com W pressionado.
            FireKiAttackKey = config.Bind(SecGeral, "FireKiAttackKey",
                new KeyboardShortcut(KeyCode.V),
                new ConfigDescription(
                    "Key that fires the selected ki attack, aimed where you are looking. " +
                    "Needs ki turned on, enough ki for the shot, and the attack unlocked. " +
                    "The ki is spent on the shot, hit or miss.",
                    null, ClientSide(79)));

            CycleKiAttackKey = config.Bind(SecGeral, "CycleKiAttackKey",
                new KeyboardShortcut(KeyCode.V, KeyCode.LeftShift),
                new ConfigDescription(
                    "Key that cycles through the ki attacks you have unlocked. " +
                    "With a single one unlocked it just names it on screen. " +
                    "The selection is not saved: every session starts on the first attack.",
                    null, ClientSide(78)));

            FlightTakeOffOnDoubleJump = config.Bind(SecGeral, "TakeOffOnDoubleJump", true,
                new ConfigDescription(
                    "Tapping the Jump button twice quickly takes off, on the ground or mid-air. " +
                    "It only takes OFF, never lands: Jump is what climbs while flying, so a " +
                    "double tap up there would fight the control you are already using. " +
                    "ToggleFlightKey and touching the ground are what land you.",
                    null, ClientSide(80)));

            FlightDoubleJumpWindow = config.Bind(SecGeral, "DoubleJumpWindow", 0.35f,
                new ConfigDescription(
                    "Maximum seconds between the two taps. Too high and normal jump spamming " +
                    "launches you by accident; too low and the double tap stops registering.",
                    new AcceptableValueRange<float>(0.05f, 1f), ClientSide(75)));

            // --- Ki ---
            MaxKi = config.Bind(SecKi, "MaxKi", 50f,
                new ConfigDescription(
                    "Maximum ki at level 0 of the Power Level skill. The cap grows from here — " +
                    "see MaxKiPerPowerLevel. With a fixed cap the bar would be the same size from " +
                    "the first boss to the last and progression would never show up on the HUD. " +
                    "(Playtest value, 2026-07-31. Still being tuned.)",
                    new AcceptableValueRange<float>(10f, 10000f), AdminOnly(100)));

            MaxKiPerPowerLevel = config.Bind(SecKi, "MaxKiPerPowerLevel", 3f,
                new ConfigDescription(
                    "Maximum ki added per level of Power Level. With the default, level 100 " +
                    "quadruples the bar (100 base + 300).",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(95)));

            KiRegenPerSecond = config.Bind(SecKi, "KiRegenPerSecond", 2f,
                new ConfigDescription(
                    "Ki regenerated per second while idle. Deliberately low: passive regeneration " +
                    "is the safety net, not the normal way to get ki back. " +
                    "If you want ki, you charge for it. " +
                    "(Playtest value, 2026-08-13. Went 0.5 → 1 on 2026-08-01, when taking damage " +
                    "started costing ki and the safety net had to hold more; 1 → 2 once flight " +
                    "got cheaper and the downtime between flights was the thing being waited on.)",
                    new AcceptableValueRange<float>(0f, 500f), AdminOnly(90)));

            // Escala pelo battle power DERIVADO, nao pelo nivel da skill: e a mesma base do
            // FlightSpeedFromPower, entao comer melhor recarrega mais rapido do mesmo jeito que ja
            // faz voar mais rapido. Trocar para PowerSkill.GetLevel e uma linha, se o playtest
            // disser que a volatilidade da comida incomoda.
            KiRegenFromPower = config.Bind(SecKi, "KiRegenFromPower", 0.0075f,
                new ConfigDescription(
                    "Ki per second ADDED to the passive regeneration for each point of raw battle power. " +
                    "Exists because the bar grows with power (MaxKiPerPowerLevel) and a " +
                    "flat tap does not: without this, the stronger the character the SLOWER he " +
                    "fills his own bar, which is the opposite of the intent. The default keeps " +
                    "seconds-to-fill roughly flat across the whole game instead of making the " +
                    "strong player faster — the conservative half of the fix. " +
                    "Check it with saiya_ki, which prints seconds to fill.",
                    new AcceptableValueRange<float>(0f, 5f), AdminOnly(85)));

            KiRegenDelay = config.Bind(SecKi, "KiRegenDelay", 5f,
                new ConfigDescription(
                    "Seconds without regenerating after spending ki. Deliberately long, together " +
                    "with a low KiRegenPerSecond: spending ki should hurt, and recovering it " +
                    "should be an action. (Calibrated in the 2026-07-28 playtest.)",
                    new AcceptableValueRange<float>(0f, 10f), AdminOnly(70)));

            // Rested e o unico loop do vanilla que recompensa montar base, e o mod meio que
            // atropela ele: um saiyajin que voa e atira ki blast tem pouco motivo pra construir
            // casa. Pendurar ki no buff devolve peso ao conforto sem inventar sistema novo.
            //
            // O buff dura 300s + 60s por nivel de conforto e ACOMPANHA o jogador no campo, entao
            // o bonus nao e "recarrega mais rapido em casa" (isso seria inutil, carregar ja e mais
            // rapido que esperar) e sim "sai de casa aguentando mais tempo de luta la fora".
            KiRegenRestedMultiplier = config.Bind(SecKi, "KiRegenRestedMultiplier", 1.5f,
                new ConfigDescription(
                    "Multiplies passive ki regeneration while the player has the vanilla Rested " +
                    "buff (fire under shelter, or waking up in a bed). Same 1.5 the game itself " +
                    "uses for health and stamina regen, so the ki bar reads as one more thing " +
                    "Rested covers instead of a mod-only rule. " +
                    "Multiplicative, unlike the additive bonuses elsewhere in the mod, because it " +
                    "has to keep up with a tap that already grows with battle power. " +
                    "1 disables it. Does NOT touch active charging: making charging faster at home " +
                    "would only say 'top up before leaving', which is not a decision worth having.",
                    new AcceptableValueRange<float>(0.1f, 5f), AdminOnly(68)));

            // Este e o lado que se sente de verdade. A regen passiva e 2/s numa barra de centenas:
            // 50% a mais somem na conta. O delay pos-gasto e o que o jogador cronometra no meio da
            // luta, e cortar dois segundos dele muda o ritmo do combate.
            KiRegenDelayRestedMultiplier = config.Bind(SecKi, "KiRegenDelayRestedMultiplier", 0.6f,
                new ConfigDescription(
                    "Multiplies KiRegenDelay while the player has the vanilla Rested buff. " +
                    "With the defaults, the 5s pause after spending ki becomes 3s. " +
                    "This is the half of the Rested bonus that actually gets noticed in combat — " +
                    "the multiplier on a small passive tap does not. " +
                    "1 disables it; 0 removes the pause entirely while rested.",
                    new AcceptableValueRange<float>(0f, 2f), AdminOnly(66)));

            ChargeKiPerSecond = config.Bind(SecKi, "ChargeKiPerSecond", 5f,
                new ConfigDescription(
                    "Ki per second while the charge key is held. " +
                    "Should be much higher than KiRegenPerSecond — the point is that charging is a " +
                    "deliberate action worth taking, not just waiting faster. " +
                    "Deliberately ignores KiRegenDelay. " +
                    "(Playtest value, 2026-07-31. Still being tuned.)",
                    new AcceptableValueRange<float>(0f, 500f), AdminOnly(60)));

            // Dez vezes o KiRegenFromPower, que e a mesma proporcao entre ChargeKiPerSecond e
            // KiRegenPerSecond. Mantem a relacao entre carregar e esperar constante ao longo do
            // jogo, em vez de fazer uma das duas formas dominar so por causa do nivel.
            ChargeKiFromPower = config.Bind(SecKi, "ChargeKiFromPower", 0.075f,
                new ConfigDescription(
                    "Ki per second ADDED to active charging for each point of raw battle power. " +
                    "Same reason as KiRegenFromPower: with a flat 5/s, filling the bar goes from " +
                    "10 seconds early on to over a minute late, because only the cap grows. " +
                    "The number that matters when tuning this is NOT ki per second, it is " +
                    "seconds-to-fill — read it off saiya_ki at a low and a high skill level.",
                    new AcceptableValueRange<float>(0f, 5f), AdminOnly(55)));

            ChargeRequiresStandingStill = config.Bind(SecKi, "ChargeRequiresStandingStill", true,
                new ConfigDescription(
                    "If true, moving interrupts charging. Charging while standing still is the " +
                    "classic Dragon Ball gesture and creates a real choice: stopping to charge " +
                    "leaves you exposed. (Both tested in the 2026-07-28 playtest; standing still won.)",
                    null, AdminOnly(50)));

            // Recompensa de luta boa, e nao so de ficar parado recarregando. Medida em SOCOS, e
            // nao em ki nem em fracao da barra: o custo do soco ja escala com o poder e ja leva o
            // desconto de fim de jogo (BattlePower.GetKiCostFactor), entao "um parry paga dois
            // socos" continua verdade do primeiro bioma ao ultimo sem recalibrar nada. Os numeros
            // 2 e 4 sao do Henrique, 2026-09-17, antes de qualquer playtest.
            //
            // 2026-09-20, rework do custo de ki: subiram para 3 e 6. Sao a compensacao pelo que
            // ficou de fora do rework — a regeneracao passiva continua desligada dentro da forma,
            // e ganhar ki por soco foi recusado. Com o custo da acao subindo, luta longa precisa
            // de alguma entrada, e a entrada escolhida exige jogar bem em vez de so acertar.
            KiOnParryPunches = config.Bind(SecKi, "KiOnParryPunches", 3f,
                new ConfigDescription(
                    "Ki gained on a successful parry (a block timed right), measured in punches: " +
                    "3 means the ki cost of three punches at your current power. Only with ki on, " +
                    "and only when the parry actually stopped damage. 0 disables it.",
                    new AcceptableValueRange<float>(0f, 50f), AdminOnly(45)));

            KiOnKillPunches = config.Bind(SecKi, "KiOnKillPunches", 6f,
                new ConfigDescription(
                    "Ki gained for landing the killing blow on a creature, measured in punches: " +
                    "6 means the ki cost of six punches at your current power. Any weapon or ki " +
                    "attack counts, as long as ki is on. Tamed creatures and players give nothing. " +
                    "0 disables it.",
                    new AcceptableValueRange<float>(0f, 50f), AdminOnly(40)));

            // --- Combate ---
            // O numero mais arriscado da etapa 3: alto demais e o combate vira gerenciamento
            // de barra em vez de porrada.
            //
            // Fracao e nao valor fixo pelo mesmo motivo do ArmorFractionWithoutKi: um custo fixo
            // envelhece mal. A 6 por soco o golpe ficava progressivamente mais BARATO em relacao
            // ao que entregava — o bonus de dano cresce com o poder e o custo nao crescia junto,
            // entao o dano por ki so subia. Cobrando sobre o bonus, a razao custo/beneficio fica
            // constante do primeiro bioma ao ultimo sem recalibrar nada.
            //
            // Substituiu a chave `PunchKiCost` (fixa, 6) em 2026-08-01. Renomeada de proposito:
            // o valor antigo num .cfg existente significaria 6 de ki por PONTO de dano bonus,
            // dezenas de ki por soco. O nome novo forca o default novo. Apagar a linha orfa.
            // (Aquele truque nao e' mais necessario: desde 2026-09-20 quem leva um .cfg existente
            // para os defaults novos e' o Util.ConfigMigration, sem sujar o nome da chave.)
            //
            // 3 -> 7 em 2026-09-20, no rework do custo de ki: o dreno das formas despencou e o
            // custo migrou para a ACAO. Estar transformado deixa de ser imposto por segundo e
            // passa a ser caro quando se luta, que e' quando a forma esta' entregando alguma
            // coisa. Ver Melhorias, "Ki deixa de ser imposto de existir".
            PunchKiCostPerDamage = config.Bind(SecCombat, "PunchKiCostPerDamage", 7f,
                new ConfigDescription(
                    "Ki consumed per point of damage the battle power ADDED to the punch — the " +
                    "mirror of DamageTakenKiCost, which charges per point the ki armor absorbed. " +
                    "Both measure the service ki rendered, so the cost scales with the payoff " +
                    "instead of aging into irrelevance. The cost is therefore " +
                    "PunchDamageFromPower * battle power * this, and the vanilla unarmed base " +
                    "damage is free — ki did not provide it. Insufficient ki does NOT cancel the " +
                    "hit: the punch lands with raw vanilla damage, without the bonus. Missing " +
                    "costs nothing (the charge happens on the hit, not on the swing). Set to zero " +
                    "to disable the cost. " +
                    "(Playtest value, 2026-08-01: started at 1 to match DamageTakenKiCost and the " +
                    "punch was nearly free — the bar barely moved in a fight. 3 is what made the " +
                    "cost readable. Raised to 7 on 2026-09-20, when the ki cost of a form moved " +
                    "from per second held to per punch thrown.)",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(100)));

            // O conserto da assimetria que o playtest de 2026-08-04 expos: os TRES custos de
            // combate saem do poder de COMBATE, que nao tem teto (o termo de fim de jogo cresce
            // para sempre e a forma multiplica), e a barra de ki sai do NIVEL da skill, que para em
            // 100. Um numero que cresce sem fim dividido por um que parou: o combate fica
            // impagavel — primeiro transformado, depois sempre.
            //
            // Uma chave para os tres, e nao uma por consumidor: e' o mesmo fenomeno nos tres, e
            // separa-las convidaria a um estado incoerente — soco barato e bloqueio caro — sem
            // nenhuma pergunta de design por tras da diferenca.
            //
            // Hiperbolico e nao linear, pela mesma razao do voo: a entrada nao tem teto, e um
            // `1 - r * poder` atravessaria o zero e viraria golpe que DEVOLVE ki.
            DefenseKiCostPowerReduction = config.Bind(SecCombat, "DefenseKiCostPowerReduction", 0f,
                new ConfigDescription(
                    "Same hyperbolic discount as KiCostPowerReduction, but for the two DEFENSIVE " +
                    "ki costs: BlockKiCost and DamageTakenKiCost. 0 (the default) means no " +
                    "discount at all. " +
                    "Why it is separate and why it is zero: those two costs are charged per point " +
                    "of the ENEMY's hit that ki stopped, and an enemy's hit does not grow with " +
                    "your power — so there is no runaway growth here for a discount to correct. " +
                    "Sharing the punch's discount made defending cheaper exactly as it got " +
                    "stronger, and transforming multiplied the effect, because the form " +
                    "multiplies the power that buys the discount. Playtested 2026-09-20: in SSJ3 " +
                    "a block stopped 3.4x more damage for the same ki. " +
                    "Raise it only if late-game biomes hit hard enough to make defending " +
                    "unpayable — that is the one question this key is here to answer.",
                    new AcceptableValueRange<float>(0f, 0.02f), AdminOnly(59)));

            // 1 -> 0,5 -> 0,2 nos playtests de 2026-09-20 e 21: lutar transformado estava caro
            // demais, e a metade nao bastou. Cortada a SOBRETAXA e nao as taxas base: o que
            // incomodava era o custo DENTRO da forma, e soco, apanhar e bloquear na forma base
            // estavam calibrados. Em 0,2 uma forma x4 custa 1,6x por soco em vez de 4x na maestria
            // 0, entao ela ja' nasce entregando 2,5x mais dano por ki que a base — o que antes so'
            // acontecia com maestria. A maestria continua zerando a sobretaxa no nivel 100, e o
            // que ela paga agora e' bem menor, porque o que sobrou para devolver e' menor.
            CombatFormKiShare = config.Bind(SecCombat, "CombatFormKiShare", 0.2f,
                new ConfigDescription(
                    "How much of a transformation's power gain turns into EXTRA ki cost for the " +
                    "three combat costs — punching, blocking and taking hits: " +
                    "cost is multiplied by 1 + (PowerMultiplier - 1) * this * " +
                    "(1 - MasteryFormCostReduction * mastery/100). " +
                    "At 1 a form with x4 power costs 4x the ki per punch at mastery 0, which " +
                    "means the SAME damage per ki as base form — what the form buys there is the " +
                    "bigger hit, not efficiency. At the default 0.2 that same form costs 1.6x, so " +
                    "it already lands 2.5x the damage per ki before any mastery. Either way the " +
                    "surcharge is gone at mastery 100 (see MasteryFormCostReduction), where the " +
                    "whole multiplier becomes profit: at first you barely hold the form, in the " +
                    "end you wear it. " +
                    "Lower it to make transforming cheaper from the start; 0 gives the form's " +
                    "power away for free, which is how it behaved before 2026-09-20. " +
                    "Mirrors FormKiShare in the flight section. " +
                    "(Playtest value, 2026-09-21: shipped at 1 and fighting transformed drained " +
                    "the bar too fast; 0.5 was still too tight, and 0.2 is what made fighting in " +
                    "a form read as a reward instead of a bill. The base combat rates were not " +
                    "touched.)",
                    new AcceptableValueRange<float>(0f, 3f), AdminOnly(57)));

            KiCostPowerReduction = config.Bind(SecCombat, "KiCostPowerReduction", 0.01f,
                new ConfigDescription(
                    "How much the combat battle power makes the three COMBAT ki costs cheaper — " +
                    "punching (PunchKiCostPerDamage), taking hits (DamageTakenKiCost) and blocking " +
                    "(BlockKiCost) — as 1 / (1 + this * combat power). 0 disables the discount and " +
                    "the costs stay strictly proportional to what ki delivered. " +
                    "Why it exists: all three costs come from the combat battle power, which has no " +
                    "ceiling — the late-game term grows forever and a transformation multiplies it " +
                    "— while the ki bar comes from the Power Level SKILL, which stops at " +
                    "100. Without this, a punch eventually costs more than a full bar and lands " +
                    "with raw vanilla damage, and blocking drains the bar in two hits. \n" +
                    "The shape matters: each cost approaches its own rate divided by this and " +
                    "never passes it, so actions per bar settles instead of falling to zero. For " +
                    "the punch at 0.01 that ceiling is 15 ki, so a full bar always buys a long " +
                    "fight no matter how far the battle power runs. \n" +
                    "It also fixes transformations without a key of its own: a form multiplies the " +
                    "power, and it is the power that buys the discount, so the ratio of damage per " +
                    "bar between transformed and not approaches the form's PowerMultiplier " +
                    "instead of sitting at 1. \n" +
                    "One key for all three deliberately: it is the same problem in all of them, and " +
                    "splitting it would invite cheap punches next to expensive blocks with no " +
                    "design question behind the difference. \n" +
                    "Early game the power is small, so the discount is a few percent and the " +
                    "values calibrated on 2026-08-01 still hold there. " +
                    "(Playtest value, 2026-08-04. Started at 0.002, which was still tight enough " +
                    "that the transformed fight lived on the edge of the bar; 0.01 is what made " +
                    "the combat read as combat instead of bar management.)",
                    new AcceptableValueRange<float>(0f, 1f), AdminOnly(95)));

            // A segunda moeda da maestria, e a resposta ao que a chave acima produziu: o desconto
            // por poder cresce JUNTO com a forma (ela multiplica o poder que compra o desconto),
            // entao subir de degrau quase nao encarece o soco — 12,3 para 13,1 de ki no meio do
            // jogo, contra 50% a mais de dano.
            //
            // Escrito contra o MULTIPLICADOR e nao como taxa fixa por nivel de maestria, e a
            // diferenca nao e' de estilo: o acrescimo que a forma cobra E' o multiplicador dela,
            // entao a devolucao tem que escalar junto. Taxa fixa precisaria de 0,01 numa forma x2 e
            // 0,02 numa x3 — nenhum numero unico acerta os dois, e cada degrau novo da escada
            // exigiria recalibrar. Assim todo degrau pousa no proprio custo base no nivel 100,
            // inclusive os que ainda nao existem.
            //
            // SOMADO ao termo do poder, e nao no lugar dele. Substituir tiraria o HP e a forma de
            // dentro do desconto, e os dois estao no numerador — o caso "HP alto, Power Level
            // baixo" que o [[Em Aberto]] ja lista como risco passaria de ~6 socos por barra para
            // menos de 3, e comer bem encareceria o soco sem nada compensando.
            //
            // ⚠️ Em 1 o alvo atingido e' "a forma maxada nao cobra a mais para lutar", que NAO e' o
            // mesmo que "o degrau velho tem nicho": a 1, o SSJ2 recem-destravado ainda rende mais
            // dano por ki (2,58) que o SSJ maxado (2,17). O cruzamento fica perto de 2,5. Sao dois
            // alvos no mesmo dial e a escolha e' de playtest — por isso o teto da faixa e' 3.
            MasteryFormCostReduction = config.Bind(SecCombat, "MasteryFormCostReduction", 1f,
                new ConfigDescription(
                    "How much of the extra ki cost that a transformation charges is paid back by " +
                    "that form's mastery. The form's surcharge is " +
                    "1 + (PowerMultiplier - 1) * CombatFormKiShare * (1 - this * mastery/100), " +
                    "and it multiplies all four ki costs of fighting: punching, blocking, taking " +
                    "hits and ki attacks. 0 means mastery pays back nothing and the form charges " +
                    "its full surcharge forever. \n" +
                    "1 is the meaningful point: at mastery 100 the form costs EXACTLY what the " +
                    "base form costs for the same action, while still landing PowerMultiplier " +
                    "times the damage. Mastering a form stops it from charging extra to fight in, " +
                    "and that is when the multiplier becomes pure profit. \n" +
                    "Why it is written against the multiplier and not as a flat rate per mastery " +
                    "level: the premium a form charges IS its multiplier, so the payback has to " +
                    "scale with it. A flat rate would need one number for a x2 form and another " +
                    "for a x3 one. This way every rung, including ones that do not exist yet, " +
                    "lands on its own base cost at mastery 100 with no retuning. \n" +
                    "It reads the mastery of the ACTIVE form, which starts at zero on every new " +
                    "rung — so the rung you have mastered is cheaper to fight in than the one you " +
                    "just unlocked, which is the whole point. \n" +
                    "Note this gives mastery a SECOND payoff next to the drain reduction. It stays " +
                    "on the economy axis, not the power axis, so a form still never hits harder " +
                    "for being trained. \n" +
                    "(Rewritten on 2026-09-20: it used to be a term added inside the power " +
                    "discount's divisor, and that discount also read the form-multiplied power, " +
                    "so the form's premium mostly cancelled itself out — in SSJ3 a punch landed " +
                    "4x the damage for 1.28x the cost. The premium is now an explicit multiplier " +
                    "and the power discount reads the BASE form's power.)",
                    new AcceptableValueRange<float>(0f, 1f), AdminOnly(94)));

            PunchDamageFromPower = config.Bind(SecCombat, "PunchDamageFromPower", 0.05f,
                new ConfigDescription(
                    "Fraction of the battle power ADDED to punch damage. Additive, not multiplicative: " +
                    "enemy HP grows roughly linearly across biomes, and an additive stat scales " +
                    "predictably against that. " +
                    "(Playtest value, 2026-08-01. Cut to a third of the 0.15 used on 2026-07-31 — " +
                    "the punch was outscaling the biomes.)",
                    new AcceptableValueRange<float>(0f, 10f), AdminOnly(90)));

            ArmorBase = config.Bind(SecCombat, "ArmorBase", 1f,
                new ConfigDescription(
                    "Armor guaranteed while ki is on, before the share that comes from power. " +
                    "It exists so the player does not end up MORE fragile by turning ki on early " +
                    "in the game, when the skill is still at a low level. " +
                    "(Playtest value, 2026-07-31. Still being tuned.)",
                    new AcceptableValueRange<float>(0f, 200f), AdminOnly(80)));

            // Baixado de 0.15 para 0.06 no playtest de 2026-08-01, junto com a entrada do termo de
            // fim de jogo (K5_LateGameBonus). O motivo e aritmetico: o poder de combate no nivel
            // 100 dobrou, e a armadura le esse numero. Em 0.15 o nivel 100 dava 91 de armadura, e
            // pelo ApplyArmor do jogo (dano²/4*armadura) um golpe de 90 virava 22 — tanque demais.
            // Em 0.06 o mesmo golpe faz 51, e a curva de armadura fica parecida com a de antes do
            // termo novo, que era o alvo: o fim de jogo compra dano e alcance de voo, nao imunidade.
            //
            // ⚠️ Esse 0.06 pressupoe o K5 LIGADO, e ele saiu de default em 2026-08-23. Sem o termo,
            // o poder de combate no nivel 100 volta a ser a METADE do que esta conta assumiu, e a
            // armadura de ki no fim de jogo caiu junto — sem ninguem ter decidido isso. Se o
            // playtest disser que o personagem ficou de papel no late game, e' aqui, e nao no
            // bloqueio, que o numero precisa subir.
            ArmorFromPower = config.Bind(SecCombat, "ArmorFromPower", 0.06f,
                new ConfigDescription(
                    "Fraction of the battle power converted into armor. While ki is on this armor " +
                    "REPLACES equipment armor — worn pieces stop counting. Turning ki off gives " +
                    "vanilla armor back immediately. " +
                    "Reads the COMBAT battle power, so it grows with K5_LateGameBonus — that is why " +
                    "this is much lower than it looks like it should be. " +
                    "(Playtest value, 2026-08-01. Lowered from 0.15 when the late-game term " +
                    "doubled power at level 100 and armor came along for the ride.)",
                    new AcceptableValueRange<float>(0f, 10f), AdminOnly(70)));

            // Fracao e nao valor fixo de propósito: um piso fixo envelhece mal, como o ArmorBase
            // ja mostrou — 1 de armadura significa alguma coisa no nivel 0 e nada nenhum no 100.
            // A fracao acompanha a progressao sem precisar ser recalibrada por bioma.
            ArmorFractionWithoutKi = config.Bind(SecCombat, "ArmorFractionWithoutKi", 0f,
                new ConfigDescription(
                    "Fraction of the ki armor that survives when the ki bar hits ZERO (the toggle " +
                    "is still on, the bar is just empty). At 0 an empty bar means no armor at all, " +
                    "since ki armor replaces equipment armor. Raise it if running out of ki mid " +
                    "fight turns into an unrecoverable death. " +
                    "It is a cliff, not a fade: armor is at full value down to the last point of " +
                    "ki and only drops at zero. Deliberate — ki swings hard during a fight " +
                    "(punches, flight), and losing armor for ATTACKING would punish the player " +
                    "for something that has nothing to do with being hit.",
                    new AcceptableValueRange<float>(0f, 1f), AdminOnly(65)));

            // Cobra sobre o ABSORVIDO, nao sobre o dano bruto nem sobre o aplicado. Duas razoes:
            // o custo mede o servico que a armadura de ki prestou, e o filtro de fontes de dano
            // sai de graça — veneno, queda e afogamento nao passam pela armadura no vanilla,
            // entao absorvem zero e custam zero sem precisar de lista de excecoes.
            //
            // 1 -> 1,5 em 2026-09-20, junto com o PunchKiCostPerDamage e pelo mesmo motivo: os
            // custos de combate sobem porque o dreno da forma desceu. Sobe MENOS que o soco (1,5x
            // contra 2,3x) de proposito — apanhar nao e' uma escolha do jogador, e encarecer na
            // mesma proporcao puniria quem esta' perdendo a luta.
            //
            // E 1,5 -> 0,6 no mesmo dia, depois do primeiro playtest do rework: a defesa deixou de
            // levar o desconto de poder do soco (DefenseKiCostPowerReduction, 0), e esse desconto
            // valia entre 0,1 e 0,3 — as taxas de antes ja' vinham infladas para compensa-lo. Sem
            // ele a conta fica plana de ponta a ponta do jogo: o golpe do inimigo e a barra de ki
            // crescem juntos, entao apanhar custa uma fatia parecida da barra em todo bioma.
            DamageTakenKiCost = config.Bind(SecCombat, "DamageTakenKiCost", 0.6f,
                new ConfigDescription(
                    "Ki consumed per point of damage the ki armor ABSORBED. Taking a hit costs ki " +
                    "the same way landing one does — the ki armor is sustained, not free. " +
                    "Damage that armor does not touch (poison, fall, drowning) absorbs nothing and " +
                    "therefore costs nothing. Set to zero to disable. " +
                    "Note the cost per hit is naturally capped near your armor value: armor can " +
                    "never absorb more than it is worth, so a huge hit does not drain the bar. " +
                    "(Playtest value, 2026-08-01. The conservative 0.15 it shipped with the same " +
                    "day was barely noticeable; at 1 a blocked point of damage costs a point of ki. " +
                    "Raised to 1.5 on 2026-09-20, when the cost of a form moved from per second " +
                    "held to per action taken, and cut to 0.6 the same day, when the defensive " +
                    "costs stopped taking the punch's power discount — see " +
                    "DefenseKiCostPowerReduction.)",
                    new AcceptableValueRange<float>(0f, 5f), AdminOnly(60)));

            // O bloqueio desarmado era 2 de block power contra escudos de 18 a 156 — nao fraco,
            // ARMADILHA: o BlockAttack manda o residuo para o AddStaggerDamage, entao bloqueio
            // pequeno staggera, e bloqueio que falha por stagger nao reduz nada.
            BlockPowerBase = config.Bind(SecCombat, "BlockPowerBase", 2f,
                new ConfigDescription(
                    "Block power guaranteed while ki is on, before the share that comes from power. " +
                    "The 2 is the vanilla unarmed value, kept as a floor so turning ki on at battle power " +
                    "zero never makes blocking WORSE than vanilla. " +
                    "Same role ArmorBase plays for armor, with one difference: unlike armor, this " +
                    "SURVIVES an empty ki bar (ArmorFractionWithoutKi only scales the power-derived " +
                    "share). Zero armor is a legal value; zero block power is a division by zero " +
                    "inside Humanoid.BlockAttack that turns your stamina into NaN permanently. " +
                    "Leave this above zero.",
                    new AcceptableValueRange<float>(0f, 200f), AdminOnly(58)));

            // Cortado de 0.22 para 0.04 no playtest de 2026-08-23, e o motivo NAO e' a escada de
            // escudos — e' a curva do ApplyArmor rodando DUAS vezes no mesmo golpe. O bloqueio
            // barra primeiro e a armadura de ki barra o resto, e a curva do jogo e' dano²/(4*ac):
            // duas passagens viram uma quarta potencia efetiva. Em 0.22 um golpe de 100 no skill
            // 100 chegava em ~14 de dano e custava 2% da barra de ki — bloquear anulava o golpe e
            // era de graca. Em 0.04 o bloqueio volta a ser um desconto sobre o golpe, e a armadura
            // de ki continua sendo a defesa principal.
            //
            // Consequencia aceita: o block power fica ABAIXO do ShieldWood (18.5), o pior escudo
            // do jogo, em toda a progressao. O punho ja ganha em nao quebrar, nao ocupar a mao e
            // escalar sozinho; nao precisa tambem ganhar no numero.
            BlockPowerFromPower = config.Bind(SecCombat, "BlockPowerFromPower", 0.04f,
                new ConfigDescription(
                    "Fraction of the battle power converted into block power. While ki is on this " +
                    "REPLACES the blocker item's value — holding a shield changes nothing, exactly " +
                    "like ArmorFromPower replaces equipment armor. Turning ki off gives the shield " +
                    "back immediately. " +
                    "Kept well BELOW the vanilla shield ladder — under the wood shield across the " +
                    "whole progression. It has to be: a blocked hit runs through Valheim's " +
                    "damage/(4*armor) curve TWICE, once against this and again against the ki " +
                    "armor, so what looks like a modest number compounds into near-immunity. " +
                    "Run 'saiya_block <damage>' to see what a given hit actually does. " +
                    "(Playtest value, 2026-08-23. Cut from the 0.22 of 2026-08-01, which made an " +
                    "endgame block absorb ~86% of a hit for 2% of the ki bar.)",
                    new AcceptableValueRange<float>(0f, 10f), AdminOnly(56)));

            // Metade do DamageTakenKiCost, e nao o mesmo valor, porque um golpe bloqueado paga as
            // DUAS contas: o bloqueio barra primeiro, a armadura barra o resto, e cada uma cobra a
            // sua. Na mesma taxa o preco de apanhar dobraria so por o jogador estar segurando o
            // botao — que e o oposto do que esta mecanica quer ensinar.
            //
            // 0,5 -> 1,2 e, no mesmo dia, 1,2 -> 0,3 pelo motivo do DamageTakenKiCost acima: a
            // defesa perdeu o desconto de poder, e as taxas vinham infladas por causa dele.
            // Continua sendo METADE do DamageTakenKiCost (0,6) pela razao de sempre: um golpe
            // bloqueado paga as duas contas — o bloqueio barra primeiro, a armadura barra o resto —
            // e na mesma taxa segurar o botao dobraria o preco de apanhar.
            BlockKiCost = config.Bind(SecCombat, "BlockKiCost", 0.3f,
                new ConfigDescription(
                    "Ki consumed per point of damage the ki BLOCK stopped, measured (not estimated) " +
                    "from the hit before and after Humanoid.BlockAttack. Same rule as the armor: if " +
                    "it stopped damage, it costs ki. A failed block stops nothing and costs nothing. " +
                    "Unlike the punch, an empty bar does not cancel anything — the block already " +
                    "happened when the charge lands, so it drains what is there, like the armor does. " +
                    "This is the most expensive thing in the mod by design: blocking stops far more " +
                    "damage than armor absorbs, so it should be a beam, not a stance. Lower it if " +
                    "holding block for two hits empties the bar. Set to zero to make blocking free. " +
                    "(2026-08-01: 0.5. Raised to 1.2 on 2026-09-20 with the rest of the combat ki " +
                    "costs, then cut to 0.3 the same day: the first playtest of the rework found " +
                    "blocking nearly free while transformed, the defensive costs stopped taking " +
                    "the punch's power discount, and the rate had been inflated to compensate for " +
                    "it.)",
                    new AcceptableValueRange<float>(0f, 5f), AdminOnly(54)));

            KiBarOffsetX = config.Bind(SecHud, "KiBarOffsetX", 0f,
                new ConfigDescription(
                    "Horizontal offset of the ki bar, in pixels. " +
                    "Applies live: edit the file with the game running and the bar moves.",
                    new AcceptableValueRange<float>(-500f, 500f), ClientSide(90)));

            // -50 nao e gosto pessoal: com -30 a barra de ki cai em cima da barra de stamina.
            // Qualquer valor acima disso precisa ser conferido na tela antes de virar default.
            KiBarOffsetY = config.Bind(SecHud, "KiBarOffsetY", -50f,
                new ConfigDescription(
                    "Vertical offset of the ki bar, in pixels, relative to the position of the " +
                    "native bars. Negative moves down. Applies live. " +
                    "Do not raise this much: at -30 the ki bar lands on top of the stamina bar. " +
                    "(Playtest value, 2026-07-31.)",
                    new AcceptableValueRange<float>(-500f, 500f), ClientSide(80)));

            // --- Poder de luta na HUD (etapa 10) ---
            // Abaixo do minimapa, clonado do rotulo do bioma. Todas as chaves sao client-side —
            // sao posicao e gosto de quem esta na frente da tela, nao balanceamento. O texto segue
            // o minimapa pequeno (some com o mapa grande aberto e com o minimapa desligado nas
            // opcoes do jogo) e some com o ki desligado, como a barra e o numero do inimigo.
            PowerHudOffsetX = config.Bind(SecHud, "PowerHudOffsetX", 0f,
                new ConfigDescription(
                    "Horizontal offset of the text, in pixels, RELATIVE to the minimap's biome " +
                    "label. Positive moves right. Zero puts it exactly on top of that label, so " +
                    "this key and the Y one below are what actually place it.",
                    new AcceptableValueRange<float>(-500f, 500f), ClientSide(48)));

            PowerHudOffsetY = config.Bind(SecHud, "PowerHudOffsetY", -200f,
                new ConfigDescription(
                    "Vertical offset of the text, in pixels, relative to the biome label. Negative " +
                    "moves down. (Playtest value, 2026-09-05. The first guess was -28, one line " +
                    "below the label; on screen the minimap needs a lot more clearance than that. " +
                    "Its layout is Unity asset data and cannot be read from code, so the number " +
                    "could only come from looking.)",
                    new AcceptableValueRange<float>(-500f, 500f), ClientSide(46)));

            PowerHudFontSize = config.Bind(SecHud, "PowerHudFontSize", 20f,
                new ConfigDescription(
                    "Font size, in canvas units — the same units the game's own UI uses, so it " +
                    "already follows resolution and UI scale. The clone has TMP auto-sizing turned " +
                    "off, which is what makes this key work at all: the biome label it is cloned " +
                    "from recomputes its own size every layout pass and would overwrite this. " +
                    "(Playtest value, 2026-09-05.)",
                    new AcceptableValueRange<float>(4f, 60f), ClientSide(44)));

            // --- Poder de luta do inimigo (etapa 10) ---
            // O outro lado do bloco acima: o mesmo numero, na mesma escala, escrito embaixo da
            // barra de vida do inimigo. Tambem client-side, pelo mesmo motivo — e' posicao na
            // tela, nao balanceamento. Ver EnemyPowerHud.
            EnemyPowerOffsetX = config.Bind(SecHud, "EnemyPowerOffsetX", 0f,
                new ConfigDescription(
                    "Horizontal offset of the text, in pixels, RELATIVE to the enemy's name " +
                    "label. Positive moves right. Zero keeps it centred like the name.",
                    new AcceptableValueRange<float>(-300f, 300f), ClientSide(39)));

            EnemyPowerOffsetY = config.Bind(SecHud, "EnemyPowerOffsetY", -30f,
                new ConfigDescription(
                    "Vertical offset of the text, in pixels, relative to the enemy's name label. " +
                    "Negative moves down. The name sits ABOVE the health bar, so this has to " +
                    "clear the bar's height to land under it. " +
                    "(Playtest value, 2026-09-06. The -34 first guess sat a touch low; -30 tucks " +
                    "the number right under the bar. The hud's layout is Unity asset data and " +
                    "cannot be read from code, so this had to be found on screen.)",
                    new AcceptableValueRange<float>(-300f, 300f), ClientSide(38)));

            EnemyPowerFontSize = config.Bind(SecHud, "EnemyPowerFontSize", 16f,
                new ConfigDescription(
                    "Font size, in canvas units. Smaller than the player's own number on purpose: " +
                    "this one is drawn in the world, over the enemy, and several can be on screen " +
                    "at once. TMP auto-sizing is turned off on the clone, which is what makes " +
                    "this key work at all. " +
                    "(Playtest value, 2026-09-06. Raised from 14: at that size the number was " +
                    "unreadable at the distance you actually scan an enemy from.)",
                    new AcceptableValueRange<float>(4f, 40f), ClientSide(37)));

            // --- Transformacoes ---
            // Uma chamada por forma, na ordem da escada. Adicionar o degrau seguinte e' repetir
            // esta linha com outra secao, outros numeros e a global key do boss dele.
            //
            // O SSJ atras do Eikthyr: e' o primeiro boss e cai na primeira hora de jogo, entao a
            // trava e' curta de proposito. Ela nao existe para segurar o jogador longe da forma —
            // existe para que transformar seja uma COISA QUE ACONTECE, com um antes e um depois,
            // em vez de um botao que sempre esteve la. Ver [[Progressao por Bosses]].
            Ssj = BindTransformation(config, SecSsj,
                powerMultiplier: 2f,
                // 5 -> 1 em 2026-09-20. O dreno deixa de cobrar por segundo de existencia e o
                // custo da forma migra para a acao (PunchKiCostPerDamage, BlockKiCost). A escada
                // inteira desceu na mesma proporcao, entao a razao entre os degraus — o que impede
                // o degrau alto de tornar o baixo letra morta — esta' preservada: 1 / 2 / 3 contra
                // os 5 / 10 / 15 de antes. Ver Melhorias, "Ki deixa de ser imposto de existir".
                kiDrainPerSecond: 1f,
                punchSlashFraction: 0.5f,
                punchLightningFraction: 0f,
                // Calibrado no playtest de 2026-08-16. Saiu em 300 — o limite base inteiro do
                // Valheim, ou seja, mochila dobrada — e desceu para 100 na primeira sessao.
                carryWeightBonus: 100f,
                hairColor: "#FFE14A",
                requiredGlobalKey: "defeated_eikthyr",
                lightning: false,
                // O cabelo do proprio personagem, espetado. Ver a nota do HairItem do SSJ3.
                hairItem: "Spiked");

            // O SSJ2 atras do Elder — o boss seguinte ao do SSJ, mantendo o ritmo de um degrau por
            // boss. Ver [[Progressao por Bosses]].
            //
            // Os numeros sao os do SSJ "mais fortes", e cada um tem uma razao para o salto que deu:
            //   PowerMultiplier 3 = uma vez e meia o SSJ. Saiu em 4 (o dobro exato, a leitura
            //     direta do genero), o playtest de 2026-08-16 desceu para 3,5 e o de 2026-08-17
            //     desceu de novo para 3 — ver [[Transformacoes]].
            //   KiDrainPerSecond 10 = MAIS que o dobro de proposito, e continua sendo depois da
            //     calibragem. Se o dreno dobrasse junto com o poder, o degrau alto seria
            //     estritamente melhor por ki gasto e o SSJ viraria letra morta — a escolha entre
            //     os dois degraus tem que custar alguma coisa. Saiu em 12.
            //   MasteryDrainReduction fica no default (1), igual ao do SSJ, e o degrau alto
            //     tambem sai de graca no nivel 100. Ate 2026-08-25 era 0,85 contra 0,8 justamente
            //     para os dois nunca fecharem: com o SSJ2 entregando 3x contra 2x ao mesmo preco,
            //     o SSJ perderia a razao de existir. Isso continua verdade, e e' o preco aceito
            //     de "maxar uma forma e' passar a vestir ela de graca" — o que separa os degraus
            //     no fim do jogo passa a ser o caminho ate la, nao o custo de manter. Note que a
            //     escada de maestria e' por forma: chegar ao 100 do SSJ2 e' um investimento
            //     proprio, e ate ele fechar o dreno maior continua sendo a escolha que o jogador
            //     paga. Ver [[Transformacoes]].
            //   CarryWeightBonus 200 = o dobro do SSJ, pela mesma leitura de "forma mais alta
            //     carrega mais". Nao passa pelo multiplicador, como no SSJ.
            //   O soco reparte em contusao e RAIO, sem corte: o tipo de dano e' o sabor do degrau,
            //     e repetir o corte do SSJ desperdicaria o unico eixo de sabor que existe.
            //
            // O cabelo e' um amarelo mais claro e mais duro que o do SSJ, mas os dois continuam
            // parecidos — quem separa os degraus na tela e' o raio, que e' como o SSJ2 se apresenta.
            Ssj2 = BindTransformation(config, SecSsj2,
                // Calibrados no playtest de 2026-08-16, o primeiro do SSJ2.
                // O multiplicador desceu de novo em 2026-08-17: 4 → 3,5 → 3.
                powerMultiplier: 3f,
                // 10 -> 2 em 2026-09-20, com a escada inteira. Continua o dobro do SSJ.
                kiDrainPerSecond: 2f,
                punchSlashFraction: 0f,
                // Desceu de 0,5 para 0,2 em playtest posterior a 2026-08-17 — motivo nao
                // registrado na hora. O soco do SSJ2 passa a ser contusao com sabor de raio,
                // e nao meio a meio.
                punchLightningFraction: 0.2f,
                carryWeightBonus: 200f,
                hairColor: "#FFF08A",
                requiredGlobalKey: "defeated_gdking",
                lightning: true,
                // Azul, contra o amarelo do cabelo e da aura. Playtest de 2026-08-16: com a cor da
                // aura o raio virava parte do brilho e sumia dentro dele.
                lightningColor: "#66D9FF",
                // Brilha metade de novo que o SSJ. E' o unico numero visual da forma que sobe
                // junto com a forca dela — de longe e a noite, quem esta' em SSJ2 acende mais
                // chao. Chute inicial: o degrau precisa ser visivel sem virar holofote.
                glowIntensity: 1.5f,
                // Mesma malha do SSJ: ha' um grau de espeto so' por penteado, entao o que separa
                // os dois degraus continua sendo a cor e o raio.
                hairItem: "Spiked");

            // O SSJ3 atras do Bonemass — o terceiro boss, mantendo o ritmo de um degrau por boss.
            // Ver [[Progressao por Bosses]].
            //
            // O que este degrau escolhe ser, e por que:
            //   PowerMultiplier 4 = um terco a mais que o SSJ2. Nasceu 4,5, repetindo o passo de
            //     uma vez e meia que o SSJ2 deu sobre o SSJ; desceu para 4 no playtest de
            //     2026-09-07, o primeiro do SSJ3.
            //   KiDrainPerSecond 15 = uma vez e meia o SSJ2, contra o passo de 2x que o SSJ2 deu
            //     sobre o SSJ. O chute inicial era 25 — duas vezes e meia — pela leitura de que o
            //     SSJ3 e' a forma que devora o dono; o playtest de 2026-09-07 cortou para 15. O
            //     dreno ainda sobe mais rapido que o poder (1,5x de dreno contra 1,33x de poder),
            //     que e' o que impede o topo da escada de tornar os degraus de baixo letra morta,
            //     mas a margem ficou bem mais estreita do que a projetada no papel.
            //   PunchLightningFraction 0,5 = meio a meio entre contusao e raio, contra os 0,2 do
            //     SSJ2 (playtest de 2026-09-07, subiu de 0,35). Nada de corte, pelo mesmo motivo
            //     que o SSJ2 nao tem: repetir o sabor do degrau anterior desperdicaria o unico
            //     eixo que existe. Aqui o raio deixa de ser tempero e vira metade do golpe — e' o
            //     degrau em que o tipo de dano vira identidade.
            //   CarryWeightBonus 300 = uma vez e meia o SSJ2, e nao o dobro. Saiu em 400, pela
            //     mesma leitura dos dois degraus anteriores, e o playtest desceu para 300: com 400
            //     o limite de peso deixava de ser uma decisao — nada que se recolhe em jogo chega
            //     perto de encher a mochila, e o [[Voo]] cobra o peso em curva justamente para que
            //     carregar muito custe alguma coisa. Promovido do .cfg em 2026-09-15.
            //
            // O visual e' onde este degrau se separa dos outros dois: ele e' o primeiro que muda a
            // SILHUETA em vez de so' a cor. HairItem SaiyaHair6 e' o Hair6 ("Long and Loose")
            // espetado, o cabelo comprido do genero, e o tom volta um pouco para o dourado
            // fechado — o SSJ2 ja' tinha ido para o amarelo quase branco, e clarear mais so'
            // entregaria dois degraus indistinguiveis. Raio branco pelo mesmo motivo do azul do
            // SSJ2: contraste com a aura, nao harmonia.
            //
            // Por que o SSJ3 fica com um penteado FIXO enquanto SSJ e SSJ2 vestem o do proprio
            // personagem espetado (2026-09-10): com "Spiked" nos tres, os tres degraus teriam a
            // mesma malha e so' a cor os separaria. O comprido fixo mantem o SSJ3 legivel de longe
            // e de costas, que e' a razao de a chave existir. Ver [[Transformacoes]].
            Ssj3 = BindTransformation(config, SecSsj3,
                // Calibrados no playtest de 2026-09-07, o primeiro do SSJ3.
                powerMultiplier: 4f,
                // 15 -> 3 em 2026-09-20, com a escada inteira. Continua uma vez e meia o SSJ2.
                kiDrainPerSecond: 3f,
                punchSlashFraction: 0f,
                punchLightningFraction: 0.5f,
                carryWeightBonus: 300f,
                hairColor: "#FFE066",
                requiredGlobalKey: "defeated_bonemass",
                lightning: true,
                lightningColor: "#FFFFFF",
                // Brilha o dobro do SSJ, meio a mais que o SSJ2 — o mesmo passo de 0,5 por degrau.
                glowIntensity: 2f,
                hairItem: "SaiyaHair6");

            // O SSJ God atras do Moder — o quarto boss, mantendo o ritmo de um degrau por boss.
            // Decidido em 2026-09-21: ver [[Decisoes Tomadas]], "O degrau do Moder e' o SSJ God"
            // e "O SSJ God e' a forma do controle, nao da forca bruta".
            //
            // Este degrau NAO sobe o teto como os anteriores. O SSJ2 ja' ensinou que o problema de
            // uma forma forte demais e' absoluto — ela trivializa o conteudo —, e repetir o passo
            // de sempre so' repetiria o problema. O que o God compra e' folego:
            //   PowerMultiplier 3,5 = ABAIXO do SSJ3 (4), entre ele e o SSJ2. Saiu em 4,5 — pouco
            //     acima do SSJ3, seguindo a escada — e o playtest de 2026-09-22 inverteu o sinal:
            //     o God troca forca por folego, e nao "um pouco mais de tudo". E' o primeiro
            //     degrau da escada que bate menos que o anterior. Ver [[Transformacoes]].
            //   KiDrainPerSecond 2 = MENOS que o SSJ3 (3), no nivel do SSJ2. E' o primeiro degrau
            //     em que o dreno desce, de proposito: a forma do controle nao devora o dono.
            //   CombatKiCostScale 0,5 = metade da sobretaxa de combate. Com o CombatFormKiShare em
            //     0,2, o soco na maestria 0 custa 1,35x o da base, contra 1,6x no SSJ3 — mais
            //     barato que o degrau de baixo mesmo batendo mais forte.
            //   HealthRegenSpeed 3 = a cura passiva da comida chega tres vezes mais rapido, de 10
            //     em 10 segundos para a cada 3,3. Pedido do Henrique, e a unica forma que mexe
            //     nisso. Saiu em 2 e subiu no playtest de 2026-09-22.
            //   HealthRegenIgnoresBlockers = a cura atravessa Molhado, Frio e Congelando, que
            //     cortam ou zeram o multiplicador compartilhado da vanilla. Sem isso a forma do
            //     folego nao curaria nada na Montanha. Ver [[Transformacoes]].
            //   Soco meio contusao, meio CORTE, sem raio nenhum — os mesmos numeros do SSJ.
            //     Saiu partido em tres (0,33 de corte e 0,33 de raio), pela leitura de que o golpe
            //     repartido e' o menos punido por qualquer resistencia isolada; o playtest de
            //     2026-09-22 tirou o raio inteiro e devolveu o corte para 0,5. O raio e' o sabor
            //     do SSJ2 e do SSJ3, as duas formas da furia, e o God sem raio no corpo tambem
            //     nao bate com raio — o visual e o golpe passam a dizer a mesma coisa.
            //   CarryWeightBonus 350 = pouco acima do SSJ3, pelo mesmo motivo do multiplicador.
            //
            // Visual: sem raios (calma, nao furia), cabelo e aura vermelhos, e o penteado volta
            // ao espetado do proprio personagem, como no SSJ — o cabelo curto e' como o genero
            // desenha o God, e sem malha nova. O brilho segue o passo de 0,5 por degrau.
            SsjGod = BindTransformation(config, SecSsjGod,
                powerMultiplier: 3.5f,
                kiDrainPerSecond: 2f,
                // Calibrados no playtest de 2026-09-22, o primeiro do SSJ God.
                punchSlashFraction: 0.5f,
                punchLightningFraction: 0f,
                carryWeightBonus: 350f,
                hairColor: "#E8303A",
                requiredGlobalKey: "defeated_dragon",
                lightning: false,
                glowIntensity: 2.5f,
                hairItem: "Spiked",
                // Calibrado no playtest de 2026-09-22: saiu em 2, o tique de 5 s ainda demorava.
                healthRegenSpeed: 3f,
                healthRegenIgnoresBlockers: true,
                combatKiCostScale: 0.5f);

            // O primeiro degrau, atras do Eikthyr — a MESMA chave do SSJ, de proposito: matar o
            // primeiro boss entrega a forma e o ataque de uma vez, e vira um marco grande em vez de
            // dois mornos. Espacar custaria mexer numa trava de forma ja calibrada em playtest.
            //
            // Adicionar o ataque seguinte e' repetir esta chamada com outra secao, outros numeros e
            // a global key do boss dele.
            // Os quatro numeros de partida, ancorados no soco em vez de chutados no vazio:
            //   dano por poder 0,04 contra os 0,05 do PunchDamageFromPower — o tiro bate um pouco
            //   MENOS por acerto que o soco, que e' o que compra o direito de ser a distancia.
            //   A base subiu de 8 para 10 no playtest de 2026-08-16: e' a parcela que NAO escala
            //   com o poder, entao ela so se faz sentir cedo, que e' exatamente onde o tiro estava
            //   fraco demais para valer os 20 de ki.
            //   Custo 20 fixo: cedo o tiro e' caro, e vai ficando barato conforme a barra cresce e
            //   este numero nao. Comecou em 8, ancorado nos ~3,75 de um soco no comeco do jogo, e o
            //   playtest de 2026-08-07 subiu para 20 — 2,5x. Isso NAO responde a pergunta do fim de
            //   jogo, so a atrasa: ver [[Em Aberto]].
            // O saiya_blast imprime dano/ki dos dois lado a lado — e' por ali que a calibracao sai.
            KiBlast = BindKiAttack(config, SecKiBlast,
                // 10 / 0,04 ate' 2026-09-17, quando o blast ganhou area de 2 m. A area da' dano
                // CHEIO a cada alvo no raio, e o Henrique pediu compensar no dano: -10%, que contra
                // alvo sozinho quase nao se sente e com dois alvos ja' entrega mais que antes.
                damageBase: 9f,
                damageFromPower: 0.036f,
                kiCost: 20f,
                cooldown: 0.5f,
                // 2 m, escolha do Henrique em 2026-09-17 antes de playtest. Danifica construcao.
                impactRadius: 2f,
                // Escolhido no playtest de 2026-08-20, ganhando do fireball Dvergr e do
                // staff_greenroots_projectile. O estouro dele traz som e clarao bons e uma fumaca
                // que nao combina com tiro de energia — dai o Strip abaixo, e nao um none no
                // ImpactEffect, que levaria os tres juntos.
                projectilePrefab: "GoblinShaman_projectile_fireball",
                impactEffect: "",
                // Confirmado na tela em 2026-08-20: o log listou os emissores smoke, fire e shockwave
                // dentro do fx_shaman_fireball_expl. Fora os dois primeiros, sobra o clarao, a
                // onda de choque e o som — que sao a parte boa do estouro.
                impactEffectStrip: "smoke, fire",
                // Vazio: o estouro sai na cor do proprio tiro. Ver ImpactColor.
                impactColor: "",
                // Amarelo de ki, aprovado na tela em 2026-08-20.
                projectileColor: "#FFFF00",
                requiredGlobalKey: "defeated_eikthyr");

            // O segundo degrau, atras do Bonemass — e a escada de ataques deixa de andar junto com
            // a de bosses aqui. O blast saiu no Eikthyr, este sai no terceiro boss, e os dois do
            // meio nao entregam ataque nenhum: um ataque por boss encheria a escada de degraus
            // mornos so' para preencher a tabela, e nao ha' cinco ataques que valham a pena.
            // Decidido em 2026-09-07. Ver [[Ataques de Ki]].
            //
            // Feixe, e nao um feixe: o projectile_beam do Yagluth foi validado na tela em
            // 2026-09-07 e NAO e' um raio sustentado — o boss dispara varios em sequencia, e o que
            // se le como feixe e' a fila. O mod faz o mesmo, e o feixe vira BeamCount +
            // BeamInterval em vez de um sistema de renderizacao novo. Ver Attacks/KiBeam.cs.
            //
            // Os numeros de partida, ancorados no ki blast e nao chutados:
            //   24 projeteis x 0,025 s = 0,6 s de feixe, com 1,25 m entre um e o seguinte a 50 m/s.
            //   O espacamento comecou em 2,5 m e caiu pela metade no playtest de 2026-09-07: a
            //   fila ficava visivel como fila. O que desceu foi o INTERVALO e nao a velocidade —
            //   espacamento e' velocidade x intervalo, e baixar a velocidade encurtaria o alcance
            //   e faria o feixe viajar devagar, que e' o oposto do que um Kamehameha parece.
            //   Dobrar a contagem para manter os 0,6 s obrigou a METADE do dano e do custo por
            //   projetil: contagem e' knob visual E de balanceamento ao mesmo tempo, porque as duas
            //   coisas sao por projetil. Os totais abaixo sao os mesmos de antes.
            //   Dano 1,75 + 0,00625 x poder POR PROJETIL = 42 + 0,15 x poder no feixe inteiro,
            //   contra os 10 + 0,04 do blast. Custo 2,5 por projetil = 60 por disparo, tres blasts.
            //   Da' ~1,3x o dano por ki do blast — o premio por ser o ataque do terceiro boss, por
            //   custar 2 s de carregamento e por so' entregar tudo se o feixe inteiro acertar.
            //   Empurrao 3, e nao os 30 do blast: com 12 acertos seguidos, o empurrao do blast
            //   jogaria o alvo para fora do proprio feixe no segundo projetil.
            // O saiya_blast imprime os totais do feixe — e' por ali que a calibracao sai.
            Kamehameha = BindKiAttack(config, SecKamehameha,
                // Playtest de 2026-09-07, e os tres andam juntos porque o feixe cheio triplicou
                // de tamanho: 60 projeteis x 1 de base = 60 de dano por carga cheia, contra os 42
                // que os 24 x 1,75 davam. O dano por projetil CAIU e o total subiu — que e' o que
                // mantem cada bolinha do feixe legivel como bolinha, e nao como um tiro que mata.
                damageBase: 1f,
                damageFromPower: 0.008f,
                // 2 x 60 = 120 de ki na carga cheia, o dobro dos 60 de antes. Com 5 s de carga sao
                // os mesmos 24 ki/s de barra descendo — a velocidade nao mudou, a aposta e' que
                // ficou maior.
                kiCost: 2f,
                // 2 s, e nao os 4 de antes: com o carregamento, quem limita a cadencia passou a
                // ser o dedo do jogador na tecla. Cooldown longo em cima de carga longa e' o mesmo
                // castigo cobrado duas vezes.
                cooldown: 2f,
                projectilePrefab: "projectile_beam",
                // Vazio: o estouro que vem com o prefab e' o fx_goblinking_beam_hit, feito para
                // este feixe. Ligar o VerboseLogging e atirar uma vez lista os emissores dele, que
                // e' de onde sai um ImpactEffectStrip se sobrar fumaca. Mesmo caminho do blast.
                impactEffect: "fx_shaman_fireball_expl",
                // Fumaca e fogo fora: sao 60 estouros num feixe, e o que em UM tiro le como
                // impacto, em sessenta vira uma cortina que esconde o alvo. Playtest de 2026-09-07.
                impactEffectStrip: "smoke, fire",
                impactColor: "",
                // Azul claro. O blast e' amarelo; o Kamehameha precisa se distinguir dele na tela
                // antes de qualquer outra coisa, e azul e' a cor da cena no anime.
                projectileColor: "#66CCFF",
                // O Elder, e nao o Bonemass: calibrado em playtest e promovido do .cfg para o
                // codigo em 2026-09-15, antes da primeira publicacao no Thunderstore. Esperar o
                // terceiro boss deixava o segundo sem entrega nenhuma para quem usa ki.
                requiredGlobalKey: "defeated_gdking",
                // Teto, e nao valor fixo: 60 e' o feixe da carga CHEIA. Com 5 s de carregamento
                // sao 12 projeteis por segundo segurado, e o custo e o dano acompanham em linha
                // reta — segurar metade do tempo entrega metade de tudo.
                //
                // 24 na tela nao lia como feixe: com 0,025 s de intervalo eram 0,6 s de disparo, e
                // o que se via era uma rajada curta. Playtest de 2026-09-07 subiu para 60, e a
                // cadencia por segundo ficou igual — o que mudou foi quanto tempo o feixe DURA.
                beamCount: 60,
                // 0,03 e nao 0,025: calibrado no .cfg e promovido em 2026-09-15. A 50 m/s sao 1,5 m
                // entre um projetil e o seguinte.
                beamInterval: 0.03f,
                knockback: 3f,
                projectileSpeed: 50f,
                // 5 s: com 50 m/s sao 250 m de alcance. O feixe agora dura 1,5 s saindo da mao, e
                // com 2 s de vida a cabeca dele morria no ar enquanto a cauda ainda estava
                // nascendo. Playtest de 2026-09-07.
                projectileLifetime: 5f,
                // Cada projetil e' o DOBRO do tamanho do prefab. Sessenta bolinhas finas em fila
                // leem como tracejado; grossas o bastante, elas se encostam e viram feixe. E' a
                // metade visual da mesma decisao do beamCount.
                projectileScale: 2f,
                // 5 s ate' a carga cheia, e nao os 2 de projeto. E' longo de proposito: o
                // Kamehameha tem que ser uma aposta, uma janela em que o jogador esta' parado com o
                // inimigo vindo. Curto demais e ele vira um ki blast mais caro — e 2 s, com a pose
                // de duas maos entrando em 0,25 s e a bola crescendo, mal davam tempo de o gesto
                // ser lido antes de acabar. Playtest de 2026-09-07.
                chargeTime: 5f,
                // 0,15 x 60 = 9 projeteis no minimo. Encostar na tecla sem querer nao gasta nada.
                minChargeRatio: 0.15f,
                // 0,2 e nao 0,4: a bola comeca menor para o CRESCIMENTO ser o que se le na tela.
                // Com 0,4 ela ja nascia quase do tamanho final e a carga nao aparecia na mao.
                // Promovido do .cfg em 2026-09-15.
                chargeMinScale: 0.2f,
                // Vazio, e foi assim que se jogou: o carregamento de cajado dos Charred
                // (fx_charred_firestaff_chargeup) entrou em 2026-09-07 e saiu no .cfg logo depois —
                // com a bola na mao crescendo, as particulas convergindo por cima viravam sujeira.
                // O que le como carga e' a bola. Promovido do .cfg em 2026-09-15.
                chargeEffectPrefab: "",
                // Vazio: a bola na mao sai da cor do que vai sair dela. Ver ChargeEffectColor.
                chargeEffectColor: "",
                chargeEffectScale: 1f,
                // Zerados porque o ponto de fixacao passou a ser a PALMA: a altura 1 e o lado 0,35
                // de antes eram a mao medida a partir dos pes, e repeti-los aqui poria o efeito um
                // metro acima da mao. Ver ChargeEffectAnchor.
                chargeEffectHeight: 0f,
                chargeEffectSide: 0f,
                // 13 cm a' frente da palma, medidos no eixo do JOGADOR — a bola sai de dentro da
                // mao e fica na frente dela. Calibrado em 2026-09-07, junto com a pose, e so' foi
                // calibravel depois de o offset deixar de ser medido nos eixos do osso.
                chargeEffectForward: 0.13f,
                chargeEffectAnchor: EffectAnchor.RightHand,
                // A bola de ki do xama goblin: a mesma esfera do ki blast, ja' aprovada na tela em
                // 2026-08-20. Junta na mao a bola que vai sair dela.
                chargeBallPrefab: "GoblinShaman_projectile_fireball",
                chargeBallColor: "",
                chargeBallScale: 1f,
                // Palpite, e nao medido: 'smoke' e' o nome que a maioria dos prefabs de projetil do
                // jogo usa para o rastro, e e' o mesmo que o estouro do ki blast cobrou. Se o nome
                // for outro neste prefab, isto nao faz nada e nao quebra nada — o log lista os
                // nomes de verdade, e a chave se corrige em uma linha.
                // 'flames_world', e nao o 'smoke' que era palpite: o log listou os emissores de
                // verdade deste prefab em 2026-09-07. As chamas sao o que fazia a bola parada na
                // mao parecer uma tocha em vez de uma esfera de energia.
                chargeBallStrip: "flames_world",
                // Escolhido em 2026-09-07. Estouro curto e branco: le como "encheu" sem competir
                // com a bola azul que ja' esta' na mao.
                chargeFullEffectPrefab: "vfx_blocked",
                // Vazio: a mesma cor do tiro, como a bola. Ver ChargeFullEffectColor.
                chargeFullEffectColor: "",
                chargeFullEffectScale: 1f,
                chargeFullEffectLoop: false,
                chargeFullEffectReplaces: true);

            // --- Voo ---
            // 5 -> 3,5 em 2026-09-20. O sintoma vem das issues 1 e 2 do GitHub: a 5/s contra uma
            // barra de 50 no nivel 0, o comeco do jogo da' DEZ segundos de voo, e a resposta que os
            // jogadores acharam sozinhos foi parar de jogar para farmar. Baixar a base e' metade do
            // conserto; a outra metade e' a KiSkillCurve, que adiantava o alivio para depois do
            // nivel 75. Ver Melhorias, "Voo: XP por distancia, e a forma paga o que acelera".
            FlightKiPerSecond = config.Bind(SecFlight, "KiPerSecond", 3.5f,
                new ConfigDescription(
                    "Ki per second while flying. Flight should be a tool, not the default way to " +
                    "get around — otherwise the game's hostile terrain turns into scenery. " +
                    "Running out of ki in the air drops you. " +
                    "(Playtest value, 2026-08-13. Went 4 → 15 on 2026-07-31, because at 4 flight " +
                    "was cheap enough to become the default way to travel, then 15 → 10 while " +
                    "playing the ki attack stage, then 10 → 5: with BaseSpeed at 2 the early-game " +
                    "flight is slow enough to hold itself back without the ki cost doing it too. " +
                    "5 → 3.5 on 2026-09-20: at 5 the level-0 bar bought ten seconds of flight, and " +
                    "players answered that by grinding the skill instead of playing.)",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(100)));

            FlightFastKiMultiplier = config.Bind(SecFlight, "FastKiMultiplier", 2f,
                new ConfigDescription(
                    "Ki cost multiplier while the Run button is held. " +
                    "(Playtest value, 2026-08-13. Was 2.5, above FastSpeedMultiplier on purpose " +
                    "so fast flight cost more per travelled metre; now equal to it, which makes " +
                    "the metre cost the same fast or slow and turns the Run button into a pure " +
                    "time-saver. Raise it above FastSpeedMultiplier to get the penalty back.)",
                    new AcceptableValueRange<float>(1f, 10f), AdminOnly(95)));

            // 15 era o valor da primeira versão e fazia o modo rápido bater o MaxSpeed (15 x 2 = 30)
            // ainda no nível 0 — nenhuma progressão aparecia com o shift pressionado. Ao mexer
            // aqui, conferir se BaseSpeed * FastSpeedMultiplier ainda sobra bem abaixo do MaxSpeed.
            //
            // 2 e um piso deliberadamente miseravel: e velocidade de caminhada no Valheim (correr
            // e ~5). Voar cedo no jogo e mais lento que andar, e quem paga a velocidade e o
            // SpeedFromPower. Voo virou privilegio de quem ja e forte, nao meio de transporte.
            // 0,5 -> 1 em 2026-09-20. O desconto tinha um motivo bom — parar no ar para mirar,
            // olhar em volta ou conversar nao pode custar o mesmo que atravessar o mapa — e um
            // efeito colateral ruim: pairar parado E' a postura do cheese de boss que as issues
            // relatam, e o mod estava pagando metade do preco por ela. Quem precisa parar no ar
            // fora de combate paga o preco cheio, que nao e' punicao; quem para no ar EM COMBATE
            // paga a sobretaxa abaixo, que e'.
            FlightHoverKiMultiplier = config.Bind(SecFlight, "HoverKiMultiplier", 1f,
                new ConfigDescription(
                    "Ki cost multiplier while hovering — airborne with no movement input at all, " +
                    "not even rising or descending. Below 1 holding position is cheaper than " +
                    "travelling, which is how this shipped until 2026-09-20; it was raised to 1 " +
                    "because hovering is exactly the posture used to cheese bosses from out of " +
                    "reach, and the discount was paying for it. See CombatHoverMultiplier for " +
                    "the part that only charges when something is actually fighting you.",
                    new AcceptableValueRange<float>(0f, 1f), AdminOnly(94)));

            // O recorte que separa o cheese do uso legitimo. Altitude nao serve para isso: a faixa
            // em que se fica fora do alcance de um boss (uns 10 m) e' a mesma em que se voa para
            // nao bater em arvore, entao encarecer por altura puniria viajar sem resolver o cheese.
            // As duas variaveis que de fato distinguem os dois casos sao PARADO e EM COMBATE.
            //
            // So' pairando: voar em combate continua no preco normal, porque mergulhar, girar e
            // sair e' lutar no ar — que e' a fantasia do mod, nao o problema.
            FlightCombatHoverMultiplier = config.Bind(SecFlight, "CombatHoverMultiplier", 2f,
                new ConfigDescription(
                    "Extra ki cost multiplier for HOVERING while something hostile is alerted " +
                    "nearby — hanging in the air out of reach while a boss or a pack cannot touch " +
                    "you. Stacks on top of HoverKiMultiplier. " +
                    "Flying in combat is NOT affected: diving, circling and pulling out is air " +
                    "combat, which is the point of the mod. Only holding still is. " +
                    "1 turns it off. " +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<float>(1f, 10f), AdminOnly(93)));

            FlightCombatHoverRange = config.Bind(SecFlight, "CombatHoverRange", 30f,
                new ConfigDescription(
                    "How far, in meters, an alerted hostile counts for CombatHoverMultiplier. " +
                    "Measured in 3D from your body, so something 30 m below you still counts — " +
                    "which is the whole point, since that is where it is when you are out of its " +
                    "reach. Tamed creatures and other players never count.",
                    new AcceptableValueRange<float>(5f, 100f), AdminOnly(92)));

            FlightBaseSpeed = config.Bind(SecFlight, "BaseSpeed", 2f,
                new ConfigDescription(
                    "Flight speed floor: skill 0, battle power 0, carrying nothing. " +
                    "Everything else is added or multiplied on top of this. " +
                    "CAREFUL: the flight skill bonus MULTIPLIES this floor, so lowering it also " +
                    "shrinks what levelling the skill is worth — at 2, a hundred levels of Flight " +
                    "buy +1 m/s on a fresh character. That is intended here: almost all of the " +
                    "speed is meant to come from SpeedFromPower. " +
                    "(Playtest value, 2026-07-31. Started at 15, went to 10, landed on 2.)",
                    new AcceptableValueRange<float>(1f, 100f), AdminOnly(90)));

            FlightSpeedFromPower = config.Bind(SecFlight, "SpeedFromPower", 0.015f,
                new ConfigDescription(
                    "Speed ADDED per point of raw battle power. With ki on, power is " +
                    "k1*HP + k4*PowerLevel — so eating better and fighting more both make you " +
                    "fly faster, which is the Dragon Ball reading of getting stronger. " +
                    "Additive, like punch damage: enemy scaling is roughly linear across biomes " +
                    "and an additive stat tracks that predictably. " +
                    "Run saiya_fly to see how much this is contributing right now.",
                    new AcceptableValueRange<float>(0f, 1f), AdminOnly(88)));

            FlightFormSpeedShare = config.Bind(SecFlight, "FormSpeedShare", 0.3f,
                new ConfigDescription(
                    "How much of a transformation's power gain turns into flight speed: the form " +
                    "multiplies speed by 1 + (PowerMultiplier - 1) * this. At 0.3, SSJ's x2 power " +
                    "is x1.3 speed and SSJ2's x3 is x1.6. " +
                    "Transformations are meant to be felt in combat first; letting the raw " +
                    "PowerMultiplier through sent flight straight into MaxSpeed, where extra " +
                    "power buys no actual velocity and SSJ and SSJ2 stop being distinguishable " +
                    "in the air. " +
                    "1 restores the old behaviour (form speed = PowerMultiplier), 0 makes forms " +
                    "not affect flight speed at all. " +
                    "(Playtest value, 2026-08-21: flying transformed felt too fast while base " +
                    "form felt right.)",
                    new AcceptableValueRange<float>(0f, 1f), AdminOnly(86)));

            FlightFastSpeedMultiplier = config.Bind(SecFlight, "FastSpeedMultiplier", 2f,
                new ConfigDescription(
                    "Speed multiplier while the Run button is held. Vanilla already reads that " +
                    "button inside its own flight code, so fast flight costs no extra keybind. " +
                    "(Playtest value, 2026-08-13. Raised from 1.8.)",
                    new AcceptableValueRange<float>(1f, 10f), AdminOnly(85)));

            FlightVerticalSpeedFactor = config.Bind(SecFlight, "VerticalSpeedFactor", 1f,
                new ConfigDescription(
                    "Climb and dive speed, as a fraction of the horizontal speed. " +
                    "1.0 goes up as fast as forward, so altitude costs no more than distance. " +
                    "(Playtest value, 2026-08-13. Went 0.75 → 0.5 on 2026-07-31, when climbing " +
                    "felt too quick; back to 1 after BaseSpeed dropped to 2 — with the horizontal " +
                    "speed that low, half of it made gaining altitude a crawl.)",
                    new AcceptableValueRange<float>(0f, 2f), AdminOnly(80)));

            FlightTurnSpeed = config.Bind(SecFlight, "TurnSpeed", 200f,
                new ConfigDescription(
                    "Turn speed in the air, in degrees per second. Goes straight into " +
                    "m_flyTurnSpeed. The vanilla value is 12, which is a flying-creature number: " +
                    "at 12 a 180-degree turn takes fifteen seconds, and the 2026-07-31 playtest " +
                    "reported exactly that. 200 turns around in about a second. Lower feels " +
                    "heavier and makes high speed harder to steer.",
                    new AcceptableValueRange<float>(1f, 720f), AdminOnly(75)));

            FlightSpeedSkillBonus = config.Bind(SecFlight, "SpeedSkillBonus", 0.5f,
                new ConfigDescription("Speed bonus at level 100 of the flight skill. 0.5 = +50%.",
                    new AcceptableValueRange<float>(0f, 3f), AdminOnly(70)));

            // 0.95 e nao 0.5 desde o playtest de 2026-08-13: no linear o nivel 100 so' cortava
            // metade do custo, e maximizar a unica skill que o voo tem precisa se PARECER com ter
            // maximizado alguma coisa. O que segura o comeco do jogo agora e' a curva abaixo, nao
            // este numero.
            FlightKiSkillReduction = config.Bind(SecFlight, "KiSkillReduction", 0.95f,
                new ConfigDescription(
                    "Fraction of the ki cost removed at level 100 of the flight skill. " +
                    "0.95 = at max level flying costs a twentieth. Together with SpeedSkillBonus " +
                    "this is the whole progression of the skill: farther per point of ki. " +
                    "KiSkillCurve decides how much of it arrives before level 100.",
                    new AcceptableValueRange<float>(0f, 0.99f), AdminOnly(65)));

            // O expoente e' o que separa "voo barato no fim" de "voo barato". Com a reducao em
            // 0.95 e curva 1 (linear) o nivel 50 ja' pagaria metade do preco, e o voo viraria o
            // transporte padrao antes de o jogador ter treinado nada. Em 2 a mesma reducao chega
            // quase toda depois do nivel 75: 50 -> -24%, 75 -> -53%, 90 -> -77%, 100 -> -95%.
            //
            // ⚠️ 2 -> 1 em 2026-09-20, e o paragrafo acima descreve exatamente o que se esta'
            // trocando — de propósito. O back-load era o que mantinha o voo caro cedo, e foi ele
            // que produziu a queixa da issue 2: "fui tirado do jogo por um tempo so' para grindar
            // uma skill antes de ela ficar usavel". Quem paga o voo cedo passa a ser so' o
            // KiPerSecond (3,5), e a reta entrega o alivio onde o jogador esta': 25 -> -24%,
            // 50 -> -48%, 75 -> -71%, 100 -> -95%.
            //
            // O risco anotado em 2026-08-13 — voo virar o transporte padrao no meio do jogo —
            // continua real e agora nao tem contrapeso nenhum no custo: o FlightKiPowerReduction
            // abaixo nao faz nada com o K5 em zero. E' o numero a vigiar no playtest, e se ceder,
            // cede aqui (1 -> 1,4 poe o nivel 50 em -33%) e nao no KiPerSecond, que calibra o
            // comeco. Ver Melhorias, "Voo: XP por distancia, e a forma paga o que acelera".
            FlightKiSkillCurve = config.Bind(SecFlight, "KiSkillCurve", 1f,
                new ConfigDescription(
                    "Shape of the flight skill discount: reduction = KiSkillReduction * " +
                    "(level/100)^this. 1 is a straight line, so half the skill gives half the " +
                    "discount. Above 1 the saving is back-loaded — the last levels are where " +
                    "flight actually gets cheap, which is what keeps early flight expensive " +
                    "while still making level 100 feel like an arrival. Below 1 front-loads it. " +
                    "Check it with saiya_fly skill <level>.",
                    new AcceptableValueRange<float>(0.25f, 5f), AdminOnly(64)));

            // Hiperbolico e nao linear: a entrada nao tem teto (o termo de fim de jogo cresce sem
            // limite), e um (1 - r * bonus) atravessaria o zero e viraria custo negativo — voar
            // dando ki. O 1/(1 + r*x) decai para sempre sem nunca chegar a zero, mesma forma do
            // ApplyArmor do proprio Valheim.
            //
            // O default derruba o custo pela METADE no nivel 100 com K5 = 3 (bonus 300 x 0.0033 = 1,
            // logo fator 1/2). Multiplica com o KiSkillReduction, entao um jogador no topo das duas
            // skills paga 15 x 0.5 x 0.5 = 3.75/s. Se o playtest disser que voo ficou barato demais
            // no fim, este e o numero a baixar — nao o KiPerSecond, que calibra o comeco.
            //
            // ⚠️ Com o K5 em 0, que e' o default desde 2026-08-23, esta chave nao faz NADA: ela le
            // o termo de fim de jogo sozinho, e ele vale zero. O desconto de voo no late game
            // simplesmente deixou de existir, e a unica progressao que sobrou no ar e' a skill de
            // voo (KiSkillReduction). Nao e' bug — e' o preco de desligar o K5, e esta anotado aqui
            // para nao virar um misterio de "por que voar nao barateia mais".
            FlightKiPowerReduction = config.Bind(SecFlight, "KiPowerReduction", 0.0033f,
                new ConfigDescription(
                    "How much the late-game power term (Battle Power.K5_LateGameBonus) cheapens " +
                    "flight, hyperbolically: cost is multiplied by 1 / (1 + this * bonus). " +
                    "This is the ONLY thing the late-game term changes about flying — speed is " +
                    "deliberately left out of it, because speed already runs into MaxSpeed, which " +
                    "is a zone-streaming limit rather than balance. What being strong buys in the " +
                    "air is range, not velocity. " +
                    "Reads the late-game term alone and not total power, so early flight stays as " +
                    "expensive as KiPerSecond says — the reward belongs to the end of the game. " +
                    "0 disables it. Check it with saiya_fly.",
                    new AcceptableValueRange<float>(0f, 0.1f), AdminOnly(63)));

            // Por METRO, e nao por segundo no ar (2026-09-20). Por tempo, pairar parado pagava
            // exatamente o mesmo que atravessar o mapa — e pagava mais barato, porque o
            // HoverKiMultiplier corta o custo pela metade justamente quando nao ha deslocamento.
            // O farm otimo era ficar parado no ar, e foi o que a issue 2 do GitHub relatou.
            //
            // Nao basta "XP enquanto se move": o IsHovering le o m_moveDir, ou seja INPUT. Voar
            // contra um paredao segurando W pagaria XP cheio parado, e como o voo rapido custa 2x
            // pelo mesmo XP por segundo, o otimo passaria a ser se mover o mais devagar possivel.
            // Por distancia o XP acompanha o que custa ki: XP por ki gasto fica constante entre o
            // voo normal e o rapido, e a unica forma de farmar e' voar de verdade.
            //
            // 0,15 e' o equivalente dos 0,5/s de antes na velocidade base. Ver Melhorias,
            // "Voo: XP por distancia, e a forma paga o que acelera".
            // A forma paga pela velocidade que ela da'. Ate 2026-09-20 voar transformado so
            // somava o dreno da forma e ganhava velocidade de graca por cima; com o dreno da forma
            // caindo para 1-3/s no rework do custo de ki, essa soma virou troco.
            //
            // Ancorado no que a forma ENTREGA e nao no multiplicador cheio: ela so converte uma
            // fracao do poder em velocidade (FormSpeedShare), entao cobrar pelo multiplicador
            // inteiro cobraria por uma coisa que ela nao da'. Chave propria e nao a mesma do
            // FormSpeedShare porque numero de balanceamento nao se compartilha entre dois eixos.
            //
            // Quem paga e' a MAESTRIA da forma, nao a skill de voo. Tres motivos: quem cobra a
            // sobretaxa e' a forma; a maestria ja' e' a moeda do custo de ki no resto do mod; e
            // com a skill de voo o desconto seria duplo (ela ja' barateia o custo base) e o farm
            // voando voltaria pela porta dos fundos — o jogador farmaria voo para baratear voar
            // transformado. A consequencia aceita e' que voar transformado nao treina nada que
            // barateie voar transformado: treina-se lutando dentro da forma.
            FlightFormKiShare = config.Bind(SecFlight, "FormKiShare", 0.3f,
                new ConfigDescription(
                    "How much a transformation makes flying cost, as a fraction of the power it " +
                    "adds: cost is multiplied by 1 + (PowerMultiplier - 1) * this * (1 - mastery). " +
                    "At 0.3, SSJ (x2 power) makes flight 30% more expensive and SSJ3 (x4) 90%, " +
                    "and both fade to nothing at level 100 of THAT form's mastery. " +
                    "It mirrors FormSpeedShare deliberately: the form pays for the speed it gives. " +
                    "Note the payer is the form's mastery, which trains by FIGHTING transformed — " +
                    "flying transformed does not make flying transformed any cheaper. " +
                    "0 gives the form's flight speed away for free, which is what happened before " +
                    "2026-09-20. " +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<float>(0f, 2f), AdminOnly(62)));

            FlightXpPerMeter = config.Bind(SecFlight, "XpPerMeter", 0.075f,
                new ConfigDescription(
                    "Flight skill XP per METER flown. Flying is its own training — there is no " +
                    "other way to raise it — but hovering in place pays nothing, because nothing " +
                    "moves. Distance here is the path travelled, not the distance from where you " +
                    "took off: flying out and back pays for both legs, since both cost ki. " +
                    "Valheim's own diminishing curve up to 100 applies. " +
                    "(Replaced XpPerSecond on 2026-09-20 — 0.15/m is what 0.5/s was worth at base " +
                    "speed. The old key stays behind in existing config files, inert. Halved to " +
                    "0.075 on 2026-09-20: the first playtest of the rework levelled far too fast.)",
                    new AcceptableValueRange<float>(0f, 5f), AdminOnly(60)));

            // 0.8 veio do playtest de 2026-09-17 (comecou em 0.75, escolha do Henrique antes de
            // testar). Abaixo de 1 de proposito:
            // zerar tiraria a consequencia da queda de vez. Linear e nao com curva como o custo de
            // ki: aqui nao ha o risco de o voo virar transporte padrao cedo demais.
            FlightFallDamageSkillReduction = config.Bind(SecFlight, "FallDamageSkillReduction", 0.8f,
                new ConfigDescription(
                    "Fraction of fall damage removed at level 100 of the flight skill, linear in the " +
                    "level (0.8 = 40% less at level 50, 80% less at 100). Only while ki is on — " +
                    "an empty bar still protects, turning ki off does not. Flying itself never " +
                    "takes fall damage; this is for jumping off cliffs and dropping out of flight. " +
                    "Stacks multiplicatively with the game's own fall protection (feather cape). " +
                    "0 disables it, 1 makes level 100 immune.",
                    new AcceptableValueRange<float>(0f, 1f), AdminOnly(58)));

            FlightWeightPenalty = config.Bind(SecFlight, "WeightPenalty", 0.6f,
                new ConfigDescription(
                    "Fraction of the speed lost with the inventory at maximum weight. " +
                    "0.5 = flying fully loaded you go at half speed. " +
                    "(Playtest value, 2026-08-11. Raised from 0.5: hauling a full inventory by air " +
                    "should cost something the player can feel.)",
                    new AcceptableValueRange<float>(0f, 0.95f), AdminOnly(55)));

            // Expoente e nao hiperbolico. O 1/(1 + r*x) do custo de ki tem a forma OPOSTA a esta:
            // ele desaba na entrada e achata no fim. La ele e obrigatorio porque a entrada nao tem
            // teto; aqui a carga vive em 0-1, entao da' para escolher a forma pelo expoente.
            // Em 5, com a penalidade em 0.6: 50% de carga -> -2%, 75% -> -14%, 90% -> -35%,
            // 100% -> -60%. Praticamente so' o inventario cheio pesa, que e' o pedido: recolher
            // coisa no caminho nao pode virar pedagio de voo.
            FlightWeightCurve = config.Bind(SecFlight, "WeightCurve", 5f,
                new ConfigDescription(
                    "Shape of the weight penalty: speed loss = WeightPenalty * " +
                    "(carried/max)^this. 1 is a straight line, so half loaded costs half the " +
                    "penalty. Above 1 the penalty is back-loaded — light and medium loads barely " +
                    "register and the speed falls off sharply as the inventory fills, which is " +
                    "what keeps casual looting from feeling taxed. Below 1 front-loads it. " +
                    "(Playtest value, 2026-09-06. Went straight to the top of the range: at 3 the " +
                    "penalty was still noticeable at half load, and the whole point of the curve " +
                    "was that only a full inventory should slow you down.)",
                    new AcceptableValueRange<float>(0.25f, 5f), AdminOnly(54)));

            FlightMinPowerLevel = config.Bind(SecFlight, "MinPowerLevel", 0f,
                new ConfigDescription(
                    "Minimum Power Level required to take off. 0 disables the gate. " +
                    "A placeholder for the boss gating of step 7 — until that decision is made, " +
                    "this is the only lock available on flight.",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(50)));

            FlightMaxSpeed = config.Bind(SecFlight, "MaxSpeed", 30f,
                new ConfigDescription(
                    "Hard speed cap, applied after every multiplier. An engine limit, not balance: " +
                    "above a certain speed zone streaming cannot keep up and the world loads in " +
                    "chunks.",
                    new AcceptableValueRange<float>(5f, 100f), AdminOnly(45)));

            // --- Battle Power ---
            // Sao DUAS formulas, porque os dois caminhos de progressao sao disjuntos:
            //   ki desligado: poder = k1*HP + k2*dano_arma + k3*armadura
            //   ki ligado:    poder = k1*HP + k4*nivel_power_level
            // Arma e armadura nao sobrevivem ao modo ki: arma da zero (o jogador soca) e
            // armadura vira laco de realimentacao, porque ela passou a ser DERIVADA do poder.
            PowerK1Health = config.Bind(SecPower, "K1_Health", 1f,
                new ConfigDescription(
                    "Weight of HP in the battle power. Applies to both formulas. Only HP ABOVE the " +
                    "game's minimum (25) counts: those 25 are handed to any new character for free, " +
                    "and counting them would give everyone a floor of power nobody earned. " +
                    "(Fixed in the 2026-07-30 playtest.)",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(100)));

            PowerK2WeaponDamage = config.Bind(SecPower, "K2_WeaponDamage", 2f,
                new ConfigDescription(
                    "Weight of the equipped weapon's damage. Only used in the ki-OFF formula.",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(90)));

            PowerK3Armor = config.Bind(SecPower, "K3_Armor", 1.5f,
                new ConfigDescription(
                    "Weight of equipment armor. Only used in the ki-OFF formula — " +
                    "with ki on, armor is an output of the battle power, not an input.",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(80)));

            PowerK4PowerSkill = config.Bind(SecPower, "K4_PowerSkill", 3f,
                new ConfigDescription(
                    "Weight of the Power Level skill (0-100). Only used in the ki-ON formula. " +
                    "It is the only axis of progression for a ki user — without it, power would be " +
                    "constant from the first boss to the last.",
                    new AcceptableValueRange<float>(0f, 1000f), AdminOnly(70)));

            // Somado ao K4, nao multiplicado nele. Um expoente sobre a parcela da skill so
            // redistribui um total fixo: para render mais no fim ele tira do meio, e o mid-game
            // fica mais fraco do que ja e. Somando um termo separado, o K4 de hoje continua
            // intocado e o novo so pesa onde o grind aperta.
            //
            // DESLIGADO por default desde o playtest de 2026-08-23. O termo dobrava o poder de
            // combate no nivel 100, e como soco, armadura e block power leem esse mesmo numero,
            // ele empurrava os tres de uma vez — cada um calibrado contra uma escala diferente do
            // jogo. A mecanica continua inteira e a um config de distancia; o que mudou e' que ela
            // nao esta' ligada enquanto nao houver uma calibragem que segure os tres consumidores
            // juntos. Ver ArmorFromPower e Flight.KiPowerReduction, que foram dimensionados com
            // ele ligado.
            PowerK5LateGame = config.Bind(SecPower, "K5_LateGameBonus", 0f,
                new ConfigDescription(
                    "Power delivered by the late-game term AT LEVEL 100, on top of K4. It exists " +
                    "because levelling gets brutally more expensive but the reward did not: going " +
                    "from 99 to 100 costs a THOUSAND times what 0 to 1 costs (Valheim's curve is " +
                    "(level+1)^1.5), yet power rose a flat K4 every level. This term makes each " +
                    "level worth more than the one before it. " +
                    "AFFECTS COMBAT ONLY: punch damage, armor, block power and the displayed " +
                    "number. Flight speed and the ki cap deliberately ignore it — see " +
                    "Flight.KiPowerReduction for what late game buys in the air. " +
                    "0 turns the term off and restores the original linear formula exactly — " +
                    "which is the default since the 2026-08-23 playtest, where doubling combat " +
                    "power at level 100 dragged punch damage, armor and block power up together.",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(65)));

            PowerLateGameExponent = config.Bind(SecPower, "LateGameExponent", 5f,
                new ConfigDescription(
                    "How LATE the K5 term wakes up. The term is normalised on level 100, so this " +
                    "never changes what it delivers at the top — only how much of it arrives " +
                    "early. At 5, level 50 has just 3% of the bonus and level 75 has 24%: almost " +
                    "all of it lands in the last quarter, which is exactly the stretch that costs " +
                    "half the total grind. Lower spreads it out, higher concentrates it further.",
                    new AcceptableValueRange<float>(1f, 10f), AdminOnly(64)));

            // --- Poder de luta escaneável (etapa 10) ---
            // Duas entradas, e só duas, porque são as únicas grandezas que jogador e bicho têm em
            // comum. Defesa ficou de fora de propósito: Character.GetBodyArmor() devolve 0 para
            // todo inimigo do jogo, então um termo de armadura só pesaria de um lado da
            // comparação — e a comparação é a razão de o número existir. Ver PowerRating.
            RatingK1Health = config.Bind(SecPower, "RatingHealthWeight", 0.8f,
                new ConfigDescription(
                    "Weight of EFFECTIVE health — max health stretched by armor — in the scannable " +
                    "power rating. Applies to players and creatures alike; that shared scale is the " +
                    "whole point of the number, so this is deliberately NOT a per-side knob. Star " +
                    "variants come included for free: the game already multiplies max health by the " +
                    "creature level. " +
                    "(Playtest value, 2026-09-06. Lowered from 1 together with RatingDamageWeight " +
                    "going up: read side by side, bulk was drowning out offence, and a boss that " +
                    "only had a big health pool scanned like a boss that could actually kill you.)",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(48)));

            RatingArmorScale = config.Bind(SecPower, "RatingArmorScale", 40f,
                new ConfigDescription(
                    "How much armor DOUBLES effective health: at 50, an armor of 50 makes you count " +
                    "as twice your max health. Transformations do not show up through this term: the " +
                    "rating uses your base-form armor and RatingFormShare applies the form on top. " +
                    "Creatures have zero armor in " +
                    "Valheim (only Player overrides GetBodyArmor), so their factor is exactly 1 and " +
                    "this key does not touch them. Lower makes armor count for more. " +
                    "(Playtest value, 2026-09-06. Lowered from 50 so the forms show up harder on " +
                    "the defensive side, which is the only side they touch.)",
                    new AcceptableValueRange<float>(0f, 1000f), AdminOnly(47)));

            RatingK2Damage = config.Bind(SecPower, "RatingDamageWeight", 10f,
                new ConfigDescription(
                    "Weight of damage PER SECOND in the scannable power rating. Same scale for " +
                    "players and creatures. Per second, not per hit: a troll hits for 70 every ~4 " +
                    "seconds and you punch about once a second, so per-hit made the troll look " +
                    "stronger than a fully maxed player, which the actual fight denies. This is " +
                    "the knob that decides whether the number reads as offence or as bulk. " +
                    "(Playtest value, 2026-09-06. Raised from 1: at parity with the health weight " +
                    "the rating was almost pure bulk, and the number is supposed to answer 'can " +
                    "this thing hurt me', not 'how long does it take to chew through it'.)",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(46)));

            // A forma entra no poder de luta como multiplicador sobre a conta da forma base, e nao
            // pela armadura e pelo soco de dentro dela. Decidido em 2026-09-17 na calculadora
            // (aba Poder de luta). Mesmo desenho do FormSpeedShare do voo.
            RatingFormShare = config.Bind(SecPower, "RatingFormShare", 1f,
                new ConfigDescription(
                    "How much of a transformation's PowerMultiplier reaches your scannable power " +
                    "rating. The rating is computed with your BASE-form armor and punch and then " +
                    "multiplied by 1 + (PowerMultiplier - 1) x this. At 1, SSJ reads exactly 2x your " +
                    "base form, SSJ2 3x, SSJ3 4x; at 0 the form does not show at all. Base form is " +
                    "unaffected. Why not just let the form show through its armor and punch: forms " +
                    "grant no health, and health is most of the rating, so SSJ only read about 1.3x. " +
                    "Who wins a fight goes with effective health TIMES damage per second, and the form " +
                    "raises both, so 2x is still conservative.",
                    new AcceptableValueRange<float>(0f, 2f), AdminOnly(45)));

            // --- Power Level ---
            // XP proporcional ao dano que passa pela luta, dos dois lados. Escala com o inimigo
            // sem tabela nenhuma: um Boar tem 10 de HP, um troll 600.
            SkillXpPerDamageDealt = config.Bind(SecPowerSkill, "XpPerDamageDealt", 0.07f,
                new ConfigDescription(
                    "XP per point of damage dealt, CAPPED at the target's remaining HP. That cap is " +
                    "what kills weak-mob farming: a 5000 damage punch on a 10 HP boar counts as 10. " +
                    "(Playtest value, 2026-08-11. Went 0.05 → 0.1 on 2026-08-01, when the early " +
                    "levels crawled; 0.07 is the settled middle, after 0.1 made them climb too fast.)",
                    new AcceptableValueRange<float>(0f, 10f), AdminOnly(100)));

            SkillXpPerDamageTaken = config.Bind(SecPowerSkill, "XpPerDamageTaken", 0.07f,
                new ConfigDescription(
                    "XP per point of damage taken, measured AFTER armor. Measuring it after armor is " +
                    "what kills the exploit of taking hits on purpose from a weak enemy: taking " +
                    "little damage pays little. " +
                    "(Playtest value, 2026-08-11: kept equal to XpPerDamageDealt on purpose — the " +
                    "two sides of the fight should pay the same.)",
                    new AcceptableValueRange<float>(0f, 10f), AdminOnly(90)));

            SkillXpWeightBonus = config.Bind(SecPowerSkill, "XpWeightBonus", 1f,
                new ConfigDescription(
                    "Extra XP with the inventory at maximum weight. 1.0 = doubles the gain. " +
                    "This is Goku's weighted clothing, and it is self-limiting: weight slows you " +
                    "down, eats stamina and lowers flight speed. The player pays mobility for " +
                    "progression.",
                    new AcceptableValueRange<float>(0f, 10f), AdminOnly(80)));

            SkillXpMaxPerEvent = config.Bind(SecPowerSkill, "XpMaxPerEvent", 5f,
                new ConfigDescription(
                    "Safety clamp: maximum XP from a single hit, dealt or taken. Prevents one hit " +
                    "on a boss from jumping several levels at once.",
                    new AcceptableValueRange<float>(0.1f, 1000f), AdminOnly(70)));

            SkillXpWeaponFactor = config.Bind(SecPowerSkill, "XpWeaponFactor", 0f,
                new ConfigDescription(
                    "Fraction of WEAPON damage that pays Power Level XP. Punches and ki attacks " +
                    "always pay in full; this key only covers hits made with a vanilla weapon. " +
                    "0 (default) means the sword path and the ki path are separate progressions: " +
                    "Power Level is what you get for fighting the mod's way, and a sword already " +
                    "trains its own vanilla skill.",
                    new AcceptableValueRange<float>(0f, 1f), AdminOnly(65)));

            // --- Debug ---

            VerboseLogging = config.Bind(SecDebug, "VerboseLogging", false,
                new ConfigDescription("Detailed logging in the BepInEx console.",
                    null, ClientSide(100)));

            // Depois de todo Bind, de proposito: a tabela de migracao referencia as entradas, e
            // antes daqui metade delas ainda e' null.
            Util.ConfigMigration.Run(config);

            SaiyaheimPlugin.Log.LogInfo("Config loaded.");
        }

        /// <summary>
        /// Liga as chaves de uma forma numa seção própria do <c>.cfg</c>.
        ///
        /// Existe para que adicionar uma transformação nova seja <b>uma chamada</b>, e não um bloco
        /// copiado com cinco descrições para manter em sincronia. O texto que o jogador lê é o
        /// mesmo para todas as formas de propósito: o que muda entre elas são os números.
        /// </summary>
        private static TransformationConfig BindTransformation(
            ConfigFile config, string section, float powerMultiplier, float kiDrainPerSecond,
            float punchSlashFraction, float punchLightningFraction, float carryWeightBonus,
            string hairColor, string requiredGlobalKey, bool lightning, string lightningColor = "",
            float masteryDrainReduction = 1f, float glowIntensity = 1f, string glowColor = "",
            string hairItem = "", float masteryXpPerDamageDealt = 0.0125f,
            float masteryXpPerDamageTaken = 0.0125f, float masteryXpMaxPerEvent = 1.25f,
            float? healthRegenSpeed = null, bool? healthRegenIgnoresBlockers = null,
            float combatKiCostScale = 1f)
        {
            return new TransformationConfig
            {
                // Multiplica o poder de COMBATE (soco, armadura, block power, numero exibido) e a
                // velocidade de voo. Teto de ki, regeneracao e carga ficam de fora de proposito:
                // se a barra crescesse ao transformar ela daria um pulo na tela, e a regeneracao
                // escalada compensaria parte do proprio dreno — a forma se pagando sozinha.
                PowerMultiplier = config.Bind(section, "PowerMultiplier", powerMultiplier,
                    new ConfigDescription(
                        "Multiplies the COMBAT battle power while this form is active — punch " +
                        "damage, armor, block power and the number on screen all scale from it, " +
                        "so this one value is the whole strength of the form. Flight speed is " +
                        "multiplied too. The ki cap, ki regeneration and charging deliberately " +
                        "are NOT: a bar that grows on transforming would jump on screen, and " +
                        "scaled regeneration would pay for part of the form's own drain. " +
                        "Note the ki costs of punching and blocking scale WITH it automatically, " +
                        "since both are charged per point of damage dealt or absorbed. " +
                        "(Starting value. Not playtested yet.)",
                        new AcceptableValueRange<float>(1f, 50f), AdminOnly(100))),

                KiDrainPerSecond = config.Bind(section, "KiDrainPerSecond", kiDrainPerSecond,
                    new ConfigDescription(
                        "Base ki drained per second while transformed, before the mastery " +
                        "reduction. This is the ONLY cost of the form: there is no activation " +
                        "cost. Hitting zero ki powers you down. " +
                        "Flat per second and not a fraction of the bar, because the bar already " +
                        "grows with Power Level (MaxKiPerPowerLevel) — so the form lasts longer " +
                        "as the character grows even before mastery, which is the intended " +
                        "reading of getting stronger. " +
                        "(Starting value. Not playtested yet.)",
                        new AcceptableValueRange<float>(0f, 100f), AdminOnly(90))),

                // O sabor da forma no golpe. Converter e nao somar e' o ponto: um tipo de dano novo
                // que viesse por cima seria um segundo multiplicador de forca escondido dentro de
                // uma decisao estetica, e o PowerMultiplier deixaria de ser "a forca inteira da
                // forma" que a descricao dele promete.
                PunchSlashFraction = config.Bind(section, "PunchSlashFraction", punchSlashFraction,
                    new ConfigDescription(
                        "Fraction of the punch's BLUNT damage turned into SLASH while this form is " +
                        "active. 0.5 = half and half. The total damage of the hit does not change: " +
                        "this moves damage between types, it does not add any. " +
                        "It applies to the whole punch — vanilla unarmed damage plus the Power Level " +
                        "bonus — and only to unarmed attacks. " +
                        "What it is for: armor is per damage type in Valheim, so a form that hits " +
                        "with two types is less punished by an enemy that resists one of them. " +
                        "Blunt and slash both count toward stagger, so the split does not change " +
                        "how fast a target staggers. " +
                        "(Starting value. Not playtested yet.)",
                        new AcceptableValueRange<float>(0f, 1f), AdminOnly(85))),

                // Um segundo tipo ao lado do corte, e nao no lugar dele: a repartição é a regra da
                // forma, não uma escolha do SSJ. Cada degrau escolhe quanto do soco vai para onde,
                // e o SSJ2 escolhe contusão e raio sem passar pelo corte.
                PunchLightningFraction = config.Bind(section, "PunchLightningFraction", punchLightningFraction,
                    new ConfigDescription(
                        "Fraction of the punch's BLUNT damage turned into LIGHTNING while this " +
                        "form is active. Same rule as PunchSlashFraction above: it MOVES damage " +
                        "between types and adds none, so the total of the hit does not change. " +
                        "The two fractions split the same total. If they add up to more than 1 " +
                        "they are scaled down to fit, rather than eating into the blunt that is " +
                        "not there. " +
                        "Why it matters: lightning is resisted by very few things in Valheim, and " +
                        "the Swamp and Mountains are full of enemies weak to it — but frost caves " +
                        "and Fenring resist it, so it is not a free upgrade over slash. " +
                        "Lightning also counts toward stagger like the other physical types. " +
                        "(Starting value. Not playtested yet.)",
                        new AcceptableValueRange<float>(0f, 1f), AdminOnly(84))),

                // O peso e' o unico numero da forma que NAO passa pelo PowerMultiplier, e de
                // proposito: multiplicar o limite pelo poder faria a mochila crescer junto com o
                // grind de Power Level, e o limite de peso e' logistica, nao combate. Aqui cada
                // degrau da escada carrega o que o degrau dele carrega, e ponto.
                //
                // Somado e nao multiplicado pelo mesmo motivo do bonus de soco: soma convive com o
                // Megingjord (+150) em vez de brigar com ele, e o numero no .cfg e' lido direto na
                // tela do inventario, sem depender do limite base.
                CarryWeightBonus = config.Bind(section, "CarryWeightBonus", carryWeightBonus,
                    new ConfigDescription(
                        "Extra carry weight while this form is active. Added on top of the " +
                        "character's own limit (300 in vanilla) and on top of Megingjord, so " +
                        "300 here would mean double the backpack while transformed. " +
                        "Falls off the moment the form does, and anything you were carrying over " +
                        "the plain limit makes you encumbered again — powering down mid-haul is " +
                        "the cost of using the form as a cart. " +
                        "Side effect worth knowing: carry load is a fraction of the LIMIT, so a " +
                        "bigger limit means the same cargo slows flight less and pays less " +
                        "Power Level XP (XpWeightBonus). " +
                        "Playtested 2026-08-16: started at 300 and came down to 100.",
                        new AcceptableValueRange<float>(0f, 2000f), AdminOnly(83))),

                // O sabor do SSJ God (2026-09-21): a forma do controle compra folego, e nao golpe.
                // So' e' ligada na forma que passa o parametro — as outras nao ganham a chave no
                // .cfg, a pedido do Henrique: uma chave que so' existe para ficar em 1 e' ruido.
                //
                // ⚠️ Houve uma segunda chave aqui, o HealthRegenMultiplier, que engordava cada
                // tique de cura. Ela saiu em 2026-09-22, depois do playtest: as duas se
                // MULTIPLICAVAM (2 e 2 davam 4x a cura por segundo), e das duas a que se sente e'
                // esta — degrau menor e mais frequente le como regeneracao, degrau grande e raro
                // le como pocao. Quem quer mais cura sobe a velocidade.
                HealthRegenSpeed = healthRegenSpeed == null ? null : config.Bind(section, "HealthRegenSpeed", healthRegenSpeed.Value,
                    new ConfigDescription(
                        "How many times faster the passive healing CLOCK runs while this form is " +
                        "active. Vanilla heals once every 10 seconds, so 2 heals every 5 and 4 " +
                        "every 2.5. It does not change how much each tick heals — it changes how " +
                        "often the tick comes, which is what makes the healing readable during a " +
                        "fight instead of arriving in one lump. Total healing per second is " +
                        "(food regen x this) / 10, times whatever the weather and Rested do to " +
                        "the shared regen multiplier. " +
                        "1 leaves the vanilla clock alone. " +
                        "(Starting value. Not playtested yet.)",
                        new AcceptableValueRange<float>(1f, 10f), AdminOnly(80))),

                // A terceira chave da cura, e a que muda o sinal da mecanica em vez do numero: o
                // multiplicador da cura e' COMPARTILHADO, e Molhado, Frio e Congelando entram nele
                // depois da forma — o ultimo ZERA. Sem esta chave, a forma do folego nao cura nada
                // exatamente onde folego importa, que e' a Montanha.
                HealthRegenIgnoresBlockers = healthRegenIgnoresBlockers == null ? null : config.Bind(section, "HealthRegenIgnoresBlockers", healthRegenIgnoresBlockers.Value,
                    new ConfigDescription(
                        "Whether this form's healing ignores everything that cuts or stops health " +
                        "regeneration — Wet, Cold and Freezing. Valheim's regen multiplier is " +
                        "SHARED: every status effect edits the same number, and Freezing sets it " +
                        "to zero, which no multiplier can survive. With this on, the normal food " +
                        "rate becomes a FLOOR: the weather can raise the healing (Rested still " +
                        "adds) but not lower it below the rate you get in good weather. " +
                        "Only affects the passive healing from food; the Freezing DAMAGE keeps " +
                        "coming, so the mountain still hurts, it just stops being a wall. " +
                        "(Starting value. Not playtested yet.)",
                        null, AdminOnly(79))),

                // A outra metade do sabor do God: o custo de lutar dentro dele. A sobretaxa em si
                // e' compartilhada (CombatFormKiShare); esta chave diz quanto dela a forma cobra.
                // Por forma, e nao um segundo CombatFormKiShare, para a conta continuar tendo um
                // dial global so' — este so' desloca um degrau em relacao aos outros.
                CombatKiCostScale = config.Bind(section, "CombatKiCostScale", combatKiCostScale,
                    new ConfigDescription(
                        "How much of the shared combat surcharge (CombatFormKiShare in the Combat " +
                        "section) THIS form charges on punching, blocking, taking hits and ki " +
                        "attacks. 1 = the full surcharge; 0.5 = half of it; 0 = fighting in this " +
                        "form costs the same ki as base form from mastery 0. Mastery still pays " +
                        "off whatever is left. " +
                        "(Starting value. Not playtested yet.)",
                        new AcceptableValueRange<float>(0f, 3f), AdminOnly(81))),

                MasteryDrainReduction = config.Bind(section, "MasteryDrainReduction", masteryDrainReduction,
                    new ConfigDescription(
                        "Fraction of the drain removed at level 100 of THIS form's skill. " +
                        "drain = KiDrainPerSecond * (1 - level/100 * this). " +
                        "1 = at level 100 the form is FREE: no drain at all, and passive ki " +
                        "regeneration runs again while transformed, so a maxed form is permanent " +
                        "until you drop it yourself. That is the intended end of the mastery " +
                        "curve — at first you barely hold the form, in the end you wear it. " +
                        "Lower it if you want the form to keep costing something forever: 0.8 " +
                        "means level 100 still pays a fifth of the level 0 drain, and ki stays a " +
                        "source of tension all game.",
                        new AcceptableValueRange<float>(0f, 1f), AdminOnly(80))),

                // ⚠️ Substituiu o `MasteryXpPerSecond` (1/s) em 2026-09-20. A maestria treinava por
                // TEMPO dentro da forma, e era o segundo grind que as issues 1 e 2 do GitHub
                // relataram: com o dreno alto, o jogador ficava parado carregando ki; com o dreno
                // baixo do rework, ele ficaria parado DENTRO da forma, que e' pior. Ficar parado
                // em forma passa a nao pagar nada — treina-se lutando.
                //
                // A chave velha fica orfa e inerte nos .cfg existentes.
                //
                // ⚠️ A taxa NAO e' a do Power Level (SkillXpPerDamageDealt, 0,07). Aquela corre a
                // sessao inteira; esta so corre durante o combate ATIVO, que e' algo entre 10% e
                // 15% do tempo de jogo. Referencia para calibrar: a curva do Valheim cobra ~1.600
                // de XP ate o nivel 30 e ~20.000 ate o 100; a 0,25 por ponto de dano, uma luta que
                // troca ~950 pontos de dano em um minuto paga ~240.
                //
                // ⚠️ Playtest de 2026-09-20: cortada pela metade, de 0,5 para 0,25, junto com o
                // teto por golpe e com o XP de voo. A maestria subia rapido demais — e o motivo de
                // ela subir mais do que a taxa sugere esta' no RaiseMasteryFromDamage: um golpe
                // paga TODAS as formas ate a ativa, e dano causado e sofrido pagam os dois.
                //
                // ⚠️ E 0,25 -> 0,0125 no playtest de 2026-09-21: a metade nao bastou e a divisao
                // por 10 tambem nao — a maestria continuava subindo rapido demais. O numero final
                // e' um vigesimo do original, com o teto por golpe cortado junto. O que faz a
                // maestria correr mais do que a taxa sugere esta' no RaiseMasteryFromDamage: um
                // golpe paga TODAS as formas ate a ativa, e dano causado e sofrido pagam os dois.
                MasteryXpPerDamageDealt = config.Bind(section, "MasteryXpPerDamageDealt", masteryXpPerDamageDealt,
                    new ConfigDescription(
                        "XP for this form's skill per point of damage DEALT while wearing it. " +
                        "Fighting inside the form is the only way to train it: holding it while " +
                        "standing still, flying or exploring pays nothing. " +
                        "The damage counted is what the target actually lost, so overkill on a " +
                        "weak creature does not pay, and weapon hits pay by XpWeaponFactor like " +
                        "Power Level does — the form trains the mod's way of fighting. " +
                        "Valheim's own diminishing curve up to 100 applies on top: reaching level " +
                        "30 costs about 1600 XP and level 100 about 20000. " +
                        "(Replaced MasteryXpPerSecond on 2026-09-20 and cut three times since, " +
                        "0.5 to 0.25 to 0.025 to 0.0125: every playtest still found mastery " +
                        "levelling far too fast. Remember a single hit pays every form up to the " +
                        "active one.)",
                        new AcceptableValueRange<float>(0f, 20f), AdminOnly(70))),

                // Mesma taxa do dano causado, e nao metade dela: apanhar transformado e' treino
                // tanto quanto bater, e a fonte ja' se auto-limita — o dano sofrido vem DEPOIS da
                // armadura, entao apanhar de proposito de bicho fraco rende quase nada.
                MasteryXpPerDamageTaken = config.Bind(section, "MasteryXpPerDamageTaken", masteryXpPerDamageTaken,
                    new ConfigDescription(
                        "XP for this form's skill per point of damage TAKEN while wearing it. " +
                        "Counted after armor and resistances, so taking hits from something weak " +
                        "is worth almost nothing. Same rate as damage dealt by default: holding " +
                        "the form through a beating is training too.",
                        new AcceptableValueRange<float>(0f, 20f), AdminOnly(69))),

                // Grampo de seguranca, e nao regulador: com 0,0125 por ponto ele so morde a partir
                // de 100 de dano num unico golpe, que e' pancada de boss e nao troca de socos.
                // Cortado junto com a taxa todas as vezes justamente para o ponto em que ele morde
                // continuar sendo o mesmo dano.
                //
                // Aplicado ANTES do multiplicador de boss abaixo, ao contrario do
                // SkillXpMaxPerEvent do Power Level, que corta por ultimo. O bonus de boss existe
                // para corrigir o degrau velho que ficou para tras, e deixar o grampo comer essa
                // correcao a anularia justamente onde ela e' necessaria.
                MasteryXpMaxPerEvent = config.Bind(section, "MasteryXpMaxPerEvent", masteryXpMaxPerEvent,
                    new ConfigDescription(
                        "Safety clamp: the most mastery XP a single hit can pay, dealt or taken, " +
                        "before the boss multiplier. Stops one boss-sized hit from jumping " +
                        "several levels at once. At the default rate it only bites above 100 " +
                        "damage in one hit. (Cut from 50 to 25 to 2.5 to 1.25, each time together " +
                        "with the rate, so it still bites at the same damage.)",
                        new AcceptableValueRange<float>(0.1f, 1000f), AdminOnly(68))),

                // A resposta ao sintoma "o degrau velho fica para tras": o XP dele sobe a cada boss
                // derrotado DEPOIS do boss que o destravou, entao o SSJ acelera enquanto o SSJ2
                // ainda engatinha. Nao e' a mesma pergunta que o MasteryXpPerSecond acima — aquele
                // regula a velocidade da escada inteira, este regula a diferenca entre os degraus.
                //
                // Global key e nao estado do jogador, pelas mesmas tres razoes do BossGate: o
                // servidor sincroniza de graca, persiste no save do MUNDO e vale para todo mundo do
                // mundo. E' funcao pura do mundo + config, sem nenhum estado novo para serializar —
                // nao ha evento de "boss morreu" para escutar nem nada que se perca offline.
                //
                // Por forma e nao global, seguindo a regra da secao: nenhum numero e' compartilhado
                // entre degraus, e um degrau distante do inicio pode querer passo proprio.
                MasteryXpPerBossBonus = config.Bind(section, "MasteryXpPerBossBonus", 2f,
                    new ConfigDescription(
                        "How much this form's mastery XP speeds up for each boss defeated AFTER " +
                        "the one that unlocked it. The multiplier is 1 + this * (bosses defeated " +
                        "- this form's rung), floored at 1 and capped by " +
                        "MasteryXpBossMultiplierMax. 0 disables it. \n" +
                        "Uncapped, the default 2 would pay x1 while the form's own boss is the " +
                        "newest kill, x3 after the next boss falls, x5 after the one after that; " +
                        "with the default cap of 2 it pays x1 and then x2 for good. \n" +
                        "What it is for: the mastery curve is the same for every rung, so the form " +
                        "unlocked first is always the one furthest up the expensive end of " +
                        "Valheim's XP curve, and it crawls exactly when a stronger form has just " +
                        "made it look useless. This makes the older rung train faster the further " +
                        "the world has moved past it, which is also the reading that makes sense " +
                        "in fiction: the form is trivial to you now. \n" +
                        "It reads the world's global keys, so a server syncs it for free and " +
                        "someone joining late arrives with whatever the group has already killed " +
                        "— the same rule the unlock gate itself follows. \n" +
                        "Only the five classic bosses count. A form tied to a key this build does " +
                        "not know, which today means the Queen and the Fader, gets no bonus at " +
                        "all rather than a wrong one. \n" +
                        "(2 rather than 1, sized against the 2026-09-06 run: SSJ mastery was at " +
                        "level 52 with the third boss about to fall, where 80-90 was the target. " +
                        "At 2 that stretch pays the SSJ x5 instead of x3, which is what closes " +
                        "the gap without touching MasteryXpPerSecond. Sized on paper, not read " +
                        "off a run yet.)",
                        new AcceptableValueRange<float>(0f, 5f), AdminOnly(69))),

                // Teto do multiplicador acima. Sem ele o degrau velho acelera sem limite conforme o
                // mundo anda (x3, x5...), e o bonus deixa de ser correcao para virar atalho.
                //
                // 2 -> 4 no playtest de 2026-09-21, junto com o corte do XP de maestria para um
                // vigesimo. Os dois andam juntos: com a taxa base tao baixa, um teto de 2 fazia o
                // degrau velho parar de recuperar terreno quase na hora — o bonus mal comecava a
                // pagar e ja' estava no limite. Em 4 ele continua subindo por mais dois bosses,
                // que e' o tempo que o degrau atrasado leva para voltar a fazer sentido.
                MasteryXpBossMultiplierMax = config.Bind(section, "MasteryXpBossMultiplierMax", 4f,
                    new ConfigDescription(
                        "Ceiling for the boss XP multiplier from MasteryXpPerBossBonus. The final " +
                        "multiplier is min(this, 1 + bonus * (bosses defeated - this form's " +
                        "rung)), floored at 1. 1 disables the boss bonus entirely. \n" +
                        "With the default 4 and a bonus of 2: a form pays x1 while its own boss " +
                        "is the newest kill, x3 after the next one falls and x4 from the one " +
                        "after that, no matter how many more fall. " +
                        "(Raised from 2 on 2026-09-21, together with the mastery XP cut: at a " +
                        "twentieth of the old rate a ceiling of 2 stopped the older form from " +
                        "catching up almost as soon as the bonus started paying.)",
                        new AcceptableValueRange<float>(1f, 20f), AdminOnly(68))),

                MinPowerLevel = config.Bind(section, "MinPowerLevel", 0f,
                    new ConfigDescription(
                        "Minimum Power Level required to enter this form. 0 disables the " +
                        "gate. This is the TRAINING gate, and it is independent of the boss gate " +
                        "below: with both set, the form needs both. Left at 0 for every form so " +
                        "far, because the ladder is paced by bosses and grinding a skill to reach " +
                        "a form would pace it twice.",
                        new AcceptableValueRange<float>(0f, 100f), AdminOnly(60))),

                // A trava de verdade da escada. Vazio = sem trava, que e' o que toda forma nova
                // deve nascer com — amarrar a um boss e' decisao de design, nao default.
                RequiredGlobalKey = config.Bind(section, "RequiredGlobalKey", requiredGlobalKey,
                    new ConfigDescription(
                        "Global key of the boss that unlocks this form. Empty disables the gate. " +
                        "The key belongs to the WORLD, not to the character: the server syncs it " +
                        "for free, it survives in the world save, and it counts for everyone in " +
                        "that world — so someone joining later arrives with whatever the group " +
                        "has already killed. That is the intended reading for a world played " +
                        "with friends: the ladder measures the world's progress, not each " +
                        "player's trophy list.\n" +
                        "The five valid keys, and mind that two of them are NOT named after the " +
                        "boss: defeated_eikthyr (Eikthyr), defeated_gdking (The Elder), " +
                        "defeated_bonemass (Bonemass), defeated_dragon (MODER), " +
                        "defeated_goblinking (YAGLUTH). A key that does not exist is not an " +
                        "error — it is a form that never unlocks. Check the current state with " +
                        "saiya_form.",
                        null, AdminOnly(58))),

                // O visual da forma saiu do .cfg em 2026-09-15, junto com a secao 8: o que segue
                // aqui e' escolha de tela ja fechada, e o valor de cada degrau chega por parametro
                // desta funcao. Os tres degraus continuam sem compartilhar numero nenhum.
                //
                // O PENTEADO e' identidade de DEGRAU, e nao decoracao: o SSJ3 e' a forma que o
                // genero define pelo comprimento do cabelo, e sem ele a unica diferenca para o
                // SSJ2 seria o tom do amarelo. Tres tipos de valor: "Spiked" (o cabelo do proprio
                // personagem, na versao espetada do CustomHair), um item do jogo (Hair1..Hair37,
                // HairNone) ou um item nosso (SaiyaHair6...). Nome invalido nao pinta nem estoura
                // — o mod avisa no log e mantem o cabelo do personagem. A troca vai pela ZDO, via
                // VisEquipment: o jogo replica de graca e o penteado de verdade do personagem
                // (Humanoid.m_hairItem, serializado no perfil) nunca e' tocado. Ver
                // Transformations.TransformationEffects.SetHairStyle.
                HairItem = hairItem,
                HairColor = hairColor,

                // Acima de 1 a cor estoura e queima, que e' o que le como cabelo de Super Saiyan:
                // um hex puro para em #FFFFFF e fica mais perto de tingido do que de brilhando.
                HairColorIntensity = 1.6f,

                // A aura sai da cor do cabelo para as duas lerem como uma coisa so'. Uma cor por
                // forma, e nao uma global: a escada quer degraus distinguiveis de longe, e a cor
                // da aura e' o unico sinal que sobrevive a distancia. O prefab e' compartilhado;
                // a cor e' a identidade.
                AuraColor = hairColor,

                // Um booleano, e nao um prefab por forma: o que separa um degrau do outro e'
                // crepitar ou nao, e a regulagem de um estalo (FormLightning*) e' a mesma em
                // qualquer forma.
                LightningEnabled = lightning,

                // A unica cor que NAO acompanha as outras, e de proposito (playtest de
                // 2026-08-16): raio da cor da aura le como mais aura, nao como eletricidade — o
                // olho precisa da quebra de matiz para separar o raio do brilho em que ele senta.
                // Vazio cai na AuraColor.
                LightningColor = lightningColor,

                // Um numero e nao um booleano, ao contrario do raio: o raio ou estala ou nao, mas
                // o brilho de um degrau alto e' o mesmo brilho MAIS FORTE. E' o unico valor visual
                // que le como ESCADA em vez de identidade, e de noite e' o que separa duas formas
                // a distancia. Multiplica o FormGlowIntensity compartilhado.
                GlowIntensity = glowIntensity,

                // Vazio cai na AuraColor, que e' o caso normal — a forma tem uma cor so'. Separar
                // so' vale se o tom da aura embarrar como luz sobre grama e pedra.
                GlowColor = glowColor
            };
        }

        /// <summary>
        /// Liga as chaves de um ataque de ki numa seção própria do <c>.cfg</c>.
        ///
        /// Mesmo papel do <see cref="BindTransformation"/>, e pelo mesmo motivo: o ataque seguinte
        /// deve ser <b>uma chamada</b>, não um bloco copiado com treze descrições para manter em
        /// sincronia.
        /// </summary>
        private static KiAttackConfig BindKiAttack(
            ConfigFile config, string section, float damageBase, float damageFromPower,
            float kiCost, float cooldown, string projectilePrefab, string impactEffect,
            string impactEffectStrip, string impactColor, string projectileColor,
            string requiredGlobalKey, int beamCount = 1, float beamInterval = 0.05f,
            float knockback = 30f, float impactRadius = 0f, float projectileSpeed = 30f, float projectileLifetime = 3f,
            float projectileScale = 1f, float chargeTime = 0f, float minChargeRatio = 0.15f,
            float chargeMinScale = 0.4f, string chargeEffectPrefab = "",
            string chargeEffectColor = "", float chargeEffectScale = 1f,
            float chargeEffectHeight = 1f, float chargeEffectSide = 0.35f,
            string chargeFullEffectPrefab = "", string chargeFullEffectColor = "",
            float chargeFullEffectScale = 1f, bool chargeFullEffectLoop = false,
            bool chargeFullEffectReplaces = true, float chargeEffectForward = 0f,
            EffectAnchor chargeEffectAnchor = EffectAnchor.RightHand,
            string chargeBallPrefab = "", string chargeBallColor = "", float chargeBallScale = 1f,
            string chargeBallStrip = "")
        {
            return new KiAttackConfig
            {
                DamageBase = config.Bind(section, "DamageBase", damageBase,
                    new ConfigDescription(
                        "Damage of this attack at battle power zero, before the power share below. " +
                        "It is the floor: a fresh character has almost no battle power, and an " +
                        "attack that did nothing at all until the bar filled would read as broken " +
                        "on the very first shot. All of it is SLASH damage. " +
                        "(Starting value. Not playtested yet.)",
                        new AcceptableValueRange<float>(0f, 1000f), AdminOnly(100))),

                // Le o poder de COMBATE, o mesmo do soco — nao o linear. Duas coisas saem de graca:
                // o termo de fim de jogo entra (o ataque acompanha o soco em vez de virar plato no
                // nivel 100) e o multiplicador da forma entra (transformar deixa o blast mais forte
                // sem uma linha de codigo a mais).
                DamageFromPower = config.Bind(section, "DamageFromPower", damageFromPower,
                    new ConfigDescription(
                        "Share of the COMBAT battle power added to this attack's damage. " +
                        "Same number the punch reads, so the attack keeps up with the fists " +
                        "instead of falling behind, transforming makes it stronger for free, and " +
                        "the late-game term applies to it as well. " +
                        "Careful: this is a RANGED hit with no wind-up, so it should sit below " +
                        "PunchDamageFromPower or there is no reason to ever close distance. " +
                        "(Starting value. Not playtested yet.)",
                        new AcceptableValueRange<float>(0f, 10f), AdminOnly(95))),

                // Fixo, e a decisao esta' registrada como provisoria em [[Ataques de Ki]]: o soco
                // cobra por ponto de dano e ganhou desconto hiperbolico, e este anda no sentido
                // contrario. No fim do jogo tende a ficar quase de graca. A pergunta do playtest e'
                // em que nivel isso acontece, e se mata o soco quando acontecer.
                KiCost = config.Bind(section, "KiCost", kiCost,
                    new ConfigDescription(
                        "Ki spent per projectile, charged when you fire — hit or miss. Charging on " +
                        "impact instead would reward aim and punish fighting anything fast, which " +
                        "is the opposite of what a ranged attack should teach. " +
                        "On a charged attack (ChargeTime above 0) this is spent WHILE THE CHARGE " +
                        "GROWS, as it produces each projectile, and letting go costs nothing. " +
                        "A full charge stops costing, however long the key is held after that: " +
                        "what is paid for is projectiles, and at the top there are no more coming. " +
                        "The rate per second is this times BeamCount divided by ChargeTime — " +
                        "saiya_blast prints it. " +
                        "FLAT on purpose, unlike the punch, which costs per point of damage: this " +
                        "is the starting shape and it is expected to get cheap late, when the bar " +
                        "has grown and this number has not. Watch it with saiya_blast, which " +
                        "prints shots per full bar.",
                        new AcceptableValueRange<float>(0f, 1000f), AdminOnly(90))),

                Cooldown = config.Bind(section, "Cooldown", cooldown,
                    new ConfigDescription(
                        "Seconds before this attack can be fired again. Without it the rate of " +
                        "fire is limited only by the frame rate and by the bar, which turns the " +
                        "attack into a machine gun and makes KiCost the only thing standing " +
                        "between the player and emptying the bar in one second.",
                        new AcceptableValueRange<float>(0f, 30f), AdminOnly(85))),

                Knockback = config.Bind(section, "Knockback", knockback,
                    new ConfigDescription(
                        "Push applied to whatever is hit. It is what makes the shot read as an " +
                        "impact rather than a scratch, and it buys back the distance the attack " +
                        "exists to keep.",
                        new AcceptableValueRange<float>(0f, 500f), AdminOnly(80))),

                // O Projectile do jogo ja' sabe explodir (m_aoe): um OverlapSphere no impacto que
                // aplica o mesmo m_damage, ja' escalado pelo poder, a todo alvo no raio. Sem queda
                // por distancia — dano cheio em cada um —, e por isso o blast perdeu 10% de dano
                // quando ganhou area (2026-09-17).
                ImpactRadius = config.Bind(section, "ImpactRadius", impactRadius,
                    new ConfigDescription(
                        "Radius in meters of the explosion where the projectile lands. Everything " +
                        "inside takes the FULL damage and knockback — the game has no falloff — " +
                        "including buildings. Hitting the ground explodes too, so a near miss still " +
                        "hits whoever is close. The shooter is never hit; other players only with " +
                        "PvP on. 0 = only the target struck, as before. " +
                        "On a beam (BeamCount above 1) this applies to every projectile of it.",
                        new AcceptableValueRange<float>(0f, 20f), AdminOnly(79))),

                // A peca que faz o Kamehameha existir sem o jogo ter feixe. Descoberta no playtest
                // de 2026-09-07: o projectile_beam do Yagluth NAO e' um raio sustentado — o boss
                // dispara varios seguidos, e o que se le como feixe e' a fila. Entao o mod faz o
                // mesmo, e o "feixe" e' um numero de config em vez de um sistema novo.
                //
                // Dano, empurrao e custo continuam sendo POR PROJETIL. E' o que mantem a formula
                // igual a' do tiro unico — o feixe nao e' um caso especial da conta, e' N vezes a
                // mesma conta — e e' o que faz meio feixe que erra bater metade. O saiya_blast
                // imprime o total, que e' o numero que se calibra.
                BeamCount = config.Bind(section, "BeamCount", beamCount,
                    new ConfigDescription(
                        "How many projectiles one press fires. 1 is a single shot. Higher turns " +
                        "the attack into a stream: fired close enough together, a line of " +
                        "projectiles reads as one continuous beam, which is exactly how the game " +
                        "itself draws Yagluth's beam. " +
                        "Damage, ki cost and knockback below are PER PROJECTILE, so this multiplies " +
                        "all three — saiya_blast prints the totals. " +
                        "Raise it for a longer beam, and lower BeamInterval to close the gaps.",
                        new AcceptableValueRange<int>(1, 60), AdminOnly(84))),

                BeamInterval = config.Bind(section, "BeamInterval", beamInterval,
                    new ConfigDescription(
                        "Seconds between one projectile of a beam and the next. It sets both how " +
                        "long the beam lasts (BeamCount x this) and how far apart the projectiles " +
                        "sit in the air (ProjectileSpeed x this) — and that spacing is what " +
                        "decides whether the eye reads a beam or a row of pellets. " +
                        "Ignored when BeamCount is 1. " +
                        "Aim is recomputed for every projectile, so a long beam follows the " +
                        "crosshair instead of pointing where it started.",
                        new AcceptableValueRange<float>(0.01f, 1f), AdminOnly(83))),

                // O carregamento e' o que separa o Kamehameha do ki blast: nao a cor nem o prefab,
                // mas a decisao de quanto gastar, tomada com o dedo na tecla e o inimigo vindo.
                // Zero devolve o ataque ao toque simples, que e' como o blast continua.
                //
                // O que a carga escala e' o COMPRIMENTO do feixe e a GROSSURA dele — nao o dano
                // por projetil. Escalar os dois faria o dano total crescer com o QUADRADO do tempo
                // segurado, e no fim do jogo isso e' um pico que nenhuma outra chave alcanca.
                // Decidido em 2026-09-07.
                ChargeTime = config.Bind(section, "ChargeTime", chargeTime,
                    new ConfigDescription(
                        "Seconds of holding the fire key to reach a full charge. 0 turns charging " +
                        "off and the attack fires on the key press, with the whole beam at once — " +
                        "which is what the ki blast does. " +
                        "Holding longer fires MORE projectiles, up to BeamCount, and makes them " +
                        "thicker; it does NOT make each projectile hit harder. So the ki spent " +
                        "and the damage dealt both grow with the hold, in a straight line. " +
                        "The ki is spent WHILE THE CHARGE GROWS, not on release: you pay for each " +
                        "projectile the moment the charge produces it, so the bar going down is " +
                        "the charge meter, and it stops going down the instant the charge is full " +
                        "— holding a finished charge is free, because there is nothing left to buy. " +
                        "Running the bar dry fires the attack immediately with whatever was paid " +
                        "for: holding a key that has stopped doing anything would only be found " +
                        "out on release.",
                        new AcceptableValueRange<float>(0f, 30f), AdminOnly(88))),

                MinChargeRatio = config.Bind(section, "MinChargeRatio", minChargeRatio,
                    new ConfigDescription(
                        "Smallest fraction of a full charge that still fires, from 0 to 1. " +
                        "Letting go below it drops the charge instead of firing, so brushing the " +
                        "key does not throw a single projectile the player never meant to fire. " +
                        "The ki that fraction of a second already drained does NOT come back — " +
                        "refunding it would mean tracking the spend tick by tick, for a few points " +
                        "of a bar measured in hundreds. " +
                        "It is ignored when the bar runs dry mid-charge: that fires whatever was " +
                        "paid for, however short, because refusing there would charge for nothing. " +
                        "Ignored entirely when ChargeTime is 0.",
                        new AcceptableValueRange<float>(0f, 1f), AdminOnly(87))),

                // Quanto um feixe carregado parece mais grosso que um curto. So' se sabia
                // olhando, e foi olhando que se decidiu: 1 tiraria a diferenca sem tirar a
                // mecanica, e o Kamehameha ficou em 0,2 para o CRESCIMENTO ser o que se le na mao.
                ChargeMinScale = chargeMinScale,

                // Prefab do jogo, nao asset novo. O vault e' explicito sobre este ser o pedaco que
                // vende a cena: segundo [[Animacoes]], o carregamento se le pelas particulas nas
                // maos, nao pela pose — e por isso ele vem antes da pose de duas maos.
                ChargeEffectPrefab = chargeEffectPrefab,

                ChargeEffectColor = chargeEffectColor,

                ChargeEffectScale = chargeEffectScale,

                // Tres numeros e nao um vetor, herdado de quando eram tres chaves do .cfg — o
                // BepInEx nao tem tipo de vetor.
                //
                // ⚠️ **As tres sao medidas nos eixos do JOGADOR** — direita, cima e frente do
                // personagem —, e nao nos do osso em que o efeito esta pendurado. A diferenca so'
                // aparece com uma pose de verdade em cima: preso a mao, os eixos do osso giram com
                // o pulso, e cada ajuste de pose invalidava a calibragem das tres. Trocado em
                // 2026-09-07, durante a calibragem da pose de duas maos. Ver
                // KiBeamChargeEffects.Place.
                ChargeEffectHeight = chargeEffectHeight,

                ChargeEffectSide = chargeEffectSide,

                ChargeEffectForward = chargeEffectForward,

                // O que faz a pose valer de graca. Presos ao OSSO da mao, os efeitos
                // vao para onde a animacao levar a mao; medidos a partir dos pes, ficam boiando
                // onde a mao estava antes. Enquanto nao ha' pose os dois parecem iguais, e e' por
                // isso que vale decidir agora e nao depois.
                //
                // ⚠️ Trocar isto muda a ORIGEM das tres chaves de offset acima: na mao elas sao
                // medidas a partir da palma (numeros perto de zero), no corpo a partir dos pes (a
                // altura da mao e' ~1). Um conjunto de numeros no outro modo poe o efeito a um
                // metro de onde deveria. O que NAO muda sao as direcoes: as tres sempre andam nos
                // eixos do jogador.
                ChargeEffectAnchor = chargeEffectAnchor,

                // Prefab de PROJETIL, e nao de efeito, e e' o ponto: o Valheim nao tem um "fx_" que
                // seja uma esfera de energia parada, mas tem varias que voam. O StaticProp arranca
                // o comportamento e deixa o visual. Ver Util/StaticProp.cs.
                ChargeBallPrefab = chargeBallPrefab,

                ChargeBallColor = chargeBallColor,

                ChargeBallScale = chargeBallScale,

                // O rastro e' a peca do projetil que so' faz sentido em movimento: parado na mao,
                // ele vira uma nuvem crescendo em volta dela. Mesmo mecanismo e mesmas regras do
                // ImpactEffectStrip — nome INTEIRO, e o log lista os nomes disponiveis.
                ChargeBallStrip = chargeBallStrip,

                // O aviso de carga cheia. Sem ele o jogador nao tem como saber que parou de ganhar
                // coisa por continuar segurando — a bola para de crescer, mas "parou de crescer" e'
                // dificil de ler numa particula que ja' esta' se mexendo sozinha.
                //
                // Sai no MESMO ponto do corpo que a bola, pelo ChargeEffectSide/Height: sao dois
                // sinais sobre a mesma coisa, e separa-los em dois lugares da tela leria como duas
                // coisas acontecendo.
                ChargeFullEffectPrefab = chargeFullEffectPrefab,

                ChargeFullEffectColor = chargeFullEffectColor,

                ChargeFullEffectScale = chargeFullEffectScale,

                // Duas leituras possiveis do mesmo pedido, e so' a tela decide: um estalo no
                // instante em que enche, ou um sinal aceso enquanto o jogador segura. A primeira e'
                // o default porque vfx_blocked e' um prefab de estouro — po-lo em loop pisca.
                ChargeFullEffectLoop = chargeFullEffectLoop,

                // Substituir e' o default porque os dois juntos ficaram ruins na tela — playtest de
                // 2026-09-07. A bola de carregamento diz "enchendo", e ela continuar ali depois de
                // cheia diz a coisa errada; o aviso de carga cheia e' que passa a ser a resposta.
                //
                // Por ataque e nao regra global: qual das duas leituras esta' certa e' julgamento
                // visual, e cada ataque pode querer a sua. Desligar devolve os dois somados.
                ChargeFullEffectReplaces = chargeFullEffectReplaces,

                // Prefab do jogo, nao asset novo — a regra de [[Efeitos Visuais]]. O nome escolhido
                // troca o visual inteiro do ataque, e por isso ele chega por parametro: e' a
                // identidade do degrau, nao um detalhe do disparo.
                ProjectilePrefab = projectilePrefab,

                // O estouro do impacto e' um EffectList do proprio Projectile, separado do que ele
                // INSTANCIA no hit — o mod ja' tirava o segundo e nao tocava no primeiro, e era de
                // la' que vinha a fumaca da bola de fogo. Chave propria e nao parte do prefab
                // porque voo e impacto sao escolhas independentes: da' para querer o voo de um e o
                // estouro de outro.
                ImpactEffect = impactEffect,

                // O corte fino do impacto. Existe porque o ImpactEffect e' grosso demais para o
                // caso real: o estouro do GoblinShaman tem clarao bom, som bom e uma fumaca que
                // nao combina com tiro de energia — e 'none' leva os tres juntos. Sao filhos do
                // mesmo prefab, entao nome de efeito nenhum separa um do outro; o que separa e'
                // remover o emissor no clone. Ver Util/StrippedEffect.cs.
                //
                // Nome INTEIRO, e nao pedaco de nome: 'fire' por pedaco casaria com
                // fx_shaman_fireball_expl e derrubaria o estouro junto com a chama. Ver a doc do
                // StrippedEffect.Matches.
                ImpactEffectStrip = impactEffectStrip,

                // A luz do impacto e' a parte do prefab que mais denuncia de onde ele veio: ela
                // pinta o terreno em volta, e o estouro do xama goblin acende ROSA. Nao ha' chave
                // do jogo para isso — o efeito e' instanciado pelo EffectList la' dentro do
                // Projectile e nunca passa pela nossa mao. O que se pinta e' o molde: ver
                // StrippedEffect.Prepare.
                //
                // Vazio segue o ProjectileColor de proposito. Duas chaves de cor para o mesmo tiro
                // sairiam de sincronia no dia em que a bola mudasse de cor.
                ImpactColor = impactColor,

                // Luz sozinha, e nao o estouro inteiro: conserta o vazamento de cor do prefab — o
                // rosa do xama goblin no terreno — sem apagar a variacao de tom que o efeito trazia
                // de fabrica. Julgado na tela em 2026-08-20.
                ImpactColorTarget = ImpactTintTarget.Light,

                // Desligado: o que morre aqui e' a copia local do projetil, no cliente de quem
                // atirou, e o rastro que sobrava depois do acerto lia como tiro que nao bateu.
                ProjectileLingerOnHit = false,

                ProjectileSpeed = config.Bind(section, "ProjectileSpeed", projectileSpeed,
                    new ConfigDescription(
                        "Projectile speed in metres per second. For reference, a player runs at " +
                        "about 5 and flies at up to 30. Too slow and anything mobile walks out of " +
                        "the way; too fast and there is nothing to see between the hand and the " +
                        "target.",
                        new AcceptableValueRange<float>(1f, 200f), AdminOnly(70))),

                ProjectileLifetime = config.Bind(section, "ProjectileLifetime", projectileLifetime,
                    new ConfigDescription(
                        "Seconds the projectile lives before vanishing. Range is this times " +
                        "ProjectileSpeed — saiya_blast prints the result in metres. Overrides the " +
                        "prefab's own lifetime.",
                        new AcceptableValueRange<float>(0.5f, 30f), AdminOnly(65))),

                ProjectileScale = projectileScale,

                // A cor do ataque. Amarelo no blast desde o playtest de 2026-08-20 — antes era vazio, "o prefab manda", que
                // era a posicao certa enquanto nao se sabia que cor o ki tinha. Agora se sabe, e
                // vazio faria o tiro nascer com a cor do prefab emprestado (verde de raiz, laranja
                // de fogo) em vez da do mod.
                //
                // O ImpactColor pendura nele: vazio la' significa "a cor deste tiro".
                ProjectileColor = projectileColor,

                MinPowerLevel = config.Bind(section, "MinPowerLevel", 0f,
                    new ConfigDescription(
                        "Minimum Power Level required to use this attack. 0 disables the " +
                        "gate. Independent of the boss gate below: with both set, the attack needs " +
                        "both. Left at 0 like the forms, because the ladder is paced by bosses.",
                        new AcceptableValueRange<float>(0f, 100f), AdminOnly(45))),

                RequiredGlobalKey = config.Bind(section, "RequiredGlobalKey", requiredGlobalKey,
                    new ConfigDescription(
                        "Global key of the boss that unlocks this attack. Empty disables the gate. " +
                        "The key belongs to the WORLD, so the server syncs it for free and someone " +
                        "joining later arrives with whatever the group has already killed.\n" +
                        "The five valid keys, and mind that two are NOT named after the boss: " +
                        "defeated_eikthyr (Eikthyr), defeated_gdking (The Elder), " +
                        "defeated_bonemass (Bonemass), defeated_dragon (MODER), " +
                        "defeated_goblinking (YAGLUTH). A key that does not exist is not an error " +
                        "— it is an attack that never unlocks. Check it with saiya_blast.",
                        null, AdminOnly(40)))
            };
        }

        /// <summary>
        /// Entrada imposta pelo servidor no multiplayer (etapa 8).
        ///
        /// <b>Tem que ser o <c>ConfigurationManagerAttributes</c> do Jotunn</b>, o do namespace
        /// global — daí o <c>global::</c>. O <c>SynchronizationManager</c> reconhece a entrada
        /// sincronizável por <c>x is ConfigurationManagerAttributes</c>, identidade de tipo e não
        /// nome. Até 2026-09-21 o mod tinha uma cópia própria em <c>Saiyaheim</c>, que fazia sombra
        /// à do Jotunn: nenhuma chave era sincronizada, o servidor mandava um pacote vazio e cada
        /// cliente jogava com o próprio <c>.cfg</c>.
        /// </summary>
        private static global::ConfigurationManagerAttributes AdminOnly(int order) =>
            new global::ConfigurationManagerAttributes { IsAdminOnly = true, Order = order };

        /// <summary>Entrada local de cada jogador; o servidor não interfere.</summary>
        private static global::ConfigurationManagerAttributes ClientSide(int order) =>
            new global::ConfigurationManagerAttributes { IsAdminOnly = false, Order = order };
    }
}
