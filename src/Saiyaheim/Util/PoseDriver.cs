using System;
using System.Collections.Generic;
using HarmonyLib;
using Saiyaheim.Flight;
using Saiyaheim.Ki;
using UnityEngine;

namespace Saiyaheim.Util
{
    /// <summary>
    /// Uma pose procedural. Voo é uma, carregamento de ki é outra, e o disparo de ki blast será a
    /// terceira ([[Melhorias#Pose procedural de disparo do ki blast]]).
    ///
    /// O contrato é dividido em dois por um motivo de custo: <see cref="Step"/> roda para
    /// <b>todo</b> jogador carregado, todo frame, e precisa ser barato para quem não está fazendo
    /// nada; <see cref="Apply"/> só roda quando alguém de fato tem o que escrever, e é o único
    /// lugar onde a pose do frame é tocada.
    /// </summary>
    internal interface IPoseContributor
    {
        /// <summary>
        /// Avança o blend de entrada e saída deste contribuinte e devolve o peso <b>agora</b>.
        /// Zero é a resposta esperada para a esmagadora maioria dos jogadores na esmagadora
        /// maioria dos frames.
        ///
        /// ⚠️ Zero quer dizer <b>"terminei"</b>, e não "não escrevo nada neste frame" — é com este
        /// número que o driver decide descartar o handler nativo do jogador. Um contribuinte que
        /// esteja momentaneamente calado (a pose sai do caminho durante um soco) tem de continuar
        /// devolvendo o peso de entrada, senão o handler é destruído e recriado a cada frame do
        /// golpe. Quem decide não escrever é o <see cref="Apply"/>.
        /// </summary>
        float Step(Player player, float deltaTime);

        /// <summary>
        /// Escreve na pose que o animator acabou de produzir. Só é chamado quando o
        /// <see cref="Step"/> deste frame devolveu mais que zero.
        /// </summary>
        void Apply(Player player, ref HumanPose pose);

        /// <summary>O personagem deixou de existir: descarta o estado por jogador.</summary>
        void Forget(Character character);
    }

    /// <summary>
    /// O ponto único onde o mod reescreve a pose do esqueleto.
    ///
    /// <b>Por que aqui.</b> <c>CharacterAnimEvent.CustomLateUpdate</c> é public e roda na fase de
    /// LateUpdate (via <c>MonoUpdaters.LateUpdate.CharacterAnimEvent</c>), ou seja <b>depois</b> de
    /// o animator ter escrito a pose do frame — a mesma janela em que o jogo aplica rotação de
    /// cabeça e IK de pés.
    ///
    /// <b>Por que um driver, e não um patch por pose.</b> Dois postfixes no mesmo método rodam em
    /// ordem indefinida, e cada um faria seu próprio <c>GetHumanPose</c> → escrever →
    /// <c>SetHumanPose</c>. São dois round-trips completos por frame e, pior, o segundo lê o que o
    /// primeiro escreveu — o tipo de acoplamento que só aparece na tela. Aqui a pose é lida uma
    /// vez, cada contribuinte escreve em cima, e ela volta uma vez.
    ///
    /// <b>Multiplayer.</b> Nada disto é replicado, e não precisa: cada contribuinte descobre
    /// sozinho quem está voando ou carregando por um canal que o jogo já sincroniza (ZDO do
    /// <c>SE_Flight</c>, ZDO do emote), e aplica a pose localmente. Por isso o postfix roda em
    /// <b>todo</b> <c>Player</c>, não só no local.
    /// </summary>
    internal static class PoseDriver
    {
        /// <summary>
        /// A ordem importa quando duas poses se sobrepõem: quem vem depois escreve por cima.
        /// Hoje elas se excluem por construção (o carregamento derruba o peso do voo), então a
        /// ordem é só o desempate de segurança.
        /// </summary>
        private static readonly IPoseContributor[] Contributors =
        {
            FlightPose.Instance,
            KiChargePose.Instance,
        };

        private static readonly float[] Weights = new float[Contributors.Length];

        private static readonly Dictionary<Character, HumanPoseHandler> Handlers =
            new Dictionary<Character, HumanPoseHandler>();

