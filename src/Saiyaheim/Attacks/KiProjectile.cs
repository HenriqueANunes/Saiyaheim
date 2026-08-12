using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Attacks
{
    /// <summary>
    /// Põe a bola de ki no mundo: clona um projétil do jogo, desarma o que ele trouxe de fábrica e
    /// entrega um <c>HitData</c> nosso.
    ///
    /// <b>Por que não um item de arma.</b> A alternativa era registrar uma arma custom com
    /// <c>m_attackProjectile</c>, o que daria animação e mira nativas de graça — mas obrigaria a
    /// equipar alguma coisa, e o desenho inteiro do modo ki é <i>o jogador está pelado</i>. O preço
    /// aceito é que a mão não anima; isso é polimento da etapa 11.
    ///
    /// <b>O que sai de graça por ser um projétil do jogo:</b>
    /// <list type="bullet">
    /// <item><b>Multiplayer.</b> <c>Projectile</c> carrega <c>ZNetView</c>, então o clone cria ZDO e
    /// replica sozinho. É o oposto do que <c>AttachedEffect</c> faz, e de propósito: lá o efeito é
    /// visual e local e o <c>m_forceDisableInit</c> impede a ZDO; aqui a ZDO <b>é</b> a
    /// entrega.</item>
    /// <item><b>XP de Battle Power.</b> O <c>Projectile</c> chama <c>hitData.SetAttacker(m_owner)</c>
    /// no impacto, e o <c>DamageXpPatch</c> credita por atacante. Nada a fazer.</item>
    /// <item><b>Não acerta quem atirou.</b> <c>IsValidTarget</c> recusa o próprio dono, e recusa
    /// outros jogadores quando o PvP do jogo está desligado.</item>
    /// </list>
    ///
    /// ⚠️ <b>Prefab de projétil não é só o visual.</b> Ele traz dano próprio, status effect (o de
    /// fogo queima o alvo), AoE e coisas para instanciar no impacto. É a mesma lição que a aura já
    /// cobrou uma vez em [[Efeitos Visuais]] — o prefab não faz só o que o nome diz. Ver
    /// <see cref="Defuse"/>.
    /// </summary>
    internal static class KiProjectile
    {
        /// <summary>
        /// Metros à frente do ponto de origem em que o projétil nasce. Não é balanceamento: é
        /// espaço para o clone não nascer dentro do próprio corpo do jogador, onde a checagem de
        /// colisão dele começaria encostada na câmera.
        /// </summary>
        private const float SpawnClearance = 0.4f;

        /// <summary>
        /// Dispara. Devolve false <b>sem ter gasto nada</b> quando não deu — quem chama só cobra o
        /// ki depois de o projétil existir, para que um nome de prefab errado no <c>.cfg</c> não
        /// coma a barra do jogador em silêncio.
        /// </summary>
        internal static bool Fire(Player player, KiAttack attack)
        {
            if (player == null || attack == null || ZNetScene.instance == null)
            {
                return false;
            }

            string prefabName = attack.Config.ProjectilePrefab.Value;
            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                SaiyaheimPlugin.Log.LogWarning(
                    $"Ki attack '{attack.Id}': projectile prefab '{prefabName}' does not exist. " +
                    "Check the name against the list in the docs.");
                return false;
            }

            // A mira é a direção do olhar, que no jogador é a da câmera — o mesmo GetAimDir que as
            // armas de projétil do jogo usam.
            Vector3 aim = player.GetLookDir().normalized;
            if (aim == Vector3.zero)
            {
                return false;
            }

            Vector3 spawnPoint = GetOrigin(player) + aim * SpawnClearance;

            GameObject instance = Object.Instantiate(prefab, spawnPoint, Quaternion.LookRotation(aim));

            Projectile projectile = instance.GetComponent<Projectile>()
                                   ?? instance.GetComponentInChildren<Projectile>();

            if (projectile == null)
            {
                // Prefab existe mas não é projétil: mais provável ser um nome de efeito digitado no
                // lugar do de projétil do que um bug do jogo. Destruir para não deixar lixo no mundo.
                SaiyaheimPlugin.Log.LogWarning(
                    $"Ki attack '{attack.Id}': prefab '{prefabName}' has no Projectile component.");
                Object.Destroy(instance);
                return false;
            }

            Defuse(projectile, attack);

            float scale = attack.Config.ProjectileScale.Value;
            if (!Mathf.Approximately(scale, 1f))
            {
                instance.transform.localScale *= scale;
            }

            AttachedEffect.ApplyTint(instance, attack.Config.ProjectileColor.Value);

            float damage = attack.GetDamage(player);
            float speed = attack.Config.ProjectileSpeed.Value;

            // hitNoise -1 mantém o do prefab: quanto barulho o tiro faz para a IA é característica
            // do efeito, não do mod.
            projectile.Setup(player, aim * speed, -1f, BuildHit(player, attack, damage), null, null);

            SaiyaheimPlugin.LogVerbose(
                $"Ki attack '{attack.Id}': {damage:0.#} slash, {attack.GetKiCost():0.#} ki, " +
                $"{speed:0.#} m/s for {attack.Config.ProjectileLifetime.Value:0.##}s " +
                $"({speed * attack.Config.ProjectileLifetime.Value:0} m range).");

            return true;
        }

        /// <summary>
        /// De onde o tiro sai: a mão direita, se o jogador tiver o esqueleto montado; senão, os
        /// olhos.
        ///
        /// A mão é o gesto certo e o <c>VisEquipment.m_rightHand</c> é público — mas o caminho até
        /// ele importa: <c>Humanoid.m_visEquipment</c> é <b>protected</b>, e a assembly publicizada
        /// deixaria compilar um acesso que estoura <c>FieldAccessException</c> em runtime. O
        /// <c>GetComponent</c> chega no mesmo objeto por caminho público — é literalmente o que o
        /// <c>Humanoid.Awake</c> faz.
        /// </summary>
        private static Vector3 GetOrigin(Player player)
        {
            VisEquipment vis = player.GetComponent<VisEquipment>();

            if (vis != null && vis.m_rightHand != null)
            {
                return vis.m_rightHand.position;
            }

            return player.GetEyePoint();
        }

        /// <summary>
        /// Tira do clone tudo o que é do prefab e não do ataque.
        ///
        /// ⚠️ Mexe na <b>instância</b>, nunca no prefab. É seguro porque o <c>Instantiate</c> copia
        /// os campos serializados do componente — ao contrário do <c>ItemData.m_shared</c>, que é
        /// compartilhado e onde a mesma escrita vazaria para todo mundo que usa o item.
        ///
        /// O status effect não aparece aqui porque o <c>Setup</c> já o apaga sozinho ao receber um
        /// <c>HitData</c> com hash zero — é como a bola de fogo do Dvergr para de incendiar o alvo.
        /// </summary>
        private static void Defuse(Projectile projectile, KiAttack attack)
        {
            // O que o prefab instancia no impacto: a poça de fogo do Dvergr, estilhaços, o que for.
            // Sai por duas razões: o ataque básico não tem área, e o dano daquilo não passa pelo
            // power level — seria dano fora da fórmula, invisível para qualquer cálculo do mod.
            //
            // ⚠️ E há uma armadilha se ficar: o Setup ZERA o dano do projétil quando o prefab tem
            // m_spawnOnHit e m_onlySpawnedProjectilesDealDamage — ou seja, o tiro sairia sem dano
            // nenhum, sem erro nenhum. Por isso isto vem ANTES do Setup.
            projectile.m_spawnOnHit = null;
            projectile.m_randomSpawnOnHit.Clear();
            projectile.m_respawnItemOnHit = false;
            projectile.m_aoe = 0f;

            projectile.m_ttl = Mathf.Max(0.1f, attack.Config.ProjectileLifetime.Value);
            projectile.m_gravity = Mathf.Max(0f, attack.Config.ProjectileGravity.Value);

            // Arrasto é o que faz um projétil perder velocidade no caminho e, com ela, alcance. Um
            // tiro de energia não desacelera; o que o apaga é o ttl acima.
            projectile.m_drag = 0f;
        }

        /// <summary>
        /// O golpe que o projétil vai aplicar.
        ///
        /// <b>Corte puro</b> — decisão de 2026-08-11, ver [[Ataques de Ki]]. Não é chave de
        /// config porque não é número de balanceamento: é o que o ataque <i>é</i>.
        /// (Era contusão até 2026-08-06; slash conta para stagger do mesmo jeito, então a troca
        /// muda só a resistência do alvo, não o ritmo do combate.)
        ///
        /// <b>Bloqueável e esquivável</b>, como qualquer projétil do jogo. Tirar isso faria o
        /// ataque ignorar em silêncio as duas defesas que o Valheim inteiro ensina, e um inimigo
        /// que não pode se defender de nada é mais barato que um inimigo forte.
        /// </summary>
        private static HitData BuildHit(Player player, KiAttack attack, float damage)
        {
            HitData hit = new HitData();

            hit.m_damage.m_slash = damage;
            hit.m_pushForce = Mathf.Max(0f, attack.Config.Knockback.Value);
            hit.m_blockable = true;
            hit.m_dodgeable = true;
            hit.SetAttacker(player);

            // Nenhuma skill vanilla: sem isto o projétil chamaria RaiseSkill na skill herdada do
            // prefab (Blood Magic, no caso do cajado Dvergr) e o ki treinaria a magia do jogo base.
            // O eixo de progressão daqui é o Battle Power, e ele já é pago pelo dano causado.
            hit.m_skill = Skills.SkillType.None;
            hit.m_skillRaiseAmount = 0f;

            // Zero apaga o status effect que o prefab trazia — ver Defuse.
            hit.m_statusEffectHash = 0;

            return hit;
        }
    }
}
