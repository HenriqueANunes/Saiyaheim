namespace Saiyaheim.Net
{
    /// <summary>
    /// O canal de estado do mod entre clientes: <b>um inteiro na ZDO do próprio jogador</b>.
    ///
    /// Responde, para <i>qualquer</i> jogador carregado e não só o local, as perguntas de que o
    /// visual precisa: está com o ki ligado? voando? carregando? em qual forma? acabou de atirar?
    ///
    /// <b>Por que não status effect, que era o plano.</b> A nota de design tratava
    /// <c>SE_Stats</c>/<c>StatusEffect</c> como "sincroniza de graça", e isso está errado —
    /// conferido no <c>SEMan</c> da assembly do jogo em 2026-08-13:
    /// <list type="bullet">
    /// <item><c>AddStatusEffect(StatusEffect)</c> só mexe na lista local. Não há RPC de saída;</item>
    /// <item>o <c>RPC_AddStatusEffect</c> que existe é o caminho <b>inverso</b> — um não-dono
    /// pedindo ao dono que aplique — e ele resolve por <c>ObjectDB.instance.GetStatusEffect</c>,
    /// que não conhece os efeitos do mod;</item>
    /// <item>a única coisa que atravessa é o bitmask <c>s_seAttrib</c>, e são os
    /// <c>StatusAttribute</c> predefinidos do jogo.</item>
    /// </list>
    /// Ou seja, <c>seman.HaveStatusEffect(...)</c> num jogador remoto responde sempre false.
    /// O mesmo vale para o voo por outro caminho: <c>Character.m_flying</c> é campo puro, não ZDO.
    ///
    /// <b>Por que ZDO e não um RPC nosso.</b> É exatamente o mecanismo do <c>ZSyncAnimation</c> e
    /// do <c>VisEquipment</c>: o dono escreve, o jogo replica, todo mundo lê. Não é rede escrita à
    /// mão, é o transporte nativo — a regra do projeto de preferir API do jogo continua de pé. E é
    /// barato: <c>ZDOExtraData.Set</c> só aumenta a revisão do dado quando o valor <b>muda</b>,
    /// então publicar todo tick não gera tráfego nenhum enquanto nada acontece.
    ///
    /// <b>Todo mundo lê daqui, inclusive quem está no teclado.</b> Poderia haver um atalho lendo o
    /// estado local direto, e ele seria pior: duas fontes de verdade para a mesma pergunta, e a
    /// divergência entre elas só apareceria na tela do amigo. Escrever e ler na mesma máquina é
    /// imediato — <c>ZDOExtraData</c> é memória local, não há ida e volta —, então o atalho não
    /// compraria latência nenhuma. Do jeito que está, um bug no canal aparece na tela do Henrique.
    ///
    /// <b>O que deliberadamente <i>não</i> passa por aqui: a matemática de combate.</b> Dano,
    /// armadura e bloqueio continuam saindo do <c>SEMan</c>, que é a autoridade na máquina do dono
    /// — e as três já rodam na máquina certa (o bônus do soco na de quem bate, a armadura na de
    /// quem apanha). Este canal é o <b>visual</b>: poses, aura, efeitos. Misturar os dois trocaria
    /// um problema resolvido por um risco de dessincronia em cima do que decide a luta.
    ///
    /// <b>Armadilha de acesso</b>, da família que o <c>CLAUDE.md</c> avisa: <c>Character.m_nview</c>
    /// é <c>protected</c> — a assembly publicizada deixaria compilar e estouraria
    /// <c>FieldAccessException</c> na tela. O caminho aqui é 100% público:
    /// <c>Character.GetZDOID()</c> mais <c>ZDOMan.instance.GetZDO(id)</c>.
    /// </summary>
    internal static class NetState
    {
        /// <summary>
        /// Prefixadas com o nome do mod, como as chaves do <c>m_customData</c>: a ZDO do jogador é
        /// espaço compartilhado com o jogo e com outros mods.
        /// </summary>
        private static readonly int StateHash = "saiyaheim.state".GetStableHashCode();

        private static readonly int BlastHash = "saiyaheim.blast".GetStableHashCode();

        // ---------- O leiaute do inteiro ----------
        //
        // Um campo só, e não um por pergunta. Cada chave de ZDO é uma entrada num dicionário
        // replicado; quatro bandeiras booleanas em quatro chaves seriam quatro vezes o overhead
        // para carregar quatro bits. E há um ganho de correção junto: com tudo no mesmo inteiro,
        // "voando na forma SSJ" chega ao outro cliente como um valor só, nunca meio aplicado.

        private const int FlagKiEnabled = 1 << 0;
        private const int FlagFlying = 1 << 1;
        private const int FlagCharging = 1 << 2;

        /// <summary>
        /// Onde começa o índice da forma. Os oito bits baixos ficam para as bandeiras — hoje
        /// sobram cinco, o que dá folga para a etapa 11 sem mexer no leiaute.
        /// </summary>
        private const int FormShift = 8;

        private const int FormMask = 0xFF;

        /// <summary>
        /// Publica o estado do jogador local. Chamado uma vez por frame, do <c>Update</c> do
        /// plugin, <b>depois</b> dos managers — o valor publicado é o do frame que acabou de ser
        /// decidido, não o do anterior.
        ///
        /// Recebe tudo pronto em vez de ir buscar: assim este arquivo não conhece nem o ki, nem o
        /// voo, nem as formas, e a ordem em que o estado é montado fica visível num lugar só.
        /// </summary>
        internal static void Publish(Player player, bool kiEnabled, bool flying, bool charging, int formIndex)
        {
            ZDO zdo = GetZdo(player);
            if (zdo == null || !zdo.IsOwner())
            {
                return;
            }

            int value = 0;

            if (kiEnabled)
            {
                value |= FlagKiEnabled;
            }

            if (flying)
            {
                value |= FlagFlying;
            }

            if (charging)
            {
                value |= FlagCharging;
            }

            // +1 porque zero precisa significar "forma base": um jogador sem o mod, ou que ainda
            // não publicou nada, lê zero na ZDO e não pode ser confundido com o primeiro degrau.
            value |= ((formIndex + 1) & FormMask) << FormShift;

            zdo.Set(StateHash, value);
        }

        internal static bool IsKiEnabled(Player player) => HasFlag(player, FlagKiEnabled);

        internal static bool IsFlying(Player player) => HasFlag(player, FlagFlying);

        internal static bool IsCharging(Player player) => HasFlag(player, FlagCharging);

        /// <summary>Índice da forma ativa na escada do <c>TransformationRegistry</c>, ou -1 na base.</summary>
        internal static int GetFormIndex(Player player)
        {
            return ((Read(player) >> FormShift) & FormMask) - 1;
        }

        /// <summary>
        /// Anuncia um disparo. <b>Um contador e não um carimbo de tempo</b>, porque relógio não é
        /// compartilhado entre máquinas: <c>Time.time</c> vale o tempo de sessão de cada cliente, e
        /// dois jogadores que entraram no mundo com meia hora de diferença não concordam sobre que
        /// horas são. Um número que só sobe, sim.
        ///
        /// Quem observa a mudança é o <c>KiBlastPose</c>, que guarda o último valor visto por
        /// jogador. Um cliente que só agora carregou aquele jogador vê o contador já em 7 e
        /// <b>não</b> dispara pose nenhuma — ele anota o 7 e espera o 8.
        /// </summary>
        internal static void PublishBlast(Player player)
        {
            ZDO zdo = GetZdo(player);
            if (zdo == null || !zdo.IsOwner())
            {
                return;
            }

            zdo.Set(BlastHash, zdo.GetInt(BlastHash) + 1);
        }

        /// <summary>Quantos disparos este jogador já anunciou nesta sessão.</summary>
        internal static int GetBlastCount(Player player)
        {
            ZDO zdo = GetZdo(player);

            return zdo == null ? 0 : zdo.GetInt(BlastHash);
        }

        /// <summary>
        /// Se o canal está de pé para este jogador. Falso quer dizer "não dá para saber", e não
        /// "está tudo desligado": jogador ainda entrando no mundo, ou sem o mod instalado.
        /// </summary>
        internal static bool IsAvailable(Player player) => GetZdo(player) != null;

        private static bool HasFlag(Player player, int flag) => (Read(player) & flag) != 0;

        private static int Read(Player player)
        {
            ZDO zdo = GetZdo(player);

            return zdo == null ? 0 : zdo.GetInt(StateHash);
        }

        /// <summary>
        /// A ZDO deste jogador, ou null enquanto ele não tiver uma.
        ///
        /// Sem cache de propósito: são duas buscas em dicionário, e um cache por
        /// <c>Character</c> precisaria de varredura para não segurar jogador que saiu — custo e
        /// risco maiores que o que economizaria.
        /// </summary>
        private static ZDO GetZdo(Player player)
        {
            if (player == null || ZDOMan.instance == null)
            {
                return null;
            }

            ZDOID id = player.GetZDOID();

            return id == ZDOID.None ? null : ZDOMan.instance.GetZDO(id);
        }
    }
}
