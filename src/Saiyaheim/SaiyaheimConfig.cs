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
    /// </summary>
    public static class SaiyaheimConfig
    {
        private const string SecGeral = "1 - Geral";
        private const string SecKi = "2 - Ki";
        private const string SecTransform = "3 - Transformacoes";
        private const string SecMastery = "4 - Maestria";
        private const string SecFlight = "5 - Voo";
        private const string SecPower = "6 - Power Level";
        private const string SecHud = "7 - HUD";
        private const string SecDebug = "9 - Debug";

        // ---------- 1 - Geral ----------

        /// <summary>Tecla que liga/desliga o ki. Client-side: cada um usa a que quiser.</summary>
        public static ConfigEntry<KeyboardShortcut> ToggleKiKey { get; private set; }

        /// <summary>Estado do ki para um personagem novo, antes de qualquer toggle.</summary>
        public static ConfigEntry<bool> KiEnabledByDefault { get; private set; }

        /// <summary>Tecla segurada para carregar ki ativamente.</summary>
        public static ConfigEntry<KeyboardShortcut> ChargeKiKey { get; private set; }

        // ---------- 2 - Ki ----------

        public static ConfigEntry<float> MaxKi { get; private set; }
        public static ConfigEntry<float> KiRegenPerSecond { get; private set; }

        /// <summary>Intervalo do tick de ki. Regeneração é por tick fixo, nunca por frame.</summary>
        public static ConfigEntry<float> KiTickInterval { get; private set; }

        /// <summary>Segundos sem regenerar depois de gastar ki.</summary>
        public static ConfigEntry<float> KiRegenDelay { get; private set; }

        /// <summary>Ki por segundo enquanto a tecla de carregar está segurada.</summary>
        public static ConfigEntry<float> ChargeKiPerSecond { get; private set; }

        /// <summary>Se true, andar interrompe o carregamento.</summary>
        public static ConfigEntry<bool> ChargeRequiresStandingStill { get; private set; }

        // ---------- 7 - HUD ----------

        public static ConfigEntry<bool> ShowKiBar { get; private set; }
        public static ConfigEntry<float> KiBarOffsetX { get; private set; }
        public static ConfigEntry<float> KiBarOffsetY { get; private set; }
        public static ConfigEntry<string> KiBarColor { get; private set; }
        public static ConfigEntry<bool> KiBarAlwaysVisible { get; private set; }

        // ---------- 3 - Transformacoes ----------

        public static ConfigEntry<float> TransformActivationCost { get; private set; }

        /// <summary>Dreno base, antes da redução por maestria.</summary>
        public static ConfigEntry<float> TransformDrainPerSecond { get; private set; }

        // ---------- 4 - Maestria ----------

        /// <summary>
        /// Fração do dreno removida no nível 100 da skill da forma.
        /// dreno = base * (1 - nivel/100 * este_valor).
        /// 0.8 = no nível 100 o dreno cai para 20% do base.
        /// </summary>
        public static ConfigEntry<float> MasteryDrainReductionAtMax { get; private set; }

        /// <summary>XP de maestria por tick enquanto transformado.</summary>
        public static ConfigEntry<float> MasteryXpPerTick { get; private set; }

        // ---------- 5 - Voo ----------

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
        public static ConfigEntry<float> PowerK2WeaponDamage { get; private set; }
        public static ConfigEntry<float> PowerK3Armor { get; private set; }
        public static ConfigEntry<float> PowerK4Ki { get; private set; }

        /// <summary>
        /// Expoente da compressão aplicada ao power level bruto antes de exibir.
        /// 0.5 = raiz quadrada. Menor comprime mais.
        /// </summary>
        public static ConfigEntry<float> PowerCompressionExponent { get; private set; }

        /// <summary>Multiplicador aplicado depois da compressão, só para o número exibido ficar legível.</summary>
        public static ConfigEntry<float> PowerDisplayScale { get; private set; }

        // ---------- 9 - Debug ----------

        public static ConfigEntry<bool> VerboseLogging { get; private set; }

        public static void Init(ConfigFile config)
        {
            // Client-side: preferência de cada jogador, servidor não impõe.
            ToggleKiKey = config.Bind(SecGeral, "ToggleKiKey",
                new KeyboardShortcut(KeyCode.K),
                new ConfigDescription(
                    "Tecla que liga/desliga o ki. Ki desligado se comporta como ki zerado: " +
                    "sem bonus de dano, sem maestria acumulando, sem componente de ki no power level.",
                    null, ClientSide(100)));

            KiEnabledByDefault = config.Bind(SecGeral, "KiEnabledByDefault", true,
                new ConfigDescription("Estado inicial do ki em um personagem novo.",
                    null, ClientSide(90)));

            ChargeKiKey = config.Bind(SecGeral, "ChargeKiKey",
                new KeyboardShortcut(KeyCode.R),
                new ConfigDescription(
                    "Tecla SEGURADA para carregar ki ativamente, bem mais rapido que a regeneracao passiva.",
                    null, ClientSide(95)));

            // --- Ki ---
            MaxKi = config.Bind(SecKi, "MaxKi", 100f,
                new ConfigDescription("Ki maximo do jogador.",
                    new AcceptableValueRange<float>(10f, 10000f), AdminOnly(100)));

            KiRegenPerSecond = config.Bind(SecKi, "KiRegenPerSecond", 2f,
                new ConfigDescription("Ki regenerado por segundo em repouso.",
                    new AcceptableValueRange<float>(0f, 500f), AdminOnly(90)));

            KiTickInterval = config.Bind(SecKi, "KiTickInterval", 0.25f,
                new ConfigDescription(
                    "Intervalo em segundos do tick de ki (regeneracao e dreno). " +
                    "Valor menor da leitura mais suave e custa mais CPU.",
                    new AcceptableValueRange<float>(0.05f, 1f), AdminOnly(80)));

            KiRegenDelay = config.Bind(SecKi, "KiRegenDelay", 1f,
                new ConfigDescription("Segundos sem regenerar depois de gastar ki.",
                    new AcceptableValueRange<float>(0f, 10f), AdminOnly(70)));

            ChargeKiPerSecond = config.Bind(SecKi, "ChargeKiPerSecond", 15f,
                new ConfigDescription(
                    "Ki por segundo enquanto a tecla de carregar esta segurada. " +
                    "Deve ser bem maior que KiRegenPerSecond — a graca e que carregar seja uma " +
                    "acao deliberada que vale a pena, nao so esperar mais rapido. " +
                    "Ignora o KiRegenDelay de proposito.",
                    new AcceptableValueRange<float>(0f, 500f), AdminOnly(60)));

            ChargeRequiresStandingStill = config.Bind(SecKi, "ChargeRequiresStandingStill", false,
                new ConfigDescription(
                    "Se true, andar interrompe o carregamento. Carregar parado e o gesto classico " +
                    "de Dragon Ball, mas atrapalha em combate — vale testar os dois.",
                    null, AdminOnly(50)));

            // --- HUD ---
            ShowKiBar = config.Bind(SecHud, "ShowKiBar", true,
                new ConfigDescription("Mostra a barra de ki.", null, ClientSide(100)));

            KiBarOffsetX = config.Bind(SecHud, "KiBarOffsetX", 0f,
                new ConfigDescription(
                    "Deslocamento horizontal da barra de ki, em pixels. " +
                    "Aplica ao vivo: edite o arquivo com o jogo aberto e a barra se move.",
                    new AcceptableValueRange<float>(-500f, 500f), ClientSide(90)));

            KiBarOffsetY = config.Bind(SecHud, "KiBarOffsetY", -34f,
                new ConfigDescription(
                    "Deslocamento vertical da barra de ki, em pixels, relativo a posicao das barras " +
                    "nativas. Negativo desce. Aplica ao vivo.",
                    new AcceptableValueRange<float>(-500f, 500f), ClientSide(80)));

            KiBarColor = config.Bind(SecHud, "KiBarColor", "#4FC3F7",
                new ConfigDescription("Cor da barra de ki, formato #RRGGBB. Aplica ao recarregar a config.",
                    null, ClientSide(70)));

            KiBarAlwaysVisible = config.Bind(SecHud, "KiBarAlwaysVisible", false,
                new ConfigDescription(
                    "Se true, a barra de ki fica sempre na tela. " +
                    "Se false (padrao), some quando o ki esta cheio e reaparece ao gastar, " +
                    "igual as barras nativas de stamina e eitr. " +
                    "Com o ki DESLIGADO a barra some nos dois casos.",
                    null, ClientSide(60)));

            // --- Transformacoes ---
            TransformActivationCost = config.Bind(SecTransform, "ActivationCost", 20f,
                new ConfigDescription("Custo pontual de ki ao ativar uma transformacao.",
                    new AcceptableValueRange<float>(0f, 1000f), AdminOnly(100)));

            TransformDrainPerSecond = config.Bind(SecTransform, "DrainPerSecond", 5f,
                new ConfigDescription("Dreno base de ki por segundo, antes da reducao por maestria.",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(90)));

            // --- Maestria ---
            MasteryDrainReductionAtMax = config.Bind(SecMastery, "DrainReductionAtMaxLevel", 0.8f,
                new ConfigDescription(
                    "Fracao do dreno removida no nivel 100. Formula: " +
                    "dreno = base * (1 - nivel/100 * este_valor). " +
                    "0.8 = no nivel 100 o dreno cai para 20% do base. " +
                    "Cuidado: valor alto demais torna a forma praticamente permanente e o ki deixa de ser tensao.",
                    new AcceptableValueRange<float>(0f, 0.95f), AdminOnly(100)));

            MasteryXpPerTick = config.Bind(SecMastery, "XpPerTick", 0.25f,
                new ConfigDescription(
                    "XP da skill da forma por tick de ki enquanto transformado. " +
                    "A curva de ganho decrescente ate o nivel 100 e a do proprio Valheim.",
                    new AcceptableValueRange<float>(0f, 10f), AdminOnly(90)));

            // --- Voo ---
            FlightKiPerSecond = config.Bind(SecFlight, "KiPerSecond", 4f,
                new ConfigDescription(
                    "Ki por segundo voando. Alto de proposito: o voo deve ser ferramenta, " +
                    "nao o modo padrao de locomocao — senao o terreno hostil do jogo vira paisagem.",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(100)));

            FlightBaseSpeed = config.Bind(SecFlight, "BaseSpeed", 15f,
                new ConfigDescription("Velocidade de voo no nivel 0 da skill, sem carga.",
                    new AcceptableValueRange<float>(1f, 100f), AdminOnly(90)));

            FlightSpeedSkillBonus = config.Bind(SecFlight, "SpeedSkillBonus", 0.5f,
                new ConfigDescription("Bonus de velocidade no nivel 100 da skill de voo. 0.5 = +50%.",
                    new AcceptableValueRange<float>(0f, 3f), AdminOnly(80)));

            FlightWeightPenalty = config.Bind(SecFlight, "WeightPenalty", 0.5f,
                new ConfigDescription(
                    "Fracao da velocidade perdida com o inventario no peso maximo. " +
                    "0.5 = voando lotado voce vai a metade da velocidade.",
                    new AcceptableValueRange<float>(0f, 0.95f), AdminOnly(70)));

            FlightMaxSpeed = config.Bind(SecFlight, "MaxSpeed", 30f,
                new ConfigDescription(
                    "Teto duro de velocidade. Limite do motor, nao balanceamento: acima de " +
                    "certa velocidade o streaming de zonas nao acompanha e o mundo carrega em pedaços.",
                    new AcceptableValueRange<float>(5f, 100f), AdminOnly(60)));

            // --- Power Level ---
            // poder = k1*HP + k2*dano_arma + k3*armadura + (ki ligado ? k4*componente_ki : 0)
            PowerK1Health = config.Bind(SecPower, "K1_Health", 1f,
                new ConfigDescription("Peso do HP efetivo no power level.",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(100)));

            PowerK2WeaponDamage = config.Bind(SecPower, "K2_WeaponDamage", 2f,
                new ConfigDescription("Peso do dano da arma equipada no power level.",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(90)));

            PowerK3Armor = config.Bind(SecPower, "K3_Armor", 1.5f,
                new ConfigDescription("Peso da armadura total no power level.",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(80)));

            PowerK4Ki = config.Bind(SecPower, "K4_Ki", 3f,
                new ConfigDescription(
                    "Peso do componente de ki (ki maximo, maestria, forma ativa). " +
                    "Zerado quando o jogador esta com o ki desligado.",
                    new AcceptableValueRange<float>(0f, 100f), AdminOnly(70)));

            PowerCompressionExponent = config.Bind(SecPower, "CompressionExponent", 0.5f,
                new ConfigDescription(
                    "Expoente da compressao do power level bruto antes de exibir. " +
                    "0.5 = raiz quadrada, 1.0 = sem compressao. Existe para o numero nao " +
                    "virar gigante e vazio cedo demais.",
                    new AcceptableValueRange<float>(0.1f, 1f), AdminOnly(60)));

            PowerDisplayScale = config.Bind(SecPower, "DisplayScale", 100f,
                new ConfigDescription("Multiplicador aplicado depois da compressao, so para leitura.",
                    new AcceptableValueRange<float>(1f, 10000f), AdminOnly(50)));

            // --- Debug ---
            VerboseLogging = config.Bind(SecDebug, "VerboseLogging", false,
                new ConfigDescription("Log detalhado no console do BepInEx.",
                    null, ClientSide(100)));

            SaiyaheimPlugin.Log.LogInfo("Config carregada.");
        }

        /// <summary>Entrada imposta pelo servidor no multiplayer (etapa 8).</summary>
        private static ConfigurationManagerAttributes AdminOnly(int order) =>
            new ConfigurationManagerAttributes { IsAdminOnly = true, Order = order };

        /// <summary>Entrada local de cada jogador; o servidor não interfere.</summary>
        private static ConfigurationManagerAttributes ClientSide(int order) =>
            new ConfigurationManagerAttributes { IsAdminOnly = false, Order = order };
    }
}