        /// <summary>
        /// Uma pose reaproveitada entre todos os personagens. O <c>GetHumanPose</c> aloca o array
        /// de músculos na primeira chamada e reusa depois — e como o ciclo ler/escrever de um
        /// personagem termina dentro da mesma chamada, nada vaza de um para o outro.
        /// </summary>
        private static HumanPose _pose = new HumanPose();

        private static float _nextSweepTime;

        private const float SweepInterval = 5f;

        [HarmonyPatch(typeof(CharacterAnimEvent), nameof(CharacterAnimEvent.CustomLateUpdate))]
        private static class Patch
        {
            private static void Postfix(CharacterAnimEvent __instance, float deltaTime)
            {
                Drive(__instance, deltaTime);
            }
        }

        private static void Drive(CharacterAnimEvent animEvent, float deltaTime)
        {
            SweepDestroyed();

            // Roda em todo personagem carregado, inclusive bicho: sair barato importa.
            if (!(GameAccess.GetAnimEventCharacter(animEvent) is Player player))
            {
                return;
            }

            float total = 0f;
            for (int i = 0; i < Contributors.Length; i++)
            {
                Weights[i] = Contributors[i].Step(player, deltaTime);
                total += Weights[i];
            }

            if (total <= 0f)
            {
                Release(player);
                return;
            }

            HumanPoseHandler handler = GetOrCreateHandler(player);
            if (handler == null)
            {
                return;
            }

            handler.GetHumanPose(ref _pose);
            if (_pose.muscles == null)
            {
                return;
            }

            for (int i = 0; i < Contributors.Length; i++)
            {
                if (Weights[i] > 0f)
                {
                    Contributors[i].Apply(player, ref _pose);
                }
            }

            handler.SetHumanPose(ref _pose);
        }

        private static HumanPoseHandler GetOrCreateHandler(Player player)
        {
            if (Handlers.TryGetValue(player, out HumanPoseHandler existing))
            {
                return existing;
            }

            Animator animator = GameAccess.GetAnimator(player);

            // isHuman é a checagem que importa: sem avatar humanoide válido o HumanPoseHandler
            // lança no construtor, e lançar aqui seria uma vez por frame.
            if (animator == null || !animator.isHuman || animator.avatar == null)
            {
                return null;
            }

            HumanPoseHandler handler;
            try
            {
                handler = new HumanPoseHandler(animator.avatar, animator.transform);
            }
            catch (Exception ex)
            {
                SaiyaheimPlugin.Log.LogWarning($"Failed to create the pose handler: {ex.Message}");
                return null;
            }

            Handlers[player] = handler;
            SaiyaheimPlugin.LogVerbose("Pose handler created.");
            return handler;
        }

        private static void Release(Character character)
        {
            if (!Handlers.TryGetValue(character, out HumanPoseHandler handler))
            {
                return;
            }

            handler?.Dispose();
            Handlers.Remove(character);
        }

        /// <summary>
        /// Morrer no meio de uma pose destrói o personagem sem passar por <see cref="Release"/> —
        /// o postfix simplesmente para de ser chamado por ele. Sem esta varredura o handler nativo
        /// ficaria pendurado até o jogo fechar.
        /// </summary>
        private static void SweepDestroyed()
        {
            if (Time.time < _nextSweepTime)
            {
                return;
            }

            _nextSweepTime = Time.time + SweepInterval;

            List<KeyValuePair<Character, HumanPoseHandler>> dead = null;
            foreach (KeyValuePair<Character, HumanPoseHandler> entry in Handlers)
            {
                // O operador == da Unity: um objeto destruído compara igual a null.
                if (entry.Key == null)
                {
                    (dead ?? (dead = new List<KeyValuePair<Character, HumanPoseHandler>>())).Add(entry);
                }
            }

            if (dead == null)
            {
                return;
            }

            foreach (KeyValuePair<Character, HumanPoseHandler> entry in dead)
            {
                entry.Value?.Dispose();
                Handlers.Remove(entry.Key);

                foreach (IPoseContributor contributor in Contributors)
                {
                    contributor.Forget(entry.Key);
                }
            }
        }
    }
}
