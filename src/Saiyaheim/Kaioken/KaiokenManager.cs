using Saiyaheim.Attacks;
using Saiyaheim.Ki;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Kaioken
{
    /// <summary>
    /// A tecla do Kaioken, as condições para ligar e as razões para cair (etapa 14).
    ///
    /// <b>Duas teclas, no desenho de T/Shift+T</b>: o primeiro toque liga no x2, cada toque seguinte
    /// sobe um tier até o maior aprendido, e a mesma tecla com Shift desliga. Segurar para desligar
    /// saiu em 2026-10-05, a pedido do Henrique; com ele, o toque só podia ser decidido ao soltar.
    ///
    /// Roda no <c>Update</c> do plugin, como o <c>TransformationManager</c>, e pelo mesmo motivo:
    /// tirar o status effect de dentro do <c>SEMan.Update</c> estouraria o laço dele.
    ///
    /// <b>Não é persistido</b>, como a forma: entra-se no mundo sempre sem Kaioken.
    /// </summary>
    internal static class KaiokenManager
    {
        private static readonly SE_Kaioken[] Templates = new SE_Kaioken[KaiokenRegistry.All.Length];
        private static SE_KaiokenExhaustion _exhaustionTemplate;

        internal static bool IsExhausted(Player player)
        {
            SEMan seman = player == null ? null : player.GetSEMan();
            return seman != null && seman.HaveStatusEffect(SE_KaiokenExhaustion.NameHashValue);
        }

        internal static void Update(Player player)
        {
            if (player == null)
            {
                KaiokenSkin.Tick(null, null);
                return;
            }

            SEMan seman = player.GetSEMan();
            if (seman == null)
            {
                return;
            }

            KaiokenTier active = KaiokenRegistry.GetActive(player);

            // Antes de qualquer saída antecipada: desligar neste frame devolve a pele no próximo.
            KaiokenSkin.Tick(player, active);

            if (active != null)
            {
                if (player.GetStamina() <= 0f)
                {
                    // Antes da vida: os dois no mesmo frame terminam em Exaustão, que é o castigo
                    // maior e o que o jogador precisa saber.
                    Stop(player, seman, null);
                    Exhaust(player, seman);
                    return;
                }

                string stopReason = GetStopReason(player);
                if (stopReason != null)
                {
                    Stop(player, seman, stopReason);
                    return;
                }
            }

            HandleInput(player, seman, active);
        }

        private static void HandleInput(Player player, SEMan seman, KaiokenTier active)
        {
            if (!InputGuard.AcceptsInput())
            {
                return;
            }

            if (Hotkey.IsDown(SaiyaheimConfig.KaiokenOffKey))
            {
                if (active != null)
                {
                    Stop(player, seman, "Kaioken off");
                }

                return;
            }

            if (Hotkey.IsDown(SaiyaheimConfig.KaiokenKey))
            {
                Tap(player, seman, active);
            }
        }

        /// <summary>Liga no primeiro tier aprendido, ou sobe um. No topo não faz nada, como a forma.</summary>
        private static void Tap(Player player, SEMan seman, KaiokenTier active)
        {
            KaiokenTier next = KaiokenRegistry.NextLearned(player, active);

            if (next == null)
            {
                if (active == null)
                {
                    // Nada aprendido: dizer onde procurar, senão a tecla parece quebrada.
                    Message(player, KiAttack.NotLearnedMessage);
                }

                return;
            }

            TryStart(player, seman, next);
        }

        /// <summary>
        /// Motivo para o Kaioken cair sozinho, ou null. A stamina no zero não está aqui: ela tem
        /// consequência própria, a Exaustão, e é tratada antes no <see cref="Update"/>.
        /// </summary>
        private static string GetStopReason(Player player)
        {
            if (!KiManager.IsEnabled)
            {
                // Sem ki o Kaioken não multiplica nada (o poder de combate do ki é o único que ele
                // toca), e seguir cobrando stamina e vida seria só castigo.
                return "Ki off — Kaioken fades.";
            }

            if (player.IsDead() || player.IsSleeping() || player.InCutscene())
            {
                return "";
            }

            if (player.GetHealthPercentage() < SaiyaheimConfig.KaiokenHealthFloor.Value)
            {
                return "Your body gives out — Kaioken off.";
            }

            return null;
        }

        private static bool TryStart(Player player, SEMan seman, KaiokenTier tier)
        {
            if (!KiManager.IsEnabled)
            {
                Message(player, "Turn ki on to use Kaioken.");
                return false;
            }

            if (IsExhausted(player))
            {
                Message(player, "Too exhausted for Kaioken.");
                return false;
            }

            if (player.GetStamina() <= 0f)
            {
                Message(player, "Not enough stamina for Kaioken.");
                return false;
            }

            if (player.GetHealthPercentage() < SaiyaheimConfig.KaiokenHealthFloor.Value)
            {
                Message(player, "Too hurt for Kaioken.");
                return false;
            }

            if (player.IsDead() || player.IsSleeping() || player.IsTeleporting() || player.InCutscene())
            {
                return false;
            }

            // Um tier de cada vez: dois ativos multiplicariam o poder duas vezes, em silêncio.
            float maxBefore = KiManager.Max;
            RemoveAll(seman);
            seman.AddStatusEffect(GetTemplate(tier));

            // O que a barra cresceu já entra cheio: subir de tier soma só a diferença, então o
            // total emprestado é sempre o bônus do tier ativo. Descer (só o menu radial desce)
            // encolhe a barra, e o corte sai do emprestado.
            float grown = KiManager.Max - maxBefore;
            if (grown >= 0f)
            {
                KiManager.GrantTemporary(grown);
            }
            else
            {
                KiManager.ShrinkToMax();
            }

            // O mesmo gesto de transformar; a explosão de aura sai do TransformationEffects quando o
            // tier novo chega à rede.
            Transformations.TransformationEffects.OnKaiokenUp(player);

            float margin = tier.GetMargin(player);
            Message(player, $"{tier.DisplayName}!");
            SaiyaheimPlugin.LogVerbose(
                $"{tier.DisplayName}: x{tier.GetPowerMultiplier():0.##} power, margin {margin:0.#} " +
                $"(skill {KaiokenSkill.GetLevel(player):0.#}), {tier.GetStaminaPerSecond(margin):0.##} stamina/s, " +
                $"{tier.GetHealthPerSecond(margin):0.##} hp/s.");

            return true;
        }

        /// <summary>
        /// Desliga, devolve o ki emprestado que não foi gasto e corta o que ficou acima do teto normal. <paramref name="message"/> vazio ou
        /// null não mostra nada.
        /// </summary>
        private static void Stop(Player player, SEMan seman, string message)
        {
            RemoveAll(seman);
            KiManager.ExpireTemporary();
            SaiyaheimPlugin.LogVerbose($"Kaioken off. {message}");

            Message(player, message);
        }

        /// <summary>
        /// Liga direto num tier, sem passar pelos de baixo, ou troca para ele — inclusive para
        /// baixo. O menu radial usa. O tier precisa estar aprendido; o tier já ativo é recusado.
        /// Devolve se ligou, que é o "fecha o menu?" do <c>SaiyaRadial.Leaf</c>.
        /// </summary>
        internal static bool StartTier(Player player, KaiokenTier tier)
        {
            SEMan seman = player == null ? null : player.GetSEMan();

            if (seman == null || tier == null || !tier.IsLearned(player) ||
                KaiokenRegistry.GetActive(player) == tier)
            {
                return false;
            }

            return TryStart(player, seman, tier);
        }

        /// <summary>Desliga de fora — o console e o menu radial usam. Devolve false se não estava ligado.</summary>
        internal static bool StopNow(Player player)
        {
            SEMan seman = player == null ? null : player.GetSEMan();
            if (seman == null || KaiokenRegistry.GetActive(player) == null)
            {
                return false;
            }

            Stop(player, seman, "Kaioken off");
            return true;
        }

        private static void Exhaust(Player player, SEMan seman)
        {
            float seconds = SaiyaheimConfig.KaiokenExhaustionSeconds.Value;
            if (seconds <= 0f)
            {
                Message(player, "Out of stamina — Kaioken off.");
                return;
            }

            if (_exhaustionTemplate == null)
            {
                _exhaustionTemplate = SE_KaiokenExhaustion.CreateTemplate();
            }

            StatusEffect effect = seman.AddStatusEffect(_exhaustionTemplate, resetTime: true);
            if (effect != null)
            {
                // Na instância ativa, e não no template: o Clone() congelaria o .cfg.
                effect.m_ttl = seconds;
            }

            Message(player, "Exhausted!");
        }

        private static void RemoveAll(SEMan seman)
        {
            foreach (int hash in KaiokenRegistry.AllNameHashes())
            {
                seman.RemoveStatusEffect(hash, quiet: true);
            }
        }

        private static SE_Kaioken GetTemplate(KaiokenTier tier)
        {
            if (Templates[tier.Index] == null)
            {
                Templates[tier.Index] = SE_Kaioken.CreateTemplate(tier);
            }

            return Templates[tier.Index];
        }

        private static void Message(Player player, string message)
        {
            if (player == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            player.Message(MessageHud.MessageType.Center, message);
        }
    }
}
