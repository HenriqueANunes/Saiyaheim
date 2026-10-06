using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Saiyaheim.Attacks;
using Saiyaheim.Kaioken;
using UnityEngine;

namespace Saiyaheim.Runes
{
    /// <summary>
    /// O que cada personagem aprendeu nas runestones, e a regra de aprender (etapa 13). Desde a
    /// etapa 14 ensina também os tiers de Kaioken: tudo aqui fala de <see cref="IRuneLesson"/>, e
    /// ataque e tier entram no mesmo sorteio do bioma.
    ///
    /// <b>Por personagem, e não por mundo</b> — o oposto do <c>BossGate</c>. Quem leu aprendeu; cada
    /// amigo precisa achar a própria pedra. Por isso o estado mora em <c>Player.m_customData</c>,
    /// como o <c>KiState</c>, e vai no save do personagem sem uma linha de rede: o ataque em si já
    /// sincroniza, e ninguém pergunta o que o vizinho aprendeu.
    ///
    /// Três entradas no dicionário:
    /// <list type="bullet">
    /// <item><c>saiyaheim.attacks.learned</c>: ids dos ataques e dos tiers de Kaioken aprendidos. O
    /// nome da chave ficou o da etapa 13 para não perder o que já foi aprendido;</item>
    /// <item><c>saiyaheim.runes.read</c>: pedras lidas, posição arredondada → instante da leitura em
    /// segundos de mundo, para o cooldown;</item>
    /// <item><c>saiyaheim.runes.misses</c>: pedras vazias seguidas por bioma, a proteção contra azar.</item>
    /// </list>
    ///
    /// Ver [[Runestones]] no vault.
    /// </summary>
    internal static class RuneKnowledge
    {
        private const string KeyLearned = "saiyaheim.attacks.learned";
        private const string KeyRead = "saiyaheim.runes.read";
        private const string KeyMisses = "saiyaheim.runes.misses";

        // Cache do que foi aprendido. O IsUnlocked é consultado a cada toque de tecla e a cada
        // abertura da roda, e reparsear a string em todas seria alocação à toa. Invalida pela
        // referência da string guardada: qualquer escrita troca a string, inclusive o load de
        // outro personagem.
        private static Player _cachedPlayer;
        private static string _cachedRaw;
        private static readonly HashSet<string> CachedLearned =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>O personagem já aprendeu isto?</summary>
        internal static bool HasLearned(Player player, IRuneLesson attack)
        {
            if (player == null || attack == null || player.m_customData == null)
            {
                return false;
            }

            return GetLearned(player).Contains(attack.Id);
        }

        /// <summary>Marca como aprendido. Devolve false se já estava.</summary>
        internal static bool Learn(Player player, IRuneLesson attack)
        {
            if (player == null || attack == null || player.m_customData == null)
            {
                return false;
            }

            HashSet<string> learned = new HashSet<string>(GetLearned(player), StringComparer.OrdinalIgnoreCase);
            if (!learned.Add(attack.Id))
            {
                return false;
            }

            WriteLearned(player, learned);
            return true;
        }

        /// <summary>Esquece. Só o console chega aqui. Devolve false se não sabia.</summary>
        internal static bool Forget(Player player, IRuneLesson attack)
        {
            if (player == null || attack == null || player.m_customData == null)
            {
                return false;
            }

            HashSet<string> learned = new HashSet<string>(GetLearned(player), StringComparer.OrdinalIgnoreCase);
            if (!learned.Remove(attack.Id))
            {
                return false;
            }

            WriteLearned(player, learned);
            return true;
        }

        /// <summary>Apaga pedras lidas e contadores. Não esquece ataque nenhum.</summary>
        internal static void ResetRunes(Player player)
        {
            if (player == null || player.m_customData == null)
            {
                return;
            }

            player.m_customData.Remove(KeyRead);
            player.m_customData.Remove(KeyMisses);
        }

