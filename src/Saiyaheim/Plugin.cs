using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using Saiyaheim.Debugging;
using Saiyaheim.Ki;
using Saiyaheim.Power;
using UnityEngine;

namespace Saiyaheim
{
    /// <summary>
    /// Ponto de entrada do mod. Etapa 1 do roadmap: esqueleto + config.
    /// Nenhuma mecânica ainda — só bootstrap, config e o comando de dump de prefabs.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class SaiyaheimPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.hman.saiyaheim";
        public const string PluginName = "Saiyaheim";
        public const string PluginVersion = "0.1.0";

        internal static SaiyaheimPlugin Instance { get; private set; }

        /// <summary>Logger do mod. Aparece no console do BepInEx prefixado com [Saiyaheim].</summary>
        internal static ManualLogSource Log { get; private set; }

        /// <summary>
        /// Log só quando <c>VerboseLogging</c> está ligado. Serve para eventos que acontecem muito
        /// (aplicar efeito, ganhar XP) e que poluiriam o console em uso normal.
        /// </summary>
        internal static void LogVerbose(string message)
        {
            if (SaiyaheimConfig.VerboseLogging != null && SaiyaheimConfig.VerboseLogging.Value)
            {
                Log.LogInfo(message);
            }
        }

        private Harmony _harmony;

        /// <summary>
        /// Recarrega a config quando o .cfg muda em disco. Sem isso, ajustar a posição da barra
        /// de ki exigiria fechar o jogo a cada 4 pixels — e ajuste de HUD é a parte mais
        /// iterativa do projeto, já que só o Henrique vê a tela.
        /// </summary>
        private ConfigFileWatcher _configWatcher;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            SaiyaheimConfig.Init(Config);

            _configWatcher = new ConfigFileWatcher(Config);
            _configWatcher.OnConfigFileReloaded += () =>
            {
                Log.LogInfo("Config reloaded from disk.");
                KiHud.OnConfigReloaded();
            };

            PowerSkill.Register();

            // Um patch só, em Character.ApplyDamage, e só para contabilizar XP — ver DamageXpPatch
            // para os motivos de não haver caminho nativo. Dano e armadura saem de StatusEffect.
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(SaiyaheimPlugin).Assembly);

            CommandManager.Instance.AddConsoleCommand(new DumpPrefabsCommand());
            CommandManager.Instance.AddConsoleCommand(new DumpEmotesCommand());
            CommandManager.Instance.AddConsoleCommand(new KiCommand());
            CommandManager.Instance.AddConsoleCommand(new PowerCommand());

            Log.LogInfo($"{PluginName} v{PluginVersion} loaded.");
        }

        /// <summary>
        /// Único ponto de update do mod. O ki tickeia daqui em vez de por patch Harmony em
        /// <c>Player.Update</c> — menos superfície para quebrar quando o jogo atualizar.
        /// </summary>
        private void Update()
        {
            float dt = Time.deltaTime;
            KiManager.Update(dt);
            KiBodyManager.Update(Player.m_localPlayer);
            KiHud.Update();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
