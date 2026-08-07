using Saiyaheim.Ki;
using Saiyaheim.Util;

namespace Saiyaheim.Attacks
{
    /// <summary>
    /// As duas teclas dos ataques de ki: disparar e trocar.
    ///
    /// Roda no <c>Update</c> do plugin, junto do voo e das transformações. Aqui não há o motivo
    /// forte que obriga o voo a isso (mexer no <c>SEMan</c> de dentro do próprio efeito), mas
    /// leitura de tecla tem que ser por frame — um tick fixo perderia toques.
    ///
    /// <b>A ordem das checagens é a ordem em que o jogador quer ser informado.</b> Trava, ki
    /// desligado e ki insuficiente <b>falam</b>; cooldown e estado (morto, dormindo) ficam em
    /// silêncio. A regra: recusa que o jogador pode consertar merece mensagem; recusa que se
    /// conserta sozinha em meio segundo vira spam na tela, porque a tecla de atirar é apertada
    /// repetidamente por construção.
    /// </summary>
    internal static class KiAttackManager
    {
        internal static void Update(Player player)
        {
            if (player == null || !InputGuard.AcceptsInput())
            {
                return;
            }

            // O atalho com Shift vem primeiro, por simetria com o TransformationManager. O Hotkey
            // exige os modificadores exatos, então V nunca dispara com Shift segurado.
            if (Hotkey.IsDown(SaiyaheimConfig.CycleKiAttackKey))
            {
                Cycle(player);
            }
            else if (Hotkey.IsDown(SaiyaheimConfig.FireKiAttackKey))
            {
                Fire(player);
            }
        }

        private static void Fire(Player player)
        {
            KiAttack attack = KiAttackRegistry.Current(player);

            if (attack == null)
            {
                // Nenhum ataque destravado. A mensagem sai do primeiro degrau, que é quem sabe qual
                // trava está fechada — ficar em silêncio deixaria a tecla parecendo quebrada, que é
                // exatamente o que ela vai parecer antes do primeiro boss.
                ExplainNothingUnlocked(player);
                return;
            }

            if (!KiManager.IsEnabled)
            {
                Message(player, "Turn ki on to attack.");
                return;
            }

            if (player.IsDead() || player.IsSleeping() || player.IsTeleporting() || player.InCutscene())
            {
                return;
            }

            if (!KiAttackRegistry.IsGlobalCooldownReady() || attack.GetRemainingCooldown() > 0f)
            {
                return;
            }

            float cost = attack.GetKiCost();
            if (KiManager.Current < cost)
            {
                Message(player, "Not enough ki.");
                return;
            }

            // Dispara ANTES de cobrar, de propósito: um nome de prefab errado no .cfg, ou o mundo
            // ainda carregando, fazem o tiro falhar — e comer a barra por um tiro que não saiu seria
            // um bug silencioso, do tipo que se diagnostica olhando o log em vez da tela.
            if (!KiProjectile.Fire(player, attack))
            {
                return;
            }

            KiManager.TryConsume(cost);

            // A pose só levanta depois de o projétil existir, pelo mesmo motivo que o ki só é
            // cobrado aqui: um prefab errado no .cfg faria o braço esticar sem nada sair da mão.
            KiBlastPose.Trigger(player);

            attack.StartCooldown();
            KiAttackRegistry.StartGlobalCooldown();
        }

        /// <summary>
        /// Troca de ataque. Com um só destravado a tecla apenas nomeia o que está selecionado — não
        /// é desperdício: é a única confirmação na tela de qual ataque a tecla de disparar usa,
        /// enquanto não existe HUD para isso (etapa 11).
        /// </summary>
        private static void Cycle(Player player)
        {
            KiAttack selected = KiAttackRegistry.SelectNext(player);

            if (selected == null)
            {
                ExplainNothingUnlocked(player);
                return;
            }

            Message(player, selected.DisplayName);
            SaiyaheimPlugin.LogVerbose($"Ki attack selected: {selected.DisplayName}.");
        }

        /// <summary>
        /// Por que não há ataque nenhum para usar. Sempre o primeiro degrau da escada: é o que o
        /// jogador vai destravar primeiro, então é a instrução útil.
        /// </summary>
        private static void ExplainNothingUnlocked(Player player)
        {
            if (KiAttackRegistry.All.Length == 0)
            {
                return;
            }

            Message(player, KiAttackRegistry.All[0].GetLockReason(player));
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
