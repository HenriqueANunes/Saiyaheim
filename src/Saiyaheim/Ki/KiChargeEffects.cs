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

        private static bool _disabled;

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

            layer.Vfx = null;
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
