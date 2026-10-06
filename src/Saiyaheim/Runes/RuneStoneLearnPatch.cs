using HarmonyLib;

namespace Saiyaheim.Runes
{
    /// <summary>
    /// Ler uma runestone de lore pode ensinar um ataque de ki (etapa 13) ou um tier de Kaioken (etapa 14). Ver <see cref="RuneKnowledge"/>.
    ///
    /// <b>Por que patch.</b> Não há evento de "leu a pedra": o <c>RuneStone.Interact</c> mostra o
    /// texto e devolve false, e nada de fora fica sabendo. Postfix e não prefix porque o texto
    /// vanilla aparece primeiro, e a mensagem do mod vem por cima dele.
    ///
    /// <b>Quais pedras ficam de fora:</b>
    /// <list type="bullet">
    /// <item>o Vegvisir, que tem <c>m_locationName</c> — a pedra que aponta boss, não de lore;</item>
    /// <item>as do templo inicial, que têm <c>BossStone</c> — onde se pendura o troféu.</item>
    /// </list>
    /// </summary>
    [HarmonyPatch(typeof(RuneStone), nameof(RuneStone.Interact))]
    internal static class RuneStoneLearnPatch
    {
        private static void Postfix(RuneStone __instance, Humanoid character, bool hold)
        {
            // hold chega true a cada frame com a tecla segurada; a vanilla também ignora.
            if (hold || __instance == null)
            {
                return;
            }

            Player player = character as Player;
            if (player == null || player != Player.m_localPlayer)
            {
                return;
            }

            if (!string.IsNullOrEmpty(__instance.m_locationName) ||
                __instance.GetComponent<BossStone>() != null)
            {
                return;
            }

            IRuneLesson taught = RuneKnowledge.TryLearn(player, __instance.transform.position);
            if (taught == null)
            {
                return;
            }

            player.Message(MessageHud.MessageType.Center, $"You learned {taught.DisplayName}!");
        }
    }
}
