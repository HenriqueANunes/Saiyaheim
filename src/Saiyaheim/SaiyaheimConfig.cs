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
        private const string SecTransform = "3 - Transformations";
        private const string SecMastery = "4 - Mastery";
        private const string SecFlight = "5 - Flight";
        private const string SecFlightPose = "5.1 - Flight Pose";
        private const string SecPower = "6 - Power Level";
        private const string SecPowerSkill = "6.1 - Battle Power";
        private const string SecHud = "7 - HUD";
        private const string SecEffects = "8 - Effects";
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

        /// <summary>Intervalo do tick de ki. Regeneração é por tick fixo, nunca por frame.</summary>
        public static ConfigEntry<float> KiTickInterval { get; private set; }

        /// <summary>Segundos sem regenerar depois de gastar ki.</summary>
        public static ConfigEntry<float> KiRegenDelay { get; private set; }

        /// <summary>Ki por segundo enquanto a tecla de carregar está segurada.</summary>
        public static ConfigEntry<float> ChargeKiPerSecond { get; private set; }

        /// <summary>Se true, andar interrompe o carregamento.</summary>
        public static ConfigEntry<bool> ChargeRequiresStandingStill { get; private set; }

        // ---------- 2.1 - Combat ----------

        /// <summary>Ki consumido por golpe desarmado. Ki insuficiente não cancela o golpe, só tira o bônus.</summary>
        public static ConfigEntry<float> PunchKiCost { get; private set; }

        /// <summary>Fração do power level somada ao dano do soco.</summary>
        public static ConfigEntry<float> PunchDamageFromPower { get; private set; }

        /// <summary>Armadura garantida com o ki ligado, antes da parcela vinda do poder.</summary>
        public static ConfigEntry<float> ArmorBase { get; private set; }

        /// <summary>Fração do power level convertida em armadura.</summary>
        public static ConfigEntry<float> ArmorFromPower { get; private set; }

        // ---------- 7 - HUD ----------

        public static ConfigEntry<bool> ShowKiBar { get; private set; }
        public static ConfigEntry<float> KiBarOffsetX { get; private set; }
        public static ConfigEntry<float> KiBarOffsetY { get; private set; }
        public static ConfigEntry<string> KiBarColor { get; private set; }
        public static ConfigEntry<bool> KiBarAlwaysVisible { get; private set; }

        // ---------- 3 - Transformations ----------

        public static ConfigEntry<float> TransformActivationCost { get; private set; }

        /// <summary>Dreno base, antes da redução por maestria.</summary>
        public static ConfigEntry<float> TransformDrainPerSecond { get; private set; }

        // ---------- 4 - Mastery ----------

        /// <summary>
        /// Fração do dreno removida no nível 100 da skill da forma.
        /// dreno = base * (1 - nivel/100 * este_valor).
        /// 0.8 = no nível 100 o dreno cai para 20% do base.
        /// </summary>
        public static ConfigEntry<float> MasteryDrainReductionAtMax { get; private set; }

        /// <summary>XP de maestria por tick enquanto transformado.</summary>
        public static ConfigEntry<float> MasteryXpPerTick { get; private set; }

        // ---------- 5 - Flight ----------

        public static ConfigEntry<float> FlightKiPerSecond { get; private set; }

        /// <summary>Multiplicador do custo de ki com o botão de correr segurado.</summary>
        public static ConfigEntry<float> FlightFastKiMultiplier { get; private set; }

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

        // ---------- 5.1 - Flight Pose ----------
        //
        // A pose procedural, em espaço de músculos humanoide. Todo valor é [-1, 1] — não é grau
        // nem radiano: é a escala normalizada da Unity, onde -1 e 1 são os limites que o próprio
        // avatar declara. Por isso os mesmos números funcionariam em qualquer rig humanoide.
        //
        // Dois conjuntos, "Hover" e "Forward", interpolados pela velocidade horizontal atual. Os
        // músculos que não mudam entre parado e voando têm um valor só.

        /// <summary>Liga a pose procedural. Desligar devolve a pose idle pura do jogo.</summary>
        public static ConfigEntry<bool> FlightPoseEnabled { get; private set; }

        // Lombar e peito separados: não são a mesma articulação, e no rig do Valheim a lombar
        // arrasta o quadril junto — foi ela que o playtest viu "mexendo as pernas".
        public static ConfigEntry<float> FlightPoseHoverSpine { get; private set; }
        public static ConfigEntry<float> FlightPoseHoverChest { get; private set; }
        public static ConfigEntry<float> FlightPoseHoverArmSpread { get; private set; }
        public static ConfigEntry<float> FlightPoseHoverArmSwing { get; private set; }

        public static ConfigEntry<float> FlightPoseForwardSpine { get; private set; }
        public static ConfigEntry<float> FlightPoseForwardChest { get; private set; }
        public static ConfigEntry<float> FlightPoseForwardArmSpread { get; private set; }
        public static ConfigEntry<float> FlightPoseForwardArmSwing { get; private set; }

        /// <summary>Graus de barriga para baixo em velocidade de corrida. Ver <c>FlightPose</c>.</summary>
        public static ConfigEntry<float> FlightPoseFastPitch { get; private set; }

        /// <summary>Graus de inclinação em velocidade de cruzeiro, bem mais suave.</summary>
        public static ConfigEntry<float> FlightPoseCruisePitch { get; private set; }

        /// <summary>Graus de nariz para cima subindo, e para baixo descendo.</summary>
        public static ConfigEntry<float> FlightPoseClimbPitch { get; private set; }

        /// <summary>
        /// Tira a pose inteira do caminho durante ataque, defesa e emote, para a animação do jogo
        /// aparecer intacta. Ver <c>FlightPose.ActionTarget</c>.
        /// </summary>
        public static ConfigEntry<bool> FlightPoseReleaseOnAction { get; private set; }

        public static ConfigEntry<float> FlightPoseActionBlendSeconds { get; private set; }

        /// <summary>Quanto da pose das pernas sobrevive a um golpe. 1 segura tudo.</summary>
        public static ConfigEntry<float> FlightPoseActionLegHold { get; private set; }

        public static ConfigEntry<float> FlightPoseElbowBend { get; private set; }
        public static ConfigEntry<float> FlightPoseToePoint { get; private set; }

        // Perna esquerda e direita separadas: a pose do gênero tem uma perna recolhida e a outra
        // estendida, e no playtest de 2026-07-31 o valor único ainda espelhava para o lado errado.
        public static ConfigEntry<float> FlightPoseLegBendLeft { get; private set; }
        public static ConfigEntry<float> FlightPoseLegBendRight { get; private set; }
        public static ConfigEntry<float> FlightPoseLegSpreadLeft { get; private set; }
        public static ConfigEntry<float> FlightPoseLegSpreadRight { get; private set; }
        public static ConfigEntry<float> FlightPoseLegSwingLeft { get; private set; }
        public static ConfigEntry<float> FlightPoseLegSwingRight { get; private set; }

        /// <summary>
        /// Quanto o corpo é girado para ficar de frente para a direção do voo. A pose idle do
        /// Valheim não é simétrica — o personagem para de lado. Ver <c>FlightPose</c>.
        /// </summary>
        public static ConfigEntry<float> FlightPoseSquareToHeading { get; private set; }

        /// <summary>Segundos para a pose entrar e sair. 0 faz a pose estalar na decolagem.</summary>
        public static ConfigEntry<float> FlightPoseBlendSeconds { get; private set; }

        // ---------- 6 - Power Level ----------

        public static ConfigEntry<float> PowerK1Health { get; private set; }

        /// <summary>Só entra na fórmula do ki desligado — com ki não há arma equipada.</summary>
        public static ConfigEntry<float> PowerK2WeaponDamage { get; private set; }

        /// <summary>Só entra na fórmula do ki desligado — com ki a armadura é saída, não entrada.</summary>
        public static ConfigEntry<float> PowerK3Armor { get; private set; }

        /// <summary>Peso do nível de Battle Power. Só entra na fórmula do ki ligado.</summary>
        public static ConfigEntry<float> PowerK4PowerSkill { get; private set; }

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

        /// <summary>Emote em loop tocado enquanto carrega. Vazio desliga.</summary>
        public static ConfigEntry<string> ChargeEmote { get; private set; }

        public static ConfigEntry<string> ChargeEffectPrefab { get; private set; }
        public static ConfigEntry<string> ChargeSoundPrefab { get; private set; }
        public static ConfigEntry<string> ChargeEffectColor { get; private set; }
        public static ConfigEntry<float> ChargeEffectScale { get; private set; }
        public static ConfigEntry<bool> ChargeEffectForceLoop { get; private set; }

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
                new KeyboardShortcut(KeyCode.V),
                new ConfigDescription(
                    "Key that takes off and lands. Once airborne, movement is the usual one: " +
                    "the game's Jump button climbs, Crouch descends and Run flies fast.",
                    null, ClientSide(85)));

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

            KiRegenPerSecond = config.Bind(SecKi, "KiRegenPerSecond", 0.5f,
                new ConfigDescription(
                    "Ki regenerated per second while idle. Deliberately low: passive regeneration " +
                    "is the safety net, not the normal way to get ki back. " +
                    "If you want ki, you charge for it. (Calibrated in the 2026-07-28 playtest.)",
                    new AcceptableValueRange<float>(0f, 500f), AdminOnly(90)));

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

            ChargeKiPerSecond = config.Bind(SecKi, "ChargeKiPerSecond", 5f,
                new ConfigDescription(
                    "Ki per second while the charge key is held. " +
                    "Should be much higher than KiRegenPerSecond — the point is that charging is a " +
                    "deliberate action worth taking, not just waiting faster. " +
                    "Deliberately ignores KiRegenDelay. " +
                    "(Playtest value, 2026-07-31. Still being tuned.)",
                    new AcceptableValueRange<float>(0f, 500f), AdminOnly(60)));

            ChargeRequiresStandingStill = config.Bind(SecKi, "ChargeRequiresStandingStill", true,
                new ConfigDescription(
                    "If true, moving interrupts charging. Charging while standing still is the " +
                    "classic Dragon Ball gesture and creates a real choice: stopping to charge " +
                    "leaves you exposed. (Both tested in the 2026-07-28 playtest; standing still won.)",
                    null, AdminOnly(50)));

            // --- Combate ---
            // O numero mais arriscado da etapa 3: alto demais e o combate vira gerenciamento
            // de barra em vez de porrada.
            PunchKiCost = config.Bind(SecCombat, "PunchKiCost", 5f,
                new ConfigDescription(
                    "Ki consumed per unarmed hit while ki is on. Insufficient ki does NOT cancel " +
                    "the hit — the punch lands with raw vanilla damage, without the power level " +
                    "bonus. Set to zero to disable the cost. Missing costs nothing (the charge " +
                    "happens on the hit, not on the swing). " +
                    "(Playtest value, 2026-07-31. Still being tuned.)",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(100)));

            PunchDamageFromPower = config.Bind(SecCombat, "PunchDamageFromPower", 0.15f,
                new ConfigDescription(
                    "Fraction of the power level ADDED to punch damage. Additive, not multiplicative: " +
                    "enemy HP grows roughly linearly across biomes, and an additive stat scales " +
                    "predictably against that. " +
                    "(Playtest value, 2026-07-31. Still being tuned.)",
                    new AcceptableValueRange<float>(0f, 10f), AdminOnly(90)));

            ArmorBase = config.Bind(SecCombat, "ArmorBase", 1f,
                new ConfigDescription(
                    "Armor guaranteed while ki is on, before the share that comes from power. " +
                    "It exists so the player does not end up MORE fragile by turning ki on early " +
                    "in the game, when the skill is still at a low level. " +
                    "(Playtest value, 2026-07-31. Still being tuned.)",
                    new AcceptableValueRange<float>(0f, 200f), AdminOnly(80)));

            ArmorFromPower = config.Bind(SecCombat, "ArmorFromPower", 0.15f,
                new ConfigDescription(
                    "Fraction of the power level converted into armor. While ki is on this armor " +
                    "REPLACES equipment armor — worn pieces stop counting. Turning ki off gives " +
                    "vanilla armor back immediately.",
                    new AcceptableValueRange<float>(0f, 10f), AdminOnly(70)));

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
            TransformActivationCost = config.Bind(SecTransform, "ActivationCost", 20f,
                new ConfigDescription("One-off ki cost to activate a transformation.",
                    new AcceptableValueRange<float>(0f, 1000f), AdminOnly(100)));

            TransformDrainPerSecond = config.Bind(SecTransform, "DrainPerSecond", 5f,
                new ConfigDescription("Base ki drain per second, before the mastery reduction.",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(90)));

            // --- Maestria ---
            MasteryDrainReductionAtMax = config.Bind(SecMastery, "DrainReductionAtMaxLevel", 0.8f,
                new ConfigDescription(
                    "Fraction of the drain removed at level 100. Formula: " +
                    "drain = base * (1 - level/100 * this_value). " +
                    "0.8 = at level 100 the drain falls to 20% of the base. " +
                    "Careful: too high a value makes the form practically permanent and ki stops " +
                    "being a source of tension.",
                    new AcceptableValueRange<float>(0f, 0.95f), AdminOnly(100)));

            MasteryXpPerTick = config.Bind(SecMastery, "XpPerTick", 0.25f,
                new ConfigDescription(
                    "XP for the form's skill per ki tick while transformed. " +
                    "The diminishing gain curve up to level 100 is Valheim's own.",
                    new AcceptableValueRange<float>(0f, 10f), AdminOnly(90)));

            // --- Voo ---
            FlightKiPerSecond = config.Bind(SecFlight, "KiPerSecond", 15f,
                new ConfigDescription(
                    "Ki per second while flying. Deliberately high: flight should be a tool, " +
                    "not the default way to get around — otherwise the game's hostile terrain " +
                    "turns into scenery. Running out of ki in the air drops you. " +
                    "(Playtest value, 2026-07-31. Raised from 4: at that cost flight was cheap " +
                    "enough to become the default way to travel.)",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(100)));

            FlightFastKiMultiplier = config.Bind(SecFlight, "FastKiMultiplier", 2.5f,
                new ConfigDescription(
                    "Ki cost multiplier while the Run button is held. Higher than the speed " +
                    "multiplier on purpose: fast flight should cost more per travelled metre, " +
                    "not just more per second.",
                    new AcceptableValueRange<float>(1f, 10f), AdminOnly(95)));

            // 15 era o valor da primeira versão e fazia o modo rápido bater o MaxSpeed (15 x 2 = 30)
            // ainda no nível 0 — nenhuma progressão aparecia com o shift pressionado. Ao mexer
            // aqui, conferir se BaseSpeed * FastSpeedMultiplier ainda sobra bem abaixo do MaxSpeed.
            //
            // 2 e um piso deliberadamente miseravel: e velocidade de caminhada no Valheim (correr
            // e ~5). Voar cedo no jogo e mais lento que andar, e quem paga a velocidade e o
            // SpeedFromPower. Voo virou privilegio de quem ja e forte, nao meio de transporte.
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

            FlightFastSpeedMultiplier = config.Bind(SecFlight, "FastSpeedMultiplier", 1.8f,
                new ConfigDescription(
                    "Speed multiplier while the Run button is held. Vanilla already reads that " +
                    "button inside its own flight code, so fast flight costs no extra keybind.",
                    new AcceptableValueRange<float>(1f, 10f), AdminOnly(85)));

            FlightVerticalSpeedFactor = config.Bind(SecFlight, "VerticalSpeedFactor", 0.5f,
                new ConfigDescription(
                    "Climb and dive speed, as a fraction of the horizontal speed. " +
                    "1.0 goes up as fast as forward, which makes altitude nearly free. " +
                    "(Lowered from 0.75 after the 2026-07-31 playtest: climbing felt too quick.)",
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

            FlightKiSkillReduction = config.Bind(SecFlight, "KiSkillReduction", 0.5f,
                new ConfigDescription(
                    "Fraction of the ki cost removed at level 100 of the flight skill. " +
                    "0.5 = at max level flying costs half. Together with SpeedSkillBonus this is " +
                    "the whole progression of the skill: farther per point of ki.",
                    new AcceptableValueRange<float>(0f, 0.95f), AdminOnly(65)));

            FlightXpPerSecond = config.Bind(SecFlight, "XpPerSecond", 0.3f,
                new ConfigDescription(
                    "Flight skill XP per second airborne. Flying is its own training — there is " +
                    "no other way to raise it. Valheim's own diminishing curve up to 100 applies. " +
                    "(Playtest value, 2026-07-31. Lowered from 1: the skill is what makes flight " +
                    "cheap, and reaching that quickly would undo the cost of KiPerSecond.)",
                    new AcceptableValueRange<float>(0f, 20f), AdminOnly(60)));

            FlightWeightPenalty = config.Bind(SecFlight, "WeightPenalty", 0.5f,
                new ConfigDescription(
                    "Fraction of the speed lost with the inventory at maximum weight. " +
                    "0.5 = flying fully loaded you go at half speed.",
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

            // --- Flight Pose ---
            // Muscle space, not degrees: [-1, 1] against the limits the avatar itself declares.
            // Tune these with the game open — they exist because the pose is a judgement call and
            // the person who can see the screen is not the one writing the code.
            FlightPoseEnabled = config.Bind(SecFlightPose, "Enabled", true,
                new ConfigDescription(
                    "Procedural flight pose. Sits on top of the forced idle pose: arms out, legs " +
                    "together, toes pointed, leaning forward as you pick up speed. Turn it off to " +
                    "fly in the plain idle pose.",
                    null, ClientSide(100)));

            FlightPoseHoverSpine = config.Bind(SecFlightPose, "HoverSpine", 0.3f,
                new ConfigDescription(
                    "Lower spine front-back while hovering. This is the joint the 2026-07-31 " +
                    "playtest saw moving the legs: on Valheim's rig the lower spine drags the " +
                    "pelvis with it. Keep it small and put the lean in HoverChest.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(95)));

            FlightPoseHoverChest = config.Bind(SecFlightPose, "HoverChest", 0f,
                new ConfigDescription(
                    "Chest front-back while hovering. Bends the upper torso without dragging the " +
                    "hips, so this is usually the one you want.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(94)));

            FlightPoseHoverArmSpread = config.Bind(SecFlightPose, "HoverArmSpread", -0.45f,
                new ConfigDescription(
                    "Arms down-up while hovering. This is an absolute target, not an offset: 0 is " +
                    "the T-pose, arms straight out sideways, and hanging at the sides is around " +
                    "-0.65. So -0.45 lifts them a little away from the body.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(94)));

            FlightPoseHoverArmSwing = config.Bind(SecFlightPose, "HoverArmSwing", 0.05f,
                new ConfigDescription(
                    "Arms front-back while hovering. 0 is the T-pose plane, positive is forward.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(93)));

            FlightPoseForwardSpine = config.Bind(SecFlightPose, "ForwardSpine", 0.15f,
                new ConfigDescription(
                    "Lower spine front-back at cruising speed. Fades back out as you speed up into " +
                    "the run range, where FastPitch takes over — the two together would fold the " +
                    "character in half, which is what the 2026-07-31 playtest reported.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(92)));

            FlightPoseForwardChest = config.Bind(SecFlightPose, "ForwardChest", 0.4f,
                new ConfigDescription(
                    "Chest front-back at cruising speed. The main forward lean of slow flight. " +
                    "(Scaled down from the 0.7 of the 2026-07-31 playtest: that value was " +
                    "compensating for a blend that only ever reached ~55% at cruising speed, and " +
                    "the blend was fixed.) Also fades out as FastPitch comes in.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(91)));

            FlightPoseFastPitch = config.Bind(SecFlightPose, "FastPitch", 55f,
                new ConfigDescription(
                    "Degrees the whole body tips belly-down once you pass cruising speed into the " +
                    "run range. Rotates the body at the hips rather than bending the spine, so the " +
                    "legs come along instead of trailing behind. Purely visual: aim, collision and " +
                    "flight direction stay horizontal (see LevelBody in section 5). 90 is flat " +
                    "Superman, 0 stays upright, negative tips belly-up.",
                    new AcceptableValueRange<float>(-90f, 90f), ClientSide(89)));

            FlightPoseCruisePitch = config.Bind(SecFlightPose, "CruisePitch", 12f,
                new ConfigDescription(
                    "Degrees the whole body tips belly-down at cruising speed — the gentle version " +
                    "of FastPitch. Fades out as FastPitch fades in, so the two never stack.",
                    new AcceptableValueRange<float>(-90f, 90f), ClientSide(88)));

            FlightPoseClimbPitch = config.Bind(SecFlightPose, "ClimbPitch", 25f,
                new ConfigDescription(
                    "Degrees the nose lifts when climbing and drops when diving, on top of the " +
                    "cruise and fast lean. Before this existed, the only vertical tilt was an " +
                    "accident — climbing and diving both bleed horizontal speed, which relaxed the " +
                    "forward lean, so both of them tipped the body the same way up. 0 keeps the " +
                    "body flat regardless of climb. Scaled by forward speed, so rising straight up " +
                    "from a standstill keeps you upright instead of pitching back.",
                    new AcceptableValueRange<float>(-90f, 90f), ClientSide(87)));

            FlightPoseReleaseOnAction = config.Bind(
                SecFlightPose, "ReleaseOnAction", true,
                new ConfigDescription(
                    "Hands the torso, arms and hip rotation back to the game while you attack, " +
                    "block, dodge or emote, so its animation plays as it does on the ground. " +
                    "Without it the pose overwrites those animations every frame and they come " +
                    "out twisted. The hips have to be part of it: a punch turns them, and free " +
                    "arms on a pinned waist is itself a twist. The legs are held separately — see " +
                    "ActionLegHold. Also covers the ki charging emote.",
                    null, ClientSide(78)));

            FlightPoseActionLegHold = config.Bind(SecFlightPose, "ActionLegHold", 1f,
                new ConfigDescription(
                    "How much of the leg pose survives an attack, block or emote, when " +
                    "ReleaseOnAction hands the rest of the body back. 1 keeps the legs flying, " +
                    "which is what stops blocking in mid-air from looking like standing still in " +
                    "mid-air. 0 releases them with everything else — try that if the kick on " +
                    "secondary attack looks wrong, since it is the one animation that does need " +
                    "the legs.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(76)));

            FlightPoseActionBlendSeconds = config.Bind(
                SecFlightPose, "ActionBlendSeconds", 0.12f,
                new ConfigDescription(
                    "Seconds to hand the upper body over when an action starts, and to take it " +
                    "back when it ends. Deliberately much quicker than BlendSeconds: a punch is " +
                    "over in a fraction of a second, and a slow hand-off would eat the start of it.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(77)));

            FlightPoseForwardArmSpread = config.Bind(SecFlightPose, "ForwardArmSpread", -0.6f,
                new ConfigDescription(
                    "Arms down-up at full speed. More negative than hovering: the arms come back " +
                    "in against the body, which is what streamlined looks like.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(91)));

            FlightPoseForwardArmSwing = config.Bind(SecFlightPose, "ForwardArmSwing", 0.4f,
                new ConfigDescription(
                    "Arms front-back at full speed, sweeping them behind the body. (Value from the " +
                    "2026-07-31 playtest; the sign shipped inverted.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(90)));

            FlightPoseElbowBend = config.Bind(SecFlightPose, "ElbowBend", 0.8f,
                new ConfigDescription(
                    "Forearm stretch — the elbow bend. (Value from the 2026-07-31 playtest; the " +
                    "sign shipped inverted.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(85)));

            FlightPoseToePoint = config.Bind(SecFlightPose, "ToePoint", 0.4f,
                new ConfigDescription(
                    "Foot up-down. Pointed toes are cheap and sell flight better than almost " +
                    "anything else here. (Value from the 2026-07-31 playtest; the sign shipped " +
                    "inverted.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(81)));

            FlightPoseSquareToHeading = config.Bind(SecFlightPose, "SquareToHeading", 1f,
                new ConfigDescription(
                    "Turns the body to face the direction of flight. Valheim's idle pose is not " +
                    "symmetrical — the character stands side-on, with the hips angled and the " +
                    "spine twisted back the other way to compensate. Walking hides it; hovering " +
                    "does not. 1 squares up fully, 0 leaves the vanilla stance. If the character " +
                    "ends up over-rotated the other way, this is the value to lower.",
                    new AcceptableValueRange<float>(0f, 1f), ClientSide(88)));

            // --- Pernas, esquerda e direita separadas ---
            // Um valor por perna e não um espelhado: o espelhamento automático saiu errado no
            // playtest (as duas pernas iam para o mesmo lado), e a pose do gênero é assimétrica
            // de qualquer forma — uma perna recolhida, a outra estendida.
            FlightPoseLegBendLeft = config.Bind(SecFlightPose, "LegBendLeft", 0.7f,
                new ConfigDescription(
                    "Left lower leg stretch — the knee bend. (Value from the 2026-07-31 playtest; " +
                    "the sign shipped inverted.)",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(84)));

            FlightPoseLegBendRight = config.Bind(SecFlightPose, "LegBendRight", 0.3f,
                new ConfigDescription(
                    "Right lower leg stretch. Set this differently from LegBendLeft to tuck one " +
                    "leg and leave the other extended.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(83)));

            FlightPoseLegSpreadLeft = config.Bind(SecFlightPose, "LegSpreadLeft", 0f,
                new ConfigDescription(
                    "Left upper leg in-out. Closes or opens the standing stance; legs apart is a " +
                    "strong tell that the character is really just standing in mid-air.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(82)));

            FlightPoseLegSpreadRight = config.Bind(SecFlightPose, "LegSpreadRight", 0.1f,
                new ConfigDescription(
                    "Right upper leg in-out. May need the opposite sign of LegSpreadLeft to look " +
                    "symmetrical.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(81)));

            FlightPoseLegSwingLeft = config.Bind(SecFlightPose, "LegSwingLeft", 0.4f,
                new ConfigDescription(
                    "Left upper leg front-back.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(80)));

            FlightPoseLegSwingRight = config.Bind(SecFlightPose, "LegSwingRight", 0.3f,
                new ConfigDescription(
                    "Right upper leg front-back.",
                    new AcceptableValueRange<float>(-1f, 1f), ClientSide(79)));

            FlightPoseBlendSeconds = config.Bind(SecFlightPose, "BlendSeconds", 0.35f,
                new ConfigDescription(
                    "Seconds to ease the pose in on take-off and out on landing. 0 snaps.",
                    new AcceptableValueRange<float>(0f, 3f), ClientSide(70)));

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
            SkillXpPerDamageDealt = config.Bind(SecPowerSkill, "XpPerDamageDealt", 0.05f,
                new ConfigDescription(
                    "XP per point of damage dealt, CAPPED at the target's remaining HP. That cap is " +
                    "what kills weak-mob farming: a 5000 damage punch on a 10 HP boar counts as 10. " +
                    "(Playtest value, 2026-07-31. Still being tuned.)",
                    new AcceptableValueRange<float>(0f, 10f), AdminOnly(100)));

            SkillXpPerDamageTaken = config.Bind(SecPowerSkill, "XpPerDamageTaken", 0.05f,
                new ConfigDescription(
                    "XP per point of damage taken, measured AFTER armor. Measuring it after armor is " +
                    "what kills the exploit of taking hits on purpose from a weak enemy: taking " +
                    "little damage pays little.",
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
            ChargeEmote = config.Bind(SecEffects, "ChargeEmote", "roar",
                new ConfigDescription(
                    "Emote looped while charging ki. Empty disables the animation. " +
                    "Run saiya_dumpemotes in the console (F5) to see the valid list — " +
                    "the ones of type Bool are the ones that loop.",
                    null, ClientSide(100)));

            ChargeEffectPrefab = config.Bind(SecEffects, "ChargeEffectPrefab", "fx_DvergerMage_Support_start",
                new ConfigDescription(
                    "Visual effect prefab attached to the player while charging. Empty disables it. " +
                    "Run saiya_dumpprefabs for the full list; candidates include " +
                    "fx_ShieldCharge_1 through _5 (increasing intensity) and fx_chainlightning_spread.",
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
                    "The original particle fade is preserved; only the base color changes.",
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

            // --- Debug ---
            VerboseLogging = config.Bind(SecDebug, "VerboseLogging", false,
                new ConfigDescription("Detailed logging in the BepInEx console.",
                    null, ClientSide(100)));

            SaiyaheimPlugin.Log.LogInfo("Config loaded.");
        }

        /// <summary>Entrada imposta pelo servidor no multiplayer (etapa 8).</summary>
        private static ConfigurationManagerAttributes AdminOnly(int order) =>
            new ConfigurationManagerAttributes { IsAdminOnly = true, Order = order };

        /// <summary>Entrada local de cada jogador; o servidor não interfere.</summary>
        private static ConfigurationManagerAttributes ClientSide(int order) =>
            new ConfigurationManagerAttributes { IsAdminOnly = false, Order = order };
    }
}
