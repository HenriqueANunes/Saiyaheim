using Saiyaheim.Ki;
using UnityEngine;

namespace Saiyaheim.Power
{
    /// <summary>
    /// O power level: stat derivado que alimenta o dano do soco e a armadura.
    ///
    /// <b>São duas fórmulas, porque os dois caminhos de progressão são disjuntos.</b>
    ///
    /// <code>
    /// ki desligado: poder = k1*HP + k2*dano_arma + k3*armadura
    /// ki ligado:    poder = k1*HP + k4*nivel_battle_power
    /// </code>
    ///
    /// Arma e armadura não sobrevivem ao modo ki:
    /// <list type="bullet">
    /// <item>arma dá <b>zero</b> — o jogador soca, não tem nada equipado; e o dano do soco vem do
    /// power level, então incluí-lo seria contar o mesmo número duas vezes;</item>
    /// <item>armadura vira <b>laço de realimentação</b>, porque passou a ser derivada do poder:
    /// <c>poder → armadura → poder</c>. Não é escolha de design, o número diverge.</item>
    /// </list>
    ///
    /// Sobra o HP, e ele fica de propósito: comida é o único eixo de progressão do jogo base que
    /// continua valendo para quem usa ki.
    /// </summary>
    internal static class PowerLevel
    {
        /// <summary>
        /// Power level bruto do jogador. É este número — não o comprimido — que alimenta dano e
        /// armadura: a compressão é só de exibição.
        /// </summary>
        internal static float GetRaw(Player player)
        {
            if (player == null)
            {
                return 0f;
            }

            return KiManager.IsEnabled ? GetKiRaw(player) : GetVanillaRaw(player);
        }

        /// <summary>
        /// Fórmula do ki ligado. <b>Nunca</b> pode tocar em <c>GetBodyArmor()</c>: é ela que
        /// alimenta a armadura, e ler a armadura aqui fecharia o laço
        /// <c>GetArmor → GetBodyArmor → ModifyArmorMods → GetArmor</c> em recursão infinita.
        /// A separação em dois métodos existe para tornar esse erro impossível de cometer por
        /// distração, não só improvável.
        /// </summary>
        private static float GetKiRaw(Player player)
        {
            return SaiyaheimConfig.PowerK1Health.Value * GetHealthAboveBase(player)
                   + SaiyaheimConfig.PowerK4PowerSkill.Value * PowerSkill.GetLevel(player);
        }

        /// <summary>Fórmula do ki desligado: a original do projeto, com arma e armadura do jogo.</summary>
        private static float GetVanillaRaw(Player player)
        {
            return SaiyaheimConfig.PowerK1Health.Value * GetHealthAboveBase(player)
                   + SaiyaheimConfig.PowerK2WeaponDamage.Value * GetWeaponDamage(player)
                   + SaiyaheimConfig.PowerK3Armor.Value * player.GetBodyArmor();
        }

        /// <summary>
        /// HP **acima do mínimo**, que é o que de fato representa progressão.
        ///
        /// O HP base do Valheim (25) é dado de graça a todo personagem recém-criado, então contá-lo
        /// daria a todo mundo um piso de poder que não foi conquistado. Descontado, um jogador sem
        /// comida entra na conta com zero deste termo — que é a leitura certa.
        ///
        /// O 25 vem de <c>Player.GetBaseFoodHP()</c>, não de uma constante nossa: é número do jogo,
        /// não de balanceamento do mod, e assim acompanha sozinho se o Valheim mudar.
        /// </summary>
        private static float GetHealthAboveBase(Player player)
        {
            return Mathf.Max(0f, player.GetMaxHealth() - player.GetBaseFoodHP());
        }

        /// <summary>Dano somado ao soco. Aditivo — a transformação multiplicará por cima (etapa 5).</summary>
        internal static float GetPunchDamageBonus(Player player)
        {
            if (player == null)
            {
                return 0f;
            }

            return GetKiRaw(player) * SaiyaheimConfig.PunchDamageFromPower.Value;
        }

        /// <summary>
        /// Armadura derivada do poder, que <b>substitui</b> a do equipamento enquanto o ki está
        /// ligado. A parcela base existe para o jogador não ficar mais frágil ao ligar o ki no
        /// começo do jogo, quando a skill ainda está em nível baixo.
        ///
        /// <b>Arredondada para inteiro</b>: a armadura do Valheim é sempre inteira (peça de couro
        /// dá 2, não 2,37), e o número aparece na tela do jogador. Casa decimal aqui só denuncia
        /// que o valor é calculado, sem trazer precisão que importe — a diferença é menor que a
        /// variação de um único ponto de skill.
        /// </summary>
        internal static float GetArmor(Player player)
        {
            if (player == null)
            {
                return 0f;
            }

            // GetKiRaw, não GetRaw: ver o comentário de recursão em GetKiRaw. Este método só é
            // chamado com o ki ligado, então o ramo é o mesmo — mas depender disso seria depender
            // de um invariante que uma troca de toggle no meio do frame quebra.
            float armor = SaiyaheimConfig.ArmorBase.Value
                          + GetKiRaw(player) * SaiyaheimConfig.ArmorFromPower.Value;

            return Mathf.Round(armor);
        }

        /// <summary>
        /// Número para exibir. Comprimido para não virar um valor gigante e vazio cedo demais.
        ///
        /// ⚠️ Só exibição. Se a compressão entrasse no cálculo, dobrar o poder deixaria de dobrar
        /// o dano e o balanceamento viraria outra coisa.
        /// </summary>
        internal static float GetDisplayValue(Player player)
        {
            float raw = GetRaw(player);
            if (raw <= 0f)
            {
                return 0f;
            }

            return Mathf.Pow(raw, SaiyaheimConfig.PowerCompressionExponent.Value)
                   * SaiyaheimConfig.PowerDisplayScale.Value;
        }

        /// <summary>
        /// Dano total da arma equipada. Só usado na fórmula do ki desligado.
        ///
        /// <c>GetCurrentWeapon()</c> nunca devolve null — sem arma equipada ele entrega o
        /// <c>m_unarmedWeapon</c>, cujo dano é de unidade dígita. Isso é o comportamento certo
        /// aqui: quem luta desarmado sem ki tem, de fato, poder de arma quase zero.
        /// </summary>
        private static float GetWeaponDamage(Player player)
        {
            ItemDrop.ItemData weapon = player.GetCurrentWeapon();
            return weapon == null ? 0f : weapon.GetDamage().GetTotalDamage();
        }
    }
}
