using BepInEx.Configuration;
using UnityEngine;

namespace Saiyaheim
{
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

        private const string SecFlight = "5 - Flight";
        private const string SecPower = "6 - Power Level";
        private const string SecPowerSkill = "6.1 - Battle Power";
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

        /// <summary>Ki máximo somado por nível de Battle Power.</summary>
        public static ConfigEntry<float> MaxKiPerPowerLevel { get; private set; }

        public static ConfigEntry<float> KiRegenPerSecond { get; private set; }

        /// <summary>Regeneração passiva somada por ponto de power level. Mantém a torneira crescendo junto com a barra.</summary>
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

        /// <summary>Carregamento ativo somado por ponto de power level.</summary>
        public static ConfigEntry<float> ChargeKiFromPower { get; private set; }

        /// <summary>Se true, andar interrompe o carregamento.</summary>
        public static ConfigEntry<bool> ChargeRequiresStandingStill { get; private set; }

        // ---------- 2.1 - Combat ----------

        /// <summary>Ki gasto por ponto de dano que o poder somou ao soco. Ki insuficiente não cancela o golpe, só tira o bônus.</summary>
        public static ConfigEntry<float> PunchKiCostPerDamage { get; private set; }

        /// <summary>
        /// Taxa do desconto hiperbólico que o poder de combate dá nos <b>três</b> custos de ki do
        /// combate: soco, dano recebido e bloqueio. 0 desliga. Ver <c>PowerLevel.KiCostFactorFor</c>.
        /// </summary>
        public static ConfigEntry<float> KiCostPowerReduction { get; private set; }

        /// <summary>Fração do power level somada ao dano do soco.</summary>
        public static ConfigEntry<float> PunchDamageFromPower { get; private set; }

        /// <summary>Armadura garantida com o ki ligado, antes da parcela vinda do poder.</summary>
        public static ConfigEntry<float> ArmorBase { get; private set; }

        /// <summary>Fração da armadura de ki que sobra com a barra zerada. Degrau, não degradê.</summary>
        public static ConfigEntry<float> ArmorFractionWithoutKi { get; private set; }

        /// <summary>Ki gasto por ponto de dano que a armadura de ki absorveu.</summary>
        public static ConfigEntry<float> DamageTakenKiCost { get; private set; }

        /// <summary>Fração do power level convertida em armadura.</summary>
        public static ConfigEntry<float> ArmorFromPower { get; private set; }

        /// <summary>Block power garantido com o ki ligado, antes da parcela vinda do poder.</summary>
        public static ConfigEntry<float> BlockPowerBase { get; private set; }

        /// <summary>Fração do power level convertida em block power. Substitui o do item equipado.</summary>
        public static ConfigEntry<float> BlockPowerFromPower { get; private set; }

        /// <summary>Ki gasto por ponto de dano que o bloqueio de ki barrou.</summary>
        public static ConfigEntry<float> BlockKiCost { get; private set; }

        // ---------- 7 - HUD ----------

        public static ConfigEntry<bool> ShowKiBar { get; private set; }
        public static ConfigEntry<float> KiBarOffsetX { get; private set; }
        public static ConfigEntry<float> KiBarOffsetY { get; private set; }
        public static ConfigEntry<string> KiBarColor { get; private set; }
        public static ConfigEntry<bool> KiBarAlwaysVisible { get; private set; }

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
            /// <summary>Multiplicador do power level de combate enquanto a forma está ativa.</summary>
            public ConfigEntry<float> PowerMultiplier { get; internal set; }

            /// <summary>Dreno base por segundo, antes da redução por maestria.</summary>
            public ConfigEntry<float> KiDrainPerSecond { get; internal set; }

            /// <summary>
            /// Fração do dano de contusão do soco convertida em corte enquanto a forma está ativa.
            /// Converte, não soma: o total do golpe não muda.
            /// </summary>
            public ConfigEntry<float> PunchSlashFraction { get; internal set; }

            /// <summary>
            /// Peso máximo somado ao limite do inventário enquanto a forma está ativa. Soma ao
            /// limite base do jogador, e ao Megingjord se ele estiver equipado.
            /// </summary>
            public ConfigEntry<float> CarryWeightBonus { get; internal set; }

            /// <summary>Fração do dreno removida no nível 100 da skill desta forma.</summary>
            public ConfigEntry<float> MasteryDrainReduction { get; internal set; }

            /// <summary>XP da skill desta forma por segundo transformado.</summary>
            public ConfigEntry<float> MasteryXpPerSecond { get; internal set; }

            /// <summary>Nível mínimo de Battle Power para entrar na forma. 0 desliga a trava.</summary>
            public ConfigEntry<float> MinBattlePower { get; internal set; }

            /// <summary>
            /// Global key do boss que destrava a forma. Vazio desliga a trava. Ver
            /// <c>Util.BossGate</c>.
            /// </summary>
            public ConfigEntry<string> RequiredGlobalKey { get; internal set; }

            /// <summary>Cor do cabelo enquanto a forma está ativa, em #RRGGBB. Vazio não pinta.</summary>
            public ConfigEntry<string> HairColor { get; internal set; }

            /// <summary>Multiplicador de brilho da cor acima. Acima de 1 estoura e queima.</summary>
            public ConfigEntry<float> HairColorIntensity { get; internal set; }

