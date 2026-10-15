using Saiyaheim.Ki;
using Saiyaheim.Net;
using Saiyaheim.Power;
using Saiyaheim.Runes;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Transmission
{
    /// <summary>
    /// Instant Transmission (etapa 16): segurar a tecla e clicar no mapa grande leva o jogador ao
    /// ponto clicado, pagando ki pela distância.
    ///
    /// <b>Três regras, e só elas:</b>
    /// <list type="bullet">
    /// <item><b>Só para onde o próprio personagem já esteve</b>: a névoa que ele mesmo tirou do
    /// mapa. O que chegou pela mesa de cartografia não conta (decisão do Henrique, 2026-10-15);</item>
    /// <item><b>custo pela distância horizontal</b>, com o ki por metro caindo com o nível de
    /// Power Level. Power Level e não battle power: o battle power muda com comida, forma e
    /// Kaioken, e teleportar viraria o ritual de comer e transformar antes;</item>
    /// <item><b>a regra de portal do mundo vale aqui também</b>: sem portais, portal travado por
    /// boss e minério no inventário recusam como o portal recusaria. O modificador de mundo que
    /// libera minério no portal libera aqui também.</item>
    /// </list>
    ///
    /// <b>O teleporte é o da vanilla</b> (<c>Player.TeleportTo</c>), o mesmo do portal e da
    /// entrada de dungeon, então forma e voo sobrevivem a ele e o dreno pausa na tela de
    /// carregamento sem nada novo. Os 2 s que a vanilla espera antes de mover o jogador fazem o
    /// papel da concentração com os dedos na testa.
    ///
    /// <b>Ensinado por runestone</b>, no mesmo sorteio dos ataques e do Kaioken. Ver
    /// [[Instant Transmission]] no vault.
    /// </summary>
    internal sealed class InstantTransmission : IRuneLesson
    {
        internal static readonly InstantTransmission Lesson = new InstantTransmission();

        /// <summary>
        /// Divide a lista de aprendidos com ataques e tiers. Mudar depois de jogar faz o
        /// personagem esquecer.
        /// </summary>
        internal const string LessonId = "instant_transmission";

        internal const string DisplayName = "Instant Transmission";

        private InstantTransmission()
        {
        }

        string IRuneLesson.Id => LessonId;
        string IRuneLesson.DisplayName => DisplayName;
        Heightmap.Biome IRuneLesson.LearnBiome => SaiyaheimConfig.TransmissionLearnBiome.Value;
        float IRuneLesson.LearnWeight => SaiyaheimConfig.TransmissionLearnWeight.Value;

        public bool PrerequisiteMet(Player player)
        {
            return true;
        }

        internal static bool IsLearned(Player player)
        {
            return RuneKnowledge.HasLearned(player, Lesson);
        }

        /// <summary>
        /// Ki por 100 m no nível de Power Level deste jogador: uma reta entre o valor no nível 0 e
        /// o no nível 100. O teto de ki também cresce com o Power Level, então o alcance de uma
        /// barra cheia sobe pelos dois lados.
        /// </summary>
        internal static float GetKiPer100m(Player player)
        {
            float t = Mathf.Clamp01(PowerSkill.GetLevel(player) / 100f);

            return Mathf.Max(0f, Mathf.Lerp(
                SaiyaheimConfig.TransmissionKiPer100mAtLevel0.Value,
                SaiyaheimConfig.TransmissionKiPer100mAtLevel100.Value,
                t));
        }

        /// <summary>Ki para ir até <paramref name="meters"/> de distância horizontal.</summary>
        internal static float GetKiCost(Player player, float meters)
        {
            return Mathf.Max(0f, SaiyaheimConfig.TransmissionKiCost.Value) +
                   GetKiPer100m(player) * Mathf.Max(0f, meters) / 100f;
        }

        /// <summary>
        /// Até onde dá para ir com <paramref name="ki"/>. Para o console: é a pergunta que a
        /// calibragem faz.
        /// </summary>
        internal static float GetRange(Player player, float ki)
        {
            float perMeter = GetKiPer100m(player) / 100f;
            float left = ki - Mathf.Max(0f, SaiyaheimConfig.TransmissionKiCost.Value);

            if (left < 0f)
            {
                return 0f;
            }

            return perMeter <= 0f ? float.PositiveInfinity : left / perMeter;
        }

        /// <summary>
        /// Distância no plano. A altura fica de fora: subir uma montanha não pode custar mais que
        /// atravessar o mesmo trecho no mapa, e dentro de dungeon o jogador está milhares de
        /// metros acima do ponto de entrada.
        /// </summary>
        internal static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// Tenta levar o jogador local até <paramref name="mapPoint"/>, o ponto clicado no mapa
        /// (a altura dele não importa). Explica na tela quando recusa. Devolve true se o teleporte
        /// começou e o ki foi cobrado.
        /// </summary>
        internal static bool TryTeleport(Player player, Vector3 mapPoint)
        {
            if (player == null || player.IsDead() || player.IsTeleporting() || Minimap.instance == null)
            {
                return false;
            }

            if (!KiManager.IsEnabled)
            {
                Message(player, "Ki is off.");
                return false;
            }

            if (!GameAccess.IsExploredBySelf(Minimap.instance, mapPoint))
            {
                Message(player, "You have never been there.");
                return false;
            }

            string blocked = WorldRuleBlocks(player);
            if (blocked != null)
            {
                Message(player, blocked);
                return false;
            }

            float meters = HorizontalDistance(player.transform.position, mapPoint);
            float cost = GetKiCost(player, meters);

            if (KiManager.Current < cost)
            {
                Message(player, $"Not enough ki: {Mathf.CeilToInt(cost)} needed for {FormatDistance(meters)}.");
                return false;
            }

            // Leme do barco, cadeira, cama: solta antes, como se levantasse. Teleportar preso a
            // um ponto de apoio deixaria o jogador amarrado a algo que ficou para trás.
            if (player.IsAttached())
            {
                player.AttachStop();
            }

            Vector3 target = GroundPoint(mapPoint);

            // Área já carregada é salto curto: sem tela de carregamento, como a ida de uma sala
            // para a outra numa dungeon. Longe, a tela da vanilla segura até a zona existir.
            bool distant = ZNetScene.instance == null || !ZNetScene.instance.IsAreaReady(target);

            // A vanilla recusa em silêncio no intervalo de 2 s entre teleportes. O ki só sai
            // depois de ela aceitar.
            if (!player.TeleportTo(target, player.transform.rotation, distant))
            {
                return false;
            }

            KiManager.TryConsume(cost);
            Minimap.instance.SetMapMode(Minimap.MapMode.Small);

            // O som sai pela rede, e a máquina do dono o toca pelo mesmo caminho dos outros.
            NetState.PublishTransmission(player);

            SaiyaheimPlugin.LogVerbose(
                $"Instant Transmission: {meters:0} m for {cost:0.#} ki " +
                $"({GetKiPer100m(player):0.##} per 100 m), distant={distant}.");

            return true;
        }

        /// <summary>
        /// As recusas do portal vanilla (<c>TeleportWorld.Teleport</c>), na mesma ordem e com as
        /// mesmas mensagens. Null se o mundo deixa.
        /// </summary>
        private static string WorldRuleBlocks(Player player)
        {
            ZoneSystem zones = ZoneSystem.instance;
            if (zones == null)
            {
                return null;
            }

            if (zones.GetGlobalKey(GlobalKeys.NoPortals))
            {
                return "$msg_blocked";
            }

            if (zones.GetGlobalKey(GlobalKeys.NoBossPortals) &&
                ((RandEventSystem.instance != null && RandEventSystem.instance.GetBossEvent() != null) ||
                 (zones.GetGlobalKey(GlobalKeys.activeBosses, out float active) && active > 0f)))
            {
                return "$msg_blockedbyboss";
            }

            // false: o personagem não tem a exceção de um portal especial; o modificador de mundo
            // TeleportAll é lido lá dentro.
            if (!player.IsTeleportable(false))
            {
                return "$msg_noteleport";
            }

            return null;
        }

        /// <summary>
        /// O chão no ponto clicado. O <c>Heightmap</c> carregado tem o terreno modificado
        /// (aplainado, escavado); longe, só existe o gerador do mundo. Abaixo do nível do mar o
        /// jogador chega nadando, e não no fundo.
        ///
        /// Meio metro acima porque o <c>UpdateTeleport</c> da vanilla procura o chão a partir de
        /// 1 m acima do alvo: nascer rente ao terreno arriscaria ficar dentro dele.
        /// </summary>
        private static Vector3 GroundPoint(Vector3 mapPoint)
        {
            float height = WorldGenerator.instance != null
                ? WorldGenerator.instance.GetHeight(mapPoint.x, mapPoint.z)
                : 0f;

            if (Heightmap.GetHeight(mapPoint, out float loaded))
            {
                height = loaded;
            }

            if (ZoneSystem.instance != null)
            {
                height = Mathf.Max(height, ZoneSystem.instance.m_waterLevel);
            }

            return new Vector3(mapPoint.x, height + 0.5f, mapPoint.z);
        }

        internal static string FormatDistance(float meters)
        {
            return meters >= 1000f ? $"{meters / 1000f:0.0} km" : $"{meters:0} m";
        }

        private static void Message(Player player, string message)
        {
            player.Message(MessageHud.MessageType.Center, message);
        }
    }
}
