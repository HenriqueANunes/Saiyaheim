using System;
using Saiyaheim.Transformations;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Ki
{
    /// <summary>
    /// Feedback do carregamento de ki: animação, efeito visual e som.
    ///
    /// Tudo reaproveitado do jogo — **nada exige Blender ou Unity**:
    ///
    /// - **Animação:** um emote existente em loop, via <c>Player.StartEmote(nome, oneshot: false)</c>.
    ///   O emote é escrito na ZDO, então **replica no multiplayer de graça** — outros jogadores
    ///   veem a pose sem nenhum RPC nosso. O jogo também interrompe emote sozinho quando o
    ///   jogador anda, o que casa com carregar parado.
    /// - **Visual e som:** prefabs `fx_`/`sfx_` do jogo, instanciados presos ao transform do
    ///   jogador pelo <see cref="AttachedEffect"/> — que é quem sabe tingir sem sujar o material
    ///   compartilhado e desarmar o autodestruir dos prefabs. Ver [[Prefabs do Jogo]] no vault
    ///   para a paleta levantada.
    ///
    /// Os nomes de prefab e de emote ficam **em config**, não no código: qual pose e qual efeito
    /// "lê" como carregar ki é julgamento visual, e quem vê a tela é o Henrique. Trocar deve
    /// custar editar um .cfg, não uma recompilação.
    ///
    /// <b>Carregar transformado usa a cor da forma</b>, não a azul do config — ver
    /// <see cref="ResolveColor"/>.
    /// </summary>
    internal static class KiChargeEffects
    {
        private static GameObject _vfx;
        private static GameObject _sfx;
        private static bool _emoteStarted;
        private static bool _disabled;

        /// <summary>
        /// A cor com que o <see cref="_vfx"/> foi criado. A cor é aplicada nos materiais no momento
        /// da instanciação, então mudá-la depois significa recriar o efeito — e comparar contra
        /// este campo é como sabemos que ela mudou.
        /// </summary>
        private static string _vfxColor;

        private static bool IsActive => _vfx != null || _sfx != null || _emoteStarted;

        internal static void Update(Player player, bool charging)
        {
            if (_disabled)
            {
                return;
            }

            try
            {
                if (charging && !IsActive)
                {
                    Start(player);
                }
                else if (!charging && IsActive)
                {
                    Stop(player);
                }
                else if (charging)
                {
                    RefreshColor(player);
                }
            }
            catch (Exception ex)
            {
                _disabled = true;
                Cleanup();
                SaiyaheimPlugin.Log.LogError($"Charging effects disabled after an error: {ex}");
            }
        }

        /// <summary>
        /// Zera o estado sem tentar tocar no jogador — usado quando ele deixou de existir
        /// (morte, saída do mundo). Os objetos de efeito são filhos do transform dele e já
        /// morreram junto; só as referências ficaram.
        /// </summary>
        internal static void Reset()
        {
            _vfx = null;
            _sfx = null;
            _vfxColor = null;
            _emoteStarted = false;
        }

        private static void Start(Player player)
        {
            if (SaiyaheimConfig.ChargeEmote.Value.Length > 0)
            {
                // oneshot: false = fica em loop até mandarmos parar.
                _emoteStarted = player.StartEmote(SaiyaheimConfig.ChargeEmote.Value, oneshot: false);
            }

            _vfxColor = ResolveColor(player);

            _vfx = Spawn(SaiyaheimConfig.ChargeEffectPrefab.Value, player, _vfxColor);
            _sfx = Spawn(SaiyaheimConfig.ChargeSoundPrefab.Value, player, _vfxColor);
        }

        /// <summary>
        /// Recria o efeito visual se a cor certa mudou no meio do carregamento.
        ///
        /// O caso que importa é transformar (ou cair da forma) **com a tecla de carregar
        /// pressionada**: sem isto o jogador ficaria carregando em azul dentro do SSJ até soltar a
        /// tecla. Comparar a cor a cada frame é uma comparação de string; recriar só acontece
        /// quando ela de fato muda.
        ///
        /// Só o visual é refeito. O som continua tocando e o emote continua em loop — reiniciar
        /// qualquer um dos dois seria audível, e nenhum tem cor.
        /// </summary>
        private static void RefreshColor(Player player)
        {
            string color = ResolveColor(player);
            if (color == _vfxColor)
            {
                return;
            }

            _vfxColor = color;

            if (_vfx != null)
            {
                UnityEngine.Object.Destroy(_vfx);
            }

            _vfx = Spawn(SaiyaheimConfig.ChargeEffectPrefab.Value, player, color);
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
        /// </summary>
        private static string ResolveColor(Player player)
        {
            Transformation active = TransformationRegistry.GetActive(player);

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

        private static void Stop(Player player)
        {
            if (_emoteStarted)
            {
                GameAccess.StopEmote(player);
                _emoteStarted = false;
            }

            Cleanup();
        }

        private static void Cleanup()
        {
            if (_vfx != null)
            {
                UnityEngine.Object.Destroy(_vfx);
                _vfx = null;
            }

            if (_sfx != null)
            {
                UnityEngine.Object.Destroy(_sfx);
                _sfx = null;
            }
        }
    }
}
