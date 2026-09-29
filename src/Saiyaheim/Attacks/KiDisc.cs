using UnityEngine;

namespace Saiyaheim.Attacks
{
    /// <summary>
    /// O disco do Kienzan, desenhado em código: uma malha plana com a borda serrilhada, girando.
    ///
    /// <b>Por que em código, e não prefab nem bundle.</b> Nenhum prefab do jogo tem forma de disco
    /// — o levantamento de 2026-09-28 achou anéis parados (<c>fx_DvergerMage_Nova_ring</c>) e golpes
    /// giratórios (<c>fx_Fader_Spin</c>), mas nada que voe como um prato. Um disco é a forma mais
    /// simples que existe, e montá-lo aqui não pede Blender, Unity nem passo novo no build. Se um dia
    /// ele for para o <c>saiyaheim_fx</c>, a mecânica não muda: só o que este arquivo põe na tela.
    ///
    /// <b>Dois usos, o mesmo disco:</b>
    /// <list type="bullet">
    /// <item><b>Na mão</b>, durante a carga, como a bola do Kamehameha — pelo
    /// <c>KiBeamChargeEffects</c>, em toda máquina, a partir da bandeira da ZDO.</item>
    /// <item><b>Em voo</b>, vestindo o projétil — pelo <c>KiProjectile.ApplyVisuals</c>, que roda
    /// na máquina de quem atira e, pelo <c>KiProjectileSyncPatch</c>, na dos outros.</item>
    /// </list>
    ///
    /// <b>Nada aqui é objeto de rede</b>: é malha, material e luz locais. O projétil que carrega o
    /// disco é que é de rede, e ele já leva na ZDO qual ataque é.
    ///
    /// <b>O shader é o <c>Sprites/Default</c></b>: sem luz nem sombra, com transparência, e sem
    /// descartar a face de trás — o disco se vê por cima e por baixo com um triângulo só. É o
    /// shader que os mods do Valheim usam para desenhar linha e forma solta, porque vem sempre no
    /// build do Unity. Se faltar, o disco sai sem malha e com a luz, e o log avisa uma vez.
    /// </summary>
    internal static class KiDisc
    {
        /// <summary>Nome do objeto do disco. É o que impede vestir o mesmo projétil duas vezes.</summary>
        private const string NodeName = "SaiyaheimKiDisc";

        private static Shader _shader;
        private static bool _shaderResolved;

