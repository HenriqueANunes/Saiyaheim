using System;
using HarmonyLib;
using UnityEngine;
using Valheim.UI;

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

        /// <summary>Valor do jogo em 1.0.7, usado se a reflexão falhar após uma atualização.</summary>
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
        /// <c>RadialMenuElement.Name</c> e <c>.SubTitle</c> têm setter <c>protected</c>: o menu
        /// radial da vanilla espera que quem escreve o rótulo seja a subclasse do elemento
        /// (<c>EmoteElement</c>, <c>ItemElement</c>...). Os elementos do mod nascem do
        /// <c>EmptyElement</c>, que escreve "Empty" e nada mais, então o rótulo passa por aqui.
        ///
        /// É o único ponto do menu radial que pede reflexão. O resto — ícone, cor, e os quatro
        /// delegates de interação — é público. Ver [[Menu Radial]].
        ///
        /// Pelo setter da propriedade e não pelo campo de apoio (<c>&lt;Name&gt;k__BackingField</c>):
        /// o nome do campo é detalhe do compilador e muda sem aviso; a propriedade é a API.
        /// </summary>
        private static readonly Action<RadialMenuElement, string> ElementNameSetter =
            CreatePropertySetter<RadialMenuElement, string>("Name");

        private static readonly Action<RadialMenuElement, string> ElementSubTitleSetter =
            CreatePropertySetter<RadialMenuElement, string>("SubTitle");

        /// <summary>
        /// Escreve o rótulo do elemento. Falha silenciosa e anotada no log: elemento sem nome
        /// continua clicável, e derrubar o menu inteiro por causa de um rótulo seria pior.
        /// </summary>
        internal static void SetElementName(RadialMenuElement element, string name)
        {
            Invoke(ElementNameSetter, element, name, "RadialMenuElement.Name");
        }

        internal static void SetElementSubTitle(RadialMenuElement element, string subTitle)
        {
            Invoke(ElementSubTitleSetter, element, subTitle, "RadialMenuElement.SubTitle");
        }

        private static void Invoke(
            Action<RadialMenuElement, string> setter, RadialMenuElement element, string value, string what)
        {
            if (setter == null || element == null)
            {
                return;
            }

            try
            {
                setter(element, value);
            }
            catch (Exception ex)
            {
                SaiyaheimPlugin.Log.LogWarning($"Failed to write {what}: {ex.Message}");
            }
        }

        // Aqui moraram dois acessos a emote, removidos em 2026-08-07 junto com o emote de
        // carregamento: `Character.StopEmote()` (protected) e `Player.m_emoteState` (private).
        //
        // O segundo vale ser lembrado, porque é a solução de um problema que vai voltar: o
        // `Player.UpdateEmote` roda em **toda** cópia do jogador, em toda máquina, e copia para o
        // `m_emoteState` o nome que veio da ZDO — e só no ramo de emote **em loop**, nunca no de
        // disparo único. Ou seja, um `StartEmote(nome, oneshot: false)` é um flag booleano
        // sincronizado de graça, legível por qualquer cliente. Se um dia for preciso replicar um
        // estado sustentado sem escrever RPC nem status effect, o caminho é esse.

        /// <summary>
        /// Cria o acessor sem derrubar o mod se o campo sumir numa atualização do jogo —
        /// o chamador usa o fallback.
        /// </summary>
        /// <summary>
        /// Delegate aberto para o setter de uma propriedade com setter não-público. Mesmo contrato
        /// do <see cref="CreateFieldRef{TObject,TField}"/>: devolve null se a propriedade sumir
        /// numa atualização, e quem chama trata.
        /// </summary>
        private static Action<TObject, TValue> CreatePropertySetter<TObject, TValue>(string propertyName)
        {
            try
            {
                return AccessTools.MethodDelegate<Action<TObject, TValue>>(
                    AccessTools.PropertySetter(typeof(TObject), propertyName));
            }
            catch (Exception ex)
            {
                SaiyaheimPlugin.Log.LogWarning(
                    $"Property '{typeof(TObject).Name}.{propertyName}' has no reachable setter " +
                    $"({ex.GetType().Name}). The game may have been updated.");
                return null;
            }
        }

        /// <summary>
        /// <c>SEMan.m_character</c> é private. É o caminho do postfix de
        /// <c>ModifyHealthRegen</c> de volta para o dono dos status effects — o método não recebe
        /// o personagem por parâmetro, e sem ele o patch não sabe de quem é a cura que está
        /// passando.
        /// </summary>
        private static readonly AccessTools.FieldRef<SEMan, Character> SEManCharacterRef =
            CreateFieldRef<SEMan, Character>("m_character");

        internal static Character GetSEManCharacter(SEMan seman)
        {
            if (SEManCharacterRef == null || seman == null)
            {
                return null;
            }

            try
            {
                return SEManCharacterRef(seman);
            }
            catch (Exception ex)
            {
                SaiyaheimPlugin.Log.LogWarning($"Failed to read m_character: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// <c>Player.m_foodRegenTimer</c> é private. É o relógio da cura passiva da comida: o
        /// <c>Player.UpdateFood</c> soma <c>dt</c> nele e cura quando passa de 10 segundos.
        ///
        /// O intervalo de 10 segundos é uma constante dentro daquele método, então não há como
        /// encurtá-lo sem transpiler. Adiantar o relógio dá o mesmo resultado e cabe num postfix.
        /// Ver <c>Transformations.HealthRegenPatch</c>.
        /// </summary>
        private static readonly AccessTools.FieldRef<Player, float> FoodRegenTimerRef =
            CreateFieldRef<Player, float>("m_foodRegenTimer");

        /// <summary>
        /// Adianta o relógio da cura passiva em <paramref name="seconds"/>. Devolve false se o
        /// campo sumiu numa atualização do jogo — quem chama desliga o efeito em vez de insistir.
        /// </summary>
        internal static bool AdvanceFoodRegenTimer(Player player, float seconds)
        {
            if (FoodRegenTimerRef == null || player == null)
            {
                return false;
            }

            try
            {
                FoodRegenTimerRef(player) += seconds;
                return true;
            }
            catch (Exception ex)
            {
                SaiyaheimPlugin.Log.LogWarning($"Failed to write m_foodRegenTimer: {ex.Message}");
                return false;
            }
        }

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
