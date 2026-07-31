using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Jotunn.Entities;
using UnityEngine;

namespace Saiyaheim.Debugging
{
    /// <summary>
    /// Dumpa os nomes de todos os prefabs carregados no ZNetScene.
    ///
    /// Motivo de existir tão cedo: o caminho certo para efeito visual no Valheim quase nunca
    /// é criar, é reaproveitar. Ter a lista na mão transforma "não sei fazer efeito visual"
    /// em "escolher da lista" — e isso precisa acontecer antes de escrever aura, bola de ki
    /// ou qualquer outro visual.
    ///
    /// Uso no console do jogo (F5): <c>saiya_dumpprefabs</c>
    /// Filtro opcional por substring: <c>saiya_dumpprefabs vfx_</c>
    /// </summary>
    internal class DumpPrefabsCommand : ConsoleCommand
    {
        public override string Name => "saiya_dumpprefabs";

        public override string Help =>
            "Dumps the ZNetScene prefab names to text files in the BepInEx config folder. " +
            "Optional argument: substring filter.";

        public override void Run(string[] args)
        {
            if (ZNetScene.instance == null)
            {
                Print("ZNetScene does not exist yet. Join a world before running this.");
                return;
            }

            List<GameObject> prefabs = ZNetScene.instance.m_prefabs;
            if (prefabs == null || prefabs.Count == 0)
            {
                Print("ZNetScene has no prefabs loaded.");
                return;
            }

            string filter = args != null && args.Length > 0 ? args[0] : null;

            List<string> names = prefabs
                .Where(p => p != null)
                .Select(p => p.name)
                .Where(n => filter == null || n.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string dir = Path.Combine(Paths.ConfigPath, "Saiyaheim");
            Directory.CreateDirectory(dir);

            try
            {
                string allPath = Path.Combine(dir, "prefabs_all.txt");
                File.WriteAllLines(allPath, names);
                Print($"{names.Count} prefabs → {allPath}");

                // Só faz sentido gerar a paleta de efeitos quando o dump é o completo.
                if (filter == null)
                {
                    string[] effectPrefixes = { "fx_", "vfx_", "sfx_" };
                    List<string> effects = names
                        .Where(n => effectPrefixes.Any(p => n.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    string fxPath = Path.Combine(dir, "prefabs_effects.txt");
                    File.WriteAllLines(fxPath, effects);
                    Print($"{effects.Count} effects (fx_/vfx_/sfx_) → {fxPath}");
                }
            }
            catch (Exception ex)
            {
                Print($"Failed to write the dump: {ex.Message}");
                SaiyaheimPlugin.Log.LogError(ex);
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