        /// <summary>
        /// O jogador local acabou de ler a runestone em <paramref name="position"/>. Sorteia e,
        /// se for o caso, ensina. Devolve o que foi aprendido, ou null.
        ///
        /// A ordem das saídas é a regra:
        /// <list type="number">
        /// <item>bioma sem nada a ensinar: sai <b>sem</b> marcar a pedra nem mexer no contador, para
        /// um ataque novo numa atualização futura encontrar as pedras ainda frescas;</item>
        /// <item>pedra em cooldown: sai, e o jogador vê só o texto vanilla;</item>
        /// <item>marca a pedra e sorteia. Errou, o contador do bioma sobe; acertou, zera.</item>
        /// </list>
        /// </summary>
        internal static IRuneLesson TryLearn(Player player, Vector3 position)
        {
            if (player == null || player.m_customData == null || WorldGenerator.instance == null ||
                ZNet.instance == null)
            {
                return null;
            }

            Heightmap.Biome biome = WorldGenerator.instance.GetBiome(position);
            List<IRuneLesson> candidates = Candidates(player, biome);
            if (candidates.Count == 0)
            {
                SaiyaheimPlugin.LogVerbose($"Runestone in {biome}: nothing left to teach here.");
                return null;
            }

            double now = ZNet.instance.GetTimeSeconds();
            string stoneId = StoneId(position);
            Dictionary<string, double> read = ReadStones(player);

            if (read.TryGetValue(stoneId, out double readAt) && !IsRecharged(readAt, now))
            {
                SaiyaheimPlugin.LogVerbose(
                    $"Runestone {stoneId} in {biome}: still recharging " +
                    $"({DaysLeft(readAt, now):0.##} days left).");
                return null;
            }

            read[stoneId] = now;
            WriteStones(player, read);

            Dictionary<int, int> misses = ReadMisses(player);
            misses.TryGetValue((int)biome, out int missed);

            float chance = FindChance(missed);
            float roll = UnityEngine.Random.value;

            if (roll >= chance)
            {
                misses[(int)biome] = missed + 1;
                WriteMisses(player, misses);
                SaiyaheimPlugin.LogVerbose(
                    $"Runestone {stoneId} in {biome}: nothing (rolled {roll:0.00} against " +
                    $"{chance:0.00}, {missed + 1} empty in a row).");
                return null;
            }

            IRuneLesson taught = PickWeighted(candidates);
            Learn(player, taught);

            misses.Remove((int)biome);
            WriteMisses(player, misses);

            SaiyaheimPlugin.Log.LogInfo(
                $"Runestone {stoneId} in {biome}: learned {taught.DisplayName} " +
                $"(rolled {roll:0.00} against {chance:0.00}).");

            return taught;
        }

        /// <summary>O que este bioma ainda pode ensinar a este personagem: ataques e tiers.</summary>
        internal static List<IRuneLesson> Candidates(Player player, Heightmap.Biome biome)
        {
            List<IRuneLesson> result = new List<IRuneLesson>();
            if (biome == Heightmap.Biome.None)
            {
                return result;
            }

            foreach (IRuneLesson lesson in AllLessons())
            {
                if ((lesson.LearnBiome & biome) != 0 &&
                    lesson.LearnWeight > 0f &&
                    !HasLearned(player, lesson) &&
                    lesson.PrerequisiteMet(player))
                {
                    result.Add(lesson);
                }
            }

            return result;
        }

        /// <summary>Tudo o que alguma runestone pode ensinar, na ordem dos registries.</summary>
        internal static IEnumerable<IRuneLesson> AllLessons()
        {
            foreach (KiAttack attack in KiAttackRegistry.All)
            {
                yield return attack;
            }

            foreach (KaiokenTier tier in KaiokenRegistry.All)
            {
                yield return tier;
            }
        }

        /// <summary>A chance da próxima pedra deste bioma, dado quantas vazias vieram antes.</summary>
        internal static float FindChance(int missed)
        {
            return Mathf.Clamp01(SaiyaheimConfig.RuneFindChance.Value +
                                 SaiyaheimConfig.RuneFindChanceStep.Value * Mathf.Max(0, missed));
        }

        /// <summary>Pedras vazias seguidas, por bioma. Para o console.</summary>
        internal static Dictionary<int, int> GetMisses(Player player)
        {
            return ReadMisses(player);
        }

        /// <summary>Pedras lidas e o instante da leitura. Para o console.</summary>
        internal static Dictionary<string, double> GetReadStones(Player player)
        {
            return ReadStones(player);
        }

        /// <summary>
        /// A pedra já pode ensinar de novo?
        ///
        /// <b>Tempo de mundo voltando para trás conta como recarregada.</b> O personagem leva o
        /// estado para qualquer mundo, e um mundo mais novo tem relógio menor: sem isso, a pedra
        /// lida num mundo velho travaria a do mesmo lugar num mundo novo por semanas.
        /// </summary>
        internal static bool IsRecharged(double readAt, double now)
        {
            return now < readAt || now - readAt >= CooldownSeconds();
        }

        /// <summary>Dias de jogo até a pedra recarregar. 0 se já recarregou.</summary>
        internal static double DaysLeft(double readAt, double now)
        {
            if (IsRecharged(readAt, now))
            {
                return 0;
            }

            return (CooldownSeconds() - (now - readAt)) / DayLengthSeconds();
        }

        private static double CooldownSeconds()
        {
            return Math.Max(0f, SaiyaheimConfig.RuneCooldownDays.Value) * DayLengthSeconds();
        }

        // 1200 s é o default do EnvMan; só serve antes de ele existir, e aí ninguém lê pedra.
        private static double DayLengthSeconds()
        {
            return EnvMan.instance != null && EnvMan.instance.m_dayLengthSec > 0
                ? EnvMan.instance.m_dayLengthSec
                : 1200.0;
        }

