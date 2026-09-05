using UnityEngine;

namespace Saiyaheim.Power
{
    /// <summary>
    /// O <b>poder de luta</b>: o número escaneável, que vale para jogador e inimigo na
    /// <b>mesma escala</b>.
    ///
    /// <b>Não confundir com o <see cref="BattlePower"/>.</b> São coisas diferentes e a distinção
    /// é a decisão central da etapa 10:
    ///
    /// <list type="table">
    /// <item><term><see cref="BattlePower"/></term><description>o stat interno
    /// (<c>k1·HP + k4·skill + termo de fim de jogo, × forma</c>). Alimenta dano do soco,
    /// armadura e block power. <b>Intocado por esta classe.</b></description></item>
    /// <item><term><see cref="PowerRating"/></term><description>o número na tela. Medido em termos
    /// <b>vanilla</b>, um passo <b>depois</b> da conta do mod.</description></item>
    /// </list>
    ///
    /// <code>
    /// poder = k1 · HP_efetivo + k2 · dano_por_segundo
    /// HP_efetivo = HP_máximo × (1 + armadura / C)
    /// </code>
    ///
    /// <b>Por segundo, e não por golpe.</b> A primeira versão media dano por ataque e o Troll
    /// saía acima de um jogador maxado em SSJ2 — o que a luta desmente. O motivo não era peso
    /// errado: o Troll bate 70 <b>a cada ~4 segundos</b> e o jogador soca ~1×/s. Por golpe eles
    /// empatam; por segundo o jogador bate quatro vezes mais, e é exatamente aí que mora o
    /// "consigo matar o Troll porque o combate é meu". Cadência é a variável, não o peso.
    ///
    /// Corrigir por peso era possível e estava errado: subir <c>k2</c> o bastante para derrubar o
    /// Troll inflava junto os <b>canhões de vidro</b> — o Deathsquito tem 10 de vida, morre de um
    /// sopro, e a <c>k2 = 20</c> lia quase como um Troll. Trocava uma distorção por outra. Por
    /// segundo, ele desinfla sozinho, sem nenhuma regra especial.
    ///
    /// <b>Por que em termos vanilla.</b> Um número escaneável só serve se escanear um troll e ler
    /// 8.400 contra o seu 12.000 significar <i>"eu ganho dele"</i>. Isso exige que os dois lados
    /// saiam da <b>mesma medida</b>, e as únicas grandezas que jogador e criatura têm em comum são
    /// HP e dano. A fórmula interna do mod não serve: ela lê nível de skill, que bicho nenhum tem.
    ///
    /// <b>E não é circular</b>, apesar de ler o dano que o mod produz. O dano do soco é
    /// <b>saída</b> do battle power, e este número nunca volta para dentro do cálculo — é
    /// projeção, não realimentação. De quebra, a progressão do mod aparece sozinha: subir Power
    /// Level sobe o dano do soco, que sobe o número, sem esta classe saber que skills existem.
    ///
    /// <b>A armadura multiplica o HP, e essa volta foi decidida no playtest.</b> A versão anterior
    /// deixava defesa de fora, porque <c>Character.GetBodyArmor()</c> devolve <c>0f</c> para todo
    /// inimigo do jogo — só o <c>Player</c> sobrescreve — e um termo que dispara de um lado só
    /// parecia quebrar a escala comum.
    ///
    /// O que derrubou isso foi o jogo: <b>entre a forma base e o SSJ2 o número mexia 10%.</b> A
    /// causa é que a transformação <b>não dá vida</b>, e o HP era 87% do total. O que ela dá é
    /// dano e <b>armadura</b> — que sai de 29 na base para 86 no SSJ2, também ×3. Ou seja: o termo
    /// jogado fora era exatamente aquele em que a transformação mais aparece. Durabilidade é o
    /// "HP" de uma forma.
    ///
    /// <b>E a assimetria quase não existe nesta forma.</b> Com armadura 0 o fator vale exatamente
    /// 1 e o termo some sozinho: é a mesma linha para os dois lados, com um valor que calha de ser
    /// zero num deles — não um ramo no código nem uma fórmula por tipo de personagem.
    ///
    /// <b>Não reproduz o <c>ApplyArmor</c> do jogo, de propósito.</b> A mitigação real depende do
    /// tamanho do golpe que entra — a mesma armadura 37 vale 14,8× de vida contra golpes de 10 e
    /// 1,2× contra golpes de 200 —, então "quanto a armadura multiplica minha vida" não tem
    /// resposta única e um número só nunca vai carregar essa curva. O <c>C</c> é uma escala
    /// legível ("<c>C</c> de armadura dobra a vida efetiva"), não uma simulação, e nada de combate
    /// lê esta classe.
    ///
    /// <b>Variante estrelada sai de graça nos dois termos</b>, e isso não era esperado: o
    /// <c>Character.SetLevel</c> faz <c>SetMaxHealth(GetMaxHealthBase() × level)</c>, então o HP
    /// já vem multiplicado; e o <c>Attack.GetLevelDamageFactor</c> é
    /// <c>1 + (nível−1)·0,5</c>, aplicado aqui na mão. A nota de design previa "reler o
    /// multiplicador na hora" e não precisa.
    /// </summary>
    internal static class PowerRating
    {
        /// <summary>
        /// Poder de luta cru de qualquer <c>Character</c> — jogador ou bicho, mesma fórmula, mesma
        /// escala. É o número **antes** da compressão; ver <see cref="GetDisplay"/>.
        /// </summary>
        internal static float GetRaw(Character character)
        {
            if (character == null)
            {
                return 0f;
            }

            return SaiyaheimConfig.RatingK1Health.Value * GetEffectiveHealth(character)
                   + SaiyaheimConfig.RatingK2Damage.Value * GetDamagePerSecond(character);
        }

