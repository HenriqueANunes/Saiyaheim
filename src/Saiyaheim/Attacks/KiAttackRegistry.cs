using System.Collections.Generic;
using UnityEngine;

namespace Saiyaheim.Attacks
{
    /// <summary>
    /// A lista de ataques que existem, e a pergunta que o resto do mod faz sobre eles: <b>qual está
    /// selecionado agora.</b>
    ///
    /// <b>A seleção é estado do jogador local</b>, e por isso mora aqui num campo estático — ao
    /// contrário da forma ativa, que mora no <c>SEMan</c> de cada jogador porque o mod pergunta a
    /// forma de <i>outro</i> personagem o tempo todo (o power level do alvo é consultado tanto
    /// quanto o do local). Ninguém pergunta qual ataque o vizinho selecionou: no multiplayer cada
    /// cliente dispara o próprio projétil, e o projétil é objeto de rede.
    ///
    /// <b>A seleção não é persistida.</b> Cada sessão começa no primeiro ataque destravado. Salvar
    /// custaria um campo no <c>KiState</c> e um caminho de save a mais; o preço de não salvar é um
    /// toque de tecla por sessão, e só quando existir mais de um ataque.
    /// </summary>
    internal static class KiAttackRegistry
    {
        /// <summary>
        /// A escada, do primeiro ao último. A ordem é a que a tecla de ciclar percorre.
        ///
        /// Um ataque só, por enquanto. O seguinte é uma linha aqui e uma chamada de
        /// <c>BindKiAttack</c> na config — a mesma aposta que a escada de formas fez e que se
        /// pagou: adicionar o SSJ2 custa uma linha.
        /// </summary>
        internal static readonly KiAttack[] All =
        {
            new KiAttack("blast", "Ki Blast", SaiyaheimConfig.KiBlast)
        };

        /// <summary>O ataque escolhido pelo jogador, ou null se ele nunca escolheu nesta sessão.</summary>
        private static KiAttack _selected;

        /// <summary>
        /// Piso comum de cadência: nenhum ataque dispara antes disto, qualquer que seja o cooldown
        /// dele. Ver <c>KiAttack.StartCooldown</c>.
        /// </summary>
        private static float _nextShotAt;

        /// <summary>
        /// O ataque que a tecla de disparar vai usar agora.
        ///
        /// Cai para o primeiro destravado quando a seleção não serve mais — nunca foi feita, ou
        /// aponta para um ataque que travou de novo (<c>saiya_blast lock</c> no meio do playtest).
        /// Devolver um ataque travado aqui faria a tecla recusar em silêncio sem que nada na tela
        /// explicasse por quê.
        /// </summary>
        internal static KiAttack Current(Player player)
        {
            if (_selected != null && _selected.IsUnlocked(player))
            {
                return _selected;
            }

            return FirstUnlocked(player);
        }

        /// <summary>O primeiro ataque destravado da escada, ou null se nenhum está.</summary>
        internal static KiAttack FirstUnlocked(Player player)
        {
            foreach (KiAttack attack in All)
            {
                if (attack.IsUnlocked(player))
                {
                    return attack;
                }
            }

            return null;
        }

        /// <summary>
        /// Passa para o próximo ataque destravado, dando a volta no fim da lista. Devolve o novo
        /// selecionado, ou null se não há nenhum destravado.
        ///
        /// Pula os travados de propósito: ciclar por eles obrigaria a apertar a tecla várias vezes
        /// para chegar ao que dá para usar, e a mensagem de trava tem lugar próprio — a tecla de
        /// <b>disparar</b>, onde ela responde a uma intenção de verdade.
        /// </summary>
        internal static KiAttack SelectNext(Player player)
        {
            KiAttack current = Current(player);
            int start = IndexOf(current);

            for (int step = 1; step <= All.Length; step++)
            {
                KiAttack candidate = All[(start + step) % All.Length];

                if (candidate.IsUnlocked(player))
                {
                    _selected = candidate;
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>Pronto para disparar? O piso comum, não o cooldown de um ataque.</summary>
        internal static bool IsGlobalCooldownReady()
        {
            return Time.time >= _nextShotAt;
        }

        internal static void StartGlobalCooldown()
        {
            _nextShotAt = Time.time + Mathf.Max(0f, SaiyaheimConfig.KiAttackMinimumInterval.Value);
        }

        /// <summary>
        /// O ataque cujo <see cref="KiAttack.Id"/> ou nome casa com <paramref name="name"/>, ou
        /// null. Insensível a caixa: quem digita no console não deve ter que acertar maiúscula.
        /// </summary>
        internal static KiAttack Find(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (KiAttack attack in All)
            {
                if (string.Equals(attack.Id, name, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(attack.DisplayName, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return attack;
                }
            }

            return null;
        }

        /// <summary>Os ataques com a trava desligada por debug. Vazio no caso normal.</summary>
        internal static IEnumerable<KiAttack> Forced()
        {
            foreach (KiAttack attack in All)
            {
                if (attack.IgnoreLocks)
                {
                    yield return attack;
                }
            }
        }

        /// <summary>Posição na escada. -1 para null, que é o que faz o ciclo começar do primeiro.</summary>
        internal static int IndexOf(KiAttack attack)
        {
            if (attack == null)
            {
                return -1;
            }

            for (int i = 0; i < All.Length; i++)
            {
                if (All[i] == attack)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
