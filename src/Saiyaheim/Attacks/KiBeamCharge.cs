using Saiyaheim.Ki;
using UnityEngine;

namespace Saiyaheim.Attacks
{
    /// <summary>
    /// O Kamehameha sendo segurado pelo jogador local: qual ataque, desde quando, e quanto disso
    /// já virou feixe.
    ///
    /// <b>O ki é gasto enquanto a carga CRESCE, e soltar não cobra nada</b> — invertido em
    /// 2026-09-07, depois de o modelo oposto ser jogado. O jogador paga <b>cada projétil no
    /// instante em que a carga o produz</b>, então a taxa por segundo é derivada e não uma chave
    /// nova: ver <see cref="KiAttack.GetChargeKiPerSecond"/>. Segurar metade do tempo custa metade
    /// e entrega metade — a relação continua sendo uma linha reta, só mudou <i>quando</i> se paga.
    ///
    /// ⚠️ <b>Carga cheia para de cobrar, mesmo com o dedo ainda na tecla.</b> O preço é dos
    /// projéteis, e no topo não há mais nenhum sendo produzido — continuar cobrando seria cobrar
    /// por nada, e transformaria segurar a bola pronta na mão num vazamento de barra. O teto do
    /// gasto é o <see cref="KiAttack.GetKiCost()"/> da carga cheia, e o <see cref="_spent"/> é o
    /// que garante que ele valha em ticks inteiros — parar quando o <see cref="Ratio"/> chega a 1
    /// deixaria a última fração de tique cobrar a mais ou a menos conforme o framerate.
    ///
    /// O que isso compra sobre cobrar ao soltar: <b>a barra descendo é o medidor de carga</b>.
    /// Antes, saber o preço exigia soltar; agora ele acontece na tela enquanto se decide.
    ///
    /// <b>Ki no fim dispara sozinho.</b> Não há como continuar carregando sem pagar, e travar o
    /// jogador segurando uma tecla que não faz mais nada seria a pior saída — ele só descobriria ao
    /// soltar. Sai o que foi pago, e sai na hora. Isso <b>ignora o <c>MinChargeRatio</c></b>: o
    /// piso existe para um toque acidental não cobrar nada, e aqui já foi cobrado.
    ///
    /// ⚠️ <b>Cancelar não devolve.</b> Largar abaixo do <c>MinChargeRatio</c> não dispara, e o ki
    /// que a carga já consumiu não volta. Devolver exigiria rastrear o gasto tique a tique para
    /// desfazê-lo, e o que está em jogo é a fração de segundo antes do piso — alguns pontos de uma
    /// barra que se mede em centenas.
    ///
    /// <b>Estado do jogador local, e só dele</b>, como o cooldown do <c>KiAttack</c> e a fila do
    /// <see cref="KiBeam"/>. O que o vizinho precisa saber é <i>que</i> este jogador carrega
    /// alguma coisa, e isso atravessa como um bit em <c>NetState.IsChargingBeam</c> — não o
    /// relógio, porque <c>Time.time</c> vale o tempo de sessão de cada máquina.
    ///
    /// <b>O efeito na mão não mora aqui</b>, e sim no <see cref="KiBeamChargeEffects"/>, pelo mesmo
    /// motivo que o do carregamento de ki mora no <c>KiChargeEffects</c>: quem acende efeito é o
    /// laço do <c>RemoteEffects</c>, que passa por todo jogador carregado — o local inclusive.
    /// </summary>
    internal static class KiBeamCharge
    {
        /// <summary>O ataque sendo carregado pelo jogador local, ou null.</summary>
        internal static KiAttack Current { get; private set; }

        /// <summary>Instante (<c>Time.time</c>) em que a tecla foi apertada.</summary>
        private static float _startedAt;

        /// <summary>
        /// Sobra de tempo entre um tique de dreno e o seguinte.
        ///
        /// Tique fixo e não por frame, como o resto do ki: o custo de carregar não pode depender do
        /// framerate de quem carrega.
        /// </summary>
        private static float _tickAccumulator;

        /// <summary>
        /// Ki já cobrado por esta carga.
        ///
        /// É o que faz o gasto ter <b>teto</b>: a carga cheia custa exatamente o
        /// <c>KiAttack.GetKiCost()</c>, nem um ponto a mais, por mais tempo que o jogador segure
        /// depois disso.
        /// </summary>
        private static float _spent;

        /// <summary>O jogador local está carregando um ataque agora?</summary>
        internal static bool IsCharging(Player player)
        {
            return Current != null && player != null && player == Player.m_localPlayer;
        }

        /// <summary>
        /// A carga do jogador local chegou ao topo?
        ///
        /// Segurar além disto não compra mais nada — o teto é o <c>BeamCount</c> — e é justamente
        /// isso que o efeito de carga cheia existe para dizer.
        /// </summary>
        internal static bool IsFull(Player player)
        {
            return IsCharging(player) && Ratio >= 1f;
        }

