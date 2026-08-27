using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Saiyaheim.Transformations
{
    /// <summary>
    /// A luz que o corpo transformado joga no terreno em volta.
    ///
    /// <b>É o segundo efeito do mod que dura o estado inteiro</b>, depois do
    /// <see cref="FormLightning"/> — e o primeiro que não é feito de partícula nenhuma. Aqui não
    /// há prefab: é um <c>Light</c> pontual pendurado no transform do jogador, e é só isso.
    ///
    /// <b>Por que uma luz nua e não um prefab de aura.</b> Prefab do jogo forçado a durar já foi
    /// tentado e recusado no playtest de 2026-08-02 — <c>fx_DvergerMage_Support_start</c> em loop
    /// leu como fumaça colada no personagem, e é dessa cicatriz que vive o comentário do
    /// <c>AttachedEffect.PrepareForSustainedUse</c>. O que se quer aqui é justamente o que sobrou
    /// de fora daquele fracasso: um prefab é partícula <i>e</i> luz, a partícula é que não
    /// sobrevive a ficar acesa, e a luz sozinha não tem esse defeito — ela não nasce nem morre,
    /// ela só ilumina. Sem partícula não há nuvem a se acumular.
    ///
    /// ⚠️ <b>A doc do <c>AttachedEffect.ApplyLightIntensity</c> avisa que luz presa ao jogador por
    /// minutos "vira uma lanterna acompanhando o personagem".</b> Isso continua verdade e não é
    /// contradição: lá o aviso é sobre herdar sem querer a luz de um prefab de <i>estouro</i>,
    /// calibrada para meio segundo de clarão. Aqui a lanterna é o pedido, e o que a torna
    /// aceitável é a regulagem ser própria e discreta — alcance de poucos metros e intensidade
    /// bem abaixo de uma tocha. Se ela incomodar, <c>FormGlowIntensity</c> em 0 apaga tudo.
    ///
    /// <b>Roda para todo jogador carregado</b>, o local inclusive, chamada pelo
    /// <see cref="TransformationEffects.Observe"/>, e a forma vem do canal do <c>NetState</c> —
    /// então um amigo em SSJ do outro lado da clareira ilumina o chão dele na tela de quem olha,
    /// sem nenhum RPC nosso.
    /// </summary>
    internal static class FormGlow
    {
        /// <summary>
        /// A luz de um jogador e o que ela precisa lembrar entre um frame e o outro.
        /// </summary>
        private sealed class Glow
        {
            internal Light Light;

            /// <summary>
            /// Quanto da luz está acesa, 0–1. É por onde o fade acontece: o alvo é sempre 0 ou 1
            /// e o que varia é o tempo de chegar lá.
            /// </summary>
            internal float Level;

            /// <summary>
            /// A intensidade cheia da última forma vista. Guardada porque o fade de saída começa
            /// no frame em que a forma já é null — sem isto não haveria de que valor descer.
            /// </summary>
            internal float Peak;

            /// <summary>A cor da última forma vista, pelo mesmo motivo do <see cref="Peak"/>.</summary>
            internal Color Color = Color.white;

            /// <summary>
            /// Deslocamento do pulso, sorteado uma vez por jogador. Mesma razão do jitter do
            /// <see cref="FormLightning"/>: dois amigos transformados lado a lado pulsando no
            /// mesmo compasso leem como um efeito da cena, não como duas pessoas cheias de energia.
            /// </summary>
            internal float Phase;
        }

        private static readonly Dictionary<Player, Glow> Glows = new Dictionary<Player, Glow>();

        /// <summary>
        /// Um passo do efeito neste jogador. Chamado todo frame, para todo jogador carregado.
        ///
        /// <paramref name="form"/> null (jogador na base) é o caso comum e o mais barato: quem não
        /// tem luz acesa sai na primeira linha, sem alocar nada.
        /// </summary>
        internal static void Tick(Player player, Transformation form)
        {
            if (player == null)
            {
                return;
            }

            float target = TargetIntensity(form);

            if (!Glows.TryGetValue(player, out Glow glow))
            {
                if (target <= 0f)
                {
                    return;
                }

                glow = Create(player);
                Glows[player] = glow;
            }

            // O operador == da Unity: o objeto morre junto com o jogador (morte, descarregamento),
            // e o que sobra aqui é só a entrada do dicionário.
            if (glow.Light == null)
            {
                Glows.Remove(player);
                return;
            }

            if (target > 0f)
            {
                glow.Peak = target;
                glow.Color = ResolveColor(form, glow.Color);
            }

            float fade = Mathf.Max(0f, SaiyaheimConfig.FormGlowFade.Value);
            float goal = target > 0f ? 1f : 0f;

            glow.Level = fade > 0f
                ? Mathf.MoveTowards(glow.Level, goal, Time.deltaTime / fade)
                : goal;

            if (glow.Level <= 0f)
            {
                // Apagada e sem forma para reacender: o componente sai em vez de ficar em zero.
                // Luz apagada ainda custa no pipeline de render, e um jogador na base pode passar
                // a sessão inteira assim.
                Object.Destroy(glow.Light.gameObject);
                Glows.Remove(player);
                return;
            }

            Apply(glow);
        }

        /// <summary>Este jogador deixou de existir: apaga a luz dele e esquece.</summary>
        internal static void Forget(Player player)
        {
            if (player == null || !Glows.TryGetValue(player, out Glow glow))
            {
                return;
            }

            if (glow.Light != null)
            {
                Object.Destroy(glow.Light.gameObject);
            }

            Glows.Remove(player);
        }

        /// <summary>
        /// Apaga todas as luzes vivas.
        ///
        /// Diferente dos dicionários do <see cref="FormLightning"/>, aqui limpar não basta: os
        /// estalos morrem sozinhos em um terço de segundo, e a luz não morre nunca. Perder a
        /// referência sem destruir deixaria o jogador aceso para sempre — que é exatamente o que
        /// aconteceria no caminho de erro do <c>TransformationEffects.Run</c>, o único que chama
        /// isto com jogadores ainda vivos na cena.
        /// </summary>
        internal static void Reset()
        {
            foreach (Glow glow in Glows.Values)
            {
                if (glow.Light != null)
                {
                    Object.Destroy(glow.Light.gameObject);
                }
            }

            Glows.Clear();
        }

        /// <summary>
        /// A intensidade cheia desta forma: a regulagem compartilhada vezes o multiplicador da
        /// forma. Zero em qualquer um dos dois desliga — o global apaga todo mundo, o da forma
        /// apaga só aquele degrau.
        /// </summary>
        private static float TargetIntensity(Transformation form)
        {
            if (form == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, SaiyaheimConfig.FormGlowIntensity.Value * form.GetGlowIntensity());
        }

        /// <summary>
        /// A cor da forma, ou a última válida se a chave estiver vazia ou inválida.
        ///
        /// Vazio aqui não significa "não tinja", como significa no <c>AttachedEffect</c>: uma luz
        /// sem cor é uma luz branca, e branco não é a ausência de escolha — é uma escolha ruim por
        /// cima de uma aura dourada. Quem quer apagar o brilho zera a intensidade.
        /// </summary>
        private static Color ResolveColor(Transformation form, Color fallback)
        {
            string raw = form.GetGlowColor();

            if (string.IsNullOrEmpty(raw) || !ColorUtility.TryParseHtmlString(raw, out Color color))
            {
                return fallback;
            }

            return color;
        }

        private static Glow Create(Player player)
        {
            GameObject holder = new GameObject("SaiyaheimFormGlow");

            // worldPositionStays: false — o que queremos é o espaço local do jogador, para a luz
            // acompanhar o corpo ao andar e ao pular. Mesma razão do localOffset do AttachedEffect.
            holder.transform.SetParent(player.transform, false);

            Light light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = 0f;
            light.shadows = LightShadows.None;

            // GI em tempo real não existe no Valheim; deixar em 1 só daria trabalho ao pipeline
            // para nada.
            light.bounceIntensity = 0f;

            return new Glow { Light = light, Phase = Random.value };
        }

        private static void Apply(Glow glow)
        {
            Light light = glow.Light;

            light.color = glow.Color;
            light.range = Mathf.Max(0.1f, SaiyaheimConfig.FormGlowRange.Value);
            light.intensity = glow.Level * glow.Peak * Pulse(glow);

            // Sombra de luz pontual custa seis mapas de sombra por quadro, e a luz vive minutos.
            // Por isso a chave existe, e por isso ela nasce desligada.
            light.shadows = SaiyaheimConfig.FormGlowShadows.Value
                ? LightShadows.Soft
                : LightShadows.None;

            light.transform.localPosition = new Vector3(0f, SaiyaheimConfig.FormGlowHeight.Value, 0f);
        }

        /// <summary>
        /// O multiplicador de respiração deste frame, centrado em 1 — a intensidade configurada é
        /// a <b>média</b>, não o teto.
        ///
        /// Sem ele a luz é uma lâmpada: constante lê como cenário, e o que se quer é energia que o
        /// personagem mal contém. Amplitude 0 devolve a lâmpada, para quem preferir.
        /// </summary>
        private static float Pulse(Glow glow)
        {
            float amount = Mathf.Clamp01(SaiyaheimConfig.FormGlowPulseAmount.Value);
            if (amount <= 0f)
            {
                return 1f;
            }

            float speed = Mathf.Max(0f, SaiyaheimConfig.FormGlowPulseSpeed.Value);
            float wave = Mathf.Sin((Time.time * speed + glow.Phase) * 2f * Mathf.PI);

            return 1f + wave * amount;
        }
    }
}
