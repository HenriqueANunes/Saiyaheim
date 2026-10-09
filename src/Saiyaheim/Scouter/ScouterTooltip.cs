using System.Text;
using Saiyaheim.Attacks;
using Saiyaheim.Power;
using UnityEngine;

namespace Saiyaheim.Scouter
{
    /// <summary>
    /// A dica do scouter no inventário: o limite do tier e, se ele consegue ler o próprio poder de
    /// luta do jogador, o dano, o bloqueio e o custo de ki do soco, do bloqueio e de cada ataque de
    /// ki aprendido. Etapa 15, fase 2. Pedido do MisterMusashi e de um post do Nexus: comparar o soco
    /// com uma arma ou um escudo.
    ///
    /// <b>Sem patch Harmony.</b> A dica de uma espada sai dos campos do próprio item; estes números
    /// são do jogador, e o jogo não tem onde buscá-los. Mas o <c>m_shared</c> é um só para todas as
    /// cópias de um item (o <c>ItemData.Clone</c> é <c>MemberwiseClone</c> e não copia o
    /// <c>m_shared</c>), e a dica lê o <c>m_description</c> dele toda vez que é montada. Então basta
    /// reescrever a descrição de cada scouter de tempos em tempos. É local, e é o certo: são os
    /// números de quem olha.
    ///
    /// <b>Só com o inventário aberto</b> (que é também a tela da bancada): fechado, ninguém vê dica
    /// e nada é reescrito. Ao abrir, reescreve na hora, para a dica nunca nascer com número velho.
    /// Reescrever só no instante do mouse em cima pediria patch no <c>GetTooltip</c>, porque nem o
    /// jogo nem o Jotunn avisam quando uma dica é montada (Henrique perguntou em 2026-10-08).
    ///
    /// Limitação aceita: com a dica já aberta o texto não muda, então a interferência fica parada
    /// ali até a dica ser aberta de novo.
    /// </summary>
    internal static class ScouterTooltip
    {
        /// <summary>
        /// Segundos entre uma reescrita e outra. Não é balanceamento nem visual: é só quão velho o
        /// número da dica pode estar quando o jogador a abre.
        /// </summary>
        private const float RefreshInterval = 0.5f;

        private static float _timer;

        private static bool _wasOpen;

        private static readonly StringBuilder Stats = new StringBuilder(512);
        private static readonly StringBuilder Text = new StringBuilder(1024);

        private static bool _disabled;

        internal static void Update(Player player, float dt)
        {
            if (_disabled || player == null)
            {
                return;
            }

            bool open = InventoryGui.IsVisible();
            bool justOpened = open && !_wasOpen;
            _wasOpen = open;
            if (!open)
            {
                return;
            }

            _timer -= dt;
            if (_timer > 0f && !justOpened)
            {
                return;
            }

            _timer = RefreshInterval;

            try
            {
                Refresh(player);
            }
            catch (System.Exception e)
            {
                _disabled = true;
                SaiyaheimPlugin.Log.LogError($"Scouter tooltip disabled after an error: {e}");
            }
        }

        private static void Refresh(Player player)
        {
            float ownRaw = PowerRating.GetRaw(player);
            int ownDisplay = Mathf.RoundToInt(PowerRating.ToDisplay(ownRaw));
            BuildStats(player);
            int frame = ScouterReader.StaticFrame();

            // O prefab: é para onde apontam os itens feitos na bancada e os carregados do save, e
            // é o que a bancada mostra antes de craftar.
            foreach (ScouterRegistry.Entry entry in ScouterRegistry.Entries)
            {
                if (entry.Shared != null)
                {
                    entry.Shared.m_description = Compose(entry, ownRaw, ownDisplay, frame);
                }
            }

            // ⚠️ E as cópias. Item que nasceu no mundo (spawn, caído no chão) e foi apanhado leva
            // uma cópia própria do SharedData, feita no Instantiate: o ItemDrop.Awake só religa ao
            // do prefab dentro do editor da Unity (conferido na assembly em 2026-10-08). Sem esta
            // volta, esse item mostrava a descrição congelada de quando nasceu. Baú aberto fica de
            // fora: o contêiner atual do InventoryGui é privado, e o item volta a valer ao ser
            // apanhado.
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                ScouterRegistry.Entry entry = ScouterRegistry.EntryOf(item);
                if (entry != null && item.m_shared != entry.Shared)
                {
                    item.m_shared.m_description = entry.Shared.m_description;
                    item.m_shared.m_icons = entry.Shared.m_icons;
                }
            }
        }

