using Saiyaheim.Ki;
using Saiyaheim.Net;
using Saiyaheim.Util;
using UnityEngine;

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
    ///
    /// <b>A mesma tecla faz as duas coisas</b>, e é o <c>ChargeTime</c> do ataque que decide qual:
    /// zero dispara no toque (o ki blast), maior que zero carrega enquanto segurado e dispara ao
    /// soltar (o Kamehameha). Uma tecla própria para carregar foi descartada por gastar um bind
    /// para uma pergunta que o ataque selecionado já responde — e porque um jogador que troca de
    /// ataque não deveria ter que trocar de dedo junto.
    /// </summary>
    internal static class KiAttackManager
    {
        internal static void Update(Player player, float deltaTime)
        {
            // ANTES do InputGuard, e de propósito: um feixe já pago tem que terminar de sair
            // mesmo que o jogador abra o inventário no meio dela. Recusar tecla é uma coisa;
            // engolir meio feixe que o jogador comprou é outra.
            KiBeam.Update();

            if (player == null || !InputGuard.AcceptsInput())
            {
                // Sem leitura de tecla não há como saber que o jogador soltou, e uma carga que
                // ninguém pode soltar ficaria presa até a próxima morte. Abrir o inventário no meio
                // de um Kamehameha larga a carga — e não custa nada, porque segurar não custa nada.
                KiBeamCharge.Cancel();
                return;
            }

            // O dreno da carga acontece aqui, e devolve o único pedido que ele sabe fazer: a barra
            // acabou, solte agora.
            bool starved = KiBeamCharge.Update(player, deltaTime);

            // O atalho com Shift vem primeiro, por simetria com o TransformationManager. O Hotkey
            // exige os modificadores exatos, então V nunca dispara com Shift segurado.
            if (Hotkey.IsDown(SaiyaheimConfig.CycleKiAttackKey))
            {
                Cycle(player);

                // Trocar de ataque no meio de uma carga larga a carga: o que estava sendo carregado
                // não é mais o que a tecla dispara.
                KiBeamCharge.Cancel();
            }
            else if (KiBeamCharge.Current != null)
            {
                // Carga em curso: ou o dedo saiu da tecla, ou a barra acabou. IsMainKeyHeld e não
                // IsPressed — encostar no Shift no meio da carga não pode soltar o tiro. Ver
                // Hotkey.IsMainKeyHeld.
                if (starved)
                {
                    // Forçado: sai o que foi pago, mesmo abaixo do piso de carga. O piso existe
                    // para um toque acidental não cobrar nada, e aqui já foi cobrado.
                    Release(player, forced: true);
                }
                else if (!Hotkey.IsMainKeyHeld(SaiyaheimConfig.FireKiAttackKey))
                {
                    Release(player, forced: false);
                }
            }
            else if (Hotkey.IsDown(SaiyaheimConfig.FireKiAttackKey))
            {
                Press(player);
            }
        }

        /// <summary>
        /// A tecla desceu: dispara na hora, ou começa a carregar, conforme o ataque.
        ///
        /// As checagens são as mesmas nos dois casos e acontecem <b>aqui</b>, e não ao soltar: uma
        /// trava fechada ou o ki desligado devem falar no instante em que o jogador aperta, e não
        /// depois de ele passar dois segundos segurando para nada.
        /// </summary>
        private static void Press(Player player)
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
                KiBeam.Cancel(player);
                return;
            }

            // Feixe em curso. Em silêncio, como o cooldown: é uma recusa que se conserta sozinha
            // em menos de um segundo, e a tecla de atirar é apertada repetidamente por construção.
            if (KiBeam.IsBusy(player))
            {
                return;
            }

            if (!KiAttackRegistry.IsGlobalCooldownReady() || attack.GetRemainingCooldown() > 0f)
            {
                return;
            }

            // O piso de ki. No ataque carregado é o da carga MÍNIMA, e não o do feixe inteiro:
            // exigir a barra cheia para começar a carregar proibiria justamente o Kamehameha curto,
            // que é a razão de a carga ser gradual.
            float floorCost = attack.IsCharged
                ? attack.GetKiCost(attack.GetBeamCount(attack.GetMinChargeRatio()))
                : attack.GetKiCost();

            if (KiManager.Current < floorCost)
            {
                Message(player, "Not enough ki.");
                return;
            }

            if (attack.IsCharged)
            {
                // Sem cobrar nada aqui: quem cobra é o próprio carregamento, tique a tique. Ver
                // KiBeamCharge.
                KiBeamCharge.Begin(attack);
                return;
            }

            Shoot(player, attack, attack.GetBeamCount(), 1f, attack.GetKiCost());
        }

        /// <summary>
        /// A tecla subiu, ou a barra acabou: solta o que foi carregado.
        ///
        /// <b>Não cobra nada.</b> O ki de um ataque carregado é gasto <i>durante</i> a carga, tique
        /// a tique — o que sai aqui já está pago. Ver <see cref="KiBeamCharge"/>.
        ///
        /// <b>Carga curta demais não dispara</b> (<c>MinChargeRatio</c>): é o que impede um toque
        /// acidental de virar um projétil solto. O ki que a fração de segundo já consumiu não
        /// volta, e essa é a contrapartida aceita de pagar enquanto carrega.
        /// </summary>
        /// <param name="forced">
        /// A barra acabou. Dispara <b>ignorando o piso de carga</b>: o piso existe para um toque
        /// acidental não cobrar nada, e aqui já foi cobrado — recusar seria cobrar por nada.
        /// </param>
        private static void Release(Player player, bool forced)
        {
            KiAttack attack = KiBeamCharge.Current;
            float ratio = KiBeamCharge.Ratio;

            KiBeamCharge.Cancel();

            if (attack == null || (!forced && ratio < attack.GetMinChargeRatio()))
            {
                return;
            }

            Shoot(player, attack, attack.GetBeamCount(ratio), ratio, kiCost: 0f);
        }

        /// <summary>
        /// O disparo em si: primeiro projétil, cobrança, fila do resto, pose e cooldown — nessa
        /// ordem, e a ordem é o desenho.
        ///
        /// Compartilhado entre o toque e a soltura de propósito. Eram dois caminhos até o
        /// carregamento entrar, e mantê-los separados teria duplicado justamente as garantias que
        /// custaram playtest: cobrar só depois de o projétil existir, e levantar a pose só depois
        /// de cobrar.
        /// </summary>
        /// <param name="kiCost">
        /// O que cobrar agora. Zero num ataque carregado, que já pagou enquanto carregava — é o
        /// único ponto em que os dois caminhos diferem, e por isso é parâmetro em vez de uma
        /// pergunta feita aqui dentro.
        /// </param>
        private static void Shoot(
            Player player, KiAttack attack, int projectiles, float ratio, float kiCost)
        {
            // Dispara ANTES de cobrar, de propósito: um nome de prefab errado no .cfg, ou o mundo
            // ainda carregando, fazem o tiro falhar — e comer a barra por um tiro que não saiu seria
            // um bug silencioso, do tipo que se diagnostica olhando o log em vez da tela.
            if (!KiProjectile.Fire(player, attack, ratio))
            {
                return;
            }

            if (kiCost > 0f)
            {
                KiManager.TryConsume(kiCost);
            }

            // O primeiro projétil já saiu; os que faltam entram na fila. Num ataque de tiro único
            // isto não faz nada, que é por que o resto do caminho acima não sabe que feixe existe.
            KiBeam.Schedule(player, attack, projectiles - 1, ratio);

            // A pose só levanta depois de o projétil existir, pelo mesmo motivo que o ki só é
            // cobrado aqui: um prefab errado no .cfg faria o braço esticar sem nada sair da mão.
            //
            // Anuncia em vez de chamar a pose direto: o contador na ZDO é o que faz o braço
            // esticar na tela de todo mundo, e quem atirou não é exceção. Ver KiBlastPose.Trigger.
            NetState.PublishBlast(player);

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
