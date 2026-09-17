using Saiyaheim.Power;

namespace Saiyaheim.Ki
{
    /// <summary>
    /// Ki ganho por lutar bem: acertar um parry e dar o golpe final.
    ///
    /// <b>A unidade é o soco.</b> O valor da config multiplica o custo de ki de um soco do jogador
    /// <b>agora</b> (<see cref="BattlePower.GetPunchKiCost"/>). Esse custo já cresce com o poder e já
    /// leva o desconto de fim de jogo, então a recompensa acompanha a barra sem calibragem própria.
    /// Pedido pelo Henrique em 2026-09-17: parry vale dois socos, kill vale quatro.
    ///
    /// <b>Tudo aqui roda na máquina do jogador que ganha</b>, porque o ki é estado local. Quem
    /// detecta o parry é o <c>BlockPowerPatch</c>, que já roda no dono do <c>Player</c>. Quem detecta
    /// a kill é o <c>DamageXpPatch</c>, que roda no dono do <b>alvo</b>. Quando esse dono é outro
    /// cliente, a kill atravessa no <c>DamageReport</c>, junto com o dano.
    /// </summary>
    internal static class KiRewards
    {
        internal static void OnParry(Player player)
        {
            Grant(player, SaiyaheimConfig.KiOnParryPunches.Value, "parry");
        }

        internal static void OnKill(Player player)
        {
            Grant(player, SaiyaheimConfig.KiOnKillPunches.Value, "kill");
        }

        /// <summary>
        /// Se uma kill conta. Bicho domesticado não conta, senão abater a criação vira fonte de ki.
        /// Jogador também não, para o PvP não virar farm entre amigos.
        /// </summary>
        internal static bool CountsAsKill(Character target)
        {
            return target != null && !target.IsPlayer() && !target.IsTamed();
        }

        private static void Grant(Player player, float punches, string reason)
        {
            if (player == null || player != Player.m_localPlayer || punches <= 0f || !KiManager.IsEnabled)
            {
                return;
            }

            float perPunch = BattlePower.GetPunchKiCost(player, BattlePower.GetPunchDamageBonus(player));
            float amount = perPunch * punches;

            KiManager.Gain(amount);

            SaiyaheimPlugin.LogVerbose(
                $"Ki reward: {reason} → +{amount:0.#} ki ({punches:0.#} x {perPunch:0.#} per punch, " +
                $"{KiManager.Current:0.#} now).");
        }
    }
}