        /// <summary>
        /// Quanto da carga já encheu, de 0 a 1. Zero sem carga em curso.
        ///
        /// Passa de 1 nunca: segurar além da carga cheia não acumula nada, e é assim de propósito
        /// — o teto é o <c>BeamCount</c>, e um excedente invisível faria o jogador segurar "mais um
        /// pouco por garantia" sem que isso comprasse coisa alguma.
        /// </summary>
        internal static float Ratio
        {
            get
            {
                if (Current == null)
                {
                    return 0f;
                }

                float time = Current.GetChargeTime();

                return time <= 0f ? 1f : Mathf.Clamp01((Time.time - _startedAt) / time);
            }
        }

        /// <summary>Começa a carregar. Substitui uma carga em curso, se houver.</summary>
        internal static void Begin(KiAttack attack)
        {
            Current = attack;
            _startedAt = Time.time;
            _tickAccumulator = 0f;
            _spent = 0f;

            SaiyaheimPlugin.LogVerbose(
                $"Ki attack '{attack.Id}': charging, {attack.GetChargeTime():0.##} s to full, " +
                $"{attack.GetChargeKiPerSecond():0.#} ki/s.");
        }

        /// <summary>
        /// Larga a carga sem disparar e sem cobrar. É o que um toque acidental faz, e também o que
        /// a morte, o menu e a troca de mundo fazem.
        /// </summary>
        internal static void Cancel()
        {
            Current = null;
            _startedAt = 0f;
            _tickAccumulator = 0f;
            _spent = 0f;
        }

        /// <summary>
        /// Cobra o ki do tique, e derruba a carga se o estado do jogador deixou de permitir.
        ///
        /// <b>Não dispara, e não lê tecla.</b> Quem solta é o <c>KiAttackManager</c>, que é quem
        /// conhece o keybind — este arquivo não conhece nenhum, do mesmo jeito que o
        /// <see cref="KiBeam"/> não. O que ele devolve é o pedido: <b>a barra acabou, solte
        /// agora.</b>
        /// </summary>
        /// <returns>true quando o ki acabou e o ataque tem que sair já.</returns>
        internal static bool Update(Player player, float deltaTime)
        {
            if (Current == null)
            {
                return false;
            }

            if (player == null || player.IsDead() || player.IsSleeping()
                || player.IsTeleporting() || player.InCutscene())
            {
                Cancel();
                return false;
            }

            // Ki desligado no meio da carga. Larga em vez de disparar: desligar o ki é dizer "não
            // quero as mecânicas agora", e responder com um Kamehameha seria o contrário disso.
            if (!KiManager.IsEnabled)
            {
                Cancel();
                return false;
            }

            return Drain(deltaTime);
        }

        /// <summary>
        /// Cobra o que a carga produziu desde o último tique — e <b>só enquanto ela produz</b>.
        ///
        /// O que se paga são projéteis. Na carga cheia não há mais nenhum saindo, então não há mais
        /// nada a pagar, e o dreno para com o dedo ainda na tecla. Sem isto, segurar a bola pronta
        /// na mão esvaziava a barra em silêncio — era o bug jogado em 2026-09-07.
        ///
        /// O teto é o custo da carga cheia e o acumulado é <see cref="_spent"/>, em vez de um
        /// simples "pare quando o <see cref="Ratio"/> chegar a 1": os dois relógios são o mesmo,
        /// mas o tique cai em cima do topo em algum ponto, e cortar pela razão deixaria essa última
        /// fração cobrar a mais ou a menos conforme o framerate. Pelo teto, a carga cheia custa
        /// exatamente o mesmo em qualquer máquina.
        ///
        /// A conta que decide o disparo forçado é feita <b>antes</b> do gasto: o
        /// <c>KiManager.Drain</c> gasta até zerar e não reclama, então depois dele não há mais como
        /// distinguir "coube exatamente" de "faltou metade".
        /// </summary>
        private static bool Drain(float deltaTime)
        {
            float owed = Current.GetKiCost() - _spent;

            if (owed <= 0f)
            {
                // Carga cheia e paga. Zera a sobra para o tempo segurado a mais não virar um tique
                // gigante caso alguma coisa reabra a cobrança.
                _tickAccumulator = 0f;
                return false;
            }

            float interval = Mathf.Max(0.01f, SaiyaheimConfig.KiTickInterval.Value);
            float perSecond = Current.GetChargeKiPerSecond();

            _tickAccumulator += deltaTime;

            while (_tickAccumulator >= interval && owed > 0f)
            {
                _tickAccumulator -= interval;

                float want = Mathf.Min(perSecond * interval, owed);
                bool starved = KiManager.Current < want;

                KiManager.Drain(want);
                _spent += want;
                owed -= want;

                if (starved)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
