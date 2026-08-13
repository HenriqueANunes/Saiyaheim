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
        /// Um degrau só, por enquanto — a etapa 5 do roadmap pede <b>uma</b> transformação, e a
        /// escada completa depende de decidir qual forma se amarra a qual boss (etapa 7, ver
        /// [[Em Aberto]]). O degrau seguinte é uma linha aqui e uma chamada de
        /// <c>BindTransformation</c> na config.
        /// </summary>
        internal static readonly Transformation[] All =
        {
            new Transformation("ssj", "SSJ", SaiyaheimConfig.Ssj)
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
        /// Quanto do dano de contusão do soco a forma ativa converte em corte. 0 fora de forma,
        /// que é o caso da esmagadora maioria das chamadas — e também o de uma forma que não
        /// tempera o golpe.
        /// </summary>
        internal static float GetPunchSlashFraction(Player player)
        {
            Transformation active = GetActive(player);

            return active == null ? 0f : active.GetPunchSlashFraction();
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
