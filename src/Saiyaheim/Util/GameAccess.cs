using System;
using HarmonyLib;

namespace Saiyaheim.Util
{
    /// <summary>
    /// Acesso a membros **privados** do jogo.
    ///
    /// A armadilha que motivou este arquivo: as assemblies publicizadas tornam tudo público em
    /// tempo de compilação, mas em runtime o jogo carrega a assembly real. Ler um campo privado
    /// direto compila sem reclamar e estoura <c>FieldAccessException</c> na tela do jogador.
    ///
    /// Regra do projeto: **campo público, acesso direto; campo privado, passa por aqui.**
    /// Conferir a real acessibilidade decompilando `assembly_valheim.dll` — a *não* publicizada.
    ///
    /// Os delegates do <c>AccessTools</c> são criados uma vez e cacheados; o custo por chamada
    /// é próximo de acesso direto.
    /// </summary>
    internal static class GameAccess
    {
        /// <summary>Padding lateral das barras da HUD. Privado em <c>Hud</c>.</summary>
        private static readonly AccessTools.FieldRef<Hud, float> StaminaBarBorderBufferRef =
            CreateFieldRef<Hud, float>("m_staminaBarBorderBuffer");

        /// <summary>Valor do jogo em 0.221.12, usado se a reflexão falhar após uma atualização.</summary>
        private const float StaminaBarBorderBufferFallback = 16f;

        internal static float GetStaminaBarBorderBuffer(Hud hud)
        {
            if (StaminaBarBorderBufferRef == null || hud == null)
            {
                return StaminaBarBorderBufferFallback;
            }

            try
            {
                return StaminaBarBorderBufferRef(hud);
            }
            catch (Exception ex)
            {
                SaiyaheimPlugin.Log.LogWarning($"Falha ao ler m_staminaBarBorderBuffer: {ex.Message}");
                return StaminaBarBorderBufferFallback;
            }
        }

        /// <summary>
        /// Cria o acessor sem derrubar o mod se o campo sumir numa atualização do jogo —
        /// o chamador usa o fallback.
        /// </summary>
        private static AccessTools.FieldRef<TObject, TField> CreateFieldRef<TObject, TField>(string fieldName)
        {
            try
            {
                return AccessTools.FieldRefAccess<TObject, TField>(fieldName);
            }
            catch (Exception ex)
            {
                SaiyaheimPlugin.Log.LogWarning(
                    $"Campo '{typeof(TObject).Name}.{fieldName}' não encontrado ({ex.GetType().Name}). " +
                    "O jogo pode ter atualizado.");
                return null;
            }
        }
    }
}
