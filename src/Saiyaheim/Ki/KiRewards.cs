namespace Saiyaheim.Ki
{
    /// <summary>
    /// Ki ganho por lutar bem: acertar um parry e dar o golpe final.
    ///
    /// <b>A unidade é a barra.</b> O valor da config é a fração do ki máximo do jogador
    /// <b>agora</b>: kill devolve metade, parry um quarto. Até 2026-09-25 a unidade era o soco
    /// (múltiplos do custo de ki de um soco), e isso enchia a barra inteira numa kill no meio do
    /// jogo, porque o custo do soco e a barra crescem por caminhos diferentes. Ver o comentário das
    /// chaves no <see cref="SaiyaheimConfig"/>.
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
            Grant(player, SaiyaheimConfig.KiOnParryBarFraction.Value, "parry");
        }

        internal static void OnKill(Player player)
        {
            Grant(player, SaiyaheimConfig.KiOnKillBarFraction.Value, "kill");
        }

        /// <summary>
        /// Se uma kill conta. Bicho domesticado não conta, senão abater a criação vira fonte de ki.
        /// Jogador também não, para o PvP não virar farm entre amigos.
        /// </summary>
        internal static bool CountsAsKill(Character target)
        {
            return target != null && !target.IsPlayer() && !target.IsTamed();
        }

        private static void Grant(Player player, float fraction, string reason)
        {
            if (player == null || player != Player.m_localPlayer || fraction <= 0f || !KiManager.IsEnabled)
            {
                return;
            }

            float amount = KiManager.MaxFor(player) * fraction;

            KiManager.Gain(amount);

            SaiyaheimPlugin.LogVerbose(
                $"Ki reward: {reason} → +{amount:0.#} ki ({fraction:0.##} of the bar, " +
                $"{KiManager.Current:0.#} now).");
        }
    }
}
