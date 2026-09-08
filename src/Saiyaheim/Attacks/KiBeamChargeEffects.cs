using System;
using System.Collections.Generic;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Attacks
{
    /// <summary>
    /// A bola de ki enchendo na mão de quem carrega um Kamehameha.
    ///
    /// <b>É o pedaço que vende a cena</b>, e não a pose — é o que [[Animações]] registra no vault, e
    /// é por isso que ele vem antes da pose de duas mãos. Sem nada na mão, carregar é indistinguível
    /// de estar parado, e o jogador não tem como saber que a tecla pegou.
    ///
    /// <b>Um conjunto por jogador</b>, no molde exato do <c>KiChargeEffects</c>: o
    /// <c>RemoteEffects</c> varre todo jogador carregado e passa por aqui, o local inclusive. Nada
    /// aqui é objeto de rede — cada máquina lê a bandeira da ZDO e instancia o próprio efeito, com
    /// o <c>m_forceDisableInit</c> que o <see cref="AttachedEffect"/> sempre usou.
    ///
    /// <b>A bola cresce só na máquina de quem carrega.</b> A ZDO leva um bit, não o relógio da
    /// carga, e sincronizá-lo custaria um campo por jogador para uma diferença de tamanho que
    /// ninguém enxerga do outro lado do campo. Nos outros ela nasce e fica no tamanho cheio.
    ///
    /// <b>São três peças, e cada uma diz uma coisa diferente:</b>
    /// <list type="bullet">
    /// <item>o <c>ChargeBallPrefab</c> — a <b>bola</b>, que cresce. Diz <i>quanto</i>.</item>
    /// <item>o <c>ChargeEffectPrefab</c> — o estalo de energia sendo puxada. Diz <i>começou</i>.</item>
    /// <item>o <c>ChargeFullEffectPrefab</c> — o aviso de topo. Diz <i>parou de comprar</i>.</item>
    /// </list>
    /// A bola precisou de caminho próprio porque o jogo <b>não tem</b> um efeito que seja uma
    /// esfera de energia parada — o que ele tem é o projétil, e o <see cref="StaticProp"/> é quem
    /// tira dele o voar. As outras duas são efeitos comuns e passam pelo <see cref="AttachedEffect"/>.
    ///
    /// <b>Tudo preso ao osso da mão</b>, pelo <see cref="BodyAnchor"/>, e não medido a partir dos
    /// pés — então os três acompanham a pose de duas mãos sem uma linha de config a mais. Era o
    /// motivo pelo qual a decisão foi tomada antes de a pose existir, e ela se pagou.
    ///
    /// <b>Mas o deslocamento não é no espaço do osso, e sim no do jogador</b> — ver
    /// <see cref="Place"/>. Preso e medido são duas perguntas diferentes, e a pose de 2026-09-07
    /// foi o que mostrou isso: com o efeito seguindo o pulso, as três chaves de offset andavam numa
    /// diagonal que mudava a cada ajuste da pose.
    /// </summary>
    internal static class KiBeamChargeEffects
    {
        private sealed class Active
        {
            /// <summary>
            /// O osso (ou o corpo) em que as três peças estão penduradas. Guardado porque o
            /// <see cref="Place"/> precisa dele <b>todo frame</b>, e re-resolvê-lo passaria por um
            /// <c>GetComponent</c> por peça por frame.
            /// </summary>
            internal Transform Anchor;

            internal GameObject Vfx;

            /// <summary>A bola que cresce na mão, ou null quando a chave está vazia.</summary>
            internal GameObject Ball;

            /// <summary>O efeito de carga cheia, ou null enquanto a carga não encheu.</summary>
            internal GameObject Full;

            /// <summary>
            /// A carga já encheu nesta seguração.
            ///
            /// Campo próprio, e não <c>Full != null</c>: com o <c>ChargeFullEffectLoop</c>
            /// desligado o efeito é um estouro que se autodestrói, e o objeto vira null sozinho
            /// segundos depois. Perguntar pelo objeto faria o estalo tocar de novo — e de novo, a
            /// cada vez que ele acabasse de morrer — enquanto o jogador segurasse a tecla.
            /// </summary>
            internal bool Charged;
        }

        private static readonly Dictionary<Player, Active> Live = new Dictionary<Player, Active>();

        private static bool _disabled;

        /// <summary>
        /// Acende, apaga ou faz crescer a bola deste jogador.
        /// </summary>
        /// <param name="charged">
        /// A carga chegou ao topo. Vem da ZDO como bandeira, e não do <paramref name="ratio"/>,
        /// justamente para valer nos outros jogadores — onde o ratio não é verdade.
        /// </param>
        /// <param name="ratio">
        /// Quanto da carga encheu, de 0 a 1. Nos outros jogadores é sempre 1 — ver a doc da classe.
        /// </param>
        internal static void Update(Player player, bool charging, bool charged, float ratio)
        {
            if (_disabled || player == null)
            {
                return;
            }

            try
            {
                bool live = Live.TryGetValue(player, out Active active);

                if (charging && !live)
                {
                    active = Start(player, ratio);
                }
                else if (!charging && live)
                {
                    Cleanup(player, active);
                    return;
                }
                else if (charging)
                {
                    Grow(active, CurrentAttack(), ratio);
                }

                if (charging && charged)
                {
                    MarkFull(player, active);
                }
            }
            catch (Exception ex)
            {
                _disabled = true;
                Reset();
                SaiyaheimPlugin.Log.LogError($"Ki beam charge effects disabled after an error: {ex}");
            }
        }

        /// <summary>
        /// Reposiciona as peças deste jogador, se ele tiver alguma acesa.
        ///
        /// Chamado do <see cref="PoseDriver"/>, depois de a pose do frame estar escrita — ver a doc
        /// do <see cref="Place(Player, Active, KiAttack)"/> para o porquê de não ser no
        /// <c>Update</c> junto com o resto.
        /// </summary>
        internal static void Place(Player player)
        {
            if (_disabled || player == null || !Live.TryGetValue(player, out Active active))
            {
                return;
            }

            Place(player, active, CurrentAttack());
        }

        /// <summary>Este jogador saiu do alcance ou morreu; o objeto morreu junto, a entrada não.</summary>
        internal static void Forget(Player player)
        {
            Live.Remove(player);
        }

        /// <summary>Apaga tudo que está aceso. Ao sair do mundo, e quando um erro desliga isto.</summary>
        internal static void Reset()
        {
            foreach (Active active in Live.Values)
            {
                if (active.Vfx != null)
                {
                    UnityEngine.Object.Destroy(active.Vfx);
                }

                if (active.Ball != null)
                {
                    UnityEngine.Object.Destroy(active.Ball);
                }

                if (active.Full != null)
                {
                    UnityEngine.Object.Destroy(active.Full);
                }
            }

            Live.Clear();
        }

        private static Active Start(Player player, float ratio)
        {
            KiAttack attack = CurrentAttack();

            if (attack == null)
            {
                return null;
            }

            Transform anchor = BodyAnchor.Resolve(player, attack.Config.ChargeEffectAnchor.Value);
            string color = ResolveColor(attack);

            Active active = new Active { Anchor = anchor };

            string prefab = attack.Config.ChargeEffectPrefab.Value?.Trim() ?? string.Empty;
            if (prefab.Length > 0)
            {
                // forceLoop porque a carga DURA: um prefab de estouro morreria no meio dela e
                // deixaria a mão vazia com o jogador ainda segurando a tecla. Mesma lição da aura.
                //
                // Escala 1 aqui e o tamanho depois, no Grow: o tamanho é o que a carga mexe todo
                // frame, e pedir ao Spawn para aplicá-lo obrigaria a recriar o efeito por frame.
                // Sem offset aqui, e a posição vem logo abaixo pelo Place: o offset do Spawn é
                // medido no espaço do PAI, e o pai é o osso da mão.
                active.Vfx = AttachedEffect.Spawn(
                    player, prefab, color, 1f, forceLoop: true, lightIntensity: 1f,
                    burstDuration: 0f, localOffset: Vector3.zero, parent: anchor);
            }

            active.Ball = StaticProp.Spawn(
                anchor, attack.Config.ChargeBallPrefab.Value?.Trim() ?? string.Empty,
                ResolveBallColor(attack), Vector3.zero,
                StrippedEffect.ParseFilter(attack.Config.ChargeBallStrip.Value));

            // A entrada existe mesmo sem nada aceso: é ela que segura o "já encheu". Sem ela, um
            // ChargeEffectPrefab vazio levaria junto o aviso de carga cheia, que é chave própria.
            Live[player] = active;

            Grow(active, attack, ratio);
            Place(player, active, attack);

            return active;
        }

        /// <summary>
        /// Põe as três peças no lugar, <b>nos eixos do jogador</b>: direita, cima e frente do
        /// personagem, sempre, esteja o efeito preso à mão ou ao corpo.
        ///
        /// <b>Por que não é o offset do <c>Spawn</c>.</b> Aquele é medido no espaço do <i>pai</i>, e
        /// o pai é o osso da mão — cujos eixos não são os do jogador e ainda giram com o pulso.
        /// O resultado é que mexer numa das três chaves movia a bola numa diagonal que mudava
        /// conforme a pose, e as três deixavam de ser calibráveis: cada ajuste de pulso invalidava
        /// o anterior. Foi o que a calibragem da pose de duas mãos encontrou em 2026-09-07.
        ///
        /// <b>Todo frame, e não uma vez</b>, justamente porque a mão gira: um offset em eixos do
        /// jogador precisa ser reescrito sempre que o osso muda de orientação. É uma soma de vetor
        /// e três atribuições por jogador que carrega — o mesmo laço que já faz a bola crescer.
        ///
        /// <b>Preso à mão continua preso à mão</b>: o pai não muda, então as peças acompanham o
        /// gesto. O que muda é só a direção em que o deslocamento é medido — e para o
        /// <c>EffectAnchor.Body</c> nada muda, porque ali os dois espaços já eram o mesmo.
        ///
        /// ⚠️ <b>Quem chama é o <see cref="PoseDriver"/>, no LateUpdate, e não o laço do
        /// <c>RemoteEffects</c> como todo o resto deste arquivo.</b> Não é arrumação: é a única
        /// fase do frame em que o osso da mão está onde a tela vai mostrá-lo.
        ///
        /// A tentativa de fazer isto no <c>Update</c> piscou na tela em 2026-09-07, e o mecanismo é
        /// a armadilha do animator outra vez: ele avalia em passo de <b>física</b>, então nos
        /// frames <i>com</i> passo a mão já foi reescrita com a pose vanilla e a do mod ainda não
        /// voltou (ela só entra no LateUpdate), e nos frames <i>sem</i> passo ela ainda está com a
        /// pose do mod. Ler o osso ali devolve duas orientações alternadas, o que vira duas
        /// posições alternadas para a bola.
        ///
        /// ⚠️ <b>E escreve <c>localPosition</c>, não <c>position</c></b> — o primeiro bug do mesmo
        /// dia, e por outra razão: a peça é filha do osso, então fixar a posição de <b>mundo</b>
        /// conta o deslocamento duas vezes assim que o osso se mexer. Em espaço local a peça fica
        /// parada em relação ao osso e o movimento dele é o único que existe.
        /// </summary>
        private static void Place(Player player, Active active, KiAttack attack)
        {
            if (active == null || attack == null || active.Anchor == null || player == null)
            {
                return;
            }

            // De eixos do jogador para eixos do osso. InverseTransformVector e não Point: o que se
            // converte é um deslocamento, e a origem já é o próprio osso.
            Vector3 local = active.Anchor.InverseTransformVector(
                player.transform.rotation * Offset(attack));

            Move(active.Vfx, local);
            Move(active.Ball, local);
            Move(active.Full, local);
        }

        private static void Move(GameObject instance, Vector3 localPosition)
        {
            if (instance != null)
            {
                instance.transform.localPosition = localPosition;
            }
        }

        /// <summary>
        /// O deslocamento comum aos três. Um só, e não um por peça: eles são o mesmo sinal em três
        /// partes, e três posições separadas na tela leriam como três coisas acontecendo.
        /// </summary>
        private static Vector3 Offset(KiAttack attack)
        {
            return new Vector3(
                attack.Config.ChargeEffectSide.Value,
                attack.Config.ChargeEffectHeight.Value,
                attack.Config.ChargeEffectForward.Value);
        }

        /// <summary>
        /// A carga encheu: toca o aviso, uma vez só.
        ///
        /// <b>É o único sinal de que segurar mais parou de comprar coisa.</b> A bola para de
        /// crescer, mas "parou de crescer" é ilegível numa partícula que já se mexe sozinha — e sem
        /// isto o jogador seguraria por garantia, pagando tempo por nada.
        ///
        /// Sai no mesmo ponto do corpo que a bola, e de propósito: são dois sinais sobre a mesma
        /// coisa, e separá-los na tela leria como duas coisas acontecendo.
        /// </summary>
        private static void MarkFull(Player player, Active active)
        {
            if (active == null || active.Charged)
            {
                return;
            }

            active.Charged = true;

            KiAttack attack = CurrentAttack();
            if (attack == null)
            {
                return;
            }

            string prefab = attack.Config.ChargeFullEffectPrefab.Value?.Trim() ?? string.Empty;
            if (prefab.Length == 0)
            {
                return;
            }

            string color = attack.Config.ChargeFullEffectColor.Value?.Trim() ?? string.Empty;
            if (color.Length == 0)
            {
                color = attack.Config.ProjectileColor.Value?.Trim() ?? string.Empty;
            }

            // Sem offset, como as outras duas: quem posiciona é o Place, nos eixos do jogador.
            active.Full = AttachedEffect.Spawn(
                player, prefab, color, 1f,
                forceLoop: attack.Config.ChargeFullEffectLoop.Value,
                lightIntensity: 1f, burstDuration: 0f, localOffset: Vector3.zero,
                parent: active.Anchor);

            // Escala 1 no Spawn e o tamanho aqui, pelo EffectScale: o do Spawn não alcança
            // partícula filha nem largura de rastro. Ver EffectScale.
            EffectScale.Apply(active.Full, attack.Config.ChargeFullEffectScale.Value);

            // A bola sai de cena. Só quando o aviso de fato nasceu: um nome errado no .cfg deve
            // custar o polimento, nunca o único sinal de que há uma carga em curso — a mesma regra
            // que o ImpactEffect segue ao manter o estouro do prefab quando o nome não existe.
            if (active.Full != null && attack.Config.ChargeFullEffectReplaces.Value
                && active.Vfx != null)
            {
                UnityEngine.Object.Destroy(active.Vfx);
                active.Vfx = null;
            }
        }

        /// <summary>
        /// A bola enchendo: da <c>ChargeMinScale</c> até a <c>ChargeEffectScale</c> cheia — a mesma
        /// curva dos projéteis, de propósito. O que se vê na mão é do tamanho do que vai sair dela.
        /// </summary>
        private static void Grow(Active active, KiAttack attack, float ratio)
        {
            if (active == null || attack == null)
            {
                return;
            }

            float min = Mathf.Clamp01(attack.Config.ChargeMinScale.Value);
            float growth = Mathf.Lerp(min, 1f, Mathf.Clamp01(ratio));

            // Pelo EffectScale, e nao pelo transform: ele guarda a medida original no proprio
            // objeto (por isso crescer todo frame nao acumula) e alcanca o que a escala do
            // transform sozinha nao alcanca — particulas filhas, rastro e luz. Ver EffectScale.
            EffectScale.Apply(
                active.Vfx, Mathf.Max(0.01f, attack.Config.ChargeEffectScale.Value) * growth);

            // A bola segue a MESMA curva, de proposito: ela e' a previa do projetil, e o
            // GetProjectileScale usa esse mesmo ChargeMinScale. O que se ve na mao e' do tamanho
            // do que vai sair dela.
            EffectScale.Apply(
                active.Ball, Mathf.Max(0.01f, attack.Config.ChargeBallScale.Value) * growth);
        }

        /// <summary>
        /// Vazio segue a cor do projétil: o que junta na mão é a cor do que sai dela, e pedir a cor
        /// duas vezes só criaria a chance de as duas saírem de sincronia.
        /// </summary>
        private static string ResolveColor(KiAttack attack)
        {
            return Fallback(attack, attack.Config.ChargeEffectColor.Value);
        }

        private static string ResolveBallColor(KiAttack attack)
        {
            return Fallback(attack, attack.Config.ChargeBallColor.Value);
        }

        private static string Fallback(KiAttack attack, string configured)
        {
            string value = configured?.Trim() ?? string.Empty;

            return value.Length > 0
                ? value
                : attack.Config.ProjectileColor.Value?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// O ataque carregável da escada.
        ///
        /// <b>Sempre o primeiro</b>, e não o que o jogador selecionou: a ZDO diz que ele carrega,
        /// não O QUE ele carrega. Publicar o índice custaria bits no mesmo inteiro do
        /// <c>NetState</c> e só se pagaria no dia em que existirem dois ataques carregáveis com
        /// efeitos de cor diferente.
        /// </summary>
        private static KiAttack CurrentAttack()
        {
            foreach (KiAttack attack in KiAttackRegistry.All)
            {
                if (attack.IsCharged)
                {
                    return attack;
                }
            }

            return null;
        }

        private static void Cleanup(Player player, Active active)
        {
            if (active.Vfx != null)
            {
                UnityEngine.Object.Destroy(active.Vfx);
            }

            if (active.Ball != null)
            {
                UnityEngine.Object.Destroy(active.Ball);
            }

            // Pode já ter morrido sozinho: com o loop desligado é um estouro com vida própria. O
            // operador == da Unity trata o destruído como null, então isto cobre os dois casos.
            if (active.Full != null)
            {
                UnityEngine.Object.Destroy(active.Full);
            }

            Live.Remove(player);
        }
    }
}
