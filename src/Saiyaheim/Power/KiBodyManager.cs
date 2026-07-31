using Saiyaheim.Ki;

namespace Saiyaheim.Power
{
    /// <summary>
    /// Mantém o <see cref="SE_KiBody"/> em sincronia com o toggle de ki.
    ///
    /// Roda no <c>Update</c> do plugin, junto com o resto — sem patch Harmony, mesma linha da
    /// etapa 2. O custo por frame é uma consulta a um <c>HashSet</c> no SEMan.
    /// </summary>
    internal static class KiBodyManager
    {
        /// <summary>
        /// Template. O <c>SEMan.AddStatusEffect</c> guarda um <c>Clone()</c> dele, não a
        /// instância — então este objeto nunca é o efeito ativo, só o molde.
        /// </summary>
        private static SE_KiBody _template;

        internal static void Update(Player player)
        {
            if (player == null)
            {
                return;
            }

            SEMan seman = player.GetSEMan();
            if (seman == null)
            {
                return;
            }

            bool shouldBeActive = KiManager.IsEnabled;
            bool isActive = seman.HaveStatusEffect(SE_KiBody.NameHashValue);

            if (shouldBeActive == isActive)
            {
                return;
            }

            if (shouldBeActive)
            {
                if (_template == null)
                {
                    _template = SE_KiBody.CreateTemplate();
                }

                seman.AddStatusEffect(_template);
                SaiyaheimPlugin.LogVerbose("Ki body applied.");
            }
            else
            {
                // quiet: sem mensagem na tela. O feedback do toggle é do toggle, não daqui.
                seman.RemoveStatusEffect(SE_KiBody.NameHashValue, quiet: true);
                SaiyaheimPlugin.LogVerbose("Ki body removed.");
            }
        }
    }
}
