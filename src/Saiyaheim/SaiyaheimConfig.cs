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
        /// O que vale para <b>todos</b> os ataques de ki: as duas teclas moram na seção 1, com as
        /// outras, e aqui fica o que é da mecânica e não de um ataque específico. Hoje é só o
        /// tempo mínimo entre disparos de ataques diferentes — ver <c>MinimumInterval</c>.
        /// </summary>
        private const string SecKiAttacks = "4 - Ki Attacks";

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
        private const string SecEffects = "8 - Effects";

        /// <summary>
        /// A pose procedural de carregamento de ki, músculo a músculo.
        ///
        /// <b>Seção temporária, de propósito.</b> Estes números estão aqui pelo mesmo motivo que os
        /// da pose de voo estiveram: pose é julgamento visual, quem vê a tela é o Henrique, e
        /// recompilar a cada ajuste de meio grau mataria a iteração. Quando a pose estiver
        /// calibrada ela vira constante no código — como o <c>FlightPose</c> virou em 2026-07-31 —
        /// e esta seção some do <c>.cfg</c> de quem instalar depois.
        ///
        /// Client-side: a pose é desenho local, cada jogador aplica a sua em todo mundo que vê.
        ///
        /// 📌 <b>Calibrada em 2026-08-07</b> e os treze valores promovidos para cá — mas a seção
        /// <b>continua</b>, e não virou <c>const</c> como a do voo. A diferença é que a do voo foi
        /// declarada fechada e esta ainda não: o Henrique disse que ajusta jogando. Quando fechar,
        /// vira <c>const</c> no <c>KiChargePose</c> e some do <c>.cfg</c>.
        /// </summary>
        private const string SecChargePose = "8.1 - Ki Charge Pose";

        /// <summary>
        /// A pose procedural de disparo do ki blast.
        ///
        /// <b>Temporária pelo mesmo motivo da 8.1</b>, e provavelmente por menos tempo: são poucas
        /// chaves e um gesto só. Quando estiver calibrada na tela ela vira constante no
        /// <c>KiBlastPose</c> — como o <c>FlightPose</c> virou em 2026-07-31 — e esta seção some do
        /// <c>.cfg</c> de quem instalar depois.
        ///
        /// Client-side: pose é desenho local.
        /// </summary>
        private const string SecBlastPose = "8.2 - Ki Blast Pose";

        /// <summary>
        /// A pose procedural do Kamehameha: a concha ao lado do quadril e o empurrão de duas mãos.
        ///
        /// <b>É a maior das três seções de pose</b>, e não por falta de corte: é a primeira pose com
        /// <b>duas fases</b> e <b>dois lados</b>. Um alvo de braço aparece quatro vezes —
        /// <c>Charge</c>/<c>Release</c> vezes <c>Cup</c>/<c>Cross</c> —, porque nenhuma das duas
        /// divisões se provou dispensável na tela: as fases são gestos diferentes, e os dois braços
        /// que se encontram num ponto do corpo não fazem a mesma coisa em eixo nenhum.
        ///
        /// O que não depende nem de fase nem de lado (pernas, pesos de grupo, seguimento de mira)
        /// continua com uma chave só.
        ///
        /// <b>Temporária pelo mesmo motivo das outras duas.</b> Quando estiver calibrada na tela,
        /// vira constante no <c>KiBeamPose</c> e some do <c>.cfg</c> de quem instalar depois.
        ///
        /// Client-side: pose é desenho local.
        /// </summary>
        private const string SecBeamPose = "8.3 - Kamehameha Pose";

        private const string SecDebug = "9 - Debug";

        /// <summary>
        /// O que este cliente desenha dos <b>outros</b> jogadores. Client-side de propósito: é
        /// preferência e diagnóstico de quem está na frente da tela, não regra do servidor.
        ///
        /// As duas chaves existem separadas porque servem para <b>bissecar</b>. A etapa 8 é
        /// validada numa sessão marcada com amigo, não iterando — quando alguém disser "ficou
        /// estranho quando o outro transformou", a pergunta seguinte é qual dos dois sistemas, e
        /// uma chave só não responde.
        /// </summary>
        private const string SecMultiplayer = "10 - Multiplayer";

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

        /// <summary>Intervalo do tick de ki. Regeneração é por tick fixo, nunca por frame.</summary>
        public static ConfigEntry<float> KiTickInterval { get; private set; }

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

        // ---------- 2.1 - Combat ----------

        /// <summary>Ki gasto por ponto de dano que o poder somou ao soco. Ki insuficiente não cancela o golpe, só tira o bônus.</summary>
        public static ConfigEntry<float> PunchKiCostPerDamage { get; private set; }

        /// <summary>
        /// Taxa do desconto hiperbólico que o poder de combate dá nos <b>três</b> custos de ki do
        /// combate: soco, dano recebido e bloqueio. 0 desliga. Ver <c>BattlePower.KiCostFactorFor</c>.
        /// </summary>
        public static ConfigEntry<float> KiCostPowerReduction { get; private set; }

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

        public static ConfigEntry<bool> ShowKiBar { get; private set; }
        public static ConfigEntry<float> KiBarOffsetX { get; private set; }
        public static ConfigEntry<float> KiBarOffsetY { get; private set; }
        public static ConfigEntry<string> KiBarColor { get; private set; }
        public static ConfigEntry<bool> KiBarAlwaysVisible { get; private set; }

        /// <summary>Mostra o poder de luta na HUD, abaixo do minimapa.</summary>
        public static ConfigEntry<bool> ShowPowerOnHud { get; private set; }

        /// <summary>Deslocamento X do texto, relativo ao rótulo do bioma no minimapa.</summary>
        public static ConfigEntry<float> PowerHudOffsetX { get; private set; }

        /// <summary>Deslocamento Y do texto, relativo ao rótulo do bioma no minimapa.</summary>
        public static ConfigEntry<float> PowerHudOffsetY { get; private set; }

        /// <summary>Tamanho da fonte, em unidades de canvas.</summary>
        public static ConfigEntry<float> PowerHudFontSize { get; private set; }

        /// <summary>Texto antes do número.</summary>
        public static ConfigEntry<string> PowerHudLabel { get; private set; }

        /// <summary>Cor do texto.</summary>
        public static ConfigEntry<string> PowerHudColor { get; private set; }

        /// <summary>Mostra o poder de luta do inimigo abaixo da barra de vida dele.</summary>
        public static ConfigEntry<bool> ShowEnemyPowerOnHud { get; private set; }

        /// <summary>Deslocamento X do texto, relativo ao nome do inimigo.</summary>
        public static ConfigEntry<float> EnemyPowerOffsetX { get; private set; }

        /// <summary>Deslocamento Y do texto, relativo ao nome do inimigo.</summary>
        public static ConfigEntry<float> EnemyPowerOffsetY { get; private set; }

        /// <summary>Tamanho da fonte do texto do inimigo, em unidades de canvas.</summary>
        public static ConfigEntry<float> EnemyPowerFontSize { get; private set; }

        /// <summary>Texto antes do numero, no rotulo do inimigo.</summary>
        public static ConfigEntry<string> EnemyPowerLabel { get; private set; }

        /// <summary>Cor do texto do inimigo.</summary>
        public static ConfigEntry<string> EnemyPowerColor { get; private set; }

        /// <summary>Alinhamento horizontal do texto do inimigo dentro da largura do hud.</summary>
        public static ConfigEntry<HudTextAlign> EnemyPowerAlign { get; private set; }

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

            /// <summary>Fração do dreno removida no nível 100 da skill desta forma.</summary>
            public ConfigEntry<float> MasteryDrainReduction { get; internal set; }

            /// <summary>XP da skill desta forma por segundo transformado.</summary>
            public ConfigEntry<float> MasteryXpPerSecond { get; internal set; }

            /// <summary>
            /// Quanto o ganho de XP desta forma sobe por boss derrotado <b>depois</b> do boss que
            /// a destravou. 0 desliga. Ver <c>Transformation.GetBossXpMultiplier</c>.
            /// </summary>
            public ConfigEntry<float> MasteryXpPerBossBonus { get; internal set; }

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
            public ConfigEntry<string> HairItem { get; internal set; }

            /// <summary>Cor do cabelo enquanto a forma está ativa, em #RRGGBB. Vazio não pinta.</summary>
            public ConfigEntry<string> HairColor { get; internal set; }

            /// <summary>Multiplicador de brilho da cor acima. Acima de 1 estoura e queima.</summary>
            public ConfigEntry<float> HairColorIntensity { get; internal set; }

            /// <summary>Cor da aura desta forma, em #RRGGBB. Vazio mantém a cor do prefab.</summary>
            public ConfigEntry<string> AuraColor { get; internal set; }

            /// <summary>
            /// Se esta forma estala raios em volta do corpo enquanto está ativa. É a única chave
            /// que decide <b>quais</b> formas crepitam; a regulagem do efeito é compartilhada, na
            /// seção 8 (<c>FormLightning*</c>).
            /// </summary>
            public ConfigEntry<bool> LightningEnabled { get; internal set; }

            /// <summary>
            /// Cor dos raios desta forma, em #RRGGBB. Vazio cai na <see cref="AuraColor"/>, que é
            /// o caso normal — a forma tem uma cor só.
            /// </summary>
            public ConfigEntry<string> LightningColor { get; internal set; }

            /// <summary>
            /// Quanto esta forma brilha, como multiplicador da regulagem compartilhada da seção 8
            /// (<c>FormGlow*</c>). 0 apaga o brilho só nesta forma.
            /// </summary>
            public ConfigEntry<float> GlowIntensity { get; internal set; }

            /// <summary>
            /// Cor do brilho desta forma, em #RRGGBB. Vazio cai na <see cref="AuraColor"/>, que é
            /// o caso normal — mesma regra da <see cref="LightningColor"/>.
            /// </summary>
            public ConfigEntry<string> GlowColor { get; internal set; }
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

        // ---------- 4.x - Ki Attacks ----------

        /// <summary>
        /// Tempo mínimo entre dois disparos, <b>qualquer que seja o ataque</b>. O cooldown de cada
        /// ataque é dele; este é o piso comum, e existe para que trocar de ataque não seja um jeito
        /// de burlar cooldown.
        /// </summary>
        public static ConfigEntry<float> KiAttackMinimumInterval { get; private set; }

        /// <summary>
        /// Liga a convergência da mira: apontar o tiro da mão <b>para o ponto</b> que a cruz está
        /// olhando, em vez de copiar a direção da câmera. Ver <see cref="Attacks.KiAim"/>.
        /// </summary>
        public static ConfigEntry<bool> KiAttackAimConvergence { get; private set; }

        /// <summary>Até onde o raio da mira procura o ponto que o jogador está olhando.</summary>
        public static ConfigEntry<float> KiAttackAimRange { get; private set; }

        /// <summary>Quanto, em graus, a correção pode desviar do olhar. Trava de segurança.</summary>
        public static ConfigEntry<float> KiAttackAimMaxCorrection { get; private set; }

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

            /// <summary>Prefab do projétil, do <c>ZNetScene</c>. Ver [[Prefabs do Jogo]].</summary>
            public ConfigEntry<string> ProjectilePrefab { get; internal set; }

            /// <summary>
            /// O que toca onde o projétil bate. Vazio mantém o do prefab, <c>none</c> tira, um nome
            /// de prefab substitui. Ver [[Prefabs do Jogo]].
            /// </summary>
            public ConfigEntry<string> ImpactEffect { get; internal set; }

            /// <summary>
            /// Nomes de emissores de partícula a tirar do efeito de impacto — a fumaça, tipicamente.
            /// É o corte fino que o <see cref="ImpactEffect"/> não faz. Ver
            /// <c>Util/StrippedEffect.cs</c>.
            /// </summary>
            public ConfigEntry<string> ImpactEffectStrip { get; internal set; }

            /// <summary>
            /// Cor do efeito de impacto. Vazio segue o <c>ProjectileColor</c>, <c>none</c> mantém a
            /// do prefab, um hex manda. Ver <c>KiProjectile.ResolveImpactColor</c>.
            /// </summary>
            public ConfigEntry<string> ImpactColor { get; internal set; }

            /// <summary>Quanto do efeito de impacto a <see cref="ImpactColor"/> pinta.</summary>
            public ConfigEntry<ImpactTintTarget> ImpactColorTarget { get; internal set; }

            /// <summary>
            /// Deixa o projétil sobreviver ao próprio impacto, como o prefab queria. Desligado, ele
            /// some no acerto e não sobra rastro depois.
            /// </summary>
            public ConfigEntry<bool> ProjectileLingerOnHit { get; internal set; }

            /// <summary>Velocidade do projétil em m/s.</summary>
            public ConfigEntry<float> ProjectileSpeed { get; internal set; }

            /// <summary>Segundos de vida do projétil. Alcance = velocidade x isto.</summary>
            public ConfigEntry<float> ProjectileLifetime { get; internal set; }

            /// <summary>Gravidade sobre o projétil. 0 voa reto.</summary>
            public ConfigEntry<float> ProjectileGravity { get; internal set; }

            /// <summary>Escala do projétil. 1 é o tamanho do prefab.</summary>
            public ConfigEntry<float> ProjectileScale { get; internal set; }

            /// <summary>Cor do projétil, em #RRGGBB. Vazio mantém a cor do prefab.</summary>
            public ConfigEntry<string> ProjectileColor { get; internal set; }

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
            public ConfigEntry<float> ChargeMinScale { get; internal set; }

            /// <summary>Efeito preso ao jogador enquanto ele carrega. Vazio não mostra nada.</summary>
            public ConfigEntry<string> ChargeEffectPrefab { get; internal set; }

            /// <summary>Cor do efeito de carregamento. Vazio segue o <see cref="ProjectileColor"/>.</summary>
            public ConfigEntry<string> ChargeEffectColor { get; internal set; }

            /// <summary>Escala do efeito de carregamento na carga cheia.</summary>
            public ConfigEntry<float> ChargeEffectScale { get; internal set; }

            /// <summary>Altura do efeito de carregamento, a partir dos pés.</summary>
            public ConfigEntry<float> ChargeEffectHeight { get; internal set; }

            /// <summary>Deslocamento lateral do efeito. Positivo é para a direita do jogador.</summary>
            public ConfigEntry<float> ChargeEffectSide { get; internal set; }

            /// <summary>Deslocamento para frente do efeito.</summary>
            public ConfigEntry<float> ChargeEffectForward { get; internal set; }

            /// <summary>
            /// Onde os efeitos de carregamento se prendem. Preso na mão, a animação os carrega.
            /// </summary>
            public ConfigEntry<EffectAnchor> ChargeEffectAnchor { get; internal set; }

            /// <summary>
            /// A bola que junta na mão. Prefab de <b>projétil</b>, parado — ver
            /// <c>Util/StaticProp.cs</c>. Vazio não mostra nada.
            /// </summary>
            public ConfigEntry<string> ChargeBallPrefab { get; internal set; }

            /// <summary>Cor da bola. Vazio segue o <see cref="ProjectileColor"/>.</summary>
            public ConfigEntry<string> ChargeBallColor { get; internal set; }

            /// <summary>Tamanho da bola na carga cheia. Ela cresce da <see cref="ChargeMinScale"/> até aqui.</summary>
            public ConfigEntry<float> ChargeBallScale { get; internal set; }

            /// <summary>Emissores a tirar da bola — o rastro que o projétil deixava ao voar.</summary>
            public ConfigEntry<string> ChargeBallStrip { get; internal set; }

            /// <summary>Efeito que marca a carga cheia. Vazio não mostra nada.</summary>
            public ConfigEntry<string> ChargeFullEffectPrefab { get; internal set; }

            /// <summary>Cor do efeito de carga cheia. Vazio segue o <see cref="ProjectileColor"/>.</summary>
            public ConfigEntry<string> ChargeFullEffectColor { get; internal set; }

            /// <summary>Escala do efeito de carga cheia.</summary>
            public ConfigEntry<float> ChargeFullEffectScale { get; internal set; }

            /// <summary>
            /// Segura o efeito de carga cheia enquanto o jogador continuar segurando, em vez de
            /// tocá-lo uma vez no instante em que a carga enche.
            /// </summary>
            public ConfigEntry<bool> ChargeFullEffectLoop { get; internal set; }

            /// <summary>
            /// A carga cheia <b>substitui</b> o efeito de carregamento em vez de somar-se a ele.
            /// </summary>
            public ConfigEntry<bool> ChargeFullEffectReplaces { get; internal set; }

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

        /// <summary>
        /// Barateamento hiperbólico do voo vindo do termo de fim de jogo. É a única coisa do voo
        /// que esse termo toca — velocidade fica de fora.
        /// </summary>
        public static ConfigEntry<float> FlightKiPowerReduction { get; private set; }

        /// <summary>XP da skill de voo por segundo voando.</summary>
        public static ConfigEntry<float> FlightXpPerSecond { get; private set; }

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

        /// <summary>Pousar encosta no chão desliga o voo sozinho.</summary>
        public static ConfigEntry<bool> FlightAutoLandOnGround { get; private set; }

        /// <summary>
        /// Mantém o corpo na horizontal, tirando a inclinação que subir/descer causa.
        /// Ver <c>FlightPosePatch.LevelBody</c>.
        /// </summary>
        public static ConfigEntry<bool> FlightLevelBody { get; private set; }


        /// <summary>
        /// Força a pose em pé no animator enquanto voa. Confirmado no playtest de 2026-07-31:
        /// funciona. Fica em config para desligar sem recompilar se alguma animação futura
        /// conflitar. Ver <c>FlightPosePatch</c>.
        /// </summary>
        public static ConfigEntry<bool> FlightForceIdlePose { get; private set; }

        // A pose procedural de voo não tem config: os valores foram calibrados no playtest de
        // 2026-07-31 e viraram constantes em <c>FlightPose</c>. São decisão de arte fechada, não
        // balanceamento — não há motivo para outro jogador querer números diferentes.

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

        /// <summary>Peso da vida efetiva no poder de luta escaneável. Vale para jogador e inimigo.</summary>
        public static ConfigEntry<float> RatingK1Health { get; private set; }

        /// <summary>Quanta armadura dobra a vida efetiva. Inimigo tem 0, então não é afetado.</summary>
        public static ConfigEntry<float> RatingArmorScale { get; private set; }

        /// <summary>Peso do dano por segundo no poder de luta. Vale para jogador e inimigo.</summary>
        public static ConfigEntry<float> RatingK2Damage { get; private set; }

        /// <summary>
        /// Segundos entre dois golpes do jogador. É a única entrada do poder de luta que não sai
        /// do jogo: a cadência do jogador vive na animação, não no item.
        /// </summary>
        public static ConfigEntry<float> RatingPlayerHitInterval { get; private set; }

        // Aqui morava o PowerCompressionExponent, removido no playtest da etapa 10: um expoente
        // sobre o valor vira o mesmo expoente sobre a razao, e ele achatava justamente as
        // diferencas que o numero existe para mostrar. Ver PowerRating.GetDisplay.

        /// <summary>Multiplicador linear do número exibido. Só escolhe o tamanho, não distorce razão.</summary>
        public static ConfigEntry<float> PowerDisplayScale { get; private set; }

        // ---------- 8 - Effects ----------

        // Aqui morava o ChargeEmote, removido em 2026-08-07: o emote de carregamento saiu inteiro
        // quando a pose procedural entrou. Ver KiChargePose.

        public static ConfigEntry<string> ChargeEffectPrefab { get; private set; }
        public static ConfigEntry<string> ChargeSoundPrefab { get; private set; }
        public static ConfigEntry<string> ChargeEffectColor { get; private set; }
        public static ConfigEntry<float> ChargeEffectScale { get; private set; }
        public static ConfigEntry<bool> ChargeEffectForceLoop { get; private set; }

        // ---------- 8.1 - Ki Charge Pose ----------
        //
        // Espaco de intencao, nao espaco de musculo: **positivo e sempre "mais do que o nome diz"**.
        // A traducao para o sinal do musculo da Unity mora em KiChargePose, nas constantes
        // LeanSign/HeadTiltSign/ShrugSign. Se alguma coisa sair invertida na tela, o conserto e la
        // e nao aqui.
        //
        // A excecao e o ArmDown, que e alvo absoluto no espaco de musculo — mesma convencao do
        // HoverArmSpread do voo, e por isso mesmo esta documentada na descricao dele.

        /// <summary>Desliga a pose. Sem ela o carregamento não tem animação nenhuma.</summary>
        public static ConfigEntry<bool> ChargePoseEnabled { get; private set; }

        public static ConfigEntry<float> ChargePoseBlendSeconds { get; private set; }

        // Um peso por grupo de músculos, porque **zero num alvo não quer dizer "não mexe"**:
        // "Left Arm Down-Up = 0" e' T-pose e "Left Upper Leg In-Out = 0" e' pernas juntas. Dizer
        // "deixa como a animacao deixou" e' nao escrever aquele musculo, e e' isso que peso zero
        // faz. Serve para acender um grupo por vez na calibragem.
        // O tronco sao **tres** grupos e nao um: peito, peito alto e lombar sao articulacoes
        // diferentes no rig do Valheim, e so a lombar arrasta o quadril. Ver KiChargePose.
        public static ConfigEntry<float> ChargePoseChestWeight { get; private set; }
        public static ConfigEntry<float> ChargePoseUpperChestWeight { get; private set; }
        public static ConfigEntry<float> ChargePoseSpineWeight { get; private set; }
        public static ConfigEntry<float> ChargePoseShoulderWeight { get; private set; }
        public static ConfigEntry<float> ChargePoseHeadWeight { get; private set; }
        public static ConfigEntry<float> ChargePoseArmWeight { get; private set; }
        public static ConfigEntry<float> ChargePoseLegWeight { get; private set; }
        public static ConfigEntry<float> ChargePoseChestLean { get; private set; }
        public static ConfigEntry<float> ChargePoseUpperChestLean { get; private set; }
        public static ConfigEntry<float> ChargePoseSpineLean { get; private set; }
        public static ConfigEntry<float> ChargePoseShoulderShrug { get; private set; }
        public static ConfigEntry<float> ChargePoseHeadTilt { get; private set; }
        public static ConfigEntry<float> ChargePoseArmDown { get; private set; }
        public static ConfigEntry<float> ChargePoseArmBack { get; private set; }
        public static ConfigEntry<float> ChargePoseArmTwist { get; private set; }
        public static ConfigEntry<float> ChargePoseElbowBend { get; private set; }
        public static ConfigEntry<float> ChargePoseFistClench { get; private set; }
        public static ConfigEntry<float> ChargePoseStanceWidth { get; private set; }
        public static ConfigEntry<float> ChargePoseKneeBend { get; private set; }
        public static ConfigEntry<float> ChargePoseHipDrop { get; private set; }
        public static ConfigEntry<float> ChargePoseStrain { get; private set; }
        public static ConfigEntry<float> ChargePoseStrainSpeed { get; private set; }
        public static ConfigEntry<float> ChargePoseTremor { get; private set; }
        public static ConfigEntry<float> ChargePoseTremorSpeed { get; private set; }

        // ---------- 8.2 - Ki Blast Pose ----------
        //
        // Mesma convencao da 8.1: espaco de **intencao**, positivo e sempre "mais do que o nome
        // diz", e a traducao para o sinal do musculo da Unity mora no KiBlastPose (ForwardSign e
        // TwistSign). Se sair invertido na tela, o conserto e la.
        //
        // As excecoes sao ArmHeight, ArmTwist, ElbowBend, ShoulderLift e WristBend: alvos
        // ABSOLUTOS no espaco de musculo, porque nao ha nome de intencao honesto para "onde fica o
        // braco". Estao documentadas uma a uma.

        /// <summary>Desliga a pose. Sem ela o disparo não tem animação nenhuma.</summary>
        public static ConfigEntry<bool> BlastPoseEnabled { get; private set; }

        // O envelope. Assimetrico de proposito — ver o comentario no topo do KiBlastPose.
        public static ConfigEntry<float> BlastPoseRiseSeconds { get; private set; }
        public static ConfigEntry<float> BlastPoseHoldSeconds { get; private set; }
        public static ConfigEntry<float> BlastPoseFallSeconds { get; private set; }

        // Um peso por grupo, porque **zero num alvo nao quer dizer "nao mexe"**: "Right Arm Down-Up
        // = 0" e' T-pose. Dizer "deixa como a animacao deixou" e' nao escrever aquele musculo, e e'
        // isso que peso zero faz.
        public static ConfigEntry<float> BlastPoseArmWeight { get; private set; }
        public static ConfigEntry<float> BlastPoseShoulderWeight { get; private set; }
        public static ConfigEntry<float> BlastPoseTorsoWeight { get; private set; }
        public static ConfigEntry<float> BlastPoseSpineTwistWeight { get; private set; }

        public static ConfigEntry<float> BlastPoseArmForward { get; private set; }
        public static ConfigEntry<float> BlastPoseArmHeight { get; private set; }
        public static ConfigEntry<float> BlastPoseAimFollowPitch { get; private set; }
        public static ConfigEntry<float> BlastPoseAimFollowYaw { get; private set; }
        public static ConfigEntry<float> BlastPoseAimYawTorsoShare { get; private set; }
        public static ConfigEntry<float> BlastPoseArmTwist { get; private set; }
        public static ConfigEntry<float> BlastPoseElbowStretch { get; private set; }
        public static ConfigEntry<float> BlastPoseShoulderPush { get; private set; }
        public static ConfigEntry<float> BlastPoseShoulderLift { get; private set; }
        public static ConfigEntry<float> BlastPoseTorsoTwist { get; private set; }
        public static ConfigEntry<float> BlastPoseHandOpen { get; private set; }
        public static ConfigEntry<float> BlastPoseWristBend { get; private set; }

        // ---------- 8.3 - Kamehameha Pose ----------
        //
        // Mesma convencao das outras duas: espaco de **intencao**, positivo e' sempre "mais do que
        // o nome diz", e a traducao para o sinal do musculo mora no KiBeamPose (ForwardSign e
        // LeanSign). As excecoes sao os alvos ABSOLUTOS — ArmHeight, ArmTwist, os cotovelos,
        // ShoulderLift, WristBend e KneeStretch —, porque nao ha nome de intencao honesto para
        // "onde fica o braco". Estao documentadas uma a uma.
        //
        // O que e' novo aqui e' o par Charge*/Release*: o mesmo alvo nas duas pontas do gesto, e a
        // pose interpola de um ao outro. Uma chave so' significaria um gesto so'.

        /// <summary>Desliga a pose. Sem ela o Kamehameha usa a pose de disparo do ki blast.</summary>
        public static ConfigEntry<bool> BeamPoseEnabled { get; private set; }

        // O envelope. Entra uma vez e sai uma vez, com a troca de fase no meio: o ReleaseRise e' a
        // costura entre as duas, e nao um segundo envelope.
        public static ConfigEntry<float> BeamPoseRiseSeconds { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseRiseSeconds { get; private set; }
        public static ConfigEntry<float> BeamPoseHoldSeconds { get; private set; }
        public static ConfigEntry<float> BeamPoseFallSeconds { get; private set; }

        // Um peso por grupo, porque **zero num alvo nao quer dizer "nao mexe"**.
        public static ConfigEntry<float> BeamPoseArmWeight { get; private set; }
        public static ConfigEntry<float> BeamPoseForearmWeight { get; private set; }
        public static ConfigEntry<float> BeamPoseShoulderWeight { get; private set; }
        public static ConfigEntry<float> BeamPoseTorsoWeight { get; private set; }
        public static ConfigEntry<float> BeamPoseSpineWeight { get; private set; }
        public static ConfigEntry<float> BeamPoseLegWeight { get; private set; }
        public static ConfigEntry<float> BeamPoseHandWeight { get; private set; }

        // ---------- Os dois lados ----------
        //
        // **Todo alvo de braco vem em par.** "Cup" e' o braco do lado do quadril onde a bola nasce,
        // "Cross" e' o que atravessa o corpo para encontra-lo. Com o ChargeEffectAnchor de fabrica
        // (mao direita), Cup e' o braco DIREITO na tela.
        //
        // Comecou com metade disto compartilhada — altura, torcao, ombros e pulso iguais nos dois
        // lados — e nao durou um playtest: dois bracos que se encontram num ponto do corpo nao
        // fazem a mesma coisa em eixo nenhum, e cada alvo compartilhado era um lado certo e um
        // errado. Desdobrado em 2026-09-07.
        //
        // Sao pares em Cup/Cross e nao em Left/Right de proposito: assim a pose espelha sozinha se
        // a bola mudar de mao, e o que o Henrique calibrou continua valendo.

        // A concha.
        public static ConfigEntry<float> BeamPoseChargeCupArmHeight { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCrossArmHeight { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCupArmForward { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCrossArmForward { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCupArmTwist { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCrossArmTwist { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCupForearmTwist { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCrossForearmTwist { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCupElbowBend { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCrossElbowBend { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCupShoulderPush { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCrossShoulderPush { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCupShoulderLift { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCrossShoulderLift { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCupWristBend { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCrossWristBend { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCupWristSide { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeCrossWristSide { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeTorsoTwist { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeTorsoLean { get; private set; }
        public static ConfigEntry<float> BeamPoseChargeHandCup { get; private set; }

        // ⚠️ **Girar o corpo nao e' torcer a coluna, e as duas chaves nao sao a mesma coisa.** O
        // TorsoTwist e' musculo: dobra o tronco e deixa o quadril onde estava. O BodyYaw e' a RAIZ
        // do humanoide (bodyRotation), e leva ombro, quadril e pernas juntos — e' o unico jeito de
        // por o personagem de lado. Em graus, e nao em espaco de musculo, porque a raiz e' rotacao
        // de verdade.
        public static ConfigEntry<float> BeamPoseChargeBodyYaw { get; private set; }

        // O empurrao. Os dois bracos convergem para o mesmo gesto, mas continuam vindo de lugares
        // diferentes — e e' por isso que o par sobrevive tambem aqui.
        public static ConfigEntry<float> BeamPoseReleaseCupArmHeight { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCrossArmHeight { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCupArmForward { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCrossArmForward { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCupArmTwist { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCrossArmTwist { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCupForearmTwist { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCrossForearmTwist { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCupElbowStretch { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCrossElbowStretch { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCupShoulderPush { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCrossShoulderPush { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCupShoulderLift { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCrossShoulderLift { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCupWristBend { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCrossWristBend { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCupWristSide { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseCrossWristSide { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseTorsoTwist { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseTorsoLean { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseBodyYaw { get; private set; }
        public static ConfigEntry<float> BeamPoseReleaseHandOpen { get; private set; }

        // A mira so' vale no empurrao — durante a concha as maos estao no quadril e nao ha para
        // onde apontar. E' o TRONCO que mira, nos dois eixos: mirar pelos bracos quebra com dois
        // bracos espelhados (gimbal na vertical, saturacao de um lado so' na horizontal).
        public static ConfigEntry<float> BeamPoseAimFollowPitch { get; private set; }
        public static ConfigEntry<float> BeamPoseAimFollowYaw { get; private set; }
        public static ConfigEntry<float> BeamPoseBodyYawCompensation { get; private set; }

        // As pernas sao as mesmas nas duas fases: e' a base que segura a carga e absorve o
        // empurrao.
        public static ConfigEntry<float> BeamPoseStanceWidth { get; private set; }
        public static ConfigEntry<float> BeamPoseKneeStretch { get; private set; }
        public static ConfigEntry<float> BeamPoseHipDrop { get; private set; }

        // As duas senoides, e elas somem junto com a concha: o empurrao e' curto demais para
        // oscilar sem virar outra coisa.
        public static ConfigEntry<float> BeamPoseStrain { get; private set; }
        public static ConfigEntry<float> BeamPoseStrainSpeed { get; private set; }
        public static ConfigEntry<float> BeamPoseTremor { get; private set; }
        public static ConfigEntry<float> BeamPoseTremorSpeed { get; private set; }

        /// <summary>Emote de disparo único tocado ao transformar. Vazio desliga.</summary>
        public static ConfigEntry<string> TransformEmote { get; private set; }

        /// <summary>Prefab da aura que fica acesa enquanto a forma dura. Vazio desliga.</summary>
        public static ConfigEntry<string> TransformAuraPrefab { get; private set; }

        public static ConfigEntry<float> TransformAuraScale { get; private set; }

        /// <summary>Segundos que o estouro dura. 0 devolve a decisão ao prefab.</summary>
        public static ConfigEntry<float> TransformAuraDuration { get; private set; }

        public static ConfigEntry<bool> TransformAuraForceLoop { get; private set; }

        /// <summary>Multiplicador da luz dinâmica da aura. 0 apaga; 1 é o prefab como veio.</summary>
        public static ConfigEntry<float> TransformAuraLightIntensity { get; private set; }

        // Os raios das formas altas. A regulagem é COMPARTILHADA e mora aqui; quais formas
        // crepitam e de que cor é por forma (LightningEnabled/LightningColor). É a mesma divisão
        // que a aura já usa — "o prefab é compartilhado; a cor é a identidade".

        /// <summary>Prefab de um estalo de raio. Vazio desliga os raios em todas as formas.</summary>
        public static ConfigEntry<string> FormLightningPrefab { get; private set; }

        /// <summary>Segundos entre um estalo e o próximo, antes do sorteio do jitter.</summary>
        public static ConfigEntry<float> FormLightningInterval { get; private set; }

        /// <summary>Quanto o intervalo varia, em fração dele. 0 vira metrônomo.</summary>
        public static ConfigEntry<float> FormLightningIntervalJitter { get; private set; }

        /// <summary>Quantos raios saem por estalo.</summary>
        public static ConfigEntry<int> FormLightningCount { get; private set; }

        public static ConfigEntry<float> FormLightningScale { get; private set; }

        /// <summary>Raio do cilindro em volta do corpo onde os estalos nascem, em metros.</summary>
        public static ConfigEntry<float> FormLightningRadius { get; private set; }

        /// <summary>Altura do centro do cilindro acima dos pés, em metros.</summary>
        public static ConfigEntry<float> FormLightningHeight { get; private set; }

        /// <summary>Altura total do cilindro, em metros.</summary>
        public static ConfigEntry<float> FormLightningSpread { get; private set; }

        /// <summary>Segundos que cada estalo dura. 0 devolve a decisão ao prefab.</summary>
        public static ConfigEntry<float> FormLightningDuration { get; private set; }

        /// <summary>Multiplicador da luz dinâmica de cada estalo. 0 apaga e deixa só as partículas.</summary>
        public static ConfigEntry<float> FormLightningLightIntensity { get; private set; }

        // O brilho das formas. Mesma divisão de sempre — a regulagem é compartilhada e mora aqui;
        // quanto cada forma brilha e de que cor é por forma (GlowIntensity/GlowColor).

        /// <summary>Intensidade da luz da forma, antes do multiplicador dela. 0 desliga em todas.</summary>
        public static ConfigEntry<float> FormGlowIntensity { get; private set; }

        /// <summary>Até onde a luz alcança, em metros.</summary>
        public static ConfigEntry<float> FormGlowRange { get; private set; }

        /// <summary>Altura da luz acima dos pés, em metros.</summary>
        public static ConfigEntry<float> FormGlowHeight { get; private set; }

        /// <summary>Amplitude da respiração da luz, em fração da intensidade. 0 vira lâmpada.</summary>
        public static ConfigEntry<float> FormGlowPulseAmount { get; private set; }

        /// <summary>Velocidade da respiração, em ciclos por segundo.</summary>
        public static ConfigEntry<float> FormGlowPulseSpeed { get; private set; }

        /// <summary>Segundos que a luz leva para acender e para apagar. 0 é instantâneo.</summary>
        public static ConfigEntry<float> FormGlowFade { get; private set; }

        /// <summary>A luz projeta sombras. Caro: são seis mapas de sombra por quadro.</summary>
        public static ConfigEntry<bool> FormGlowShadows { get; private set; }

        // ---------- 10 - Multiplayer ----------

        /// <summary>Poses procedurais dos outros jogadores: voo, carregamento e disparo.</summary>
        public static ConfigEntry<bool> ShowRemotePoses { get; private set; }

        /// <summary>Efeitos dos outros jogadores: o estouro da transformação e o brilho do carregamento.</summary>
        public static ConfigEntry<bool> ShowRemoteEffects { get; private set; }

        // ---------- 9 - Debug ----------

        public static ConfigEntry<bool> VerboseLogging { get; private set; }

        public static void Init(ConfigFile config)
        {
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

            PowerDownKey = config.Bind(SecGeral, "PowerDownKey",
                new KeyboardShortcut(KeyCode.G),
                new ConfigDescription(
                    "Key that drops you straight back to base form, from whatever step you are " +
                    "on — no walking back down the ladder. Running out of ki does the same thing " +
                    "on its own: at zero there is nothing to hold any form with.",
                    null, ClientSide(82)));

            TransformStepDownKey = config.Bind(SecGeral, "TransformStepDownKey",
                new KeyboardShortcut(KeyCode.G, KeyCode.LeftShift),
                new ConfigDescription(
                    "Key that goes DOWN one step, to trade power for a smaller ki drain without " +
                    "leaving the ladder entirely. From the first form it returns to base.",
                    null, ClientSide(81)));

            // V e Shift+V pelo mesmo desenho de T/G: a acao comum num toque, a troca no Shift. Nao
            // sao G nem H porque G ja e' o power down — e disparar e destransformar sao as duas
            // teclas que mais se aperta com pressa, entao vizinhas seria pedir engano.
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

            KiTickInterval = config.Bind(SecKi, "KiTickInterval", 0.25f,
                new ConfigDescription(
                    "Ki tick interval in seconds (regeneration and drain). " +
                    "A smaller value reads smoother and costs more CPU.",
                    new AcceptableValueRange<float>(0.05f, 1f), AdminOnly(80)));

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
            PunchKiCostPerDamage = config.Bind(SecCombat, "PunchKiCostPerDamage", 3f,
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
                    "cost readable.)",
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
                    "How much of the EXTRA combat ki cost that a transformation adds is paid back " +
                    "by that form's mastery. Full formula for the discount on the three combat " +
                    "costs: 1 / (1 + KiCostPowerReduction * combat power + (PowerMultiplier - 1) * " +
                    "(mastery level / 100) * this). 0 disables it and the formula is exactly what " +
                    "it was before this key existed. \n" +
                    "1 is the meaningful point: at mastery 100 a punch in the form costs EXACTLY " +
                    "what the same punch costs out of form, while still landing PowerMultiplier " +
                    "times the damage. Mastering a form stops it from charging extra to fight in. \n" +
                    "Why it is written against the multiplier and not as a flat rate per mastery " +
                    "level: the premium a form charges IS its multiplier, so the payback has to " +
                    "scale with it. A flat rate would need 0.01 for a x2 form and 0.02 for a x3 " +
                    "one, and no single number could hit both. This way every rung of the ladder, " +
                    "including ones that do not exist yet, lands on its own base cost at " +
                    "mastery 100 with no retuning. \n" +
                    "Above 1 the maxed form costs LESS than the base, which is the dial for the " +
                    "other problem: at 1 the freshly unlocked higher rung is still more ki " +
                    "efficient than the mastered lower one, so there is no reason to step back " +
                    "down. Around 2.5 the mastered SSJ overtakes a fresh SSJ2 in damage per ki " +
                    "and the lower rung gets a niche of its own. Which of the two targets is " +
                    "right is a playtest question, and this key is the whole answer to it. \n" +
                    "Exact only for the punch: taking hits and blocking are charged per point the " +
                    "ki armor absorbed, and absorption does not scale linearly with the " +
                    "multiplier, so those two land near the base cost rather than on it. \n" +
                    "It reads the mastery of the ACTIVE form, which starts at zero on every new " +
                    "rung. Out of form there is no mastery to read and only the power discount " +
                    "applies. \n" +
                    "Note this gives mastery a SECOND payoff next to the drain reduction. It stays " +
                    "on the economy axis, not the power axis, so a form still never hits harder " +
                    "for being trained. \n" +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<float>(0f, 3f), AdminOnly(94)));

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
            DamageTakenKiCost = config.Bind(SecCombat, "DamageTakenKiCost", 1f,
                new ConfigDescription(
                    "Ki consumed per point of damage the ki armor ABSORBED. Taking a hit costs ki " +
                    "the same way landing one does — the ki armor is sustained, not free. " +
                    "Damage that armor does not touch (poison, fall, drowning) absorbs nothing and " +
                    "therefore costs nothing. Set to zero to disable. " +
                    "Note the cost per hit is naturally capped near your armor value: armor can " +
                    "never absorb more than it is worth, so a huge hit does not drain the bar. " +
                    "(Playtest value, 2026-08-01. The conservative 0.15 it shipped with the same " +
                    "day was barely noticeable; at 1 a blocked point of damage costs a point of ki.)",
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
            BlockKiCost = config.Bind(SecCombat, "BlockKiCost", 0.5f,
                new ConfigDescription(
                    "Ki consumed per point of damage the ki BLOCK stopped, measured (not estimated) " +
                    "from the hit before and after Humanoid.BlockAttack. Same rule as the armor: if " +
                    "it stopped damage, it costs ki. A failed block stops nothing and costs nothing. " +
                    "Unlike the punch, an empty bar does not cancel anything — the block already " +
                    "happened when the charge lands, so it drains what is there, like the armor does. " +
                    "This is the most expensive thing in the mod by design: blocking stops far more " +
                    "damage than armor absorbs, so it should be a beam, not a stance. Lower it if " +
                    "holding block for two hits empties the bar. Set to zero to make blocking free. " +
                    "(Starting value, 2026-08-01. Not playtested yet.)",
                    new AcceptableValueRange<float>(0f, 5f), AdminOnly(54)));

            // --- HUD ---
            ShowKiBar = config.Bind(SecHud, "ShowKiBar", true,
                new ConfigDescription("Show the ki bar.", null, ClientSide(100)));

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

            KiBarColor = config.Bind(SecHud, "KiBarColor", "#4FC3F7",
                new ConfigDescription("Ki bar color, #RRGGBB format. Applies when the config reloads.",
                    null, ClientSide(70)));

            KiBarAlwaysVisible = config.Bind(SecHud, "KiBarAlwaysVisible", false,
                new ConfigDescription(
                    "If true, the ki bar stays on screen at all times. " +
                    "If false (default), it hides when ki is full and comes back when you spend, " +
                    "just like the native stamina and eitr bars. " +
                    "With ki turned OFF the bar hides in both cases.",
                    null, ClientSide(60)));

            // --- Poder de luta na HUD (etapa 10) ---
            // Abaixo do minimapa, clonado do rotulo do bioma. Todas as chaves sao client-side —
            // sao posicao e gosto de quem esta na frente da tela, nao balanceamento.
            ShowPowerOnHud = config.Bind(SecHud, "ShowPowerOnHud", true,
                new ConfigDescription(
                    "Shows your battle power on the HUD, under the minimap. It follows the small " +
                    "minimap: it hides with the big map open, and with the minimap turned off in " +
                    "the game options — where it would otherwise float in an empty corner. It " +
                    "also hides while ki is turned off, same as the ki bar and the enemy's " +
                    "number: with the toggle off there is no battle power to read.",
                    null, ClientSide(50)));

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

            PowerHudLabel = config.Bind(SecHud, "PowerHudLabel", "PB:",
                new ConfigDescription(
                    "Text printed before the number. The separating space is added for you, and " +
                    "surrounding whitespace here is ignored - BepInEx trims this file's values on " +
                    "the way in and out, so a space typed at the end would never survive anyway. " +
                    "Empty shows the bare number.",
                    null, ClientSide(43)));

            PowerHudColor = config.Bind(SecHud, "PowerHudColor", "#FFFFFF",
                new ConfigDescription(
                    "Text colour, as hex. Defaults to white, which is what the biome label uses.",
                    null, ClientSide(42)));

            // --- Poder de luta do inimigo (etapa 10) ---
            // O outro lado do bloco acima: o mesmo numero, na mesma escala, escrito embaixo da
            // barra de vida do inimigo. Tambem client-side, pelo mesmo motivo — e' posicao na
            // tela, nao balanceamento. Ver EnemyPowerHud.
            ShowEnemyPowerOnHud = config.Bind(SecHud, "ShowEnemyPowerOnHud", true,
                new ConfigDescription(
                    "Shows the enemy's battle power under its health bar. Same scale as your own " +
                    "number, so the two can be compared directly - that comparison is the whole " +
                    "point of the stat. Only shows while YOUR ki is turned on: reading an enemy's " +
                    "power is something the ki lets you do, so it goes away with the toggle, just " +
                    "like the ki bar. Does NOT show on other players: their skill levels and " +
                    "status effects are not replicated to this machine, so the number would come " +
                    "out too low. Applies live.",
                    null, ClientSide(40)));

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

            EnemyPowerLabel = config.Bind(SecHud, "EnemyPowerLabel", "PB:",
                new ConfigDescription(
                    "Text printed before the number. The separating space is added for you, and " +
                    "surrounding whitespace here is ignored. Empty shows the bare number, which " +
                    "is the tidier option when several enemies are on screen.",
                    null, ClientSide(36)));

            EnemyPowerColor = config.Bind(SecHud, "EnemyPowerColor", "#FFFFFF",
                new ConfigDescription(
                    "Text colour, as hex. Defaults to white, matching the enemy's name above it.",
                    null, ClientSide(35)));

            EnemyPowerAlign = config.Bind(SecHud, "EnemyPowerAlign", HudTextAlign.Center,
                new ConfigDescription(
                    "Horizontal alignment of the text INSIDE the hud's own width - the same box " +
                    "the enemy name is centred in, which is about as wide as the health bar. " +
                    "Right puts the number at the bar's right end, Center (default) keeps it " +
                    "under the middle like the name. Only the horizontal alignment is touched, so the " +
                    "vertical one stays as the cloned name label had it. Combine with " +
                    "EnemyPowerOffsetX to nudge it past the edge. " +
                    "(Playtest value, 2026-09-06. Centred beats right-aligned: stacked under the " +
                    "name, the two lines read as one label instead of two loose bits of text.)",
                    null, ClientSide(34)));

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
                kiDrainPerSecond: 5f,
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
                kiDrainPerSecond: 10f,
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
            //   CarryWeightBonus 400 = o dobro do SSJ2, seguindo a mesma leitura dos dois degraus
            //     anteriores.
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
                kiDrainPerSecond: 15f,
                punchSlashFraction: 0f,
                punchLightningFraction: 0.5f,
                carryWeightBonus: 400f,
                hairColor: "#FFE066",
                requiredGlobalKey: "defeated_bonemass",
                lightning: true,
                lightningColor: "#FFFFFF",
                // Brilha o dobro do SSJ, meio a mais que o SSJ2 — o mesmo passo de 0,5 por degrau.
                glowIntensity: 2f,
                hairItem: "SaiyaHair6");

            // --- Ataques de ki ---
            KiAttackMinimumInterval = config.Bind(SecKiAttacks, "MinimumInterval", 0.2f,
                new ConfigDescription(
                    "Minimum seconds between two ki attacks, whatever they are. Each attack has " +
                    "its own Cooldown; this is the shared floor, and it exists so that switching " +
                    "attacks is not a way around a cooldown.",
                    new AcceptableValueRange<float>(0f, 5f), AdminOnly(100)));

            // Correcao de mira, nao balanceamento — mas fica aqui porque muda ONDE o tiro cai, e
            // isso e' gameplay. Desligar existe so' para o playtest poder comparar com o antes.
            KiAttackAimConvergence = config.Bind(SecKiAttacks, "AimConvergence", true,
                new ConfigDescription(
                    "Aim the shot from the hand AT THE POINT the crosshair is on, instead of just " +
                    "copying the camera direction. " +
                    "Off, the shot leaves the hand on a line PARALLEL to the aim line — offset by " +
                    "the distance between the eye and the hand — and two parallel lines never " +
                    "meet, so it always lands below the crosshair, worse the closer the target is. " +
                    "This is the same defect the vanilla bow has. Leave it on; the switch exists " +
                    "to compare against the old behaviour.",
                    null, AdminOnly(98)));

            KiAttackAimRange = config.Bind(SecKiAttacks, "AimRange", 200f,
                new ConfigDescription(
                    "How far the aim ray looks for whatever the crosshair is on. Past this the " +
                    "shot is aimed at a point this far down the camera line, which is close " +
                    "enough — the eye-to-hand offset stops mattering long before that. " +
                    "Only worth raising if a ki attack ever outranges it.",
                    new AcceptableValueRange<float>(10f, 1000f), AdminOnly(97)));

            KiAttackAimMaxCorrection = config.Bind(SecKiAttacks, "AimMaxCorrection", 30f,
                new ConfigDescription(
                    "How far, in degrees, the correction may bend the shot away from where you " +
                    "are looking. Safety rail: aiming at the ground by your feet, the point under " +
                    "the crosshair can end up BEHIND the hand, and an uncapped correction would " +
                    "fire back at the player. Beyond this the shot bends partway, never inverts. " +
                    "90 effectively disables the cap.",
                    new AcceptableValueRange<float>(0f, 90f), AdminOnly(96)));

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
                damageBase: 10f,
                damageFromPower: 0.04f,
                kiCost: 20f,
                cooldown: 0.5f,
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
                requiredGlobalKey: "defeated_bonemass",
                // Teto, e nao valor fixo: 60 e' o feixe da carga CHEIA. Com 5 s de carregamento
                // sao 12 projeteis por segundo segurado, e o custo e o dano acompanham em linha
                // reta — segurar metade do tempo entrega metade de tudo.
                //
                // 24 na tela nao lia como feixe: com 0,025 s de intervalo eram 0,6 s de disparo, e
                // o que se via era uma rajada curta. Playtest de 2026-09-07 subiu para 60, e a
                // cadencia por segundo ficou igual — o que mudou foi quanto tempo o feixe DURA.
                beamCount: 60,
                beamInterval: 0.025f,
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
                chargeMinScale: 0.4f,
                // O carregamento de cajado dos Charred: particulas convergindo para um ponto, que
                // e' o gesto certo. Catalogado em [[Prefabs do Jogo]] justamente para isto.
                chargeEffectPrefab: "fx_charred_firestaff_chargeup",
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
            FlightKiPerSecond = config.Bind(SecFlight, "KiPerSecond", 5f,
                new ConfigDescription(
                    "Ki per second while flying. Flight should be a tool, not the default way to " +
                    "get around — otherwise the game's hostile terrain turns into scenery. " +
                    "Running out of ki in the air drops you. " +
                    "(Playtest value, 2026-08-13. Went 4 → 15 on 2026-07-31, because at 4 flight " +
                    "was cheap enough to become the default way to travel, then 15 → 10 while " +
                    "playing the ki attack stage, then 10 → 5: with BaseSpeed at 2 the early-game " +
                    "flight is slow enough to hold itself back without the ki cost doing it too.)",
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
            FlightHoverKiMultiplier = config.Bind(SecFlight, "HoverKiMultiplier", 0.5f,
                new ConfigDescription(
                    "Ki cost multiplier while hovering — airborne with no movement input at all, " +
                    "not even rising or descending. Holding position is cheaper than travelling, " +
                    "so stopping in the air to look around, aim or talk is not charged at the " +
                    "same rate as crossing the map. Set to 1 to charge the full cost regardless.",
                    new AcceptableValueRange<float>(0f, 1f), AdminOnly(94)));

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
            FlightKiSkillCurve = config.Bind(SecFlight, "KiSkillCurve", 2f,
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

            FlightXpPerSecond = config.Bind(SecFlight, "XpPerSecond", 0.5f,
                new ConfigDescription(
                    "Flight skill XP per second airborne. Flying is its own training — there is " +
                    "no other way to raise it. Valheim's own diminishing curve up to 100 applies. " +
                    "(Playtest value, 2026-08-11. Went 1 → 0.3 on 2026-07-31, because the skill is " +
                    "what makes flight cheap and reaching that quickly would undo the cost of " +
                    "KiPerSecond; 0.3 turned out to be the other extreme, with the skill barely " +
                    "moving over a whole session.)",
                    new AcceptableValueRange<float>(0f, 20f), AdminOnly(60)));

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

            FlightAutoLandOnGround = config.Bind(SecFlight, "AutoLandOnGround", true,
                new ConfigDescription(
                    "Touching the ground ends the flight by itself, so you do not walk around " +
                    "still burning ki. Only after you have actually left the ground once — " +
                    "otherwise taking off would land you on the same frame.",
                    null, AdminOnly(42)));

            FlightLevelBody = config.Bind(SecFlight, "LevelBody", true,
                new ConfigDescription(
                    "Keeps the body horizontal while flying, turning only left and right. The game " +
                    "aims flight rotation at the full movement direction, and the mod puts the " +
                    "climb/dive input into that same vector — so without this, going up flips you " +
                    "belly-up and going down flips you belly-down. The deliberate belly-down lean " +
                    "at speed is a separate, purely visual setting (FastPitch in 5.1) that does " +
                    "not tilt aim or collision.",
                    null, AdminOnly(41)));

            FlightForceIdlePose = config.Bind(SecFlight, "ForceIdlePose", true,
                new ConfigDescription(
                    "Forces the standing idle pose while flying, which is the Dragon Ball look. " +
                    "The vanilla player animator has no flight state, so without this you fly in " +
                    "the free-fall pose. Purely visual — turn it off if it breaks some animation. " +
                    "(Confirmed working in the 2026-07-31 playtest.)",
                    null, ClientSide(40)));

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
                    "as twice your max health. This is what makes a transformation show up on the " +
                    "defensive side — forms grant no health, they grant armor, and without this term " +
                    "base form and SSJ2 read within 10% of each other. Creatures have zero armor in " +
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

            RatingPlayerHitInterval = config.Bind(SecPower, "RatingPlayerHitInterval", 5f,
                new ConfigDescription(
                    "Seconds between two of YOUR hits, used to turn your damage into damage per " +
                    "second. Creatures carry their own cadence in the weapon item, so this key is " +
                    "only about you — the player's swing rate lives in the animation and cannot " +
                    "be read from the item. Lower makes your rating climb against everything. " +
                    "(Playtest value, 2026-09-06. Raised from 1: one punch per second is the " +
                    "animation's rate, not the fight's — between approach, block and recovery the " +
                    "real cadence is far slower, and at 1 the player outscanned everything.)",
                    new AcceptableValueRange<float>(0.1f, 10f), AdminOnly(44)));

            PowerDisplayScale = config.Bind(SecPower, "DisplayScale", 1f,
                new ConfigDescription(
                    "Linear multiplier on the displayed power rating, purely cosmetic. Linear is " +
                    "the point: it changes how big the number looks without touching any ratio " +
                    "between two characters. At 1 you read the raw rating (a boar around 55, a " +
                    "troll around 800, Fader around 25000); raise it if you want Dragon Ball sized " +
                    "numbers on screen. There used to be a square root here as well — it was " +
                    "removed because it flattened the very differences the number exists to show.",
                    new AcceptableValueRange<float>(0.01f, 10000f), AdminOnly(50)));

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

            // --- Efeitos ---
            // Nomes de prefab e de emote ficam aqui, e nao no codigo, porque qual pose e qual
            // efeito "le" como carregar ki e julgamento visual — e quem ve a tela e o Henrique.
            // Trocar deve custar editar este arquivo, nao uma recompilacao.
            // "roar" foi o emote escolhido no playtest: é o que lê como power up.
            ChargeEffectPrefab = config.Bind(SecEffects, "ChargeEffectPrefab", "fx_DvergerMage_Support_start",
                new ConfigDescription(
                    "Visual effect prefab attached to the player while charging. Empty disables it. " +
                    "Candidates include fx_ShieldCharge_1 through _5 (increasing intensity) " +
                    "and fx_chainlightning_spread.",
                    null, ClientSide(90)));

            ChargeSoundPrefab = config.Bind(SecEffects, "ChargeSoundPrefab", "sfx_charred_mage_attack_charge",
                new ConfigDescription(
                    "Sound prefab looped while charging. Empty disables it. " +
                    "Alternatives: sfx_StaffLightning_charge, sfx_staff_lightning_charge.",
                    null, ClientSide(80)));

            ChargeEffectColor = config.Bind(SecEffects, "ChargeEffectColor", "#4FC3F7",
                new ConfigDescription(
                    "Charging effect color, #RRGGBB format. Empty keeps the prefab's original " +
                    "color. Applies on the next charge — no restart needed. " +
                    "The original particle fade is preserved; only the base color changes. " +
                    "Ignored while you are transformed: charging in a form glows in that form's " +
                    "AuraColor instead, so the two read as one thing happening harder.",
                    null, ClientSide(85)));

            ChargeEffectScale = config.Bind(SecEffects, "ChargeEffectScale", 2f,
                new ConfigDescription(
                    "Scale of the visual effect. The Dverger support effect is born far too small " +
                    "at player scale and needs to be doubled. (Calibrated in the 2026-07-28 playtest.)",
                    new AcceptableValueRange<float>(0.1f, 5f), ClientSide(70)));

            ChargeEffectForceLoop = config.Bind(SecEffects, "ChargeEffectForceLoop", true,
                new ConfigDescription(
                    "Forces the effect's particles and audio to repeat. Game prefabs are built for " +
                    "a quick beam; without this the effect disappears on its own after a second. " +
                    "Turn it off if some prefab looks wrong when repeating.",
                    null, ClientSide(60)));

            // --- A pose de carregamento ---
            //
            // Ordem decrescente para o ConfigurationManager mostrar de cima para baixo na sequencia
            // em que se calibra: primeiro o interruptor, depois o tronco, os bracos, as pernas e por
            // ultimo a vida.

            ChargePoseEnabled = config.Bind(SecChargePose, "Enabled", true,
                new ConfigDescription(
                    "Procedurally poses the body while charging ki, muscle by muscle, on top of " +
                    "whatever the game's animation is doing. Off means charging has no animation " +
                    "at all — the emote it replaced was removed on 2026-08-07.",
                    null, ClientSide(200)));

            ChargePoseBlendSeconds = config.Bind(SecChargePose, "BlendSeconds", 0.25f,
                new ConfigDescription(
                    "Seconds for the pose to come in when you start charging and to leave when you " +
                    "stop. Zero snaps.",
                    new AcceptableValueRange<float>(0f, 2f), ClientSide(199)));

            // Os sete interruptores de grupo. Ficam no topo da secao, logo abaixo do Enabled,
            // porque sao por onde a calibragem comeca: acender um grupo, ajustar os alvos dele,
            // acender o proximo.
            //
            // Depois do playtest de 2026-08-07 cinco deles nascem LIGADOS — peito, peito alto,
            // ombros, cabeca e bracos. Os dois que continuam em zero sao os dois que o rig do
            // Valheim desaconselha: a **lombar**, que arrasta o quadril, e as **pernas**, que sem
            // o agachamento do HipDrop levantam os pes do chao. Nao e' "falta calibrar": e' a
            // pose calibrada.
            ChargePoseChestWeight = config.Bind(SecChargePose, "ChestWeight", 1f,
                new ConfigDescription(
                    "How much of the CHEST the pose owns. THIS IS A SWITCH, not an intensity: zero " +
                    "is not 'neutral chest', it is 'do not touch the chest, leave the animation " +
                    "alone'. That distinction is why every group here has a weight — a target of " +
                    "zero would force a muscle to its neutral value, which is itself a pose. " +
                    "Same for every other weight below. " +
                    "The chest is the joint that bends the torso WITHOUT taking the hips with it, " +
                    "which is why it is the one that starts on.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(198)));

            ChargePoseUpperChestWeight = config.Bind(SecChargePose, "UpperChestWeight", 1f,
                new ConfigDescription(
                    "How much of the UPPER chest the pose owns — the joint above the chest, the " +
                    "most isolated of the three. Use it on top of the chest for a deeper bend that " +
                    "still does not reach the hips, or alone for the subtlest possible lean.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(197)));

            ChargePoseSpineWeight = config.Bind(SecChargePose, "SpineWeight", 0f,
                new ConfigDescription(
                    "How much of the LOWER BACK the pose owns. Off by default and it should " +
                    "probably stay that way: in the Valheim rig this joint drags the hips along " +
                    "with it, so bending it moves the legs too. It is what made the first version " +
                    "of this pose look wrong on 2026-08-07, and it is the same joint the flight " +
                    "pose keeps small for the same reason. Turn it on only if the lean needs to " +
                    "come from the waist.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(196)));

            ChargePoseShoulderWeight = config.Bind(SecChargePose, "ShoulderWeight", 1f,
                new ConfigDescription(
                    "How much of the shoulders the pose owns. Zero leaves them to the animation.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(195)));

            ChargePoseHeadWeight = config.Bind(SecChargePose, "HeadWeight", 1f,
                new ConfigDescription(
                    "How much of the neck and head the pose owns. Zero leaves them to the " +
                    "animation — including the game's own look-at, which aims the head at whatever " +
                    "you are looking at. Keeping the look-at was the original guess; the " +
                    "calibrated pose takes the head instead, because a dropped chin is half of " +
                    "what reads as bracing. " +
                    "(Playtest value, 2026-08-07.)",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(194)));

            ChargePoseArmWeight = config.Bind(SecChargePose, "ArmWeight", 1f,
                new ConfigDescription(
                    "How much of both arms the pose owns — upper arm, twist and elbow. Zero leaves " +
                    "them exactly as the animation has them. FistClench is deliberately outside " +
                    "this group: a closed hand still reads as tension on an otherwise normal arm.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(193)));

            ChargePoseLegWeight = config.Bind(SecChargePose, "LegWeight", 0f,
                new ConfigDescription(
                    "How much of both legs the pose owns — stance width, knees and the hip drop " +
                    "that goes with them. Zero leaves the legs to the animation, which also makes " +
                    "the feet-off-the-ground problem described under KneeBend impossible.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(192)));

            ChargePoseChestLean = config.Bind(SecChargePose, "ChestLean", 0.5f,
                new ConfigDescription(
                    "How far the CHEST bends FORWARD, when ChestWeight is on. Negative arches back. " +
                    "This and the two below used to be a single 'Lean' split between the joints by " +
                    "a fixed ratio in code — which meant the lower back always came along, hips " +
                    "and all. Reported as bad on screen on 2026-08-07 and split into three.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(189)));

            ChargePoseUpperChestLean = config.Bind(SecChargePose, "UpperChestLean", 0.5f,
                new ConfigDescription(
                    "How far the UPPER chest bends forward, when UpperChestWeight is on. " +
                    "Negative arches back.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(188)));

            ChargePoseSpineLean = config.Bind(SecChargePose, "SpineLean", 0f,
                new ConfigDescription(
                    "How far the LOWER BACK bends forward, when SpineWeight is on. Keep it small: " +
                    "this joint carries the hips. Negative arches back.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(187)));

            // ⚠️ NEGATIVO depois do playtest de 2026-08-07: a pose calibrada puxa os ombros para
            // BAIXO, nao para cima. O desenho original apostava no contrario — ombro erguido como
            // sinal de tensao —, e a tela decidiu o oposto.
            ChargePoseShoulderShrug = config.Bind(SecChargePose, "ShoulderShrug", -0.5f,
                new ConfigDescription(
                    "How far the shoulders are pulled UP — and the default is NEGATIVE, meaning " +
                    "down. Pulling them up was the original guess (shrugged reads as tense) and " +
                    "the screen went the other way: shoulders driven down and back read as " +
                    "bracing, which is what charging is. " +
                    "(Playtest value, 2026-08-07.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(185)));

            ChargePoseHeadTilt = config.Bind(SecChargePose, "HeadTilt", 0.5f,
                new ConfigDescription(
                    "How far the chin drops. Split between neck and head. Negative looks up " +
                    "instead, which is the other classic reading of the same pose — worth trying " +
                    "if the effect column ever goes up into the sky. " +
                    "(Playtest value, 2026-08-07. Started at 0.15, which barely moved the head.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(180)));

            // Alvo ABSOLUTO no espaco de musculo, e nao intencao: e o unico numero desta secao que
            // segue a convencao da Unity direto, porque nao ha nome de intencao honesto para "onde
            // fica o braco" — e a mesma escolha que o HoverArmSpread do voo fez.
            ChargePoseArmDown = config.Bind(SecChargePose, "ArmDown", -0.2f,
                new ConfigDescription(
                    "Where the upper arms sit. This one is raw muscle space, not intent: 0 is a " +
                    "T-pose and about -0.65 is arms hanging straight down, so the calibrated " +
                    "-0.2 keeps the elbows well away from the ribs. " +
                    "(Playtest value, 2026-08-07. The first guess was -0.5, aiming for fists " +
                    "beside the hips; the screen wanted the arms further out than that.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(175)));

            // ⚠️ NEGATIVO depois do playtest de 2026-08-07: os bracos vao para FRENTE das
            // costelas, nao para tras. Junto com o ArmDown -0,2 e o cotovelo mais aberto, a pose
            // calibrada e' de bracos abertos e a frente, e nao de punhos colados no quadril.
            ChargePoseArmBack = config.Bind(SecChargePose, "ArmBack", -0.25f,
                new ConfigDescription(
                    "How far the upper arms are pulled BACK, behind the ribs — and the default is " +
                    "NEGATIVE, meaning forward. Together with ArmDown and ElbowBend this is what " +
                    "decides where the hands end up. The original guess pulled them back, to put " +
                    "the fists at the hips; the calibrated pose pushes them forward instead. " +
                    "(Playtest value, 2026-08-07.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(170)));

            ChargePoseArmTwist = config.Bind(SecChargePose, "ArmTwist", 0.15f,
                new ConfigDescription(
                    "Rotation of the upper arm along its own length. It does nothing by itself and " +
                    "everything in combination: it decides WHERE the bent elbow points the " +
                    "forearm. Change this first if the forearms cross the body or stick out " +
                    "sideways instead of coming forward.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(165)));

            ChargePoseElbowBend = config.Bind(SecChargePose, "ElbowBend", 0.3f,
                new ConfigDescription(
                    "How far the elbows are bent — and the name is backwards, kept only because " +
                    "renaming it would throw away a calibrated .cfg key. The muscle is 'Forearm " +
                    "Stretch': 1 is a STRAIGHT arm, -1 the tightest bend, 0 the middle. So this " +
                    "pose's 0.3 is a slightly straightened arm, not a slightly bent one. Found " +
                    "out on 2026-08-21 calibrating the firing pose, which had the same lie in its " +
                    "description. " +
                    "(Playtest value, 2026-08-07. Started at 0.7 — closer to straight, which " +
                    "belonged to the fists-at-the-hips version of this pose.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(160)));

            ChargePoseFistClench = config.Bind(SecChargePose, "FistClench", 1f,
                new ConfigDescription(
                    "How hard the hands close, independently of ArmWeight — a closed fist reads as " +
                    "tension even on an arm the pose is not touching. Fingers are muscles like any " +
                    "other, so this costs nothing but the names. Zero leaves the hands as the " +
                    "animation had them. Does nothing if the player rig has no mapped finger " +
                    "bones — no error, just no fist.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(155)));

            ChargePoseStanceWidth = config.Bind(SecChargePose, "StanceWidth", 0.3f,
                new ConfigDescription(
                    "How far apart the feet are planted. Both legs get the same value, which in " +
                    "Unity muscle space should spread them symmetrically — if they scissor instead " +
                    "of spreading, one of the two signs needs flipping in code, so report it.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(150)));

            ChargePoseKneeBend = config.Bind(SecChargePose, "KneeBend", 0.25f,
                new ConfigDescription(
                    "How deep the half squat is. READ HipDrop BEFORE RAISING THIS: bending the " +
                    "knees shortens the leg, and if the hips do not come down by the same amount " +
                    "the feet leave the ground. The game's foot IK will NOT save it — it runs " +
                    "before the mod writes the pose.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(145)));

            ChargePoseHipDrop = config.Bind(SecChargePose, "HipDrop", 0.16f,
                new ConfigDescription(
                    "How far the hips come down, per unit of KneeBend, so the squat is a squat and " +
                    "not a character floating with folded legs. In avatar units, which for a " +
                    "player-sized rig is roughly meters — the default is about 4 cm at the default " +
                    "KneeBend. Too low and the feet hover; too high and the legs sink into the " +
                    "ground. Zero disables it, which is the safe fallback if it looks wrong.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(140)));

            // As duas senoides que tiram a pose do estado de boneco de vitrine. A lenta e o esforco,
            // a rapida e a tensao — ver o comentario no KiChargePose.Apply.
            ChargePoseStrain = config.Bind(SecChargePose, "Strain", 0.07f,
                new ConfigDescription(
                    "Amplitude of the slow heave: the whole body sinking and rising, like someone " +
                    "straining. This is the one that makes the pose look alive. Zero freezes it.",
                    new AcceptableValueRange<float>(0f, 0.5f), ClientSide(135)));

            ChargePoseStrainSpeed = config.Bind(SecChargePose, "StrainSpeed", 2.2f,
                new ConfigDescription(
                    "Speed of the slow heave, in radians per second. Around 2 is roughly one cycle " +
                    "every three seconds — heavy breathing. Much faster stops reading as effort.",
                    new AcceptableValueRange<float>(0.1f, 20f), ClientSide(130)));

            ChargePoseTremor = config.Bind(SecChargePose, "Tremor", 0f,
                new ConfigDescription(
                    "Amplitude of the fast tremble, ON TOP of whichever muscle groups are turned " +
                    "on above — it has nothing to shake in a group whose weight is zero. Off by " +
                    "default: start from the still pose and dial this up until it reads as effort. " +
                    "Deliberately tiny when on, around 0.02: this should be felt, not seen. If you " +
                    "can tell it is a sine wave, it is too high.",
                    new AcceptableValueRange<float>(0f, 0.3f), ClientSide(125)));

            ChargePoseTremorSpeed = config.Bind(SecChargePose, "TremorSpeed", 17f,
                new ConfigDescription(
                    "Speed of the fast tremble, in radians per second. The two sides run at " +
                    "slightly different rates on purpose — in sync it reads as machine vibration, " +
                    "out of phase it reads as muscle.",
                    new AcceptableValueRange<float>(1f, 60f), ClientSide(120)));

            // --- A pose de disparo ---
            //
            // Mesma ordem decrescente da secao acima: o interruptor, o envelope, e depois os
            // grupos de cima para baixo.
            //
            // Os defaults sao os do playtest de 2026-08-21, e a ordem "um grupo de cada vez" que
            // este comentario descrevia era o **caminho** ate eles, nao o destino: braco, ombro e
            // tronco subiram os tres, porque estender o braco sem o ombro para no encaixe do umero
            // e sem o tronco lê como o boneco apontando em vez de empurrar. So a lombar ficou fora.
            //
            // ⚠️ Calibrar isto SEM o `saiya_blast pose` e' impossivel: a pose dura menos que o
            // tempo de arrastar um slider e olhar o personagem.

            BlastPoseEnabled = config.Bind(SecBlastPose, "Enabled", true,
                new ConfigDescription(
                    "Procedurally throws the right arm forward when a ki blast leaves your hand, " +
                    "on top of whatever the game's animation is doing. Off means firing has no " +
                    "animation at all, which is how it shipped on 2026-08-06.",
                    null, ClientSide(200)));

            BlastPoseRiseSeconds = config.Bind(SecBlastPose, "RiseSeconds", 0.06f,
                new ConfigDescription(
                    "Seconds for the arm to snap out. Keep it SHORT and shorter than FallSeconds: " +
                    "a slow rise turns a thrust into a stretch. Zero snaps instantly, which is " +
                    "also fine here.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(199)));

            BlastPoseHoldSeconds = config.Bind(SecBlastPose, "HoldSeconds", 0.7f,
                new ConfigDescription(
                    "Seconds the arm stays out at full extension before relaxing. Firing again " +
                    "before it ends just pushes this deadline forward — the arm does not drop and " +
                    "snap back between shots of a beam. Playtest landed on 0.7, four times the " +
                    "0.18 this was designed with: the shot is still in the air at 0.18, and an " +
                    "arm already on its way down while the ball flies reads as a flinch.",
                    new AcceptableValueRange<float>(0f, 3f), ClientSide(198)));

            BlastPoseFallSeconds = config.Bind(SecBlastPose, "FallSeconds", 0.3f,
                new ConfigDescription(
                    "Seconds for the arm to come back. LONGER than RiseSeconds on purpose: the " +
                    "gesture is a snap out and a relax back, and a fast return reads as the " +
                    "animation being cut rather than ending.",
                    new AcceptableValueRange<float>(0f, 3f), ClientSide(197)));

            BlastPoseArmWeight = config.Bind(SecBlastPose, "ArmWeight", 1f,
                new ConfigDescription(
                    "How much of the RIGHT ARM the pose owns — upper arm, twist and elbow. THIS " +
                    "IS A SWITCH, not an intensity: zero is not 'neutral arm', it is 'do not " +
                    "touch the arm, leave the animation alone'. A target of zero would instead " +
                    "force the muscle to its neutral value, which is itself a pose (a T-pose, in " +
                    "this case). Same for every other weight below. Playtest ended with this, " +
                    "the shoulder and the torso all on — the arm alone was the request, but not " +
                    "the gesture.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(196)));

            BlastPoseShoulderWeight = config.Bind(SecBlastPose, "ShoulderWeight", 1f,
                new ConfigDescription(
                    "How much of the right SHOULDER the pose owns. This is what turns 'arm raised' " +
                    "into 'arm extended' — without it the reach stops at the shoulder socket and " +
                    "the character looks like he is pointing rather than pushing. Full on since " +
                    "the 2026-08-21 playtest, which is where that sentence stopped being a guess.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(195)));

            BlastPoseTorsoWeight = config.Bind(SecBlastPose, "TorsoWeight", 1f,
                new ConfigDescription(
                    "How much of the TORSO TWIST the pose owns — chest and upper chest bringing " +
                    "the right shoulder around to follow the arm. On since the 2026-08-21 " +
                    "playtest. Zero leaves the torso to the animation, including whatever twist " +
                    "the game's aiming does.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(194)));

            BlastPoseSpineTwistWeight = config.Bind(SecBlastPose, "SpineTwistWeight", 0f,
                new ConfigDescription(
                    "How much of the twist reaches the LOWER BACK, as a fraction of TorsoWeight. " +
                    "Off by default and it should probably stay that way: in the Valheim rig this " +
                    "joint drags the hips along, so twisting it turns the whole character away " +
                    "from where he is aiming. It is the same joint the charging pose and the " +
                    "flight pose both keep out for the same reason.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(193)));

            BlastPoseArmForward = config.Bind(SecBlastPose, "ArmForward", 0.6f,
                new ConfigDescription(
                    "How far the upper arm swings FORWARD. This is the gesture: at 0 the arm is " +
                    "out to the side, at 1 it points straight ahead. Negative pulls it behind the " +
                    "ribs, which is the charging pose, not this one. Playtest settled at 0.6 and " +
                    "not the 0.9 this started with, because the shoulder and the torso now carry " +
                    "part of the reach — the joints add up, and 0.9 on top of them overshot.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(190)));

            BlastPoseArmHeight = config.Bind(SecBlastPose, "ArmHeight", 0.3f,
                new ConfigDescription(
                    "Where the upper arm sits vertically. Raw muscle space, not intent: 0 is a " +
                    "T-pose, meaning the arm is horizontal — shoulder height, and the obvious " +
                    "place to aim straight from. About -0.65 is the arm hanging down. Playtest " +
                    "ended at 0.3, slightly ABOVE the shoulder: the ball is thrown from over the " +
                    "shoulder, not levelled from it like a gun.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(189)));

            BlastPoseAimFollowPitch = config.Bind(SecBlastPose, "AimFollowPitch", 1f,
                new ConfigDescription(
                    "How much the arm follows where you are LOOKING UP OR DOWN, added on top of " +
                    "ArmHeight. This matters more than it sounds: the projectile spawns at the " +
                    "right hand and flies along your look direction, so with a locked arm, aiming " +
                    "at the sky sends the shot upward out of a hand that is pointing at the " +
                    "horizon. 1 is full follow, 0 pins the arm to ArmHeight. Playtest went " +
                    "straight to 1: half a follow is the arm neither locked nor aimed. Renamed " +
                    "from 'AimFollow' on 2026-08-21, when AimFollowYaw joined it — a leftover " +
                    "'AimFollow' line in an old .cfg does nothing and can be deleted.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(188)));

            BlastPoseAimFollowYaw = config.Bind(SecBlastPose, "AimFollowYaw", 1f,
                new ConfigDescription(
                    "How much the arm follows where you are LOOKING LEFT OR RIGHT, subtracted " +
                    "from ArmForward. The other half of AimFollowPitch, and the one that is only " +
                    "visible some of the time: the body usually turns to the camera on its own, " +
                    "and while it does, the arm is already aimed and this does nothing. It earns " +
                    "its keep exactly when the two come apart — strafing, running one way while " +
                    "looking another — which is also when the shot leaves a hand pointing " +
                    "somewhere else. 1 means the arm reaches the same yaw as the camera, 0 pins " +
                    "it to ArmForward.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(187)));

            BlastPoseAimYawTorsoShare = config.Bind(SecBlastPose, "AimYawTorsoShare", 0f,
                new ConfigDescription(
                    "How much of that horizontal aim the TORSO takes instead of the shoulder " +
                    "joint, from 0 (all arm) to 1 (all torso). A big sideways angle done entirely " +
                    "at the shoulder ends with the arm crossing the chest; handing part of it to " +
                    "the spine turns the character into the shot instead. Needs TorsoWeight above " +
                    "zero to do anything at all — with the torso group off, this only takes " +
                    "rotation away from the arm and nothing gives it back. The muscle scales are " +
                    "not the same, so this is a feel knob, not a split of degrees.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(186)));

            BlastPoseArmTwist = config.Bind(SecBlastPose, "ArmTwist", 0f,
                new ConfigDescription(
                    "Rotation of the upper arm along its own length. Raw muscle space. With the " +
                    "elbow straight it barely changes the silhouette, but it decides which way the " +
                    "PALM faces — and the ki ball is born in that hand. Change this if the back of " +
                    "the hand ends up facing the target.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(185)));

            BlastPoseElbowStretch = config.Bind(SecBlastPose, "ElbowStretch", 1f,
                new ConfigDescription(
                    "How STRAIGHT the elbow is. Raw muscle space, and it runs the way the muscle " +
                    "is named ('Forearm Stretch'): 1 is the straightest the rig goes, -1 the " +
                    "tightest bend, and 0 is the middle — a visibly bent arm, not a straight one. " +
                    "Playtest sits at 1, because a thrust with a bend in it reads as a shove. " +
                    "Was called 'ElbowBend' until 2026-08-21, with the sign backwards in its own " +
                    "description; that name is why a straight arm looked impossible to get.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(184)));

            BlastPoseShoulderPush = config.Bind(SecBlastPose, "ShoulderPush", 0.5f,
                new ConfigDescription(
                    "How far the right shoulder is pushed FORWARD, when ShoulderWeight is on. This " +
                    "is the extra reach. Negative pulls it back.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(183)));

            BlastPoseShoulderLift = config.Bind(SecBlastPose, "ShoulderLift", 0.1f,
                new ConfigDescription(
                    "How far the right shoulder rides UP, when ShoulderWeight is on. Raw muscle " +
                    "space. Small: a shrugged shoulder on an extended arm reads as flinching.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(182)));

            BlastPoseTorsoTwist = config.Bind(SecBlastPose, "TorsoTwist", 0.3f,
                new ConfigDescription(
                    "How far the torso rotates to bring the right shoulder AROUND, when " +
                    "TorsoWeight is on. Split across the three spine joints, most at the top and " +
                    "least at the bottom. Keep it small — the point is the torso following the " +
                    "arm, not the character turning sideways to his own aim. Negative twists the " +
                    "other way, which would pull the firing shoulder back.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(181)));

            BlastPoseHandOpen = config.Bind(SecBlastPose, "HandOpen", 1f,
                new ConfigDescription(
                    "How far the right hand opens into a flat palm, independently of ArmWeight — " +
                    "the exact opposite of the charging pose's FistClench, and the contrast is the " +
                    "point: charging is a closed fist, firing is an open palm. Full on since the " +
                    "2026-08-21 playtest. Zero leaves the hand as the animation had it. Does " +
                    "nothing if the player rig has no mapped finger bones — no error, just no palm.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(180)));

            BlastPoseWristBend = config.Bind(SecBlastPose, "WristBend", 1f,
                new ConfigDescription(
                    "How far the wrist bends back, so the palm faces where the shot is going. Raw " +
                    "muscle space, and it rides on HandOpen rather than having a weight of its " +
                    "own: a pushed palm and an open hand are one gesture. Playtest took it to the " +
                    "limit, 1 — the wrist is what aims the palm, and anything less pointed the " +
                    "palm at the ground while the shot went forward. Negative curls it the other " +
                    "way.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(179)));

            // --- A pose do Kamehameha ---
            //
            // Mesma ordem das outras duas: o interruptor, o envelope, os pesos de grupo, e depois
            // os alvos. A diferenca e' que os alvos vem em PARES — Charge e Release —, porque este
            // e' o primeiro gesto com duas fases.
            //
            // ⚠️ Calibrar isto sem o `saiya_blast pose charge` e o `saiya_blast pose release` e'
            // impossivel: a carga dura dois segundos e o empurrao menos de um.
            //
            // Os alvos da CONCHA sao playtest de 2026-09-07. O envelope, os pesos de grupo e tudo
            // que e' do EMPURRAO continuam sendo chute — a calibragem parou na primeira metade do
            // gesto.

            BeamPoseEnabled = config.Bind(SecBeamPose, "Enabled", true,
                new ConfigDescription(
                    "Procedurally cups both hands at your hip while a ki attack charges, then " +
                    "pushes them forward when the beam fires. Off falls back to the one-armed ki " +
                    "blast pose, which is how the Kamehameha shipped on 2026-09-07.",
                    null, ClientSide(270)));

            BeamPoseRiseSeconds = config.Bind(SecBeamPose, "RiseSeconds", 0.25f,
                new ConfigDescription(
                    "Seconds for the body to settle into the cupped stance when the charge starts. " +
                    "This is a stance and not a strike, so it should NOT snap — compare with the " +
                    "ki blast's RiseSeconds, which is four times faster because a thrust that " +
                    "eases in reads as a stretch.",
                    new AcceptableValueRange<float>(0f, 2f), ClientSide(269)));

            BeamPoseReleaseRiseSeconds = config.Bind(SecBeamPose, "ReleaseRiseSeconds", 0.08f,
                new ConfigDescription(
                    "Seconds to go from the cupped hands to the two-handed push. THIS IS THE SEAM " +
                    "between the two halves of the gesture, not a second entrance: the pose keeps " +
                    "the body the whole time and only the targets move. Keep it SHORT — the beam " +
                    "is already leaving the hands while this runs.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(268)));

            BeamPoseHoldSeconds = config.Bind(SecBeamPose, "HoldSeconds", 0.7f,
                new ConfigDescription(
                    "Seconds the push stays out after the LAST projectile of the beam. Each " +
                    "projectile pushes this deadline forward, so the arms stay out from the first " +
                    "to the last without the pose knowing how long the beam is. Same 0.7 the ki " +
                    "blast landed on: the shot is still in the air below that, and arms already " +
                    "coming down while it flies read as a flinch.",
                    new AcceptableValueRange<float>(0f, 3f), ClientSide(267)));

            BeamPoseFallSeconds = config.Bind(SecBeamPose, "FallSeconds", 0.35f,
                new ConfigDescription(
                    "Seconds for the body to come back, after the beam ends OR after a charge is " +
                    "cancelled without firing. Longer than the rise on purpose: the gesture ends " +
                    "by relaxing, and a fast return reads as the animation being cut.",
                    new AcceptableValueRange<float>(0f, 3f), ClientSide(266)));

            BeamPoseArmWeight = config.Bind(SecBeamPose, "ArmWeight", 1f,
                new ConfigDescription(
                    "How much of BOTH ARMS the pose owns — upper arm, twist and elbow. THIS IS A " +
                    "SWITCH, not an intensity: zero is not 'neutral arms', it is 'do not touch " +
                    "the arms, leave the animation alone'. A target of zero would instead force " +
                    "the muscle to its neutral value, which is itself a pose (a T-pose, here). " +
                    "Same for every other weight below.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(265)));

            BeamPoseForearmWeight = config.Bind(SecBeamPose, "ForearmWeight", 1f,
                new ConfigDescription(
                    "How much of both FOREARMS the pose owns — the twist below the elbow, and " +
                    "nothing else. Its own group and not part of ArmWeight, because it is the " +
                    "only target that turns the PALM without moving where the arm is: with the " +
                    "elbow folded, twisting the upper arm swings the whole forearm somewhere " +
                    "else, while this rotates the hand in place.\n" +
                    "⚠️ ZERO IS THE FALLBACK, not a neutral: a target of 0 in the keys below is " +
                    "the forearm at the rig's neutral, which is itself a choice and not " +
                    "necessarily what was on screen. Set this to 0 to get back exactly what the " +
                    "pose did before the group existed.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(264)));

            BeamPoseShoulderWeight = config.Bind(SecBeamPose, "ShoulderWeight", 1f,
                new ConfigDescription(
                    "How much of both SHOULDERS the pose owns. This is what turns 'arms raised' " +
                    "into 'arms extended' — without it the reach stops at the shoulder socket and " +
                    "the character points instead of pushing. It was full on in the ki blast " +
                    "after playtest, for exactly that reason.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(263)));

            BeamPoseTorsoWeight = config.Bind(SecBeamPose, "TorsoWeight", 1f,
                new ConfigDescription(
                    "How much of the TORSO the pose owns — chest and upper chest, both the twist " +
                    "and the lean. The twist is half of the charging gesture: it is what takes " +
                    "the cupping shoulder back and brings the other one across. With this at " +
                    "zero the two hands meet at the hip with the chest square, which is a " +
                    "position the body does not make.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(262)));

            BeamPoseSpineWeight = config.Bind(SecBeamPose, "SpineWeight", 0f,
                new ConfigDescription(
                    "How much of the torso work reaches the LOWER BACK, as a fraction of " +
                    "TorsoWeight. Covers both the twist and the lean. Off by default and it " +
                    "should probably stay that way: in the Valheim rig this joint drags the hips " +
                    "along, so using it turns the whole character away from where he is aiming. " +
                    "The other three poses all keep it out for the same reason.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(261)));

            BeamPoseLegWeight = config.Bind(SecBeamPose, "LegWeight", 0f,
                new ConfigDescription(
                    "How much of the LEGS the pose owns — stance width, knees and the hip drop. " +
                    "OFF since the 2026-09-07 playtest, which built the whole gesture with the " +
                    "legs left to the animation. " +
                    "Unlike the other groups this one steps aside on its own while you walk, run " +
                    "or fly: a squat written over the running animation fights it, and in the air " +
                    "it fights the flight pose. Zero leaves the legs to the animation always.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(260)));

            BeamPoseHandWeight = config.Bind(SecBeamPose, "HandWeight", 1f,
                new ConfigDescription(
                    "How much of both HANDS the pose owns — fingers and wrists. Independent of " +
                    "ArmWeight: cupped hands read as holding something even on arms the pose is " +
                    "not touching. Does nothing if the player rig has no mapped finger bones — no " +
                    "error, just no hands.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(259)));

            // ---------- Os alvos, em pares ----------
            //
            // Cup e' o braco do lado onde a bola nasce, Cross e' o que atravessa o corpo. Com o
            // ChargeEffectAnchor de fabrica, Cup e' o DIREITO na tela.
            //
            // 📌 **Os alvos da CONCHA sao valores de playtest, de 2026-09-07**, e o par se pagou
            // no primeiro dia: dos sete alvos laterais, SEIS terminaram com numeros diferentes nos
            // dois lados, e tres deles em pontas opostas da faixa. Estes defaults nao sao ponto de
            // partida — sao a pose que ficou na tela.
            //
            // Os do EMPURRAO continuam sendo chute: a calibragem parou na concha.

            BeamPoseChargeCupArmHeight = config.Bind(SecBeamPose, "ChargeCupArmHeight", -0.3f,
                new ConfigDescription(
                    "Where the CUPPING upper arm sits while charging — the one on the same side " +
                    "as the ball. Raw muscle space, not intent: 0 is a T-pose (arm horizontal) " +
                    "and about -0.65 is the arm hanging straight down. The hand is at the hip, so " +
                    "the arm is nearly down and it is the elbow that brings it in front." +
                    " (Playtest value, 2026-09-08.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(258)));

            BeamPoseChargeCrossArmHeight = config.Bind(SecBeamPose, "ChargeCrossArmHeight", -0.35f,
                new ConfigDescription(
                    "Where the CROSSING upper arm sits while charging — the one reaching across " +
                    "the body. Same raw muscle space as the one above. Raising this one alone " +
                    "lifts that hand over the other instead of beside it, which is what the two " +
                    "hands cupping a sphere actually do." +
                    " (Playtest value, 2026-09-08.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(257)));

            BeamPoseChargeCupArmForward = config.Bind(SecBeamPose, "ChargeCupArmForward", 0f,
                new ConfigDescription(
                    "How far FORWARD the CUPPING arm swings while charging. Negative pulls it " +
                    "behind the ribs, which is where it belongs: this arm barely moves, it is the " +
                    "other one that travels. Which side is which comes from the attack's " +
                    "ChargeEffectAnchor, so the hands always meet where the ball is born." +
                    " (Playtest value, 2026-09-08.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(256)));

            BeamPoseChargeCrossArmForward = config.Bind(SecBeamPose, "ChargeCrossArmForward", 0.65f,
                new ConfigDescription(
                    "How far FORWARD the CROSSING arm swings while charging. Bigger than the " +
                    "cupping arm by definition; this and ChargeCrossElbowBend are the two knobs " +
                    "that decide whether the two hands actually meet." +
                    " (Playtest value, 2026-09-08.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(255)));

            BeamPoseChargeCupArmTwist = config.Bind(SecBeamPose, "ChargeCupArmTwist", -0.2f,
                new ConfigDescription(
                    "Rotation of the CUPPING upper arm along its own length, while charging. Raw " +
                    "muscle space, and the two sides do NOT mirror: 'Twist In-Out' is already " +
                    "named relative to the body, so the same number means 'inward' on both arms. " +
                    "It does nothing by itself and everything in combination — it decides where " +
                    "the bent elbow points the forearm. Change this first if the hand ends up in " +
                    "front of the belly instead of beside the hip.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(254)));

            BeamPoseChargeCrossArmTwist = config.Bind(SecBeamPose, "ChargeCrossArmTwist", -0.7f,
                new ConfigDescription(
                    "Rotation of the CROSSING upper arm along its own length, while charging. " +
                    "Same raw muscle space as the one above. This is the knob that decides " +
                    "whether that forearm arrives at the hip pointing along the body or jabbing " +
                    "into it." +
                    " (Playtest value, 2026-09-08.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(253)));

            BeamPoseChargeCupForearmTwist = config.Bind(SecBeamPose, "ChargeCupForearmTwist", 1f,
                new ConfigDescription(
                    "Rotation of the CUPPING FOREARM along its own length, while charging — the " +
                    "hand turning in place around the ball. Raw muscle space. This is the joint " +
                    "BELOW the elbow: ChargeCupArmTwist swings the whole folded forearm somewhere " +
                    "else, this only rolls the palm. Use it when the arm is where you want it and " +
                    "the palm is facing the wrong way. Needs ForearmWeight above zero.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(252)));

            BeamPoseChargeCrossForearmTwist = config.Bind(SecBeamPose, "ChargeCrossForearmTwist", 1f,
                new ConfigDescription(
                    "Rotation of the CROSSING FOREARM along its own length, while charging. Same " +
                    "raw muscle space as the one above, and the same number is not a mirror: " +
                    "'Twist In-Out' is named relative to the body, so it means inward on both " +
                    "sides. The two palms face each other around the ball, so this one rarely " +
                    "wants the cupping arm's value.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(251)));

            BeamPoseChargeCupElbowBend = config.Bind(SecBeamPose, "ChargeCupElbowBend", -0.3f,
                new ConfigDescription(
                    "How bent the CUPPING elbow is while charging. Raw muscle space, and it runs " +
                    "the way the muscle is named ('Forearm Stretch'): 1 is the straightest the " +
                    "rig goes, -1 the tightest bend, and 0 is the MIDDLE — a visibly bent arm, " +
                    "not a straight one. Negative here, because cupped hands are a folded arm." +
                    " (Playtest value, 2026-09-08.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(250)));

            BeamPoseChargeCrossElbowBend = config.Bind(SecBeamPose, "ChargeCrossElbowBend", 0.4f,
                new ConfigDescription(
                    "How bent the CROSSING elbow is while charging. Same raw muscle space as the " +
                    "one above and tighter to start with — reaching across the belly needs more " +
                    "fold, not less. Raise it toward 1 if that hand ends up short of the other." +
                    " (Playtest value, 2026-09-08.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(249)));

            BeamPoseChargeCupShoulderPush = config.Bind(SecBeamPose, "ChargeCupShoulderPush", -1f,
                new ConfigDescription(
                    "How far the CUPPING shoulder is pushed FORWARD while charging. Negative " +
                    "pulls it back, which is what the loading side of the body does. Needs " +
                    "ShoulderWeight above zero.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(248)));

            BeamPoseChargeCrossShoulderPush = config.Bind(SecBeamPose, "ChargeCrossShoulderPush", 0f,
                new ConfigDescription(
                    "How far the CROSSING shoulder is pushed FORWARD while charging. This is the " +
                    "one that usually wants to be positive: the shoulder comes around with the " +
                    "arm that travels. It is a separate knob from ChargeTorsoTwist on purpose — " +
                    "the twist turns the whole ribcage, this moves one joint.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(247)));

            BeamPoseChargeCupShoulderLift = config.Bind(SecBeamPose, "ChargeCupShoulderLift", 1f,
                new ConfigDescription(
                    "How far the CUPPING shoulder rides UP while charging. Raw muscle space. A " +
                    "little reads as effort; a lot reads as a shrug.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(246)));

            BeamPoseChargeCrossShoulderLift = config.Bind(SecBeamPose, "ChargeCrossShoulderLift", 0f,
                new ConfigDescription(
                    "How far the CROSSING shoulder rides UP while charging. Raw muscle space, " +
                    "same as the one above.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(245)));

            BeamPoseChargeCupWristBend = config.Bind(SecBeamPose, "ChargeCupWristBend", 1f,
                new ConfigDescription(
                    "How far the CUPPING wrist bends back while charging — the palm rising and " +
                    "falling — so it faces the ball instead of the ground. Raw muscle space. " +
                    "Negative curls it the other way. It answers to HandWeight and NOTHING else: " +
                    "until 2026-09-07 it also rode on ChargeHandCup, so turning the fingers off " +
                    "silently turned the wrists off with them.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(244)));

            BeamPoseChargeCrossWristBend = config.Bind(SecBeamPose, "ChargeCrossWristBend", 0.6f,
                new ConfigDescription(
                    "How far the CROSSING wrist bends back while charging. The two hands face " +
                    "each other around the ball, so this one rarely wants the same number as the " +
                    "cupping wrist." +
                    " (Playtest value, 2026-09-08.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(243)));

            BeamPoseChargeCupWristSide = config.Bind(SecBeamPose, "ChargeCupWristSide", 0f,
                new ConfigDescription(
                    "The OTHER axis of the CUPPING wrist while charging: the hand deviating " +
                    "sideways, toward the thumb or toward the little finger — the waving " +
                    "movement. Raw muscle space. WristBend alone cannot point a palm anywhere in " +
                    "space; it takes both axes, and this is the one that closes the last gap when " +
                    "the hand is nearly right.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(242)));

            BeamPoseChargeCrossWristSide = config.Bind(SecBeamPose, "ChargeCrossWristSide", 0f,
                new ConfigDescription(
                    "The sideways axis of the CROSSING wrist while charging. Raw muscle space, " +
                    "same as the one above. Like every other pair here, the same number on both " +
                    "hands is not a mirror — 'In-Out' is named relative to the body.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(241)));

            BeamPoseChargeTorsoTwist = config.Bind(SecBeamPose, "ChargeTorsoTwist", 0.35f,
                new ConfigDescription(
                    "How far the SPINE twists while charging, taking the shoulder on the cupping " +
                    "side back and bringing the other one across. Positive is always that " +
                    "direction, whichever hand holds the ball — the code flips the sign with the " +
                    "side. Split across the three spine joints, most at the top and least at the " +
                    "bottom. This bends the body; ChargeBodyYaw TURNS it. Needs TorsoWeight above " +
                    "zero.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(240)));

            BeamPoseChargeTorsoLean = config.Bind(SecBeamPose, "ChargeTorsoLean", 0.2f,
                new ConfigDescription(
                    "How far the torso leans FORWARD while charging, over the cupped hands. " +
                    "Negative arches back instead. Small: this is the body closing around the " +
                    "ball, not a bow.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(239)));

            BeamPoseChargeBodyYaw = config.Bind(SecBeamPose, "ChargeBodyYaw", 30f,
                new ConfigDescription(
                    "How far the WHOLE BODY turns to the side while charging, in DEGREES — " +
                    "shoulders, hips and legs together, the classic side-on stance. Positive " +
                    "turns the cupping side away from the target, whichever hand holds the ball.\n" +
                    "This is a different thing from ChargeTorsoTwist and they stack: the twist " +
                    "bends the spine and leaves the hips facing forward, this rotates the root of " +
                    "the skeleton and takes everything with it. If you want the character " +
                    "standing sideways, this is the one; if you want him facing the target with " +
                    "his chest turned, that is the twist.\n" +
                    "It is DRAWING ONLY: aim, collision and where the beam goes are unchanged, " +
                    "and the arms compensate so the push still points where you are looking. " +
                    "Zero is how the pose shipped.",
                    new AcceptableValueRange<float>(-90f, 90f), ClientSide(238)));

            BeamPoseChargeHandCup = config.Bind(SecBeamPose, "ChargeHandCup", 0f,
                new ConfigDescription(
                    "How far the hands close into a CUP while charging — halfway curled fingers, " +
                    "slightly together, the hand that holds a sphere. Not a fist: a fist is the " +
                    "ki charging pose, and it is a different gesture. Both hands, since they hold " +
                    "the same ball. Zero leaves the fingers as the animation had them.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(237)));

            BeamPoseReleaseCupArmHeight = config.Bind(SecBeamPose, "ReleaseCupArmHeight", 0.2f,
                new ConfigDescription(
                    "Where the CUPPING upper arm sits at the push. Raw muscle space: 0 is " +
                    "horizontal, shoulder height. Slightly above, because the beam leaves from " +
                    "between the hands and a level push reads like presenting something. " +
                    "AimFollowPitch is added on top of this.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(236)));

            BeamPoseReleaseCrossArmHeight = config.Bind(SecBeamPose, "ReleaseCrossArmHeight", 0.2f,
                new ConfigDescription(
                    "Where the CROSSING upper arm sits at the push. Same raw muscle space. The " +
                    "two arms converge on the same gesture but they start from different places, " +
                    "which is why this is still a pair.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(235)));

            BeamPoseReleaseCupArmForward = config.Bind(SecBeamPose, "ReleaseCupArmForward", 0.75f,
                new ConfigDescription(
                    "How far FORWARD the CUPPING upper arm swings at the push. THIS IS THE " +
                    "GESTURE: at 0 the arm is out to the side, at 1 it points straight ahead. " +
                    "Higher than the ki blast's 0.6 to start with, because two hands pushing " +
                    "together end up closer to the centre line than one arm thrown out. " +
                    "AimFollowYaw moves the two arms off this in opposite directions.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(234)));

            BeamPoseReleaseCrossArmForward = config.Bind(SecBeamPose, "ReleaseCrossArmForward", 0.75f,
                new ConfigDescription(
                    "How far FORWARD the CROSSING upper arm swings at the push. Equal to the " +
                    "cupping arm is the symmetric push; unequal is one hand ahead of the other, " +
                    "which is a different and also real reading of the gesture.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(233)));

            BeamPoseReleaseCupArmTwist = config.Bind(SecBeamPose, "ReleaseCupArmTwist", 0f,
                new ConfigDescription(
                    "Rotation of the CUPPING upper arm along its own length at the push. Raw " +
                    "muscle space. With the elbow straight it barely changes the silhouette, but " +
                    "it decides which way the PALM faces — and the beam is born between the " +
                    "palms. Change this if the back of that hand ends up facing the target.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(232)));

            BeamPoseReleaseCrossArmTwist = config.Bind(SecBeamPose, "ReleaseCrossArmTwist", 1f,
                new ConfigDescription(
                    "Rotation of the CROSSING upper arm along its own length at the push. Same " +
                    "raw muscle space as the one above." +
                    " (Playtest value, 2026-09-08 — the first calibration of the push, and it only "
                    + "became possible once the aim moved out of the arms and into "
                    + "the torso.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(231)));

            BeamPoseReleaseCupForearmTwist = config.Bind(SecBeamPose, "ReleaseCupForearmTwist", 0f,
                new ConfigDescription(
                    "Rotation of the CUPPING FOREARM along its own length at the push. Raw muscle " +
                    "space. With the elbow straight this and ReleaseCupArmTwist do nearly the " +
                    "same thing — a straight arm has no bend for the two joints to disagree " +
                    "about — so the one to reach for here is whichever leaves the shoulder alone.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(230)));

            BeamPoseReleaseCrossForearmTwist = config.Bind(SecBeamPose, "ReleaseCrossForearmTwist", 0.5f,
                new ConfigDescription(
                    "Rotation of the CROSSING FOREARM along its own length at the push. Raw " +
                    "muscle space, same as the one above." +
                    " (Playtest value, 2026-09-08 — the first calibration of the push, and it only "
                    + "became possible once the aim moved out of the arms and into "
                    + "the torso.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(229)));

            BeamPoseReleaseCupElbowStretch = config.Bind(SecBeamPose, "ReleaseCupElbowStretch", 1f,
                new ConfigDescription(
                    "How STRAIGHT the CUPPING elbow is at the push. Raw muscle space, same scale " +
                    "as the charge elbows: 1 is the straightest the rig goes, 0 is already a " +
                    "visibly bent arm. The ki blast sits at 1 after playtest, because a thrust " +
                    "with a bend in it reads as a shove.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(228)));

            BeamPoseReleaseCrossElbowStretch = config.Bind(SecBeamPose, "ReleaseCrossElbowStretch", 1f,
                new ConfigDescription(
                    "How STRAIGHT the CROSSING elbow is at the push. Same raw muscle space as the " +
                    "one above.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(227)));

            BeamPoseReleaseCupShoulderPush = config.Bind(SecBeamPose, "ReleaseCupShoulderPush", 1f,
                new ConfigDescription(
                    "How far the CUPPING shoulder is pushed FORWARD at the push. This is the " +
                    "extra reach, and it is the difference between throwing the beam and holding " +
                    "it out." +
                    " (Playtest value, 2026-09-08 — the first calibration of the push, and it only "
                    + "became possible once the aim moved out of the arms and into "
                    + "the torso.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(226)));

            BeamPoseReleaseCrossShoulderPush = config.Bind(SecBeamPose, "ReleaseCrossShoulderPush", 0.9f,
                new ConfigDescription(
                    "How far the CROSSING shoulder is pushed FORWARD at the push." +
                    " (Playtest value, 2026-09-08 — the first calibration of the push, and it only "
                    + "became possible once the aim moved out of the arms and into "
                    + "the torso.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(225)));

            BeamPoseReleaseCupShoulderLift = config.Bind(SecBeamPose, "ReleaseCupShoulderLift", 0.1f,
                new ConfigDescription(
                    "How far the CUPPING shoulder rides UP at the push. Raw muscle space. Small: " +
                    "a shrugged shoulder on an extended arm reads as flinching.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(224)));

            BeamPoseReleaseCrossShoulderLift = config.Bind(SecBeamPose, "ReleaseCrossShoulderLift", 0.1f,
                new ConfigDescription(
                    "How far the CROSSING shoulder rides UP at the push. Raw muscle space.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(223)));

            BeamPoseReleaseCupWristBend = config.Bind(SecBeamPose, "ReleaseCupWristBend", 1f,
                new ConfigDescription(
                    "How far the CUPPING wrist bends back at the push, so the palm faces where " +
                    "the beam is going. Raw muscle space. The ki blast took this to the limit " +
                    "after playtest — the wrist is what aims the palm, and anything less pointed " +
                    "it at the ground while the shot went forward.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(222)));

            BeamPoseReleaseCrossWristBend = config.Bind(SecBeamPose, "ReleaseCrossWristBend", 1f,
                new ConfigDescription(
                    "How far the CROSSING wrist bends back at the push. Raw muscle space, same as " +
                    "the one above.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(221)));

            BeamPoseReleaseCupWristSide = config.Bind(SecBeamPose, "ReleaseCupWristSide", 0f,
                new ConfigDescription(
                    "The sideways axis of the CUPPING wrist at the push — the hand deviating " +
                    "toward the thumb or the little finger. Raw muscle space. This is what brings " +
                    "the two palms together at the centre when ReleaseWristBend has already got " +
                    "them facing forward.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(220)));

            BeamPoseReleaseCrossWristSide = config.Bind(SecBeamPose, "ReleaseCrossWristSide", -1f,
                new ConfigDescription(
                    "The sideways axis of the CROSSING wrist at the push. Raw muscle space, same " +
                    "as the one above." +
                    " (Playtest value, 2026-09-08 — the first calibration of the push, and it only "
                    + "became possible once the aim moved out of the arms and into "
                    + "the torso.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(219)));

            BeamPoseReleaseTorsoTwist = config.Bind(SecBeamPose, "ReleaseTorsoTwist", 0f,
                new ConfigDescription(
                    "How far the spine is still twisted at the push. Zero by default, and that is " +
                    "the point of the gesture: the charge is held from the side and the release " +
                    "squares the chest to the target. Positive keeps the cupping shoulder back.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(218)));

            BeamPoseReleaseTorsoLean = config.Bind(SecBeamPose, "ReleaseTorsoLean", 0.25f,
                new ConfigDescription(
                    "How far the torso leans FORWARD at the push — the body going into the beam. " +
                    "Negative arches back, which is the recoil reading if the beam should look " +
                    "heavy enough to push back.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(217)));

            BeamPoseReleaseBodyYaw = config.Bind(SecBeamPose, "ReleaseBodyYaw", 0f,
                new ConfigDescription(
                    "How far the WHOLE BODY is still turned to the side at the push, in DEGREES. " +
                    "Zero squares the character to his target as the beam leaves, which is what " +
                    "makes the side-on charge worth having: the turn is the wind-up and " +
                    "untwisting it is the throw. See ChargeBodyYaw.",
                    new AcceptableValueRange<float>(-90f, 90f), ClientSide(216)));

            BeamPoseReleaseHandOpen = config.Bind(SecBeamPose, "ReleaseHandOpen", 1f,
                new ConfigDescription(
                    "How far both hands open into flat palms at the push — the opposite of " +
                    "ChargeHandCup, and the contrast is the point: charging holds a sphere, " +
                    "firing pushes it. Zero leaves the fingers as the animation had them.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(215)));

            BeamPoseAimFollowPitch = config.Bind(SecBeamPose, "AimFollowPitch", 1f,
                new ConfigDescription(
                    "How much the CHEST follows where you are LOOKING UP OR DOWN at the push, on " +
                    "top of the two TorsoLean keys: looking up arches the chest back and both " +
                    "arms rise with it. It only applies to the push — while charging the hands " +
                    "are at the hip and there is nothing to aim. The projectiles spawn at the " +
                    "right hand and fly along your look direction, so with a locked torso, " +
                    "aiming at the sky sends the beam upward out of hands pointing at the " +
                    "horizon. The chest covers less angle than a free arm would, and that is the " +
                    "trade: the arms cannot aim this gesture at all — mirrored, the same delta " +
                    "sends one arm to each side. 1 is full follow, 0 pins the lean to the " +
                    "TorsoLean keys. Needs TorsoWeight above zero.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(214)));

            BeamPoseAimFollowYaw = config.Bind(SecBeamPose, "AimFollowYaw", 1f,
                new ConfigDescription(
                    "How much the SPINE TWIST follows where you are LOOKING LEFT OR RIGHT at the " +
                    "push, on top of the two TorsoTwist keys. The chest carries both arms " +
                    "together, so the hands stay together and the beam keeps leaving from " +
                    "between them — which is why this is not in the arms, where aiming right " +
                    "adds to one arm and subtracts from the other until one of them hits its " +
                    "limit and stops while the other keeps going. Most of the time the body has " +
                    "already turned to the camera and this does nothing; it earns its keep when " +
                    "the two come apart — strafing, or running one way while looking another. It " +
                    "does NOT cover the body turn from BodyYaw: see BodyYawCompensation. Needs " +
                    "TorsoWeight above zero.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(213)));

            BeamPoseBodyYawCompensation = config.Bind(SecBeamPose, "BodyYawCompensation", 1f,
                new ConfigDescription(
                    "How far the chest twists BACK to undo the body turn from ChargeBodyYaw and " +
                    "ReleaseBodyYaw, so the push still points where you are looking while the " +
                    "character stands side-on. 1 is the geometric amount, assuming a full unit " +
                    "of twist is worth a quarter turn; the spine covers less than that in " +
                    "practice, so raise it if the hands still point off to the side of the beam, " +
                    "and lower it if they over-rotate past it. 0 leaves the whole gesture turned " +
                    "with the body. This is the pose undoing its own rotation, not the player's " +
                    "aim, which is why AimFollowYaw does not switch it off. Needs TorsoWeight " +
                    "above zero.",
                    new AcceptableValueRange<float>(0f, 2f), ClientSide(212)));

            BeamPoseStanceWidth = config.Bind(SecBeamPose, "StanceWidth", 0.35f,
                new ConfigDescription(
                    "How far apart the feet are planted, in both halves of the gesture. Both legs " +
                    "get the same value, which in Unity muscle space should spread them " +
                    "symmetrically — if they scissor instead of spreading, a sign needs flipping " +
                    "in code, so report it.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(211)));

            BeamPoseKneeStretch = config.Bind(SecBeamPose, "KneeStretch", 0.25f,
                new ConfigDescription(
                    "How straight the knees are, in both halves of the gesture. Raw muscle space " +
                    "('Lower Leg Stretch'): 1 is a straight leg, -1 the tightest bend. READ " +
                    "HipDrop BEFORE LOWERING THIS: bending the knees shortens the leg, and if the " +
                    "hips do not come down with it the feet leave the ground. The game's foot IK " +
                    "will NOT save it — it runs before the mod writes the pose.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(210)));

            BeamPoseHipDrop = config.Bind(SecBeamPose, "HipDrop", 0.05f,
                new ConfigDescription(
                    "How far the hips come down, so the stance is a stance and not a character " +
                    "floating with folded legs. In avatar units, which for a player-sized rig is " +
                    "roughly metres. INDEPENDENT of KneeStretch here, unlike the ki charging " +
                    "pose, where the drop is proportional to the knee: two separate numbers " +
                    "calibrate against each other on screen in two steps, and neither of them " +
                    "lies about what it does. Zero disables it, which is the safe fallback if it " +
                    "looks wrong.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(209)));

            // As duas senoides, e elas valem so' para a concha: o empurrao e' curto demais para
            // oscilar sem virar outra coisa. Mesma divisao de trabalho da recarga — o esforco e'
            // lento e grande, o tremor e' rapido e minusculo.

            BeamPoseStrain = config.Bind(SecBeamPose, "Strain", 0.07f,
                new ConfigDescription(
                    "How much the whole body sinks and rises while HOLDING the charge, like " +
                    "someone breathing in through the effort. Fades out completely as the push " +
                    "takes over. Zero freezes the charge into a shop-window dummy — which matters " +
                    "here because the charge lasts seconds and the eye has time to notice.",
                    new AcceptableValueRange<float>(0f, 0.5f), ClientSide(208)));

            BeamPoseStrainSpeed = config.Bind(SecBeamPose, "StrainSpeed", 2.2f,
                new ConfigDescription(
                    "Speed of that slow breathing, in radians per second. Slow: this is effort, " +
                    "not panting.",
                    new AcceptableValueRange<float>(0.1f, 20f), ClientSide(207)));

            BeamPoseTremor = config.Bind(SecBeamPose, "Tremor", 0.02f,
                new ConfigDescription(
                    "How much the arms and shoulders shake while holding the charge. " +
                    "Deliberately tiny: this should be felt, not seen. If you can tell it is a " +
                    "sine wave, it is too high. Fades out with the push, same as Strain.",
                    new AcceptableValueRange<float>(0f, 0.3f), ClientSide(206)));

            BeamPoseTremorSpeed = config.Bind(SecBeamPose, "TremorSpeed", 17f,
                new ConfigDescription(
                    "Speed of that fast tremble, in radians per second. The two sides run at " +
                    "slightly different rates on purpose — in sync it reads as machine vibration, " +
                    "out of phase it reads as muscle.",
                    new AcceptableValueRange<float>(1f, 60f), ClientSide(205)));

            // Mesmo emote do carregamento, mas de disparo unico: carregar segura a pose, transformar
            // e' um estouro. O grito replica sozinho pela ZDO — os amigos veem e ouvem.
            TransformEmote = config.Bind(SecEffects, "TransformEmote", "roar",
                new ConfigDescription(
                    "One-shot emote played when you power up into a form. Empty disables it. " +
                    "Not played when stepping DOWN a form: coming down is relief, not a beam. " +
                    "Any emote the player Animator knows works — the same names the /emote chat " +
                    "command lists.",
                    null, ClientSide(55)));

            // Mesmo prefab do carregamento de ki de proposito: ele ja se provou legivel preso ao
            // jogador, e a cor e' quem separa os dois estados — azul carregando, a cor da forma
            // transformado. A cor NAO fica aqui: e' por forma, na secao de cada uma.
            TransformAuraPrefab = config.Bind(SecEffects, "TransformAuraPrefab",
                "fx_DvergerMage_Support_start",
                new ConfigDescription(
                    "Effect beam when you power up into a form. Empty disables it. " +
                    "It fires once and fades — see TransformAuraForceLoop for why it is not kept " +
                    "alive while the form lasts. Not played when stepping DOWN a form, same as " +
                    "the emote. The color comes from each form's own AuraColor, not from here. " +
                    "Alternatives: fx_goblinking_nova, fx_ShieldCharge_1 through _5 " +
                    "(increasing), DvergerStaffNova_aoe.",
                    null, ClientSide(50)));

            TransformAuraScale = config.Bind(SecEffects, "TransformAuraScale", 2.5f,
                new ConfigDescription(
                    "Scale of the beam. Slightly larger than the charging effect on purpose: " +
                    "transforming should read bigger than charging up to it.",
                    new AcceptableValueRange<float>(0.1f, 5f), ClientSide(45)));

            // A duracao e' imposta por nos, nao herdada do prefab: prefab de efeito sustentado ja
            // vem com as particulas em loop, e o TimedDestruction dele so dispara sozinho se o
            // prefab marcou m_triggerOnAwake. Confiar nos dois foi o que deixou o efeito aceso a
            // forma inteira (2026-08-02).
            TransformAuraDuration = config.Bind(SecEffects, "TransformAuraDuration", 2f,
                new ConfigDescription(
                    "How long the beam lasts, in seconds, before it is removed from the player. " +
                    "This is enforced by the mod and does not depend on the prefab cleaning up " +
                    "after itself — some of them never do, which is what used to leave the " +
                    "effect burning for the whole transformation. " +
                    "Ignored when TransformAuraForceLoop is on, where the effect is meant to " +
                    "last as long as the form. 0 hands the decision back to the prefab. " +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<float>(0f, 10f), ClientSide(42)));

            // false por playtest (2026-08-02). Ver a descricao: em loop o efeito virou fumaca
            // colada no personagem, e o Henrique pediu de volta so o estouro da ativacao.
            TransformAuraForceLoop = config.Bind(SecEffects, "TransformAuraForceLoop", false,
                new ConfigDescription(
                    "Keeps the effect alive for as long as the form lasts, by forcing its " +
                    "particles and audio to repeat. OFF by default, and that is a playtest " +
                    "result, not an oversight: game prefabs are built for a half-second beam, " +
                    "and looping one does not make it last longer — it makes it a permanent " +
                    "cloud stuck to the player. The particles never get to disperse. " +
                    "Turning this on with a prefab designed for a sustained aura is fine; " +
                    "turning it on with a beam prefab is what produced the smoke. " +
                    "(Playtest value, 2026-08-02.)",
                    null, ClientSide(40)));

            // 1 (nao mexe) porque o efeito voltou a ser um estouro: luz num flash de meio segundo
            // e' justamente o que da' o baque. A chave existe para quem ligar o ForceLoop, onde
            // luz presa ao jogador por minutos vira lanterna iluminando o terreno em volta.
            TransformAuraLightIntensity = config.Bind(SecEffects, "TransformAuraLightIntensity", 1f,
                new ConfigDescription(
                    "Multiplier for the effect's dynamic light. 1 leaves the prefab as it came, " +
                    "which is right for a beam — the flash is most of the punch. " +
                    "0 removes the light entirely and keeps only the particles. " +
                    "That matters if you turn TransformAuraForceLoop on: a light that follows " +
                    "you for minutes lights up the terrain around you and gets tiring, while " +
                    "the particles glow on their own and do not need it.",
                    new AcceptableValueRange<float>(0f, 2f), ClientSide(38)));

            // --- Raios das formas altas ---
            //
            // Estalos REPETIDOS, e nao um efeito sustentado, e a razao esta' na chave acima: prefab
            // do jogo forcado a repetir vira nuvem colada no personagem (playtest de 2026-08-02).
            // Raio nao tem esse problema porque ele JA' e' intermitente por natureza — a leitura
            // certa e' um estalo curto aqui, outro ali, que e' literalmente como o SSJ2 se
            // apresenta. O efeito dura enquanto a forma durar sem nunca ficar aceso.
            FormLightningPrefab = config.Bind(SecEffects, "FormLightningPrefab", "fx_Lightning",
                new ConfigDescription(
                    "Prefab of a single lightning crackle, spawned over and over around the body " +
                    "while a form with LightningEnabled is active. Empty disables the crackles " +
                    "for every form at once — the per-form key only says WHICH forms crackle. " +
                    "Alternatives: fx_chainlightning_spread (spreads wider), fx_redlightning_beam " +
                    "(red variant, for a form of another color), vfx_HitSparks (small sparks).",
                    null, ClientSide(36)));

            FormLightningInterval = config.Bind(SecEffects, "FormLightningInterval", 0.55f,
                new ConfigDescription(
                    "Average seconds between two crackles. Lower is more frantic. " +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<float>(0.05f, 10f), ClientSide(35)));

            // Sem jitter os estalos batem em compasso e leem como maquina, nao como energia
            // instavel. E' a mesma razao pela qual o proprio jogo randomiza som de passo.
            FormLightningIntervalJitter = config.Bind(SecEffects, "FormLightningIntervalJitter", 0.6f,
                new ConfigDescription(
                    "How much the interval varies, as a fraction of it. 0.6 means each gap is " +
                    "drawn between 40% and 160% of FormLightningInterval. " +
                    "0 turns the crackles into a metronome, which reads as a machine rather than " +
                    "as unstable energy.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(34)));

            FormLightningCount = config.Bind(SecEffects, "FormLightningCount", 1,
                new ConfigDescription(
                    "How many bolts fire per crackle. Above 1 they are scattered independently " +
                    "around the body in the same instant. " +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<int>(1, 8), ClientSide(33)));

            FormLightningScale = config.Bind(SecEffects, "FormLightningScale", 0.5f,
                new ConfigDescription(
                    "Scale of each bolt. Well under the transformation beam on purpose: these " +
                    "are sparks around the body, not an explosion. " +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<float>(0.05f, 5f), ClientSide(32)));

            FormLightningRadius = config.Bind(SecEffects, "FormLightningRadius", 0.6f,
                new ConfigDescription(
                    "How far from the body's center line the bolts pop, in meters. " +
                    "A player is roughly 0.4m wide, so values under that put the bolts inside " +
                    "the character and values well over it read as weather instead of aura. " +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<float>(0f, 5f), ClientSide(31)));

            FormLightningHeight = config.Bind(SecEffects, "FormLightningHeight", 1.1f,
                new ConfigDescription(
                    "Height above the feet of the center of the band where bolts spawn, in " +
                    "meters. 1.1 is about chest height on a Valheim character. " +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<float>(-1f, 4f), ClientSide(30)));

            FormLightningSpread = config.Bind(SecEffects, "FormLightningSpread", 1.8f,
                new ConfigDescription(
                    "Total height of that band, in meters. 1.8 covers the whole body, so bolts " +
                    "appear from the feet to just over the head. Smaller values concentrate them " +
                    "around FormLightningHeight. " +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<float>(0f, 6f), ClientSide(29)));

            // Imposta por nos pelo mesmo motivo da TransformAuraDuration: o TimedDestruction do
            // prefab so dispara sozinho se ele marcou m_triggerOnAwake, e aqui o custo de confiar
            // seria pior que la' — sao dezenas de objetos por minuto, nao um por transformacao.
            FormLightningDuration = config.Bind(SecEffects, "FormLightningDuration", 0.35f,
                new ConfigDescription(
                    "How long each bolt lasts, in seconds. Short on purpose: a crackle that " +
                    "lingers stops reading as lightning. " +
                    "Enforced by the mod and not left to the prefab, which matters more here " +
                    "than for the transformation beam — this spawns dozens of objects a minute, " +
                    "and one that forgets to clean itself up would pile up on the player. " +
                    "0 hands the decision back to the prefab. " +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<float>(0f, 5f), ClientSide(28)));

            // 0 de proposito: a luz dinamica de fx_Lightning ilumina o terreno, e piscando a cada
            // meio segundo ela vira estroboscopio na cara de quem esta' jogando a noite. As
            // particulas tem brilho proprio e continuam visiveis sem ela.
            FormLightningLightIntensity = config.Bind(SecEffects, "FormLightningLightIntensity", 0f,
                new ConfigDescription(
                    "Multiplier for each bolt's dynamic light. 0 removes the light and keeps only " +
                    "the particles, which is the default and deliberate: a light flashing every " +
                    "half second lights up the terrain around you and turns into a strobe at " +
                    "night. The particles glow on their own. 1 leaves the prefab as it came. " +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<float>(0f, 2f), ClientSide(27)));

            // --- Brilho das formas ---
            //
            // Uma luz nua presa ao jogador, sem prefab e sem particula nenhuma. E' o contrario
            // exato da TransformAuraLightIntensity logo acima: la' a luz e' herdada de um prefab
            // de ESTOURO e o aviso e' para nao deixa-la acesa por engano; aqui ela e' o pedido, e
            // por isso tem regulagem propria e discreta. Ver a cabeca do FormGlow.
            FormGlowIntensity = config.Bind(SecEffects, "FormGlowIntensity", 0.8f,
                new ConfigDescription(
                    "How brightly a transformed body lights up the ground around it, before each " +
                    "form's own GlowIntensity multiplier. 0 turns the glow off for every form at " +
                    "once. " +
                    "Deliberately well under a torch (~1.5): this is meant to be noticed at " +
                    "night and to barely register at noon, not to light your way. " +
                    "This is a plain point light, not a particle effect — it is the one sustained " +
                    "effect that cannot turn into the cloud that looping a beam prefab did. " +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<float>(0f, 5f), ClientSide(26)));

            FormGlowRange = config.Bind(SecEffects, "FormGlowRange", 6f,
                new ConfigDescription(
                    "How far the glow reaches, in meters. Small on purpose: the point is a pool " +
                    "of light around the character, so you can tell someone is transformed from " +
                    "the ground under them. Large values light up half a clearing and stop " +
                    "reading as coming from the body. " +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<float>(0.5f, 30f), ClientSide(25)));

            FormGlowHeight = config.Bind(SecEffects, "FormGlowHeight", 1.1f,
                new ConfigDescription(
                    "Height of the light above the feet, in meters. 1.1 is about chest height on " +
                    "a Valheim character, which lights the character and the ground at once. " +
                    "Lower puts more light on the ground and less on the body. " +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<float>(-1f, 4f), ClientSide(24)));

            // Sem pulso a luz le como lampada — cenario, nao energia. E' o mesmo motivo pelo qual
            // os estalos sao sorteados em vez de bater em compasso.
            FormGlowPulseAmount = config.Bind(SecEffects, "FormGlowPulseAmount", 0.15f,
                new ConfigDescription(
                    "How much the glow breathes, as a fraction of its intensity. 0.15 means it " +
                    "swings between 85% and 115%, with the configured intensity as the AVERAGE " +
                    "rather than the ceiling. " +
                    "0 leaves it perfectly steady, which reads as a lamp bolted to the character " +
                    "instead of energy he is barely holding in. " +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(23)));

            // Calibrado no playtest de 2026-08-27, o primeiro do brilho: saiu em 1,2 — "um ciclo
            // por segundo e' o ritmo de uma respiracao" — e desceu para 0,3, quatro vezes mais
            // lento. A analogia estava errada: o corpo transformado nao respira no ritmo de quem
            // esta' em repouso, e a cadencia de respiracao literal lida de fora le como a luz
            // piscando. Devagar o bastante para o olho nao contar os ciclos, a luz volta a parecer
            // instavel em vez de pulsante.
            FormGlowPulseSpeed = config.Bind(SecEffects, "FormGlowPulseSpeed", 0.3f,
                new ConfigDescription(
                    "Speed of that breathing, in full cycles per second. Slow on purpose: 0.3 is " +
                    "one swell every three seconds or so, slow enough that the eye reads it as " +
                    "energy shifting rather than counting the cycles. " +
                    "Anything near 1 — the rate of actual breathing — reads as the light " +
                    "flickering, which is a playtest result (2026-08-27) and not what it sounds " +
                    "like on paper. " +
                    "Ignored when FormGlowPulseAmount is 0.",
                    new AcceptableValueRange<float>(0f, 10f), ClientSide(22)));

            FormGlowFade = config.Bind(SecEffects, "FormGlowFade", 0.4f,
                new ConfigDescription(
                    "Seconds the glow takes to come up when you transform and to go out when you " +
                    "drop back. 0 snaps it on and off, which pops — especially on the way out, " +
                    "where nothing else is happening on screen to cover it. " +
                    "(Starting value. Not playtested yet.)",
                    new AcceptableValueRange<float>(0f, 5f), ClientSide(21)));

            // Desligada de proposito: luz pontual com sombra custa SEIS mapas de sombra por
            // quadro, e esta luz vive minutos, nao frames — e uma por jogador transformado na
            // cena. A chave existe porque sombra projetada de um corpo brilhante e' bonita, e a
            // decisao de pagar por ela e' de quem olha a tela.
            FormGlowShadows = config.Bind(SecEffects, "FormGlowShadows", false,
                new ConfigDescription(
                    "Let the glow cast shadows. Off by default and deliberately: a point light " +
                    "with shadows renders six shadow maps per frame, this light stays on for as " +
                    "long as the form does, and there is one per transformed player in the scene. " +
                    "On, the character throws his own shadow outward, which looks great and costs " +
                    "real frames. " +
                    "(Starting value. Not playtested yet.)",
                    null, ClientSide(20)));

            // --- Debug ---
            ShowRemotePoses = config.Bind(SecMultiplayer, "ShowRemotePoses", true,
                new ConfigDescription(
                    "Draw the mod poses (flight, ki charge, ki blast) on other players.",
                    null, ClientSide(0)));

            ShowRemoteEffects = config.Bind(SecMultiplayer, "ShowRemoteEffects", true,
                new ConfigDescription(
                    "Draw the mod effects (transformation beam, ki charge glow) on other players.",
                    null, ClientSide(1)));

            VerboseLogging = config.Bind(SecDebug, "VerboseLogging", false,
                new ConfigDescription("Detailed logging in the BepInEx console.",
                    null, ClientSide(100)));

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
            string hairItem = "")
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

                // Referencia para calibrar: a curva do Valheim ((nivel+1)^1.5 * 0.5 + 0.5 por
                // nivel) cobra ~20.000 de XP para ir do 0 ao 100, e ~1.600 para chegar ao 30.
                // A 1/s, o nivel 30 sai com ~27 minutos DENTRO da forma — que nao e' o mesmo que
                // 27 minutos de jogo, porque o dreno obriga a recarregar entre uma e outra.
                MasteryXpPerSecond = config.Bind(section, "MasteryXpPerSecond", 1f,
                    new ConfigDescription(
                        "XP for this form's skill per second transformed. Holding the form is the " +
                        "only way to train it, the same way flying is the only way to train Flight. " +
                        "Valheim's own diminishing curve up to 100 applies on top: reaching level " +
                        "30 costs about 1600 XP and level 100 about 20000. \n" +
                        "(Kept at 1 after the 2026-09-06 run, deliberately. That run read SSJ " +
                        "mastery at level 52 with the third boss about to fall, where 80-90 was " +
                        "the target, and raising this key was the obvious fix — it was rejected " +
                        "because it speeds up the whole ladder from the first minute, including " +
                        "the rung the player has just unlocked, which is exactly where the slow " +
                        "climb is supposed to be felt. MasteryXpPerBossBonus below carries the " +
                        "correction instead: the deficit is on the OLD rung and only in the last " +
                        "stretch of play. This key sets the overall pace, that one sets the shape.)",
                        new AcceptableValueRange<float>(0f, 20f), AdminOnly(70))),

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
                        "- this form's rung), floored at 1. 0 disables it. \n" +
                        "With the default 2: the form pays x1 while its own boss is the newest " +
                        "kill, x3 after the next boss falls, x5 after the one after that. A form " +
                        "unlocked at the second boss is one rung behind, so at the third boss it " +
                        "is still on x3 while the first form is already on x5. \n" +
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

                // O PENTEADO da forma, e nao so' a cor dele. Mesma via da cor — VisEquipment
                // escreve na ZDO, o jogo replica de graca e o penteado de verdade do personagem
                // (Humanoid.m_hairItem, que E' serializado no perfil) nunca e' tocado. Ver
                // Transformations.TransformationEffects.SetHairStyle.
                //
                // Cabe aqui e nao numa secao global pelo mesmo motivo da cor: o penteado e'
                // identidade de DEGRAU. O SSJ3 e' a forma que o genero define pelo comprimento do
                // cabelo, e sem esta chave a unica diferenca dele para o SSJ2 seria o tom do
                // amarelo.
                //
                // Tres tipos de valor: "Spiked" (o cabelo do proprio personagem, na versao
                // espetada do CustomHair), um item do jogo (Hair1..Hair37, HairNone) ou um item
                // nosso (SaiyaHair6...), fixo para quem quer que transforme. Nome invalido nao
                // pinta nem estoura — o mod avisa no log e mantem o cabelo do personagem, porque
                // um Hair99 no .cfg deixaria o jogador CARECA em forma, que e' pior que ignorar a
                // chave. "Spiked" sobre um cabelo sem versao espetada tambem mantem o do
                // personagem, mas sem aviso: ali nao ha erro de ninguem.
                HairItem = config.Bind(section, "HairItem", hairItem,
                    new ConfigDescription(
                        "Hairstyle worn while this form is active. Empty keeps the character's " +
                        "own hair, which is what every form did before this key existed. \n" +
                        "'Spiked' wears the spiked version of the character's own hair, whatever " +
                        "it is. Hairs with no spiked version (bald, and any the mod has not " +
                        "sculpted yet) keep the character's own. \n" +
                        "Any other value is a fixed hairstyle, by item name: the game's own " +
                        "Hair1 to Hair37 and HairNone — the barber's list — or the mod's spiked " +
                        "SaiyaHair1, SaiyaHair2 and so on, one per game hair. They are numbered, " +
                        "not named, so run 'saiya_form hair' in the console to print the list " +
                        "with the readable name of each one, and 'saiya_form hair <name>' to try " +
                        "one on without transforming. \n" +
                        "The long ones are Hair6 (Long and Loose), Hair11 (Long Braid) and Hair30 " +
                        "(Loose Waves). \n" +
                        "A helmet hides the hair exactly as it hides your normal one — the form " +
                        "keeps its hairstyle, you just cannot see it. Hoods and helmets that swap " +
                        "the hair for a shorter variant show the game's plain variant, without " +
                        "spikes. \n" +
                        "The character's real hairstyle is never overwritten: this only lives for " +
                        "as long as the form does, and a crash while transformed leaves nothing " +
                        "behind.",
                        null, ClientSide(51))),

                // Cosmetico, entao ClientSide como o resto da secao 8: pintar o cabelo nao muda
                // numero nenhum, e o servidor nao tem por que impor gosto visual. A cor troca via
                // ZDO e replica sozinha, entao os amigos veem o cabelo de quem transformou mesmo
                // com .cfg diferente do deles.
                HairColor = config.Bind(section, "HairColor", hairColor,
                    new ConfigDescription(
                        "Hair color while this form is active, #RRGGBB format. Empty keeps the " +
                        "character's own color. Applies on the next transformation — no restart " +
                        "needed. The character's real hair color is never overwritten: this only " +
                        "lives for as long as the form does.",
                        null, ClientSide(50))),

                HairColorIntensity = config.Bind(section, "HairColorIntensity", 1.6f,
                    new ConfigDescription(
                        "Brightness multiplier applied on top of HairColor. Above 1 the color " +
                        "blows out and burns, which is what reads as Super Saiyan hair — a plain " +
                        "hex tops out at #FFFFFF and lands closer to dyed than to glowing. " +
                        "1 uses the hex as written. " +
                        "(Starting value. Not playtested yet.)",
                        new AcceptableValueRange<float>(0f, 5f), ClientSide(45))),

                // Uma cor por forma, e nao uma global na secao 8: a escada da etapa 7 quer degraus
                // distinguiveis de longe, e a cor da aura e' o unico sinal que sobrevive a
                // distancia. O prefab e' compartilhado; a cor e' a identidade.
                AuraColor = config.Bind(section, "AuraColor", hairColor,
                    new ConfigDescription(
                        "Aura color while this form is active, #RRGGBB format. Empty keeps the " +
                        "prefab's original color. Applies on the next transformation — no restart " +
                        "needed. Defaults to the same color as the hair so the two read as one " +
                        "thing; splitting them is fine if the aura washes out at that tone. " +
                        "This also becomes the color of the ki CHARGING glow while you hold the " +
                        "form, replacing Effects.ChargeEffectColor.",
                        null, ClientSide(40))),

                // Um booleano, e nao um prefab por forma: o que separa um degrau do outro é
                // crepitar ou não, e a regulagem de um estalo é a mesma em qualquer forma. Mesma
                // divisão da aura — prefab compartilhado na seção 8, identidade aqui.
                LightningEnabled = config.Bind(section, "LightningEnabled", lightning,
                    new ConfigDescription(
                        "Crackle bolts of lightning around the body for as long as this form is " +
                        "active. It is the visual signature of the higher forms, and unlike the " +
                        "transformation beam it lasts the whole time — lightning is intermittent " +
                        "by nature, so repeating it does not turn into the permanent cloud that " +
                        "looping a beam prefab did. " +
                        "Everything about HOW the crackles look lives in Effects " +
                        "(FormLightning*), shared by every form; this key only says which forms " +
                        "get them.",
                        null, ClientSide(37))),

                // A unica cor que NAO acompanha as outras, e de proposito. Ver o playtest de
                // 2026-08-16 na descricao: raio da cor da aura le como mais aura, nao como
                // eletricidade.
                LightningColor = config.Bind(section, "LightningColor", lightningColor,
                    new ConfigDescription(
                        "Color of this form's lightning, #RRGGBB format. Empty falls back to " +
                        "AuraColor. " +
                        "This is the one color of a form that is meant to CONTRAST with the " +
                        "others rather than match them, and that is a playtest result " +
                        "(2026-08-16): bolts tinted like the aura read as more aura, not as " +
                        "electricity — the eye needs the hue break to tell them apart from the " +
                        "glow they sit on top of. Electric blue over a gold aura is the classic " +
                        "read; white also works. " +
                        "Applies to the next bolt, so you can retune it with the game open. " +
                        "Ignored when LightningEnabled is off.",
                        null, ClientSide(35))),

                // Um numero e nao um booleano, ao contrario do raio: o raio ou estala ou nao
                // estala, mas o brilho de um degrau alto e' o mesmo brilho MAIS FORTE, e e' esse
                // eixo continuo que deixa a escada legivel de longe.
                GlowIntensity = config.Bind(section, "GlowIntensity", glowIntensity,
                    new ConfigDescription(
                        "How brightly this form lights up its surroundings, as a multiplier on " +
                        "Effects.FormGlowIntensity. 0 turns the glow off for this form only. " +
                        "Higher forms are meant to be brighter — this is the one visual key that " +
                        "reads as a LADDER rather than as an identity, and at a distance it is " +
                        "what tells two forms apart at night. " +
                        "Applies immediately, so you can retune it with the game open.",
                        new AcceptableValueRange<float>(0f, 5f), ClientSide(34))),

                GlowColor = config.Bind(section, "GlowColor", glowColor,
                    new ConfigDescription(
                        "Color of this form's glow, #RRGGBB format. Empty falls back to " +
                        "AuraColor, which is the normal case — the form has one color. " +
                        "Worth splitting only if the aura tone washes out as light on terrain: " +
                        "a saturated hue that reads well on particles can turn muddy once it is " +
                        "lighting grass and stone. " +
                        "Unlike LightningColor, this one is meant to MATCH the rest of the form. " +
                        "Ignored when the glow is off.",
                        null, ClientSide(33)))
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
            float knockback = 30f, float projectileSpeed = 30f, float projectileLifetime = 3f,
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

                // Visual, e por isso config: o quanto um feixe carregado deve parecer mais grosso
                // que um curto so' se sabe olhando. 1 tira a diferenca sem tirar a mecanica.
                ChargeMinScale = config.Bind(section, "ChargeMinScale", chargeMinScale,
                    new ConfigDescription(
                        "How thick the projectiles are at the smallest charge, as a fraction of " +
                        "ProjectileScale — the size they reach at a full one. 1 makes charge " +
                        "change only the length of the beam, never its thickness. " +
                        "Visual only: it does NOT change what the projectiles hit, nor the damage.",
                        new AcceptableValueRange<float>(0.05f, 1f), ClientSide(54))),

                // Prefab do jogo, nao asset novo. O vault e' explicito sobre este ser o pedaco que
                // vende a cena: segundo [[Animacoes]], o carregamento se le pelas particulas nas
                // maos, nao pela pose — e por isso ele vem antes da pose de duas maos.
                ChargeEffectPrefab = config.Bind(section, "ChargeEffectPrefab", chargeEffectPrefab,
                    new ConfigDescription(
                        "Game prefab attached to the player while the attack charges. Empty shows " +
                        "nothing, which leaves the player with no way to tell a charge is running. " +
                        "Worth trying: fx_charred_firestaff_chargeup, fx_DvergerMage_Support, " +
                        "vfx_blocked, fx_Potion_stamina_medium.",
                        null, ClientSide(53))),

                ChargeEffectColor = config.Bind(section, "ChargeEffectColor", chargeEffectColor,
                    new ConfigDescription(
                        "Colour of the charge effect, as #RRGGBB. Empty follows ProjectileColor, " +
                        "so what gathers in the hand is the colour of what comes out of it — " +
                        "asking twice would only create the chance of the two drifting apart.",
                        null, ClientSide(52))),

                ChargeEffectScale = config.Bind(section, "ChargeEffectScale", chargeEffectScale,
                    new ConfigDescription(
                        "Size of the charge effect at a full charge, 1 being the prefab as it came. " +
                        "It grows from ChargeMinScale x this up to this as the charge fills, so " +
                        "the ball in the hand reads as filling up.",
                        new AcceptableValueRange<float>(0.1f, 10f), ClientSide(51))),

                // Tres chaves e nao um vetor: o .cfg do BepInEx nao tem tipo de vetor, e as tres
                // coordenadas viriam de uma string parseada a mao.
                //
                // ⚠️ **As tres sao medidas nos eixos do JOGADOR** — direita, cima e frente do
                // personagem —, e nao nos do osso em que o efeito esta pendurado. A diferenca so'
                // aparece com uma pose de verdade em cima: preso a mao, os eixos do osso giram com
                // o pulso, e cada ajuste de pose invalidava a calibragem das tres. Trocado em
                // 2026-09-07, durante a calibragem da pose de duas maos. Ver
                // KiBeamChargeEffects.Place.
                ChargeEffectHeight = config.Bind(section, "ChargeEffectHeight", chargeEffectHeight,
                    new ConfigDescription(
                        "Height of the charge effect, in metres, measured along the PLAYER'S up — " +
                        "not the hand bone's, so it does not turn with the wrist. Anchored to the " +
                        "body it counts from the feet, where about 1 is hand height on a standing " +
                        "character; anchored to a hand it counts from the palm, so the useful " +
                        "numbers are small and negative means below the hand.",
                        new AcceptableValueRange<float>(-3f, 3f), ClientSide(50))),

                ChargeEffectSide = config.Bind(section, "ChargeEffectSide", chargeEffectSide,
                    new ConfigDescription(
                        "Sideways offset of the charge effect, in metres. Positive is the " +
                        "player's right, whatever the effect is anchored to and whatever the pose " +
                        "is doing with that hand. It follows the body, so turning around does not " +
                        "leave it behind.",
                        new AcceptableValueRange<float>(-2f, 2f), ClientSide(49))),

                ChargeEffectForward = config.Bind(
                    section, "ChargeEffectForward", chargeEffectForward,
                    new ConfigDescription(
                        "Forward offset of the charge effects, in metres — the player's forward, " +
                        "the direction the character's body faces. Anchored to a hand, this is " +
                        "what pushes the ball off the palm instead of leaving it inside it.",
                        new AcceptableValueRange<float>(-2f, 2f), ClientSide(42))),

                // A chave que faz a pose futura valer de graca. Presos ao OSSO da mao, os efeitos
                // vao para onde a animacao levar a mao; medidos a partir dos pes, ficam boiando
                // onde a mao estava antes. Enquanto nao ha' pose os dois parecem iguais, e e' por
                // isso que vale decidir agora e nao depois.
                //
                // ⚠️ Trocar isto muda a ORIGEM das tres chaves de offset acima: na mao elas sao
                // medidas a partir da palma (numeros perto de zero), no corpo a partir dos pes (a
                // altura da mao e' ~1). Um conjunto de numeros no outro modo poe o efeito a um
                // metro de onde deveria. O que NAO muda sao as direcoes: as tres sempre andam nos
                // eixos do jogador.
                ChargeEffectAnchor = config.Bind(
                    section, "ChargeEffectAnchor", chargeEffectAnchor,
                    new ConfigDescription(
                        "What the charge effects are pinned to. RightHand and LeftHand pin them to " +
                        "the hand BONE, so they are carried by whatever the character does — a " +
                        "future charging pose moves them with it, at no cost. Body pins them to " +
                        "the character root, which is steady but has to be re-measured by hand " +
                        "every time the pose changes. " +
                        "This changes what the three offsets above MEAN: from a hand they are " +
                        "measured from the palm, and near zero; from the body they are measured " +
                        "from the feet, where hand height is about 1. " +
                        "Falls back to the body while the skeleton is not built yet.",
                        null, ClientSide(41))),

                // Prefab de PROJETIL, e nao de efeito, e e' o ponto: o Valheim nao tem um "fx_" que
                // seja uma esfera de energia parada, mas tem varias que voam. O StaticProp arranca
                // o comportamento e deixa o visual. Ver Util/StaticProp.cs.
                ChargeBallPrefab = config.Bind(section, "ChargeBallPrefab", chargeBallPrefab,
                    new ConfigDescription(
                        "The ball of ki that gathers in the hand while charging. This is the name " +
                        "of a PROJECTILE prefab, not an effect one: the game has no effect that is " +
                        "a ball of energy sitting still, but it has several that fly, and the mod " +
                        "strips the flying part. It grows from ChargeMinScale to ChargeBallScale " +
                        "as the charge fills. Empty shows no ball. " +
                        "Worth trying: GoblinShaman_projectile_fireball, " +
                        "DvergerStaffBlocker_projectile (a denser sphere), " +
                        "DvergerStaffIce_projectile, staff_greenroots_projectile.",
                        null, ClientSide(39))),

                ChargeBallColor = config.Bind(section, "ChargeBallColor", chargeBallColor,
                    new ConfigDescription(
                        "Colour of the charge ball, as #RRGGBB. Empty follows ProjectileColor, so " +
                        "what gathers in the hand is the colour of what comes out of it.",
                        null, ClientSide(38))),

                ChargeBallScale = config.Bind(section, "ChargeBallScale", chargeBallScale,
                    new ConfigDescription(
                        "Size of the charge ball at a full charge, 1 being the projectile prefab " +
                        "as it came. It starts at ChargeMinScale times this and grows to it, which " +
                        "is the same curve the projectiles themselves follow — so the ball in the " +
                        "hand is the size of what is about to leave it.",
                        new AcceptableValueRange<float>(0.1f, 10f), ClientSide(37))),

                // O rastro e' a peca do projetil que so' faz sentido em movimento: parado na mao,
                // ele vira uma nuvem crescendo em volta dela. Mesmo mecanismo e mesmas regras do
                // ImpactEffectStrip — nome INTEIRO, e o log lista os nomes disponiveis.
                ChargeBallStrip = config.Bind(section, "ChargeBallStrip", chargeBallStrip,
                    new ConfigDescription(
                        "Comma-separated names of particle emitters to remove from the charge " +
                        "ball. A projectile prefab carries the trail it left while flying, and a " +
                        "trail on something STANDING STILL reads as a cloud of smoke growing " +
                        "around the hand — which is the one part of the projectile that only makes " +
                        "sense in motion. Empty removes nothing. " +
                        "Names must match in full, case aside, exactly like ImpactEffectStrip. " +
                        "Turn VerboseLogging on and start a charge: the log prints " +
                        "'Static prop <prefab>: emitters: ...' with every name inside the ball, " +
                        "ready to copy from.",
                        null, ClientSide(36))),

                // O aviso de carga cheia. Sem ele o jogador nao tem como saber que parou de ganhar
                // coisa por continuar segurando — a bola para de crescer, mas "parou de crescer" e'
                // dificil de ler numa particula que ja' esta' se mexendo sozinha.
                //
                // Sai no MESMO ponto do corpo que a bola, pelo ChargeEffectSide/Height: sao dois
                // sinais sobre a mesma coisa, e separa-los em dois lugares da tela leria como duas
                // coisas acontecendo.
                ChargeFullEffectPrefab = config.Bind(
                    section, "ChargeFullEffectPrefab", chargeFullEffectPrefab,
                    new ConfigDescription(
                        "Game prefab played when the charge reaches full, on top of the charge " +
                        "effect that is already there. It is the only sign that holding longer has " +
                        "stopped buying anything. Empty shows nothing. " +
                        "It appears where ChargeEffectSide and ChargeEffectHeight put it — the " +
                        "same spot as the charge effect, because the two are one signal. " +
                        "Worth trying: vfx_blocked, fx_DvergerMage_Support_hit, " +
                        "fx_lightningstaffprojectile_hit, vfx_HealthUpgrade.",
                        null, ClientSide(48))),

                ChargeFullEffectColor = config.Bind(
                    section, "ChargeFullEffectColor", chargeFullEffectColor,
                    new ConfigDescription(
                        "Colour of the full-charge effect, as #RRGGBB. Empty follows " +
                        "ProjectileColor, like the charge effect does.",
                        null, ClientSide(47))),

                ChargeFullEffectScale = config.Bind(
                    section, "ChargeFullEffectScale", chargeFullEffectScale,
                    new ConfigDescription(
                        "Size of the full-charge effect, 1 being the prefab as it came. " +
                        "It does not grow: the charge is done, and something still growing would " +
                        "say the opposite of what this exists to say.",
                        new AcceptableValueRange<float>(0.1f, 10f), ClientSide(46))),

                // Duas leituras possiveis do mesmo pedido, e so' a tela decide: um estalo no
                // instante em que enche, ou um sinal aceso enquanto o jogador segura. A primeira e'
                // o default porque vfx_blocked e' um prefab de estouro — po-lo em loop pisca.
                ChargeFullEffectLoop = config.Bind(
                    section, "ChargeFullEffectLoop", chargeFullEffectLoop,
                    new ConfigDescription(
                        "Hold the full-charge effect for as long as the player keeps holding, " +
                        "instead of playing it once the moment the charge fills. " +
                        "Off suits a burst prefab such as vfx_blocked, which loops as a flicker. " +
                        "On suits a prefab meant to sit there, and keeps telling the player the " +
                        "charge is done however long they hold — which a one-off flash stops " +
                        "doing a second after it fires.",
                        null, ClientSide(44))),

                // Substituir e' o default porque os dois juntos ficaram ruins na tela — playtest de
                // 2026-09-07. A bola de carregamento diz "enchendo", e ela continuar ali depois de
                // cheia diz a coisa errada; o aviso de carga cheia e' que passa a ser a resposta.
                //
                // Chave e nao regra fixa: qual das duas leituras esta' certa e' julgamento visual,
                // e o codigo nao pode ser o lugar onde ele mora. Desligar devolve os dois somados.
                ChargeFullEffectReplaces = config.Bind(
                    section, "ChargeFullEffectReplaces", chargeFullEffectReplaces,
                    new ConfigDescription(
                        "Take the charge effect away the moment the charge fills, leaving only " +
                        "ChargeFullEffectPrefab. On, the two never share the screen: the ball " +
                        "means 'filling up', and leaving it there once it is full says the " +
                        "opposite of what the full-charge effect is for. Off keeps both. " +
                        "Ignored when ChargeFullEffectPrefab is empty or names a prefab that does " +
                        "not exist — a broken name should cost the polish, not the only sign the " +
                        "player has that a charge is running. " +
                        "It never touches ChargeBallPrefab: the ball is what is about to be " +
                        "thrown, and it belongs on screen right up to the moment it leaves.",
                        null, ClientSide(43))),

                // Prefab do jogo, nao asset novo — a regra de [[Efeitos Visuais]]. Trocar o nome
                // aqui troca o visual inteiro sem recompilar, que e' o ponto de ser config.
                ProjectilePrefab = config.Bind(section, "ProjectilePrefab", projectilePrefab,
                    new ConfigDescription(
                        "Name of the game prefab used as the projectile. It is instantiated from " +
                        "ZNetScene, so it must be a prefab the game has loaded — a name that does " +
                        "not exist logs a warning and fires nothing. " +
                        "The mod strips whatever the prefab brought with it: its own damage, its " +
                        "status effect (no more setting things on fire) and whatever it spawned " +
                        "on impact. Only the visual and the sound are kept. " +
                        "Alternatives worth trying: staff_fireball_projectile, Imp_fireball_projectile, " +
                        "DvergerStaffIce_projectile (blue), charred_fireball_projectile, " +
                        "DvergerStaffFire_clusterbomb_projectile.",
                        null, AdminOnly(75))),

                // O estouro do impacto e' um EffectList do proprio Projectile, separado do que ele
                // INSTANCIA no hit — o mod ja' tirava o segundo e nao tocava no primeiro, e era de
                // la' que vinha a fumaca da bola de fogo. Chave propria e nao parte do prefab
                // porque voo e impacto sao escolhas independentes: da' para querer o voo de um e o
                // estouro de outro.
                ImpactEffect = config.Bind(section, "ImpactEffect", impactEffect,
                    new ConfigDescription(
                        "What plays where the projectile lands. Empty keeps whatever the " +
                        "projectile prefab brought with it — for a fireball, that is a cloud of " +
                        "smoke. 'none' strips it: the shot lands with no beam and no sound, " +
                        "which reads as a miss, so it is more useful for telling the smoke apart " +
                        "from the rest than as a final answer. Anything else is the name of a " +
                        "prefab to play instead; a name that does not exist logs a warning and " +
                        "leaves the prefab's own effect alone. " +
                        "Worth trying: fx_lightningstaffprojectile_hit, fx_DvergerMage_Support_hit, " +
                        "fx_goblinking_beam_hit, fx_greenroots_projectile_hit.",
                        null, ClientSide(74))),

                // O corte fino do impacto. Existe porque o ImpactEffect e' grosso demais para o
                // caso real: o estouro do GoblinShaman tem clarao bom, som bom e uma fumaca que
                // nao combina com tiro de energia — e 'none' leva os tres juntos. Sao filhos do
                // mesmo prefab, entao nome de efeito nenhum separa um do outro; o que separa e'
                // remover o emissor no clone. Ver Util/StrippedEffect.cs.
                //
                // Nome INTEIRO, e nao pedaco de nome: 'fire' por pedaco casaria com
                // fx_shaman_fireball_expl e derrubaria o estouro junto com a chama. Ver a doc do
                // StrippedEffect.Matches.
                ImpactEffectStrip = config.Bind(section, "ImpactEffectStrip", impactEffectStrip,
                    new ConfigDescription(
                        "Comma-separated names of particle emitters to remove from the impact " +
                        "effect, so the good half of it can stay. A game effect is a tree of " +
                        "emitters — the flash, the fire and the smoke are separate objects " +
                        "inside one prefab — and ImpactEffect can only take or leave the whole " +
                        "tree. This removes the emitters named here and keeps the rest, sound " +
                        "included. Empty changes nothing. " +
                        "Names must match in full, case aside: a partial name like 'fire' would " +
                        "also match the effect fx_shaman_fireball_expl that contains it, and take " +
                        "the whole beam with it. " +
                        "A name that matches a whole impact effect drops that effect from the " +
                        "list — which is how a prefab that keeps its smoke in a separate effect " +
                        "is handled. " +
                        "Turn VerboseLogging on and fire once: the log lists every impact effect " +
                        "and the name of every emitter inside it, ready to copy from.",
                        null, ClientSide(73))),

                // A luz do impacto e' a parte do prefab que mais denuncia de onde ele veio: ela
                // pinta o terreno em volta, e o estouro do xama goblin acende ROSA. Nao ha' chave
                // do jogo para isso — o efeito e' instanciado pelo EffectList la' dentro do
                // Projectile e nunca passa pela nossa mao. O que se pinta e' o molde: ver
                // StrippedEffect.Prepare.
                //
                // Vazio segue o ProjectileColor de proposito. Duas chaves de cor para o mesmo tiro
                // sairiam de sincronia no dia em que a bola mudasse de cor.
                ImpactColor = config.Bind(section, "ImpactColor", impactColor,
                    new ConfigDescription(
                        "Colour of the impact effect, as #RRGGBB. Empty follows ProjectileColor, " +
                        "so the beam matches the shot that made it without being set twice. " +
                        "'none' keeps the effect's own colours, whatever the prefab shipped with " +
                        "— which for the goblin shaman beam means a pink light on the ground " +
                        "around the hit. Anything else overrides both.",
                        null, ClientSide(72))),

                // Escolha visual, e por isso config e nao constante: qual das duas leituras esta'
                // certa so' se sabe olhando a tela. Trocar aqui vale no proximo tiro, sem
                // reiniciar — a cor faz parte da chave de cache do template.
                ImpactColorTarget = config.Bind(section, "ImpactColorTarget", ImpactTintTarget.Light,
                    new ConfigDescription(
                        "How much of the impact effect ImpactColor paints. 'Light' repaints only " +
                        "the dynamic light the beam casts on the ground, which is what gives a " +
                        "borrowed prefab away, and leaves the flash and the shockwave drawn the " +
                        "way the game drew them. 'Everything' repaints particles, trails and " +
                        "materials too, so the whole beam reads as the ki that caused it — at " +
                        "the cost of the shading the effect came with.",
                        null, ClientSide(71))),

                // Cosmetico e client-side pelo mesmo motivo que o ImpactEffect: o que morre aqui
                // e' a copia local do projetil, no cliente de quem atirou.
                ProjectileLingerOnHit = config.Bind(section, "ProjectileLingerOnHit", false,
                    new ConfigDescription(
                        "Let the projectile survive its own impact, the way the prefab wanted. " +
                        "Prefabs meant for arrows use this to stick into the wall they hit, and a " +
                        "prefab with a particle trail uses it to keep trailing after it lands — " +
                        "which reads as a puff of smoke sitting where the shot went off, seconds " +
                        "after the beam is over. Off, the shot is gone the instant it connects " +
                        "and only the impact effect is left. Turn it on to check whether lingering " +
                        "smoke is coming from the projectile or from ImpactEffect.",
                        null, ClientSide(70))),

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

                ProjectileGravity = config.Bind(section, "ProjectileGravity", 0f,
                    new ConfigDescription(
                        "Gravity pulling the projectile down. 0 flies dead straight, which is what " +
                        "reads as energy rather than as a thrown rock. Raise it for an arc.",
                        new AcceptableValueRange<float>(0f, 20f), AdminOnly(60))),

                ProjectileScale = config.Bind(section, "ProjectileScale", projectileScale,
                    new ConfigDescription(
                        "Size of the projectile, 1 being the prefab as it came. " +
                        "Visual only: it does NOT change what the projectile hits.",
                        new AcceptableValueRange<float>(0.1f, 10f), ClientSide(55))),

                // Cosmetico, entao ClientSide como a cor da aura. Vazio de proposito: o primeiro
                // playtest deve ver o prefab como ele e', antes de decidir que cor o ki tem.
                // Amarelo desde o playtest de 2026-08-20 — antes era vazio, "o prefab manda", que
                // era a posicao certa enquanto nao se sabia que cor o ki tinha. Agora se sabe, e
                // deixar vazio faria a instalacao limpa de outro jogador nascer com a cor do prefab
                // emprestado (verde de raiz, laranja de fogo) em vez da do mod.
                //
                // O ImpactColor pendura nele: vazio la' significa "a cor deste tiro".
                ProjectileColor = config.Bind(section, "ProjectileColor", projectileColor,
                    new ConfigDescription(
                        "Projectile color, #RRGGBB format. Empty keeps the prefab's own colors. " +
                        "Tinting touches particles, lights and this clone's own materials only — " +
                        "never the game's shared assets. " +
                        "The impact beam follows this colour unless ImpactColor says otherwise.",
                        null, ClientSide(50))),

                // Cosmetico, entao ClientSide — mas note que ele replica: o ZSyncAnimation.SetTrigger
                // manda RPC para todo mundo, entao os amigos veem a pose de quem atirou mesmo com
                // .cfg diferente. Mesmo padrao da cor de cabelo.
                //
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

        /// <summary>Entrada imposta pelo servidor no multiplayer (etapa 8).</summary>
        private static ConfigurationManagerAttributes AdminOnly(int order) =>
            new ConfigurationManagerAttributes { IsAdminOnly = true, Order = order };

        /// <summary>Entrada local de cada jogador; o servidor não interfere.</summary>
        private static ConfigurationManagerAttributes ClientSide(int order) =>
            new ConfigurationManagerAttributes { IsAdminOnly = false, Order = order };
    }
}
