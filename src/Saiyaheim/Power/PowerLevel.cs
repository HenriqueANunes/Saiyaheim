using Saiyaheim.Ki;
using UnityEngine;

namespace Saiyaheim.Power
{
    /// <summary>
    /// O power level: stat derivado que alimenta o dano do soco e a armadura.
    ///
    /// <b>São duas fórmulas, porque os dois caminhos de progressão são disjuntos.</b>
    ///
    /// <code>
    /// ki desligado: poder = k1*HP + k2*dano_arma + k3*armadura
    /// ki ligado:    poder = k1*HP + k4*nivel_battle_power
    /// combate:      (o de cima + termo de fim de jogo) * multiplicador da forma ativa
    /// </code>
    ///
    /// Arma e armadura não sobrevivem ao modo ki:
    /// <list type="bullet">
    /// <item>arma dá <b>zero</b> — o jogador soca, não tem nada equipado; e o dano do soco vem do
    /// power level, então incluí-lo seria contar o mesmo número duas vezes;</item>
    /// <item>armadura vira <b>laço de realimentação</b>, porque passou a ser derivada do poder:
    /// <c>poder → armadura → poder</c>. Não é escolha de design, o número diverge.</item>
    /// </list>
    ///
    /// Sobra o HP, e ele fica de propósito: comida é o único eixo de progressão do jogo base que
    /// continua valendo para quem usa ki.
    ///
    /// <b>⚠️ São dois números de poder, e a diferença importa.</b> O
    /// <see cref="GetLateGameBonus"/> é um termo que só acorda perto do nível 100, e ele
    /// <b>não</b> vale para todo consumidor:
    ///
    /// <list type="table">
    /// <item><term><see cref="GetCombatRaw"/> (com o termo)</term><description>dano do soco,
    /// armadura, block power e o número exibido</description></item>
    /// <item><term><see cref="GetRaw"/> (linear, sem o termo)</term><description>velocidade de
    /// voo, teto de ki, regeneração e carga de ki</description></item>
    /// </list>
    ///
    /// <b>O multiplicador da transformação não segue a mesma divisão</b>, e a diferença é
    /// deliberada (2026-08-02): ele entra no poder de combate <b>e</b> na velocidade de voo, mas
    /// fica fora do teto de ki, da regeneração e da carga. Se a barra crescesse ao transformar,
    /// ela daria um pulo na tela; e uma regeneração escalada pela forma pagaria parte do próprio
    /// dreno, que é a única coisa que a forma custa. A velocidade entra porque tem teto duro
    /// (<c>FlightMaxSpeed</c>) e nada quebra ao encostar nele.
    ///
    /// A separação é decisão de design de 2026-08-01, não detalhe de implementação. Um poder que
    /// acelera no fim serve para o jogador <b>bater e aguentar</b> mais; deixá-lo também inflar a
    /// barra de ki e a velocidade quebraria coisas já calibradas — a velocidade encosta no teto de
    /// engine (<c>FlightMaxSpeed</c>) e o teto de ki cresceria junto com a regeneração, mantendo o
    /// segundos-para-encher igual mas inchando o número na tela sem significado. O que o fim de
    /// jogo compra no voo é <b>eficiência</b>, via <c>FlightKiPowerReduction</c>, não velocidade.
    /// </summary>
    internal static class PowerLevel
    {
        /// <summary>
        /// Power level bruto <b>linear</b>: sem o termo de fim de jogo. É a fórmula original do mod,
        /// intocada, e continua sendo a que alimenta voo e ki.
        /// </summary>
        internal static float GetRaw(Player player)
        {
            if (player == null)
            {
                return 0f;
            }

            return KiManager.IsEnabled ? GetKiRaw(player) : GetVanillaRaw(player);
        }

        /// <summary>
        /// Power level de <b>combate</b>: o linear mais o termo de fim de jogo. Alimenta dano do
        /// soco, armadura, block power e o número exibido. Ver a nota na doc da classe.
        /// </summary>
        internal static float GetCombatRaw(Player player)
        {
            if (player == null)
            {
                return 0f;
            }

            return KiManager.IsEnabled ? GetKiCombatRaw(player) : GetVanillaRaw(player);
        }

