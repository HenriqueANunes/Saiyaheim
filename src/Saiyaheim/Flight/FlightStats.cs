using UnityEngine;

namespace Saiyaheim.Flight
{
    /// <summary>
    /// As contas do voo, separadas do <see cref="SE_Flight"/> para que o comando de debug possa
    /// mostrar exatamente os mesmos números que o efeito usa — sem duplicar fórmula.
    ///
    /// Tudo aqui é função pura do estado atual do jogador (skill, peso, config). Nada é cacheado:
    /// o peso muda a cada item pego e a config pode ser editada com o jogo aberto.
    /// </summary>
    internal static class FlightStats
    {
        /// <summary>
        /// Carga do inventário em 0–1. É o mesmo dado que o <c>SkillXpWeightBonus</c> usa na
        /// etapa 3, e de propósito: peso paga XP de Power Level e cobra velocidade de voo.
        /// A roupa pesada do Goku é exatamente essa troca.
        /// </summary>
        internal static float GetWeightLoad(Player player)
        {
            if (player == null)
            {
                return 0f;
            }

            float maxWeight = player.GetMaxCarryWeight();
            Inventory inventory = player.GetInventory();
            if (maxWeight <= 0f || inventory == null)
            {
                return 0f;
            }

            return Mathf.Clamp01(inventory.GetTotalWeight() / maxWeight);
        }

        /// <summary>
        /// Velocidade somada pelo battle power.
        ///
        /// <b>Aditiva, não multiplicativa</b>, pelo mesmo motivo do dano do soco: o poder cresce
        /// sem teto (HP acima da base + nível da skill Power Level) e multiplicá-lo faria a velocidade
        /// explodir contra o teto do <c>MaxSpeed</c> cedo demais.
        ///
        /// Efeito colateral desejado: comer melhor faz voar mais rápido, porque HP entra na
        /// fórmula do poder. É a leitura correta de "ficar mais forte" no gênero.
        ///
        /// ⚠️ <c>GetRaw</c> e não <c>GetCombatRaw</c>: a velocidade fica de fora do termo de fim de
        /// jogo <b>de propósito</b>. Ela já encosta no <c>FlightMaxSpeed</c>, que é limite de
        /// streaming de zonas e não balanceamento — acelerar aqui não daria velocidade nenhuma, só
        /// tempo gasto contra um teto. O que o fim de jogo compra no voo é
        /// <see cref="GetKiCostPerSecond"/> mais barato.
        /// </summary>
        internal static float GetSpeedFromPower(Player player)
        {
            return Power.BattlePower.GetRaw(player) * SaiyaheimConfig.FlightSpeedFromPower.Value;
        }

        /// <summary>
        /// Multiplicador da forma ativa <b>amortecido</b>, ou 1 fora dela:
        /// <c>1 + (PowerMultiplier - 1) × FormSpeedShare</c>.
        ///
        /// <b>A velocidade é a única coisa do voo que a transformação toca.</b> O custo de ki fica
        /// de fora de propósito: voar transformado já custa o dreno da forma <i>somado</i> ao custo
        /// do voo, e encarecer o voo por cima seria cobrar duas vezes pela mesma decisão.
        ///
        /// O <c>PowerMultiplier</c> cru não serve aqui, e o playtest de 2026-08-21 mostrou por quê:
        /// a forma base ficou boa e a transformada, rápida demais. O número é calibrado para
        /// combate — dobrar o soco é uma coisa, dobrar a velocidade de voo é outra — e passado
        /// direto ele jogava a velocidade contra o <c>FlightMaxSpeed</c>, onde poder a mais não
        /// compra velocidade nenhuma e SSJ e SSJ2 deixam de se distinguir no ar. Amortecer só o
        /// <i>ganho</i> (e não o fator inteiro) mantém "sem forma = 1" de graça, sem caso especial.
        ///
        /// Vem do <c>TransformationRegistry</c> e não do <c>BattlePower</c> porque a velocidade lê o
        /// poder <b>linear</b>, e o multiplicador mora no de <b>combate</b> — pegá-lo pelo
        /// <c>GetCombatRaw</c> traria junto o termo de fim de jogo, que a velocidade recusa por
        /// razão própria (ver <see cref="GetSpeedFromPower"/>).
        /// </summary>
        internal static float GetFormSpeedFactor(Player player)
        {
            float multiplier = Transformations.TransformationRegistry.GetPowerMultiplier(player);

            return 1f + (multiplier - 1f) * SaiyaheimConfig.FlightFormSpeedShare.Value;
        }

