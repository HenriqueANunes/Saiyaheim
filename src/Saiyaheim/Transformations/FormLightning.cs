using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Saiyaheim.Transformations
{
    /// <summary>
    /// Os raios que estalam em volta do corpo enquanto uma forma alta está ativa.
    ///
    /// <b>É o primeiro efeito do mod que dura o estado inteiro, e não o instante.</b> Até aqui a
    /// regra era o contrário: grito e estouro acontecem ao subir e acabam, e o único sinal
    /// permanente era o cabelo. A regra veio do playtest de 2026-08-02, em que
    /// <c>fx_DvergerMage_Support_start</c> em loop leu como fumaça colada no personagem — prefab
    /// feito para meio segundo, forçado a repetir, não fica igual e mais longo: vira uma nuvem em
    /// regime permanente.
    ///
    /// <b>Raio escapa daquela armadilha por construção</b>, e é por isso que ele podia ser
    /// permanente onde a aura não podia. A nuvem apareceu porque as partículas <i>nunca se
    /// dissipavam</i>; aqui cada estalo nasce, vive um terço de segundo e morre. O que dura não é
    /// um objeto aceso, é a <b>repetição</b> — e intermitência é literalmente o que um raio é. O
    /// jeito errado de fazer isto seria ligar o <c>forceLoop</c> num prefab de raio, que
    /// reproduziria a fumaça com outra textura.
    ///
    /// <b>Roda para todo jogador carregado</b>, o local inclusive, chamado pelo
    /// <see cref="TransformationEffects.Observe"/> — e a forma vem do canal do <c>NetState</c>, não
    /// do <c>SEMan</c>, porque status effect não replica. Um amigo em SSJ2 do outro lado da
    /// clareira crepita na tela de quem olha sem nenhum RPC nosso.
    ///
    /// Nada aqui é objeto de rede: cada máquina instancia os próprios estalos, como todo efeito do
    /// mod. Ver <c>AttachedEffect</c>.
    /// </summary>
    internal static class FormLightning
    {
        /// <summary>
        /// Quando sai o próximo estalo de cada jogador, em <c>Time.time</c>.
        ///
        /// <b>Por jogador e não um relógio só</b> porque dois amigos em SSJ2 lado a lado com o
        /// mesmo relógio estalariam em uníssono — que lê como um efeito da cena, e não como duas
        /// pessoas instáveis. A ausência da chave é o que significa "este jogador não estava
        /// crepitando", e é ela que agenda o primeiro estalo.
        /// </summary>
        private static readonly Dictionary<Player, float> NextSpark = new Dictionary<Player, float>();

        /// <summary>
        /// Um passo do efeito neste jogador. Chamado todo frame, para todo jogador carregado.
        ///
        /// <paramref name="form"/> null (jogador na base) ou uma forma que não crepita apagam o
        /// agendamento: voltar a subir precisa recomeçar o relógio, senão quem entrasse em SSJ2 de
        /// novo dentro do intervalo estalaria no primeiro frame ou esperaria um tempo herdado da
        /// vez passada.
        /// </summary>
        internal static void Tick(Player player, Transformation form)
        {
            if (player == null)
            {
                return;
            }

            string prefab = SaiyaheimConfig.FormLightningPrefab.Value;

            if (form == null || !form.HasLightning || string.IsNullOrEmpty(prefab))
            {
                NextSpark.Remove(player);
                return;
            }

            if (!NextSpark.TryGetValue(player, out float next))
            {
                // Não estala no frame em que a forma sobe, de propósito: ali já estão o grito e o
                // estouro da transformação, e um raio no meio deles não seria visto. O primeiro
                // sai quando o estouro estiver acabando.
                NextSpark[player] = Time.time + DrawInterval();
                return;
            }

            if (Time.time < next)
            {
                return;
            }

            NextSpark[player] = Time.time + DrawInterval();

            int count = Mathf.Max(1, SaiyaheimConfig.FormLightningCount.Value);
            for (int i = 0; i < count; i++)
            {
                SpawnBolt(player, form, prefab);
            }
        }

        /// <summary>Este jogador deixou de existir: descarta o relógio dele.</summary>
        internal static void Forget(Player player)
        {
            NextSpark.Remove(player);
        }

        internal static void Reset()
        {
            NextSpark.Clear();
        }

        /// <summary>
        /// Quanto esperar até o próximo estalo, já sorteado.
        ///
        /// O sorteio não é enfeite: em compasso fixo os estalos leem como um mecanismo ligado ao
        /// personagem, não como energia que ele não controla. É a mesma razão pela qual o próprio
        /// jogo sorteia o passo do som de caminhada.
        /// </summary>
        private static float DrawInterval()
        {
            float interval = Mathf.Max(0.01f, SaiyaheimConfig.FormLightningInterval.Value);
            float jitter = Mathf.Clamp01(SaiyaheimConfig.FormLightningIntervalJitter.Value);

            return interval * Random.Range(1f - jitter, 1f + jitter);
        }

        /// <summary>
        /// Um estalo, num ponto sorteado do cilindro em volta do jogador.
        ///
        /// <b>O ponto é sorteado em disco e não em anel</b>, então os raios se adensam perto do
        /// corpo e rareiam na borda. É o que se quer de uma aura colada na pele: um anel perfeito
        /// deixaria um vazio no meio do personagem, e o efeito leria como um círculo desenhado no
        /// chão em vez de energia saindo dele.
        ///
        /// <b>A rotação também é sorteada</b>, porque o prefab instanciado sem isso sai sempre na
        /// mesma direção do personagem — dez estalos idênticos empilhados no mesmo eixo lêem como
        /// uma textura piscando, não como raios diferentes.
        /// </summary>
        private static void SpawnBolt(Player player, Transformation form, string prefab)
        {
            Vector2 disc = Random.insideUnitCircle * SaiyaheimConfig.FormLightningRadius.Value;

            float spread = Mathf.Max(0f, SaiyaheimConfig.FormLightningSpread.Value);
            float height = SaiyaheimConfig.FormLightningHeight.Value
                           + Random.Range(-spread * 0.5f, spread * 0.5f);

            GameObject bolt = Util.AttachedEffect.Spawn(
                player,
                prefab,
                form.GetLightningColor(),
                SaiyaheimConfig.FormLightningScale.Value,
                forceLoop: false,
                lightIntensity: SaiyaheimConfig.FormLightningLightIntensity.Value,
                burstDuration: SaiyaheimConfig.FormLightningDuration.Value,
                localOffset: new Vector3(disc.x, height, disc.y));

            if (bolt != null)
            {
                bolt.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
        }
    }
}
