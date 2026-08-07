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
    /// paga e é pago pelo <c>PowerSkill</c> (Battle Power) como o soco: o dano sai do power level
    /// de combate, e acertar dá XP de Battle Power sozinho — o <c>DamageXpPatch</c> credita por
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

            float required = Config.MinBattlePower.Value;
            if (required > 0f && PowerSkill.GetLevel(player) < required)
            {
                return $"Battle Power {required:0} required for {DisplayName}.";
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
            return DamageFor(PowerLevel.GetCombatRaw(player));
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
        /// O custo de um disparo. Fixo — não escala com poder nem com forma, e é a decisão que
        /// [[Ataques de Ki]] registra como provisória.
        /// </summary>
        internal float GetKiCost()
        {
            return Mathf.Max(0f, Config.KiCost.Value);
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