        /// <summary>
        /// Fator pelo qual o peso multiplica a velocidade:
        /// <c>1 - WeightPenalty × carga^WeightCurve</c>.
        ///
        /// <b>Curva de potência, não hiperbólica.</b> O hiperbólico <c>1/(1 + r×x)</c> que o custo
        /// de ki usa tem a forma <i>oposta</i> à que se quer aqui: ele desaba logo na entrada e
        /// depois achata. Lá ele é obrigatório porque a entrada não tem teto; aqui a carga vive em
        /// 0–1, então o expoente é a ferramenta certa — a mesma do <see cref="GetSkillCostFactor"/>.
        ///
        /// Com o expoente acima de 1 a penalidade fica quase toda encostada no limite: levar
        /// algumas peças a mais não se sente, e é encher o inventário que pesa no voo. Preserva a
        /// leitura de "a roupa pesada do Goku é uma troca" sem cobrar por cada pedra recolhida no
        /// caminho. Em 1 a forma volta a ser a reta antiga.
        /// </summary>
        internal static float GetWeightSpeedFactor(Player player)
        {
            float load = Mathf.Pow(GetWeightLoad(player), SaiyaheimConfig.FlightWeightCurve.Value);

            return 1f - SaiyaheimConfig.FlightWeightPenalty.Value * load;
        }

        /// <summary>
        /// Velocidade base, já com poder, skill, peso e forma. É o valor que vai para
        /// <c>Character.m_flySlowSpeed</c>.
        ///
        /// O peso multiplica <b>tudo</b>, inclusive a parcela do poder: carregar meio inventário
        /// deve doer no jogador forte tanto quanto no fraco.
        /// </summary>
        internal static float GetSlowSpeed(Player player)
        {
            float skillFactor = 1f + SaiyaheimConfig.FlightSpeedSkillBonus.Value * FlightSkill.GetLevelFactor(player);
            float weightFactor = GetWeightSpeedFactor(player);

            float baseSpeed = SaiyaheimConfig.FlightBaseSpeed.Value + GetSpeedFromPower(player);
            float speed = baseSpeed * skillFactor * weightFactor * GetFormSpeedFactor(player);

            // Piso baixo, não zero: com WeightPenalty em 1 e peso máximo o jogador ficaria parado
            // no ar sem entender por quê.
            return Mathf.Clamp(speed, 1f, SaiyaheimConfig.FlightMaxSpeed.Value);
        }

        /// <summary>Velocidade com o botão de correr segurado. Vai para <c>m_flyFastSpeed</c>.</summary>
        internal static float GetFastSpeed(Player player)
        {
            float speed = GetSlowSpeed(player) * SaiyaheimConfig.FlightFastSpeedMultiplier.Value;

            // O teto vale aqui também: ele é limite do streaming de zonas, não balanceamento,
            // e o modo rápido é justamente onde ele seria estourado.
            return Mathf.Clamp(speed, 1f, SaiyaheimConfig.FlightMaxSpeed.Value);
        }

