using UnityEngine;

namespace Saiyaheim.Util
{
    /// <summary>
    /// Onde no corpo do jogador um efeito se prende.
    ///
    /// <b>Prender no osso da mão, e não medir a partir dos pés</b>, é a diferença entre um efeito
    /// que a animação carrega junto e um que ela deixa para trás. Enquanto o Kamehameha não tem
    /// pose própria, os dois parecem iguais — a mão do personagem parado está sempre no mesmo
    /// lugar. No dia em que a pose de duas mãos entrar, o efeito preso ao osso vai para o quadril
    /// junto com ela sem uma linha de config a mais, e o medido a partir dos pés fica boiando no ar
    /// onde a mão estava antes.
    ///
    /// <b>Os <c>Transform</c> das mãos vêm do <c>VisEquipment</c></b>, onde <c>m_rightHand</c> e
    /// <c>m_leftHand</c> são <b>públicos</b> — conferido na assembly não-publicizada. O caminho até
    /// o componente é que importa: <c>Humanoid.m_visEquipment</c> é <c>protected</c>, e a assembly
    /// publicizada deixaria compilar um acesso que estoura <c>FieldAccessException</c> na tela do
    /// jogador. O <c>GetComponent</c> chega no mesmo objeto por caminho público, que é literalmente
    /// o que o <c>Humanoid.Awake</c> faz. Mesma volta que o <c>KiProjectile.GetOrigin</c> dá.
    /// </summary>
    internal static class BodyAnchor
    {
        /// <summary>
        /// O transform em que prender, nunca null: sem esqueleto montado — o personagem ainda
        /// carregando, ou um modelo sem as mãos mapeadas — cai no corpo, que é o comportamento que
        /// o mod tinha antes de existir mão nenhuma.
        /// </summary>
        internal static Transform Resolve(Player player, EffectAnchor anchor)
        {
            if (player == null)
            {
                return null;
            }

            if (anchor == EffectAnchor.Body)
            {
                return player.transform;
            }

            VisEquipment vis = player.GetComponent<VisEquipment>();
            Transform hand = vis == null
                ? null
                : (anchor == EffectAnchor.LeftHand ? vis.m_leftHand : vis.m_rightHand);

            return hand != null ? hand : player.transform;
        }

        /// <summary>
        /// Este ponto de fixação é uma mão que de fato existe agora?
        ///
        /// Quem pergunta é quem precisa decidir o significado do offset: preso na mão ele é medido
        /// <b>a partir da palma</b>, e no corpo, a partir dos pés. Os dois pedem números de ordem
        /// de grandeza diferente, e usar um no lugar do outro põe o efeito a um metro de onde
        /// deveria — silenciosamente.
        /// </summary>
        internal static bool IsHand(Player player, EffectAnchor anchor)
        {
            return anchor != EffectAnchor.Body && Resolve(player, anchor) != player?.transform;
        }
    }
}
