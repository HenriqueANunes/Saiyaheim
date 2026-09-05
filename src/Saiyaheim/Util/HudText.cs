namespace Saiyaheim.Util
{
    /// <summary>
    /// Formatação dos rótulos de texto da HUD.
    /// </summary>
    internal static class HudText
    {
        /// <summary>
        /// O prefixo de um rótulo, pronto para colar no número: sem espaço em branco nas pontas e
        /// com <b>um</b> espaço no fim.
        ///
        /// <b>Isto conserta um bug, e não é só conveniência.</b> As chaves de prefixo nasceram
        /// pedindo "mantenha o espaço no fim, nenhum é adicionado" — e esse espaço
        /// <b>nunca chegava</b>. O <c>ConfigFile</c> do BepInEx faz <c>Trim()</c> nos dois lados do
        /// <c>=</c> ao ler o arquivo <i>e</i> ao escrevê-lo, então o default <c>"PB: "</c> ia para
        /// o disco como <c>PB:</c> e voltava como <c>PB:</c>. Na tela saía <c>PB:622</c>. O espaço
        /// só sobrevivia na primeiríssima sessão, antes de o arquivo existir — que é exatamente a
        /// sessão em que ninguém repara.
        ///
        /// Não havia como resolver do lado da config: o valor não tem como carregar espaço no fim
        /// através de um arquivo que apara espaços. A separação tinha que virar responsabilidade
        /// de quem monta a linha, que é aqui.
        ///
        /// Prefixo vazio devolve vazio, sem o espaço — é o caso de quem quer só o número, e um
        /// espaço solto na frente empurraria o texto para o lado sem nada para mostrar.
        /// </summary>
        internal static string Prefix(string configured)
        {
            if (string.IsNullOrEmpty(configured))
            {
                return string.Empty;
            }

            string trimmed = configured.Trim();

            return trimmed.Length == 0 ? string.Empty : trimmed + " ";
        }
    }
}