        /// <summary>
        /// Parado no ar: nenhum input de movimento, nem horizontal nem vertical.
        ///
        /// Lê o <c>m_moveDir</c> em vez do teclado porque ele é o resultado final do input —
        /// já passou pelo <c>PlayerController</c> e pelo <c>SE_Flight.ApplyVerticalInput</c>,
        /// então subir com o Jump ou descer com o Crouch aparece aqui e não conta como parado.
        /// Só vale chamar <b>depois</b> do <c>ApplyVerticalInput</c> do tick.
        ///
        /// O épsilon é teste de zero, não número de balanceamento: sem input o vetor é
        /// exatamente zero, com input ele é normalizado. Existe só para não deixar drift de
        /// analógico contar como movimento.
        /// </summary>
        internal static bool IsHovering(Player player)
        {
            if (player == null)
            {
                return false;
            }

            return player.GetMoveDir().sqrMagnitude < 0.0001f;
        }

        /// <summary>
        /// Ki por segundo. O <paramref name="fast"/> vem do mesmo <c>m_run</c> que o
        /// <c>UpdateFlying</c> vanilla lê para escolher a velocidade — os dois andam juntos.
        ///
        /// <paramref name="hovering"/> (ver <see cref="IsHovering"/>) barateia o voo parado no ar:
        /// manter altitude é menos esforço do que atravessar o mapa, e sem isso parar para mirar,
        /// olhar em volta ou conversar custava o mesmo que viajar.
        ///
        /// <b>Duas reduções, e elas têm formas diferentes de propósito.</b> A da skill é
        /// subtrativa (ver <see cref="GetSkillCostFactor"/>) porque a entrada é limitada: o fator
        /// de skill vive em 0–1 e o config em 0–0,99, então o resultado nunca chega a zero
        /// sozinho. A do poder <b>não</b> pode usar essa forma: o termo de fim de jogo não tem
        /// teto, e um <c>1 - r × poder</c> atravessaria o zero e viraria negativo — voar
        /// <b>dando</b> ki. Daí o hiperbólico <c>1 / (1 + r × bônus)</c>, que decai para sempre
        /// sem nunca chegar a zero, do mesmo jeito que o <c>ApplyArmor</c> do próprio Valheim faz
        /// com a armadura.
        /// </summary>
        internal static float GetKiCostPerSecond(Player player, bool fast, bool hovering = false)
        {
            float cost = SaiyaheimConfig.FlightKiPerSecond.Value;

            if (hovering)
            {
                // O botão de correr é ignorado de propósito: parado no ar ele não compra
                // velocidade nenhuma, e cobrar o FastKiMultiplier por um shift esquecido seria
                // punir o jogador por um input que não fez nada.
                cost *= SaiyaheimConfig.FlightHoverKiMultiplier.Value;

                // A sobretaxa do cheese. Ver IsFightingSomething.
                if (IsFightingSomething(player))
                {
                    cost *= Mathf.Max(1f, SaiyaheimConfig.FlightCombatHoverMultiplier.Value);
                }
            }
            else if (fast)
            {
                cost *= SaiyaheimConfig.FlightFastKiMultiplier.Value;
            }

            cost *= GetSkillCostFactor(player);

            // A sobretaxa da forma entra ANTES das reducoes, e nao depois: as duas reducoes sao
            // multiplicativas, entao a ordem nao muda a conta — mas ler "custo base, o que a forma
            // acrescenta, o que as skills devolvem" e' a ordem em que as tres coisas acontecem na
            // cabeca do jogador.
            cost *= GetFormCostFactor(player);

            return Mathf.Max(0f, cost * GetPowerCostFactor(player));
        }

