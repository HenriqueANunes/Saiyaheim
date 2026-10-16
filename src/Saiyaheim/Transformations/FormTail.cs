using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Saiyaheim.Net;
using UnityEngine;

namespace Saiyaheim.Transformations
{
    /// <summary>
    /// O rabo (2026-10-15 no SSJ4, em todas as formas desde 2026-10-16): uma malha com ossos
    /// próprios, pendurada num osso do jogador (<c>Hips</c> solto, <c>Spine</c> enrolado) e
    /// balançando por código.
    ///
    /// <b>Dois modos, a mesma malha.</b> A forma com rabo próprio (<c>Tail</c> no
    /// <c>BindTransformation</c>, hoje só o SSJ4) leva o pelo vermelho, descrito abaixo. Fora dela,
    /// com o <c>TailEnabled</c> ligado, o rabo tem a <b>cor do cabelo</b>: na forma base a do
    /// personagem, transformado a da forma. Pedido do Henrique: o rabo acompanha o cabelo, e só o
    /// SSJ4 é exceção.
    ///
    /// <b>Como o rabo pega a cor do cabelo.</b> Cabelo e corpo usam o mesmo shader do jogo
    /// (<c>Custom/Player</c>), e a cor do cabelo é só a <c>_SkinColor</c> multiplicando uma
    /// <c>_MainTex</c> cinza (<c>Hair_01_bw</c>). O <c>VisEquipment.UpdateColors</c> escreve essa
    /// cor também no segundo material do corpo, que é público. Então o modo cabelo copia esse
    /// material, põe na <c>_MainTex</c> uma versão cinza do pelo e copia a <c>_SkinColor</c> dele a
    /// cada tique: a cor da forma, com a intensidade acima de 1, chega igual ao cabelo e pela ZDO,
    /// sem rede nova.
    ///
    /// <b>Por que não vem do bundle de cabelo.</b> O <c>BundleBuilder</c> herda peso e bindpose de
    /// uma malha do jogo, e o rabo não tem malha do jogo de onde herdar: os ossos dele não existem
    /// no esqueleto do jogador. Então o <c>tools/ssj4_rabo.py</c> do projeto Unity grava a malha,
    /// a UV e os pesos num texto (<c>Assets/body/ssj4_rabo.txt</c>, embutido na DLL), já no espaço
    /// local do <c>Hips</c>, e este arquivo monta a <c>SkinnedMeshRenderer</c> e a cadeia de ossos
    /// em tempo de execução. É a mesma geometria que o Henrique julgou no Blender.
    ///
    /// <b>Material: uma cópia do material do corpo</b>, com o pelo na <c>_ChestTex</c>. A UV do rabo
    /// aponta para um bloco da textura do pelo todo coberto, então ele sai do mesmo vermelho e com
    /// o mesmo shader do jogo, sem shader no bundle (ver [[Malhas Custom]], o bundle com shader que
    /// derrubou o jogo). A <c>_LegsTex</c> da cópia vira transparente: a cueca não tem nada a fazer
    /// no rabo. A cor de pele (<c>_SkinColor</c>) é copiada do corpo a cada tique, para o rabo
    /// acompanhar o vermelho do Kaioken.
    ///
    /// <b>Solto ou enrolado na cintura</b> (2026-10-16): escolha do personagem, pelo menu radial,
    /// guardada no <c>m_customData</c> (vai no save, como o modo do voo) e publicada pela ZDO, para
    /// os outros verem a mesma pose. São duas malhas com a mesma cadeia de 8 ossos e a mesma raiz:
    /// a enrolada é o nome da solta com <see cref="WrappedSuffix"/>. Enrolado, o rabo não dobra com
    /// o movimento e só a ponta balança, de leve: a volta está deitada no corpo, e girar a cadeia
    /// inteira a descolaria da cintura.
    ///
    /// <b>O balanço</b> é por osso, somado ao longo da cadeia: uma onda lenta parado, e uma dobra
    /// para trás do movimento (correr levanta o rabo, andar de lado joga para o outro lado, cair
    /// levanta), com a velocidade do próprio rabo no mundo e suavizada para ter peso. Os números
    /// estão no <c>.cfg</c>, client-side, na seção das transformações.
    ///
    /// <b>Roda para todo jogador carregado</b>, pelo <c>TransformationEffects.Observe</c> e a forma
    /// do <c>NetState</c>, como o pelo do corpo. Os ossos são nossos e o Animator não os conhece,
    /// então nada sobrescreve a rotação que o tique escreve.
    /// </summary>
    internal static class FormTail
    {
        private static readonly int ChestTexId = Shader.PropertyToID("_ChestTex");
        private static readonly int ChestBumpId = Shader.PropertyToID("_ChestBumpMap");
        private static readonly int ChestMetalId = Shader.PropertyToID("_ChestMetal");
        private static readonly int LegsTexId = Shader.PropertyToID("_LegsTex");
        private static readonly int LegsMetalId = Shader.PropertyToID("_LegsMetal");
        private static readonly int SkinColorId = Shader.PropertyToID("_SkinColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int SkinBumpId = Shader.PropertyToID("_SkinBumpMap");

        /// <summary>A textura de pelo que o rabo usa: a do corpo masculino, onde fica o bloco da UV.</summary>
        private const string FurTexture = "ssj4_pelo";

        /// <summary>A malha do rabo de quem não está numa forma com rabo próprio.</summary>
        private const string DefaultTail = "ssj4_rabo";

        /// <summary>A malha enrolada é a solta com este sufixo (<c>tools/ssj4_rabo.py</c>, <c>export_cintura</c>).</summary>
        private const string WrappedSuffix = "_cintura";

        /// <summary>Quantos ossos da ponta balançam com o rabo enrolado.</summary>
        private const int WrappedSwayBones = 2;

        /// <summary>
        /// Rabo enrolado, salvo no personagem. Estado e não preferência, como o modo do voo
        /// (<c>FlightManager</c>): ausente é solto.
        /// </summary>
        private const string KeyWrapped = "saiyaheim.tailWrapped";

        /// <summary>
        /// Brilho médio do <c>Hair_01_bw.png</c> do jogo, a textura cinza que a cor do cabelo
        /// multiplica (medido no jogo extraído em 2026-10-16). O pelo cinza sai com essa média,
        /// para a mesma cor dar o mesmo tom no cabelo e no rabo.
        /// </summary>
        private const float HairTextureMean = 0.526f;

        /// <summary>Material do corpo onde o jogo escreve a cor do cabelo.</summary>
        private const int HairMaterialSlot = 1;

        private sealed class TailData
        {
            internal string Parent = "Hips";
            internal Vector3[] Chain;
            internal Vector3[] Vertices;
            internal Vector2[] Uvs;
            internal BoneWeight[] Weights;
        }

        private sealed class Instance
        {
            internal string Name;
            internal bool Fur;
            internal bool Wrapped;
            internal GameObject Root;
            internal Transform[] Bones;
            internal Mesh Mesh;
            internal Material Material;
            internal SkinnedMeshRenderer Body;
            internal Vector3 LastPosition;
            internal bool HasLast;
            internal Vector2 Bend;
            internal float Phase;
        }

        private static readonly Dictionary<Player, Instance> Live = new Dictionary<Player, Instance>();

        /// <summary>Rabos lidos, por nome. Null guardado quer dizer "falhou, não tente de novo".</summary>
        private static readonly Dictionary<string, TailData> Loaded =
            new Dictionary<string, TailData>(StringComparer.OrdinalIgnoreCase);

        private static Texture2D _clear;
        private static Texture2D _grayFur;
        private static bool _grayFurTried;

        internal static void Tick(Player player, Transformation form)
        {
            if (player == null)
            {
                return;
            }

            // A forma com rabo próprio manda; sem ela, o rabo da cor do cabelo, se ligado.
            string name = form == null ? null : form.Config.Tail;
            bool fur = !string.IsNullOrEmpty(name);
            if (!fur)
            {
                name = SaiyaheimConfig.TailEnabled.Value ? DefaultTail : null;
            }

            // Quem não é o jogador local chega aqui pela ZDO; o local também, por regra do canal.
            bool wrapped = name != null && NetState.IsTailWrapped(player);
            if (wrapped)
            {
                name += WrappedSuffix;
            }

            Live.TryGetValue(player, out Instance inst);

            if (inst != null && (inst.Root == null || inst.Name != name || inst.Fur != fur))
            {
                Destroy(inst);
                Live.Remove(player);
                inst = null;
            }

            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (inst == null)
            {
                inst = Create(player, name, fur);
                if (inst == null)
                {
                    return;
                }

                inst.Wrapped = wrapped;

                Live[player] = inst;
            }

            Animate(inst);
        }

        /// <summary>O rabo deste personagem está enrolado na cintura.</summary>
        internal static bool IsWrapped(Player player)
        {
            return player != null && player.m_customData != null
                   && player.m_customData.TryGetValue(KeyWrapped, out string raw) && raw == "1";
        }

        /// <summary>Enrola ou solta o rabo do personagem. A pose troca no próximo tique, pela ZDO.</summary>
        internal static void SetWrapped(Player player, bool wrapped)
        {
            if (player == null || player.m_customData == null)
            {
                return;
            }

            if (wrapped)
            {
                player.m_customData[KeyWrapped] = "1";
            }
            else
            {
                player.m_customData.Remove(KeyWrapped);
            }
        }

        /// <summary>
        /// Este jogador tem rabo agora: na forma com rabo próprio, ou em qualquer outra com o
        /// <c>TailEnabled</c> ligado. O menu radial só oferece enrolar quando há o que enrolar.
        /// </summary>
        internal static bool HasTail(Player player)
        {
            if (player == null)
            {
                return false;
            }

            Transformation form = TransformationRegistry.GetActive(player);
            return SaiyaheimConfig.TailEnabled.Value || (form != null && !string.IsNullOrEmpty(form.Config.Tail));
        }

        /// <summary>Este jogador deixou de existir: o rabo morreu junto, falta a malha e o material.</summary>
        internal static void Forget(Player player)
        {
            if (player != null && Live.TryGetValue(player, out Instance inst))
            {
                Destroy(inst);
                Live.Remove(player);
            }
        }

        /// <summary>Tira o rabo de todo mundo. Caminho de erro dos efeitos.</summary>
        internal static void Reset()
        {
            foreach (Instance inst in Live.Values)
            {
                Destroy(inst);
            }

            Live.Clear();
        }

        private static void Destroy(Instance inst)
        {
            if (inst.Root != null)
            {
                UnityEngine.Object.Destroy(inst.Root);
            }

            if (inst.Mesh != null)
            {
                UnityEngine.Object.Destroy(inst.Mesh);
            }

            if (inst.Material != null)
            {
                UnityEngine.Object.Destroy(inst.Material);
            }
        }

        private static Instance Create(Player player, string name, bool fur)
        {
            VisEquipment vis = player.GetVisEquipment();
            SkinnedMeshRenderer body = vis == null ? null : vis.m_bodyModel;
            TailData data = body == null ? null : Get(name);
            Transform anchor = data == null ? null : FindBone(body, data.Parent);
            if (anchor == null || !FormBodyFur.TryGetTextures(FurTexture, out Texture2D furColor, out Texture2D normal))
            {
                return null;
            }

            Texture2D gray = fur ? null : GrayFur();
            if (!fur && gray == null)
            {
                return null;
            }

            var root = new GameObject("SaiyaheimTail");
            root.layer = body.gameObject.layer;
            root.transform.SetParent(anchor, false);

            // A cadeia: cada osso filho do anterior, na posição de repouso, sem rotação. O balanço
            // gira cada um em volta da própria origem.
            var bones = new Transform[data.Chain.Length];
            for (int k = 0; k < bones.Length; k++)
            {
                var bone = new GameObject("tail_" + k).transform;
                bone.SetParent(k == 0 ? root.transform : bones[k - 1], false);
                bone.localPosition = k == 0 ? data.Chain[0] : data.Chain[k] - data.Chain[k - 1];
                bones[k] = bone;
            }

            var mesh = new Mesh { name = "saiyaheim_" + name };
            mesh.vertices = data.Vertices;
            mesh.uv = data.Uvs;
            mesh.boneWeights = data.Weights;
            int[] triangles = new int[data.Vertices.Length];
            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i] = i;
            }

