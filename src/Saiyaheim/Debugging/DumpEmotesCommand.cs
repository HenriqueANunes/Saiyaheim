using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Jotunn.Entities;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Debugging
{
    /// <summary>
    /// Lista os emotes que o esqueleto do jogador realmente conhece, lendo os parâmetros do
    /// Animator (o jogo dispara emote como <c>emote_&lt;nome&gt;</c>).
    ///
    /// Mesmo princípio do <see cref="DumpPrefabsCommand"/>: como não dá para criar animação nova
    /// sem Blender, o trabalho vira **escolher da lista**. Sem isso, achar a pose que lê como
    /// "carregando ki" seria chutar nome e queimar rodada de teste.
    ///
    /// Uso no console (F5): <c>saiya_dumpemotes</c>
    /// </summary>
    internal class DumpEmotesCommand : ConsoleCommand
    {
        public override string Name => "saiya_dumpemotes";

        public override string Help =>
            "Lists the emotes available in the player's Animator. Use the name in ChargeEmote in the config.";

        public override void Run(string[] args)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Print("No player. Join a world first.");
                return;
            }

            Animator animator = GameAccess.GetAnimator(player);
            if (animator == null)
            {
                Print("Player Animator not found.");
                return;
            }

            const string prefix = "emote_";

            List<string> emotes = animator.parameters
                .Where(p => p.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(p => $"{p.name.Substring(prefix.Length)}  ({p.type})")
                .Where(n => !n.StartsWith("stop"))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (emotes.Count == 0)
            {
                Print("No 'emote_' parameter in the Animator.");
                return;
            }

            // Bool = emote que fica em loop, que é o que serve para carregar ki.
            // Trigger = emote de disparo único, acaba sozinho.
            Print($"{emotes.Count} emotes. The ones of type Bool loop and work for charging:");
            foreach (string emote in emotes)
            {
                Print("  " + emote);
            }

            try
            {
                string dir = Path.Combine(Paths.ConfigPath, "Saiyaheim");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "emotes.txt");
                File.WriteAllLines(path, emotes);
                Print($"→ {path}");
            }
            catch (Exception ex)
            {
                SaiyaheimPlugin.Log.LogError($"Failed to write emotes.txt: {ex.Message}");
            }
        }

        private static void Print(string message)
        {
            SaiyaheimPlugin.Log.LogInfo(message);
            if (Console.instance != null)
            {
                Console.instance.Print($"[Saiyaheim] {message}");
            }
        }
    }
}