        /// <summary>
        /// Quanto a forma ativa encarece o voo: <c>1 + (multiplicador - 1) × share × (1 - maestria)</c>.
        /// Devolve 1 fora de forma, que e' o caso da esmagadora maioria das chamadas.
        ///
        /// <b>Por que a forma paga.</b> Ela multiplica a velocidade de voo
        /// (<see cref="GetFormSpeedFactor"/>) e, desde que o dreno da forma virou manutencao
        /// simbolica em 2026-09-20, praticamente nao paga nada por isso — a soma "dreno da forma +
        /// custo do voo" que respondia pela conta virou troco. Sem esta sobretaxa, voar
        /// transformado e' velocidade de graca.
        ///
        /// <b>Ancorada na fracao que vira velocidade</b>, e nao no multiplicador cheio: a forma so
        /// entrega <c>FormSpeedShare</c> do ganho de poder no ar, e cobrar pelo resto seria cobrar
        /// por uma coisa que ela nao da'. A chave e' propria mesmo assim
        /// (<c>FlightFormKiShare</c>), porque numero de balanceamento nao se compartilha.
        ///
        /// <b>Quem paga e' a maestria da forma</b>, que treina lutando — nao a skill de voo. Com a
        /// skill de voo o desconto seria duplo, ja' que ela tambem barateia o custo base, e o farm
        /// voando voltaria pela porta dos fundos.
        ///
        /// ⚠️ Nao le battle power, direta nem indiretamente: so config, <c>SEMan</c> e nivel de
        /// skill. Mesma regra do <c>TransformationRegistry.GetPowerMultiplier</c>, e pelo mesmo
        /// motivo — o poder de combate ja' e' multiplicado pela forma e uma leitura de volta
        /// fecharia recursao.
        /// </summary>
        internal static float GetFormCostFactor(Player player)
        {
            float share = SaiyaheimConfig.FlightFormKiShare.Value;
            if (share <= 0f || player == null)
            {
                return 1f;
            }

            Transformations.Transformation active =
                Transformations.TransformationRegistry.GetActive(player);

            if (active == null)
            {
                return 1f;
            }

            // Piso em zero nos dois: um multiplicador abaixo de 1 seria forma que enfraquece (o
            // GetPowerMultiplier ja' barra isso), e maestria fora de 0-100 nao existe.
            float premium = Mathf.Max(0f, active.GetPowerMultiplier() - 1f);
            float mastery = Mathf.Clamp01(active.GetSkillLevel(player) / 100f);

            return 1f + premium * share * (1f - mastery);
        }

        /// <summary>
        /// Fator pelo qual a skill de voo multiplica o custo: <c>1 - reducao × (nivel/100)^curva</c>.
        ///
        /// A curva existe porque os dois extremos são requisitos que brigam entre si. O nível 100
        /// precisa parecer chegada — a skill de voo é o <b>único</b> eixo de progressão do voo, e
        /// maximizá-la para pagar metade do preço não se parece com ter maximizado nada. Mas uma
        /// reta até 95% de desconto entrega metade da economia no nível 50, e aí o voo vira o
        /// transporte padrão antes de o jogador ter treinado qualquer coisa.
        ///
        /// O expoente resolve os dois: a economia fica quase toda depois do nível 75, o começo
        /// continua pagando o <c>FlightKiPerSecond</c> quase cheio, e o topo é praticamente de
        /// graça. Com <c>KiSkillCurve</c> em 1 a forma volta a ser a reta antiga.
        ///
        /// Nunca chega a zero por si só: o config tem teto em 0,99.
        /// </summary>
        internal static float GetSkillCostFactor(Player player)
        {
            float curve = SaiyaheimConfig.FlightKiSkillCurve.Value;
            float progress = Mathf.Pow(FlightSkill.GetLevelFactor(player), curve);

            return 1f - SaiyaheimConfig.FlightKiSkillReduction.Value * progress;
        }

