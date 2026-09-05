using TMPro;
using UnityEngine;

namespace Saiyaheim.Power
{
    /// <summary>
    /// O poder de luta na HUD, logo abaixo do minimapa.
    ///
    /// <b>Terceira posição tentada, e as duas anteriores ensinaram alguma coisa.</b> A nota de
    /// design pedia o número solto no HUD; o playtest de 2026-09-05 tentou primeiro o
    /// <b>inventário</b>, num bloco clonado do da armadura, e o resultado foi feio na tela.
    /// O problema não era o lugar — era o <b>molde</b>: o bloco da armadura carrega ícone, fundo e
    /// layout próprios, e clonar tudo isso para exibir um número trouxe junto uma caixa que não
    /// combinava com nada.
    ///
    /// <b>Aqui o molde é o rótulo do bioma</b> (<c>Minimap.m_biomeNameSmall</c>), e ele é o molde
    /// certo porque é <i>exatamente</i> o que se quer ser: um texto solto, sem caixa, já
    /// posicionado no canto do minimapa, com a fonte, o corpo, a cor e o contorno que o jogo usa
    /// para escrever ali. Clonar um irmão dele é herdar a aparência inteira sem herdar mobília.
    ///
    /// Sem ícone, por decisão do mesmo playtest: só <c>"Battle Power: 622"</c>.
    ///
    /// ⚠️ <b>Sem patch Harmony.</b> <c>Minimap.instance</c> e <c>m_biomeNameSmall</c> são públicos,
    /// e o texto é atualizado do <c>Update</c> do plugin.
    /// </summary>
    internal static class PowerHud
    {
        private const string ObjectName = "Saiyaheim_PowerHud";

        private static GameObject _root;
        private static RectTransform _rootRect;
        private static TMP_Text _text;
        private static Vector2 _biomeAnchoredPosition;

        /// <summary>
        /// Desliga a HUD depois de um erro, pelo mesmo motivo do <see cref="Ki.KiHud"/>: isto roda
        /// todo frame, e uma exceção viraria 60 linhas por segundo no log.
        /// </summary>
        private static bool _disabled;

        /// <summary>
        /// Último valor escrito. O <c>TMP_Text.text</c> remonta a malha do texto a cada
        /// atribuição, mesmo quando a string é idêntica — e este número fica parado a maior parte
        /// do tempo. Comparar antes evita esse trabalho em quase todo frame.
        /// </summary>
        private static int _lastValue = int.MinValue;

        internal static void OnConfigReloaded()
        {
            // Invalida o cache: o texto só é reescrito quando o VALOR muda, então trocar o rótulo
            // no .cfg não apareceria na tela até o poder de luta mexer sozinho. Sem esta linha,
            // editar o rótulo parece não funcionar.
            _lastValue = int.MinValue;

            // Posição e tamanho são lidos todo frame; só a cor precisa ser reaplicada aqui.
            if (_root != null && !_disabled)
            {
                ApplyColor();
            }
        }

        internal static void Update()
        {
            if (_disabled)
            {
                return;
            }

            try
            {
                UpdateInternal();
            }
            catch (System.Exception e)
            {
                _disabled = true;
                SaiyaheimPlugin.Log.LogError($"Power HUD disabled after an error: {e}");
            }
        }

        private static void UpdateInternal()
        {
            Player player = Player.m_localPlayer;
            Minimap map = Minimap.instance;

            if (player == null || map == null || !ShouldBeVisible(map))
            {
                if (_root != null)
                {
                    _root.SetActive(false);
                }

                return;
            }

            if (_root == null && !TryCreate(map))
            {
                return;
            }

            _root.SetActive(true);
            UpdateLayout();
            UpdateValue(player);
        }

