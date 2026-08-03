using UnityEngine;

namespace Saiyaheim.Util
{
    /// <summary>
    /// Instancia um prefab de efeito do jogo preso ao jogador, tingido e preparado para durar.
    ///
    /// Nasceu no <c>KiChargeEffects</c> e virou arquivo próprio quando a transformação passou a
    /// querer a mesma coisa com outra cor. As três decisões que moram aqui — não sujar o material
    /// compartilhado, não criar ZDO, e desarmar o autodestruir dos prefabs — são sutis o bastante
    /// para que ter duas cópias significasse consertar cada bug duas vezes.
    ///
    /// Nada de asset novo: tudo são prefabs <c>fx_</c>/<c>sfx_</c> que o jogo já carrega. Ver
    /// [[Prefabs do Jogo]] no vault para a paleta levantada.
    /// </summary>
    internal static class AttachedEffect
    {
        /// <summary>
        /// Cria o efeito no jogador. Devolve null se o prefab não existir ou o nome for vazio —
        /// desligar um efeito é apagar o nome no <c>.cfg</c>, e isso não é erro.
        /// </summary>
        /// <param name="colorHex">Cor em #RRGGBB. Vazio mantém a cor original do prefab.</param>
        /// <param name="forceLoop">Ver <see cref="PrepareForSustainedUse"/>.</param>
        /// <param name="lightIntensity">Ver <see cref="ApplyLightIntensity"/>. 1 não mexe.</param>
        /// <param name="burstDuration">
        /// Segundos de vida quando <paramref name="forceLoop"/> está desligado. Ver
        /// <see cref="PrepareForBurst"/>. 0 deixa o prefab decidir sozinho.
        /// </param>
        internal static GameObject Spawn(
            Player player, string prefabName, string colorHex, float scale, bool forceLoop,
            float lightIntensity = 1f, float burstDuration = 0f)
        {
            if (player == null || string.IsNullOrEmpty(prefabName) || ZNetScene.instance == null)
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
                instance = Object.Instantiate(
                    prefab, player.transform.position, player.transform.rotation, player.transform);
            }
            finally
            {
                ZNetView.m_forceDisableInit = previousDisableInit;
            }

            if (forceLoop)
            {
                PrepareForSustainedUse(instance);
            }
            else
            {
                PrepareForBurst(instance, burstDuration);
            }