        /// <summary>
        /// O número que vai para a tela: o cru, escalado.
        ///
        /// <b>Houve uma compressão aqui, e ela foi removida no playtest.</b> Era
        /// <c>raw ^ CompressionExponent</c>, em 0,5 — raiz quadrada. O problema é que um expoente
        /// sobre o valor vira o mesmo expoente sobre a <b>razão</b>: a diferença entre a forma
        /// base e o SSJ2 saía de 1,79× na conta para 1,34× na tela, e a diferença entre um Boar e
        /// um Troll saía de 14× para 3,8×. Ela achatava exatamente o que o número existe para
        /// mostrar.
        ///
        /// Ela fazia sentido quando o exibido era o battle power <b>interno</b>, que cresce sem
        /// teto. O poder de luta tem topo conhecido — o Fader, na casa dos 25 mil crus —, que é
        /// perfeitamente legível sem tratamento nenhum. A compressão resolvia um problema que
        /// deixou de existir quando a fórmula mudou.
        ///
        /// Sobrou o <c>DisplayScale</c>, que é linear e portanto <b>não distorce razão nenhuma</b>
        /// — só escolhe o tamanho do número.
        /// </summary>
        internal static float GetDisplay(Character character)
        {
            return ToDisplay(GetRaw(character));
        }

        /// <summary>
        /// A escala sozinha, para quem já tem o valor cru em mãos — o <c>saiya_power</c> imprime
        /// os dois lado a lado, e o scan de jogador remoto recebe o cru pela rede.
        /// </summary>
        internal static float ToDisplay(float raw)
        {
            return raw <= 0f ? 0f : raw * SaiyaheimConfig.PowerDisplayScale.Value;
        }

        /// <summary>
        /// O termo de dano sozinho, para o <c>saiya_power scan</c> imprimir a coluna ao lado do
        /// HP. Quando a ordem da lista sair errada, é a comparação entre as duas colunas que diz
        /// qual dos dois pesos está mentindo — sem elas o comando só mostra o sintoma.
        /// </summary>
        internal static float GetDps(Character character)
        {
            return character == null ? 0f : GetDamagePerSecond(character);
        }

        /// <summary>
        /// A vida efetiva, para o <c>saiya_power</c> imprimir. Mesmo papel do
        /// <see cref="GetDps"/>: sem esta coluna, a lista mostra HP cru e esconde justamente a
        /// parcela que a transformação move.
        /// </summary>
        internal static float GetEffectiveHp(Character character)
        {
            return character == null ? 0f : GetEffectiveHealth(character);
        }

        /// <summary>
        /// Vida efetiva: o HP máximo esticado pela armadura. É o termo em que a transformação
        /// aparece do lado defensivo — ela multiplica a armadura junto com o dano.
        ///
        /// <b>Sem ramo por tipo de personagem, e isso é a propriedade que importa.</b> O
        /// <c>Character.GetBodyArmor()</c> é <c>virtual</c> e a base devolve <c>0f</c>; só o
        /// <c>Player</c> sobrescreve. Então bicho entra aqui com fator 1 e sai com o próprio HP,
        /// pela mesma linha que o jogador percorre.
        ///
        /// ⚠️ <b>Chama <c>GetBodyArmor()</c>, e isso é seguro — mas por pouco.</b> No jogador com
        /// ki ligado esse método passa pelo <c>SE_KiBody.ModifyArmorMods</c>, que chama o
        /// <c>BattlePower.GetArmor</c>, que lê o <c>GetKiCombatRaw</c>. A cadeia fecha ali porque
        /// nada dentro do <c>BattlePower</c> conhece esta classe. <b>Se um dia o battle power
        /// passar a ler o poder de luta, a recursão fecha exatamente aqui</b> — é a mesma
        /// armadilha que o <c>BattlePower.GetKiRaw</c> documenta, vista do outro lado.
        /// </summary>
        private static float GetEffectiveHealth(Character character)
        {
            float scale = SaiyaheimConfig.RatingArmorScale.Value;
            if (scale <= 0f)
            {
                return character.GetMaxHealth();
            }

            return character.GetMaxHealth() * (1f + character.GetBodyArmor() / scale);
        }

