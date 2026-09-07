using System.Collections.Generic;
using Saiyaheim.Net;
using UnityEngine;

namespace Saiyaheim.Attacks
{
    /// <summary>
    /// A fila de projéteis que ainda faltam sair de um disparo já pago.
    ///
    /// <b>É como o mod tem feixe sem o jogo ter feixe.</b> A decisão de [[Decisões Tomadas]] dizia
    /// que o Valheim não tinha nada parecido com laser sustentado, e o dump de prefabs a pôs em
    /// revisão ao achar o <c>projectile_beam</c> do Yagluth. O playtest de 2026-09-07 fechou a
    /// revisão pelo meio: o prefab existe e <b>não</b> é um raio sustentado — o boss dispara vários
    /// em sequência, e o que se lê como feixe é a fila. Ou seja, a resposta não era um sistema de
    /// renderização novo; era repetir o disparo que o mod já sabia fazer.
    ///
    /// Por isso esta classe é tão pequena, e por isso o feixe é <b>config</b> e não código:
    /// <c>BeamCount</c> e <c>BeamInterval</c> decidem se sai um raio ou uma fila de bolinhas, e
    /// essa é uma pergunta que só a tela responde.
    ///
    /// <b>Estado do jogador local, e só dele.</b> Mesmo argumento do <c>KiAttack.ReadyAt</c>: cada
    /// cliente dispara os próprios projéteis, e projétil é objeto de rede que replica sozinho. O
    /// que a máquina do vizinho precisa saber sobre o feixe é a pose, e essa vai pelo contador do
    /// <see cref="NetState.PublishBlast"/> — um por projétil, de propósito, que é o que mantém o
    /// braço levantado do primeiro ao último em vez de ele piscar no meio.
    ///
    /// <b>Sem coroutine.</b> Uma <c>MonoBehaviour</c> por feixe seria um objeto de Unity para
    /// guardar três campos, e coroutine morre junto com o componente que a hospeda — o que aqui
    /// significaria meio feixe sumindo em qualquer troca de cena. Uma lista percorrida no
    /// <c>Update</c> que já existe custa menos e é interrompível de propósito, no
    /// <see cref="Cancel"/>.
    /// </summary>
    internal static class KiBeam
    {
        private sealed class Pending
        {
            internal Player Player;
            internal KiAttack Attack;

            /// <summary>Projéteis que ainda faltam. O primeiro já saiu quando isto é criado.</summary>
            internal int Remaining;

            /// <summary>Instante (<c>Time.time</c>) do próximo projétil.</summary>
            internal float NextShotAt;

            /// <summary>
            /// A carga com que este feixe saiu, de 0 a 1. Guardada porque o feixe já foi pago: a
            /// grossura dos projéteis que faltam é a da carga que o comprou, e não a de uma carga
            /// nova que o jogador possa ter começado enquanto este ainda sai.
            /// </summary>
            internal float ChargeRatio;
        }

        private static readonly List<Pending> Beams = new List<Pending>();

        /// <summary>
        /// Este jogador ainda tem projéteis para sair? Quem pergunta é a tecla de disparar, para
        /// não deixar dois feixes se atravessarem quando o <c>Cooldown</c> for menor que a
        /// duração do feixe — o que é fácil de configurar por acidente, e na tela vira dois feixes
        /// saindo da mesma mão em direções diferentes.
        /// </summary>
        internal static bool IsBusy(Player player)
        {
            foreach (Pending beam in Beams)
            {
                if (beam.Player == player)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Agenda os projéteis que faltam depois do primeiro. Não faz nada num ataque de tiro
        /// único, que é o caso do ki blast e o motivo de o resto do mod não precisar saber que
        /// feixe existe.
        /// </summary>
        /// <param name="remaining">
        /// Quantos projéteis ainda faltam. Vem de quem disparou e não do <c>BeamCount</c>: num
        /// ataque carregado o comprimento do feixe é o da carga que o jogador segurou, e a chave é
        /// só o teto dela.
        /// </param>
        internal static void Schedule(Player player, KiAttack attack, int remaining, float ratio)
        {
            if (player == null || remaining <= 0)
            {
                return;
            }

            Beams.Add(new Pending
            {
                Player = player,
                Attack = attack,
                Remaining = remaining,
                NextShotAt = Time.time + attack.GetBeamInterval(),
                ChargeRatio = ratio,
            });
        }

        /// <summary>
        /// Solta os projéteis cujo instante chegou. Chamado do <c>KiAttackManager.Update</c>, junto
        /// da leitura das teclas.
        ///
        /// <b>Um projétil por passada, não todos os atrasados.</b> Num soluço de frame rate a fila
        /// atrasa e o feixe fica um pouco mais comprido; recuperar o atraso disparando três de uma
        /// vez os empilharia no mesmo ponto do espaço, que é a única das duas saídas que aparece
        /// na tela.
        /// </summary>
        internal static void Update()
        {
            for (int i = Beams.Count - 1; i >= 0; i--)
            {
                Pending beam = Beams[i];

                if (!IsAlive(beam.Player))
                {
                    Beams.RemoveAt(i);
                    continue;
                }

                if (Time.time < beam.NextShotAt)
                {
                    continue;
                }

                // A mira é resolvida agora, dentro do Fire, e não guardada de quando a tecla foi
                // apertada. É o que faz o feixe acompanhar a cruz enquanto sai, em vez de apontar
                // para onde o jogador estava olhando meio segundo atrás.
                if (!KiProjectile.Fire(beam.Player, beam.Attack, beam.ChargeRatio))
                {
                    // Já foi pago no primeiro projétil, então não há o que devolver. Cortar a fila
                    // é o certo: se o prefab sumiu do ZNetScene, os que faltam falhariam igual e
                    // encheriam o log com a mesma linha doze vezes.
                    SaiyaheimPlugin.LogVerbose(
                        $"Ki attack '{beam.Attack.Id}': beam cut short, a projectile failed to fire.");
                    Beams.RemoveAt(i);
                    continue;
                }

                // Um por projétil: o contador é o que segura a pose de disparo levantada na tela de
                // todo mundo, e o KiBlastPose só empurra o HoldUntil para a frente em vez de
                // reiniciar o peso. Ver KiBlastPose.
                NetState.PublishBlast(beam.Player);

                beam.Remaining--;
                beam.NextShotAt += beam.Attack.GetBeamInterval();

                if (beam.Remaining <= 0)
                {
                    Beams.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Corta os feixes deste jogador. Existe para o momento em que ele morre ou sai do mundo
        /// no meio de um feixe — o resto sairia do corpo caído, ou de um <c>Player</c> destruído.
        /// </summary>
        internal static void Cancel(Player player)
        {
            for (int i = Beams.Count - 1; i >= 0; i--)
            {
                if (Beams[i].Player == player)
                {
                    Beams.RemoveAt(i);
                }
            }
        }

        private static bool IsAlive(Player player)
        {
            return player != null && !player.IsDead() && !player.IsTeleporting();
        }
    }
}
