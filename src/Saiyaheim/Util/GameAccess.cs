using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

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
                SaiyaheimPlugin.Log.LogWarning($"Failed to read m_staminaBarBorderBuffer: {ex.Message}");
                return StaminaBarBorderBufferFallback;
            }
        }

        /// <summary><c>Character.m_animator</c> é protected. Usado para listar os emotes disponíveis.</summary>
        private static readonly AccessTools.FieldRef<Character, Animator> AnimatorRef =
            CreateFieldRef<Character, Animator>("m_animator");

        internal static Animator GetAnimator(Character character)
        {
            if (AnimatorRef == null || character == null)
            {
                return null;
            }

            try
            {
                return AnimatorRef(character);
            }
            catch (Exception ex)
            {
                SaiyaheimPlugin.Log.LogWarning($"Failed to read m_animator: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// <c>Character.m_run</c> é protected. <c>IsRunning()</c> não serve como substituto:
        /// ele lê <c>m_running</c>, que só é escrito dentro do <c>UpdateWalking</c> — voando esse
        /// caminho nunca roda e o valor fica preso em false. O <c>UpdateFlying</c> vanilla lê
        /// justamente <c>m_run</c> para escolher entre velocidade lenta e rápida.
        /// </summary>
        private static readonly AccessTools.FieldRef<Character, bool> RunRef =
            CreateFieldRef<Character, bool>("m_run");

        internal static bool IsRunPressed(Character character)
        {
            if (RunRef == null || character == null)
            {
                return false;
            }

            try
            {
                return RunRef(character);
            }
            catch (Exception ex)
            {
                SaiyaheimPlugin.Log.LogWarning($"Failed to read m_run: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// <c>CharacterAnimEvent.m_character</c> é private. É o caminho do postfix de
        /// <c>CustomLateUpdate</c> de volta para o dono do esqueleto.
        ///
        /// <c>GetComponentInParent&lt;Character&gt;()</c> resolveria sem reflexão, mas roda em todo
        /// personagem carregado a cada frame — o delegate cacheado é mais barato que a busca na
        /// hierarquia.
        /// </summary>
        private static readonly AccessTools.FieldRef<CharacterAnimEvent, Character> AnimEventCharacterRef =
            CreateFieldRef<CharacterAnimEvent, Character>("m_character");

        internal static Character GetAnimEventCharacter(CharacterAnimEvent animEvent)
        {
            if (AnimEventCharacterRef == null || animEvent == null)
            {
                return null;
            }

            try
            {
                return AnimEventCharacterRef(animEvent);
            }
            catch (Exception ex)
            {
                SaiyaheimPlugin.Log.LogWarning($"Failed to read m_character: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// <c>Character.StopEmote()</c> é protected. Poderia ser substituído por
        /// <c>StartEmote("")</c>, mas esse caminho passa antes por checagens de
        /// <c>CanMove()</c>/<c>InAttack()</c> e falharia justamente quando mais precisamos parar.
        /// </summary>
        private static readonly MethodInfo StopEmoteMethod =
            AccessTools.Method(typeof(Character), "StopEmote");

        internal static void StopEmote(Player player)
        {
            if (StopEmoteMethod == null || player == null)
            {
                return;
            }

            try
            {
                StopEmoteMethod.Invoke(player, null);
            }
            catch (Exception ex)
            {
                SaiyaheimPlugin.Log.LogWarning($"Failed to stop the emote: {ex.Message}");
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
                    $"Field '{typeof(TObject).Name}.{fieldName}' not found ({ex.GetType().Name}). " +
                    "The game may have been updated.");
                return null;
            }
        }
    }
}
