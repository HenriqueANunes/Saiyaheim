using Saiyaheim.Ki;

namespace Saiyaheim.Transformations
{
    /// <summary>
    /// A forma ativa: o <c>StatusEffect</c> que representa estar transformado.
    ///
    /// <b>Ele faz três coisas, e só.</b> Drena ki por segundo, paga XP de maestria pelo tempo
    /// segurando a forma e levanta o limite de peso do inventário. O <b>poder</b> da transformação
    /// não está aqui: é o <c>PowerLevel.GetKiCombatRaw</c> que consulta o
    /// <see cref="TransformationRegistry"/> e multiplica.
    ///
    /// <b>Por que o multiplicador não mora neste arquivo.</b> Ele não é um modificador de dano —
    /// é um modificador de <i>power level</i>, e o power level alimenta soco, armadura, block
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
    /// e regeneração vêm todos do power level agora. O que o <c>SE_Stats</c> ainda tem de útil
    /// para o futuro é o <c>m_mods</c> — resistência elemental por forma, que hoje está em aberto
    /// ([[Dano e Resistências]]) e, se entrar, entra preenchendo uma lista neste arquivo, sem
    /// patch Harmony nenhum.
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

        /// <summary>Segundos de forma ainda não convertidos em XP. Ver <see cref="FlushXp"/>.</summary>
        private float _pendingXpSeconds;

        internal Transformation Form => _form;

        internal static SE_Transformation CreateTemplate(Transformation form)
        {
            var effect = CreateInstance<SE_Transformation>();

            // O nome do objeto é a identidade: StatusEffect.NameHash() lê UnityEngine.Object.name,
            // e é por ele que o SEMan acha (e o registry reconhece) a forma.
            effect.name = form.ObjectName;
            effect.m_name = form.DisplayName;
            effect.m_tooltip = "Your power level is multiplied and you carry more. " +
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

            // UpdateStatusEffect vem do FixedUpdate, então isto já é tick fixo — a regra do projeto
            // de nunca cobrar recurso por frame está atendida sem acumulador próprio.
            //
            // Drain e não TryConsume: o dreno não é tudo ou nada. Chegar a zero é o gatilho de
            // destransformação, e quem percebe isso é o TransformationManager — remover um status
            // effect de dentro do SEMan.Update corromperia o laço dele, que cacheia o Count antes
            // de iterar. Mesma divisão do voo.
            KiManager.Drain(_form.GetKiDrainPerSecond(player) * dt);

            FlushXp(player, dt);
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
        /// pesar menos no voo e pagar menos XP de Battle Power pelo <c>XpWeightBonus</c>.
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

        public override void Stop()
        {
            // Sem isto, uma forma segurada por menos de um segundo nunca pagaria XP — e no começo
            // do jogo, com a barra pequena e o dreno cheio, formas curtas são a regra.
            FlushXp(m_character as Player, 0f, force: true);

            base.Stop();
        }

        /// <summary>
        /// Acumula o tempo transformado e converte em XP uma vez por segundo. Chamar
        /// <c>RaiseSkill</c> a cada passo de física seriam ~50 chamadas por segundo para o mesmo
        /// efeito. Mesmo padrão do <c>SE_Flight.FlushXp</c>.
        ///
        /// Quem reparte é o <see cref="TransformationRegistry.RaiseMastery"/>, e não este efeito:
        /// o tempo pago treina esta forma <b>e todos os degraus abaixo dela</b>, e quem sabe onde
        /// a forma cai na escada é o registry. Este arquivo só sabe o seu próprio degrau.
        /// </summary>
        private void FlushXp(Player player, float dt, bool force = false)
        {
            _pendingXpSeconds += dt;

            if (_pendingXpSeconds <= 0f || (!force && _pendingXpSeconds < 1f))
            {
                return;
            }

            if (_form != null)
            {
                TransformationRegistry.RaiseMastery(player, _form, _pendingXpSeconds);
            }

            _pendingXpSeconds = 0f;
        }
    }
}
