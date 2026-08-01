namespace Saiyaheim.Util
{
    /// <summary>
    /// Uma única resposta para "o jogador está mesmo pilotando o personagem agora?".
    ///
    /// Sem essa guarda, digitar no chat aciona todas as teclas do mod: escrever "voo" decola.
    /// É literalmente o bug que o mod de referência de voo tem.
    /// </summary>
    internal static class InputGuard
    {
        internal static bool AcceptsInput()
        {
            return !Console.IsVisible()
                   && !TextInput.IsVisible()
                   && !Menu.IsVisible()
                   && !InventoryGui.IsVisible()
                   && (Chat.instance == null || !Chat.instance.HasFocus());
        }
    }
}
