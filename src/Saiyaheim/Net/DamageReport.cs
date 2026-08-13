using HarmonyLib;
using Saiyaheim.Power;
using UnityEngine;

namespace Saiyaheim.Net
{
    /// <summary>
    /// Conta a quem bateu quanto de vida de fato saiu do alvo.
    ///
    /// <b>O único RPC próprio do mod, e a exceção a uma regra que vale.</b> O projeto prefere
    /// mecanismo nativo a rede escrita à mão, e todo o resto da etapa 8 respeita isso: o estado
    /// visual anda na ZDO do jogador, a cor do cabelo no <c>VisEquipment</c>, o grito no emote, o
    /// projétil no próprio <c>ZNetView</c>. Aqui não há nativo que sirva, e vale registrar o que
    /// foi descartado para ninguém reabrir a discussão:
    /// <list type="bullet">
    /// <item><b>O <c>RPC_DamageText</c> do jogo</b> carrega o número certo e é a tentação óbvia —
    /// mas vai para <c>ZRoutedRpc.Everybody</c> com posição e texto, e <b>não</b> diz quem bateu
    /// em quem. Usá-lo daria XP a todo mundo por todo dano na tela;</item>
    /// <item><b>Ler a vida do alvo pela ZDO</b> na máquina de quem bate parece render o mesmo de
    /// graça. Não rende: a atualização chega assíncrona, o dano de outro jogador entra no meio da
    /// medição, e o caso que mais importa — o golpe que mata — destrói a ZDO antes da segunda
    /// leitura;</item>
    /// <item><b>Estimar armadura e resistência no atacante</b>, como o <c>SE_KiBody</c> faz com o
    /// que a armadura absorve, funcionaria e custaria zero de rede. Foi descartado por perder o
    /// desconto de overkill: um soco de 5000 num Boar de 10 de vida contaria 5000, e é justamente
    /// o overkill que faz o conteúdo de tier baixo parar de pagar XP sozinho.</item>
    /// </list>
    ///
    /// <b>Por que o <c>ZRoutedRpc</c> cru e não o <c>CustomRPC</c> do Jotunn.</b> O do Jotunn é
    /// feito para pacote grande — ele fragmenta, comprime e espera o peer numa corrotina. Isto
    /// aqui é um float por golpe, várias vezes por segundo: um pacote, sem corrotina, é o formato
    /// certo. É literalmente o que o próprio jogo faz com o texto de dano.
    ///
    /// <b>Confia no cliente</b>, e é decisão registrada: o mod é para jogar entre amigos, e
    /// validar isto no servidor custaria refazer a conta de dano duas vezes. Ver
    /// [[Multiplayer#Autoridade]].
    /// </summary>
    internal static class DamageReport
    {
        private const string RpcName = "Saiyaheim_DamageDealt";

        /// <summary>
        /// Registra o RPC uma vez por sessão de rede.
        ///
        /// <b>No construtor do <c>ZRoutedRpc</c> e não no <c>Game.Start</c></b>, por uma razão
        /// concreta: o <c>Register</c> faz <c>m_functions.Add</c>, que <b>estoura</b> em chave
        /// repetida. Um gancho que rodasse duas vezes na mesma instância derrubaria o mod ao
        /// reentrar num mundo. O construtor roda exatamente uma vez por instância, por definição.
        /// </summary>
        [HarmonyPatch(typeof(ZRoutedRpc), MethodType.Constructor, typeof(bool))]
        internal static class RegisterPatch
        {
            private static void Postfix(ZRoutedRpc __instance)
            {
                __instance.Register<float>(RpcName, RPC_DamageDealt);
            }
        }

        /// <summary>
        /// Chamado na máquina do <b>dono do alvo</b>, que é a única que sabe quanto de vida saiu.
        /// Se quem bateu for outro cliente, o número atravessa; se for o jogador local, quem
        /// credita é o <c>DamageXpPatch</c> ali mesmo, sem passar pela rede.
        /// </summary>
        internal static void Send(Player attacker, float applied)
        {
            if (attacker == null || ZRoutedRpc.instance == null || applied <= 0f)
            {
                return;
            }

            long peer = attacker.GetOwner();
            if (peer == 0L)
            {
                return;
            }

            ZRoutedRpc.instance.InvokeRoutedRPC(peer, RpcName, applied);
        }

        /// <summary>
        /// Chegou um aviso de dano causado: credita XP ao jogador local.
        ///
        /// <b>O número chega pronto e descontado</b> — vida perdida de verdade, overkill já fora —
        /// porque quem o mediu foi quem tinha como medir. É o mesmo valor que o caminho local usa,
        /// e por isso bater num bicho do amigo rende exatamente o mesmo que bater num seu.
        ///
        /// Guarda contra <c>NaN</c> e infinito, e não contra valor alto: um float corrompido
        /// envenenaria a skill de forma irreversível, enquanto um valor alto é o que um golpe forte
        /// legitimamente é.
        /// </summary>
        private static void RPC_DamageDealt(long sender, float applied)
        {
            Player local = Player.m_localPlayer;

            if (local == null || applied <= 0f || float.IsNaN(applied) || float.IsInfinity(applied))
            {
                return;
            }

            PowerSkill.RaiseFromDamageDealt(local, applied);

            SaiyaheimPlugin.LogVerbose(
                $"Battle Power XP: dealt {applied:0.#} damage to something owned by {sender}.");
        }
    }
}
