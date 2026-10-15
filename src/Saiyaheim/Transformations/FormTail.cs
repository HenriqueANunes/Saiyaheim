using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Saiyaheim.Transformations
{
    /// <summary>
    /// O rabo do SSJ4 (2026-10-15): uma malha com ossos próprios, pendurada no osso <c>Hips</c> do
    /// jogador enquanto a forma está ativa, e balançando por código.
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
    /// <b>O balanço</b> é por osso, somado ao longo da cadeia: uma onda lenta parado, e uma dobra
    /// para trás do movimento (correr levanta o rabo, andar de lado joga para o outro lado, cair
    /// levanta), com a velocidade do próprio rabo no mundo e suavizada para ter peso. Os números
    /// estão no <c>.cfg</c>, client-side, na seção do SSJ4.
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

        /// <summary>A textura de pelo que o rabo usa: a do corpo masculino, onde fica o bloco da UV.</summary>
        private const string FurTexture = "ssj4_pelo";

        private sealed class TailData
        {
            internal Vector3[] Chain;
            internal Vector3[] Vertices;
            internal Vector2[] Uvs;
            internal BoneWeight[] Weights;
        }

        private sealed class Instance
        {
            internal string Name;
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

        internal static void Tick(Player player, Transformation form)
        {
            if (player == null)
            {
                return;
            }

            string name = form == null ? null : form.Config.Tail;
            Live.TryGetValue(player, out Instance inst);

            if (inst != null && (inst.Root == null || inst.Name != name))
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
                inst = Create(player, name);
                if (inst == null)
                {
                    return;
                }

                Live[player] = inst;
            }

            Animate(inst);
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

        private static Instance Create(Player player, string name)
        {
            VisEquipment vis = player.GetVisEquipment();
            SkinnedMeshRenderer body = vis == null ? null : vis.m_bodyModel;
            Transform hips = FindHips(body);
            TailData data = hips == null ? null : Get(name);
            if (data == null || !FormBodyFur.TryGetTextures(FurTexture, out Texture2D fur, out Texture2D normal))
            {
                return null;
            }

            var root = new GameObject("SaiyaheimTail");
            root.layer = body.gameObject.layer;
            root.transform.SetParent(hips, false);

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

            // Cópia do material do corpo: o shader e as propriedades do jogo, com o pelo no peito.
            var material = new Material(body.sharedMaterials[0]) { name = "saiyaheim_tail" };
            material.SetTexture(ChestTexId, fur);
            material.SetTexture(ChestBumpId, normal);
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

            if (inst.Body != null && inst.Body.sharedMaterials.Length > 0 &&
                inst.Body.sharedMaterials[0].HasProperty(SkinColorId))
            {
                inst.Material.SetColor(SkinColorId, inst.Body.sharedMaterials[0].GetColor(SkinColorId));
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

            int n = inst.Bones.Length;
            float amp = SaiyaheimConfig.TailSwayDegrees.Value;
            float wave = Time.time * 2f * Mathf.PI * SaiyaheimConfig.TailSwaySpeed.Value + inst.Phase;

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
                lines.Add("No tail on this player. It only exists while in a form that has one (SSJ4).");
                return lines;
            }

            var renderer = inst.Root.GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = renderer == null ? null : renderer.sharedMesh;
            lines.Add($"Tail '{inst.Name}' on bone '{inst.Root.transform.parent?.name}', " +
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

        /// <summary>O osso Hips do corpo: pelo nome, e o primeiro osso se o nome não estiver lá.</summary>
        private static Transform FindHips(SkinnedMeshRenderer body)
        {
            if (body == null || body.bones == null || body.bones.Length == 0)
            {
                return null;
            }

            foreach (Transform bone in body.bones)
            {
                if (bone != null && bone.name == "Hips")
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

        /// <summary>Lê o texto do <c>tools/ssj4_rabo.py</c>: linhas <c>b</c> (ossos) e <c>t</c> (triângulos).</summary>
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
                Chain = chain.ToArray(),
                Vertices = vertices.ToArray(),
                Uvs = uvs.ToArray(),
                Weights = weights.ToArray(),
            };
        }

        private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    }
}
