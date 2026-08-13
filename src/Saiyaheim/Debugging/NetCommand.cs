using System.Collections.Generic;
using Saiyaheim.Net;
using Saiyaheim.Transformations;
using UnityEngine;

namespace Saiyaheim.Debugging
{
    /// <summary>
    /// O que este cliente sabe sobre cada jogador carregado.
    ///
    /// <b>Este comando importa mais que os outros de debug, e por uma razão de processo.</b> Os
    /// números do voo e do poder são calibrados em ciclo curto: o Henrique joga, vê, reporta,
    /// ajusta. A etapa 8 não tem ciclo curto — ela é validada numa sessão marcada com amigo, e o
    /// feedback chega em bloco, depois. "A aura do fulano não apareceu" é uma frase que, sem isto,
    /// não tem como virar diagnóstico: não dá para saber se a bandeira não foi publicada, se não
    /// chegou, ou se chegou e o efeito é que não subiu.
    ///
    /// Com o comando, a pergunta se parte em duas metades que se respondem em máquinas diferentes:
    /// <b>o que eu publico de mim</b> e <b>o que eu vejo do outro</b>. Rodar nos dois clientes e
    /// comparar as duas saídas localiza o furo sem ninguém precisar repetir a sessão.
    ///
    /// <code>
    /// saiya_net    lista todos os jogadores carregados e o estado publicado de cada um
    /// </code>
    ///
    /// Só leitura, então não pede <c>devcommands</c>.
    /// </summary>
    internal class NetCommand : SaiyaheimCommand
    {
        public override string Name => "saiya_net";

        public override string Help => "Shows what this client knows about every loaded player.";

        protected override void Execute(string[] args)
        {
            Player local = Player.m_localPlayer;
            if (local == null)
            {
                Print("No player. Join a world first.");
                return;
            }

            List<Player> players = Player.GetAllPlayers();

            Print($"{players.Count} player(s) loaded. " +
                  $"Remote poses {OnOff(SaiyaheimConfig.ShowRemotePoses.Value)}, " +
                  $"remote effects {OnOff(SaiyaheimConfig.ShowRemoteEffects.Value)}.");

            for (int i = 0; i < players.Count; i++)
            {
                PrintPlayer(players[i], local);
            }
        }

        private void PrintPlayer(Player player, Player local)
        {
            if (player == null)
            {
                return;
            }

            string who = ReferenceEquals(player, local)
                ? $"{player.GetPlayerName()} (you)"
                : $"{player.GetPlayerName()} at {Vector3.Distance(player.transform.position, local.transform.position):0} m";

            if (!NetState.IsAvailable(player))
            {
                // Sem ZDO não é "está tudo desligado", é "não dá para saber" — e a diferença é o
                // diagnóstico inteiro. Acontece com jogador ainda entrando no mundo, e é o sintoma
                // de alguém sem o mod.
                Print($"  {who}: no ZDO — cannot read state.");
                return;
            }

            string form = TransformationRegistry.At(NetState.GetFormIndex(player))?.DisplayName ?? "base";

            Print($"  {who}: ki {OnOff(NetState.IsKiEnabled(player))}, " +
                  $"{Flag("flying", NetState.IsFlying(player))}, " +
                  $"{Flag("charging", NetState.IsCharging(player))}, " +
                  $"form {form}, blasts {NetState.GetBlastCount(player)}");
        }

        private static string OnOff(bool value) => value ? "on" : "off";

        private static string Flag(string name, bool value) => value ? name : $"not {name}";
    }
}
