using Saiyaheim.Power;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Attacks
{
    /// <summary>
    /// Um ataque de ki: os números dele, a trava dele e o cooldown dele.
    ///
    /// <b>Ataque é dado, não código.</b> Mesma aposta que as <c>Transformation</c> fizeram e que já
    /// se pagou: a escada inteira é este objeto instanciado N vezes no
    /// <see cref="KiAttackRegistry"/>, e nenhuma outra classe do mod sabe quantos ataques existem
    /// nem qual está selecionado.
    ///
    /// <b>Não tem skill própria.</b> Diferente da forma, que tem a maestria dela, o ataque de ki
    /// paga e é pago pelo <c>PowerSkill</c> (Power Level) como o soco: o dano sai do battle power
    /// de combate, e acertar dá XP de Power Level sozinho — o <c>DamageXpPatch</c> credita por
    /// <c>hit.GetAttacker()</c>, e o projétil carrega o jogador como atacante. Uma skill própria
    /// seria uma quarta curva de progressão para calibrar, e está em [[Em Aberto]] justamente
    /// porque ainda não se sabe se ela é necessária.
    /// </summary>
    internal class KiAttack
    {
        /// <summary>Identificador estável, usado no console e no <c>.cfg</c>.</summary>
        internal string Id { get; }

        /// <summary>Nome que o jogador lê na tela ao trocar de ataque.</summary>
        internal string DisplayName { get; }

        /// <summary>Os números deste ataque, ligados à seção própria dele no <c>.cfg</c>.</summary>
        internal SaiyaheimConfig.KiAttackConfig Config { get; }

        /// <summary>
        /// Ignora as travas <b>deste</b> ataque. Mesmo desenho, mesmo motivo e mesmas ressalvas do
        /// <c>Transformation.IgnoreLocks</c>: existe para não sujar o mundo com <c>setglobalkey</c>
        /// só para testar, é por ataque e não um interruptor geral, e <b>não é persistido</b> — uma
        /// trava desligada que sobrevivesse ao restart produziria um playtest mentindo em silêncio.
        /// </summary>
        internal bool IgnoreLocks { get; set; }

        /// <summary>
        /// Instante (<c>Time.time</c>) a partir do qual este ataque pode disparar de novo.
        ///
        /// Estado do <b>jogador local</b>, e só dele. Isso é aceitável aqui pelo mesmo motivo que
        /// não é aceitável em <c>TransformationRegistry.GetActive</c>: cooldown só é consultado
        /// para decidir se <i>este</i> cliente pode atirar agora. Ninguém pergunta o cooldown do
        /// vizinho — e no multiplayer cada cliente dispara o próprio projétil.
        /// </summary>
        internal float ReadyAt { get; set; }

        internal KiAttack(string id, string displayName, SaiyaheimConfig.KiAttackConfig config)
        {
            Id = id;
            DisplayName = displayName;
            Config = config;
        }

        internal bool IsUnlocked(Player player)
        {
            return GetLockReason(player) == null;
        }

        /// <summary>
        /// O que falta para este ataque destravar, em uma frase, ou null se já está destravado.
        ///
        /// Derivado, e não o contrário, pelo mesmo motivo do <c>Transformation.GetLockReason</c>:
        /// as duas travas falham por motivos diferentes e o jogador precisa saber qual.
        /// </summary>
        internal string GetLockReason(Player player)
        {
            if (player == null)
            {
                return $"{DisplayName} is not available.";
            }

            if (IgnoreLocks)
            {
                return null;
            }

            string bossLock = BossGate.DescribeLock(Config.RequiredGlobalKey.Value);
            if (bossLock != null)
            {
                return bossLock;
            }

            float required = Config.MinPowerLevel.Value;
            if (required > 0f && PowerSkill.GetLevel(player) < required)
            {
                return $"Power Level {required:0} required for {DisplayName}.";
            }

            return null;
        }

        /// <summary>
        /// O dano de um disparo agora.
        ///
        /// <code>dano = base + fracao * poder_de_combate</code>
        ///
        /// Aditivo, como todo o resto do mod. Lê o poder de <b>combate</b> — o mesmo do soco e da
        /// armadura — e não o linear, então o termo de fim de jogo e o multiplicador da forma ativa
        /// entram nos dois de uma vez, sem um segundo lugar onde a forma multiplica coisas.
        /// </summary>
        internal float GetDamage(Player player)
        {
            return DamageFor(BattlePower.GetCombatRaw(player));
        }

        /// <summary>
        /// O dano a um poder de combate hipotético. Existe para o <c>saiya_blast</c> imprimir o
        /// antes e o depois da forma sem ter que transformar para medir.
        /// </summary>
        internal float DamageFor(float combatPower)
        {
            return Mathf.Max(0f,
                Config.DamageBase.Value + Config.DamageFromPower.Value * Mathf.Max(0f, combatPower));
        }

        /// <summary>
        /// Quantos projéteis um toque de tecla solta. 1 é o tiro único do ki blast.
        ///
        /// É o que faz o Kamehameha ser um feixe sem o jogo ter feixe: <c>projectile_beam</c> não
        /// é raio sustentado — foi validado na tela em 2026-09-07 —, e o próprio Yagluth desenha o
        /// feixe dele encadeando projéteis. Ver <c>KiBeam</c>.
        /// </summary>
        internal int GetBeamCount()
        {
            return Mathf.Max(1, Config.BeamCount.Value);
        }

        /// <summary>
        /// Quantos projéteis saem numa carga de <paramref name="ratio"/>, de 0 a 1. É o
        /// <see cref="GetBeamCount"/> proporcional, com piso de 1.
        ///
        /// <b>Arredonda, não trunca.</b> Truncar faria a carga cheia render <c>BeamCount</c> só no
        /// instante exato de 1,0 — e como o jogador solta a tecla um frame depois de a barra
        /// encher, ele perderia o último projétil quase sempre.
        /// </summary>
        internal int GetBeamCount(float ratio)
        {
            if (!IsCharged)
            {
                return GetBeamCount();
            }

            return Mathf.Clamp(
                Mathf.RoundToInt(GetBeamCount() * Mathf.Clamp01(ratio)), 1, GetBeamCount());
        }

        /// <summary>Este ataque é segurado para carregar, em vez de disparar no toque?</summary>
        internal bool IsCharged => Config.ChargeTime.Value > 0f;

        /// <summary>Segundos de tecla segurada até a carga cheia.</summary>
        internal float GetChargeTime()
        {
            return Mathf.Max(0f, Config.ChargeTime.Value);
        }

        /// <summary>
        /// O ki que este ataque consome por segundo de carregamento.
        ///
        /// <b>Derivado, e não uma chave própria.</b> É o custo de um projétil vezes quantos
        /// projéteis a carga produz por segundo — ou seja, <b>o jogador paga cada projétil no
        /// instante em que a carga o produz</b>. Uma chave separada seria um segundo lugar onde o
        /// preço do ataque mora, e os dois sairiam de sincronia no dia em que o
        /// <c>BeamCount</c> mudasse.
        ///
        /// Consequência: segurar metade do tempo custa metade e entrega metade. A relação que o
        /// jogador consegue prever continua sendo uma linha reta — só mudou <i>quando</i> ele paga.
        /// </summary>
        internal float GetChargeKiPerSecond()
        {
            float time = GetChargeTime();

            return time <= 0f ? 0f : GetKiCost() / time;
        }

        /// <summary>Fração de carga abaixo da qual soltar a tecla não dispara.</summary>
        internal float GetMinChargeRatio()
        {
            return Mathf.Clamp01(Config.MinChargeRatio.Value);
        }

        /// <summary>
        /// A escala dos projéteis numa carga de <paramref name="ratio"/>: da
        /// <c>ChargeMinScale</c> até a <c>ProjectileScale</c> cheia.
        ///
        /// <b>Só o visual.</b> O projétil do jogo colide pelo colisor dele, que o
        /// <c>localScale</c> acompanha — mas o dano não muda, e é isso que mantém a carga uma
        /// escolha de <i>quanto</i> feixe e não de quanto dano por acerto. Ver a doc do
        /// <c>ChargeTime</c>.
        /// </summary>
        internal float GetProjectileScale(float ratio)
        {
            float full = Config.ProjectileScale.Value;

            if (!IsCharged)
            {
                return full;
            }

            return full * Mathf.Lerp(
                Mathf.Clamp01(Config.ChargeMinScale.Value), 1f, Mathf.Clamp01(ratio));
        }

        /// <summary>Segundos entre um projétil do feixe e o seguinte.</summary>
        internal float GetBeamInterval()
        {
            return Mathf.Max(0.01f, Config.BeamInterval.Value);
        }

        /// <summary>Quanto tempo o feixe leva do primeiro projétil ao último.</summary>
        internal float GetBeamDuration()
        {
            return (GetBeamCount() - 1) * GetBeamInterval();
        }

        /// <summary>O dano do feixe inteira, se tudo acertar. É o número que se calibra.</summary>
        internal float GetTotalDamage(Player player)
        {
            return GetDamage(player) * GetBeamCount();
        }

        /// <summary>
        /// O custo de <b>um projétil</b>. Fixo — não escala com poder nem com forma, e é a decisão
        /// que [[Ataques de Ki]] registra como provisória.
        /// </summary>
        internal float GetKiCostPerProjectile()
        {
            return Mathf.Max(0f, Config.KiCost.Value);
        }

        /// <summary>
        /// O custo de <b>um toque de tecla</b>: o de um projétil vezes o tamanho do feixe.
        ///
        /// É o que o mod cobra, e de uma vez, no primeiro projétil. Cobrar projétil a projétil
        /// deixaria a barra acabar no meio de um feixe já saindo da mão — meio feixe pela qual o
        /// jogador não escolheu pagar, e um custo que ele não consegue prever antes de apertar.
        /// </summary>
        internal float GetKiCost()
        {
            return GetKiCostPerProjectile() * GetBeamCount();
        }

        /// <summary>
        /// O custo de um disparo de <paramref name="projectiles"/> projéteis.
        ///
        /// É o que o carregamento cobra, e é por isso que segurar não gasta nada por si: o preço
        /// é do que <b>sai</b>, não do tempo com o dedo na tecla. Meia carga custa metade, e o
        /// jogador consegue prever isso sem olhar relógio nenhum.
        /// </summary>
        internal float GetKiCost(int projectiles)
        {
            return GetKiCostPerProjectile() * Mathf.Max(1, projectiles);
        }

        /// <summary>Segundos que faltam para poder atirar de novo. 0 quer dizer pronto.</summary>
        internal float GetRemainingCooldown()
        {
            return Mathf.Max(0f, ReadyAt - Time.time);
        }

        /// <summary>
        /// Arma o cooldown deste ataque e o piso comum a todos.
        ///
        /// O piso existe porque, sem ele, trocar de ataque seria a maneira mais barata de burlar
        /// cooldown: dois ataques de 1 s alternados dariam um disparo a cada frame.
        /// </summary>
        internal void StartCooldown()
        {
            ReadyAt = Time.time + Mathf.Max(0f, Config.Cooldown.Value);
        }
    }
}
