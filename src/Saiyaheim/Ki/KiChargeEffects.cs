using System;
using System.Collections.Generic;
using Saiyaheim.Net;
using Saiyaheim.Transformations;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Ki
{
    /// <summary>
    /// Feedback do carregamento de ki: efeito visual e som.
    ///
    /// **Visual e som:** prefabs `fx_`/`sfx_` do jogo, instanciados presos ao transform do
    /// jogador pelo <see cref="AttachedEffect"/> — que é quem sabe tingir sem sujar o material
    /// compartilhado e desarmar o autodestruir dos prefabs. Ver [[Prefabs do Jogo]] no vault
    /// para a paleta levantada. Nada disto exige Blender ou Unity.
    ///
    /// **A pose saiu daqui.** Até 2026-08-07 este arquivo também tocava um emote em loop
    /// (<c>roar</c>), que era o que fazia as vezes de animação. Quem faz a pose agora é a
    /// <see cref="KiChargePose"/>, procedural, e o emote foi removido: um grito por cima de uma
    /// pose escrita músculo a músculo é uma animação brigando com a outra, e na tela ficou ruim.
    ///
    /// Os nomes de prefab ficam **em config**, não no código: qual efeito "lê" como carregar ki é
    /// julgamento visual, e quem vê a tela é o Henrique. Trocar deve custar editar um .cfg, não
    /// uma recompilação.
    ///
    /// <b>Carregar transformado usa a cor da forma</b>, não a azul do config — ver
    /// <see cref="ResolveColor"/>.
    ///
    /// <b>Etapa 8: um conjunto de efeitos por jogador, não um só.</b> Até aqui esta classe tinha
    /// dois campos estáticos e servia exclusivamente o jogador local — um amigo carregando ki era
    /// invisível. Quem diz quem está carregando agora é o <see cref="NetState"/>, e quem varre
    /// todos os jogadores é o <see cref="RemoteEffects"/>. O <c>m_forceDisableInit</c> do
    /// <see cref="AttachedEffect"/> continua igual e continua certo: o efeito é <b>criado
    /// localmente em cada máquina</b> a partir da bandeira, e não replicado como objeto de rede.
    ///
    /// <b>Etapa 14: a aura tem duas camadas.</b> Com o Kaioken ligado, na forma base a aura
    /// vermelha do tier <b>substitui</b> a azul; transformado, a da forma fica e a vermelha vem
    /// <b>por cima</b>, maior, contornando a outra (<c>KaiokenAuraScaleOverForm</c>). A aura e o som
    /// chegam por argumentos separados: hoje os dois são "está carregando", mas o toggle de aura
    /// previsto acende só a aura, e o Kaioken vem junto sem mudar nada aqui.
    /// </summary>
    internal static class KiChargeEffects
    {
        /// <summary>
        /// Uma camada de aura: o objeto e a cor com que ele foi criado. A cor é aplicada nos
        /// materiais na instanciação, então mudá-la significa recriar — e comparar contra
        /// <see cref="Color"/> é como sabemos que ela mudou.
        /// </summary>
        private sealed class Layer
        {
            internal GameObject Vfx;
            internal string Color;
            internal float Scale;

            /// <summary>
            /// A chama grande em cartão, só com o corpo deitado no voo. Ver <see cref="Lean"/>.
            /// </summary>
            internal GameObject Card;
        }

        /// <summary>
        /// O que está aceso num jogador. Ausente quer dizer "nem aura nem som" — o dicionário é a
        /// resposta, não um cache.
        /// </summary>
        private sealed class Active
        {
            /// <summary>A aura de sempre: azul na base, da forma transformado, vermelha com Kaioken na base.</summary>
            internal readonly Layer Aura = new Layer();

            /// <summary>A vermelha por cima, só com Kaioken <b>e</b> forma.</summary>
            internal readonly Layer Kaioken = new Layer();

            internal GameObject Sfx;
        }

        private static readonly Dictionary<Player, Active> Live = new Dictionary<Player, Active>();

        /// <summary>
        /// Aura acesa o tempo todo (2026-10-16), escolha do jogador no menu radial, publicada pela
        /// ZDO para os outros verem. <b>Vale só até fechar o jogo</b>, pedido do Henrique antes da
        /// 0.10.0: cada sessão começa com a aura só ao carregar. Por isso fica num campo, e não no
        /// <c>m_customData</c> como o rabo e o modo do voo.
        /// </summary>
        private static bool _alwaysOn;

        /// <summary>
        /// Onde a escolha ficava salva no personagem durante o desenvolvimento. Lida só para ser
        /// apagada, para quem testou a versão que salvava não carregar o "1" no save para sempre.
        /// </summary>
        private const string LegacyKeyAlwaysOn = "saiyaheim.auraAlwaysOn";

        private static bool _disabled;

        /// <summary>O jogador local escolheu a aura acesa o tempo todo, nesta sessão.</summary>
        internal static bool IsAlwaysOn(Player player)
        {
            if (player == null || player != Player.m_localPlayer)
            {
                return false;
            }

            if (player.m_customData != null && player.m_customData.ContainsKey(LegacyKeyAlwaysOn))
            {
                player.m_customData.Remove(LegacyKeyAlwaysOn);
            }

            return _alwaysOn;
        }

        /// <summary>Liga ou desliga a aura permanente até fechar o jogo. Vale no próximo frame, pela ZDO.</summary>
        internal static void SetAlwaysOn(Player player, bool on)
        {
            _alwaysOn = on;
        }

        /// <param name="charging">Toca o som do carregamento.</param>
        /// <param name="auraOn">Acende a aura, com a camada do Kaioken se houver.</param>
        internal static void Update(Player player, bool charging, bool auraOn)
        {
            if (_disabled || player == null)
            {
                return;
            }

            try
            {
                bool live = Live.TryGetValue(player, out Active active);

                if (!charging && !auraOn)
                {
                    if (live)
                    {
                        Cleanup(player, active);
                    }

                    return;
                }

                if (!live)
                {
                    active = new Active();
                    Live[player] = active;
                }

                SyncSound(player, active, charging);
                SyncAura(player, active, auraOn);
            }
            catch (Exception ex)
            {
                _disabled = true;
                Reset();
                SaiyaheimPlugin.Log.LogError($"Charging effects disabled after an error: {ex}");
            }
        }

        /// <summary>
        /// Este jogador deixou de existir (morte, saída do mundo, saiu do alcance). Os objetos de
        /// efeito são filhos do transform dele e já morreram junto; só a entrada ficou.
        /// </summary>
        internal static void Forget(Player player)
        {
            Live.Remove(player);
        }

        /// <summary>
        /// Apaga tudo que está aceso. Usado ao sair do mundo e quando um erro desliga os efeitos.
        ///
        /// <b>Destrói, não só esquece</b>: o caminho de erro pode disparar com jogadores vivos na
        /// tela, e esquecer deixaria o brilho aceso para sempre.
        /// </summary>
        internal static void Reset()
        {
            foreach (Active active in Live.Values)
            {
                DestroyAll(active);
            }

            Live.Clear();
        }

        /// <summary>
        /// O som não tem cor nem camada, e não é recriado quando a aura muda: reiniciá-lo seria
        /// audível.
        /// </summary>
        private static void SyncSound(Player player, Active active, bool charging)
        {
            if (charging && active.Sfx == null)
            {
                active.Sfx = Spawn(SaiyaheimConfig.ChargeSoundPrefab, player, SaiyaheimConfig.ChargeEffectColor,
                    SaiyaheimConfig.ChargeEffectScale);
            }
            else if (!charging && active.Sfx != null)
            {
                UnityEngine.Object.Destroy(active.Sfx);
                active.Sfx = null;
            }
        }

        /// <summary>
        /// Acende, troca ou apaga as duas camadas conforme forma e Kaioken de agora.
        ///
        /// O caso que importa é mudar de estado <b>com a aura acesa</b> — transformar, ligar ou subir
        /// o Kaioken com a tecla de carregar pressionada. Comparar cor e escala a cada frame é
        /// barato; recriar só acontece quando uma das duas de fato muda.
        /// </summary>
        private static void SyncAura(Player player, Active active, bool auraOn)
        {
            if (!auraOn)
            {
                Clear(active.Aura);
                Clear(active.Kaioken);
                return;
            }

            Transformation form = TransformationRegistry.At(NetState.GetFormIndex(player));
            Kaioken.KaiokenTier tier = Kaioken.KaiokenTier.At(NetState.GetKaiokenIndex(player));
            float scale = SaiyaheimConfig.ChargeEffectScale;

            // Na base o Kaioken toma o lugar da azul, no tamanho normal; transformado, a da forma
            // fica e o Kaioken vai para a camada de fora.
            string auraColor = form == null && tier != null ? tier.GetAuraColor() : ResolveColor(form);

            Set(player, active.Aura, auraColor, scale);

            if (form != null && tier != null)
            {
                Set(player, active.Kaioken, tier.GetAuraColor(),
                    scale * Mathf.Max(1f, SaiyaheimConfig.KaiokenAuraScaleOverForm));
            }
            else
            {
                Clear(active.Kaioken);
            }

            Lean(player, active.Aura);
            Lean(player, active.Kaioken);
        }

        /// <summary>
        /// Inclina a aura junto com o corpo no voo (2026-10-16). O <c>FlightPose</c> deita o corpo
        /// para a frente, e a aura, presa ao transform do jogador, ficava de pé atravessada nele.
        ///
        /// <b>Girar o efeito inteiro basta</b>, como o Henrique apontou: as partículas da "Aura base"
        /// simulam no espaço local do efeito (<c>moveWithTransform: 0</c> no prefab), então elas e a
        /// direção em que esticam giram junto com a raiz. A raiz do prefab fica a 1 m dos pés, na
        /// altura do quadril, e o giro é em volta dela — o mesmo ponto em volta do qual o corpo
        /// deita. Mesmo eixo e sinal do <c>FlightPose.PitchForward</c>: positivo é barriga para baixo.
        ///
        /// A inclinação vem do frame anterior (o pose roda depois deste Update). Ela muda suave, e
        /// isso não pisca como ler osso no Update piscava na tentativa de 2026-09-26.
        ///
        /// <b>A chama grande não deita com o giro.</b> Ela é praticamente uma partícula só, enorme,
        /// em <i>Stretched Billboard</i> com <i>Freeform Stretching</i>, desenhada sempre de pé na tela:
        /// sonda no Unity em 2026-10-16 (<c>Editor/AuraLeanProbe.cs</c> no projeto Unity) — girar a
        /// raiz, mudar a velocidade, girar a partícula e trocar o alinhamento não a inclinam.
        ///
        /// <b>Então, deitado, ela vira um cartão</b> (opção C, aprovada pelo Henrique nos renders da
        /// sonda): uma cópia do sistema da chama, sem os filhos, desenhada como <i>Billboard</i> em
        /// alinhamento local, com a base da chama para os pés e a ponta para a cabeça, girando em
        /// volta do eixo do corpo para ficar de frente para a câmera de quem olha. Três
        /// cartões cruzados também deitavam, mas mostravam duas ou três chamas sobrepostas; desligar
        /// o freeform desenhava a chama de lado e na altura da cabeça. A chama original some
        /// enquanto o cartão existe e volta fora do voo rápido, onde é a aprovada.
        ///
        /// No primeiro teste no jogo (2026-10-16) o cartão contornou o corpo certo; o Henrique pediu
        /// a troca só no voo rápido e o cartão mais largo, porque a aura de pé é mais arredondada.
        /// </summary>
        private static void Lean(Player player, Layer layer)
        {
            if (layer.Vfx == null)
            {
                return;
            }

            float pitch = Flight.FlightPose.GetPitch(player);
            Transform root = layer.Vfx.transform;
            root.localRotation = Quaternion.AngleAxis(pitch, Vector3.right);

            // Só no voo rápido (pedido do Henrique no primeiro teste: pela inclinação, descer devagar
            // trocava a chama e a troca brusca ficava estranha). Com folga na saída, para não piscar
            // perto do limite.
            ParticleSystemRenderer flame = layer.Vfx.GetComponent<ParticleSystemRenderer>();
            float enter = SaiyaheimConfig.AuraFlightCardSpeed;
            float fast = Flight.FlightPose.GetFast(player);
            bool card = flame != null && enter < 1f && fast >= (layer.Card != null ? enter - 0.2f : enter);

            if (!card)
            {
                if (layer.Card != null)
                {
                    UnityEngine.Object.Destroy(layer.Card);
                    layer.Card = null;
                }

                if (flame != null && !flame.enabled)
                {
                    flame.enabled = true;
                }

                return;
            }

            if (layer.Card == null)
            {
                layer.Card = MakeCard(layer.Vfx);
                flame.enabled = false;
            }

            // De frente para a câmera de quem olha, girando só em volta do eixo do corpo.
            Transform cardTransform = layer.Card.transform;
            cardTransform.localPosition = root.localPosition;
            Vector3 axis = root.up;
            Vector3 eye = GameCamera.instance != null
                ? GameCamera.instance.transform.position
                : cardTransform.position - root.forward;
            Vector3 toEye = Vector3.ProjectOnPlane(eye - cardTransform.position, axis);
            if (toEye.sqrMagnitude < 1e-6f)
            {
                toEye = Vector3.ProjectOnPlane(-root.forward, axis);
            }

            cardTransform.rotation = Quaternion.LookRotation(-toEye.normalized, axis);
        }

        /// <summary>
        /// A cópia da chama grande em cartão: o sistema da raiz da aura, sem os filhos (faíscas,
        /// clarões e onda continuam na aura, deitando com ela). Números da sonda no Unity.
        /// </summary>
        private static GameObject MakeCard(GameObject vfx)
        {
            GameObject card = UnityEngine.Object.Instantiate(vfx, vfx.transform.parent);
            card.name = vfx.name + " (card)";
            foreach (Transform child in card.transform)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }

            card.transform.localScale = vfx.transform.localScale;

            ParticleSystem system = card.GetComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = card.GetComponent<ParticleSystemRenderer>();
            ParticleSystem.MainModule main = system.main;
            // Sem giro de textura: de pé, o cartão assim é a chama original (base larga embaixo,
            // ponta em cima), conferido na sonda. O giro de 90° da primeira versão deitava a chama de
            // lado no cartão, e a "largura" crescia para o lado da ponta (visto pelo Henrique).
            float size = main.startSize.constant * SaiyaheimConfig.AuraFlightCardSize;
            main.startSize3D = true;
            main.startSizeX = size * SaiyaheimConfig.AuraFlightCardWidth;   // em volta do corpo
            main.startSizeY = size * SaiyaheimConfig.AuraFlightCardLength;  // ao longo do corpo
            main.startSizeZ = size;

            renderer.enabled = true;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.pivot = Vector3.zero;

            system.Clear(true);
            system.Play(true);
            return card;
        }

        private static void Set(Player player, Layer layer, string color, float scale)
        {
            if (layer.Vfx != null && layer.Color == color && Mathf.Approximately(layer.Scale, scale))
            {
                return;
            }

            Clear(layer);
            layer.Color = color;
            layer.Scale = scale;
            layer.Vfx = Spawn(SaiyaheimConfig.ChargeEffectPrefab, player, color, scale);
        }

        private static void Clear(Layer layer)
        {
            if (layer.Vfx != null)
            {
                UnityEngine.Object.Destroy(layer.Vfx);
            }

            if (layer.Card != null)
            {
                UnityEngine.Object.Destroy(layer.Card);
            }

            layer.Vfx = null;
            layer.Card = null;
            layer.Color = null;
        }

        /// <summary>
        /// A cor da aura sem Kaioken: <b>a da forma, se houver</b>, senão a do config.
        ///
        /// Carregar transformado brilhando de azul leria como duas mecânicas soltas acontecendo no
        /// mesmo corpo. Com a cor da forma, o carregamento e a aura da transformação viram a mesma
        /// coisa acontecendo mais forte.
        ///
        /// Forma com <c>AuraColor</c> vazio cai na cor de carregamento em vez de na cor crua do
        /// prefab: vazio ali quer dizer "não tinja a aura", não "volte ao azul do jogo".
        ///
        /// <b>A forma vem do <see cref="NetState"/> e não do <c>SEMan</c></b>, senão a resposta
        /// valeria só para o jogador local e um amigo em SSJ carregaria em azul. O tier de Kaioken
        /// vem de lá pelo mesmo motivo.
        /// </summary>
        private static string ResolveColor(Transformation form)
        {
            if (form != null && !string.IsNullOrEmpty(form.Config.AuraColor))
            {
                return form.Config.AuraColor;
            }

            return SaiyaheimConfig.ChargeEffectColor;
        }

        private static GameObject Spawn(string prefabName, Player player, string color, float scale)
        {
            return AttachedEffect.Spawn(
                player,
                prefabName,
                color,
                scale,
                SaiyaheimConfig.ChargeEffectForceLoop);
        }

        private static void DestroyAll(Active active)
        {
            Clear(active.Aura);
            Clear(active.Kaioken);

            if (active.Sfx != null)
            {
                UnityEngine.Object.Destroy(active.Sfx);
                active.Sfx = null;
            }
        }

        private static void Cleanup(Player player, Active active)
        {
            DestroyAll(active);
            Live.Remove(player);
        }
    }
}
