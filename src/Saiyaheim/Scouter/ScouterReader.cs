using System.Text;
using UnityEngine;

namespace Saiyaheim.Scouter
{
    /// <summary>
    /// Decide o que a tela mostra no lugar de um poder de luta: nada, o número, ou interferência.
    /// Etapa 15.
    ///
    /// <list type="bullet">
    /// <item>Sem scouter equipado, <b>nada</b> — nem o dos bichos, nem o dos amigos, nem o próprio.
    /// Ver [[Decisões Tomadas#Poder de luta só se lê com scouter — 2026-10-08]].</item>
    /// <item>Acima do limite do tier, <b>interferência</b>: dígitos sorteados, com o mesmo
    /// comprimento do número real, trocando a cada <c>ScouterInterferenceInterval</c>.</item>
    /// <item>O ki <b>não</b> entra: com o scouter na cabeça o número aparece de ki desligado também
    /// (decisão de 2026-10-08; até então quem decidia era o ki de quem olha).</item>
    /// </list>
    ///
    /// Só lê o jogador local: quem "escaneia" é quem está na frente da tela.
    /// </summary>
    internal static class ScouterReader
    {
        internal enum Result
        {
            Hidden,
            Value,
            Static,
        }

        /// <summary>Tier do scouter na cabeça do jogador local, ou null. Atualizado uma vez por frame.</summary>
        internal static ScouterTier Equipped { get; private set; }

        private static readonly StringBuilder Builder = new StringBuilder(8);

        /// <summary>
        /// Uma vez por frame, do <c>Update</c> do plugin. O capacete equipado do <c>Humanoid</c>
        /// (<c>m_helmetItem</c>) é <c>protected</c> na assembly real, então a leitura passa pela
        /// lista pública de itens equipados.
        /// </summary>
        internal static void Update(Player player)
        {
            Equipped = null;
            if (player == null)
            {
                return;
            }

            foreach (ItemDrop.ItemData item in player.GetInventory().GetEquippedItems())
            {
                if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Helmet)
                {
                    continue;
                }

                Equipped = ScouterRegistry.TierOf(item);
                return;
            }
        }

        /// <summary>O que fazer com um poder de luta <b>já na escala da tela</b>.</summary>
        internal static Result Read(float display)
        {
            ScouterTier tier = Equipped;
            if (tier == null)
            {
                return Result.Hidden;
            }

            return display > Power.PowerRating.ToDisplay(tier.GetLimitRaw()) ? Result.Static : Result.Value;
        }

        /// <summary>
        /// Em que "quadro" da interferência estamos. Muda a cada <c>ScouterInterferenceInterval</c>;
        /// quem escreve o texto compara com o último para só remontar a malha quando trocar.
        /// </summary>
        internal static int StaticFrame()
        {
            float interval = Mathf.Max(0.01f, SaiyaheimConfig.ScouterInterferenceInterval);

            return Mathf.FloorToInt(Time.time / interval);
        }

        /// <summary>
        /// Dígitos sorteados com o comprimento de <paramref name="value"/>. A semente mistura o quadro
        /// com <paramref name="seed"/> para dois rótulos na tela não piscarem o mesmo número.
        /// </summary>
        internal static string StaticDigits(int value, int frame, int seed)
        {
            int length = Mathf.Max(1, Mathf.Abs(value).ToString().Length);
            uint state = (uint)(frame * 73856093 ^ seed * 19349663) | 1u;

            Builder.Length = 0;
            for (int i = 0; i < length; i++)
            {
                // xorshift: barato e sem alocar, e só precisa parecer ruído
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                Builder.Append((char)('0' + state % 10));
            }

            return Builder.ToString();
        }
    }
}
