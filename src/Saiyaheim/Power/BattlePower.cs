using Saiyaheim.Ki;
using UnityEngine;

namespace Saiyaheim.Power
{
    /// <summary>
    /// O battle power: stat derivado que alimenta o dano do soco e a armadura.
    ///
    /// <b>São duas fórmulas, porque os dois caminhos de progressão são disjuntos.</b>
    ///
    /// <code>
    /// ki desligado: poder = k1*HP + k2*dano_arma + k3*armadura
    /// ki ligado:    poder = k1*HP + k4*nivel_power_level
    /// combate:      (o de cima + termo de fim de jogo) * multiplicador da forma ativa
    /// </code>
    ///
    /// Arma e armadura não sobrevivem ao modo ki:
    /// <list type="bullet">
    /// <item>arma dá <b>zero</b> — o jogador soca, não tem nada equipado; e o dano do soco vem do
    /// battle power, então incluí-lo seria contar o mesmo número duas vezes;</item>
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
    internal static class BattlePower
    {
        /// <summary>
        /// Battle power bruto <b>linear</b>: sem o termo de fim de jogo. É a fórmula original do mod,
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
        /// Battle power de <b>combate</b>: o linear mais o termo de fim de jogo. Alimenta dano do
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
        /// ⚠️ O <c>GetPowerMultiplier</c> lê config e <c>SEMan</c>, nunca battle power. Se um dia
        /// ele passar a depender do poder — um multiplicador que cresce com a maestria, por
        /// exemplo — a recursão fecha aqui.
        /// </summary>
        private static float GetKiCombatRaw(Player player)
        {
            return GetKiCombatRawWithoutForm(player)
                   * Transformations.TransformationRegistry.GetPowerMultiplier(player);
        }