        /// <summary>
        /// O termo de fim de jogo, sozinho.
        ///
        /// <b>Por que somar um termo em vez de pôr um expoente no que já existe.</b> Um expoente
        /// sobre a parcela da skill apenas <b>redistribui</b> um total fixo: para render mais no
        /// fim, ele tira do meio, e o mid-game fica mais fraco do que já é. Somando, o
        /// <c>k4 × nível</c> de hoje continua exatamente como está — early e mid game intocados —
        /// e o termo novo só pesa onde o grind aperta.
        ///
        /// <b>O problema que ele resolve.</b> Subir do 99 para o 100 custa mil vezes o que custa
        /// subir do 0 para o 1 (o <c>GetNextLevelRequirement</c> do Valheim é
        /// <c>(nível+1)^1.5</c>), mas o poder subia sempre <c>k4</c> por nível. O nível caro rendia
        /// igual ao barato.
        ///
        /// Normalizado no nível 100: o termo entrega exatamente <c>k5 × 100</c> no topo,
        /// independentemente do expoente. O expoente decide só <b>quão tarde</b> ele acorda —
        /// com <c>p = 5</c>, no nível 50 ele vale 3% do que vale no 100.
        ///
        /// Com <c>k5 = 0</c> a fórmula inteira volta a ser a de antes, o que torna a mudança
        /// reversível por config, sem recompilar.
        /// </summary>
        internal static float GetLateGameBonus(Player player)
        {
            if (player == null || !KiManager.IsEnabled)
            {
                return 0f;
            }

            float k5 = SaiyaheimConfig.PowerK5LateGame.Value;
            if (k5 <= 0f)
            {
                return 0f;
            }

            float normalized = PowerSkill.GetLevel(player) / PowerSkill.MaxLevel;

            return k5 * PowerSkill.MaxLevel
                   * Mathf.Pow(normalized, SaiyaheimConfig.PowerLateGameExponent.Value);
        }

        /// <summary>
        /// Fórmula do ki ligado. <b>Nunca</b> pode tocar em <c>GetBodyArmor()</c>: é ela que
        /// alimenta a armadura, e ler a armadura aqui fecharia o laço
        /// <c>GetArmor → GetBodyArmor → ModifyArmorMods → GetArmor</c> em recursão infinita.
        /// A separação em dois métodos existe para tornar esse erro impossível de cometer por
        /// distração, não só improvável.
        /// </summary>
        private static float GetKiRaw(Player player)
        {
            return SaiyaheimConfig.PowerK1Health.Value * GetHealthAboveBase(player)
                   + SaiyaheimConfig.PowerK4PowerSkill.Value * PowerSkill.GetLevel(player);
        }

        /// <summary>
        /// Fórmula de combate do ki ligado. Existe pelo mesmo motivo do <see cref="GetKiRaw"/>:
        /// armadura e block power precisam de um caminho que <b>nunca</b> possa cair no
        /// <c>GetVanillaRaw</c> e daí no <c>GetBodyArmor()</c>, fechando a recursão. Chamar o
        /// <see cref="GetCombatRaw"/> a partir deles seria depender de o toggle não virar no meio
        /// do frame — e depender disso é o erro que esta separação torna impossível de cometer.
        ///
        /// <b>É aqui que a transformação entra</b>, multiplicando o que a soma produziu — o
        /// "aditivo + multiplicativo" do design, na única linha em que ele existe. Consequência
        /// desejada: soco, armadura, block power, velocidade de voo e o número na tela sobem
        /// juntos, sem nenhum deles saber que formas existem.
        ///
        /// ⚠️ O <c>GetPowerMultiplier</c> lê config e <c>SEMan</c>, nunca power level. Se um dia
        /// ele passar a depender do poder — um multiplicador que cresce com a maestria, por
        /// exemplo — a recursão fecha aqui.
        /// </summary>
        private static float GetKiCombatRaw(Player player)
        {
            return (GetKiRaw(player) + GetLateGameBonus(player))
                   * Transformations.TransformationRegistry.GetPowerMultiplier(player);
        }

        /// <summary>Fórmula do ki desligado: a original do projeto, com arma e armadura do jogo.</summary>
        private static float GetVanillaRaw(Player player)
        {
            return SaiyaheimConfig.PowerK1Health.Value * GetHealthAboveBase(player)
                   + SaiyaheimConfig.PowerK2WeaponDamage.Value * GetWeaponDamage(player)
                   + SaiyaheimConfig.PowerK3Armor.Value * player.GetBodyArmor();
        }

