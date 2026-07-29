using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using Saiyaheim.Debugging;
using Saiyaheim.Ki;
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
                Log.LogInfo("Config recarregada do disco.");
                KiHud.OnConfigReloaded();
            };

            // Ainda sem patches. O PatchAll fica aqui desde já para que adicionar a
            // primeira classe [HarmonyPatch] não exija tocar no bootstrap.
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(SaiyaheimPlugin).Assembly);

            CommandManager.Instance.AddConsoleCommand(new DumpPrefabsCommand());
            CommandManager.Instance.AddConsoleCommand(new DumpEmotesCommand());
            CommandManager.Instance.AddConsoleCommand(new KiCommand());

            Log.LogInfo($"{PluginName} v{PluginVersion} carregado.");
        }

        /// <summary>
        /// Único ponto de update do mod. O ki tickeia daqui em vez de por patch Harmony em
        /// <c>Player.Update</c> — menos superfície para quebrar quando o jogo atualizar.
        /// </summary>
        private void Update()
        {
            float dt = Time.deltaTime;
            KiManager.Update(dt);
            KiHud.Update();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
