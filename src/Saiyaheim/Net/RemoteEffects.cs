using System.Collections.Generic;
using Saiyaheim.Ki;
using Saiyaheim.Transformations;
using UnityEngine;

namespace Saiyaheim.Net
{
    /// <summary>
    /// Aplica em <b>todo</b> jogador carregado o que o <see cref="NetState"/> conta sobre ele.
    ///
    /// <b>O que este arquivo é, em uma frase:</b> o laço que faltava. As poses já rodavam para
    /// todo jogador, porque o <c>PoseDriver</c> pendura num hook que o jogo chama por personagem;
    /// os efeitos não tinham hook nenhum e eram chamados de dentro do <c>KiManager</c> e do
    /// <c>TransformationManager</c>, que só conhecem o jogador local. Daí o laço.
    ///
    /// <b>O jogador local passa por aqui igual aos outros</b>, e é a decisão que dá o valor do
    /// arquivo inteiro. O caminho curto — efeito local direto do manager, efeito remoto pelo canal
    /// — existia e foi desmontado: ele significaria que uma bandeira que parasse de ser publicada
    /// continuaria funcionando na tela do Henrique e falharia só na dos amigos, que é a categoria
    /// exata de bug que esta etapa não pode se dar ao luxo de esconder. A etapa é validada numa
    /// sessão marcada, não iterando.
    ///
    /// <b>Efeito nunca é objeto de rede.</b> Nada aqui instancia coisa replicada: cada máquina lê
    /// a bandeira e cria o próprio efeito com <c>ZNetView.m_forceDisableInit</c>, como o
    /// <c>AttachedEffect</c> sempre fez. O que atravessa a rede é um inteiro.
    /// </summary>
    internal static class RemoteEffects
    {
        /// <summary>
        /// Jogadores que este cliente já viu, para saber de quem esquecer quando somem. O
        /// <c>Player.GetAllPlayers()</c> responde quem <b>existe</b>; a diferença entre um frame e
        /// o outro é quem saiu.
        /// </summary>
        private static readonly HashSet<Player> Known = new HashSet<Player>();

        private static readonly List<Player> Gone = new List<Player>();

        private static float _nextSweepTime;

        private const float SweepInterval = 5f;

        internal static void Update()
        {
            List<Player> players = Player.GetAllPlayers();
            Player local = Player.m_localPlayer;
            bool showOthers = SaiyaheimConfig.ShowRemoteEffects.Value;

            for (int i = 0; i < players.Count; i++)
            {
                Player player = players[i];

                if (player == null)
                {
                    continue;
                }

                if (!showOthers && !ReferenceEquals(player, local))
                {
                    // Desligado no meio de uma sessão: o que já estava aceso precisa apagar, senão
                    // a chave só valeria para quem ainda não tinha começado a carregar.
                    KiChargeEffects.Update(player, charging: false);
                    continue;
                }

                Known.Add(player);

                KiChargeEffects.Update(player, NetState.IsCharging(player));
                TransformationEffects.Observe(player);
            }

            Sweep(players);
        }

        /// <summary>
        /// Descarta o estado de quem saiu do alcance ou morreu.
        ///
        /// Os objetos de efeito são filhos do transform do jogador e morrem junto com ele — o que
        /// vaza sem isto não é <c>GameObject</c>, são as entradas de dicionário e a referência que
        /// elas seguram. A cada cinco segundos é de sobra: o custo de uma entrada extra por esse
        /// tempo é nada, e varrer todo frame seria pagar por um caso que quase nunca acontece.
        /// Mesma cadência e mesmo motivo do <c>PoseDriver.SweepDestroyed</c>.
        /// </summary>
        private static void Sweep(List<Player> players)
        {
            if (Time.time < _nextSweepTime)
            {
                return;
            }

            _nextSweepTime = Time.time + SweepInterval;

            Gone.Clear();

            foreach (Player known in Known)
            {
                // O operador == da Unity: um objeto destruído compara igual a null.
                if (known == null || !players.Contains(known))
                {
                    Gone.Add(known);
                }
            }

            for (int i = 0; i < Gone.Count; i++)
            {
                KiChargeEffects.Forget(Gone[i]);
                TransformationEffects.Forget(Gone[i]);
                Known.Remove(Gone[i]);
            }
        }

    }
}