        /// <summary>
        /// Cria o disco preso a <paramref name="parent"/>, deitado no plano XZ dele e girando em
        /// torno do Y. Devolve null se o ataque não tem disco.
        ///
        /// <b>O tamanho é em metros de mundo</b>, qualquer que seja a escala do pai: o osso da mão
        /// e a raiz do projétil não têm escala 1, e sem compensar o <c>Radius</c> mediria outra
        /// coisa em cada um dos dois lugares. Quem cresce o disco depois é o <c>EffectScale</c>, que
        /// parte desta escala compensada.
        /// </summary>
        internal static GameObject Create(Transform parent, KiAttack attack)
        {
            SaiyaheimConfig.KiDiscConfig disc = attack?.Config.Disc;
            if (disc == null || parent == null)
            {
                return null;
            }

            Color color = ResolveColor(attack);

            GameObject root = new GameObject(NodeName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one / Mathf.Max(0.0001f, parent.lossyScale.x);

            // A lâmina é filha, e não a própria raiz, porque quem gira é só ela: a raiz tem a
            // orientação imposta de fora (deitada nos eixos do jogador, na mão) e a rotação do giro
            // não pode brigar com isso.
            GameObject blade = new GameObject("Blade");
            blade.transform.SetParent(root.transform, false);

            Mesh mesh = BuildMesh(disc, color);
            Material material = BuildMaterial(disc);

            if (material != null)
            {
                blade.AddComponent<MeshFilter>().sharedMesh = mesh;

                MeshRenderer renderer = blade.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            KiDiscSpin spin = blade.AddComponent<KiDiscSpin>();
            spin.DegreesPerSecond = disc.SpinSpeed;
            spin.OwnedMesh = mesh;
            spin.OwnedMaterial = material;

            float intensity = disc.LightIntensity;
            if (intensity > 0f && disc.LightRange > 0f)
            {
                Light light = root.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.intensity = intensity;
                light.range = disc.LightRange;
                light.shadows = LightShadows.None;
            }

            return root;
        }

        /// <summary>
        /// Troca o visual do projétil pelo disco: apaga tudo que o prefab desenhava e pendura o
        /// disco no lugar. Idempotente — um projétil já vestido não ganha um segundo disco.
        ///
        /// O disco fica deitado no plano do projétil, cuja frente é a direção do voo: ele corta de
        /// lado, como no anime, e se inclina junto quando é jogado para cima ou para baixo.
        /// </summary>
        /// <param name="scale">
        /// O <c>GetProjectileScale</c> da carga. Aplicado aqui porque o <see cref="Create"/>
        /// desfaz a escala do pai, e é nela que o <c>EffectScale</c> tinha posto esse fator.
        /// </param>
        internal static void DressProjectile(GameObject instance, KiAttack attack, float scale)
        {
            if (instance == null || attack?.Config.Disc == null
                || instance.transform.Find(NodeName) != null)
            {
                return;
            }

            HideBase(instance);

            GameObject disc = Create(instance.transform, attack);
            if (disc != null)
            {
                disc.transform.localScale *= Mathf.Max(0.01f, scale);
            }
        }

        /// <summary>
        /// Apaga o que o prefab emprestado desenha — a bola de fogo do xamã, no Kienzan — sem tirar
        /// nada do que ele <i>faz</i>. O objeto de rede, o som de voo e o estouro do impacto (que é
        /// outro prefab, instanciado no acerto) continuam.
        ///
        /// Desligar e não destruir: o <c>Projectile</c> e o <c>EffectScale</c> guardam referência a
        /// alguns destes componentes, e um componente destruído no meio da vida do projétil vira
        /// uma exceção esperando o próximo acesso.
        /// </summary>
        private static void HideBase(GameObject instance)
        {
            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }

            foreach (Light light in instance.GetComponentsInChildren<Light>(true))
            {
                light.enabled = false;
            }
        }

        /// <summary>
        /// A malha: uma lente — dois leques de triângulos, um de cada centro (o de cima e o de
        /// baixo) até a borda, que é comum aos dois e alterna entre a ponta do dente e o fundo dele.
        ///
        /// <b>Lente, e não folha</b>: o disco de uma face só sumia visto de lado (playtest de
        /// 2026-09-28). Com os centros afastados pela <c>Thickness</c>, de lado ele vira um
        /// losango fino, e a borda continua afiada como a do anime.
        ///
        /// A cor vai por vértice — branca nos centros, a do ataque na borda —, e o shader interpola
        /// entre as duas. É o que dá o núcleo quente sem textura nenhuma.
        /// </summary>
        private static Mesh BuildMesh(SaiyaheimConfig.KiDiscConfig disc, Color color)
        {
            int teeth = Mathf.Clamp(disc.Teeth, 3, 96);
            int rim = teeth * 2;
            float radius = Mathf.Max(0.01f, disc.Radius);
            float valley = radius * (1f - Mathf.Clamp(disc.ToothDepth, 0f, 0.9f));
            float half = Mathf.Max(0f, disc.Thickness) * 0.5f;
            float alpha = Mathf.Clamp01(disc.Opacity);

            Color core = Color.Lerp(color, Color.white, Mathf.Clamp01(disc.CoreWhiteness));
            core.a = alpha;

            Color edge = color;
            edge.a = alpha;

            // 0 é o centro de cima, 1 o de baixo, e a borda vem depois.
            const int top = 0;
            const int bottom = 1;
            const int first = 2;

            Vector3[] vertices = new Vector3[rim + first];
            Color[] colors = new Color[rim + first];
            Vector3[] normals = new Vector3[rim + first];
            Vector2[] uvs = new Vector2[rim + first];
            int[] triangles = new int[rim * 6];

            vertices[top] = new Vector3(0f, half, 0f);
            vertices[bottom] = new Vector3(0f, -half, 0f);
            colors[top] = core;
            colors[bottom] = core;
            normals[top] = Vector3.up;
            normals[bottom] = Vector3.down;

            for (int i = 0; i < rim; i++)
            {
                float angle = i * Mathf.PI * 2f / rim;
                float r = i % 2 == 0 ? radius : valley;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                vertices[first + i] = outward * r;
                colors[first + i] = edge;
                normals[first + i] = outward;
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                // Todo UV no mesmo ponto: o material não tem textura (o branco padrão do shader), e
                // a cor inteira vem dos vértices.
                uvs[i] = new Vector2(0.5f, 0.5f);
            }

            // O shader não descarta face de trás, então o sentido dos triângulos não esconde nada;
            // os dois leques estão em sentidos opostos só para a malha ficar correta se o material
            // mudar um dia.
            for (int i = 0; i < rim; i++)
            {
                int a = first + i;
                int b = first + (i + 1) % rim;
                int t = i * 6;

                triangles[t] = top;
                triangles[t + 1] = b;
                triangles[t + 2] = a;

                triangles[t + 3] = bottom;
                triangles[t + 4] = a;
                triangles[t + 5] = b;
            }

            Mesh mesh = new Mesh { name = NodeName };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Material próprio de cada disco. Ele morre com o disco — ver
        /// <see cref="KiDiscSpin.OnDestroy"/>.
        ///
        /// O brilho vai na cor do material, acima de 1. O <c>Sprites/Default</c> multiplica a cor do
        /// vértice por ela, e o que sai acima de 1 alimenta o bloom do jogo — que é o que faz o
        /// disco ler como energia, e não como um prato pintado.
        /// </summary>
        private static Material BuildMaterial(SaiyaheimConfig.KiDiscConfig disc)
        {
            Shader shader = ResolveShader();
            if (shader == null)
            {
                return null;
            }

            float glow = Mathf.Max(0f, disc.Glow);

            return new Material(shader)
            {
                name = NodeName,
                color = new Color(glow, glow, glow, 1f),
            };
        }

        private static Shader ResolveShader()
        {
            if (_shaderResolved)
            {
                return _shader;
            }

            _shaderResolved = true;
            _shader = Shader.Find("Sprites/Default");

            if (_shader == null)
            {
                SaiyaheimPlugin.Log.LogWarning(
                    "Kienzan disc: shader 'Sprites/Default' not found. The disc will only show " +
                    "its light.");
            }

            return _shader;
        }

        /// <summary>A cor do ataque, ou amarelo de ki se ela não servir.</summary>
        private static Color ResolveColor(KiAttack attack)
        {
            string hex = attack.Config.ProjectileColor?.Trim() ?? string.Empty;

            return ColorUtility.TryParseHtmlString(hex, out Color color)
                ? color
                : new Color(1f, 0.9f, 0.3f);
        }
    }

    /// <summary>
    /// Gira a lâmina do disco e, quando ela morre, leva junto a malha e o material.
    ///
    /// <b>A segunda parte é a que importa.</b> Malha e material criados em código não são
    /// destruídos com o <c>GameObject</c> — a Unity os trata como assets soltos. Sem isto cada
    /// Kienzan deixaria um par deles na memória até o fim da sessão.
    /// </summary>
    internal sealed class KiDiscSpin : MonoBehaviour
    {
        internal float DegreesPerSecond;
        internal Mesh OwnedMesh;
        internal Material OwnedMaterial;

        private void Update()
        {
            transform.Rotate(0f, DegreesPerSecond * Time.deltaTime, 0f, Space.Self);
        }

        internal void OnDestroy()
        {
            if (OwnedMesh != null)
            {
                Destroy(OwnedMesh);
            }

            if (OwnedMaterial != null)
            {
                Destroy(OwnedMaterial);
            }
        }
    }
}
