using System;
using System.Linq;
using Saiyaheim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Saiyaheim.Ki
{
    /// <summary>
    /// Barra de ki na HUD.
    ///
    /// Estratégia: **clonar a barra de eitr** em vez de construir UI do zero. O clone herda
    /// prefab, âncoras, animator de auto-hide, fonte e estilo — fica nativo de graça e continua
    /// nativo quando o jogo mudar o visual das barras.
    ///
    /// Posição e cor ficam em config porque acertar pixel exato é iteração pura, e recompilar a
    /// cada 4px seria insuportável.
    /// </summary>
    internal static class KiHud
    {
        private const string ObjectName = "Saiyaheim_KiBar";

        private static GameObject _root;
        private static RectTransform _rootRect;
        private static GuiBar _barFast;
        private static GuiBar _barSlow;
        private static TMP_Text _text;
        private static Animator _animator;
        private static Hud _hud;

        private static readonly int VisibleParam = Animator.StringToHash("Visible");

        /// <summary>
        /// Desliga a HUD depois de um erro. Roda todo frame: sem isso, uma exceção vira
        /// 60 linhas por segundo no log e enterra qualquer outra informação útil.
        /// </summary>
        private static bool _disabled;

        /// <summary>
        /// Segundos com o ki cheio antes da barra sumir. Não é balanceamento: é o mesmo valor
        /// que o <c>Hud.UpdateStamina</c> do jogo usa, para a barra de ki sumir no mesmo ritmo
        /// que a de stamina.
        /// </summary>
        private const float HideDelay = 1f;

        private static float _hideTimer;

        /// <summary>
        /// Chamado quando o .cfg é reescrito em disco. Posição e valores são lidos todo frame,
        /// então só a cor precisa ser reaplicada aqui.
        /// </summary>
        internal static void OnConfigReloaded()
        {
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
            catch (Exception ex)
            {
                _disabled = true;
                SaiyaheimPlugin.Log.LogError($"Barra de ki desligada após erro: {ex}");
            }
        }

        private static void UpdateInternal()
        {
            Hud hud = Hud.instance;

            if (hud == null || Player.m_localPlayer == null || !SaiyaheimConfig.ShowKiBar.Value)
            {
                if (_root != null)
                {
                    _root.SetActive(false);
                }
                return;
            }

            // A HUD é recriada ao trocar de mundo; o clone antigo morre junto.
            if (_root == null || _hud != hud)
            {
                if (!TryCreate(hud))
                {
                    return;
                }
            }

            _root.SetActive(true);
            UpdateLayout();
            UpdateValues();
        }

        private static bool TryCreate(Hud hud)
        {
            if (hud.m_eitrBarRoot == null)
            {
                return false;
            }

            _hud = hud;
            _root = UnityEngine.Object.Instantiate(hud.m_eitrBarRoot.gameObject, hud.m_eitrBarRoot.parent);
            _root.name = ObjectName;

            _rootRect = _root.GetComponent<RectTransform>();

            // O clone traz as duas GuiBar do eitr: a rápida e a lenta (a que "escorre" atrás).
            GuiBar[] bars = _root.GetComponentsInChildren<GuiBar>(true);
            if (bars.Length > 0) _barSlow = bars[0];
            if (bars.Length > 1) _barFast = bars[1];

            _text = _root.GetComponentInChildren<TMP_Text>(true);

            // Mantido ligado de propósito: é ele que controla o fade das barras vanilla.
            // Desligar deixaria o clone preso no alpha em que foi copiado.
            _animator = _root.GetComponentInChildren<Animator>(true);

            ApplyColor();

            SaiyaheimPlugin.Log.LogInfo(
                $"Barra de ki criada (GuiBar: {bars.Length}, texto: {_text != null}, animator: {_animator != null}).");

            // Se o clone não trouxe as barras, a estrutura do prefab mudou. Listar os filhos é
            // o que permite consertar sem precisar de outra rodada de teste na tela.
            if (bars.Length < 2)
            {
                string children = string.Join(", ",
                    _root.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray());
                SaiyaheimPlugin.Log.LogWarning($"Barra de ki com {bars.Length} GuiBar. Filhos: {children}");
            }

            return true;
        }

        private static void ApplyColor()
        {
            if (!ColorUtility.TryParseHtmlString(SaiyaheimConfig.KiBarColor.Value, out Color color))
            {
                SaiyaheimPlugin.Log.LogWarning(
                    $"KiBarColor '{SaiyaheimConfig.KiBarColor.Value}' não é uma cor válida. Use formato #RRGGBB.");
                return;
            }

            foreach (GuiBar bar in new[] { _barSlow, _barFast })
            {
                // GuiBar.m_barImage é privado na assembly real. Em vez de reflexão, pegamos o
                // Image pelo m_bar (público) — exatamente o que o GuiBar.Awake faz.
                Image image = bar != null && bar.m_bar != null ? bar.m_bar.GetComponent<Image>() : null;
                if (image != null)
                {
                    image.color = color;
                }
            }
        }

        private static void UpdateLayout()
        {
            if (_rootRect == null)
            {
                return;
            }

            // Mesma lógica que o jogo usa para as barras de stamina/eitr: elas sobem quando o
            // HUD de construção ou de navio está aberto. Sem isso a barra de ki ficaria por baixo.
            bool raised = (_hud.m_buildHud != null && _hud.m_buildHud.activeSelf)
                          || (_hud.m_shipHudRoot != null && _hud.m_shipHudRoot.activeSelf);

            float baseY = raised ? 285f : 130f;

            _rootRect.anchoredPosition = new Vector2(
                SaiyaheimConfig.KiBarOffsetX.Value,
                baseY + SaiyaheimConfig.KiBarOffsetY.Value);

            // Largura proporcional ao ki máximo, na mesma escala das barras nativas.
            float size = KiManager.Max / 25f * 32f;
            _rootRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, size + GameAccess.GetStaminaBarBorderBuffer(_hud));
            _barSlow?.SetWidth(size);
            _barFast?.SetWidth(size);
        }

        private static void UpdateValues()
        {
            float max = Mathf.Max(1f, KiManager.Max);
            float current = KiManager.State?.Current ?? 0f;
            float fraction = Mathf.Clamp01(current / max);

            _barSlow?.SetValue(fraction);
            _barFast?.SetValue(fraction);

            if (_text != null)
            {
                _text.text = Mathf.CeilToInt(current).ToString();
            }

            if (_animator != null)
            {
                _animator.SetBool(VisibleParam, ShouldBeVisible(current, max));
            }
        }

        /// <summary>
        /// Mesmo comportamento da barra de stamina: some quando está cheia, reaparece assim que
        /// o recurso é gasto. O atraso existe no jogo base — sem ele a barra pisca a cada gasto
        /// pequeno.
        /// </summary>
        private static bool ShouldBeVisible(float current, float max)
        {
            // Ki desligado não tem o que dizer: a barra some, inclusive com KiBarAlwaysVisible.
            if (!KiManager.IsEnabled)
            {
                _hideTimer = HideDelay;
                return false;
            }

            if (SaiyaheimConfig.KiBarAlwaysVisible.Value)
            {
                _hideTimer = 0f;
                return true;
            }

            bool active = current < max - 0.01f || KiManager.IsCharging;
            _hideTimer = active ? 0f : _hideTimer + Time.deltaTime;

            return _hideTimer < HideDelay;
        }
    }
}
