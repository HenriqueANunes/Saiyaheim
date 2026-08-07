using System;
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
    /// </summary>
    internal static class KiChargeEffects
    {
        private static GameObject _vfx;
        private static GameObject _sfx;
        private static bool _disabled;

        /// <summary>
        /// A cor com que o <see cref="_vfx"/> foi criado. A cor é aplicada nos materiais no momento
        /// da instanciação, então mudá-la depois significa recriar o efeito — e comparar contra
        /// este campo é como sabemos que ela mudou.
        /// </summary>
        private static string _vfxColor;

        private static bool IsActive => _vfx != null || _sfx != null;

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
                    Cleanup();
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
        }

        private static void Start(Player player)
        {
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
