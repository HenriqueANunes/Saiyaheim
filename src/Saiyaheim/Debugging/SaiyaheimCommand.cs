using System.Globalization;
using Jotunn.Entities;

namespace Saiyaheim.Debugging
{
    /// <summary>
    /// Base dos comandos de console do mod. Existe por dois motivos: juntar o que os três
    /// comandos repetiam (imprimir, parsear número) e, principalmente, dar o guard de cheat.
    ///
    /// <b>Por que o guard não é o <c>IsCheat</c> do Jotunn:</b> o <c>IsCheat</c> tranca o comando
    /// inteiro, e a granularidade que o projeto quer é por subcomando — <c>saiya_power</c> sozinho
    /// só lê o estado do personagem e continua livre, enquanto <c>saiya_power skill 100</c> é
    /// trapaça e exige <c>devcommands</c>, igual ao <c>spawn</c> do jogo base.
    ///
    /// O teste é o mesmo do jogo (<c>Terminal.IsCheatsEnabled</c>, público), então herda de graça
    /// a regra de que no multiplayer só o host consegue ligar cheats.
    /// </summary>
    internal abstract class SaiyaheimCommand : ConsoleCommand
    {
        /// <summary>
        /// Terminal que disparou o comando (console F5 ou janela de chat), válido só durante o
        /// <see cref="Execute"/>. Comando roda na thread principal da Unity, um de cada vez.
        /// </summary>
        private Terminal _context;

        public sealed override void Run(string[] args)
        {
            Run(args, Console.instance);
        }

        public sealed override void Run(string[] args, Terminal context)
        {
            _context = context ?? Console.instance;
            try
            {
                Execute(args ?? new string[0]);
            }
            finally
            {
                _context = null;
            }
        }

        /// <summary>Corpo do comando. <paramref name="args"/> nunca é nulo.</summary>
        protected abstract void Execute(string[] args);

        /// <summary>
        /// Porteiro dos subcomandos que trapaceiam. Retorna <c>false</c> e já explica ao jogador
        /// quando o <c>devcommands</c> não está ligado — quem chama só precisa dar <c>return</c>.
        /// </summary>
        protected bool RequireCheats(string action)
        {
            if (_context != null && _context.IsCheatsEnabled())
            {
                return true;
            }

            Print($"'{Name} {action}' is a cheat. Run 'devcommands' first.");
            return false;
        }

        protected void Print(string message)
        {
            SaiyaheimPlugin.Log.LogInfo(message);
            _context?.AddString($"[Saiyaheim] {message}");
        }

        /// <summary>O número logo depois do subcomando, no formato <c>saiya_x acao 50</c>.</summary>
        protected static bool TryParseAmount(string[] args, out float amount)
        {
            return TryParseAmount(args, 1, out amount);
        }

        /// <summary>
        /// O número numa posição qualquer. O <c>saiya_form</c> precisa disto porque aceita o nome
        /// da forma antes do subcomando (<c>saiya_form ssj skill 50</c>), e aí o número desliza uma
        /// casa para a direita.
        /// </summary>
        protected static bool TryParseAmount(string[] args, int index, out float amount)
        {
            amount = 0f;
            return args.Length > index &&
                   float.TryParse(args[index], NumberStyles.Float, CultureInfo.InvariantCulture,
                       out amount);
        }
    }
}
