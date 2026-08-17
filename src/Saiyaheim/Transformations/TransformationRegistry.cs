using System.Collections.Generic;

namespace Saiyaheim.Transformations
{
    /// <summary>
    /// A lista de formas que existem, e a pergunta que o resto do mod faz sobre elas:
    /// <b>qual está ativa neste jogador e quanto ela multiplica.</b>
    ///
    /// <b>A resposta sai do <c>SEMan</c> do próprio jogador</b>, não de um campo estático do
    /// mod. Parece rodeio e não é: o <c>PowerLevel</c> é consultado para o jogador que está sendo
    /// atingido tanto quanto para o local, e no multiplayer (etapa 8) o status effect é o que
    /// sincroniza. Um "forma ativa" global mentiria sobre todo mundo menos um.
    /// </summary>
    internal static class TransformationRegistry
    {
        /// <summary>
        /// A escada, <b>da mais fraca para a mais forte</b>. A ordem não é cosmética: é ela que as
        /// teclas de degrau percorrem, e é ela que define qual é o "mais alto destravado" que a
        /// tecla de transformar direto procura.
        ///
        /// Dois degraus. O terceiro é uma linha aqui e uma chamada de
        /// <c>BindTransformation</c> na config — a promessa de "forma é dado, não código" foi
        /// cobrada quando o SSJ2 entrou, e o preço foi exatamente esse.
        ///
        /// A qual boss cada forma acima do SSJ2 se amarra continua aberto, e depende de decidir
        /// quais formas existirão — ver [[Progressão por Bosses]] e [[Em Aberto]].
        /// </summary>
        internal static readonly Transformation[] All =
        {
            new Transformation("ssj", "SSJ", SaiyaheimConfig.Ssj),
            new Transformation("ssj2", "SSJ2", SaiyaheimConfig.Ssj2)
        };

