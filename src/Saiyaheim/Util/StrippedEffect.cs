using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saiyaheim.Util
{
    /// <summary>
    /// Devolve um prefab de efeito <b>sem os emissores cujo nome bate num filtro</b>, para poder
    /// ficar com metade de um efeito do jogo.
    ///
    /// <b>Por que existe.</b> Trocar o <c>ImpactEffect</c> inteiro é a escolha grossa: ou fica tudo
    /// que o prefab traz, ou não fica nada. O caso real é mais fino — o estouro do
    /// <c>GoblinShaman_projectile_fireball</c> tem clarão bom, som bom e uma nuvem de fumaça que
    /// não combina com um tiro de energia. As três coisas são <b>filhos do mesmo prefab</b>, então
    /// nenhuma troca de nome de efeito separa uma da outra.
    ///
    /// ⚠️ <b>Nada disto pode ser feito no prefab do jogo.</b> Apagar a fumaça do
    /// <c>fx_shaman_fireball_expl</c> apagaria também a do xamã goblin, do Dvergr e de todo mundo
    /// que compartilha o efeito, até reiniciar o jogo — a mesma armadilha do <c>sharedMaterial</c>
    /// em <see cref="AttachedEffect"/>. O que se faz é um <b>clone</b>, e é o clone que perde a
    /// fumaça.
    ///
    /// <b>O truque do pai inativo.</b> O clone precisa ser um template: existir, mas não tocar. Um
    /// <c>GameObject</c> só roda <c>Awake</c> e emite partícula quando está ativo <i>na
    /// hierarquia</i>, então basta pendurá-lo sob um pai desativado. O <c>activeSelf</c> dele
    /// continua <c>true</c>, e é isso que importa na hora em que o <c>EffectList.Create</c> fizer
    /// <c>Instantiate</c>: a cópia nasce sem pai, ativa, e toca normalmente. É o padrão de "prefab
    /// montado em runtime" do Unity, e o motivo de o template não precisar de nenhum
    /// <c>SetActive</c> no caminho.
    /// </summary>
    internal static class StrippedEffect
    {
        /// <summary>Pai desativado que segura os templates. Ver o truque na doc da classe.</summary>
        private static GameObject _templates;

        /// <summary>
        /// Um template por (prefab × filtro). Sem cache, cada tiro clonaria o efeito de novo — e o
        /// clone é feito uma vez e usado para sempre.
        /// </summary>
        private static readonly Dictionary<string, GameObject> Templates =
            new Dictionary<string, GameObject>();

        /// <summary>
        /// Quebra a chave de config em nomes. Vazio devolve <c>null</c>, que é o "não filtra nada"
        /// que todo mundo aqui testa.
        /// </summary>
        internal static string[] ParseFilter(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            string[] parts = raw.Split(',');
            List<string> names = new List<string>(parts.Length);
            foreach (string part in parts)
            {
                string name = part.Trim();
                if (name.Length > 0)
                {
                    names.Add(name);
                }
            }

            return names.Count == 0 ? null : names.ToArray();
        }

        /// <summary>
        /// Casa por <b>nome inteiro</b>, sem ligar para maiúscula.
        ///
        /// ⚠️ <b>Era por pedaço do nome, e estava errado.</b> O caso que derrubou a ideia é real:
        /// o estouro do xamã goblin é o efeito <c>fx_shaman_fireball_expl</c> com os emissores
        /// <c>smoke</c>, <c>fire</c> e <c>shockwave</c> dentro. Filtrar <c>fire</c> por pedaço
        /// casa com <c>fx_shaman_<b>fire</b>ball_expl</c> — o nome do efeito inteiro — e o tiro
        /// perde o estouro em vez de perder uma chama. Nome de efeito e nome de emissor vivem no
        /// mesmo espaço de nomes aqui, e num prefab de fogo eles compartilham as mesmas palavras.
        ///
        /// O preço é que um nome digitado errado não faz nada em silêncio. Barato: os nomes saem
        /// do log, prontos para copiar, e um filtro que não casa aparece lá como emissor que
        /// continua na lista.
        /// </summary>
        internal static bool Matches(string name, string[] filter)
        {
            if (filter == null || string.IsNullOrEmpty(name))
            {
                return false;
            }

            foreach (string entry in filter)
            {
                if (string.Equals(name, entry, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// O prefab pronto para entrar no impacto: sem os emissores filtrados e na cor pedida.
        ///
        /// Devolve o <b>próprio</b> <paramref name="source"/> quando não há nada a fazer — sem
        /// filtro e sem cor, ou filtro que não casa com emissor nenhum — para que o caso comum não
        /// pague por um clone.
        ///
        /// <b>A cor entra aqui, e não na instância do impacto</b>, por um motivo simples: quem
        /// instancia o efeito do impacto é o <c>EffectList.Create</c> do jogo, lá dentro do
        /// <c>Projectile</c>, e nós nunca vemos o objeto que nasce. O que se pinta é o molde; o
        /// que o jogo instanciar já sai pintado.
        /// </summary>
        internal static GameObject Prepare(
            GameObject source, string[] filter, string tintHex, bool tintLightsOnly, string logContext)
        {
            bool hasTint = !string.IsNullOrEmpty(tintHex);
            if (source == null || (filter == null && !hasTint))
            {
                return source;
            }

            // A cor faz parte da chave: sem isso, mudar o ImpactColor no .cfg com o jogo aberto
            // devolveria o template já montado na cor velha, e a chave pareceria não funcionar.
            string key = source.name +
                         "|" + (filter == null ? string.Empty : string.Join(",", filter)) +
                         "|" + tintHex + "|" + tintLightsOnly;

            if (Templates.TryGetValue(key, out GameObject cached) && cached != null)
            {
                return cached;
            }

            if (!hasTint && !HasMatch(source, filter))
            {
                // Guardado assim mesmo: a resposta "não há nada para tirar" vale para sempre, e
                // sem isto todo tiro varreria a hierarquia de novo para chegar nela.
                Templates[key] = source;
                return source;
            }

            GameObject template = Build(source, filter, tintHex, tintLightsOnly, logContext);
            Templates[key] = template;
            return template;
        }

        private static bool HasMatch(GameObject source, string[] filter)
        {
            foreach (ParticleSystem particles in source.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (Matches(particles.gameObject.name, filter))
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject Build(
            GameObject source, string[] filter, string tintHex, bool tintLightsOnly, string logContext)
        {
            if (_templates == null)
            {
                _templates = new GameObject("SaiyaheimEffectTemplates");
                _templates.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_templates);
            }

            GameObject template = UnityEngine.Object.Instantiate(source, _templates.transform);

            // Sem isto o nome vira "fx_shaman_fireball_expl(Clone)" e reaparece assim no log da
            // próxima sessão, como se fosse outro efeito.
            template.name = source.name;

            List<string> removed = new List<string>();
            foreach (ParticleSystem particles in template.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particles == null || !Matches(particles.gameObject.name, filter))
                {
                    continue;
                }

                removed.Add(particles.gameObject.name);

                if (particles.gameObject == template)
                {
                    // O emissor é o objeto raiz: destruí-lo levaria junto o som e o clarão que são
                    // filhos dele. Tira-se só a peça que emite.
                    UnityEngine.Object.DestroyImmediate(particles.GetComponent<ParticleSystemRenderer>());
                    UnityEngine.Object.DestroyImmediate(particles);
                    continue;
                }

                // Immediate, e não Destroy: o Destroy comum só acontece no fim do frame, e um
                // template que vai ser instanciado antes disso nasceria com a fumaça ainda dentro.
                UnityEngine.Object.DestroyImmediate(particles.gameObject);
            }

            // Depois de remover, e nao antes: pintar o que vai ser destruido e trabalho jogado fora,
            // e o material instanciado do emissor apagado sobraria na memoria sem dono.
            AttachedEffect.ApplyTint(template, tintHex, tintLightsOnly);

            string strippedNote = removed.Count == 0
                ? "nothing stripped"
                : $"stripped {removed.Count} emitter(s): {string.Join(", ", removed.ToArray())}";

            string tintNote = string.IsNullOrEmpty(tintHex)
                ? "no tint"
                : $"tinted {tintHex}" + (tintLightsOnly ? " (lights only)" : string.Empty);

            SaiyaheimPlugin.LogVerbose(
                $"{logContext}: template of '{source.name}' — {strippedNote}; {tintNote}.");

            return template;
        }
    }
}
