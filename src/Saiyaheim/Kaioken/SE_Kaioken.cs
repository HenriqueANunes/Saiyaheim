using System.Globalization;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Kaioken
{
    /// <summary>
    /// O tier de Kaioken ligado: o <c>StatusEffect</c> que representa estar em Kaioken (etapa 14).
    ///
    /// <b>Faz três coisas.</b> Cobra stamina e vida por segundo, pela margem; acelera o movimento
    /// no chão; e mostra na barra de status o tier e, com a margem negativa, quanto de vida está
    /// indo embora. O <b>poder</b> não mora aqui: quem multiplica é o
    /// <c>BattlePower.GetKiCombatRaw</c>, que consulta o <see cref="KaiokenRegistry"/> — o mesmo
    /// desenho da forma, e pelo mesmo motivo (ver <c>SE_Transformation</c>). O teto de ki maior sai
    /// do <c>KiManager.MaxFor</c>.
    ///
    /// <b>Quem desliga é o <see cref="KaiokenManager"/></b>, nunca este efeito: tirar um status
    /// effect de dentro do <c>SEMan.Update</c> corromperia o laço dele. Aqui só se cobra; vida no
    /// piso e stamina no zero são lidas lá.
    ///
    /// Os números são lidos da config a cada tick, e não copiados para campos do <c>SE_Stats</c>:
    /// o <c>Clone()</c> congelaria o <c>.cfg</c> lido na criação do template.
    /// </summary>
    internal class SE_Kaioken : SE_Stats
    {
        /// <summary>O tier deste efeito. Sobrevive ao <c>Clone()</c>, que é um <c>MemberwiseClone</c>.</summary>
        private KaiokenTier _tier;

        internal KaiokenTier Tier => _tier;

        /// <summary>Vida por segundo do último tick, para o texto do ícone.</summary>
        private float _lastHealthPerSecond;

        internal static SE_Kaioken CreateTemplate(KaiokenTier tier)
        {
            var effect = CreateInstance<SE_Kaioken>();

            // O nome do objeto é a identidade: StatusEffect.NameHash() lê UnityEngine.Object.name.
            effect.name = tier.ObjectName;
            effect.m_name = tier.DisplayName;
            effect.m_tooltip = "Battle power multiplied, faster on your feet and in melee, more room for ki. " +
                               "Burns stamina — and health while the tier is beyond your limit.";
            effect._tier = tier;

            // Com ícone, ao contrário da forma: a barra de status é o indicador de tier até a HUD
            // própria existir, e o texto embaixo avisa quando a vida está drenando.
            effect.m_icon = IconLoader.LoadOptional("kaioken");

            // Permanente. Quem tira é o KaiokenManager.
            effect.m_ttl = 0f;

            return effect;
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);

            if (_tier == null || !(m_character is Player player) || player != Player.m_localPlayer)
            {
                return;
            }

            // Mesma regra da forma: a tela de carregamento do portal não é jogo.
            if (player.IsTeleporting())
            {
                return;
            }

            // FixedUpdate: já é tick fixo, como o dreno da forma.
            float margin = _tier.GetMargin(player);

            // UseStamina rearma o atraso de regeneração a cada chamada (Player.RPC_UseStamina), e o
            // piso garante que o dreno nunca é zero — então a regeneração de stamina fica parada
            // enquanto o Kaioken estiver ligado, sem patch. É a decisão de 2026-10-04: o stamina/s
            // do tier é o custo líquido.
            float stamina = _tier.GetStaminaPerSecond(margin) * dt;
            if (stamina > 0f)
            {
                player.UseStamina(stamina);
            }

            // SetHealth e não UseHealth: aquele pisca a tela de dano a cada chamada, e isto roda
            // todo FixedUpdate. Nem ApplyDamage, que contaria como dano recebido e pagaria XP de
            // Power Level por se machucar sozinho. Nunca mata: o manager desliga no piso, e o
            // corte aqui segura o último tick antes disso.
            _lastHealthPerSecond = _tier.GetHealthPerSecond(margin);
            float health = _lastHealthPerSecond * dt;
            if (health > 0f)
            {
                player.SetHealth(Mathf.Max(1f, player.GetHealth() - health));
            }
        }

        /// <summary>
        /// Movimento no chão e nadando. <b>Fora do voo</b>: o <c>UpdateFlying</c> vanilla também passa
        /// pelos modificadores de status, e o voo tem a própria velocidade e o próprio teto
        /// (<c>FlightMaxSpeed</c>). Somar os dois era o cuidado anotado no design.
        /// </summary>
        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            base.ModifySpeed(baseSpeed, ref speed, character, dir);

            if (_tier == null || character == null || character.IsFlying())
            {
                return;
            }

            speed += baseSpeed * _tier.GetMoveSpeedBonus();
        }

        /// <summary>O texto embaixo do ícone: o dreno de vida, só quando há.</summary>
        public override string GetIconText()
        {
            return _lastHealthPerSecond > 0.05f
                ? "-" + _lastHealthPerSecond.ToString("0.#", CultureInfo.InvariantCulture) + " hp/s"
                : "";
        }
    }
}
