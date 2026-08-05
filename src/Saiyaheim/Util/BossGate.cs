using System.Collections.Generic;

namespace Saiyaheim.Util
{
    /// <summary>
    /// A trava por boss: "este boss já caiu neste mundo?", respondida pelas <b>global keys</b> do
    /// Valheim.
    ///
    /// <b>Por que global key e não estado do jogador.</b> O jogo já mantém
    /// <c>defeated_eikthyr</c> e companhia, e elas vêm com três coisas prontas que custariam
    /// trabalho de verdade se o mod inventasse a própria trava:
    ///
    /// <list type="bullet">
    /// <item>o servidor <b>sincroniza sozinho</b>, então a etapa 8 não precisa fazer nada aqui;</item>
    /// <item>persistem no save do <b>mundo</b>, não no do personagem;</item>
    /// <item>valem para todos os jogadores do mundo de uma vez.</item>
    /// </list>
    ///
    /// <b>Consequência de design, e ela é escolhida.</b> A chave é do mundo, então quem entra
    /// depois no servidor já chega com o que os outros destravaram — sem ter matado nada. Para um
    /// mundo entre amigos isso é o comportamento desejado: a escada mede o progresso <i>do
    /// mundo</i>, não a lista de troféus de cada um. Se um dia a resposta mudar, o lugar de mexer
    /// é aqui, e <c>Player.HaveUniqueKey</c> é o equivalente por jogador.
    ///
    /// ⚠️ <b>A trava só é consultada na entrada da forma, nunca para derrubá-la.</b> Global key
    /// não é removida em jogo normal, mas o console remove — e perder a forma no meio da luta
    /// porque alguém digitou algo no console seria pior que a inconsistência.
    /// </summary>
    internal static class BossGate
    {
        /// <summary>
        /// Nome legível de cada chave conhecida, para a mensagem que o jogador lê quando a forma
        /// está travada. "Defeat Eikthyr" diz o que fazer; "defeated_eikthyr" não.
        ///
        /// ⚠️ <b>Duas chaves não têm o nome do boss:</b> <c>defeated_dragon</c> é a Moder e
        /// <c>defeated_goblinking</c> é o Yagluth. Errar isso no <c>.cfg</c> não dá erro nenhum —
        /// dá uma forma que nunca destrava.
        ///
        /// São só os cinco bosses clássicos porque são os únicos que existem como string na
        /// assembly do jogo. Rainha e Fader guardam a chave deles no <b>prefab</b>, que é dado de
        /// asset e não sai por decompilação — quando a escada chegar lá, confirmar em jogo com
        /// <c>saiya_form gate</c> antes de escrever no <c>.cfg</c>.
        /// </summary>
        private static readonly Dictionary<string, string> BossNames =
            new Dictionary<string, string>
            {
                { "defeated_eikthyr", "Eikthyr" },
                { "defeated_gdking", "The Elder" },
                { "defeated_bonemass", "Bonemass" },
                { "defeated_dragon", "Moder" },
                { "defeated_goblinking", "Yagluth" },
            };

        /// <summary>As chaves que este código sabe nomear, na ordem dos biomas.</summary>
        internal static IEnumerable<KeyValuePair<string, string>> Known => BossNames;

        /// <summary>
        /// Todas as global keys deste mundo, inclusive as que não são de boss. É o caminho para
        /// descobrir a chave da Rainha e a do Fader, que não existem como string na assembly.
        /// </summary>
        internal static List<string> WorldKeys()
        {
            ZoneSystem zones = ZoneSystem.instance;

            return zones == null ? new List<string>() : zones.GetGlobalKeys();
        }

        /// <summary>
        /// A trava está aberta? Chave vazia quer dizer <b>sem trava</b>, que é o default de toda
        /// forma nova — uma forma só entra na escada de bosses quando alguém decide a qual boss
        /// ela pertence.
        /// </summary>
        internal static bool IsOpen(string globalKey)
        {
            if (string.IsNullOrEmpty(globalKey))
            {
                return true;
            }

            ZoneSystem zones = ZoneSystem.instance;
            if (zones == null)
            {
                // Fora de um mundo não há chave nenhuma para consultar. Tratar como travado é
                // seguro: quem chama é a entrada da forma, e não dá para transformar sem mundo.
                return false;
            }

            // GetGlobalKey(string) já normaliza para minúsculas, então a caixa que o jogador
            // escrever no .cfg não importa.
            return zones.GetGlobalKey(globalKey);
        }

        /// <summary>
        /// O que falta fazer, em uma frase, ou null se não falta nada. É o texto que aparece no
        /// meio da tela quando a tecla de transformar é recusada.
        /// </summary>
        internal static string DescribeLock(string globalKey)
        {
            if (IsOpen(globalKey))
            {
                return null;
            }

            return $"Defeat {DisplayName(globalKey)} first.";
        }

        /// <summary>
        /// Nome do boss, ou a própria chave quando ela não é conhecida. Devolver a chave crua é de
        /// propósito: uma chave escrita errada no <c>.cfg</c> aparece na tela como está escrita,
        /// que é a informação de que se precisa para consertar.
        /// </summary>
        internal static string DisplayName(string globalKey)
        {
            if (string.IsNullOrEmpty(globalKey))
            {
                return "nothing";
            }

            return BossNames.TryGetValue(globalKey.ToLowerInvariant(), out string name)
                ? name
                : globalKey;
        }
    }
}
