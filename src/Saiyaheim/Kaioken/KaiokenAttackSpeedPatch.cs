using HarmonyLib;
using UnityEngine;

namespace Saiyaheim.Kaioken
{
    /// <summary>
    /// Velocidade de ataque do Kaioken (etapa 14): acelera a animação do golpe corpo a corpo.
    ///
    /// <b>Por que patch Harmony.</b> O jogo não tem stat de velocidade de ataque — o <c>SE_Stats</c>
    /// só mexe no dano e na stamina do golpe. O ataque vanilla anda pela animação: o dano sai no
    /// evento <c>OnAttackTrigger</c>, a janela de combo no <c>Chain()</c> e o fim do ataque na tag
    /// <c>attack</c> do animator. Acelerar o <c>Animator.speed</c> acelera os três juntos.
    ///
    /// <b>Multiplayer sem RPC.</b> O <c>ZSyncAnimation</c> do dono publica <c>m_animator.speed</c>
    /// no ZDO, e os outros clientes aplicam. Por isso tudo aqui vale só para o jogador local.
    ///
    /// <b>Quem mais escreve no mesmo valor</b> (conferido na assembly real, <c>l-1.0.12</c>):
    /// <list type="bullet">
    /// <item><c>CharacterAnimEvent.CustomFixedUpdate</c> volta para 1 fora do ataque. É o que
    /// desliga isto: nada vaza para correr, nadar ou emote.</item>
    /// <item><c>CharacterAnimEvent.Speed</c>, evento de alguns clips, grava velocidade
    /// <b>absoluta</b> no meio do golpe e apagaria o multiplicador. O segundo patch reaplica.</item>
    /// <item><c>CharacterAnimEvent.FreezeFrame</c> (hit-stop) guarda e devolve a velocidade atual:
    /// preserva o multiplicador sozinho.</item>
    /// <item><c>Character.RPC_Stagger</c> força 1. Correto: atordoado não ataca.</item>
    /// </list>
    ///
    /// Fica de fora: arco, besta e cajado (o tempo de puxar é por relógio e skill, não pela
    /// animação), e os ataques de ki do mod, que têm timing próprio. Ver [[Kaioken]].
    /// </summary>
    internal static class KaiokenAttackSpeedPatch
    {
        /// <summary>
        /// Multiplicador do ataque em curso do jogador local. 1 quando o golpe não é corpo a corpo
        /// ou começou sem Kaioken. Gravado no início do golpe para o reaplicar valer o mesmo número,
        /// mesmo se o tier mudar no meio do golpe.
        /// </summary>
        private static float _currentMultiplier = 1f;

        /// <summary>
        /// Corpo a corpo, inclusive o soco e o ataque em área (marreta, secundário da alabarda).
        /// Projétil cobre arco, besta, cajado e lança arremessada.
        /// </summary>
        private static bool IsMelee(Attack attack)
        {
            return attack.m_attackType == Attack.AttackType.Horizontal ||
                   attack.m_attackType == Attack.AttackType.Vertical ||
                   attack.m_attackType == Attack.AttackType.Area;
        }

        [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
        private static class StartPatch
        {
            private static void Postfix(Attack __instance, bool __result, Humanoid character, ZSyncAnimation zanim)
            {
                if (!__result || zanim == null || character == null || character != Player.m_localPlayer)
                {
                    return;
                }

                KaiokenTier tier = IsMelee(__instance) ? KaiokenRegistry.GetActive(Player.m_localPlayer) : null;
                _currentMultiplier = tier == null ? 1f : tier.GetAttackSpeedMultiplier();

                if (_currentMultiplier > 1f)
                {
                    zanim.SetSpeed(_currentMultiplier);
                }
            }
        }

        // m_character e m_animator são privados na assembly real; a injeção ___campo do Harmony
        // resolve sem passar pelo GameAccess.
        [HarmonyPatch(typeof(CharacterAnimEvent), nameof(CharacterAnimEvent.Speed))]
        private static class SpeedEventPatch
        {
            private static void Postfix(Character ___m_character, Animator ___m_animator)
            {
                if (_currentMultiplier <= 1f || ___m_animator == null ||
                    ___m_character == null || ___m_character != Player.m_localPlayer ||
                    !___m_character.InAttack())
                {
                    return;
                }

                ___m_animator.speed *= _currentMultiplier;
            }
        }
    }
}