        /// <summary>
        /// Some junto com o minimapa pequeno.
        ///
        /// <b>Seguir o minimapa e não a config</b> resolve dois casos de uma vez sem código
        /// próprio: o mapa grande aberto (que cobre a tela inteira, e um texto por cima dele seria
        /// lixo visual) e o jogador que desligou o minimapa nas opções do jogo — nesse segundo
        /// caso o número ficaria flutuando sozinho num canto vazio, ancorado em nada.
        /// </summary>
        private static bool ShouldBeVisible(Minimap map)
        {
            return SaiyaheimConfig.ShowPowerOnHud.Value
                   && map.m_smallRoot != null
                   && map.m_smallRoot.activeSelf;
        }

        private static bool TryCreate(Minimap map)
        {
            if (map.m_biomeNameSmall == null || map.m_biomeNameSmall.transform.parent == null)
            {
                _disabled = true;
                SaiyaheimPlugin.Log.LogError(
                    "Minimap.m_biomeNameSmall is missing — power HUD disabled.");
                return false;
            }

            Transform biome = map.m_biomeNameSmall.transform;

            _root = Object.Instantiate(biome.gameObject, biome.parent);
            _root.name = ObjectName;

            _rootRect = _root.GetComponent<RectTransform>();
            _text = _root.GetComponent<TMP_Text>();

            if (_rootRect == null || _text == null)
            {
                _disabled = true;
                SaiyaheimPlugin.Log.LogError("Cloned biome label has no RectTransform or text — power HUD disabled.");
                Object.Destroy(_root);
                _root = null;
                return false;
            }

            // Guardado uma vez: o offset de config é RELATIVO ao rótulo do bioma. Ler a posição
            // dele todo frame seria seguir um alvo que o jogo anima — o nome do bioma faz fade ao
            // entrar num novo, e o número iria junto.
            _biomeAnchoredPosition = biome is RectTransform biomeRect
                ? biomeRect.anchoredPosition
                : Vector2.zero;

            // ⚠️ Sem isto o tamanho da fonte é ignorado, e foi o bug do playtest de 2026-09-05: o
            // texto saía enorme e mexer na config não mudava nada. O rótulo do bioma vem com
            // auto-sizing ligado — o TMP recalcula o corpo entre fontSizeMin e fontSizeMax a cada
            // passada de layout, sobrescrevendo qualquer fontSize que a gente escreva. Desligar é
            // o que devolve o controle.
            _text.enableAutoSizing = false;

            ApplyColor();

            // O corpo original vai para o log porque ele é dado de asset e nao sai por
            // decompilação: é o único jeito de saber de que numero se está partindo ao calibrar o
            // PowerHudFontSize.
            SaiyaheimPlugin.Log.LogInfo(
                $"Power HUD created from the minimap biome label (its font size: {_text.fontSize}).");

            return true;
        }

        private static void UpdateLayout()
        {
            _rootRect.anchoredPosition = _biomeAnchoredPosition + new Vector2(
                SaiyaheimConfig.PowerHudOffsetX.Value,
                SaiyaheimConfig.PowerHudOffsetY.Value);

            // Corpo absoluto, e a fração que morava aqui foi um erro de raciocínio meu. O
            // argumento era resolução: um número fixo ficaria certo numa tela e errado nas
            // outras. Só que quem resolve isso é o CanvasScaler do próprio jogo — o corpo é dado
            // em unidades de canvas, não em pixels de monitor, então ele já acompanha a
            // resolução sozinho. A fração não protegia de nada e só escondia o número real.
            _text.fontSize = SaiyaheimConfig.PowerHudFontSize.Value;
        }

        private static void UpdateValue(Player player)
        {
            int value = Mathf.RoundToInt(PowerRating.GetDisplay(player));
            if (value == _lastValue)
            {
                return;
            }

            _lastValue = value;
            _text.text = SaiyaheimConfig.PowerHudLabel.Value + value;
        }

        private static void ApplyColor()
        {
            if (ColorUtility.TryParseHtmlString(SaiyaheimConfig.PowerHudColor.Value, out Color color))
            {
                _text.color = color;
            }
        }
    }
}