        /// <summary>
        /// HP **acima do mínimo**, que é o que de fato representa progressão.
        ///
        /// O HP base do Valheim (25) é dado de graça a todo personagem recém-criado, então contá-lo
        /// daria a todo mundo um piso de poder que não foi conquistado. Descontado, um jogador sem
        /// comida entra na conta com zero deste termo — que é a leitura certa.
        ///
        /// O 25 vem de <c>Player.GetBaseFoodHP()</c>, não de uma constante nossa: é número do jogo,
        /// não de balanceamento do mod, e assim acompanha sozinho se o Valheim mudar.
        /// </summary>
        private static float GetHealthAboveBase(Player player)
        {
            return Mathf.Max(0f, player.GetMaxHealth() - player.GetBaseFoodHP());
        }

        /// <summary>
        /// Dano somado ao soco. Aditivo, e a transformação multiplica por cima — mas isso já
        /// aconteceu dentro do <see cref="GetKiCombatRaw"/>, não aqui.
        /// </summary>
        internal static float GetPunchDamageBonus(Player player)
        {
            if (player == null)
            {
                return 0f;
            }

            return PunchBonusFor(GetKiCombatRaw(player));
        }

        /// <summary>
        /// O bônus de soco de um poder de combate <b>hipotético</b>. Existe para o
        /// <c>saiya_form</c> poder mostrar o antes e o depois da transformação sem copiar a
        /// fórmula — e sem transformar o jogador para descobrir.
        /// </summary>
        internal static float PunchBonusFor(float combatPower)
        {
            return combatPower * SaiyaheimConfig.PunchDamageFromPower.Value;
        }

        /// <summary>
        /// Armadura derivada do poder, que <b>substitui</b> a do equipamento enquanto o ki está
        /// ligado. A parcela base existe para o jogador não ficar mais frágil ao ligar o ki no
        /// começo do jogo, quando a skill ainda está em nível baixo.
        ///
        /// <b>Arredondada para inteiro</b>: a armadura do Valheim é sempre inteira (peça de couro
        /// dá 2, não 2,37), e o número aparece na tela do jogador. Casa decimal aqui só denuncia
        /// que o valor é calculado, sem trazer precisão que importe — a diferença é menor que a
        /// variação de um único ponto de skill.
        ///
        /// <b>A barra vazia é um degrau, não um degradê.</b> A armadura vale cheia do ki máximo
        /// até o último ponto, e só cai — para <c>ArmorFractionWithoutKi</c> — quando zera. Um
        /// degradê proporcional à fração de ki seria mais dramático, mas o ki oscila muito durante
        /// a luta por motivos <b>ofensivos</b> (socar, voar), e perder armadura por atacar puniria
        /// o jogador por algo que não tem relação nenhuma com apanhar.
        /// </summary>
        internal static float GetArmor(Player player)
        {
            if (player == null)
            {
                return 0f;
            }

            // GetKiCombatRaw, não GetCombatRaw: ver o comentário de recursão em GetKiRaw. Este
            // método só é chamado com o ki ligado, então o ramo é o mesmo — mas depender disso
            // seria depender de um invariante que uma troca de toggle no meio do frame quebra.
            float armor = ArmorFor(GetKiCombatRaw(player));

            if (KiManager.Current <= 0f)
            {
                armor *= SaiyaheimConfig.ArmorFractionWithoutKi.Value;
            }

            return Mathf.Round(armor);
        }

        /// <summary>
        /// A armadura de um poder de combate <b>hipotético</b>, sem o degrau da barra vazia — que é
        /// estado do jogador, não do poder. Mesmo papel do <see cref="PunchBonusFor"/>: deixar o
        /// <c>saiya_form</c> comparar dentro e fora da forma sem duplicar a conta.
        /// </summary>
        internal static float ArmorFor(float combatPower)
        {
            return SaiyaheimConfig.ArmorBase.Value + combatPower * SaiyaheimConfig.ArmorFromPower.Value;
        }