        /// <summary>
        /// Dano por segundo deste personagem, já com o fator de estrela.
        ///
        /// <b>Jogador:</b> o dano da arma equipada mais o bônus que o mod soma no soco, dividido
        /// pela cadência. O <c>GetCurrentWeapon()</c> nunca devolve null — sem nada equipado ele
        /// entrega o <c>m_unarmedWeapon</c>, cujo dano é de unidade dígita, e é o valor certo:
        /// quem soca sem ki bate mesmo quase nada. Com o ki ligado, o bônus domina.
        ///
        /// <b>Criatura:</b> a média das armas do inventário dela, cada uma dividida pelo
        /// <b>próprio</b> intervalo. Bicho do Valheim guarda cada ataque como um item de arma
        /// (<c>m_defaultItems</c> do prefab, que o <c>GiveDefaultItem</c> despeja no inventário),
        /// e cada item traz a cadência dele no <c>m_aiAttackInterval</c> — então isto é
        /// literalmente "o dano por segundo deste bicho", sem tabela escrita à mão.
        ///
        /// ⚠️ A divisão é por arma, e não do total pela média dos intervalos. São coisas
        /// diferentes quando um bicho tem um golpe rápido e fraco e um lento e forte, que é o caso
        /// comum — a média das taxas é a certa, a taxa das médias não.
        /// </summary>
        private static float GetDamagePerSecond(Character character)
        {
            float dps = character is Player player
                ? GetPlayerDps(player)
                : GetCreatureDps(character);

            return dps * GetLevelDamageFactor(character);
        }

        /// <summary>
        /// Dano por segundo do jogador. Soma o bônus do mod só com o ki ligado — com o toggle
        /// desligado o <c>SE_KiBody</c> não está aplicado e o soco sai vanilla cru, então somar
        /// aqui mostraria na tela um dano que ele não dá.
        ///
        /// <b>⚠️ A cadência do jogador é config, e é a única entrada desta classe que não sai do
        /// jogo.</b> Não é preguiça: o intervalo entre golpes do jogador vive na <b>animação</b>,
        /// não no item — o <c>m_aiAttackInterval</c> existe no <c>ItemData</c>, mas é o campo que
        /// a <b>IA</b> usa para decidir quando atacar, e nas armas de jogador ele fica no default
        /// sem significar nada. Ler dali daria um número silenciosamente errado, que é pior que um
        /// número declaradamente aproximado.
        /// </summary>
        private static float GetPlayerDps(Player player)
        {
            ItemDrop.ItemData weapon = player.GetCurrentWeapon();
            float damage = weapon == null ? 0f : weapon.GetDamage().GetTotalDamage();

            if (Ki.KiManager.IsEnabled)
            {
                damage += BattlePower.GetPunchDamageBonus(player);
            }

            return damage / Mathf.Max(0.05f, SaiyaheimConfig.RatingPlayerHitInterval.Value);
        }

        /// <summary>
        /// Média do dano por segundo das armas do inventário da criatura.
        ///
        /// ⚠️ Devolve zero para bicho sem arma nenhuma no inventário, e isso é resposta legítima
        /// — não um erro a mascarar. Existe criatura passiva no jogo (cervo, corvo) que de fato
        /// não ataca, e o poder de luta dela ser só HP é a leitura certa.
        /// </summary>
        private static float GetCreatureDps(Character character)
        {
            if (!(character is Humanoid humanoid))
            {
                return 0f;
            }

            Inventory inventory = humanoid.GetInventory();
            if (inventory == null)
            {
                return 0f;
            }

            float total = 0f;
            int count = 0;

            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item == null || !item.IsWeapon())
                {
                    continue;
                }

                // O piso existe porque intervalo zero é divisão por zero, e o campo é dado de
                // asset: nada garante que todo prefab do jogo o preencheu.
                float interval = Mathf.Max(0.05f, item.m_shared.m_aiAttackInterval);

                total += item.GetDamage().GetTotalDamage() / interval;
                count++;
            }

            return count == 0 ? 0f : total / count;
        }

        /// <summary>
        /// O multiplicador de dano das variantes estreladas, copiado do
        /// <c>Attack.GetLevelDamageFactor</c>: <c>1 + (nível−1) · 0,5</c>. Uma estrela bate 1,5×,
        /// duas batem 2×.
        ///
        /// Copiado e não chamado porque o método do jogo é <c>private</c> e lê o
        /// <c>m_character</c> do próprio <c>Attack</c> — instância que aqui não existe. É número
        /// do jogo, não de balanceamento, então não vai para config; se o Valheim mudar,
        /// esta linha muda junto.
        /// </summary>
        private static float GetLevelDamageFactor(Character character)
        {
            return 1f + Mathf.Max(0, character.GetLevel() - 1) * 0.5f;
        }
    }
}
