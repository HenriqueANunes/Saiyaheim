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
        public static ConfigEntry<float> FlightBaseSpeed { get; private set; }

        /// <summary>Bônus de velocidade no nível 100 da skill de voo. 0.5 = +50%.</summary>
        public static ConfigEntry<float> FlightSpeedSkillBonus { get; private set; }

        /// <summary>Fração da velocidade perdida com o inventário no peso máximo.</summary>
        public static ConfigEntry<float> FlightWeightPenalty { get; private set; }

        /// <summary>
        /// Teto duro de velocidade. Não é balanceamento: acima de certa velocidade o
        /// streaming de zonas do Valheim não acompanha e o mundo carrega em pedaços
        /// (ou o jogador cai pelo chão). Limite do motor, não do mod.
        /// </summary>
        public static ConfigEntry<float> FlightMaxSpeed { get; private set; }

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
            FlightKiPerSecond = config.Bind(SecFlight, "KiPerSecond", 4f,
                new ConfigDescription(
                    "Ki per second while flying. Deliberately high: flight should be a tool, " +
                    "not the default way to get around — otherwise the game's hostile terrain " +
                    "turns into scenery.",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(100)));

            FlightBaseSpeed = config.Bind(SecFlight, "BaseSpeed", 15f,
                new ConfigDescription("Flight speed at skill level 0, carrying nothing.",
                    new AcceptableValueRange<float>(1f, 100f), AdminOnly(90)));

            FlightSpeedSkillBonus = config.Bind(SecFlight, "SpeedSkillBonus", 0.5f,
                new ConfigDescription("Speed bonus at level 100 of the flight skill. 0.5 = +50%.",
                    new AcceptableValueRange<float>(0f, 3f), AdminOnly(80)));

            FlightWeightPenalty = config.Bind(SecFlight, "WeightPenalty", 0.5f,
                new ConfigDescription(
                    "Fraction of the speed lost with the inventory at maximum weight. " +
                    "0.5 = flying fully loaded you go at half speed.",
                    new AcceptableValueRange<float>(0f, 0.95f), AdminOnly(70)));

            FlightMaxSpeed = config.Bind(SecFlight, "MaxSpeed", 30f,
                new ConfigDescription(
                    "Hard speed cap. An engine limit, not balance: above a certain speed zone " +
                    "streaming cannot keep up and the world loads in chunks.",
                    new AcceptableValueRange<float>(5f, 100f), AdminOnly(60)));

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