        /// <summary>
        /// Há alguma coisa hostil e <b>alertada</b> por perto — ou seja, o jogador está numa luta
        /// mesmo que ninguém consiga alcançá-lo.
        ///
        /// <b>Para que serve.</b> Pairar parado no ar enquanto um boss não alcança é o <i>cheese</i>
        /// que o feedback público relata, e até 2026-09-20 o mod <b>pagava</b> por ele: pairar
        /// custava metade de voar. Encarecer por altitude não resolveria — a faixa em que se fica
        /// fora do alcance de um boss é a mesma em que se voa para não bater em árvore, então o
        /// número puniria viajar sem tocar no cheese. As duas variáveis que separam os dois casos
        /// são <b>parado</b> e <b>em combate</b>, e é o que esta pergunta responde.
        ///
        /// <b>Cacheado.</b> A varredura roda no máximo a cada <see cref="CombatScanInterval"/>
        /// segundos, e não a cada tick de física. A lista de personagens do jogo é curta (dezenas),
        /// mas o custo do voo é lido várias vezes por frame — pelo dreno, pela HUD e pelo
        /// <c>saiya_fly</c> — e nenhuma delas precisa de resposta nova a cada leitura.
        ///
        /// Tudo por API pública do jogo, conferido na assembly não-publicizada em 2026-09-20:
        /// <c>Character.GetAllCharacters</c>, <c>Character.GetBaseAI</c>, <c>BaseAI.IsAlerted</c>,
        /// <c>IsTamed</c>, <c>IsDead</c> e <c>GetCenterPoint</c> são todos públicos.
        /// </summary>
        internal static bool IsFightingSomething(Player player)
        {
            if (player == null || SaiyaheimConfig.FlightCombatHoverMultiplier.Value <= 1f)
            {
                return false;
            }

            if (ReferenceEquals(player, _combatCachePlayer) && Time.time < _combatCacheUntil)
            {
                return _combatCacheValue;
            }

            _combatCachePlayer = player;
            _combatCacheUntil = Time.time + CombatScanInterval;
            _combatCacheValue = ScanForAlertedEnemies(player, SaiyaheimConfig.FlightCombatHoverRange.Value);

            return _combatCacheValue;
        }

        /// <summary>Segundos entre duas varreduras. Fixo: é taxa de amostragem, não balanceamento.</summary>
        private const float CombatScanInterval = 0.5f;

        private static Player _combatCachePlayer;
        private static float _combatCacheUntil;
        private static bool _combatCacheValue;

        /// <summary>
        /// A varredura em si. Distância em <b>3D</b>, e não no plano: o inimigo do cheese está
        /// justamente <i>embaixo</i> do jogador, e medir só o eixo horizontal daria zero metros
        /// para quem está a cinquenta de altura.
        /// </summary>
        private static bool ScanForAlertedEnemies(Player player, float range)
        {
            System.Collections.Generic.List<Character> all = Character.GetAllCharacters();
            if (all == null)
            {
                return false;
            }

            Vector3 center = player.GetCenterPoint();
            float sqrRange = range * range;

            // For sem enumerador e sem LINQ: isto roda duas vezes por segundo com o jogador no ar,
            // e a lista e' do jogo — alocar aqui seria lixo por voo inteiro.
            for (int i = 0; i < all.Count; i++)
            {
                Character other = all[i];

                // Jogador nao conta nem em PvP: dois amigos voando lado a lado nao sao uma luta.
                // Domesticado tambem nao — o lobo do jogador fica alertado o tempo todo.
                if (other == null || other == player || other.IsPlayer() || other.IsTamed() || other.IsDead())
                {
                    continue;
                }

                if ((other.GetCenterPoint() - center).sqrMagnitude > sqrRange)
                {
                    continue;
                }

                BaseAI ai = other.GetBaseAI();
                if (ai != null && ai.IsAlerted())
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Fator hiperbólico com que o termo de fim de jogo barateia o voo. Devolve 1 (sem efeito)
        /// enquanto o config estiver em zero, que é o default.
        ///
        /// Lê o <c>GetLateGameBonus</c> e <b>não</b> o poder inteiro: quem já voa barato no começo
        /// do jogo desmontaria o <c>FlightKiPerSecond</c> alto, que existe justamente para o voo
        /// não virar o meio de transporte padrão. A recompensa é do fim de jogo, e só dele.
        /// </summary>
        internal static float GetPowerCostFactor(Player player)
        {
            float rate = SaiyaheimConfig.FlightKiPowerReduction.Value;
            if (rate <= 0f)
            {
                return 1f;
            }

            return 1f / (1f + rate * Power.BattlePower.GetLateGameBonus(player));
        }
    }
}
