using HarmonyLib;

namespace Saiyaheim.Transformations
{
    /// <summary>
    /// Devolve a cor da forma ao cabelo depois que o jogo a apaga.
    ///
    /// <b>O bug (playtest de 2026-08-16).</b> Equipar ou desequipar qualquer coisa — picareta,
    /// martelo, elmo — devolvia o cabelo à cor original enquanto o jogador continuava
    /// transformado: o ki drenava, as teclas respondiam, só o visual mentia.
    ///
    /// <b>A causa</b> está no fim do <c>Player.SetupVisEquipment</c>:
    /// <code>visEq.SetHairColor(m_hairColor);</code>
    /// O <c>m_hairColor</c> do <c>Player</c> é a cor de verdade do personagem, que o
    /// <see cref="TransformationEffects.SetHairColor"/> deliberadamente <b>nunca</b> toca — é
    /// justamente ela que serve de fonte para restaurar. Só que esse método é chamado a cada
    /// mudança de equipamento (<c>Humanoid.SetupEquipment</c>, <c>UnequipItem</c>, <c>DropItem</c>),
    /// e ele escreve a cor original na mesma chave de ZDO que nós usamos. O
    /// <c>VisEquipment.UpdateColors</c> lê essa chave todo frame, então a tinta some no frame
    /// seguinte.
    ///
    /// <b>Por que é patch Harmony, contra a regra do projeto.</b> Não há caminho nativo: o jogo
    /// não expõe hook nenhum de mudança de equipamento, e o único jeito de não perder a corrida é
    /// escrever <i>depois</i> de quem apaga. Reaplicar do <c>Update</c> do plugin também
    /// funcionaria, mas custaria uma escrita por frame para consertar um evento raro, e ainda
    /// deixaria um frame de cabelo errado a cada troca.
    ///
    /// <b>Ragdoll fica de fora.</b> Naquele caminho o <c>visEq</c> recebido é o do cadáver, não o
    /// do jogador — reaplicar ali pintaria o <c>VisEquipment</c> errado, e o corpo caído com a
    /// aparência normal do personagem é o comportamento certo de qualquer forma.
    /// </summary>
    [HarmonyPatch(typeof(Player), "SetupVisEquipment")]
    internal static class HairColorPatch
    {
        private static void Postfix(Player __instance, bool isRagdoll)
        {
            if (isRagdoll)
            {
                return;
            }

            TransformationEffects.ReapplyHairColor(__instance);
        }
    }
}