        /// <summary>
        /// A forma cujo <see cref="Transformation.Id"/> ou nome casa com <paramref name="name"/>,
        /// ou null. Insensível a caixa: quem digita no console não deve ter que acertar
        /// maiúscula.
        /// </summary>
        internal static Transformation Find(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (Transformation form in All)
            {
                if (string.Equals(form.Id, name, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(form.DisplayName, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return form;
                }
            }

            return null;
        }

        /// <summary>As formas com a trava desligada por debug. Vazio no caso normal.</summary>
        internal static IEnumerable<Transformation> Unlocked()
        {
            foreach (Transformation form in All)
            {
                if (form.IgnoreLocks)
                {
                    yield return form;
                }
            }
        }

        /// <summary>
        /// O degrau acima de <paramref name="current"/>, ou null se já está no topo. Null como
        /// entrada significa forma base, então devolve o primeiro degrau.
        /// </summary>
        internal static Transformation Next(Transformation current)
        {
            if (current == null)
            {
                return All.Length > 0 ? All[0] : null;
            }

            int index = System.Array.IndexOf(All, current);

            return index >= 0 && index + 1 < All.Length ? All[index + 1] : null;
        }

        /// <summary>
        /// O degrau abaixo de <paramref name="current"/>, ou null para a forma base. Descer do
        /// primeiro degrau é voltar à base, e é por isso que null é resposta legítima aqui.
        /// </summary>
        internal static Transformation Previous(Transformation current)
        {
            int index = current == null ? -1 : System.Array.IndexOf(All, current);

            return index > 0 ? All[index - 1] : null;
        }

        /// <summary>
        /// A forma mais alta que este jogador já destravou, ou null se nenhuma.
        ///
        /// Varre a escada inteira em vez de parar na primeira travada: se um dia o desbloqueio
        /// deixar de ser monotônico (uma forma amarrada a um boss opcional, por exemplo), o
        /// "mais alto destravado" continua sendo a resposta certa.
        /// </summary>
        internal static Transformation HighestUnlocked(Player player)
        {
            Transformation highest = null;

            foreach (Transformation form in All)
            {
                if (form.IsUnlocked(player))
                {
                    highest = form;
                }
            }

            return highest;
        }

        /// <summary>
        /// A forma nesta posição da escada, ou null. É a volta do <see cref="IndexOf"/>, e existe
        /// porque o índice é o que atravessa a rede: o canal do <c>NetState</c> carrega a posição
        /// na escada, não o id, para caber em oito bits.
        ///
        /// -1 é forma base e devolve null, como o índice de qualquer coisa fora da escada.
        /// </summary>
        internal static Transformation At(int index)
        {
            return index >= 0 && index < All.Length ? All[index] : null;
        }

        /// <summary>Posição na escada, ou -1. Usado para comparar dois degraus.</summary>
        internal static int IndexOf(Transformation form)
        {
            return form == null ? -1 : System.Array.IndexOf(All, form);
        }

        /// <summary>
        /// Paga XP de maestria pelo tempo segurando <paramref name="active"/> — para ela
        /// <b>e para todo degrau abaixo dela</b>.
        ///
        /// <b>Por que a escada inteira abaixo, e na taxa cheia.</b> A escada é linear: quem segura
        /// o SSJ2 está segurando o SSJ por dentro, e não faz sentido a maestria do degrau que ele
        /// já domina congelar no instante em que ele passa a viver no degrau de cima. Sem isto o
        /// jogador que subiu é <b>punido</b> por usar a forma nova: o dreno do SSJ, para o qual ele
        /// volta toda vez que a barra aperta, para de melhorar. A alternativa era vazar só uma
        /// fração; foi descartada porque a fração transformaria o degrau baixo numa segunda barra
        /// de grind, e a moeda da maestria — dreno menor — já é onde os degraus se diferenciam.
        ///
        /// <b>Cada forma na taxa dela</b>: o XP sai do <c>MasteryXpPerSecond</c> de <i>quem
        /// recebe</i>, não do da forma ativa. Continua valendo que nenhum número de balanceamento
        /// é compartilhado entre formas — cada degrau é dono da própria velocidade de treino.
        ///
        /// Não checa se o degrau abaixo está destravado, de propósito: hoje a escada é monotônica
        /// e a checagem seria sempre verdadeira; se um dia deixar de ser, XP numa skill que o
        /// jogador ainda não pode usar não faz mal nenhum — ela só é lida quando ele entra
        /// naquela forma.
        /// </summary>
        internal static void RaiseMastery(Player player, Transformation active, float seconds)
        {
            int top = IndexOf(active);

            for (int i = 0; i <= top; i++)
            {
                All[i].RaiseMastery(player, seconds);
            }
        }

        internal static void Register()
        {
            foreach (Transformation form in All)
            {
                form.Register();
            }
        }

        /// <summary>A forma ativa neste jogador, ou null se ele não está transformado.</summary>
        internal static Transformation GetActive(Player player)
        {
            SEMan seman = player == null ? null : player.GetSEMan();
            if (seman == null)
            {
                return null;
            }

            foreach (Transformation form in All)
            {
                if (seman.HaveStatusEffect(form.NameHashValue))
                {
                    return form;
                }
            }

            return null;
        }

        /// <summary>
        /// Quanto multiplicar o poder de combate deste jogador. 1 quando ele não está transformado,
        /// que é o caso da esmagadora maioria das chamadas.
        ///
        /// ⚠️ <b>Não pode ler power level nenhum</b>, direta ou indiretamente: é o
        /// <c>PowerLevel.GetKiCombatRaw</c> quem chama, e uma leitura de volta fecharia recursão
        /// infinita. Por isso a resposta sai só de config e do <c>SEMan</c>.
        /// </summary>
        internal static float GetPowerMultiplier(Player player)
        {
            Transformation active = GetActive(player);

            return active == null ? 1f : active.GetPowerMultiplier();
        }

        /// <summary>
        /// Como a forma ativa reparte o soco entre corte e raio. Zero nos dois fora de forma, que
        /// é o caso da esmagadora maioria das chamadas — e também o de uma forma que não tempera
        /// o golpe.
        /// </summary>
        internal static void GetPunchSplit(Player player, out float slash, out float lightning)
        {
            Transformation active = GetActive(player);

            if (active == null)
            {
                slash = 0f;
                lightning = 0f;
                return;
            }

            active.GetPunchSplit(out slash, out lightning);
        }

        /// <summary>Nome da forma ativa, para log e HUD. Null se não há forma.</summary>
        internal static string GetActiveName(Player player)
        {
            return GetActive(player)?.DisplayName;
        }

        /// <summary>Os hashes de todas as formas. Usado pelo manager para desligar o que estiver ligado.</summary>
        internal static IEnumerable<int> AllNameHashes()
        {
            foreach (Transformation form in All)
            {
                yield return form.NameHashValue;
            }
        }
    }
}
