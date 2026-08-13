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
        /// etapa 3, e de propósito: peso paga XP de Battle Power e cobra velocidade de voo.
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
        /// Velocidade somada pelo power level.
        ///
        /// <b>Aditiva, não multiplicativa</b>, pelo mesmo motivo do dano do soco: o poder cresce
        /// sem teto (HP acima da base + nível de Battle Power) e multiplicá-lo faria a velocidade
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
            return Power.PowerLevel.GetRaw(player) * SaiyaheimConfig.FlightSpeedFromPower.Value;
        }

        /// <summary>
        /// Multiplicador da forma ativa, ou 1 fora dela.
        ///
        /// <b>A velocidade é a única coisa do voo que a transformação toca.</b> O custo de ki fica
        /// de fora de propósito: voar transformado já custa o dreno da forma <i>somado</i> ao custo
        /// do voo, e encarecer o voo por cima seria cobrar duas vezes pela mesma decisão.
        ///
        /// Vem do <c>TransformationRegistry</c> e não do <c>PowerLevel</c> porque a velocidade lê o
        /// poder <b>linear</b>, e o multiplicador mora no de <b>combate</b> — pegá-lo pelo
        /// <c>GetCombatRaw</c> traria junto o termo de fim de jogo, que a velocidade recusa por
        /// razão própria (ver <see cref="GetSpeedFromPower"/>).
        /// </summary>
        internal static float GetFormSpeedFactor(Player player)
        {
            return Transformations.TransformationRegistry.GetPowerMultiplier(player);
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
            float weightFactor = 1f - SaiyaheimConfig.FlightWeightPenalty.Value * GetWeightLoad(player);

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
        /// <b>Duas reduções, e elas têm formas diferentes de propósito.</b> A da skill é linear
        /// (<c>1 - r × fator</c>) porque a entrada é limitada: o fator de skill vive em 0–1 e o
        /// config em 0–0,95, então o resultado nunca chega a zero sozinho. A do poder <b>não</b>
        /// pode usar essa forma: o termo de fim de jogo não tem teto, e um <c>1 - r × poder</c>
        /// atravessaria o zero e viraria negativo — voar <b>dando</b> ki. Daí o hiperbólico
        /// <c>1 / (1 + r × bônus)</c>, que decai para sempre sem nunca chegar a zero, do mesmo
        /// jeito que o <c>ApplyArmor</c> do próprio Valheim faz com a armadura.
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
            }
            else if (fast)
            {
                cost *= SaiyaheimConfig.FlightFastKiMultiplier.Value;
            }

            float reduction = SaiyaheimConfig.FlightKiSkillReduction.Value * FlightSkill.GetLevelFactor(player);
            cost *= 1f - reduction;

            return Mathf.Max(0f, cost * GetPowerCostFactor(player));
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

            return 1f / (1f + rate * Power.PowerLevel.GetLateGameBonus(player));
        }
    }
}
