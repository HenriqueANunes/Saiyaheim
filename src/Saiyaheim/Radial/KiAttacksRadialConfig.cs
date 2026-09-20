using System.Collections.Generic;
using Saiyaheim.Attacks;
using Saiyaheim.Util;
using UnityEngine;
using Valheim.UI;

namespace Saiyaheim.Radial
{
    /// <summary>
    /// O anel dos ataques de ki: um item por ataque, e clicar <b>seleciona</b> — quem dispara
    /// continua sendo o <c>FireKiAttackKey</c>.
    ///
    /// <b>É o caso que motivou o menu.</b> A alternativa é o <c>CycleKiAttackKey</c>, um ciclo
    /// às cegas: com quatro ataques destravados, chegar no terceiro custa três toques e nenhum
    /// feedback antes do disparo. Aqui a escolha é direta, com o custo de ki à vista.
    ///
    /// Selecionar não dispara de propósito. Mirar acontece depois de escolher, e um anel que
    /// atirasse no fechar mandaria o tiro para onde o cursor do menu estava.
    ///
    /// <b>Ataque travado não aparece</b>, decidido em 2026-09-20 junto com o anel das formas. Com
    /// todos travados o anel fica vazio, e aí o grupo some do anel principal — ver
    /// <see cref="HasContent"/>.
    /// </summary>
    internal sealed class KiAttacksRadialConfig : IRadialConfig
    {
        public string LocalizedName => "Ki attacks";

        // O grupo usa o ícone do Ki Blast: é o ataque que todo mundo tem desde o começo, e
        // reaproveitar o arquivo evita embutir a mesma arte duas vezes na DLL.
        public Sprite Sprite => IconLoader.LoadOptional("blast");

        public void InitRadialConfig(RadialBase radial)
        {
            Player player = Player.m_localPlayer;
            List<RadialMenuElement> elements = new List<RadialMenuElement>();
            KiAttack current = player == null ? null : KiAttackRegistry.Current(player);

            foreach (KiAttack attack in KiAttackRegistry.All)
            {
                if (!attack.IsUnlocked(player))
                {
                    continue;
                }

                // Cópia local: o foreach entrega a mesma variável a todas as closures.
                KiAttack target = attack;

                // O jogador é lido no clique, não capturado aqui: o anel principal da vanilla
                // guarda o último item usado e o devolve à roda na abertura seguinte, então um
                // elemento do mod pode sobreviver a uma morte ou a uma troca de personagem.
                elements.Add(SaiyaRadial.Leaf(
                    target.DisplayName,
                    Subtitle(player, target, current),
                    IconLoader.LoadOptional(target.Id),
                    () => Select(Player.m_localPlayer, target)));
            }

            radial.ConstructRadial(elements);
        }

        /// <summary>
        /// Seleciona, ou explica a trava e deixa o menu aberto. A mensagem é a mesma que a tecla
        /// de disparo daria — a trava tem um dono só, e é o <c>KiAttack</c>.
        /// </summary>
        private static bool Select(Player player, KiAttack attack)
        {
            if (player == null)
            {
                return false;
            }

            string lockReason = attack.GetLockReason(player);
            if (lockReason != null)
            {
                player.Message(MessageHud.MessageType.Center, lockReason);
                return false;
            }

            KiAttackRegistry.Select(attack);
            player.Message(MessageHud.MessageType.Center, attack.DisplayName);
            SaiyaheimPlugin.LogVerbose($"Selected {attack.DisplayName} from the radial menu.");

            return true;
        }

        private static string Subtitle(Player player, KiAttack attack, KiAttack current)
        {
            if (player == null)
            {
                return null;
            }

            // Não há caso de ataque travado aqui: ele nem chega a virar item.
            string cost = $"{attack.GetKiCost():0} ki";

            return attack == current ? $"{cost} — selected" : cost;
        }

        /// <summary>
        /// Este anel tem alguma coisa para mostrar? Chamado pelo <see cref="RadialMainMenuPatch"/>
        /// antes de pendurar o grupo: um grupo que abre um anel vazio é um item morto na roda
        /// principal, do mesmo tipo que o <c>EmptyElement</c> que o patch remove.
        /// </summary>
        internal static bool HasContent(Player player)
        {
            if (player == null)
            {
                return false;
            }

            foreach (KiAttack attack in KiAttackRegistry.All)
            {
                if (attack.IsUnlocked(player))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
