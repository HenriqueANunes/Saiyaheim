using System.Collections.Generic;
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

            // NÃO é a direção do olhar. Copiar o olhar — o que o GetAimDir do jogo faz, e por isso
            // o arco vanilla erra para baixo — deixaria o tiro numa reta paralela à da mira,
            // deslocada pela distância entre o olho e a mão. Ver KiAim.
            Vector3 origin = GetOrigin(player);
            Vector3 aim = KiAim.Resolve(player, origin);
            if (aim == Vector3.zero)
            {
                return false;
            }

            Vector3 spawnPoint = origin + aim * SpawnClearance;

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
        /// Imprime, com <c>VerboseLogging</c>, o que o prefab toca no impacto — e, dentro de cada
        /// efeito, o nome de cada emissor de partícula.
        ///
        /// Existe porque "o estouro é bom, o que sobra depois é que não" não se resolve olhando
        /// para a tela: o impacto é uma <b>lista</b> de prefabs, e cada prefab é uma árvore de
        /// emissores. A fumaça pode estar em qualquer um dos dois níveis, e nenhum deles aparece
        /// na tela com nome. Sem esta lista, escolher o que fica é adivinhação; com ela, o
        /// <c>ImpactEffect</c> e o <c>ImpactEffectStrip</c> apontam direto para a peça certa.
        /// </summary>
        private static void LogImpactEffects(Projectile projectile, KiAttack attack)
        {
            EffectList.EffectData[] effects = projectile.m_hitEffects?.m_effectPrefabs;
            if (effects == null || effects.Length == 0)
            {
                SaiyaheimPlugin.LogVerbose($"Ki attack '{attack.Id}': prefab has no impact effects.");
                return;
            }

            foreach (EffectList.EffectData effect in effects)
            {
                GameObject effectPrefab = effect?.m_prefab;
                if (effectPrefab == null)
                {
                    SaiyaheimPlugin.LogVerbose($"Ki attack '{attack.Id}': impact effect <none>.");
                    continue;
                }

                ParticleSystem[] emitters = effectPrefab.GetComponentsInChildren<ParticleSystem>(true);
                string[] names = new string[emitters.Length];
                for (int i = 0; i < emitters.Length; i++)
                {
                    names[i] = emitters[i].gameObject.name;
                }

                string state = effect.m_enabled ? string.Empty : " (disabled)";
                string emitterList = emitters.Length == 0
                    ? "no particle emitters"
                    : $"emitters: {string.Join(", ", names)}";

                // A luz entra no log porque ela e' o que pinta o terreno em volta do impacto — a
                // cor que mais denuncia o prefab emprestado. Se a contagem for zero e ainda assim
                // houver cor estranha na tela, ela vem de particula ou material, e o
                // ImpactColorTarget precisa ser Everything.
                Light[] lights = effectPrefab.GetComponentsInChildren<Light>(true);
                string lightList = lights.Length == 0
                    ? "no lights"
                    : $"{lights.Length} light(s)";

                SaiyaheimPlugin.LogVerbose(
                    $"Ki attack '{attack.Id}': impact effect '{effectPrefab.name}'{state} — " +
                    $"{emitterList}; {lightList}.");
            }
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

            ApplyLingerOnHit(projectile, attack);
            ApplyImpactEffect(projectile, attack);
        }

        /// <summary>
        /// Faz o projétil morrer no impacto, em vez de ficar por ali.
        ///
        /// <b>O que isto conserta não é o efeito de impacto.</b> É o rastro do próprio projétil:
        /// um prefab com <c>m_stayAfterHit*</c> continua existindo por <c>m_stayTTL</c> segundos
        /// depois de acertar — feito para a flecha ficar cravada na parede — e um prefab de
        /// magia leva junto o sistema de partículas dele. Resultado: o estouro acontece e some,
        /// e onde ele foi fica uma nuvem parada emitindo. É fumaça que vem do <i>tiro</i>, não do
        /// <i>impacto</i>, e nenhuma troca de <c>ImpactEffect</c> a alcança.
        ///
        /// O <c>m_stopEmittersOnHit</c> junto porque o par é que resolve: um impede novas
        /// partículas, o outro tira o objeto que as segurava. Sozinho, o primeiro ainda deixa
        /// terminar de morrer o que já estava no ar.
        /// </summary>
        private static void ApplyLingerOnHit(Projectile projectile, KiAttack attack)
        {
            SaiyaheimPlugin.LogVerbose(
                $"Ki attack '{attack.Id}': prefab lingers on hit — " +
                $"static {projectile.m_stayAfterHitStatic}, dynamic {projectile.m_stayAfterHitDynamic}, " +
                $"stayTTL {projectile.m_stayTTL:0.##}s, stopEmitters {projectile.m_stopEmittersOnHit}.");

            if (attack.Config.ProjectileLingerOnHit.Value)
            {
                return;
            }

            projectile.m_stayAfterHitStatic = false;
            projectile.m_stayAfterHitDynamic = false;
            projectile.m_stopEmittersOnHit = true;
        }

        /// <summary>
        /// Troca o que toca no impacto.
        ///
        /// <b>Não é o mesmo campo que o <see cref="Defuse"/> zera acima.</b> O
        /// <c>m_spawnOnHit</c> é o que o projétil <i>deixa no mundo</i> — a poça de fogo, um
        /// objeto de verdade, com dano. Isto aqui é o <c>m_hitEffects</c>: o estouro puramente
        /// visual e sonoro. Eram dois campos e o mod só mexia num, que é por que a bola de fogo
        /// continuava soltando fumaça mesmo já desarmada.
        ///
        /// ⚠️ Substituir <b>troca a lista inteira por uma nova</b> em vez de escrever dentro da
        /// que veio. O <c>EffectList</c> é uma classe <c>[Serializable]</c>, e ainda que o
        /// <c>Instantiate</c> a copie junto com o componente, escrever no array herdado é o tipo
        /// de coisa que vaza para o prefab se essa garantia mudar. Lista nova não tem como vazar.
        /// </summary>
        private static void ApplyImpactEffect(Projectile projectile, KiAttack attack)
        {
            LogImpactEffects(projectile, attack);

            string context = $"Ki attack '{attack.Id}'";
            string[] strip = StrippedEffect.ParseFilter(attack.Config.ImpactEffectStrip.Value);
            string tint = ResolveImpactColor(attack);
            bool lightsOnly = attack.Config.ImpactColorTarget.Value == ImpactTintTarget.Light;
            string effectName = attack.Config.ImpactEffect.Value?.Trim() ?? string.Empty;

            if (effectName.Equals("none", System.StringComparison.OrdinalIgnoreCase))
            {
                projectile.m_hitEffects = new EffectList();
                return;
            }

            // Vazio: o prefab manda — mas ainda passa pelo filtro abaixo, que é o ponto. Trocar o
            // efeito inteiro é a escolha grossa (ou fica tudo, ou não fica nada); o filtro é o que
            // deixa ficar com o clarão e o som de um estouro e jogar fora só a fumaça dele.
            if (effectName.Length == 0)
            {
                StripImpactEffects(projectile, strip, tint, lightsOnly, context);
                return;
            }

            GameObject effectPrefab = ZNetScene.instance.GetPrefab(effectName);
            if (effectPrefab == null)
            {
                // Deixa o do prefab no lugar: um nome errado deve tirar o polimento do jogador, não
                // o feedback de que o tiro acertou.
                SaiyaheimPlugin.Log.LogWarning(
                    $"{context}: impact effect '{effectName}' does not exist. " +
                    "Keeping the projectile prefab's own effect.");
                StripImpactEffects(projectile, strip, tint, lightsOnly, context);
                return;
            }

            projectile.m_hitEffects = new EffectList
            {
                m_effectPrefabs = new[]
                {
                    new EffectList.EffectData
                    {
                        m_prefab = StrippedEffect.Prepare(effectPrefab, strip, tint, lightsOnly, context),
                        m_enabled = true,
                    },
                },
            };
        }

        /// <summary>
        /// A cor do impacto, resolvida a partir das duas chaves que a governam.
        ///
        /// <b>Vazio segue o <c>ProjectileColor</c></b>, e esse é o ponto: o estouro é a mesma bola
        /// chegando, então pedir a cor duas vezes só criaria a chance de as duas saírem de
        /// sincronia no dia em que o tiro mudar de cor. Quem quiser um impacto de cor própria escreve
        /// o hex; quem quiser a cor original do prefab do jogo escreve <c>none</c>.
        /// </summary>
        private static string ResolveImpactColor(KiAttack attack)
        {
            string configured = attack.Config.ImpactColor.Value?.Trim() ?? string.Empty;

            if (configured.Equals("none", System.StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return configured.Length > 0
                ? configured
                : attack.Config.ProjectileColor.Value?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Tira do impacto os emissores filtrados, e o item inteiro quando é o <i>efeito</i> que
        /// tem o nome filtrado.
        ///
        /// São os dois lugares onde a fumaça pode estar, e de fora não dá para saber qual: ela
        /// pode ser um prefab próprio na lista de impacto — caso em que o item some — ou um filho
        /// do mesmo prefab que traz o clarão e o som, caso em que o que entra na lista é o clone
        /// sem ela. Cobrir só um dos dois deixaria metade dos efeitos do jogo sem resposta.
        /// </summary>
        private static void StripImpactEffects(
            Projectile projectile, string[] strip, string tint, bool lightsOnly, string context)
        {
            EffectList.EffectData[] effects = projectile.m_hitEffects?.m_effectPrefabs;
            if ((strip == null && string.IsNullOrEmpty(tint)) || effects == null || effects.Length == 0)
            {
                return;
            }

            List<EffectList.EffectData> kept = new List<EffectList.EffectData>(effects.Length);
            foreach (EffectList.EffectData effect in effects)
            {
                if (effect?.m_prefab == null)
                {
                    continue;
                }

                if (StrippedEffect.Matches(effect.m_prefab.name, strip))
                {
                    SaiyaheimPlugin.LogVerbose(
                        $"{context}: dropped impact effect '{effect.m_prefab.name}' — name matches the filter.");
                    continue;
                }

                EffectList.EffectData copy = Copy(effect);
                copy.m_prefab = StrippedEffect.Prepare(effect.m_prefab, strip, tint, lightsOnly, context);
                kept.Add(copy);
            }

            projectile.m_hitEffects = new EffectList { m_effectPrefabs = kept.ToArray() };
        }

        /// <summary>
        /// Cópia campo a campo de um <c>EffectData</c>.
        ///
        /// Feita à mão porque não há alternativa: <c>EffectData</c> não tem construtor de cópia
        /// nem <c>MemberwiseClone</c> acessível daqui. Escrever no que veio do prefab está fora de
        /// questão pelo motivo da doc de <see cref="ApplyImpactEffect"/>, e um <c>EffectData</c>
        /// novo com só o <c>m_prefab</c> preenchido perderia o <c>m_attach</c>, o <c>m_follow</c> e
        /// o resto — que é o que faz um efeito grudar no alvo em vez de ficar boiando onde o tiro
        /// bateu.
        ///
        /// ⚠️ Campo novo no <c>EffectData</c> numa atualização do Valheim tem que ser adicionado
        /// aqui, e o compilador não vai avisar.
        /// </summary>
        private static EffectList.EffectData Copy(EffectList.EffectData source)
        {
            return new EffectList.EffectData
            {
                m_prefab = source.m_prefab,
                m_enabled = source.m_enabled,
                m_variant = source.m_variant,
                m_attach = source.m_attach,
                m_follow = source.m_follow,
                m_inheritParentRotation = source.m_inheritParentRotation,
                m_inheritParentScale = source.m_inheritParentScale,
                m_multiplyParentVisualScale = source.m_multiplyParentVisualScale,
                m_randomRotation = source.m_randomRotation,
                m_scale = source.m_scale,
                m_childTransform = source.m_childTransform,
            };
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