            mesh.triangles = triangles;
            var bindposes = new Matrix4x4[bones.Length];
            for (int k = 0; k < bones.Length; k++)
            {
                bindposes[k] = bones[k].worldToLocalMatrix * root.transform.localToWorldMatrix;
            }

            mesh.bindposes = bindposes;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            Material material;
            if (fur)
            {
                // Cópia do material do corpo: o shader e as propriedades do jogo, com o pelo no peito.
                material = new Material(body.sharedMaterials[0]) { name = "saiyaheim_tail" };
                material.SetTexture(ChestTexId, furColor);
                material.SetTexture(ChestBumpId, normal);
            }
            else
            {
                // Cópia do material do cabelo do corpo: o pelo cinza no lugar da pele, que é a
                // camada que a _SkinColor pinta, e o peito vazio para a pele aparecer.
                material = new Material(body.sharedMaterials[HairSlot(body)]) { name = "saiyaheim_tail_hair" };
                material.SetTexture(MainTexId, gray);
                material.SetTexture(SkinBumpId, normal);
                material.SetTexture(ChestTexId, Clear());
            }

            if (material.HasProperty(ChestMetalId))
            {
                material.SetTexture(ChestMetalId, null);
            }

            if (material.HasProperty(LegsTexId))
            {
                material.SetTexture(LegsTexId, Clear());
            }