            /// <summary>Cor da aura desta forma, em #RRGGBB. Vazio mantém a cor do prefab.</summary>
            public ConfigEntry<string> AuraColor { get; internal set; }
        }

        /// <summary>
        /// O primeiro degrau da escada. O segundo (SSJ2) é outra propriedade como esta, com seção
        /// própria — ver <see cref="BindTransformation"/>.
        /// </summary>
        public static TransformationConfig Ssj { get; private set; }

        // ---------- 4.x - Ki Attacks ----------

        /// <summary>
        /// Tempo mínimo entre dois disparos, <b>qualquer que seja o ataque</b>. O cooldown de cada
        /// ataque é dele; este é o piso comum, e existe para que trocar de ataque não seja um jeito
        /// de burlar cooldown.
        /// </summary>
        public static ConfigEntry<float> KiAttackMinimumInterval { get; private set; }

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
            /// <summary>Dano no power level zero. O piso do ataque, antes da parcela do poder.</summary>
            public ConfigEntry<float> DamageBase { get; internal set; }

            /// <summary>Fração do power level de combate somada ao dano.</summary>
            public ConfigEntry<float> DamageFromPower { get; internal set; }

            /// <summary>Ki gasto por disparo. Fixo: não escala com nada, de propósito.</summary>
            public ConfigEntry<float> KiCost { get; internal set; }

            /// <summary>Segundos até este ataque poder ser disparado de novo.</summary>
            public ConfigEntry<float> Cooldown { get; internal set; }

            /// <summary>Empurrão no alvo atingido.</summary>
            public ConfigEntry<float> Knockback { get; internal set; }

            /// <summary>Prefab do projétil, do <c>ZNetScene</c>. Ver [[Prefabs do Jogo]].</summary>
            public ConfigEntry<string> ProjectilePrefab { get; internal set; }

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

            /// <summary>Nível mínimo de Battle Power para usar o ataque. 0 desliga a trava.</summary>
            public ConfigEntry<float> MinBattlePower { get; internal set; }

            /// <summary>Global key do boss que destrava o ataque. Vazio desliga a trava.</summary>
            public ConfigEntry<string> RequiredGlobalKey { get; internal set; }
        }

        /// <summary>
        /// O primeiro ataque da escada. O segundo é outra propriedade como esta, com seção própria
        /// — ver <see cref="BindKiAttack"/>.
        /// </summary>
        public static KiAttackConfig KiBlast { get; private set; }

        // ---------- 5 - Flight ----------

        public static ConfigEntry<float> FlightKiPerSecond { get; private set; }

        /// <summary>Multiplicador do custo de ki com o botão de correr segurado.</summary>
        public static ConfigEntry<float> FlightFastKiMultiplier { get; private set; }

        /// <summary>Multiplicador do custo de ki parado no ar, sem nenhum input de movimento.</summary>
        public static ConfigEntry<float> FlightHoverKiMultiplier { get; private set; }

        public static ConfigEntry<float> FlightBaseSpeed { get; private set; }

        /// <summary>Velocidade somada por ponto de power level bruto.</summary>
        public static ConfigEntry<float> FlightSpeedFromPower { get; private set; }

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

        /// <summary>Nível mínimo de Battle Power para decolar. 0 desliga a trava.</summary>
        public static ConfigEntry<float> FlightMinBattlePower { get; private set; }

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

        // ---------- 6 - Power Level ----------

        public static ConfigEntry<float> PowerK1Health { get; private set; }

        /// <summary>Só entra na fórmula do ki desligado — com ki não há arma equipada.</summary>
        public static ConfigEntry<float> PowerK2WeaponDamage { get; private set; }

        /// <summary>Só entra na fórmula do ki desligado — com ki a armadura é saída, não entrada.</summary>
        public static ConfigEntry<float> PowerK3Armor { get; private set; }

        /// <summary>Peso do nível de Battle Power. Só entra na fórmula do ki ligado.</summary>
        public static ConfigEntry<float> PowerK4PowerSkill { get; private set; }

        /// <summary>
        /// Poder que o termo de fim de jogo entrega no nível 100. 0 desliga o termo e devolve a
        /// fórmula linear original. Só afeta combate — ver <see cref="Power.PowerLevel"/>.
        /// </summary>
        public static ConfigEntry<float> PowerK5LateGame { get; private set; }

        /// <summary>Quão tarde o termo de fim de jogo acorda. Maior = mais concentrado no topo.</summary>
        public static ConfigEntry<float> PowerLateGameExponent { get; private set; }

        // ---------- 6.1 - Battle Power ----------

        public static ConfigEntry<float> SkillXpPerDamageDealt { get; private set; }
        public static ConfigEntry<float> SkillXpPerDamageTaken { get; private set; }

        /// <summary>XP extra no peso máximo de inventário. 1.0 = dobra.</summary>
        public static ConfigEntry<float> SkillXpWeightBonus { get; private set; }

        /// <summary>Trava de segurança: XP máximo de um único golpe.</summary>
        public static ConfigEntry<float> SkillXpMaxPerEvent { get; private set; }

        /// <summary>
        /// Expoente da compressão aplicada ao power level bruto antes de exibir.
        /// 0.5 = raiz quadrada. Menor comprime mais.
        /// </summary>
        public static ConfigEntry<float> PowerCompressionExponent { get; private set; }

        /// <summary>Multiplicador aplicado depois da compressão, só para o número exibido ficar legível.</summary>
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
        public static ConfigEntry<float> BlastPoseAimFollow { get; private set; }
        public static ConfigEntry<float> BlastPoseArmTwist { get; private set; }
        public static ConfigEntry<float> BlastPoseElbowBend { get; private set; }
        public static ConfigEntry<float> BlastPoseShoulderPush { get; private set; }
        public static ConfigEntry<float> BlastPoseShoulderLift { get; private set; }
        public static ConfigEntry<float> BlastPoseTorsoTwist { get; private set; }
        public static ConfigEntry<float> BlastPoseHandOpen { get; private set; }
        public static ConfigEntry<float> BlastPoseWristBend { get; private set; }

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
                    "no damage bonus, no mastery accumulating, no ki term in the power level.",
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
                    "Maximum ki at level 0 of the Battle Power skill. The cap grows from here — " +
                    "see MaxKiPerPowerLevel. With a fixed cap the bar would be the same size from " +
                    "the first boss to the last and progression would never show up on the HUD. " +
                    "(Playtest value, 2026-07-31. Still being tuned.)",
                    new AcceptableValueRange<float>(10f, 10000f), AdminOnly(100)));

            MaxKiPerPowerLevel = config.Bind(SecKi, "MaxKiPerPowerLevel", 3f,
                new ConfigDescription(
                    "Maximum ki added per level of Battle Power. With the default, level 100 " +
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

            // Escala pelo power level DERIVADO, nao pelo nivel da skill: e a mesma base do
            // FlightSpeedFromPower, entao comer melhor recarrega mais rapido do mesmo jeito que ja
            // faz voar mais rapido. Trocar para PowerSkill.GetLevel e uma linha, se o playtest
            // disser que a volatilidade da comida incomoda.
            KiRegenFromPower = config.Bind(SecKi, "KiRegenFromPower", 0.0075f,
                new ConfigDescription(
                    "Ki per second ADDED to the passive regeneration for each point of raw power " +
                    "level. Exists because the bar grows with power (MaxKiPerPowerLevel) and a " +
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
                    "has to keep up with a tap that already grows with power level. " +
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
                    "Ki per second ADDED to active charging for each point of raw power level. " +
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
                    "Ki consumed per point of damage the power level ADDED to the punch — the " +
                    "mirror of DamageTakenKiCost, which charges per point the ki armor absorbed. " +
                    "Both measure the service ki rendered, so the cost scales with the payoff " +
                    "instead of aging into irrelevance. The cost is therefore " +
                    "PunchDamageFromPower * power level * this, and the vanilla unarmed base " +
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
                    "How much the combat power level makes the three COMBAT ki costs cheaper — " +
                    "punching (PunchKiCostPerDamage), taking hits (DamageTakenKiCost) and blocking " +
                    "(BlockKiCost) — as 1 / (1 + this * combat power). 0 disables the discount and " +
                    "the costs stay strictly proportional to what ki delivered. " +
                    "Why it exists: all three costs come from the combat power level, which has no " +
                    "ceiling — the late-game term grows forever and a transformation multiplies it " +
                    "— while the ki bar comes from the Battle Power SKILL level, which stops at " +
                    "100. Without this, a punch eventually costs more than a full bar and lands " +
                    "with raw vanilla damage, and blocking drains the bar in two hits. \n" +
                    "The shape matters: each cost approaches its own rate divided by this and " +
                    "never passes it, so actions per bar settles instead of falling to zero. For " +
                    "the punch at 0.01 that ceiling is 15 ki, so a full bar always buys a long " +
                    "fight no matter how far the power level runs. \n" +
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

            PunchDamageFromPower = config.Bind(SecCombat, "PunchDamageFromPower", 0.05f,
                new ConfigDescription(
                    "Fraction of the power level ADDED to punch damage. Additive, not multiplicative: " +
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
            ArmorFromPower = config.Bind(SecCombat, "ArmorFromPower", 0.06f,
                new ConfigDescription(
                    "Fraction of the power level converted into armor. While ki is on this armor " +
                    "REPLACES equipment armor — worn pieces stop counting. Turning ki off gives " +
                    "vanilla armor back immediately. " +
                    "Reads the COMBAT power level, so it grows with K5_LateGameBonus — that is why " +
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
                    "The 2 is the vanilla unarmed value, kept as a floor so turning ki on at power " +
                    "level zero never makes blocking WORSE than vanilla. " +
                    "Same role ArmorBase plays for armor, with one difference: unlike armor, this " +
                    "SURVIVES an empty ki bar (ArmorFractionWithoutKi only scales the power-derived " +
                    "share). Zero armor is a legal value; zero block power is a division by zero " +
                    "inside Humanoid.BlockAttack that turns your stamina into NaN permanently. " +
                    "Leave this above zero.",
                    new AcceptableValueRange<float>(0f, 200f), AdminOnly(58)));

            // Ancorado na tabela do `saiya_block shields`, com o poder bruto indo de ~61 (skill 10)
            // a ~327 (skill 100):
            //   raw  61 -> 15.5   abaixo do ShieldWood (18.5), o pior escudo do jogo
            //   raw 150 -> 35     entre ShieldBronzeBuckler (28.7) e ShieldIronBuckler (41)
            //   raw 327 -> 74     ShieldSerpentscale (73.8), longe do ShieldFlametalTower (155.8)
            // Deliberadamente ABAIXO da escada de escudos: o punho ja ganha em nao quebrar, nao
            // ocupar a mao e escalar sozinho.
            BlockPowerFromPower = config.Bind(SecCombat, "BlockPowerFromPower", 0.22f,
                new ConfigDescription(
                    "Fraction of the power level converted into block power. While ki is on this " +
                    "REPLACES the blocker item's value — holding a shield changes nothing, exactly " +
                    "like ArmorFromPower replaces equipment armor. Turning ki off gives the shield " +
                    "back immediately. " +
                    "Calibrated to sit BELOW the vanilla shield ladder: at power skill 10 it lands " +
                    "under the wood shield, at 100 around the serpentscale, never near flametal. " +
                    "Run 'saiya_block shields' for the table and 'saiya_block <damage>' to see " +
                    "what a given hit does. " +
                    "(Starting value, 2026-08-01. Not playtested yet.)",
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
                    "damage than armor absorbs, so it should be a burst, not a stance. Lower it if " +
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
                // Calibrado no playtest de 2026-08-16. Saiu em 300 — o limite base inteiro do
                // Valheim, ou seja, mochila dobrada — e desceu para 100 na primeira sessao.
                carryWeightBonus: 100f,
                hairColor: "#FFE14A",
                requiredGlobalKey: "defeated_eikthyr");

            // --- Ataques de ki ---
            KiAttackMinimumInterval = config.Bind(SecKiAttacks, "MinimumInterval", 0.2f,
                new ConfigDescription(
                    "Minimum seconds between two ki attacks, whatever they are. Each attack has " +
                    "its own Cooldown; this is the shared floor, and it exists so that switching " +
                    "attacks is not a way around a cooldown.",
                    new AcceptableValueRange<float>(0f, 5f), AdminOnly(100)));

            // O primeiro degrau, atras do Eikthyr — a MESMA chave do SSJ, de proposito: matar o
            // primeiro boss entrega a forma e o ataque de uma vez, e vira um marco grande em vez de
            // dois mornos. Espacar custaria mexer numa trava de forma ja calibrada em playtest.
            //
            // Adicionar o ataque seguinte e' repetir esta chamada com outra secao, outros numeros e
            // a global key do boss dele.
            // Os quatro numeros de partida, ancorados no soco em vez de chutados no vazio:
            //   dano por poder 0,04 contra os 0,05 do PunchDamageFromPower — o tiro bate um pouco
            //   MENOS por acerto que o soco, que e' o que compra o direito de ser a distancia.
            //   Custo 20 fixo: cedo o tiro e' caro, e vai ficando barato conforme a barra cresce e
            //   este numero nao. Comecou em 8, ancorado nos ~3,75 de um soco no comeco do jogo, e o
            //   playtest de 2026-08-07 subiu para 20 — 2,5x. Isso NAO responde a pergunta do fim de
            //   jogo, so a atrasa: ver [[Em Aberto]].
            // O saiya_blast imprime dano/ki dos dois lado a lado — e' por ali que a calibracao sai.
            KiBlast = BindKiAttack(config, SecKiBlast,
                damageBase: 8f,
                damageFromPower: 0.04f,
                kiCost: 20f,
                cooldown: 0.5f,
                projectilePrefab: "DvergerStaffFire_fireball_projectile",
                requiredGlobalKey: "defeated_eikthyr");

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
                    "Flight speed floor: skill 0, power level 0, carrying nothing. " +
                    "Everything else is added or multiplied on top of this. " +
                    "CAREFUL: the flight skill bonus MULTIPLIES this floor, so lowering it also " +
                    "shrinks what levelling the skill is worth — at 2, a hundred levels of Flight " +
                    "buy +1 m/s on a fresh character. That is intended here: almost all of the " +
                    "speed is meant to come from SpeedFromPower. " +
                    "(Playtest value, 2026-07-31. Started at 15, went to 10, landed on 2.)",
                    new AcceptableValueRange<float>(1f, 100f), AdminOnly(90)));

            FlightSpeedFromPower = config.Bind(SecFlight, "SpeedFromPower", 0.015f,
                new ConfigDescription(
                    "Speed ADDED per point of raw power level. With ki on, power is " +
                    "k1*HP + k4*BattlePower — so eating better and fighting more both make you " +
                    "fly faster, which is the Dragon Ball reading of getting stronger. " +
                    "Additive, like punch damage: enemy scaling is roughly linear across biomes " +
                    "and an additive stat tracks that predictably. " +
                    "Run saiya_fly to see how much this is contributing right now.",
                    new AcceptableValueRange<float>(0f, 1f), AdminOnly(88)));

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
            FlightKiPowerReduction = config.Bind(SecFlight, "KiPowerReduction", 0.0033f,
                new ConfigDescription(
                    "How much the late-game power term (Power Level.K5_LateGameBonus) cheapens " +
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

            FlightMinBattlePower = config.Bind(SecFlight, "MinBattlePower", 0f,
                new ConfigDescription(
                    "Minimum Battle Power level required to take off. 0 disables the gate. " +
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

            // --- Power Level ---
            // Sao DUAS formulas, porque os dois caminhos de progressao sao disjuntos:
            //   ki desligado: poder = k1*HP + k2*dano_arma + k3*armadura
            //   ki ligado:    poder = k1*HP + k4*nivel_battle_power
            // Arma e armadura nao sobrevivem ao modo ki: arma da zero (o jogador soca) e
            // armadura vira laco de realimentacao, porque ela passou a ser DERIVADA do poder.
            PowerK1Health = config.Bind(SecPower, "K1_Health", 1f,
                new ConfigDescription(
                    "Weight of HP in the power level. Applies to both formulas. Only HP ABOVE the " +
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
                    "with ki on, armor is an output of the power level, not an input.",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(80)));

            PowerK4PowerSkill = config.Bind(SecPower, "K4_PowerSkill", 3f,
                new ConfigDescription(
                    "Weight of the Battle Power skill level (0-100). Only used in the ki-ON formula. " +
                    "It is the only axis of progression for a ki user — without it, power would be " +
                    "constant from the first boss to the last.",
                    new AcceptableValueRange<float>(0f, 1000f), AdminOnly(70)));

            // Somado ao K4, nao multiplicado nele. Um expoente sobre a parcela da skill so
            // redistribui um total fixo: para render mais no fim ele tira do meio, e o mid-game
            // fica mais fraco do que ja e. Somando um termo separado, o K4 de hoje continua
            // intocado e o novo so pesa onde o grind aperta.
            PowerK5LateGame = config.Bind(SecPower, "K5_LateGameBonus", 3f,
                new ConfigDescription(
                    "Power delivered by the late-game term AT LEVEL 100, on top of K4. It exists " +
                    "because levelling gets brutally more expensive but the reward did not: going " +
                    "from 99 to 100 costs a THOUSAND times what 0 to 1 costs (Valheim's curve is " +
                    "(level+1)^1.5), yet power rose a flat K4 every level. This term makes each " +
                    "level worth more than the one before it. " +
                    "AFFECTS COMBAT ONLY: punch damage, armor, block power and the displayed " +
                    "number. Flight speed and the ki cap deliberately ignore it — see " +
                    "Flight.KiPowerReduction for what late game buys in the air. " +
                    "0 turns the term off and restores the original linear formula exactly.",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(65)));

            PowerLateGameExponent = config.Bind(SecPower, "LateGameExponent", 5f,
                new ConfigDescription(
                    "How LATE the K5 term wakes up. The term is normalised on level 100, so this " +
                    "never changes what it delivers at the top — only how much of it arrives " +
                    "early. At 5, level 50 has just 3% of the bonus and level 75 has 24%: almost " +
                    "all of it lands in the last quarter, which is exactly the stretch that costs " +
                    "half the total grind. Lower spreads it out, higher concentrates it further.",
                    new AcceptableValueRange<float>(1f, 10f), AdminOnly(64)));

            PowerCompressionExponent = config.Bind(SecPower, "CompressionExponent", 0.5f,
                new ConfigDescription(
                    "Exponent of the compression applied to the raw power level before display. " +
                    "0.5 = square root, 1.0 = no compression. It exists so the number does not " +
                    "become huge and meaningless too early.",
                    new AcceptableValueRange<float>(0.1f, 1f), AdminOnly(60)));

            PowerDisplayScale = config.Bind(SecPower, "DisplayScale", 100f,
                new ConfigDescription("Multiplier applied after the compression, purely for readability.",
                    new AcceptableValueRange<float>(1f, 10000f), AdminOnly(50)));

            // --- Battle Power ---
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
                    "a quick burst; without this the effect disappears on its own after a second. " +
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
                    "How far the elbows are bent. 0 is a straight arm, 1 is the tightest bend the " +
                    "rig allows. " +
                    "(Playtest value, 2026-08-07. Started at 0.7 — a tight bend that belonged to " +
                    "the fists-at-the-hips version of this pose.)",
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
            // Mesma ordem decrescente da secao acima, e a mesma logica de calibragem: o
            // interruptor, o envelope, e depois os grupos de cima para baixo. So o braco comeca
            // ligado — e' o pedido inteiro ("apontar a mao direita para frente"); o resto sobe
            // um de cada vez.
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

            BlastPoseHoldSeconds = config.Bind(SecBlastPose, "HoldSeconds", 0.18f,
                new ConfigDescription(
                    "Seconds the arm stays out at full extension before relaxing. Firing again " +
                    "before it ends just pushes this deadline forward — the arm does not drop and " +
                    "snap back between shots of a burst.",
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
                    "this case). Same for every other weight below. This is the one group that " +
                    "starts ON, because it is the whole request.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(196)));

            BlastPoseShoulderWeight = config.Bind(SecBlastPose, "ShoulderWeight", 0f,
                new ConfigDescription(
                    "How much of the right SHOULDER the pose owns. This is what turns 'arm raised' " +
                    "into 'arm extended' — without it the reach stops at the shoulder socket and " +
                    "the character looks like he is pointing rather than pushing. Turn it on " +
                    "second, right after the arm.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(195)));

            BlastPoseTorsoWeight = config.Bind(SecBlastPose, "TorsoWeight", 0f,
                new ConfigDescription(
                    "How much of the TORSO TWIST the pose owns — chest and upper chest bringing " +
                    "the right shoulder around to follow the arm. Turn it on third. Zero leaves " +
                    "the torso to the animation, including whatever twist the game's aiming does.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(194)));

            BlastPoseSpineTwistWeight = config.Bind(SecBlastPose, "SpineTwistWeight", 0f,
                new ConfigDescription(
                    "How much of the twist reaches the LOWER BACK, as a fraction of TorsoWeight. " +
                    "Off by default and it should probably stay that way: in the Valheim rig this " +
                    "joint drags the hips along, so twisting it turns the whole character away " +
                    "from where he is aiming. It is the same joint the charging pose and the " +
                    "flight pose both keep out for the same reason.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(193)));

            BlastPoseArmForward = config.Bind(SecBlastPose, "ArmForward", 0.9f,
                new ConfigDescription(
                    "How far the upper arm swings FORWARD. This is the gesture: at 0 the arm is " +
                    "out to the side, at 1 it points straight ahead. Negative pulls it behind the " +
                    "ribs, which is the charging pose, not this one.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(190)));

            BlastPoseArmHeight = config.Bind(SecBlastPose, "ArmHeight", 0f,
                new ConfigDescription(
                    "Where the upper arm sits vertically. Raw muscle space, not intent: 0 is a " +
                    "T-pose, meaning the arm is horizontal — which is already shoulder height, so " +
                    "0 is the right starting point for aiming straight ahead. About -0.65 is the " +
                    "arm hanging down. Positive raises it above the shoulder.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(189)));

            BlastPoseAimFollow = config.Bind(SecBlastPose, "AimFollow", 0.5f,
                new ConfigDescription(
                    "How much the arm follows where you are LOOKING, added on top of ArmHeight. " +
                    "This matters more than it sounds: the projectile spawns at the right hand and " +
                    "flies along your look direction, so with a locked arm, aiming at the sky " +
                    "sends the shot upward out of a hand that is pointing at the horizon. 1 is " +
                    "full follow, 0 pins the arm to ArmHeight.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(188)));

            BlastPoseArmTwist = config.Bind(SecBlastPose, "ArmTwist", 0f,
                new ConfigDescription(
                    "Rotation of the upper arm along its own length. Raw muscle space. With the " +
                    "elbow straight it barely changes the silhouette, but it decides which way the " +
                    "PALM faces — and the ki ball is born in that hand. Change this if the back of " +
                    "the hand ends up facing the target.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(187)));

            BlastPoseElbowBend = config.Bind(SecBlastPose, "ElbowBend", 0f,
                new ConfigDescription(
                    "How far the elbow is bent. Raw muscle space: 0 is a straight arm, which is " +
                    "what the pose asks for, and 1 is the tightest bend the rig allows. Raise it " +
                    "slightly if a fully locked elbow reads as stiff.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(186)));

            BlastPoseShoulderPush = config.Bind(SecBlastPose, "ShoulderPush", 0.5f,
                new ConfigDescription(
                    "How far the right shoulder is pushed FORWARD, when ShoulderWeight is on. This " +
                    "is the extra reach. Negative pulls it back.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(185)));

            BlastPoseShoulderLift = config.Bind(SecBlastPose, "ShoulderLift", 0.1f,
                new ConfigDescription(
                    "How far the right shoulder rides UP, when ShoulderWeight is on. Raw muscle " +
                    "space. Small: a shrugged shoulder on an extended arm reads as flinching.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(184)));

            BlastPoseTorsoTwist = config.Bind(SecBlastPose, "TorsoTwist", 0.3f,
                new ConfigDescription(
                    "How far the torso rotates to bring the right shoulder AROUND, when " +
                    "TorsoWeight is on. Split across the three spine joints, most at the top and " +
                    "least at the bottom. Keep it small — the point is the torso following the " +
                    "arm, not the character turning sideways to his own aim. Negative twists the " +
                    "other way, which would pull the firing shoulder back.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(183)));

            BlastPoseHandOpen = config.Bind(SecBlastPose, "HandOpen", 0f,
                new ConfigDescription(
                    "How far the right hand opens into a flat palm, independently of ArmWeight — " +
                    "the exact opposite of the charging pose's FistClench, and the contrast is the " +
                    "point: charging is a closed fist, firing is an open palm. Zero leaves the " +
                    "hand as the animation had it. Does nothing if the player rig has no mapped " +
                    "finger bones — no error, just no palm.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(182)));

            BlastPoseWristBend = config.Bind(SecBlastPose, "WristBend", 0f,
                new ConfigDescription(
                    "How far the wrist bends back, so the palm faces where the shot is going. Raw " +
                    "muscle space, and it rides on HandOpen rather than having a weight of its " +
                    "own: a pushed palm and an open hand are one gesture. Negative curls the wrist " +
                    "the other way.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(181)));

            // Mesmo emote do carregamento, mas de disparo unico: carregar segura a pose, transformar
            // e' um estouro. O grito replica sozinho pela ZDO — os amigos veem e ouvem.
            TransformEmote = config.Bind(SecEffects, "TransformEmote", "roar",
                new ConfigDescription(
                    "One-shot emote played when you power up into a form. Empty disables it. " +
                    "Not played when stepping DOWN a form: coming down is relief, not a burst. " +
                    "Any emote the player Animator knows works — the same names the /emote chat " +
                    "command lists.",
                    null, ClientSide(55)));

            // Mesmo prefab do carregamento de ki de proposito: ele ja se provou legivel preso ao
            // jogador, e a cor e' quem separa os dois estados — azul carregando, a cor da forma
            // transformado. A cor NAO fica aqui: e' por forma, na secao de cada uma.
            TransformAuraPrefab = config.Bind(SecEffects, "TransformAuraPrefab",
                "fx_DvergerMage_Support_start",
                new ConfigDescription(
                    "Effect burst when you power up into a form. Empty disables it. " +
                    "It fires once and fades — see TransformAuraForceLoop for why it is not kept " +
                    "alive while the form lasts. Not played when stepping DOWN a form, same as " +
                    "the emote. The color comes from each form's own AuraColor, not from here. " +
                    "Alternatives: fx_goblinking_nova, fx_ShieldCharge_1 through _5 " +
                    "(increasing), DvergerStaffNova_aoe.",
                    null, ClientSide(50)));

            TransformAuraScale = config.Bind(SecEffects, "TransformAuraScale", 2.5f,
                new ConfigDescription(
                    "Scale of the burst. Slightly larger than the charging effect on purpose: " +
                    "transforming should read bigger than charging up to it.",
                    new AcceptableValueRange<float>(0.1f, 5f), ClientSide(45)));

            // A duracao e' imposta por nos, nao herdada do prefab: prefab de efeito sustentado ja
            // vem com as particulas em loop, e o TimedDestruction dele so dispara sozinho se o
            // prefab marcou m_triggerOnAwake. Confiar nos dois foi o que deixou o efeito aceso a
            // forma inteira (2026-08-02).
            TransformAuraDuration = config.Bind(SecEffects, "TransformAuraDuration", 2f,
                new ConfigDescription(
                    "How long the burst lasts, in seconds, before it is removed from the player. " +
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
                    "result, not an oversight: game prefabs are built for a half-second burst, " +
                    "and looping one does not make it last longer — it makes it a permanent " +
                    "cloud stuck to the player. The particles never get to disperse. " +
                    "Turning this on with a prefab designed for a sustained aura is fine; " +
                    "turning it on with a burst prefab is what produced the smoke. " +
                    "(Playtest value, 2026-08-02.)",
                    null, ClientSide(40)));

            // 1 (nao mexe) porque o efeito voltou a ser um estouro: luz num flash de meio segundo
            // e' justamente o que da' o baque. A chave existe para quem ligar o ForceLoop, onde
            // luz presa ao jogador por minutos vira lanterna iluminando o terreno em volta.
            TransformAuraLightIntensity = config.Bind(SecEffects, "TransformAuraLightIntensity", 1f,
                new ConfigDescription(
                    "Multiplier for the effect's dynamic light. 1 leaves the prefab as it came, " +
                    "which is right for a burst — the flash is most of the punch. " +
                    "0 removes the light entirely and keeps only the particles. " +
                    "That matters if you turn TransformAuraForceLoop on: a light that follows " +
                    "you for minutes lights up the terrain around you and gets tiring, while " +
                    "the particles glow on their own and do not need it.",
                    new AcceptableValueRange<float>(0f, 2f), ClientSide(38)));

            // --- Debug ---
            ShowRemotePoses = config.Bind(SecMultiplayer, "ShowRemotePoses", true,
                new ConfigDescription(
                    "Draw the mod poses (flight, ki charge, ki blast) on other players.",
                    null, ClientSide(0)));

            ShowRemoteEffects = config.Bind(SecMultiplayer, "ShowRemoteEffects", true,
                new ConfigDescription(
                    "Draw the mod effects (transformation burst, ki charge glow) on other players.",
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
            float punchSlashFraction, float carryWeightBonus, string hairColor,
            string requiredGlobalKey)
        {
            return new TransformationConfig
            {
                // Multiplica o poder de COMBATE (soco, armadura, block power, numero exibido) e a
                // velocidade de voo. Teto de ki, regeneracao e carga ficam de fora de proposito:
                // se a barra crescesse ao transformar ela daria um pulo na tela, e a regeneracao
                // escalada compensaria parte do proprio dreno — a forma se pagando sozinha.
                PowerMultiplier = config.Bind(section, "PowerMultiplier", powerMultiplier,
                    new ConfigDescription(
                        "Multiplies the COMBAT power level while this form is active — punch " +
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
                        "grows with Battle Power (MaxKiPerPowerLevel) — so the form lasts longer " +
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
                        "It applies to the whole punch — vanilla unarmed damage plus the Battle " +
                        "Power bonus — and only to unarmed attacks. " +
                        "What it is for: armor is per damage type in Valheim, so a form that hits " +
                        "with two types is less punished by an enemy that resists one of them. " +
                        "Blunt and slash both count toward stagger, so the split does not change " +
                        "how fast a target staggers. " +
                        "(Starting value. Not playtested yet.)",
                        new AcceptableValueRange<float>(0f, 1f), AdminOnly(85))),

                // O peso e' o unico numero da forma que NAO passa pelo PowerMultiplier, e de
                // proposito: multiplicar o limite pelo poder faria a mochila crescer junto com o
                // grind de Battle Power, e o limite de peso e' logistica, nao combate. Aqui cada
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
                        "Battle Power XP (XpWeightBonus). " +
                        "Playtested 2026-08-16: started at 300 and came down to 100.",
                        new AcceptableValueRange<float>(0f, 2000f), AdminOnly(83))),

                MasteryDrainReduction = config.Bind(section, "MasteryDrainReduction", 0.8f,
                    new ConfigDescription(
                        "Fraction of the drain removed at level 100 of THIS form's skill. " +
                        "drain = KiDrainPerSecond * (1 - level/100 * this). " +
                        "0.8 = at level 100 the form costs a fifth of what it costs at level 0, " +
                        "which is the whole progression of mastery: at first you barely hold the " +
                        "form, later you own it. " +
                        "Careful: too high and the form becomes permanent and ki stops being a " +
                        "source of tension.",
                        new AcceptableValueRange<float>(0f, 0.95f), AdminOnly(80))),

                // Referencia para calibrar: a curva do Valheim ((nivel+1)^1.5 * 0.5 + 0.5 por
                // nivel) cobra ~20.000 de XP para ir do 0 ao 100, e ~1.600 para chegar ao 30.
                // A 1/s, o nivel 30 sai com ~27 minutos DENTRO da forma — que nao e' o mesmo que
                // 27 minutos de jogo, porque o dreno obriga a recarregar entre uma e outra.
                MasteryXpPerSecond = config.Bind(section, "MasteryXpPerSecond", 1f,
                    new ConfigDescription(
                        "XP for this form's skill per second transformed. Holding the form is the " +
                        "only way to train it, the same way flying is the only way to train Flight. " +
                        "Valheim's own diminishing curve up to 100 applies on top: reaching level " +
                        "30 costs about 1600 XP and level 100 about 20000. " +
                        "(Starting value. Not playtested yet.)",
                        new AcceptableValueRange<float>(0f, 20f), AdminOnly(70))),

                MinBattlePower = config.Bind(section, "MinBattlePower", 0f,
                    new ConfigDescription(
                        "Minimum Battle Power level required to enter this form. 0 disables the " +
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
                        null, ClientSide(40)))
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
            float kiCost, float cooldown, string projectilePrefab, string requiredGlobalKey)
        {
            return new KiAttackConfig
            {
                DamageBase = config.Bind(section, "DamageBase", damageBase,
                    new ConfigDescription(
                        "Damage of this attack at power level zero, before the power share below. " +
                        "It is the floor: a fresh character has almost no power level, and an " +
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
                        "Share of the COMBAT power level added to this attack's damage. " +
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
                        "Ki spent per shot, charged when you fire — hit or miss. Charging on " +
                        "impact instead would reward aim and punish fighting anything fast, which " +
                        "is the opposite of what a ranged attack should teach. " +
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

                Knockback = config.Bind(section, "Knockback", 30f,
                    new ConfigDescription(
                        "Push applied to whatever is hit. It is what makes the shot read as an " +
                        "impact rather than a scratch, and it buys back the distance the attack " +
                        "exists to keep.",
                        new AcceptableValueRange<float>(0f, 500f), AdminOnly(80))),

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
                        "Alternatives worth trying: DvergerStaffIce_projectile (blue), " +
                        "DvergerStaffFire_clusterbomb_projectile, charred_magestaff_fire.",
                        null, AdminOnly(75))),

                ProjectileSpeed = config.Bind(section, "ProjectileSpeed", 30f,
                    new ConfigDescription(
                        "Projectile speed in metres per second. For reference, a player runs at " +
                        "about 5 and flies at up to 30. Too slow and anything mobile walks out of " +
                        "the way; too fast and there is nothing to see between the hand and the " +
                        "target.",
                        new AcceptableValueRange<float>(1f, 200f), AdminOnly(70))),

                ProjectileLifetime = config.Bind(section, "ProjectileLifetime", 3f,
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

                ProjectileScale = config.Bind(section, "ProjectileScale", 1f,
                    new ConfigDescription(
                        "Size of the projectile, 1 being the prefab as it came. " +
                        "Visual only: it does NOT change what the projectile hits.",
                        new AcceptableValueRange<float>(0.1f, 10f), ClientSide(55))),

                // Cosmetico, entao ClientSide como a cor da aura. Vazio de proposito: o primeiro
                // playtest deve ver o prefab como ele e', antes de decidir que cor o ki tem.
                ProjectileColor = config.Bind(section, "ProjectileColor", "",
                    new ConfigDescription(
                        "Projectile color, #RRGGBB format. Empty keeps the prefab's own colors, " +
                        "which is the default on purpose: look at the effect as the game made it " +
                        "before deciding what color ki is. Tinting touches particles, lights and " +
                        "this clone's own materials only.",
                        null, ClientSide(50))),

                // Cosmetico, entao ClientSide — mas note que ele replica: o ZSyncAnimation.SetTrigger
                // manda RPC para todo mundo, entao os amigos veem a pose de quem atirou mesmo com
                // .cfg diferente. Mesmo padrao da cor de cabelo.
                //
                MinBattlePower = config.Bind(section, "MinBattlePower", 0f,
                    new ConfigDescription(
                        "Minimum Battle Power level required to use this attack. 0 disables the " +
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
