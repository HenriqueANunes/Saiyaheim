using HarmonyLib;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Transmission
{
    /// <summary>
    /// Clique no mapa grande com a tecla do Instant Transmission segurada teleporta (etapa 16). Ver
    /// <see cref="InstantTransmission"/>.
    ///
    /// <b>Por que patch.</b> O mapa não tem evento de clique para fora: o <c>UIInputHandler</c> da
    /// imagem do mapa chama métodos do próprio <c>Minimap</c>, e a conversão de tela para mundo é
    /// privada. O <c>OnMapLeftClick</c> é público e só roda em clique curto (arrastar o mapa não
    /// chega aqui), que é exatamente o gesto.
    ///
    /// <b>Prefix que pula o original</b> quando teleporta ou recusa: o clique era do Instant
    /// Transmission, e marcar o pin embaixo do cursor seria efeito colateral. Sem a tecla, ou sem a
    /// técnica aprendida, o clique segue para a vanilla intacto.
    /// </summary>
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.OnMapLeftClick))]
    internal static class InstantTransmissionMapPatch
    {
        private static bool Prefix(Minimap __instance)
        {
            Player player = Player.m_localPlayer;
            if (player == null ||
                !Hotkey.IsPressed(SaiyaheimConfig.TransmissionMapKey) ||
                !InstantTransmission.IsLearned(player))
            {
                return true;
            }

            if (!GameAccess.TryMapScreenToWorld(__instance, ZInput.pointerPosition, out Vector3 point))
            {
                return true;
            }

            InstantTransmission.TryTeleport(player, point);
            return false;
        }
    }
}
