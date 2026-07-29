using System.Globalization;
using UnityEngine;

namespace Saiyaheim.Ki
{
    /// <summary>
    /// Estado de ki de um jogador.
    ///
    /// Persistido em <c>Player.m_customData</c> — um <c>Dictionary&lt;string,string&gt;</c> que o
    /// próprio Valheim serializa junto do personagem. Não precisamos escrever serialização nem
    /// patchar Save/Load: basta escrever no dicionário.
    /// </summary>
    internal class KiState
    {
        // Prefixadas com o nome do mod para não colidir com outros mods no mesmo dicionário.
        private const string KeyCurrent = "saiyaheim.ki";
        private const string KeyEnabled = "saiyaheim.kiEnabled";

        /// <summary>Ki atual.</summary>
        public float Current;

        /// <summary>
        /// Ki ligado/desligado pelo jogador. Desligado se comporta como ki zerado:
        /// sem bônus, sem maestria acumulando, sem componente de ki no power level.
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// Momento (<c>Time.time</c>) a partir do qual a regeneração passiva volta.
        /// Não é persistido: um delay de segundos não sobrevive a logout de propósito.
        /// </summary>
        public float RegenBlockedUntil;

        public static KiState Load(Player player)
        {
            var state = new KiState
            {
                Current = SaiyaheimConfig.MaxKi.Value,
                Enabled = SaiyaheimConfig.KiEnabledByDefault.Value
            };

            if (player == null || player.m_customData == null)
            {
                return state;
            }

            if (player.m_customData.TryGetValue(KeyCurrent, out string rawCurrent) &&
                float.TryParse(rawCurrent, NumberStyles.Float, CultureInfo.InvariantCulture, out float current))
            {
                state.Current = Mathf.Clamp(current, 0f, SaiyaheimConfig.MaxKi.Value);
            }

            if (player.m_customData.TryGetValue(KeyEnabled, out string rawEnabled))
            {
                state.Enabled = rawEnabled == "1";
            }

            return state;
        }

        public void Save(Player player)
        {
            if (player == null || player.m_customData == null)
            {
                return;
            }

            // InvariantCulture obrigatório: em pt-BR o separador decimal é vírgula, e um save
            // escrito com vírgula não volta a ler em outra locale.
            player.m_customData[KeyCurrent] = Current.ToString("R", CultureInfo.InvariantCulture);
            player.m_customData[KeyEnabled] = Enabled ? "1" : "0";
        }
    }
}