        /// <summary>
        /// Block power derivado do poder, que <b>substitui</b> o do item enquanto o ki está ligado
        /// — mesma regra da armadura, e pelo mesmo motivo: quem usa ki abre mão da build vanilla
        /// inteira. Segurar um escudo bom não muda nada; desligar o toggle devolve o escudo na hora.
        ///
        /// <b>Por que isto existe.</b> O block power do punho é <b>2</b>, fixo: o
        /// <c>m_blockPowerPerLevel</c> do <c>PlayerUnarmed</c> é zero, a qualidade é 1, e a skill
        /// Blocking nativa no máximo soma +50%. O pior escudo do jogo (madeira, primeiros dez
        /// minutos) está em 18,5 e o melhor em 155,8. Sem escalar, o bloqueio desarmado não é fraco
        /// — é uma armadilha, porque o <c>BlockAttack</c> manda o <b>resíduo</b> para o
        /// <c>AddStaggerDamage</c>: bloqueio pequeno deixa resíduo grande, o resíduo staggera, e um
        /// bloqueio que falha por stagger não reduz nada. O jogador para de andar e de atacar para
        /// tomar o mesmo dano e ainda ficar preso na animação.
        ///
        /// <b>Calibrado abaixo dos escudos de propósito</b> (decisão de 2026-08-01): começa mais
        /// fraco que o escudo de madeira e termina perto do serpentscale, sem chegar no flametal.
        /// O punho ganha em não quebrar, não ocupar a mão e escalar sozinho; não precisa ganhar
        /// também no número. Rodar <c>saiya_block shields</c> para a tabela.
        ///
        /// Sem arredondar, ao contrário do <see cref="GetArmor"/>: block power não aparece na tela
        /// do jogador, então não há motivo para esconder a casa decimal.
        ///
        /// ⚠️ <b>Nunca pode devolver zero, e não é preciosismo.</b> O <c>Humanoid.BlockAttack</c>
        /// divide pelo block power sem checar:
        /// <code>Mathf.Clamp01(bloqueado / blockPower)</code>
        /// Com block power 0 o <c>ApplyArmor(0)</c> é no-op, então <c>bloqueado</c> também é 0, e
        /// <c>0f / 0f</c> é <b>NaN</b>. O NaN vira o custo de stamina, e aí o jogo escolhe o pior
        /// caminho possível: <c>if (custo &gt; 0f)</c> é <c>false</c> para NaN, então em vez do
        /// <c>UseStamina</c> — que <b>tem</b> guard de NaN — ele chama o <c>AddStamina</c>, que
        /// <b>não tem</b>. A stamina do jogador vira NaN e nunca mais regenera, porque
        /// <c>NaN + regen</c> continua NaN. Só sair e voltar para o mundo conserta.
        ///
        /// Foi bug de verdade, encontrado no primeiro playtest em 2026-08-01: bloquear até a barra
        /// de ki zerar quebrava a stamina permanentemente.
        /// </summary>
        internal static float GetBlockPower(Player player)
        {
            if (player == null)
            {
                return 0f;
            }

            float fromPower = GetKiCombatRaw(player) * SaiyaheimConfig.BlockPowerFromPower.Value;

            // Mesmo degrau da armadura, e de propósito o mesmo config — mas só sobre a parcela que
            // vem do poder. A base sobrevive, e é essa a diferença em relação ao GetArmor: armadura
            // zero é um valor legítimo (o jogador está pelado), block power zero é uma divisão por
            // zero. Com a barra vazia você bloqueia como uma pessoa pelada, não como alguém sem mãos.
            if (KiManager.Current <= 0f)
            {
                fromPower *= SaiyaheimConfig.ArmorFractionWithoutKi.Value;
            }

            return SaiyaheimConfig.BlockPowerBase.Value + fromPower;
        }

        /// <summary>
        /// Número para exibir. Comprimido para não virar um valor gigante e vazio cedo demais.
        ///
        /// Lê o poder de <b>combate</b>: é o número que o jogador associa a "ficar mais forte", e
        /// esconder dele justamente a parcela que dispara no fim do jogo tiraria da tela o momento
        /// que o termo novo existe para criar.
        ///
        /// ⚠️ Só exibição. Se a compressão entrasse no cálculo, dobrar o poder deixaria de dobrar
        /// o dano e o balanceamento viraria outra coisa.
        /// </summary>
        internal static float GetDisplayValue(Player player)
        {
            float raw = GetCombatRaw(player);
            if (raw <= 0f)
            {
                return 0f;
            }

            return Mathf.Pow(raw, SaiyaheimConfig.PowerCompressionExponent.Value)
                   * SaiyaheimConfig.PowerDisplayScale.Value;
        }

        /// <summary>
        /// Dano total da arma equipada. Só usado na fórmula do ki desligado.
        ///
        /// <c>GetCurrentWeapon()</c> nunca devolve null — sem arma equipada ele entrega o
        /// <c>m_unarmedWeapon</c>, cujo dano é de unidade dígita. Isso é o comportamento certo
        /// aqui: quem luta desarmado sem ki tem, de fato, poder de arma quase zero.
        /// </summary>
        private static float GetWeaponDamage(Player player)
        {
            ItemDrop.ItemData weapon = player.GetCurrentWeapon();
            return weapon == null ? 0f : weapon.GetDamage().GetTotalDamage();
        }
    }
}