            ApplyTint(instance, colorHex);
            ApplyLightIntensity(instance, lightIntensity);

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
        /// pintaria de amarelo o efeito original para todo mundo que o usa — Dvergr, poções,
        /// qualquer coisa — até reiniciar o jogo.
        ///
        /// Só a cor base é trocada. O <c>colorOverLifetime</c> das partículas multiplica por
        /// cima, então o fade original é preservado.
        /// </summary>
        private static void ApplyTint(GameObject instance, string colorHex)
        {
            if (string.IsNullOrEmpty(colorHex))
            {
                return;
            }

            if (!ColorUtility.TryParseHtmlString(colorHex, out Color color))
            {
                SaiyaheimPlugin.Log.LogWarning(
                    $"Effect color '{colorHex}' is not a valid color. Use the #RRGGBB format.");
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
        /// Regula a **luz dinâmica** do efeito, que é coisa diferente do brilho das partículas.
        ///
        /// A distinção importa e não é óbvia na tela: o <c>Light</c> de um prefab ilumina o
        /// **terreno em volta** do jogador, e num efeito sustentado isso vira uma lanterna
        /// acompanhando o personagem — cansativo de um jeito que o mesmo efeito num estouro de
        /// meio segundo nunca é. As partículas, essas continuam visíveis: elas têm brilho próprio
        /// (shader aditivo) e não dependem do <c>Light</c> para aparecer.
        ///
        /// <b>Zero destrói o componente</b> em vez de zerar a intensidade. Luz apagada ainda custa
        /// no pipeline de render, e um efeito preso ao jogador vive minutos, não frames.
        /// </summary>
        private static void ApplyLightIntensity(GameObject instance, float intensity)
        {
            if (Mathf.Approximately(intensity, 1f))
            {
                return;
            }

            foreach (Light light in instance.GetComponentsInChildren<Light>(true))
            {
                if (intensity <= 0f)
                {
                    Object.Destroy(light);
                    continue;
                }

                light.intensity *= intensity;
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
        /// Prepara o efeito para durar como <b>estado</b>: tira o timer e força o loop.
        ///
        /// Prefabs do jogo são feitos para um estouro rápido: quase todos carregam um
        /// <c>TimedDestruction</c> e as partículas não repetem. É esta rotina que faz o
        /// carregamento de ki durar enquanto a tecla estiver pressionada.
        ///
        /// ⚠️ <b>Mas isso não transforma qualquer estouro em aura.</b> Playtest de 2026-08-02:
        /// o efeito de suporte do Dvergr em loop leu como <i>fumaça presa no personagem</i>. As
        /// partículas nunca se dissipam, então em vez de algo que nasce e morre sobra uma nuvem em
        /// regime permanente — o prefab não fica igual e mais longo, ele vira outra coisa. Loop
        /// funciona para segundos (a tecla pressionada), não para minutos (uma forma ativa).
        ///
        /// Quem quer estouro vai para <see cref="PrepareForBurst"/>.
        /// </summary>
        private static void PrepareForSustainedUse(GameObject instance)
        {
            foreach (TimedDestruction timed in instance.GetComponentsInChildren<TimedDestruction>(true))
            {
                Object.Destroy(timed);
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

        /// <summary>
        /// Prepara o efeito para ser um <b>estouro</b>: toca uma vez, apaga e some.
        ///
        /// ⚠️ <b>Não basta não forçar o loop.</b> Foi o que a primeira versão fez, e o efeito da
        /// transformação continuou aceso a forma inteira. Duas razões, e nenhuma delas é o nosso
        /// código:
        ///
        /// <list type="bullet">
        /// <item>Prefab de efeito <i>sustentado</i> — e <c>fx_DvergerMage_Support_start</c> é a
        /// abertura de um, não um estouro solto — já vem com <c>loop</c> ligado nas partículas.
        /// Deixar como veio é pedir a nuvem presa no personagem.</item>
        /// <item><c>TimedDestruction</c> só se dispara sozinho se o prefab marcou
        /// <c>m_triggerOnAwake</c>. Quando não marcou, quem instancia é que deveria chamar
        /// <c>Trigger()</c> — e nós não chamávamos. O objeto ficava pendurado no jogador até a
        /// próxima transformação passar por cima dele.</item>
        /// </list>
        ///
        /// Então aqui a duração é <b>imposta</b>, não herdada: um único <c>TimedDestruction</c> na
        /// raiz com o tempo do config. O de dentro do prefab é descartado justamente para não haver
        /// dois relógios discordando sobre quando o efeito acaba.
        ///
        /// <paramref name="duration"/> zero devolve a decisão ao prefab — escotilha para quando
        /// alguém apontar a chave para um prefab que já se comporta bem sozinho.
        /// </summary>
        private static void PrepareForBurst(GameObject instance, float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            // Uma partícula em loop não para nunca; sem loop ela emite o ciclo dela e morre. O
            // Stop() aqui seria pior: cortaria o estouro antes de ele acontecer.
            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                main.loop = false;
            }

            foreach (AudioSource audio in instance.GetComponentsInChildren<AudioSource>(true))
            {
                audio.loop = false;
            }

            foreach (TimedDestruction timed in instance.GetComponentsInChildren<TimedDestruction>(true))
            {
                Object.Destroy(timed);
            }

            // O TimedDestruction do jogo destrói o GameObject inteiro quando não acha ZNetView
            // válido — e o nosso não tem, porque o m_forceDisableInit do Spawn matou o dele. É
            // exatamente o caminho que queremos: efeito local, morte local.
            instance.AddComponent<TimedDestruction>().Trigger(duration);
        }
    }
}
