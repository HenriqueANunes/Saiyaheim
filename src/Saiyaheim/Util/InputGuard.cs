namespace Saiyaheim.Util
{
    /// <summary>
    /// Uma única resposta para "o jogador está mesmo pilotando o personagem agora?".
    ///
    /// Sem essa guarda, digitar no chat aciona todas as teclas do mod: escrever "voo" decola.
    /// É literalmente o bug que o mod de referência de voo tem.
    ///
    /// <b>O menu radial conta como não-pilotando</b>, pela mesma razão que a vanilla checa
    /// <c>Hud.InRadial()</c> em todo input de ataque, bloqueio e pulo: clicar num item da roda não
    /// pode disparar um Kamehameha junto.
    ///
    /// <b>O menu de construção também</b>: ele tem campo de pesquisa, e digitar o nome de uma peça
    /// acionava as teclas do mod — a letra do voo desligava o voo e o jogador caía.
    ///
    /// <b>O nome do marcador no mapa também</b>: é um <c>InputField</c> próprio do <c>Minimap</c>,
    /// que não passa pelo <c>TextInput</c>. Só o campo com foco, e não o mapa aberto inteiro.
    /// </summary>
    internal static class InputGuard
    {
        internal static bool AcceptsInput()
        {
            return !Console.IsVisible()
                   && !TextInput.IsVisible()
                   && !Menu.IsVisible()
                   && !InventoryGui.IsVisible()
                   && !Hud.InRadial()
                   && !Hud.InBuildUi()
                   && !Minimap.InTextInput()
                   && (Chat.instance == null || !Chat.instance.HasFocus());
        }
    }
}
