using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace Saiyaheim.Transformations
{
    /// <summary>
    /// Cabelo de malha própria, vindo de um AssetBundle embutido na DLL.
    ///
    /// <b>Este arquivo é o teste do caminho de volta</b>: a malha sai do jogo (extraída dos
    /// bundles do Valheim), passa pelo Blender, volta pelo Unity num AssetBundle e é vestida
    /// aqui. Enquanto a malha embutida for a cópia não editada do <c>Hair6</c>, o resultado
    /// correto na tela é <b>indistinguível do cabelo vanilla</b> — e é exatamente isso que prova
    /// o caminho. Só depois de ver isso funcionar vale gastar horas esculpindo.
    ///
    /// <b>Por que clonar um cabelo do jogo em vez de montar um prefab do zero.</b> O clone já vem
    /// com o <c>ItemDrop</c> preenchido, com o filho <c>attach_skin</c> que o
    /// <c>VisEquipment.AttachItem</c> procura, com o material do jogo (que é quem responde ao
    /// <c>_SkinColor</c>, ou seja, à cor da forma) e com a lista <c>m_helmetHairSettings</c>. Essa
    /// última importa mais do que parece: o <c>VisEquipment.GetHairItem</c> devolve <b>0</b> —
    /// jogador careca — quando o elmo pede uma variante que o cabelo não declara. Prefab do zero
    /// deixaria o jogador sem cabelo ao vestir capuz. Trocar só a malha do clone herda tudo isso
    /// de graça.
    ///
    /// <b>Contrato com o Blender: não mudar a contagem de vértices.</b> A malha do jogo é
    /// <c>SkinnedMeshRenderer</c>, e os pesos de osso vêm da malha vanilla, copiados vértice a
    /// vértice. O formato <c>.obj</c> não carrega peso nenhum, então a única forma de a malha
    /// editada continuar acompanhando a cabeça é os dois arrays terem o mesmo tamanho. Mover
    /// vértices: pode. Extrudar, subdividir, apagar: quebra o pareamento, e o
    /// <see cref="SwapMesh"/> recusa a troca e deixa o cabelo vanilla no lugar.
    /// </summary>
    internal static class CustomHair
    {
        /// <summary>
        /// Nome do bundle embutido. O <c>LoadAssetBundleFromResources</c> casa por sufixo, então
        /// isto não precisa do namespace na frente.
        /// </summary>
        private const string BundleName = "saiyaheim_hair";

        /// <summary>Nome da malha dentro do bundle, sem extensão. Sai do nome do arquivo no
        /// projeto Unity — <c>Assets/HairMeshes/hair6.asset</c>.</summary>
        private const string MeshAsset = "hair6";

        /// <summary>Cabelo do jogo que serve de molde. É o comprido que o SSJ3 já usa.</summary>
        private const string SourceHair = "Hair6";

        /// <summary>
        /// Nome do item novo no <c>ObjectDB</c>. Sem <c>_</c> de propósito: o
        /// <c>saiya_form hair</c> descarta os nomes com underscore, porque no jogo eles são as
        /// variantes internas de elmo e não opções de barbeiro.
        /// </summary>
        internal const string HairName = "SaiyaHair6";

        private static AssetBundle _bundle;

        /// <summary>
        /// Pendura o registro no evento do Jotunn. O clone só pode ser feito quando os prefabs do
        /// jogo existem — antes disso não há <c>Hair6</c> para copiar.
        /// </summary>
        internal static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabsAvailable;
        }

        private static void OnVanillaPrefabsAvailable()
        {
            // O evento pode disparar mais de uma vez numa sessão (voltar ao menu e entrar de novo).
            if (PrefabManager.Instance.GetPrefab(HairName) != null)
            {
                return;
            }

            Mesh mesh = LoadMesh();
            if (mesh == null)
            {
                return;
            }

            CustomItem item = new CustomItem(HairName, SourceHair);
            if (item.ItemPrefab == null)
            {
                SaiyaheimPlugin.Log.LogError(
                    $"Could not clone '{SourceHair}'. Custom hair is off this session.");
                return;
            }

            if (!SwapMesh(item.ItemPrefab, mesh))
            {
                return;
            }

            ItemManager.Instance.AddItem(item);
            SaiyaheimPlugin.Log.LogInfo($"Custom hair '{HairName}' registered.");
        }

        /// <summary>
        /// A malha do bundle.
        ///
        /// ⚠️ <b>Nada de <c>LoadAllAssets</c> aqui.</b> A primeira versão usava, e o Valheim
        /// morria com <c>Caught fatal signal</c> dentro de
        /// <c>LoadAssetWithSubAssets_Internal</c>, ainda na tela de carregamento — crash nativo,
        /// sem exceção gerenciada para pegar. O bundle daquela vez trazia junto o material e o
        /// shader que o import do <c>.obj</c> gera, e carregar shader compilado fora do jogo
        /// derruba o processo.
        ///
        /// O <c>BundleBuilder</c> do projeto Unity hoje empacota <b>só malhas</b>, e aqui se
        /// carrega um asset por nome. As duas pontas precisam continuar assim: bundle sem
        /// material nem shader, e carga nomeada em vez de carga cega.
        /// </summary>
        private static Mesh LoadMesh()
        {
            if (_bundle == null)
            {
                _bundle = AssetUtils.LoadAssetBundleFromResources(BundleName);
            }

            if (_bundle == null)
            {
                SaiyaheimPlugin.Log.LogError($"Asset bundle '{BundleName}' not found in the DLL.");
                return null;
            }

            string assetName = _bundle.GetAllAssetNames()
                                      .FirstOrDefault(n => n.EndsWith($"/{MeshAsset}.asset"));
            if (assetName == null)
            {
                SaiyaheimPlugin.Log.LogError(
                    $"Asset bundle '{BundleName}' has no mesh named '{MeshAsset}'. It holds: " +
                    string.Join(", ", _bundle.GetAllAssetNames()));
                return null;
            }

            Mesh mesh = _bundle.LoadAsset<Mesh>(assetName);
            if (mesh == null)
            {
                SaiyaheimPlugin.Log.LogError($"'{assetName}' did not load as a mesh.");
            }

            return mesh;
        }

        /// <summary>
        /// Troca a malha do clone pela nossa.
        ///
        /// ⚠️ <b>Os pesos de osso e as bindposes já vêm dentro da malha, e é obrigatório que
        /// venham.</b> Duas tentativas anteriores morreram exatamente aqui, e as duas com sintoma
        /// visível no jogo:
        ///
        /// 1. Copiar <c>boneWeights</c> da malha vanilla em tempo de execução devolveu array
        ///    vazio — as malhas do jogo guardam o peso empacotado no stream de vértices, não no
        ///    array que o <c>Mesh.boneWeights</c> expõe. Resultado: cabelo sem esqueleto, que o
        ///    Unity desenha no espaço do pai. Andava com o corpo e não virava com a cabeça.
        /// 2. Montar a malha na mão a partir dos dados crus do bundle do jogo acertou vértice,
        ///    peso e índice de osso, mas montou a <b>bindpose transposta</b>. Resultado: cabelo
        ///    distorcido.
        ///
        /// Quem resolve é o <b>AssetRipper</b>, que extrai a malha do jogo já como asset do Unity,
        /// com peso e bindpose no lugar, sem parser de formato binário no caminho. O
        /// <c>BundleBuilder</c> do projeto Unity só copia esse asset para dentro do bundle. Aqui
        /// se confere e se troca.
        ///
        /// A conferência de contagem de vértices continua valendo: ela pega o caso de a malha do
        /// bundle ter sido esculpida com vértice a mais ou a menos, que embaralharia o pareamento
        /// com os pesos.
        /// </summary>
        private static bool SwapMesh(GameObject prefab, Mesh mesh)
        {
            SkinnedMeshRenderer renderer = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                                                 .FirstOrDefault();
            if (renderer == null || renderer.sharedMesh == null)
            {
                SaiyaheimPlugin.Log.LogError(
                    $"'{SourceHair}' has no skinned mesh renderer. The game's prefab layout changed.");
                return false;
            }

            if (mesh.bindposes.Length == 0 || mesh.boneWeights.Length != mesh.vertexCount)
            {
                SaiyaheimPlugin.Log.LogError(
                    $"Mesh '{mesh.name}' carries no skinning ({mesh.boneWeights.Length} weights, " +
                    $"{mesh.bindposes.Length} bindposes for {mesh.vertexCount} vertices). It would " +
                    "hang off the body instead of the head. Rebuild the bundle from the mesh that " +
                    "AssetRipper extracts.");
                return false;
            }

            renderer.sharedMesh = mesh;
            SaiyaheimPlugin.LogVerbose(
                $"Hair mesh '{mesh.name}': {mesh.vertexCount} vertices, " +
                $"{mesh.bindposes.Length} bindposes.");

            return true;
        }
    }
}
