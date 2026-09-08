using UnityEngine;

namespace Saiyaheim.Util
{
    /// <summary>
    /// Para onde o jogador está olhando, no espaço em que uma pose sabe escrever.
    ///
    /// <b>Por que uma pose precisa disto.</b> O projétil nasce <b>na mão</b>
    /// (<c>KiProjectile.GetOrigin</c>) e voa na direção do olhar. Com o braço travado num alvo
    /// fixo, mirar no céu produz um tiro saindo da mão para cima com o braço apontando para o
    /// horizonte — e é o tipo de erro que só aparece quando alguém atira num Draugr numa torre.
    ///
    /// <b>Os dois eixos não são o mesmo problema.</b> O vertical é óbvio e foi o primeiro a
    /// nascer. O horizontal parecia resolvido sozinho, porque a pose é escrita no espaço do
    /// <i>corpo</i> e o corpo em geral já está virado para a câmera — mas "em geral" não é
    /// "sempre": correndo para um lado e olhando para outro, o corpo segue o movimento e a câmera
    /// não, e é justamente aí que o tiro sai numa direção com a mão apontando para outra.
    ///
    /// <b>Sai em espaço de músculo, não em grau</b>, que é o que permite somar o resultado direto
    /// num alvo de pose. Quanto disso cada pose de fato usa é chave dela — as duas que existem
    /// hoje chamam isto de <c>AimFollowPitch</c> e <c>AimFollowYaw</c>.
    ///
    /// Nasceu dentro do <c>KiBlastPose</c> e saiu de lá quando a pose do Kamehameha precisou da
    /// mesma conta. É geometria da câmera, não desenho de gesto: não há nada aqui que uma das duas
    /// poses queira diferente da outra.
    /// </summary>
    internal static class AimPose
    {
        /// <summary>
        /// Quanto o braço sobe ou desce para acompanhar a mira, de -1 (olhando para os pés) a 1
        /// (olhando para o alto).
        ///
        /// A componente vertical do olhar já é o seno do ângulo de mira. Não precisa virar grau: o
        /// espaço de músculo também é normalizado, e o que se quer aqui é proporção e não ângulo
        /// exato.
        /// </summary>
        internal static float Pitch(Player player)
        {
            return player == null ? 0f : player.GetLookDir().normalized.y;
        }

        /// <summary>
        /// Quanto o gesto gira na horizontal para acompanhar a mira. Positivo é a câmera olhando à
        /// <b>direita</b> de para onde o corpo aponta.
        ///
        /// <b>O denominador é 90°</b> porque é o que uma unidade de músculo vale no swing do
        /// braço: do braço aberto de lado (0) ao braço apontado para frente (1) vai exatamente um
        /// quarto de volta. Passar disso é o braço atravessando o peito, e o clamp existe só para
        /// o jogador olhando para trás — onde não há gesto possível e o certo é parar no limite em
        /// vez de o braço dar a volta.
        /// </summary>
        internal static float Yaw(Player player)
        {
            if (player == null)
            {
                return 0f;
            }

            Vector3 look = player.GetLookDir();
            Vector3 body = player.transform.forward;

            // Só o plano do chão: a parte vertical do olhar já é problema do <see cref="Pitch"/>, e
            // deixá-la aqui faria mirar no céu contar como girar para o lado.
            look.y = 0f;
            body.y = 0f;

            if (look.sqrMagnitude < 0.0001f || body.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            // SignedAngle em torno do "para cima" dá positivo quando o alvo está à direita da
            // referência. Se na tela o braço for para o lado errado, é este sinal que troca.
            float degrees = Vector3.SignedAngle(body, look, Vector3.up);

            return Mathf.Clamp(degrees / 90f, -1f, 1f);
        }
    }
}
