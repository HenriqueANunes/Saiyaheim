using System.Collections.Generic;
using Saiyaheim.Ki;

namespace Saiyaheim.Transformations
{
    /// <summary>
    /// A forma ativa: o <c>StatusEffect</c> que representa estar transformado.
    ///
    /// <b>Ele faz quatro coisas, e só.</b> Drena ki por segundo, levanta o limite de peso do
    /// inventário, aplica a resistência a contusão da forma e garante a cura mínima, quando ela
    /// tem essas chaves. O resto da cura passiva do SSJ God <b>não</b> passa por aqui: ela é
    /// intervalo e piso do multiplicador, e nenhum dos dois cabe num modificador de
    /// <c>SE_Stats</c> — ver <c>HealthRegenPatch</c>.
    ///
    /// O XP de maestria NÃO sai daqui desde 2026-09-20 — ele vem do dano trocado, pelo
    /// <c>DamageXpPatch</c>. O <b>poder</b> da transformação
    /// não está aqui: é o <c>BattlePower.GetKiCombatRaw</c> que consulta o
    /// <see cref="TransformationRegistry"/> e multiplica.
    ///
    /// <b>Por que o multiplicador não mora neste arquivo.</b> Ele não é um modificador de dano —
    /// é um modificador de <i>battle power</i>, e o battle power alimenta soco, armadura, block
    /// power, velocidade de voo e o número exibido. Multiplicar na fonte faz os cinco andarem
    /// juntos de graça; multiplicar aqui, via <c>ModifyAttack</c>, só alcançaria o dano e ainda
    /// deixaria o resultado dependente da <b>ordem</b> em que os status effects foram adicionados
    /// — o <c>SEMan.ModifyAttack</c> percorre a lista na ordem de inserção, e o
    /// <c>SE_KiBody</c> já soma o poder cru ali. Com a multiplicação na fonte, existe um único
    /// efeito mexendo no golpe e a armadilha de ordem deixa de existir.
    ///
    /// <b>Sem custo de ativação</b> (decisão de 2026-08-02): o preço da forma é o dreno, e só ele.
    /// Um custo pontual castigaria alternar a forma — que é exatamente o gesto que o jogo quer
    /// ensinar quando a barra está apertada.
    ///
    /// Herda de <c>SE_Stats</c> sem usar nenhum modificador dele, e de propósito: dano, velocidade
    /// e regeneração vêm todos do battle power agora. Nem a resistência usa o <c>m_mods</c>, que
    /// congelaria no <c>Clone()</c>: ela sobrescreve o <c>ModifyDamageMods</c> e lê a config a
    /// cada golpe ([[Dano e Resistências]]).
    /// </summary>
    internal class SE_Transformation : SE_Stats
    {
        /// <summary>
        /// A forma que este efeito representa.
        ///
        /// Sobrevive ao <c>Clone()</c> porque o <c>StatusEffect.Clone</c> do Valheim é um
        /// <c>MemberwiseClone</c>, não um <c>Object.Instantiate</c>: campos que a Unity nem
        /// serializaria — como uma referência a objeto C# comum — são copiados do mesmo jeito.
        /// Confirmado na decompilação de <c>StatusEffect</c>.
        /// </summary>
        private Transformation _form;

        internal Transformation Form => _form;

        internal static SE_Transformation CreateTemplate(Transformation form)
        {
            var effect = CreateInstance<SE_Transformation>();

            // O nome do objeto é a identidade: StatusEffect.NameHash() lê UnityEngine.Object.name,
            // e é por ele que o SEMan acha (e o registry reconhece) a forma.
            effect.name = form.ObjectName;
            effect.m_name = form.DisplayName;
            effect.m_tooltip = "Your battle power is multiplied and you carry more. " +
                           "Ki drains while you hold the form.";
            effect._form = form;

            // Sem ícone: SEMan.GetHUDStatusEffects filtra por m_icon, então a forma não ocupa
            // espaço na barra de status. Arte é polimento da etapa 11.
            effect.m_icon = null;

            // m_ttl = 0 é permanente. Quem tira é o TransformationManager: tecla, ki no zero, ki
            // desligado ou um estado incompatível.
            effect.m_ttl = 0f;

            return effect;
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);

            if (_form == null || !(m_character is Player player))
            {
                return;
            }

            // A tela de carregamento do portal ou da dungeon dura de 2 a 15 segundos e não é
            // jogo: cobrar ali derrubaria a forma de quem entrou com pouco ki sem ter feito nada.
            if (player.IsTeleporting())
            {
                return;
            }

            // UpdateStatusEffect vem do FixedUpdate, então isto já é tick fixo — a regra do projeto
            // de nunca cobrar recurso por frame está atendida sem acumulador próprio.
            //
            // Drain e não TryConsume: o dreno não é tudo ou nada. Chegar a zero é o gatilho de
            // destransformação, e quem percebe isso é o TransformationManager — remover um status
            // effect de dentro do SEMan.Update corromperia o laço dele, que cacheia o Count antes
            // de iterar. Mesma divisão do voo.
            KiManager.Drain(_form.GetKiDrainPerSecond(player) * dt);

            ApplyHealthRegenMinimum(player, dt);
        }

        /// <summary>Intervalo da cura mínima. Um tique por segundo lê como regeneração contínua.</summary>
        private const float RegenMinimumInterval = 1f;

        /// <summary>O relógio da cura passiva da vanilla, constante literal no <c>Player.UpdateFood</c>.</summary>
        private const float VanillaRegenInterval = 10f;

        private float _regenMinimumTimer;

