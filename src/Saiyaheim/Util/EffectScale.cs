using UnityEngine;

namespace Saiyaheim.Util
{
    /// <summary>
    /// Redimensiona um efeito do jogo <b>de verdade</b>.
    ///
    /// <b>O problema.</b> Escrever no <c>transform.localScale</c> de um prefab de efeito não muda
    /// nada na tela, e o mod fez exatamente isso por semanas — o <c>ProjectileScale</c>, o
    /// <c>ChargeEffectScale</c> e o <c>ChargeMinScale</c> saíam do <c>.cfg</c>, chegavam ao
    /// transform e não apareciam. Não é bug do mod nem da config: é como o Unity desenha
    /// partículas.
    ///
    /// <list type="number">
    /// <item><b>Partícula ignora a escala do pai.</b> O <c>ParticleSystem.main.scalingMode</c> vem
    /// como <c>Local</c> por padrão, e <c>Local</c> quer dizer "use só a escala do <i>meu</i>
    /// transform". Como os emissores de um efeito do jogo são <b>filhos</b> do prefab — é o que a
    /// lista do <c>LogImpactEffects</c> mostra: <c>smoke</c>, <c>fire</c>, <c>shockwave</c> —,
    /// escalar a raiz não os alcança. Só <c>Hierarchy</c> faz a escala descer a árvore.</item>
    /// <item><b>Rastro e linha têm largura em unidades de mundo.</b> O <c>widthMultiplier</c> de um
    /// <c>TrailRenderer</c> ou <c>LineRenderer</c> não é afetado por escala nenhuma. Um feixe
    /// desenhado como rastro fica exatamente da mesma grossura por mais que se escale o objeto — e
    /// esse é o caso do <c>projectile_beam</c> ser um feixe.</item>
    /// <item><b>Alcance de luz também não escala.</b> Um efeito duas vezes maior com a mesma
    /// bolha de luz denuncia o truque.</item>
    /// </list>
    ///
    /// <b>Por que isto não entrou no <see cref="AttachedEffect"/>.</b> Ele já escala, do jeito
    /// ingênuo, e a aura das formas e o carregamento de ki foram <b>calibrados na tela por cima
    /// desse jeito ingênuo</b> — o "2x do efeito de suporte do Dvergr" de [[Decisões Tomadas]] é um
    /// número achado assim. Consertar a escala lá dentro mudaria em silêncio o tamanho de todo
    /// efeito já ajustado do mod, que é uma regressão visual que ninguém pediu. Quem quer a escala
    /// que funciona chama isto de propósito.
    ///
    /// <b>As medidas originais ficam guardadas no próprio objeto</b>, num
    /// <see cref="ScaledEffect"/>. Sem isso, crescer um efeito por frame — que é o que a bola do
    /// Kamehameha faz — multiplicaria em cima do resultado do frame anterior e o tamanho explodiria
    /// em exponencial.
    /// </summary>
    internal static class EffectScale
    {
        /// <summary>
        /// Põe <paramref name="instance"/> em <paramref name="factor"/> vezes o tamanho com que o
        /// prefab veio. Idempotente: chamar todo frame com o mesmo fator dá sempre o mesmo tamanho.
        /// </summary>
        internal static void Apply(GameObject instance, float factor)
        {
            if (instance == null)
            {
                return;
            }

            ScaledEffect state = Capture(instance);
            Vector3 target = state.BaseScale * factor;

            // O ZNetView primeiro, e escrevendo no transform por ele: o SetLocalScale só publica na
            // ZDO quando o valor MUDA, então escrever no transform antes o transformaria num no-op
            // e o projétil sairia do tamanho certo aqui e do tamanho do prefab na tela dos amigos.
            if (state.NetView != null && state.NetView.m_syncInitialScale)
            {
                state.NetView.SetLocalScale(target);
            }
            else
            {
                instance.transform.localScale = target;
            }

            for (int i = 0; i < state.Trails.Length; i++)
            {
                if (state.Trails[i] != null)
                {
                    state.Trails[i].widthMultiplier = state.TrailWidths[i] * factor;
                }
            }

            for (int i = 0; i < state.Lines.Length; i++)
            {
                if (state.Lines[i] != null)
                {
                    state.Lines[i].widthMultiplier = state.LineWidths[i] * factor;
                }
            }

            for (int i = 0; i < state.Lights.Length; i++)
            {
                if (state.Lights[i] != null)
                {
                    state.Lights[i].range = state.LightRanges[i] * factor;
                }
            }
        }

