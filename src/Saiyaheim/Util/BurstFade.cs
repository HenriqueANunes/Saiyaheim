using System.Collections.Generic;
using UnityEngine;

namespace Saiyaheim.Util
{
    /// <summary>
    /// Fim suave de um estouro: nos últimos <see cref="FadeDuration"/> segundos o efeito encolhe
    /// e fica transparente, e só então é destruído.
    ///
    /// Existe por causa do estouro da transformação com a aura do Hovl Studio (2026-09-26). A
    /// partícula principal dela vive de 1 a 5 segundos, então o corte do <c>TimedDestruction</c>
    /// aos 2 segundos apagava uma aura ainda cheia, de uma vez.
    ///
    /// <b>A transparência é escrita em cada partícula viva.</b> O shader da aura não tem
    /// propriedade de cor no material; a cor chega pela cor de vértice da partícula. Mexer só na
    /// <c>startColor</c> valeria para as que ainda vão nascer, e o estouro já parou de emitir.
    /// O alpha original de cada partícula fica guardado pela <c>randomSeed</c> dela, para a
    /// conta ser sempre "original × fator" e não acumular arredondamento de byte frame a frame.
    ///
    /// <b>Encolher funciona porque a aura simula em espaço local com <c>Scaling Mode:
    /// Hierarchy</c></b>: a escala da raiz desce para partícula já viva. Efeito do jogo com
    /// <c>Local</c> ou em espaço de mundo só apaga, sem encolher — ver <see cref="EffectScale"/>.
    /// </summary>
    internal sealed class BurstFade : MonoBehaviour
    {
        /// <summary>Segundos de vida do efeito, contando o fade.</summary>
        internal float Duration;

        /// <summary>Segundos finais em que o efeito encolhe e some. Cortado em <see cref="Duration"/>.</summary>
        internal float FadeDuration;

        /// <summary>Fração da escala em que o efeito termina. 1 só apaga, sem encolher.</summary>
        internal float EndScale;

        private float _elapsed;
        private Vector3 _baseScale;
        private ParticleSystem[] _systems;
        private Light[] _lights;
        private float[] _lightBase;
        private ParticleSystem.Particle[] _buffer = new ParticleSystem.Particle[256];
        private readonly Dictionary<uint, byte> _originalAlpha = new Dictionary<uint, byte>();

        /// <summary>
        /// No <c>Start</c> e não no <c>Awake</c>: o componente é adicionado dentro do
        /// <see cref="AttachedEffect.Spawn"/>, antes de ele aplicar a escala. No primeiro frame a
        /// escala já é a final.
        /// </summary>
        private void Start()
        {
            _baseScale = transform.localScale;
            _systems = GetComponentsInChildren<ParticleSystem>(true);
            _lights = GetComponentsInChildren<Light>(true);
            _lightBase = new float[_lights.Length];
            for (int i = 0; i < _lights.Length; i++)
            {
                _lightBase[i] = _lights[i].intensity;
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= Duration)
            {
                Destroy(gameObject);
                return;
            }

            float fade = Mathf.Clamp(FadeDuration, 0f, Duration);
            float fadeStart = Duration - fade;
            if (fade <= 0f || _elapsed < fadeStart)
            {
                return;
            }

            float t = Mathf.Clamp01((_elapsed - fadeStart) / fade);
            float remaining = 1f - t;

            transform.localScale = _baseScale * Mathf.Lerp(1f, EndScale, t);

            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] != null)
                {
                    _lights[i].intensity = _lightBase[i] * remaining;
                }
            }

            foreach (ParticleSystem particles in _systems)
            {
                if (particles != null)
                {
                    FadeParticles(particles, remaining);
                }
            }
        }

        private void FadeParticles(ParticleSystem particles, float factor)
        {
            int capacity = particles.main.maxParticles;
            if (_buffer.Length < capacity)
            {
                _buffer = new ParticleSystem.Particle[capacity];
            }

            int count = particles.GetParticles(_buffer);
            if (count == 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                uint seed = _buffer[i].randomSeed;
                Color32 color = _buffer[i].startColor;

                if (!_originalAlpha.TryGetValue(seed, out byte alpha))
                {
                    alpha = color.a;
                    _originalAlpha[seed] = alpha;
                }

                color.a = (byte)Mathf.RoundToInt(alpha * factor);
                _buffer[i].startColor = color;
            }

            particles.SetParticles(_buffer, count);
        }
    }
}
