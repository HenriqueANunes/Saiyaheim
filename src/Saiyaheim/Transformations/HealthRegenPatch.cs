using HarmonyLib;
using Saiyaheim.Util;

namespace Saiyaheim.Transformations
{
    /// <summary>
    /// Encurta o <b>intervalo</b> da cura passiva enquanto a forma ativa pede isso — hoje só o
    /// SSJ God (<c>HealthRegenSpeed</c>).
    ///
    /// <b>O que a cura passiva é, na vanilla.</b> Toda ela vem da comida, e roda num relógio só:
    /// o <c>Player.UpdateFood</c> soma o <c>dt</c> em <c>m_foodRegenTimer</c> e, quando ele passa
    /// de <b>10 segundos</b>, soma o <c>m_foodRegen</c> dos três alimentos, multiplica pelo que o
    /// <c>SEMan.ModifyHealthRegen</c> devolver e cura isso de uma vez. A vida sobe em degraus de
    /// 10 em 10 segundos, e não continuamente.
    ///
    /// <b>Por que o intervalo e não o tamanho do tique.</b> Houve uma segunda chave, que
    /// multiplicava a cura de cada tique; ela saiu em 2026-09-22, depois do playtest. As duas se
    /// multiplicavam — 2 e 2 davam quatro vezes a cura por segundo —, e das duas a que se
    /// <i>sente</i> é esta: degrau menor e mais frequente lê como regeneração, degrau grande e
    /// raro lê como poção. Quem quer mais cura sobe a velocidade.
    ///
    /// <b>Por que é patch Harmony, contra a regra do projeto.</b> Os 10 segundos são uma constante
    /// literal dentro do <c>UpdateFood</c>: não há chave, campo nem API nativa para mexer neles, e
    /// o único caminho sem patch seria um transpiler — mais frágil, não menos. O postfix não toca
    /// na constante: ele <b>adianta o relógio</b>, somando o tempo que falta para o intervalo
    /// efetivo virar <c>10 / velocidade</c>. O resultado é idêntico e sobrevive a uma atualização
    /// que mexa na conta da cura, porque ele não copia conta nenhuma.
    ///
    /// <b>Só o dono do jogador</b>, e não todo <c>Player</c> carregado: a cura de cada personagem
    /// roda na máquina dele, como todo o resto do mod. O <c>m_nview</c> de um jogador remoto não é
    /// nosso, e adiantar o relógio dele aqui não curaria ninguém — só gastaria reflexão por frame.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdateFood")]
    internal static class HealthRegenPatch
    {
        /// <summary>
        /// Desliga o efeito se o campo privado sumir numa atualização do jogo. Sem isto o aviso
        /// do <c>GameAccess</c> sairia uma vez por frame no log.
        /// </summary>
        private static bool _disabled;

        private static void Postfix(Player __instance, float dt, bool forceUpdate)
        {
            // O forceUpdate é o caminho de comer: ele roda com dt zero e volta ANTES do bloco da
            // cura. Adiantar o relógio ali daria um tique de cura de graça a cada garfada.
            if (_disabled || forceUpdate || dt <= 0f || __instance == null ||
                __instance != Player.m_localPlayer)
            {
                return;
            }

            Transformation active = TransformationRegistry.GetActive(__instance);
            if (active == null)
            {
                return;
            }

            // 1 é o default e a forma que não pede nada: sem isto, toda forma pagaria uma escrita
            // por reflexão por frame para somar zero.
            float speed = active.GetHealthRegenSpeed();
            if (speed <= 1f)
            {
                return;
            }

            // O jogo já somou dt no relógio. Somando (velocidade - 1) × dt por cima, o relógio
            // anda `velocidade` vezes mais rápido e o intervalo efetivo vira 10 / velocidade.
            if (!GameAccess.AdvanceFoodRegenTimer(__instance, dt * (speed - 1f)))
            {
                _disabled = true;
                SaiyaheimPlugin.Log.LogWarning(
                    "HealthRegenSpeed is off for this session: the game's food regen timer could " +
                    "not be reached.");
            }
        }
    }

    /// <summary>
    /// Faz a cura da forma <b>atravessar</b> o que o clima corta ou zera — Molhado, Frio e
    /// Congelando. Ligado só onde a chave existe, hoje o SSJ God
    /// (<c>HealthRegenIgnoresBlockers</c>).
    ///
    /// <b>O problema, e por que ele não se resolve com um número maior.</b> O multiplicador da
    /// cura passiva é <b>um só</b>, compartilhado por todos os status effects: o
    /// <c>SEMan.ModifyHealthRegen</c> passa a mesma variável por referência para cada efeito
    /// ativo, na ordem em que eles entraram, e o <c>SE_Stats</c> da vanilla soma o que está acima
    /// de 1 e <b>multiplica</b> o que está abaixo. O Congelando multiplica por zero. Qualquer
    /// coisa que a forma tenha somado antes dele morre ali, por maior que seja — foi o que o
    /// playtest de 2026-09-22 encontrou na Montanha: a forma do fôlego não curava nada justamente
    /// onde fôlego importa.
    ///
    /// <b>Por que um postfix no <c>SEMan</c> e não um lugar mais fundo.</b> Este é o único ponto
    /// depois de <i>todos</i> os efeitos terem falado. Reordenar o <c>SEMan</c> resolveria por
    /// acidente e quebraria na primeira vez que o jogo adicionasse os efeitos noutra ordem; um
    /// número maior na forma não resolve de jeito nenhum, porque o problema é multiplicação por
    /// zero.
    ///
    /// <b>O piso é 1</b>, a taxa normal da comida: a forma devolve o que o clima tira, e nada
    /// além disso. O que o Descansado soma continua valendo por cima — é piso, não substituição, e
    /// uma forma que <i>fixasse</i> o multiplicador apagaria o bônus e puniria quem dormiu.
    ///
    /// ⚠️ <b>O dano do Congelando continua chegando.</b> Isto mexe só na cura vinda da comida. A
    /// Montanha sem capa continua doendo — ela deixa de ser uma parede.
    /// </summary>
    [HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyHealthRegen))]
    internal static class HealthRegenFloorPatch
    {
        /// <summary>A taxa normal da comida, sem nada por cima nem por baixo. É o piso.</summary>
        private const float VanillaRegenMultiplier = 1f;


        private static void Postfix(SEMan __instance, ref float regenMultiplier)
        {
            // O método não diz de quem é a cura; o dono sai do campo privado do SEMan. Só o
            // jogador local interessa: a cura de cada personagem roda na máquina dele.
            if (!(GameAccess.GetSEManCharacter(__instance) is Player player) ||
                player != Player.m_localPlayer)
            {
                return;
            }

            Transformation active = TransformationRegistry.GetActive(player);
            if (active == null || !active.GetHealthRegenIgnoresBlockers())
            {
                return;
            }

            if (regenMultiplier < VanillaRegenMultiplier)
            {
                regenMultiplier = VanillaRegenMultiplier;
            }
        }
    }
}
