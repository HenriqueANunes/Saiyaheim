using Saiyaheim.Attacks;
using Saiyaheim.Ki;
using Saiyaheim.Net;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Transmission
{
    /// <summary>
    /// O Instant Transmission curto (etapa 16): uma tecla leva o jogador para onde a cruz mira, até
    /// <c>BlinkMaxDistance</c>, na hora e sem tela de carregamento. Mesma técnica do mapa, então
    /// a mesma runestone ensina os dois.
    ///
    /// <b>Sem o <c>TeleportTo</c> da vanilla, de propósito.</b> Ele liga o <c>IsTeleporting</c>, e é
    /// isso que escurece a tela (<c>Hud.UpdateBlackScreen</c>) e segura o jogador 2 s. Aqui a
    /// posição é escrita direto, e o <c>ZSyncTransform</c> dos outros encaixa o salto sozinho (acima
    /// de 5 m ele não interpola).
    ///
    /// <b>As regras, decididas pelo Henrique em 2026-10-15:</b>
    /// <list type="bullet">
    /// <item>chega <b>no ponto mirado</b>, mesmo no ar: a pé, cai dali;</item>
    /// <item><b>para antes do obstáculo</b> — parede, árvore, chão —, e nunca dentro de geometria;</item>
    /// <item><b>minério não bloqueia</b>: o voo já carrega minério pelo mundo, e 30 m não furam nada
    /// além disso. A regra de portal fica só no teleporte do mapa.</item>
    /// </list>
    ///
    /// Custo: <c>BlinkKiCost</c> fixo mais o mesmo ki por 100 m do mapa, que cai com o Power Level.
    /// </summary>
    internal static class TransmissionBlink
    {
        /// <summary>Altura do peito acima dos pés: de onde a mira sai e para onde o salto aponta.</summary>
        private const float ChestHeight = 1.2f;

        /// <summary>Raio do corpo para o caminho e para o espaço livre no destino.</summary>
        private const float BodyRadius = 0.4f;

        /// <summary>Altura do jogador em pé, para o teste de espaço livre.</summary>
        private const float BodyHeight = 1.8f;

        /// <summary>Folga até o obstáculo que parou o salto.</summary>
        private const float ObstacleGap = 0.3f;

        /// <summary>
        /// Recuo a cada tentativa quando o destino não cabe o corpo. Pequeno o bastante para não
        /// jogar fora metade do salto num canto apertado.
        /// </summary>
        private const float StepBack = 0.5f;

        /// <summary>Abaixo disto o salto não acontece: seria pagar ki para ficar no lugar.</summary>
        private const float MinDistance = 2f;

        private static int _mask;
        private static bool _maskReady;

        private static float _readyAt;

        internal static void Update(Player player)
        {
            if (player == null || !Hotkey.IsDown(SaiyaheimConfig.TransmissionBlinkKey) ||
                !InputGuard.AcceptsInput() || Minimap.IsOpen() ||
                !InstantTransmission.IsLearned(player))
            {
                return;
            }

            TryBlink(player);
        }

        private static void TryBlink(Player player)
        {
            if (player.IsDead() || player.IsTeleporting() || player.InCutscene() || Time.time < _readyAt)
            {
                return;
            }

            if (!KiManager.IsEnabled)
            {
                Message(player, "Ki is off.");
                return;
            }

            Vector3 feet = player.transform.position;
            Vector3 chest = feet + Vector3.up * ChestHeight;
            Vector3 direction = KiAim.Resolve(player, chest);
            if (direction == Vector3.zero)
            {
                return;
            }

            if (!TryFindDestination(chest, direction, out Vector3 destination, out float distance))
            {
                Message(player, "No room there.");
                return;
            }

            float cost = Mathf.Max(0f, SaiyaheimConfig.TransmissionBlinkKiCost.Value) +
                         InstantTransmission.GetKiPer100m(player) * distance / 100f;

            if (!KiManager.TryConsume(cost))
            {
                Message(player, "Not enough ki.");
                return;
            }

            if (player.IsAttached())
            {
                player.AttachStop();
            }

            Move(player, destination);
            _readyAt = Time.time + Mathf.Max(0f, SaiyaheimConfig.TransmissionBlinkCooldown.Value);

            NetState.PublishBlink(player);

            SaiyaheimPlugin.LogVerbose($"Instant Transmission (short): {distance:0.#} m for {cost:0.#} ki.");
        }

        /// <summary>
        /// Onde os pés chegam. O corpo viaja como uma esfera na altura do peito e para antes do
        /// primeiro obstáculo; se o ponto final não couber o corpo inteiro em pé, recua pelo mesmo
        /// caminho até caber. Perto do chão, os pés pousam nele em vez de afundar.
        /// </summary>
        private static bool TryFindDestination(
            Vector3 chest, Vector3 direction, out Vector3 destination, out float distance)
        {
            int mask = Mask();
            distance = Mathf.Max(0f, SaiyaheimConfig.TransmissionBlinkMaxDistance.Value);

            if (Physics.SphereCast(chest, BodyRadius, direction, out RaycastHit hit, distance, mask,
                    QueryTriggerInteraction.Ignore))
            {
                distance = hit.distance - ObstacleGap;
            }

            for (; distance >= MinDistance; distance -= StepBack)
            {
                Vector3 point = chest + direction * distance;
                Vector3 feet = point - Vector3.up * ChestHeight;

                // Mirando o chão, o peito para logo acima dele e os pés ficariam enterrados.
                if (Physics.Raycast(point, Vector3.down, out RaycastHit ground, ChestHeight, mask,
                        QueryTriggerInteraction.Ignore))
                {
                    feet = ground.point;
                }

                if (Fits(feet, mask))
                {
                    destination = feet;
                    return true;
                }
            }

            destination = Vector3.zero;
            return false;
        }

        private static bool Fits(Vector3 feet, int mask)
        {
            // A cápsula começa um pouco acima dos pés: encostar no chão em que se pousa não é
            // estar dentro dele.
            Vector3 bottom = feet + Vector3.up * (BodyRadius + 0.1f);
            Vector3 top = feet + Vector3.up * (BodyHeight - BodyRadius);

            return !Physics.CheckCapsule(bottom, top, BodyRadius, mask, QueryTriggerInteraction.Ignore);
        }

        private static void Move(Player player, Vector3 feet)
        {
            Rigidbody body = player.GetComponent<Rigidbody>();

            player.transform.position = feet;
            if (body != null)
            {
                body.position = feet;
                body.linearVelocity = Vector3.zero;
            }

            // A queda conta a partir do ponto mais alto do ar. Sem isto, saltar de um morro para o
            // vale cobraria a altura do morro ao pousar; chegando no ar, a queda conta dali.
            GameAccess.SetMaxAirAltitude(player, feet.y);
        }

        /// <summary>
        /// O que para o salto: terreno, construção, árvore, pedra, barco. Criatura e jogador não —
        /// atravessar um inimigo é justamente para que serve.
        /// </summary>
        private static int Mask()
        {
            if (!_maskReady)
            {
                _mask = LayerMask.GetMask(
                    "Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
                _maskReady = true;
            }

            return _mask;
        }

        private static void Message(Player player, string message)
        {
            player.Message(MessageHud.MessageType.Center, message);
        }
    }
}
