using UnityEngine;

namespace Saiyaheim.Attacks
{
    /// <summary>
    /// Para onde o tiro vai, dado de onde ele sai.
    ///
    /// <b>O problema.</b> A mira do jogo é a <b>câmera</b>, e a câmera do Valheim fica atrás e
    /// acima do ombro. O projétil, porém, nasce na mão. Usar <c>GetLookDir()</c> como direção põe
    /// os dois numa reta <i>paralela</i> à da mira, deslocada pela distância entre o olho e a mão —
    /// e uma reta paralela nunca cruza a outra. Resultado: o tiro acerta sempre abaixo (e um pouco
    /// ao lado) da cruz, com o erro crescendo quanto mais perto está o alvo.
    ///
    /// Não é bug do mod: é exatamente o mesmo defeito do arco vanilla, que o
    /// <c>Attack.GetProjectileSpawnPoint</c> do jogo tem por escrever
    /// <c>aimDir = m_character.GetAimDir(spawnPoint)</c> e o <c>Humanoid.GetAimDir</c> ignorar o
    /// <c>fromPoint</c> e devolver o olhar puro.
    ///
    /// <b>A correção: convergência.</b> Em vez de copiar a direção da câmera, descobrimos o
    /// <i>ponto</i> que a câmera está mirando — raycast pela cruz — e apontamos da mão <b>para
    /// aquele ponto</b>. As duas retas passam a se encontrar onde o jogador está olhando, em
    /// qualquer distância.
    ///
    /// É mais do que o Better Archery faz. Ele move o spawn para a mão do arco, zera o
    /// <c>m_projectileAccuracy</c> (a dispersão aleatória do vanilla) e soma um empurrão fixo de
    /// <c>(0, 0.05, 0)</c> na direção — um ajuste que só está certo a <b>uma</b> distância, porque
    /// o erro do paralelismo depende de quão longe está o alvo. Convergência acerta em todas.
    /// </summary>
    internal static class KiAim
    {
        /// <summary>
        /// Distância mínima entre a origem do tiro e o ponto mirado para valer a pena convergir.
        ///
        /// Não é balanceamento, é estabilidade numérica: encostado numa parede o ponto mirado cai
        /// praticamente em cima da mão, e a direção normalizada daquele vetor minúsculo vira ruído
        /// — no limite, aponta para trás. Abaixo disto o olhar puro é a resposta menos errada.
        /// </summary>
        private const float MinConvergeDistance = 2f;

        /// <summary>
        /// Buffer do raycast. Reaproveitado porque isto roda a cada disparo e o
        /// <c>RaycastAll</c> alocaria um array novo toda vez.
        /// </summary>
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[64];

        private static int _mask;
        private static bool _maskReady;

        /// <summary>
        /// A direção em que o projétil deve sair de <paramref name="origin"/> para acertar a cruz.
        /// Devolve <c>Vector3.zero</c> só quando nem o olhar existe — quem chama trata como
        /// "não dá para atirar agora".
        /// </summary>
        internal static Vector3 Resolve(Player player, Vector3 origin)
        {
            Vector3 look = player.GetLookDir().normalized;

            if (look == Vector3.zero || !SaiyaheimConfig.KiAttackAimConvergence.Value)
            {
                return look;
            }

            // A cruz é um elemento fixo no centro da tela, então o raio dela é o da câmera para a
            // frente — o mesmo que o Player.FindHoverObject usa para decidir o que está sob a mira.
            // GameCamera.instance é público; m_camera não é, e não precisa ser.
            GameCamera camera = GameCamera.instance;
            if (camera == null)
            {
                return look;
            }

            float range = Mathf.Max(1f, SaiyaheimConfig.KiAttackAimRange.Value);
            Vector3 camPos = camera.transform.position;
            Vector3 camDir = camera.transform.forward;

            // Sem obstáculo (mirando o céu) o alvo é um ponto longe na reta da câmera. A essa
            // distância o resto do deslocamento olho-mão já é irrelevante.
            Vector3 target = TryFindAimPoint(player, camPos, camDir, range, out Vector3 impact)
                ? impact
                : camPos + camDir * range;

            Vector3 corrected = target - origin;
            if (corrected.sqrMagnitude < MinConvergeDistance * MinConvergeDistance)
            {
                return look;
            }

            corrected.Normalize();

            // Trava de segurança. Mirando o chão aos próprios pés, ou uma parede colada nas costas
            // da câmera, o ponto mirado pode cair ATRÁS da mão — e a direção corrigida apontaria
            // para o jogador. O ângulo limita o quanto a correção pode divergir do olhar; o que
            // passar disso vira uma correção parcial na direção certa, nunca uma inversão.
            float maxCorrection = SaiyaheimConfig.KiAttackAimMaxCorrection.Value;
            if (Vector3.Angle(look, corrected) > maxCorrection)
            {
                return Vector3.RotateTowards(look, corrected, maxCorrection * Mathf.Deg2Rad, 0f)
                    .normalized;
            }

            return corrected;
        }

        /// <summary>
        /// O primeiro obstáculo sob a cruz, ignorando o próprio atirador.
        ///
        /// A máscara é a mesma lista de layers do <c>Projectile.s_rayMaskSolids</c> — de propósito:
        /// o ponto que a mira encontra tem que ser o mesmo em que o projétil vai parar, senão a
        /// correção resolveria a mira contra uma geometria e o tiro morreria em outra.
        /// (Copiada em vez de lida: o campo do jogo é <c>private static</c>.)
        ///
        /// O <c>RaycastNonAlloc</c> não devolve os acertos em ordem, então o mais próximo sai daqui
        /// na mão. O corpo de quem atirou está no caminho porque a câmera fica atrás dele —
        /// descartar por hierarquia pega de uma vez colisor, hitbox e o que mais estiver preso ao
        /// jogador.
        /// </summary>
        private static bool TryFindAimPoint(
            Player player, Vector3 from, Vector3 direction, float range, out Vector3 point)
        {
            point = Vector3.zero;

            if (!_maskReady)
            {
                _mask = LayerMask.GetMask(
                    "Default", "static_solid", "Default_small", "piece", "piece_nonsolid",
                    "terrain", "character", "character_net", "character_ghost", "hitbox",
                    "character_noenv", "vehicle");
                _maskReady = true;
            }

            int count = Physics.RaycastNonAlloc(from, direction, HitBuffer, range, _mask);
            float nearest = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = HitBuffer[i];

                if (candidate.collider == null || IsOwnBody(player, candidate.collider))
                {
                    continue;
                }

                if (candidate.distance < nearest)
                {
                    nearest = candidate.distance;
                    point = candidate.point;
                    found = true;
                }
            }

            return found;
        }

        private static bool IsOwnBody(Player player, Collider collider)
        {
            if (collider.transform.IsChildOf(player.transform))
            {
                return true;
            }

            Rigidbody body = collider.attachedRigidbody;
            return body != null && body.transform.IsChildOf(player.transform);
        }
    }
}