        /// <summary>
        /// Identidade da pedra: a posição arredondada ao metro, em x e z.
        ///
        /// Runestone não tem ZDO própria — é parte da location, recriada a cada carregamento de
        /// zona —, então a posição é o único id estável. Ao metro porque a location sempre nasce no
        /// mesmo lugar, e o arredondamento só absorve erro de ponto flutuante.
        /// </summary>
        private static string StoneId(Vector3 position)
        {
            return Mathf.RoundToInt(position.x).ToString(CultureInfo.InvariantCulture) + "," +
                   Mathf.RoundToInt(position.z).ToString(CultureInfo.InvariantCulture);
        }

        private static IRuneLesson PickWeighted(List<IRuneLesson> candidates)
        {
            float total = 0f;
            foreach (IRuneLesson attack in candidates)
            {
                total += attack.LearnWeight;
            }

            float pick = UnityEngine.Random.value * total;
            foreach (IRuneLesson attack in candidates)
            {
                pick -= attack.LearnWeight;
                if (pick < 0f)
                {
                    return attack;
                }
            }

            // Random.value pode devolver exatamente 1, e aí a soma acaba sem ninguém abaixo de 0.
            return candidates[candidates.Count - 1];
        }

        private static HashSet<string> GetLearned(Player player)
        {
            player.m_customData.TryGetValue(KeyLearned, out string raw);

            if (ReferenceEquals(player, _cachedPlayer) && ReferenceEquals(raw, _cachedRaw))
            {
                return CachedLearned;
            }

            CachedLearned.Clear();
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (string id in raw.Split(','))
                {
                    if (id.Length > 0)
                    {
                        CachedLearned.Add(id);
                    }
                }
            }

            _cachedPlayer = player;
            _cachedRaw = raw;
            return CachedLearned;
        }

        private static void WriteLearned(Player player, HashSet<string> learned)
        {
            if (learned.Count == 0)
            {
                player.m_customData.Remove(KeyLearned);
                return;
            }

            player.m_customData[KeyLearned] = string.Join(",", new List<string>(learned).ToArray());
        }

        // Formato: "x,z=segundos;x,z=segundos". InvariantCulture pelo mesmo motivo do KiState.
        private static Dictionary<string, double> ReadStones(Player player)
        {
            Dictionary<string, double> result = new Dictionary<string, double>();
            if (player?.m_customData == null || !player.m_customData.TryGetValue(KeyRead, out string raw) ||
                string.IsNullOrEmpty(raw))
            {
                return result;
            }

            foreach (string entry in raw.Split(';'))
            {
                int eq = entry.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                if (double.TryParse(entry.Substring(eq + 1), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double at))
                {
                    result[entry.Substring(0, eq)] = at;
                }
            }

            return result;
        }

        /// <summary>
        /// Grava as pedras lidas, já descartando as recarregadas: uma pedra livre e uma que nunca
        /// foi lida se comportam igual, e sem a limpeza a lista cresceria com cada pedra do mapa.
        /// </summary>
        private static void WriteStones(Player player, Dictionary<string, double> read)
        {
            double now = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0;
            StringBuilder sb = new StringBuilder();

            foreach (KeyValuePair<string, double> pair in read)
            {
                if (IsRecharged(pair.Value, now))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(';');
                }

                sb.Append(pair.Key).Append('=').Append(pair.Value.ToString("R", CultureInfo.InvariantCulture));
            }

            if (sb.Length == 0)
            {
                player.m_customData.Remove(KeyRead);
            }
            else
            {
                player.m_customData[KeyRead] = sb.ToString();
            }
        }

        // Formato: "biome:n;biome:n", com o bioma como inteiro do enum — o valor é estável entre
        // versões do jogo, o nome também, mas o inteiro dispensa parse de enum.
        private static Dictionary<int, int> ReadMisses(Player player)
        {
            Dictionary<int, int> result = new Dictionary<int, int>();
            if (player?.m_customData == null || !player.m_customData.TryGetValue(KeyMisses, out string raw) ||
                string.IsNullOrEmpty(raw))
            {
                return result;
            }

            foreach (string entry in raw.Split(';'))
            {
                string[] parts = entry.Split(':');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int biome) &&
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                {
                    result[biome] = count;
                }
            }

            return result;
        }

        private static void WriteMisses(Player player, Dictionary<int, int> misses)
        {
            StringBuilder sb = new StringBuilder();

            foreach (KeyValuePair<int, int> pair in misses)
            {
                if (pair.Value <= 0)
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(';');
                }

                sb.Append(pair.Key.ToString(CultureInfo.InvariantCulture)).Append(':')
                  .Append(pair.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (sb.Length == 0)
            {
                player.m_customData.Remove(KeyMisses);
            }
            else
            {
                player.m_customData[KeyMisses] = sb.ToString();
            }
        }
    }
}
