using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Saiyaheim.Transformations
{
    /// <summary>
    /// Cabelo de malha própria, vindo de um AssetBundle embutido na DLL.
    ///
    /// <b>A malha dá uma volta inteira fora deste repositório</b>: sai do jogo (extraída dos
    /// bundles do Valheim), é esculpida no projeto Unity vizinho —
    /// <c>~/Documents/projetos/Saiyaheim-Unity/</c>, com os espetos gerados por script — um por
    /// penteado, <c>tools/spike_build.py</c> para o <c>Hair6</c>, <c>tools/hair1_spikes.py</c>
    /// para o <c>Hair1</c>, <c>tools/hair2_spikes.py</c> para o <c>Hair2</c> e
    /// <c>tools/hair3_spikes.py</c> para o <c>Hair3</c> — volta num
    /// AssetBundle e é vestida aqui. O bundle viaja embutido na DLL, então o deploy continua
    /// sendo um arquivo só.
    ///
    /// <b>Por que clonar um cabelo do jogo em vez de montar um prefab do zero.</b> O clone já vem
    /// com o <c>ItemDrop</c> preenchido, com o filho de encaixe que o
    /// <c>VisEquipment.AttachItem</c> procura (<c>attach_skin</c> nos cabelos com esqueleto,
    /// <c>attach</c> nos rígidos), com o material do jogo (que é quem responde ao
    /// <c>_SkinColor</c>, ou seja, à cor da forma) e com a lista <c>m_helmetHairSettings</c>. Essa
    /// última importa mais do que parece: o <c>VisEquipment.GetHairItem</c> devolve <b>0</b> —
    /// jogador careca — quando o elmo pede uma variante que o cabelo não declara. Prefab do zero
    /// deixaria o jogador sem cabelo ao vestir capuz. Trocar só a malha do clone herda tudo isso
    /// de graça.
    ///
    /// <b>Vértice novo é permitido, e é assim que espeto entra.</b> A malha do jogo é
    /// <c>SkinnedMeshRenderer</c>, e o formato <c>.obj</c> não carrega peso de osso nenhum. Quem
    /// resolve isso é um arquivo <c>.parents</c> ao lado do <c>.obj</c>, com um índice por
    /// vértice dizendo de qual vértice <b>original</b> ele nasceu; o <c>BundleBuilder</c> do
    /// projeto Unity herda peso e UV do pai. Espeto que nasce de uma mecha se mexe com aquela
    /// mecha, que é o comportamento certo.
    /// </summary>
    internal static class CustomHair
    {
        /// <summary>
        /// Nome do bundle embutido. O recurso é casado por <b>sufixo</b>, então isto não precisa
        /// do namespace na frente.
        /// </summary>
        private const string BundleName = "saiyaheim_hair";

        /// <summary>
        /// Uma malha nossa e onde ela entra no clone.
        ///
        /// <c>Child</c> nulo quer dizer "o primeiro renderer com malha que existir", que é o
        /// caso dos penteados de uma peça só. Quando o cabelo do jogo é feito de várias peças,
        /// cada uma é nomeada, porque aí a ordem em que os filhos aparecem não é garantia de
        /// nada.
        /// </summary>
        private sealed class HairPart
        {
            internal HairPart(string child, string mesh)
            {
                Child = child;
                Mesh = mesh;
            }

            /// <summary>Nome do filho do prefab que carrega o renderer, ou nulo.</summary>
            internal string Child { get; }

            /// <summary>Nome da malha dentro do bundle, sem extensão. Sai do nome do arquivo no
            /// projeto Unity — <c>Assets/HairMeshes/&lt;nome&gt;.asset</c>.</summary>
            internal string Mesh { get; }
        }

        /// <summary>
        /// Um penteado espetado: o nome do item novo, o cabelo do jogo que serve de molde e as
        /// malhas que substituem as dele.
        /// </summary>
        private sealed class HairEntry
        {
            internal HairEntry(string name, string source, params HairPart[] parts)
            {
                Name = name;
                Source = source;
                Parts = parts;
            }

            /// <summary>
            /// Nome do item novo no <c>ObjectDB</c>. Sem <c>_</c> de propósito: o
            /// <c>saiya_form hair</c> descarta os nomes com underscore, porque no jogo eles são
            /// as variantes internas de elmo e não opções de barbeiro.
            /// </summary>
            internal string Name { get; }

            /// <summary>Cabelo do jogo que serve de molde.</summary>
            internal string Source { get; }

            /// <summary>As peças a trocar. Uma na maioria dos penteados, três no Hair2.</summary>
            internal HairPart[] Parts { get; }
        }

        /// <summary>
        /// Os penteados espetados que o mod registra. Um por penteado do jogo; qual forma usa
        /// qual continua sendo a chave <c>HairItem</c> de cada forma, no <c>.cfg</c>.
        ///
        /// ⚠️ <b>O nome do molde e o nome da malha não são o mesmo.</b> O prefab do jogo se chama
        /// <c>Hair1</c>, mas a malha dentro dele é <c>Hair_01</c>. Só o <c>Hair6</c> tem os dois
        /// iguais, e escrever o código como se isso valesse sempre não sobrevive ao segundo
        /// cabelo.
        ///
        /// ⚠️ <b>E nem todo penteado é uma malha só.</b> O <c>Hair2</c> são três: duas metades de
        /// casquete (<c>pCube225</c> e <c>pCube226</c>, malhas iguais, uma delas com
        /// <c>scale.x</c> negativo) e um rabo de cavalo (<c>pCylinder623</c>). As duas metades
        /// recebem a <b>mesma</b> malha nossa — quem espelha é o transform do jogo, então o
        /// casquete é esculpido uma vez só.
        /// </summary>
        private static readonly HairEntry[] Hairs =
        {
            // O comprido que o SSJ3 usa. Malha com esqueleto: filho `attach_skin` e armature.
            new HairEntry("SaiyaHair6", "Hair6", new HairPart(null, "hair6")),
            // Casquete com rabo de cavalo. Malha RÍGIDA: filho `attach`, `m_Bones: []`.
            new HairEntry("SaiyaHair1", "Hair1", new HairPart(null, "Hair_01")),
            // Três peças: casquete espelhado em dois `MeshRenderer` e o rabo num
            // `SkinnedMeshRenderer` sem ossos, com um `Cloth` que o jogo mantém desligado.
            new HairEntry("SaiyaHair2", "Hair2",
                          new HairPart("pCube225", "hair2_cap"),
                          new HairPart("pCube226", "hair2_cap"),
                          new HairPart("pCylinder623", "hair2_tail")),
            // Casquete curto com duas mechas longas na frente das orelhas. Malha com
            // esqueleto (filho `attach_skin`), e uma peça só — mas 12 cascas soltas dentro
            // dela, espelhadas em X.
            new HairEntry("SaiyaHair3", "Hair3", new HairPart(null, "hair3")),
            // Casquete raso no alto do crânio com um rabo de cavalo longo pela nuca. Malha
            // com esqueleto (filho `attach_skin`), uma peça só, 8 cascas espelhadas em X.
            // A malha vem deitada: o bindpose dela é uma rotação de -90° em X, então no
            // arquivo +Z é cima e -Y é o rosto. Isso é problema do gerador de espeto, não
            // daqui — o jogo desfaz pelo bindpose, como faz com a vanilla.
            new HairEntry("SaiyaHair4", "Hair4", new HairPart(null, "hair4")),
            // Casquete curto e raso, sem rabo, colado à cabeça do alto até a nuca. Malha com
            // esqueleto (filho `attach_skin`), uma peça só, 2 cascas espelhadas em X. Também
            // deitada, mesmo bindpose do Hair4 (+Z cima, -Y rosto).
            new HairEntry("SaiyaHair5", "Hair5", new HairPart(null, "hair5")),
            // Capuz inteiro: cobre o crânio e desce em duas abas largas até os ombros, com
            // fenda em V na frente, mais um par de "chamas" atrás da cabeça que já nasce
            // pontudo na vanilla. Malha com esqueleto (filho `attach_skin`), uma peça só,
            // deitada como o Hair4. A malha do jogo se chama `hair7.001`; o nome foi
            // encurtado para `hair7` ao entrar no bundle.
            new HairEntry("SaiyaHair7", "Hair7", new HairPart(null, "hair7")),
            // Casquete pequeno com um "flick" já esculpido pendendo para um lado e uma mecha
            // solta caindo pela bochecha do lado oposto. Malha com esqueleto (filho
            // `attach_skin`), uma peça só, 2 cascas — e assimétrica de propósito: quase
            // nenhum vértice tem gêmeo espelhado. Deitada como o Hair4.
            new HairEntry("SaiyaHair8", "Hair8", new HairPart(null, "hair8")),
            // Uma mecha só, em onda: nasce fina no crânio, incha no meio e se enrola para a
            // frente. Malha com esqueleto (filho `attach_skin`), uma peça só; assimétrica,
            // sem nenhum vértice em X = 0. Deitada como o Hair4.
            new HairEntry("SaiyaHair9", "Hair9", new HairPart(null, "hair9")),
            // Casquete curto com uma única mecha longa nascendo do centro do topo e caindo
            // para a frente, passando do queixo. Malha com esqueleto (filho `attach_skin`),
            // uma peça só, 2 cascas — assimétrica de propósito (mecha cai só de um lado, sem
            // gêmeo). Deitada como o Hair4.
            new HairEntry("SaiyaHair10", "Hair10", new HairPart(null, "hair10")),
            // Casquete redondo e facetado com uma trança longa "em contas" descendo pela nuca.
            // Malha com esqueleto (filho `attach_skin`), uma peça só, 14 cascas espelhadas em
            // X. Deitada como o Hair4.
            new HairEntry("SaiyaHair11", "Hair11", new HairPart(null, "hair11")),
            // Capuz inteiro, sem rabo nem mecha, com um aro decorativo em volta da abertura do
            // rosto (172 quadrados soltos, intacto). Malha com esqueleto (filho `attach_skin`),
            // uma peça só, casquete espelhado em X. Deitada como o Hair4.
            new HairEntry("SaiyaHair12", "Hair12", new HairPart(null, "hair12")),
            // Casquete curto repartido ao meio, com duas tranças grossas "em contas" descendo
            // até o peito. Malha com esqueleto (filho `attach_skin`), uma peça só, 6 cascas
            // espelhadas em X. Bindpose **identidade** (não deitada, ao contrário do Hair4) —
            // casquete e raiz da trança são a mesma casca, separados por corte de altura
            // dentro dela, não por casca.
            new HairEntry("SaiyaHair13", "Hair13", new HairPart(null, "hair13")),
            // A menor malha do projeto: uma mecha única em gancho, sem casquete, nascendo perto
            // do topo do crânio e curvando para um lado. Malha com esqueleto (filho
            // `attach_skin`), uma peça só, assimétrica (sem par espelhado). Deitada como o
            // Hair4.
            new HairEntry("SaiyaHair14", "Hair14", new HairPart(null, "hair14")),
            // Cúpula fechada cobrindo o crânio inteiro, sem rabo nem mecha — mais parecido com
            // um capacete curto. Malha com esqueleto (filho `attach_skin`), uma peça só,
            // espelhada em X, com franja na frente que fica intacta. Deitada como o Hair4.
            new HairEntry("SaiyaHair15", "Hair15", new HairPart(null, "hair15")),
            // Rabo torcido em corda, curvo, sem casquete separado — a corda é a malha inteira,
            // sem simetria bilateral (um único cordão central, não duas metades espelhadas).
            // Malha com esqueleto (filho `attach_skin`), uma peça só. Deitada como o Hair4.
            new HairEntry("SaiyaHair16", "Hair16", new HairPart(null, "hair16")),
            // Trança única, sem casquete separado, com desfiado solto na ponta (intocado).
            // Espeto ao longo de quase toda a trança, não só no topo. Malha com esqueleto
            // (filho `attach_skin`), uma peça só. Deitada como o Hair4.
            new HairEntry("SaiyaHair17", "Hair17", new HairPart(null, "hair17")),
            // Crânio com cauda longa, coroa de espetos concentrada no topo, cauda mantida lisa.
            // Malha com esqueleto (filho `attach_skin`), uma peça só, espelhada em X, bindpose
            // identidade (não deitada). Variante "crina" (espeto por toda a cauda) descartada
            // aqui, marcada como candidata a um SSJ2 futuro.
            new HairEntry("SaiyaHair18", "Hair18", new HairPart(null, "hair18")),
            // Touca fechada cobrindo o crânio todo, sem franja baixa. Cobertura total de
            // espetos, intensidade média. Malha com esqueleto (filho `attach_skin`), uma peça
            // só, espelhada em X. Deitada como o Hair4.
            new HairEntry("SaiyaHair19", "Hair19", new HairPart(null, "hair19")),
            // Touca redonda tipo afro, sem rabo nem mecha, franja apagada pelos espetos. Malha
            // com esqueleto (filho `attach_skin`), uma peça só, espelhada em X. Deitada como o
            // Hair4.
            new HairEntry("SaiyaHair20", "Hair20", new HairPart(null, "hair20")),
            // Corte curto: calota lisa no alto e dois coques baixos bem laterais (tipo
            // maria-chiquinha) na altura da orelha, mais painéis finos perto da nuca. Espeto só
            // na coroa (calota + nuca); coques ficam lisos, redondos, por escolha de silhueta.
            // Malha com esqueleto (filho `attach_skin`), uma peça só, 9 cascas espelhadas em X.
            // Deitada como o Hair4. ⚠️ No jogo, o guid da malha do prefab `Hair21` aponta pro
            // arquivo `Hair22.asset` e vice-versa (nomes trocados); a malha certa foi escolhida
            // pelo guid, não pelo nome do arquivo.
            new HairEntry("SaiyaHair21", "Hair21", new HairPart(null, "hair21")),
            // Bola de mechas soltas cobrindo a cabeça inteira até a nuca — 7 mechas facetadas,
            // cada uma já nascendo inteira de um lado, sem casquete contínuo. Espeto em todas as
            // mechas, nuca incluída (a alternativa "só topo" foi descartada). Malha com esqueleto
            // (filho `attach_skin`), uma peça só, 14 cascas espelhadas em X. Deitada como o
            // Hair4 (mesma troca de guid do Hair21/Hair22 no jogo, aqui resolvida do mesmo jeito).
            new HairEntry("SaiyaHair22", "Hair22", new HairPart(null, "hair22")),
            // A menor malha do projeto (46 vértices): toco curto e arredondado sobre o
            // topo/nuca, sem franja nem casquete de verdade. Espeto em dois grupos: coroa no
            // topo (4 por lado) e um segundo grupo na nuca (5 por lado, quase na vertical,
            // cobrindo da base das abas até o meio de trás da cabeça) — pedido explícito do
            // Henrique depois de ver o topo sozinho. Malha com esqueleto (filho `attach_skin`),
            // uma peça só, 2 cascas espelhadas em X. Deitada como o Hair4.
            new HairEntry("SaiyaHair23", "Hair23", new HairPart(null, "hair23")),
            // Trança única e fina, sem casquete, curvando quase 180° do topo do crânio até uma
            // pontinha ornamental (mantida intacta). Espeto ao longo da trança inteira, seguindo
            // a curva por um fluxo local (não uma reta raiz→ponta, que apontaria pro lado errado
            // na metade de baixo). Malha com esqueleto (filho `attach_skin`), uma peça só,
            // bindpose identidade (não deitada, como o Hair13/Hair18).
            new HairEntry("SaiyaHair24", "Hair24", new HairPart(null, "hair24")),
            // Casquete com um bico frontal tipo topete e um coque redondo colado atrás, colado à
            // cabeça. Espeto no casquete inteiro e no coque também (a opção de coque liso foi
            // descartada). Malha com esqueleto (filho `attach_skin`), uma peça só, 6 cascas
            // espelhadas em X. Deitada como o Hair4.
            new HairEntry("SaiyaHair25", "Hair25", new HairPart(null, "hair25")),
            // Casquete colado com trança curta de cada lado, cada trança terminando numa conta
            // em forma de laço (mantida intacta). Espeto no casquete e ao longo da trança
            // inteira, bem denso — o Henrique pediu mais espeto depois de ver a primeira leva
            // (trança rala) e a versão final cobre quase toda a trança, não só a raiz. Malha com
            // esqueleto (filho `attach_skin`), uma peça só, 6 cascas espelhadas em X. Bindpose
            // identidade (não deitada, como o Hair13/Hair18).
            new HairEntry("SaiyaHair27", "Hair27", new HairPart(null, "hair27")),
            // Coque/bola no topo com rabo fino descendo em espiral (ondulação vinda da malha
            // original, não é espeto) até uma ponta afinada. Espeto no coque (intensidade
            // média) e no rabo inteiro — a opção de rabo liso foi descartada. Malha com
            // esqueleto (filho `attach_skin`), uma peça só, sem espelho (rabo cai pra um lado
            // só, mesh assimétrica de propósito). Bindpose identidade (como o Hair6).
            new HairEntry("SaiyaHair28", "Hair28", new HairPart(null, "hair28")),
            // Corte tipo chanel/pageboy: cúpula curta sobre o crânio com aba sólida descendo até
            // a nuca. Espeto na cúpula e também na aba de trás até o pescoço — a opção de aba
            // lisa foi descartada. Malha com esqueleto (filho `attach_skin`), uma peça só, 2
            // cascas espelhadas em X. Deitada como o Hair4.
            new HairEntry("SaiyaHair29", "Hair29", new HairPart(null, "hair29")),
            // Casquete arredondado tipo capacete, sem rabo, com franja (apagada, nasce sobre o
            // rosto) e dois tufos de milímetro perto da nuca/orelha (intactos). Espeto cobrindo
            // o casquete até a nuca, bem vertical — a primeira leva deixava a nuca lisa e os
            // espetos de lá saindo pro lado, corrigido com `cap_lift` bem mais alto e mais
            // espeto na área liberada. Malha com esqueleto (filho `attach_skin`), uma peça só, 5
            // cascas espelhadas em X. Deitada como o Hair4.
            new HairEntry("SaiyaHair30", "Hair30", new HairPart(null, "hair30")),
            // Casquete cobrindo o crânio inteiro, laço prendendo a nuca (peça de milímetro,
            // intacto) e uma "saia" de pontas soltas abaixo do laço. Espeto no casquete
            // (intensidade longa) e na saia inteira, não só na raiz — opção de saia lisa
            // descartada. A malha vem fatiada em 42 mechas separadas, não superfície contínua;
            // a saia precisou de um ajuste no agrupamento das arestas (um grupo só, não um por
            // lado) porque várias mechas têm a borda colada em X=0, e separar por lado sobrava
            // quase vazio e gerava espeto solto na costura. Malha com esqueleto (filho
            // `attach_skin`), uma peça só, mecha assimétrica na franja sem espelho de propósito.
            // Deitada como o Hair4.
            new HairEntry("SaiyaHair31", "Hair31", new HairPart(null, "hair31")),
            // Touca/coifa rasa cobrindo a cabeça inteira até o ombro, sem rabo. Espeto denso do
            // topo até a nuca/lateral — a primeira leva concentrava tudo no topo porque a cota
            // de espeto esgotava antes de alcançar a parte de trás (o algoritmo varre de cima
            // para baixo e para assim que atinge o limite); corrigido com cota bem maior e
            // espaçamento menor, e espeto mais fino depois do Henrique reportar grossura demais.
            // Malha com esqueleto (filho `attach_skin`), uma peça só, 6 cascas espelhadas em X,
            // duas pontinhas de milímetro (franja e nuca) mantidas intactas. Deitada como o
            // Hair4.
            new HairEntry("SaiyaHair32", "Hair32", new HairPart(null, "hair32")),
            // Mecha única enrolada em gancho de quase 180°, sem casquete separado — a mecha
            // inteira é a silhueta do penteado. Espeto ao longo do gancho inteiro (opção de só
            // a raiz descartada). A normal de vértice saía pouco confiável no meio da curva e
            // fazia espeto nascer apontando para dentro da cabeça; corrigido trocando a normal
            // de face pela direção radial a partir do centroide local do próprio gancho, que não
            // aponta para dentro por construção. Malha com esqueleto (filho `attach_skin`), uma
            // peça só, duas tiras cobrindo o mesmo trecho raiz-a-ponta. Deitada como o Hair4.
            new HairEntry("SaiyaHair33", "Hair33", new HairPart(null, "hair33")),
            // Casquete arredondado tipo capacete/bob colado à cabeça, sem rabo, com franja
            // cortada bem rente e 23 tufos de milímetro decorativos por cima (mantidos
            // intactos). Espeto denso cobrindo do topo até a nuca. Malha com esqueleto (filho
            // `attach_skin`), uma peça só, 25 cascas soltas espelhadas em X. Deitada como o
            // Hair4/Hair30.
            new HairEntry("SaiyaHair34", "Hair34", new HairPart(null, "hair34")),
            // Manto/capuz cobrindo a cabeça inteira até o ombro, com um coque preso atrás e
            // duas abas curtas na frente/nuca (corte em V) — sem casquete separado, o próprio
            // manto é a silhueta. Espeto denso no manto inteiro, cota alta e espaçamento
            // pequeno depois do Henrique pedir mais cobertura na parte de trás (a primeira leva
            // ficava só no topo, mesma causa do Hair32). Coque e tiras de acabamento ficam
            // intactos (peça de milímetro). Malha com esqueleto (filho `attach_skin`), uma peça
            // só, 20 cascas soltas espelhadas em X. Deitada como o Hair4.
            new HairEntry("SaiyaHair35", "Hair35", new HairPart(null, "hair35")),
            // Casquete raso colado ao crânio com dois fios longos caindo dos lados até o
            // ombro/peito — silhueta de cortina, não de rabo central. Fio fino secundário junto
            // do fio principal tratado como parte da mesma cauda (evita par de espeto colidindo
            // no mesmo lugar). Franja frontal dupla (duas camadas empilhadas) mantida lisa —
            // opção de franja espetada descartada. Espeto forte no casquete e na cauda,
            // intensidade média e leve descartadas. Bug corrigido: a raiz da cauda sozinha já
            // tinha candidato de sobra pra cota inteira e empilhava tudo no anel do topo;
            // corrigido escolhendo por fatia de altura em vez de distância euclidiana. Malha com
            // esqueleto (filho `attach_skin`), 16 cascas espelhadas em X. Deitada como o Hair4.
            new HairEntry("SaiyaHair36", "Hair36", new HairPart(null, "hair36")),
            // Casquete arredondado tipo couve-flor/voxel colado à cabeça, sem rabo, com uma
            // mecha fina em gancho/nadadeira só do lado esquerdo, sem gêmea, mantida intacta.
            // As duas metades do casquete não são espelho exato uma da outra (182 vs. 180
            // vértices) — sem `mirror_map`, cada lado escolhe a própria aresta direto, deixando
            // a assimetria original aparecer nos espetos também. Espeto denso do topo até a
            // nuca; a primeira leva (26 espetos) esgotava a cota antes de descer e ficava careca
            // na nuca — mesma causa do Hair32/Hair35, corrigida subindo a cota para 46 em vez de
            // aumentar o `lift`. Malha com esqueleto (filho `attach_skin`), uma peça só, 9
            // cascas soltas. Deitada como o Hair4/Hair30.
            new HairEntry("SaiyaHair37", "Hair37", new HairPart(null, "hair37")),
        };

        private static AssetBundle _bundle;

        /// <summary>
        /// Pendura o registro no evento do Jotunn. O clone só pode ser feito quando os prefabs do
        /// jogo existem — antes disso não há o que copiar.
        /// </summary>
        internal static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabsAvailable;
        }

        private static void OnVanillaPrefabsAvailable()
        {
            foreach (HairEntry hair in Hairs)
            {
                RegisterHair(hair);
            }
        }

        /// <summary>
        /// Registra um penteado. Um que falhe não derruba os outros: cada um é independente, e
        /// perder um cabelo é melhor do que perder todos.
        /// </summary>
        private static void RegisterHair(HairEntry hair)
        {
            // O evento pode disparar mais de uma vez numa sessão (voltar ao menu e entrar de novo).
            if (PrefabManager.Instance.GetPrefab(hair.Name) != null)
            {
                return;
            }

            // As malhas primeiro: um bundle sem a peça que o penteado pede derruba o registro
            // antes de o clone existir, e clone registrado pela metade é pior do que nenhum.
            Mesh[] meshes = new Mesh[hair.Parts.Length];
            for (int i = 0; i < hair.Parts.Length; i++)
            {
                meshes[i] = LoadMesh(hair.Parts[i].Mesh);
                if (meshes[i] == null)
                {
                    return;
                }
            }

            CustomItem item = new CustomItem(hair.Name, hair.Source);
            if (item.ItemPrefab == null)
            {
                SaiyaheimPlugin.Log.LogError(
                    $"Could not clone '{hair.Source}'. Custom hair '{hair.Name}' is off this session.");
                return;
            }

            for (int i = 0; i < hair.Parts.Length; i++)
            {
                if (!SwapMesh(item.ItemPrefab, hair.Parts[i], meshes[i]))
                {
                    return;
                }
            }

            ItemManager.Instance.AddItem(item);
            SaiyaheimPlugin.Log.LogInfo($"Custom hair '{hair.Name}' registered.");
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
        private static Mesh LoadMesh(string meshAsset)
        {
            if (_bundle == null)
            {
                _bundle = LoadBundle();
            }

            if (_bundle == null)
            {
                SaiyaheimPlugin.Log.LogError($"Asset bundle '{BundleName}' not found in the DLL.");
                return null;
            }

            // Os nomes dentro do bundle vêm em minúsculas, e o do Hair1 é `Hair_01` no projeto
            // Unity. Comparar sem diferenciar caixa evita um "não achei" que não é verdade.
            string assetName = _bundle.GetAllAssetNames()
                                      .FirstOrDefault(n => n.EndsWith(
                                          $"/{meshAsset}.asset", StringComparison.OrdinalIgnoreCase));
            if (assetName == null)
            {
                SaiyaheimPlugin.Log.LogError(
                    $"Asset bundle '{BundleName}' has no mesh named '{meshAsset}'. It holds: " +
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
        /// Carrega o bundle embutido, do jeito que sobrevive a ele crescer.
        ///
        /// ⚠️ <b>Não trocar isto pelo <c>AssetUtils.LoadAssetBundleFromResources</c> do
        /// Jotunn.</b> Aquele helper faz <c>using Stream stream = ...</c> e devolve
        /// <c>AssetBundle.LoadFromStream(stream)</c> — ou seja, <b>fecha o stream e entrega o
        /// bundle</b>. O Unity lê o conteúdo do bundle <b>sob demanda</b>, e enquanto o bundle
        /// inteiro coube no buffer interno de leitura (uns 32 KB) ele nunca precisou voltar ao
        /// stream. Foi assim com três penteados.
        ///
        /// Com o quarto (<c>hair3</c>) o bundle passou de 45 KB, o <c>LoadAsset</c> foi buscar
        /// bytes num stream já fechado e o jogo morreu na tela de carregamento:
        /// <c>ArgumentException: ManagedStream object must be readable</c>, seguido de
        /// <c>Size overflow in allocator</c> e <c>Caught fatal signal</c>. O <c>LogOutput.log</c>
        /// não mostra nada disso — quem conta é o <c>Player.log</c> do Unity.
        ///
        /// A correção é ler o recurso inteiro para um <c>byte[]</c> e usar
        /// <c>LoadFromMemory</c>: os bytes são nossos, e não há stream para fechar. O bundle tem
        /// dezenas de KB, então o custo de memória não é assunto.
        /// </summary>
        private static AssetBundle LoadBundle()
        {
            Assembly assembly = typeof(CustomHair).Assembly;
            string resource = assembly.GetManifestResourceNames()
                                      .FirstOrDefault(n => n.EndsWith(BundleName, StringComparison.Ordinal));
            if (resource == null)
            {
                SaiyaheimPlugin.Log.LogError(
                    $"Asset bundle '{BundleName}' not found in the DLL. It holds: " +
                    string.Join(", ", assembly.GetManifestResourceNames()));
                return null;
            }

            byte[] data;
            using (Stream stream = assembly.GetManifestResourceStream(resource))
            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                data = buffer.ToArray();
            }

            AssetBundle bundle = AssetBundle.LoadFromMemory(data);
            if (bundle == null)
            {
                SaiyaheimPlugin.Log.LogError($"'{resource}' did not load as an asset bundle.");
            }

            return bundle;
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
        /// A conferência aqui é só de coerência interna — um peso por vértice, bindposes
        /// presentes. Não se compara com a contagem da malha vanilla: a malha editada tem
        /// <b>mais</b> vértices que ela de propósito, um por espeto acrescentado.
        ///
        /// ⚠️ <b>Nem todo cabelo do Valheim tem esqueleto, e exigir um quebra os que não têm.</b>
        /// São duas famílias. A do <c>Hair6</c> pendura um filho <c>attach_skin</c> com a armature
        /// inteira, e a malha traz bindposes e um peso por vértice. A do <c>Hair1</c> pendura um
        /// filho <c>attach</c> num <c>SkinnedMeshRenderer</c> de <c>m_Bones: []</c>: a malha é
        /// rígida e quem a leva junto com a cabeça é o osso em que o <c>attach</c> é preso. Aí
        /// <c>bindposes</c> e <c>boneWeights</c> vêm <b>vazios de fábrica</b>, e é assim que tem
        /// que ser. Por isso a exigência é <b>relativa à malha vanilla do clone</b>: se ela tem
        /// esqueleto, a nossa também precisa ter; se não tem, a nossa também não pode inventar um.
        ///
        /// ⚠️ <b>Nem todo penteado é uma malha só, e nem toda peça é <c>SkinnedMeshRenderer</c>.</b>
        /// O casquete do <c>Hair2</c> são dois <c>MeshFilter</c> comuns. Procurar sempre o
        /// primeiro <c>SkinnedMeshRenderer</c> trocaria só o rabo de cavalo e deixaria o resto da
        /// cabeça com o cabelo do jogo — sem erro nenhum no log, com o defeito só na tela.
        /// </summary>
        private static bool SwapMesh(GameObject prefab, HairPart part, Mesh mesh)
        {
            SkinnedMeshRenderer skinned = null;
            MeshFilter filter = null;

            if (part.Child == null)
            {
                skinned = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault();
            }
            else
            {
                Transform child = prefab.GetComponentsInChildren<Transform>(true)
                                        .FirstOrDefault(t => t.name == part.Child);
                if (child == null)
                {
                    SaiyaheimPlugin.Log.LogError(
                        $"'{prefab.name}' has no child named '{part.Child}'. The game's prefab " +
                        "layout changed. It holds: " +
                        string.Join(", ", prefab.GetComponentsInChildren<Transform>(true)
                                                .Select(t => t.name).ToArray()));
                    return false;
                }

                skinned = child.GetComponent<SkinnedMeshRenderer>();
                filter = child.GetComponent<MeshFilter>();
            }

            Mesh vanilla = skinned != null ? skinned.sharedMesh
                                           : filter != null ? filter.sharedMesh : null;
            if (vanilla == null)
            {
                SaiyaheimPlugin.Log.LogError(
                    $"'{prefab.name}' has no mesh to replace for part '{part.Child ?? "(first)"}'. " +
                    "The game's prefab layout changed.");
                return false;
            }

            // A exigência é relativa à malha vanilla: com esqueleto, a nossa precisa de um; sem
            // esqueleto, a nossa não pode inventar um.
            if (vanilla.bindposes.Length > 0)
            {
                if (mesh.bindposes.Length == 0 || mesh.boneWeights.Length != mesh.vertexCount)
                {
                    SaiyaheimPlugin.Log.LogError(
                        $"Mesh '{mesh.name}' carries no skinning ({mesh.boneWeights.Length} " +
                        $"weights, {mesh.bindposes.Length} bindposes for {mesh.vertexCount} " +
                        "vertices). It would hang off the body instead of the head. Rebuild the " +
                        "bundle from the mesh that AssetRipper extracts.");
                    return false;
                }
            }
            else if (mesh.bindposes.Length > 0)
            {
                SaiyaheimPlugin.Log.LogError(
                    $"Mesh '{mesh.name}' carries {mesh.bindposes.Length} bindposes, but " +
                    $"'{prefab.name}' is a rigid hair with none. Rebuild the bundle from the " +
                    "mesh that AssetRipper extracts, without adding skinning to it.");
                return false;
            }

            if (skinned != null)
            {
                skinned.sharedMesh = mesh;
            }
            else
            {
                filter.sharedMesh = mesh;
            }

            SaiyaheimPlugin.LogVerbose(
                $"Hair mesh '{mesh.name}' on '{part.Child ?? prefab.name}': {mesh.vertexCount} " +
                $"vertices, {mesh.bindposes.Length} bindposes.");

            return true;
        }
    }
}