        /// <summary>
        /// Mede o efeito uma vez e deixa a medida guardada nele, já com as partículas convertidas
        /// para <c>Hierarchy</c>.
        ///
        /// A conversão acontece aqui, no caminho da medição, e não no do redimensionamento, porque
        /// ela é uma mudança de <i>estado</i> do sistema de partículas e refazê-la a cada frame
        /// seria trabalho por nada.
        /// </summary>
        private static ScaledEffect Capture(GameObject instance)
        {
            ScaledEffect existing = instance.GetComponent<ScaledEffect>();
            if (existing != null)
            {
                return existing;
            }

            ScaledEffect state = instance.AddComponent<ScaledEffect>();

            state.BaseScale = instance.transform.localScale;
            state.NetView = instance.GetComponent<ZNetView>();

            // true: inclui os desligados. Um emissor que só acende no impacto ainda precisa nascer
            // no tamanho certo, e depois de aceso é tarde para converter.
            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in particles)
            {
                // O MainModule é struct mas escreve através no sistema — é a API que a Unity dá.
                ParticleSystem.MainModule main = particle.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            state.Trails = instance.GetComponentsInChildren<TrailRenderer>(true);
            state.TrailWidths = new float[state.Trails.Length];
            for (int i = 0; i < state.Trails.Length; i++)
            {
                state.TrailWidths[i] = state.Trails[i].widthMultiplier;
            }

            state.Lines = instance.GetComponentsInChildren<LineRenderer>(true);
            state.LineWidths = new float[state.Lines.Length];
            for (int i = 0; i < state.Lines.Length; i++)
            {
                state.LineWidths[i] = state.Lines[i].widthMultiplier;
            }

            state.Lights = instance.GetComponentsInChildren<Light>(true);
            state.LightRanges = new float[state.Lights.Length];
            for (int i = 0; i < state.Lights.Length; i++)
            {
                state.LightRanges[i] = state.Lights[i].range;
            }

            // Sem isto, "a escala não faz nada" volta a ser um diagnóstico de olhar para a tela.
            // Com a lista, dá para saber se o prefab sequer tem a peça que a escala deveria mexer.
            SaiyaheimPlugin.LogVerbose(
                $"Effect scale on '{instance.name}': base {state.BaseScale.x:0.##}, " +
                $"{particles.Length} particle system(s) forced to Hierarchy, " +
                $"{state.Trails.Length} trail(s), {state.Lines.Length} line(s), " +
                $"{state.Lights.Length} light(s).");

            return state;
        }
    }

    /// <summary>
    /// As medidas originais de um efeito, penduradas no próprio objeto.
    ///
    /// <b>Componente e não dicionário</b> por uma razão e não por gosto: a vida disto é exatamente
    /// a vida do <c>GameObject</c>. Um dicionário estático sobreviveria ao objeto destruído e
    /// precisaria de uma varredura para não vazar — que é a manutenção que o <c>PoseDriver</c> e o
    /// <c>RemoteEffects</c> pagam porque lá não há objeto onde pendurar.
    ///
    /// Puramente local: nasce depois do <c>Instantiate</c>, não tem <c>ZNetView</c> e não replica.
    /// </summary>
    internal sealed class ScaledEffect : MonoBehaviour
    {
        internal Vector3 BaseScale;
        internal ZNetView NetView;
        internal TrailRenderer[] Trails;
        internal float[] TrailWidths;
        internal LineRenderer[] Lines;
        internal float[] LineWidths;
        internal Light[] Lights;
        internal float[] LightRanges;
    }
}