            if (material.HasProperty(LegsMetalId))
            {
                material.SetTexture(LegsMetalId, null);
            }

            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.rootBone = bones[0];
            renderer.sharedMaterial = material;
            // O rabo balança para fora da caixa de repouso; com uma malha deste tamanho, recalcular
            // a caixa todo frame custa menos que o rabo sumindo na borda da tela.
            renderer.updateWhenOffscreen = true;

            SaiyaheimPlugin.LogVerbose($"Tail '{name}' attached to {player.GetPlayerName()}.");

            return new Instance
            {
                Name = name,
                Fur = fur,
                Root = root,
                Bones = bones,
                Mesh = mesh,
                Material = material,
                Body = body,
                // Cada jogador num ponto diferente da onda, para dois SSJ4 lado a lado não
                // balançarem em uníssono.
                Phase = (player.GetPlayerID() & 0xFFFF) / 65536f * 2f * Mathf.PI,
            };
        }

        private static void Animate(Instance inst)
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || inst.Bones.Length == 0)
            {
                return;
            }

            // Com pelo, a cor da pele, para acompanhar o vermelho do Kaioken; sem, a do cabelo.
            Material source = inst.Body == null || inst.Body.sharedMaterials.Length == 0
                ? null
                : inst.Body.sharedMaterials[inst.Fur ? 0 : HairSlot(inst.Body)];
            if (source != null && source.HasProperty(SkinColorId))
            {
                Color color = source.GetColor(SkinColorId);
                if (!inst.Fur)
                {
                    float brightness = SaiyaheimConfig.TailHairBrightness.Value;
                    color = new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
                }

                inst.Material.SetColor(SkinColorId, color);
            }

            int n = inst.Bones.Length;
            float amp = SaiyaheimConfig.TailSwayDegrees.Value;
            float wave = Time.time * 2f * Mathf.PI * SaiyaheimConfig.TailSwaySpeed.Value + inst.Phase;

            if (inst.Wrapped)
            {
                // Deitado na cintura: sem dobra do movimento, e só a ponta mexe. Os ossos de antes
                // dela ficam no repouso, que é a volta colada ao corpo.
                float tip = SaiyaheimConfig.TailWrappedTipSway.Value;
                int first = Mathf.Max(0, n - WrappedSwayBones);
                for (int k = 0; k < n; k++)
                {
                    if (k < first)
                    {
                        inst.Bones[k].localRotation = Quaternion.identity;
                        continue;
                    }

                    float reach = (k - first + 1f) / WrappedSwayBones;
                    float yaw = tip * reach * Mathf.Sin(wave - k * 0.6f);
                    float pitch = tip * 0.4f * reach * Mathf.Sin(wave * 0.7f - k * 0.6f);
                    inst.Bones[k].localRotation = Quaternion.Euler(pitch, yaw, 0f);
                }

                inst.HasLast = false;
                inst.Bend = Vector2.zero;
                return;
            }

            Transform root = inst.Root.transform;
            Vector3 position = root.position;
            Vector3 velocity = inst.HasLast ? (position - inst.LastPosition) / dt : Vector3.zero;
            inst.LastPosition = position;
            inst.HasLast = true;

            // Teleporte, respawn, porta de portal: um salto desses não é movimento.
            if (velocity.sqrMagnitude > 50f * 50f)
            {
                velocity = Vector3.zero;
            }

            // No espaço do Hips: X é o lado, Y é cima, Z é a frente (o bindpose do jogo leva o Z
            // da malha para Y e o -Y para Z). O rabo repousa para -Z e para baixo.
            Vector3 local = root.parent.InverseTransformDirection(velocity);
            float gain = SaiyaheimConfig.TailMotionBend.Value;
            float max = SaiyaheimConfig.TailMotionMaxBend.Value;
            // Girar em +Y leva o -Z para -X: andar para +X joga o rabo para -X. Girar em +X leva
            // o -Z para +Y: andar para a frente, ou cair, levanta o rabo.
            var target = new Vector2(
                Mathf.Clamp(local.x * gain, -max, max),
                Mathf.Clamp((local.z - local.y) * gain, -max, max));
            inst.Bend = Vector2.Lerp(inst.Bend, target,
                1f - Mathf.Exp(-SaiyaheimConfig.TailResponsiveness.Value * dt));

            for (int k = 0; k < n; k++)
            {
                // A onda anda da raiz para a ponta (o atraso por osso) e cresce para a ponta.
                float reach = (k + 1f) / n;
                float yaw = inst.Bend.x / n + amp * reach * Mathf.Sin(wave - k * 0.6f);
                float pitch = inst.Bend.y / n + amp * 0.4f * reach * Mathf.Sin(wave * 0.7f - k * 0.6f);
                inst.Bones[k].localRotation = Quaternion.Euler(pitch, yaw, 0f);
            }
        }

        /// <summary>
        /// O estado do rabo de um jogador, para o <c>saiya_form tail</c>: se existe, onde está
        /// pendurado, se a malha tem os ossos que deveria e a rotação de cada osso agora. Rodar duas
        /// vezes seguidas mostra se o balanço está andando.
        /// </summary>
        internal static List<string> Describe(Player player)
        {
            var lines = new List<string>();
            if (player == null || !Live.TryGetValue(player, out Instance inst) || inst.Root == null)
            {
                lines.Add("No tail on this player. Outside SSJ4 it only exists with TailEnabled on.");
                return lines;
            }

            var renderer = inst.Root.GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = renderer == null ? null : renderer.sharedMesh;
            lines.Add($"Tail '{inst.Name}' ({(inst.Fur ? "fur" : "hair color")}, {(inst.Wrapped ? "wrapped" : "loose")}) on bone '{inst.Root.transform.parent?.name}', " +
                      $"renderer {(renderer == null ? "missing" : renderer.enabled ? "on" : "off")}, " +
                      $"visible {renderer != null && renderer.isVisible}, quality {renderer?.quality}.");
            lines.Add($"Mesh: {mesh?.vertexCount} vertices, {mesh?.bindposes.Length} bindposes, " +
                      $"{mesh?.boneWeights.Length} weights; renderer bones {renderer?.bones?.Length}, " +
                      $"root bone {renderer?.rootBone?.name}.");
            lines.Add($"Motion bend (yaw, pitch): {inst.Bend.x:0.0}, {inst.Bend.y:0.0} degrees. " +
                      $"Sway {SaiyaheimConfig.TailSwayDegrees.Value} deg at {SaiyaheimConfig.TailSwaySpeed.Value}/s.");
            for (int k = 0; k < inst.Bones.Length; k++)
            {
                Vector3 e = inst.Bones[k].localEulerAngles;
                lines.Add($"  bone {k}: local rotation ({Signed(e.x):0.0}, {Signed(e.y):0.0}, {Signed(e.z):0.0})");
            }

            if (renderer != null)
            {
                lines.Add($"Bounds center {renderer.bounds.center}, tip at {inst.Bones[inst.Bones.Length - 1].position}.");
            }

            return lines;
        }

        private static float Signed(float degrees) => degrees > 180f ? degrees - 360f : degrees;

        /// <summary>
        /// O osso do corpo onde o rabo fica pendurado: pelo nome, e o primeiro osso (Hips) se o
        /// nome não estiver lá. O solto vai no Hips; o enrolado no Spine, que é quem move a pele da
        /// cintura (ver o <c>CINTURA_BONE</c> do <c>tools/ssj4_rabo.py</c>).
        /// </summary>
        private static Transform FindBone(SkinnedMeshRenderer body, string name)
        {
            if (body == null || body.bones == null || body.bones.Length == 0)
            {
                return null;
            }

            foreach (Transform bone in body.bones)
            {
                if (bone != null && bone.name == name)
                {
                    return bone;
                }
            }

            return body.bones[0];
        }

        private static Texture2D Clear()
        {
            if (_clear == null)
            {
                _clear = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "saiyaheim_clear" };
                _clear.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
                _clear.Apply();
            }

            return _clear;
        }

        /// <summary>O material do corpo com a cor do cabelo; o primeiro, se o corpo só tiver um.</summary>
        private static int HairSlot(SkinnedMeshRenderer body) =>
            body.sharedMaterials.Length > HairMaterialSlot ? HairMaterialSlot : 0;

        /// <summary>
        /// O pelo do SSJ4 sem a cor: o canal mais forte de cada pixel, escalado para o brilho médio
        /// do cabelo do jogo. Onde o pelo é transparente entra a média, opaca, para a borda do
        /// bloco não abrir buraco nos mipmaps. Feito uma vez, na primeira vez que alguém precisa.
        /// </summary>
        private static Texture2D GrayFur()
        {
            if (_grayFurTried)
            {
                return _grayFur;
            }

            _grayFurTried = true;
            Texture2D source = FormBodyFur.LoadReadableTexture(FurTexture);
            if (source == null)
            {
                return null;
            }

            Color32[] pixels = source.GetPixels32();
            int width = source.width;
            int height = source.height;
            UnityEngine.Object.Destroy(source);

            // O mesmo corte de transparência do material do cabelo (_Cutoff 0,35).
            const byte opaque = 89;
            double sum = 0;
            int count = 0;
            foreach (Color32 p in pixels)
            {
                if (p.a >= opaque)
                {
                    sum += Math.Max(p.r, Math.Max(p.g, p.b));
                    count++;
                }
            }

            if (count == 0)
            {
                SaiyaheimPlugin.Log.LogError($"Fur texture '{FurTexture}' has no opaque pixel; the hair-colored tail is off.");
                return null;
            }

            float scale = HairTextureMean * 255f / (float)(sum / count);
            byte fill = (byte)Mathf.RoundToInt(HairTextureMean * 255f);
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                byte v = p.a >= opaque
                    ? (byte)Mathf.Clamp(Mathf.RoundToInt(Math.Max(p.r, Math.Max(p.g, p.b)) * scale), 0, 255)
                    : fill;
                pixels[i] = new Color32(v, v, v, 255);
            }

            _grayFur = new Texture2D(width, height, TextureFormat.RGBA32, true, false)
            {
                name = "saiyaheim_" + FurTexture + "_gray",
                wrapMode = TextureWrapMode.Clamp,
            };
            _grayFur.SetPixels32(pixels);
            _grayFur.Apply(true, true);

            return _grayFur;
        }

        private static TailData Get(string name)
        {
            if (Loaded.TryGetValue(name, out TailData cached))
            {
                return cached;
            }

            TailData data = null;
            try
            {
                data = Load(name);
            }
            catch (Exception e)
            {
                SaiyaheimPlugin.Log.LogError($"Tail '{name}' did not load: {e.Message}");
            }

            Loaded[name] = data;
            return data;
        }

        /// <summary>
        /// Lê o texto do <c>tools/ssj4_rabo.py</c>: linha <c>p</c> (osso do corpo, Hips se faltar),
        /// linhas <c>b</c> (ossos) e <c>t</c> (triângulos).
        /// </summary>
        private static TailData Load(string name)
        {
            var assembly = typeof(FormTail).Assembly;
            string suffix = $"body.{name}.txt";
            string resource = assembly.GetManifestResourceNames()
                                      .FirstOrDefault(r => r.EndsWith(suffix, StringComparison.Ordinal));
            if (resource == null)
            {
                SaiyaheimPlugin.Log.LogError($"Tail '{name}' not found in the DLL as '{suffix}'.");
                return null;
            }

            string parent = "Hips";
            var chain = new List<Vector3>();
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();

            using (var reader = new StreamReader(assembly.GetManifestResourceStream(resource)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] f = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (f.Length == 0 || f[0].StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (f[0] == "p" && f.Length == 2)
                    {
                        parent = f[1];
                        continue;
                    }

                    if (f[0] == "b")
                    {
                        chain.Add(new Vector3(F(f[1]), F(f[2]), F(f[3])));
                        continue;
                    }

                    if (f[0] != "t" || f.Length != 1 + 3 * 7)
                    {
                        throw new InvalidDataException($"bad line: {line}");
                    }

                    for (int c = 0; c < 3; c++)
                    {
                        int o = 1 + c * 7;
                        vertices.Add(new Vector3(F(f[o]), F(f[o + 1]), F(f[o + 2])));
                        uvs.Add(new Vector2(F(f[o + 3]), F(f[o + 4])));
                        int bone = int.Parse(f[o + 5], CultureInfo.InvariantCulture);
                        float w = F(f[o + 6]);
                        // O peso maior primeiro: é a ordem que a Unity espera das influências, e
                        // com "1 osso" na qualidade de skinning é a primeira que vale.
                        weights.Add(w <= 0.5f
                            ? new BoneWeight { boneIndex0 = bone, weight0 = 1f - w, boneIndex1 = bone + 1, weight1 = w }
                            : new BoneWeight { boneIndex0 = bone + 1, weight0 = w, boneIndex1 = bone, weight1 = 1f - w });
                    }
                }
            }

            if (chain.Count < 2 || vertices.Count == 0 ||
                weights.Any(w => Math.Max(w.boneIndex0, w.boneIndex1) >= chain.Count))
            {
                throw new InvalidDataException($"{chain.Count} bones, {vertices.Count} vertices");
            }

            return new TailData
            {
                Parent = parent,
                Chain = chain.ToArray(),
                Vertices = vertices.ToArray(),
                Uvs = uvs.ToArray(),
                Weights = weights.ToArray(),
            };
        }

        private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    }
}