        /// <summary>
        /// O poder de combate do ki ligado <b>antes</b> do multiplicador da forma. Transformado ou
        /// não, devolve o que o jogador teria na forma base.
        ///
        /// Existe para o <see cref="PowerRating"/>, que calcula o poder de luta na forma base e
        /// aplica a forma por cima, em vez de deixar a forma entrar só pela armadura e pelo soco.
        /// </summary>
        internal static float GetKiCombatRawWithoutForm(Player player)
        {
            return GetKiRaw(player) + GetLateGameBonus(player);
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
        /// Quanto ki este soco custa, já com o desconto do poder. Ver
        /// <see cref="GetKiCostFactor"/> para o desconto em si.
        /// </summary>
        internal static float GetPunchKiCost(Player player, float bonus)
        {
            if (player == null || bonus <= 0f)
            {
                return 0f;
            }

            // ⚠️ O `bonus` que chega aqui e' o da FORMA — o GetPunchDamageBonus le o poder ja
            // multiplicado. Dividir por ela devolve o bonus da forma base, e a sobretaxa da forma
            // entra depois, explicita. Parece rodeio e nao e': sem isso o multiplicador da forma
            // entraria duas vezes, uma escondida no bonus e outra na sobretaxa.
            //
            // E' tambem o que torna a conta legivel numa frase: o soco custa o que custaria na
            // forma base, vezes o que a forma cobra a mais. Ver GetFormKiCostMultiplier.
            float form = Transformations.TransformationRegistry.GetPowerMultiplier(player);
            float baseBonus = form > 0f ? bonus / form : bonus;

            return baseBonus
                   * SaiyaheimConfig.PunchKiCostPerDamage.Value
                   * GetKiCostFactor(player)
                   * GetFormKiCostMultiplier(player);
        }

        /// <summary>
        /// O desconto que o poder dá no custo de ki do <b>soco</b>. Em 0–1; devolve 1 (sem
        /// desconto) enquanto o config estiver em zero.
        ///
        /// ⚠️ <b>Valia para os três custos de combate até 2026-09-20</b>, e o texto abaixo defende
        /// essa escolha. Ela estava errada para dois deles: ver
        /// <see cref="GetDefenseKiCostFactor"/>, que agora atende apanhar e bloquear.
        ///
        /// <b>O problema que ele resolve</b> (playtest de 2026-08-04): os três custos nascem
        /// proporcionais ao serviço que o ki prestou, e esse serviço vem do poder de
        /// <b>combate</b> — que não tem teto. O termo de fim de jogo cresce para sempre e a
        /// transformação multiplica tudo. A barra de ki, do outro lado, vem do <b>nível</b> da
        /// skill de Power Level, que para em 100. Um número que cresce sem fim dividido por um que
        /// parou de crescer: mais cedo ou mais tarde um soco custa a barra inteira e sai com o dano
        /// vanilla cru, e dois bloqueios esvaziam a barra. O SSJ apenas antecipou isso, dobrando os
        /// custos hoje em vez de daqui a vinte níveis.
        ///
        /// <b>Era um fator para os três</b>, e não um por consumidor, porque parecia ser o mesmo
        /// fenômeno nos três. <b>Não é</b>, e o playtest de 2026-09-20 mostrou onde: o custo do
        /// soco é proporcional ao <i>seu</i> poder, que cresce sem teto e por isso precisa do
        /// desconto; o de bloquear e o de apanhar são proporcionais ao <b>golpe do inimigo</b>, que
        /// não cresce com o seu poder nenhum. Ali o desconto não corrige crescimento nenhum — ele
        /// só torna a defesa progressivamente grátis, e a forma acelera isso, porque é ela que
        /// multiplica o poder que compra o desconto. Em SSJ3 um bloqueio barrava 3,4x mais dano
        /// pelo mesmo ki.
        ///
        /// <b>Hiperbólico, e pela mesma razão do voo</b> (<c>FlightStats.GetPowerCostFactor</c>):
        /// a entrada não tem teto, então um <c>1 - r × poder</c> atravessaria o zero e viraria um
        /// golpe que <i>devolve</i> ki. O <c>1 / (1 + r × poder)</c> decai para sempre sem nunca
        /// chegar a zero — a mesma forma que o <c>ApplyArmor</c> do próprio Valheim usa.
        ///
        /// A consequência boa é que <b>cada custo satura</b>: tende à taxa dele dividida por esta e
        /// nunca passa disso. Como a barra também tem teto, ações por barra estabiliza em vez de
        /// cair a zero.
        ///
        /// <b>E conserta a transformação sem chave própria.</b> A forma multiplica o poder, e é o
        /// poder que compra o desconto: a razão de dano por barra entre transformado e não
        /// transformado tende ao <c>PowerMultiplier</c> da forma, em vez de ficar em 1 como ficava.
        ///
        /// ⚠️ <b>Lê o poder inteiro, ao contrário do voo</b>, que lê só o termo de fim de jogo. Lá
        /// o motivo é que o custo do voo é fixo por segundo e baratear cedo desmontaria o
        /// <c>FlightKiPerSecond</c> alto. Aqui os custos <b>já</b> são proporcionais ao poder,
        /// então o hiperbólico só corrige um crescimento que já existe — e no começo do jogo, com o
        /// poder pequeno, o desconto é de poucos por cento.
        ///
        /// <b>Um segundo termo no mesmo divisor: a maestria da forma ativa</b>
        /// (<see cref="FormCostPayback"/>, desligada com o config em 0). O termo do poder acima
        /// resolveu o crescimento sem teto, mas produziu um efeito colateral: a forma multiplica o
        /// poder, e é o poder que compra o desconto, então <b>subir de degrau quase não encarece o
        /// soco</b> — no meio do jogo o SSJ2 dá 50% mais dano por soco que o SSJ e custa 6% a
        /// mais.
        ///
        /// A maestria é <b>por forma</b> e nasce em zero a cada degrau, então o degrau dominado
        /// fica mais barato de lutar do que o recém-destravado — sem nenhum dial novo de força, só
        /// a curva de maestria que já existe.
        /// </summary>
        internal static float GetKiCostFactor(Player player)
        {
            return player == null ? 1f : KiCostFactorFor(GetKiCombatRawWithoutForm(player));
        }

        /// <summary>
        /// Quanto a forma ativa cobra a mais nos <b>três</b> custos de combate, em relação ao que
        /// a forma base pagaria pelo mesmo serviço. 1 fora de forma.
        ///
        /// <code>1 + (multiplicador - 1) × CombatFormKiShare × CombatKiCostScale × (1 - MasteryFormCostReduction × maestria/100)</code>
        ///
        /// <b>Por que passou a ser explícito em 2026-09-20.</b> Antes o acréscimo da forma era
        /// implícito: o custo do soco é proporcional ao bônus de dano, o bônus é multiplicado pela
        /// forma, e a devolução da maestria era um termo somado no divisor do desconto de poder.
        /// Parecia equivalente e não era — o desconto de poder também lia o poder <b>multiplicado
        /// pela forma</b>, então ele crescia junto e comia quase todo o acréscimo. Em SSJ3, no meio
        /// do jogo, o soco dava 4x o dano por 1,28x o custo, e apanhar e bloquear ficavam mais
        /// baratos transformado do que fora. O relato do playtest foi exatamente esse: "o custo é
        /// sempre muito próximo ao da forma base".
        ///
        /// Agora as duas coisas são separadas e cada uma lê o que lhe diz respeito: o desconto de
        /// poder lê o poder da <b>forma base</b> (<see cref="GetKiCostFactor"/>), e o preço da
        /// forma é este multiplicador aqui.
        ///
        /// <b>Com os defaults (share 1, redução 1)</b>: na maestria 0 a forma cobra o
        /// multiplicador cheio — 4x o custo por 4x o dano, ou seja, <i>dano por ki igual ao da
        /// forma base</i>, e o que ela compra é o golpe maior, não eficiência. Na maestria 100 ela
        /// cobra 1x, e aí sim o multiplicador inteiro é lucro. É a curva da progressão dita em
        /// números: no começo você mal segura a forma, no fim você a veste.
        ///
        /// ⚠️ <b>Não pode ler battle power</b>, direta ou indiretamente — mesma regra do
        /// <c>GetPowerMultiplier</c> e pelo mesmo motivo: quem chama está no meio da conta do
        /// poder, e uma leitura de volta fecharia recursão. Config, <c>SEMan</c> e nível de skill
        /// não passam nem perto disso.
        /// </summary>
        internal static float GetFormKiCostMultiplier(Player player)
        {
            Transformations.Transformation active =
                Transformations.TransformationRegistry.GetActive(player);

            return active == null
                ? 1f
                : FormKiCostMultiplier(active.GetPowerMultiplier(), active.GetSkillLevel(player),
                    active.GetCombatKiCostScale());
        }

        /// <summary>
        /// O multiplicador de custo de uma forma <b>hipotética</b>. Existe pelo mesmo motivo que o
        /// <see cref="PunchBonusFor"/>: o <c>saiya_form</c> mostra o antes e o depois sem
        /// transformar o jogador nem copiar a fórmula.
        ///
        /// <paramref name="costScale"/> é a fração da sobretaxa compartilhada que a forma cobra
        /// (<c>CombatKiCostScale</c>, por forma): 1 em quase todas, menos no SSJ God, que é a
        /// forma do controle e compra fôlego em vez de golpe.
        /// </summary>
        internal static float FormKiCostMultiplier(float powerMultiplier, float masteryLevel, float costScale)
        {
            float premium = Mathf.Max(0f, powerMultiplier - 1f);
            if (premium <= 0f)
            {
                return 1f;
            }

            float share = Mathf.Max(0f, SaiyaheimConfig.CombatFormKiShare.Value) * Mathf.Max(0f, costScale);
            float paid = Mathf.Clamp01(SaiyaheimConfig.MasteryFormCostReduction.Value)
                         * Mathf.Clamp01(masteryLevel / 100f);

            // Piso em 1: uma forma que deixasse o combate mais barato que a forma base seria um
            // segundo multiplicador de forca escondido num dial de custo.
            return Mathf.Max(1f, 1f + premium * share * (1f - paid));
        }

        /// <summary>
        /// O desconto que o poder dá nos custos de ki da <b>defesa</b> — apanhar e bloquear.
        /// <b>Nenhum</b> com o config no default (<c>DefenseKiCostPowerReduction</c> = 0).
        ///
        /// <b>Por que estes dois saíram do fator do soco em 2026-09-20.</b> O custo deles é
        /// proporcional ao dano que o ki parou, e esse dano vem do <b>inimigo</b>: ele cresce por
        /// bioma, não com o poder do jogador. Descontar pelo poder tornava a defesa mais barata
        /// exatamente quando ela ficava mais forte, e transformar dobrava a dose. O sintoma
        /// relatado no jogo foi "bloquear transformado não custa nada".
        ///
        /// <b>Chave própria, e não a remoção do desconto</b>, porque a pergunta de fim de jogo
        /// continua legítima — se o dano dos biomas finais crescer mais que a barra de ki, é aqui
        /// que se compra alívio, sem mexer no soco.
        ///
        /// <b>Inclui o multiplicador da forma</b> (<see cref="GetFormKiCostMultiplier"/>), e é a
        /// única coisa que a forma faz com estes dois custos — aqui ela não tem um acréscimo
        /// implícito para cancelar, como o soco tem no bônus de dano. Transformado, apanhar e
        /// bloquear custam o multiplicador da forma a mais, e a maestria dissolve isso.
        /// </summary>
        internal static float GetDefenseKiCostFactor(Player player)
        {
            if (player == null)
            {
                return 1f;
            }

            float rate = SaiyaheimConfig.DefenseKiCostPowerReduction.Value;
            float discount = rate <= 0f
                ? 1f
                : 1f / (1f + rate * Mathf.Max(0f, GetKiCombatRawWithoutForm(player)));

            return discount * GetFormKiCostMultiplier(player);
        }

        /// <summary>
        /// O fator de desconto de um poder de combate <b>hipotético</b>, sempre o da forma
        /// <b>base</b>. Existe pelo mesmo motivo que o <see cref="PunchBonusFor"/>: o
        /// <c>saiya_form</c> mostra o antes e o depois da transformação sem transformar o jogador
        /// nem copiar a fórmula.
        ///
        /// ⚠️ <b>Recebia um segundo termo somado, a devolução da maestria, até 2026-09-20.</b> O
        /// preço da forma saiu do divisor e virou um multiplicador próprio
        /// (<see cref="FormKiCostMultiplier"/>) — misturar os dois no mesmo divisor era o que
        /// fazia o acréscimo da forma quase desaparecer.
        /// </summary>
        internal static float KiCostFactorFor(float combatPower)
        {
            float rate = SaiyaheimConfig.KiCostPowerReduction.Value;

            if (rate <= 0f)
            {
                return 1f;
            }

            return 1f / (1f + rate * Mathf.Max(0f, combatPower));
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
            return ArmorWithKiStep(ArmorFor(GetKiCombatRaw(player)));
        }

        /// <summary>
        /// A armadura que o jogador teria <b>fora da forma</b>, com o mesmo degrau da barra vazia e
        /// o mesmo arredondamento do <see cref="GetArmor"/>. Mesmo papel do
        /// <see cref="GetKiCombatRawWithoutForm"/>.
        /// </summary>
        internal static float GetArmorWithoutForm(Player player)
        {
            if (player == null)
            {
                return 0f;
            }

            return ArmorWithKiStep(ArmorFor(GetKiCombatRawWithoutForm(player)));
        }

        private static float ArmorWithKiStep(float armor)
        {
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

        // Aqui morava o GetDisplayValue, removido na etapa 10. Ele comprimia o poder de combate
        // — o stat INTERNO — e o mandava para a tela. O número exibido passou a sair do
        // PowerRating, em termos vanilla (HP e dano), porque um número que só o jogador tem não
        // se compara com inimigo nenhum, e comparar é a razão de o scan existir. O que esta
        // classe calcula continua sendo o que alimenta dano, armadura e block power, e nada
        // disso mudou.

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