        /// <summary>
        /// Cura mínima da forma (<c>HealthRegenMinimum</c>), hoje só no SSJ God: a forma
        /// multiplica a cura da comida, e sem comida não sobra nada para multiplicar.
        ///
        /// <b>Piso, e não soma.</b> A cada segundo cura só a diferença entre o mínimo e o que a
        /// comida já está curando por segundo — com o relógio mais rápido da forma e o
        /// multiplicador compartilhado do clima. Bem alimentado, isto não faz nada.
        ///
        /// A conta da comida repete a do <c>Player.UpdateFood</c> (soma do <c>m_foodRegen</c>
        /// vezes o <c>SEMan.ModifyHealthRegen</c>, a cada 10 segundos). Errar aqui só desloca o
        /// ponto em que o piso deixa de agir; não dá cura em dobro além do mínimo.
        /// </summary>
        private void ApplyHealthRegenMinimum(Player player, float dt)
        {
            float minimum = _form.GetHealthRegenMinimum();
            if (minimum <= 0f || player != Player.m_localPlayer || player.IsDead())
            {
                _regenMinimumTimer = 0f;
                return;
            }

            _regenMinimumTimer += dt;
            if (_regenMinimumTimer < RegenMinimumInterval)
            {
                return;
            }

            _regenMinimumTimer -= RegenMinimumInterval;

            float food = 0f;
            foreach (Player.Food entry in player.GetFoods())
            {
                if (entry?.m_item?.m_shared != null)
                {
                    food += entry.m_item.m_shared.m_foodRegen;
                }
            }

            float foodPerSecond = 0f;
            if (food > 0f)
            {
                float multiplier = 1f;
                player.GetSEMan().ModifyHealthRegen(ref multiplier);
                foodPerSecond = food * multiplier * _form.GetHealthRegenSpeed() / VanillaRegenInterval;
            }

            float missing = (minimum - foodPerSecond) * RegenMinimumInterval;
            if (missing > 0f)
            {
                // Sem o número flutuante: um "+1" por segundo poluiria a tela.
                player.Heal(missing, false);
            }
        }

        /// <summary>
        /// Levanta o limite de peso do inventário enquanto a forma está ativa.
        ///
        /// <b>API nativa, zero patch Harmony.</b> <c>Player.GetMaxCarryWeight</c> chama
        /// <c>SEMan.ModifyMaxCarryWeight</c>, que percorre os efeitos ativos — o mesmo caminho por
        /// onde o Megingjord passa. Tudo que lê o limite (a barra de peso do inventário, o
        /// "encumbered", o que dá para pegar do chão) vem de graça e continua certo se o Valheim
        /// mexer nas contas.
        ///
        /// <b>Aqui e não no <c>m_addMaxCarryWeight</c> do <c>SE_Stats</c></b>, que existe e faria
        /// exatamente isto: aquele campo é copiado do template no <c>Clone()</c> e congelaria o
        /// valor do <c>.cfg</c> lido na inicialização. Lendo a config a cada chamada, mexer no
        /// número com o jogo aberto vale na hora — que é o ciclo de playtest inteiro deste mod.
        ///
        /// <b>Some junto com a forma</b>, e isso é intencional: destransformar carregando mais do
        /// que o limite normal deixa o jogador sobrecarregado na hora. É o preço de usar a forma
        /// como carroça, e o dreno já avisa que ela vai acabar.
        ///
        /// ⚠️ Efeito colateral que o <c>.cfg</c> explica e vale repetir: carga é uma <b>fração do
        /// limite</b> (<c>FlightStats.GetWeightLoad</c>), então um limite maior faz a mesma carga
        /// pesar menos no voo e pagar menos XP de Power Level pelo <c>XpWeightBonus</c>.
        /// </summary>
        public override void ModifyMaxCarryWeight(float baseLimit, ref float limit)
        {
            base.ModifyMaxCarryWeight(baseLimit, ref limit);

            if (_form == null)
            {
                return;
            }

            limit += _form.GetCarryWeightBonus();
        }

        /// <summary>
        /// Resistência a contusão da forma, hoje só no SSJ God.
        ///
        /// <b>API nativa, zero patch Harmony.</b> <c>Character.GetDamageModifiers</c> passa por
        /// <c>SEMan.ApplyDamageMods</c>, que chama isto em cada efeito ativo — o mesmo caminho das
        /// meads de resistência. O golpe recebido, o bloqueio e o <c>saiya_block</c> leem dali.
        ///
        /// <b>Aqui e não no <c>m_mods</c> do <c>SE_Stats</c></b>, pelo mesmo motivo do peso: a
        /// lista é copiada do template no <c>Clone()</c> e congelaria o <c>.cfg</c>.
        /// </summary>
        public override void ModifyDamageMods(ref HitData.DamageModifiers modifiers)
        {
            base.ModifyDamageMods(ref modifiers);

            if (_form == null)
            {
                return;
            }

            HitData.DamageModifier blunt = _form.GetBluntResistance();

            if (blunt != HitData.DamageModifier.Normal)
            {
                // Lista reaproveitada: isto roda a cada golpe recebido e a cada leitura do
                // bloqueio, e o DamageModifiers.Apply só aceita lista.
                _bluntMods[0] = new HitData.DamageModPair { m_type = HitData.DamageType.Blunt, m_modifier = blunt };
                modifiers.Apply(_bluntMods);
            }
        }

        private static readonly List<HitData.DamageModPair> _bluntMods =
            new List<HitData.DamageModPair> { default(HitData.DamageModPair) };

        // A maestria NAO e' paga aqui desde 2026-09-20. Ela vinha do tempo em forma, acumulado
        // neste efeito; agora vem do dano trocado dentro dela, e quem mede dano e' o
        // DamageXpPatch. Este efeito voltou a ter uma conta so: o dreno.
    }
}