        private static string Compose(ScouterRegistry.Entry entry, float ownRaw, int ownDisplay, int frame)
        {
            float limitRaw = entry.Tier.GetLimitRaw();

            // Curta, a pedido do Henrique (2026-10-08): uma linha de limite e uma por ação.
            Text.Length = 0;
            Text.Append("Reads battle power up to <color=orange>")
                .Append(Mathf.RoundToInt(PowerRating.ToDisplay(limitRaw)))
                .Append("</color>.\n\n");

            if (ownRaw > limitRaw)
            {
                // Mesma regra da HUD: forte demais para o aparelho, interferência.
                Text.Append("Your power: <color=red>")
                    .Append(ScouterReader.StaticDigits(ownDisplay, frame, entry.Tier.Index))
                    .Append("</color> (too strong)");
            }
            else
            {
                Text.Append(Stats);
            }

            return Text.ToString();
        }

        /// <summary>
        /// As linhas de combate, montadas uma vez por reescrita e coladas em todos os tiers que
        /// conseguem ler. Usa as mesmas funções que o combate usa, então o número da dica é o do
        /// golpe — inclusive com a forma e o Kaioken ativos.
        ///
        /// Com o ki desligado o soco é vanilla e os ataques de ki não saem, mas o scouter mede o que
        /// o corpo faria com ele ligado — e avisa.
        /// </summary>
        private static void BuildStats(Player player)
        {
            Stats.Length = 0;

            float bonus = BattlePower.GetPunchDamageBonus(player);
            Stats.Append("Punch: ").Append(Number(BattlePower.GetPunchDamage(player)))
                .Append(" dmg, ").Append(Number(BattlePower.GetPunchKiCost(player, bonus), "0.#"))
                .Append(" ki");

            float blockCost = SaiyaheimConfig.BlockKiCost.Value * BattlePower.GetDefenseKiCostFactor(player);
            // O termo do próprio jogo, o mesmo da dica do escudo, e no idioma do jogador. O número
            // equivale ao amarelo do escudo, entre parênteses: o valor final do bloqueio. O de ki
            // não tem "base" separada nem passa pela skill de Blocking, por isso um número só.
            Stats.Append("\n$item_blockarmor: ").Append(Number(BattlePower.GetBlockPower(player)))
                .Append(", ").Append(Number(blockCost, "0.##"))
                .Append(" ki per dmg blocked");

            // Poder de combate com o ki ligado, para o ataque medir o mesmo que o soco mede. O
            // GetDamage do ataque lê o poder do toggle atual, e de ki desligado daria o vanilla.
            float combat = BattlePower.GetKiCombatRawWithoutForm(player) * BattlePower.GetCombatMultiplier(player);
            foreach (KiAttack attack in KiAttackRegistry.All)
            {
                if (!attack.IsUnlocked(player))
                {
                    continue;
                }

                float damage = attack.DamageFor(combat) * attack.GetBeamCount();
                Stats.Append('\n').Append(attack.DisplayName).Append(": ")
                    .Append(Number(damage)).Append(" dmg, ")
                    .Append(Number(attack.GetKiCost(), "0.#")).Append(" ki");
            }

            if (!Ki.KiManager.IsEnabled)
            {
                Stats.Append("\n<color=#a0a0a0>(as if ki were on)</color>");
            }
        }

        private static string Number(float value, string format = "0")
        {
            return "<color=orange>" + value.ToString(format) + "</color>";
        }
    }
}
