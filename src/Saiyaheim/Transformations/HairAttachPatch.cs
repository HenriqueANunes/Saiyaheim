using HarmonyLib;
using UnityEngine;

namespace Saiyaheim.Transformations
{
    /// <summary>
    /// Mantém a nossa malha no <c>SkinnedMeshRenderer</c> de um cabelo depois que o jogo o prende
    /// na cabeça.
    ///
    /// <b>O bug (2026-09-11).</b> O <c>SaiyaHair1</c> aparecia com o cabelo vanilla, e o rabo do
    /// <c>SaiyaHair2</c> também — o do <c>SaiyaHair2</c> passou despercebido porque a cúpula
    /// espetada domina a cabeça. As duas peças são as únicas com <c>SkinnedMeshRenderer</c> e
    /// <c>MeshFilter</c> no mesmo objeto (cabelo rígido, <c>m_Bones: []</c>). O
    /// <c>saiya_form hair inspect</c> mostrou a malha certa no prefab do <c>ObjectDB</c> e, na
    /// instância presa na cabeça, o <c>MeshFilter</c> ainda com a nossa, mas o renderer com a
    /// vanilla: primeiro o próprio asset (<c>Hair_01</c>, 66 vértices) um frame depois do
    /// <c>AttachItem</c>, e mais tarde uma cópia dele criada em tempo de execução
    /// (<c>Hair_01(Clone)</c>, id negativo). A malha é escolhida pelo nome do objeto — o rabo do
    /// <c>Hair2</c> recebe a <c>pCylinder623</c> vanilla. Quem faz a troca não foi achado: não
    /// está no código do Valheim, nem no Jotunn, nem nos outros mods do perfil.
    ///
    /// Corrigir uma vez não bastou: com a correção no frame seguinte ao <c>AttachItem</c>, o
    /// cabelo certo aparecia por um ou dois frames e voltava ao vanilla. Por isso o
    /// <see cref="HairMeshKeeper"/> fica na instância e confere a cada frame.
    ///
    /// <b>Por que é patch Harmony, contra a regra do projeto.</b> O prefab já está certo, e a
    /// troca acontece na instância, que o jogo cria dentro do <c>VisEquipment.AttachItem</c>
    /// privado. Não há outro momento em que a instância exista e ainda não tenha sido desenhada.
    ///
    /// Só mexe em renderer cujo <c>MeshFilter</c> traz malha do nosso bundle
    /// (<see cref="CustomHair.IsCustomMesh"/>): qualquer outra peça do jogo passa intocada.
    /// </summary>
    [HarmonyPatch(typeof(VisEquipment), "AttachItem")]
    internal static class HairAttachPatch
    {
        private static void Postfix(GameObject __result)
        {
            if (__result == null)
            {
                return;
            }

            foreach (SkinnedMeshRenderer skinned in __result.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                MeshFilter filter = skinned.GetComponent<MeshFilter>();
                if (filter == null || !CustomHair.IsCustomMesh(filter.sharedMesh))
                {
                    continue;
                }

                skinned.gameObject.AddComponent<HairMeshKeeper>().Keep(skinned, filter.sharedMesh);
            }
        }
    }

    /// <summary>
    /// Devolve a malha ao renderer sempre que ela for trocada. Ver <see cref="HairAttachPatch"/>.
    ///
    /// Roda por último no frame (<c>DefaultExecutionOrder</c> alto), para ganhar de quem troca no
    /// mesmo <c>LateUpdate</c>. O custo é uma comparação de referência por frame, só nos nossos
    /// cabelos rígidos.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    internal sealed class HairMeshKeeper : MonoBehaviour
    {
        // Quantas correções entram no log: o bastante para ver se a troca é uma vez só ou todo
        // frame, sem encher o log de um jogador que fique horas transformado.
        private const int LoggedRestores = 5;

        private SkinnedMeshRenderer _renderer;
        private Mesh _mesh;
        private int _restores;

        internal void Keep(SkinnedMeshRenderer target, Mesh mesh)
        {
            _renderer = target;
            _mesh = mesh;
            Restore();
        }

        private void LateUpdate()
        {
            Restore();
        }

        private void Restore()
        {
            if (_renderer == null || _renderer.sharedMesh == _mesh)
            {
                return;
            }

            Mesh replaced = _renderer.sharedMesh;
            _renderer.sharedMesh = _mesh;

            _restores++;
            if (_restores <= LoggedRestores)
            {
                SaiyaheimPlugin.LogVerbose(
                    $"Hair mesh restored on '{_renderer.name}' at frame {Time.frameCount}: " +
                    $"{(replaced == null ? "none" : replaced.name + " " + replaced.vertexCount)} -> " +
                    $"{_mesh.name} {_mesh.vertexCount} (#{_restores}).");
            }
        }
    }
}
