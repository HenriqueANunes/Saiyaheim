namespace Saiyaheim.Runes
{
    /// <summary>
    /// Algo que uma runestone pode ensinar: hoje um ataque de ki (etapa 13) ou um tier de Kaioken
    /// (etapa 14). O <see cref="RuneKnowledge"/> só conhece esta interface, e por isso os dois
    /// entram no mesmo sorteio do bioma, com a mesma chance e a mesma proteção contra azar.
    ///
    /// <b>O id divide espaço com todo o resto</b> na mesma lista de aprendidos do personagem, então
    /// tem de ser único entre ataques e tiers. Mudar depois de jogar faz o personagem esquecer.
    /// </summary>
    internal interface IRuneLesson
    {
        string Id { get; }

        string DisplayName { get; }

        /// <summary>Bioma cujas runestones ensinam isto. None tira das runestones.</summary>
        Heightmap.Biome LearnBiome { get; }

        /// <summary>Peso no sorteio entre candidatos do mesmo bioma. 0 nunca sai.</summary>
        float LearnWeight { get; }

        /// <summary>
        /// Pode entrar no sorteio para este personagem, fora a regra de bioma e de já saber? É a
        /// ordem estrita dos tiers de Kaioken: o x3 só é candidato com o x2 aprendido.
        /// </summary>
        bool PrerequisiteMet(Player player);
    }
}
