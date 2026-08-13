using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using Saiyaheim.Attacks;
using Saiyaheim.Debugging;
using Saiyaheim.Flight;
using Saiyaheim.Ki;
using Saiyaheim.Net;
using Saiyaheim.Power;
using Saiyaheim.Transformations;
using UnityEngine;

namespace Saiyaheim
{
    /// <summary>
    /// Ponto de entrada do mod. Etapa 1 do roadmap: esqueleto + config.
    /// Nenhuma mec—nica ainda — só bootstrap, config e o comando de dump de prefabs.
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
            FlightSkill.Register();

            // Uma skill de maestria por forma, registrada do mesmo jeito que as duas de cima.
            TransformationRegistry.Register();

            // Seis patches, todos mínimos e nenhum em física: Character.ApplyDamage para
            // contabilizar XP (ver DamageXpPatch), Character.CustomFixedUpdate para forçar a pose
            // em pé depois que o UpdateFlying escreve no animator (ver FlightPosePatch),
            // CharacterAnimEvent.CustomLateUpdate para as poses procedurais — voo, carregamento de
            // ki e disparo — escreverem depois do animator (ver PoseDriver), o par
            // Humanoid.BlockAttack + ItemData.GetBlockPower para o bloqueio escalar com o poder
            // (ver BlockPowerPatch — é o único stat sem hook nativo de StatusEffect) e o construtor
            // do ZRoutedRpc, que é só onde o RPC de XP do coop se registra (ver DamageReport).
            // Dano, armadura e o voo em si saem de StatusEffect.
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(SaiyaheimPlugin).Assembly);

            CommandManager.Instance.AddConsoleCommand(new KiCommand());
            CommandManager.Instance.AddConsoleCommand(new PowerCommand());
            CommandManager.Instance.AddConsoleCommand(new FlightCommand());
            CommandManager.Instance.AddConsoleCommand(new BlockCommand());
            CommandManager.Instance.AddConsoleCommand(new TransformCommand());
            CommandManager.Instance.AddConsoleCommand(new AttackCommand());
            CommandManager.Instance.AddConsoleCommand(new NetCommand());

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
            FlightManager.Update(Player.m_localPlayer);
            TransformationManager.Update(Player.m_localPlayer);
            KiAttackManager.Update(Player.m_localPlayer);

            // Publicar antes de aplicar: o que os efeitos leem neste frame é o estado deste frame.
            PublishNetState(Player.m_localPlayer);
            RemoteEffects.Update();

            KiHud.Update();
        }

        /// <summary>
        /// Conta aos outros clientes o que este jogador está fazendo. Etapa 8.
        ///
        /// <b>Depois dos managers, de propósito</b>: o valor publicado é o do frame que acabou de
        /// ser decidido, e não o do anterior. Transformar e atirar no mesmo frame chegam juntos ao
        /// outro lado.
        ///
        /// A montagem do estado mora aqui e não dentro do <see cref="NetState"/> para o canal não
        /// precisar conhecer ki, voo nem formas — e para a ordem estar visível num lugar só.
        /// </summary>
        private static void PublishNetState(Player player)
        {
            if (player == null)
            {
                return;
            }

            NetState.Publish(
                player,
                KiManager.IsEnabled,
                FlightManager.IsFlying(player),
                KiManager.IsCharging,
                TransformationRegistry.IndexOf(TransformationRegistry.GetActive(player)));
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
