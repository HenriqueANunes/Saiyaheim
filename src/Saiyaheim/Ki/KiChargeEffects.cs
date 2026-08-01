using System;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Ki
{
    /// <summary>
    /// Feedback do carregamento de ki: animação, efeito visual e som.
    ///
    /// Tudo reaproveitado do jogo — **nada exige Blender ou Unity**:
    ///
    /// - **Animação:** um emote existente em loop, via <c>Player.StartEmote(nome, oneshot: false)</c>.
    ///   O emote é escrito na ZDO, então **replica no multiplayer de graça** — outros jogadores
    ///   veem a pose sem nenhum RPC nosso. O jogo também interrompe emote sozinho quando o
    ///   jogador anda, o que casa com carregar parado.
    /// - **Visual e som:** prefabs `fx_`/`sfx_` do jogo, instanciados presos ao transform do
    ///   jogador. Ver [[Prefabs do Jogo]] no vault para a paleta levantada.
    ///
    /// Os nomes de prefab e de emote ficam **em config**, não no código: qual pose e qual efeito
    /// "lê" como carregar ki é julgamento visual, e quem vê a tela é o Henrique. Trocar deve
    /// custar editar um .cfg, não uma recompilação.
    /// </summary>
    internal static class KiChargeEffects
    {
        private static GameObject _vfx;
        private static GameObject _sfx;
        private static bool _emoteStarted;
        private static bool _disabled;

        private static bool IsActive => _vfx != null || _sfx != null || _emoteStarted;

        internal static void Update(Player player, bool charging)
        {
            if (_disabled)
            {
                return;
            }

            try
            {
                if (charging && !IsActive)
                {
                    Start(player);
                }
                else if (!charging && IsActive)
                {
                    Stop(player);
                }
            }
            catch (Exception ex)
            {
                _disabled = true;
                Cleanup();
                SaiyaheimPlugin.Log.LogError($"Charging effects disabled after an error: {ex}");
            }
        }

        /// <summary>
        /// Zera o estado sem tentar tocar no jogador — usado quando ele deixou de existir
        /// (morte, saída do mundo). Os objetos de efeito são filhos do transform dele e já
        /// morreram junto; só as referências ficaram.
        /// </summary>
        internal static void Reset()
        {
            _vfx = null;
            _sfx = null;
            _emoteStarted = false;
        }

        private static void Start(Player player)
        {
            if (SaiyaheimConfig.ChargeEmote.Value.Length > 0)
            {
                // oneshot: false = fica em loop até mandarmos parar.
                _emoteStarted = player.StartEmote(SaiyaheimConfig.ChargeEmote.Value, oneshot: false);
            }

            _vfx = Spawn(SaiyaheimConfig.ChargeEffectPrefab.Value, player);
            _sfx = Spawn(SaiyaheimConfig.ChargeSoundPrefab.Value, player);
        }

        private static void Stop(Player player)
        {
            if (_emoteStarted)
            {
                GameAccess.StopEmote(player);
                _emoteStarted = false;
            }

            Cleanup();
        }

        private static void Cleanup()
        {
            if (_vfx != null)
            {
                UnityEngine.Object.Destroy(_vfx);
                _vfx = null;
            }

            if (_sfx != null)
            {
                UnityEngine.Object.Destroy(_sfx);
                _sfx = null;
            }
        }

        private static GameObject Spawn(string prefabName, Player player)
        {
            if (string.IsNullOrEmpty(prefabName) || ZNetScene.instance == null)
            {
                return null;
            }

            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                SaiyaheimPlugin.Log.LogWarning(
                    $"Prefab '{prefabName}' does not exist. Check the name against the list in the docs.");
                return null;
            }

            // Efeito puramente local e visual: sem ZDO, sem replicação, sem sujar a rede.
            // Cada cliente instancia o seu ao ver o emote — que esse sim replica sozinho.
            bool previousDisableInit = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;

            GameObject instance;
            try
            {
                instance = UnityEngine.Object.Instantiate(
                    prefab, player.transform.position, player.transform.rotation, player.transform);
            }
            finally
            {
                ZNetView.m_forceDisableInit = previousDisableInit;
            }

            PrepareForSustainedUse(instance);
            ApplyTint(instance);

            float scale = SaiyaheimConfig.ChargeEffectScale.Value;
            if (!Mathf.Approximately(scale, 1f))
            {
                instance.transform.localScale *= scale;
            }

            return instance;
        }

        /// <summary>
        /// Tinge o efeito. Vale para partículas, luzes e materiais.
        ///
        /// ⚠️ O ponto crítico é usar <c>renderer.materials</c> e **nunca**
        /// <c>sharedMaterials</c>: o material compartilhado é o asset do jogo, e escrever nele
        /// pintaria de azul o efeito original para todo mundo que o usa — Dvergr, poções,
        /// qualquer coisa — até reiniciar o jogo.
        ///
        /// Só a cor base é trocada. O <c>colorOverLifetime</c> das partículas multiplica por
        /// cima, então o fade original é preservado.
        /// </summary>
        private static void ApplyTint(GameObject instance)
        {
            string raw = SaiyaheimConfig.ChargeEffectColor.Value;
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            if (!ColorUtility.TryParseHtmlString(raw, out Color color))
            {
                SaiyaheimPlugin.Log.LogWarning(
                    $"ChargeEffectColor '{raw}' is not a valid color. Use the #RRGGBB format.");
                return;
            }

            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                main.startColor = color;
            }

            foreach (Light light in instance.GetComponentsInChildren<Light>(true))
            {
                light.color = color;
            }

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                // .materials devolve instâncias exclusivas deste clone. Elas morrem junto com ele.
                foreach (Material material in renderer.materials)
                {
                    TintMaterial(material, color);
                }
            }
        }

        /// <summary>
        /// Os shaders do Valheim não usam uma propriedade única de cor. Escrevemos em todas as
        /// que o material declarar — <c>HasProperty</c> evita erro nas que não existem.
        /// </summary>
        private static void TintMaterial(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            foreach (string property in TintProperties)
            {
                if (material.HasProperty(property))
                {
                    material.SetColor(property, color);
                }
            }
        }

        private static readonly string[] TintProperties =
        {
            "_Color",
            "_TintColor",
            "_EmissionColor",
            "_ColorTint"
        };

        /// <summary>
        /// Prefabs de efeito do jogo são feitos para um estouro rápido: quase todos se
        /// autodestroem e as partículas não repetem. Carregar ki é sustentado, então tiramos o
        /// timer e forçamos o loop — senão o efeito some sozinho depois de um segundo.
        /// </summary>
        private static void PrepareForSustainedUse(GameObject instance)
        {
            foreach (TimedDestruction timed in instance.GetComponentsInChildren<TimedDestruction>(true))
            {
                UnityEngine.Object.Destroy(timed);
            }

            if (!SaiyaheimConfig.ChargeEffectForceLoop.Value)
            {
                return;
            }

            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                main.loop = true;
                particles.Play();
            }

            foreach (AudioSource audio in instance.GetComponentsInChildren<AudioSource>(true))
            {
                audio.loop = true;
                if (!audio.isPlaying)
                {
                    audio.Play();
                }
            }
        }
    }
}
