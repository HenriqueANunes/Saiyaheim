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
    /// </summary>
    internal static class KiChargeEffects
    {
        /// <summary>
        /// O que está aceso num jogador. Ausente quer dizer "não está carregando" — o dicionário é
        /// a resposta, não um cache.
        /// </summary>
        private sealed class Active
        {
            internal GameObject Vfx;
            internal GameObject Sfx;

            /// <summary>
            /// A cor com que o <see cref="Vfx"/> foi criado. A cor é aplicada nos materiais no
            /// momento da instanciação, então mudá-la depois significa recriar o efeito — e
            /// comparar contra este campo é como sabemos que ela mudou.
            /// </summary>
            internal string Color;
        }

        private static readonly Dictionary<Player, Active> Live = new Dictionary<Player, Active>();

        private static bool _disabled;

        internal static void Update(Player player, bool charging)
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
                    Start(player);
                }
                else if (!charging && live)
                {
                    Cleanup(player, active);
                }
                else if (charging)
                {
                    RefreshColor(player, active);
                }
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
        /// <b>Destrói, não só esquece.</b> Enquanto isto servia só o jogador local dava para largar
        /// as referências e confiar que os objetos morriam junto com ele; agora o caminho de erro
        /// pode disparar com jogadores vivos na tela, e esquecer deixaria o brilho aceso para
        /// sempre, sem ninguém que soubesse apagá-lo.
        /// </summary>
        internal static void Reset()
        {
            foreach (Active active in Live.Values)
            {
                if (active.Vfx != null)
                {
                    UnityEngine.Object.Destroy(active.Vfx);
                }

                if (active.Sfx != null)
                {
                    UnityEngine.Object.Destroy(active.Sfx);
                }
            }

            Live.Clear();
        }

        private static void Start(Player player)
        {
            string color = ResolveColor(player);

            Live[player] = new Active
            {
                Color = color,
                Vfx = Spawn(SaiyaheimConfig.ChargeEffectPrefab.Value, player, color),
                Sfx = Spawn(SaiyaheimConfig.ChargeSoundPrefab.Value, player, color)
            };
        }

        /// <summary>
        /// Recria o efeito visual se a cor certa mudou no meio do carregamento.
        ///
        /// O caso que importa é transformar (ou cair da forma) **com a tecla de carregar
        /// pressionada**: sem isto o jogador ficaria carregando em azul dentro do SSJ até soltar a
        /// tecla. Comparar a cor a cada frame é uma comparação de string; recriar só acontece
        /// quando ela de fato muda.
        ///
        /// Só o visual é refeito. O som continua tocando — reiniciá-lo seria audível, e ele não
        /// tem cor.
        /// </summary>
        private static void RefreshColor(Player player, Active active)
        {
            string color = ResolveColor(player);
            if (color == active.Color)
            {
                return;
            }

            active.Color = color;

            if (active.Vfx != null)
            {
                UnityEngine.Object.Destroy(active.Vfx);
            }

            active.Vfx = Spawn(SaiyaheimConfig.ChargeEffectPrefab.Value, player, color);
        }

        /// <summary>
        /// A cor do carregamento agora: <b>a da forma ativa, se houver</b>, senão a do config.
        ///
        /// Carregar transformado brilhando de azul leria como duas mecânicas soltas acontecendo no
        /// mesmo corpo. Com a cor da forma, o carregamento e a aura da transformação viram a mesma
        /// coisa acontecendo mais forte — que é o que de fato está acontecendo.
        ///
        /// Forma com <c>AuraColor</c> vazio cai na cor de carregamento em vez de na cor crua do
        /// prefab: vazio ali quer dizer "não tinja a aura", não "volte ao azul do jogo".
        ///
        /// <b>Lê a forma pelo <see cref="NetState"/> e não pelo <c>SEMan</c></b>, senão a resposta
        /// valeria só para o jogador local e um amigo em SSJ carregaria em azul.
        /// </summary>
        private static string ResolveColor(Player player)
        {
            Transformation active = TransformationRegistry.At(NetState.GetFormIndex(player));

            if (active != null && !string.IsNullOrEmpty(active.Config.AuraColor.Value))
            {
                return active.Config.AuraColor.Value;
            }

            return SaiyaheimConfig.ChargeEffectColor.Value;
        }

        private static GameObject Spawn(string prefabName, Player player, string color)
        {
            return AttachedEffect.Spawn(
                player,
                prefabName,
                color,
                SaiyaheimConfig.ChargeEffectScale.Value,
                SaiyaheimConfig.ChargeEffectForceLoop.Value);
        }

        private static void Cleanup(Player player, Active active)
        {
            if (active.Vfx != null)
            {
                UnityEngine.Object.Destroy(active.Vfx);
            }

            if (active.Sfx != null)
            {
                UnityEngine.Object.Destroy(active.Sfx);
            }

            Live.Remove(player);
        }
    }
}
