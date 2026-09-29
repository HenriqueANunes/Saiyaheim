using HarmonyLib;
using Saiyaheim.Ki;
using UnityEngine;

namespace Saiyaheim.Power
{
    /// <summary>
    /// Soco e ataque de ki em árvore, pedra e minério.
    ///
    /// <b>Como o jogo decide, e por que isso bastou para o desenho.</b> Árvore e minério fazem duas
    /// perguntas ao golpe, as duas no <c>RPC_Damage</c> de cada classe:
    /// <list type="number">
    /// <item><c>HitData.CheckToolTier(m_minToolTier)</c> — o <c>m_toolTier</c> do golpe alcança o
    /// do alvo? Se não, o jogo mostra "duro demais" e para. É uma trava, não uma armadura;</item>
    /// <item>quanto sobra do golpe depois do <c>ApplyResistance</c>. Toda árvore é imune a tudo menos
    /// <c>m_chop</c>, e pedra e minério a tudo menos <c>m_pickaxe</c>. O soco, contusão pura, dá
    /// zero em qualquer uma.</item>
    /// </list>
    /// A dureza entre tiers é só HP, e ele quase não cresce: faia 80, carvalho 200, broto de
    /// Yggdrasil 100 (prefabs extraídos, 2026-09-27).
    ///
    /// <b>O tier que o golpe alcança sai do battle power de combate</b>, contra uma tabela de
    /// limiares fixa aqui (<see cref="TierPower"/>). Transformar conta, porque é o poder de combate —
    /// no começo do jogo cada forma passa um limiar a mais, e no fim a forma base alcança sozinha o
    /// que antes pedia transformação.
    ///
    /// <b>O tier do alvo é o do jogo, com a picareta um degrau acima.</b> O jogo numera machado e
    /// picareta separados, e a picareta anda um degrau atrás: Copper é tier 0 de picareta e sai
    /// depois da Wood, tier 0 de machado; Silver é tier 2 de picareta e sai depois da Fine Wood,
    /// tier 2 de machado. Numa escada só, alvo de picareta vale o tier do jogo +1. Classificar
    /// pelo material dropado foi tentado e caiu no mesmo dia (2026-09-27): a escada por material era
    /// opinião, e o jogo já mostrou que ela erra — Ashwood é tier 0, as Ashlands travam pelo lugar.
    ///
    /// <b>O dano é relativo ao tier do alvo</b>, não ao poder cru — decisão de 2026-09-27:
    /// <code>dano = ChopDamage × poder / limiar_do_tier_do_alvo</code>
    /// Com dano proporcional ao poder, o jogador chegava a um tier novo já forte o bastante para
    /// derrubar a árvore recém-liberada em dois socos, porque o HP não acompanha o poder. Pela razão,
    /// toda árvore recém-liberada custa o mesmo número de golpes, em qualquer tier, e as velhas caem
    /// cada vez mais rápido.
    ///
    /// <b>Ataque de ki multiplica o poder só para o tier</b> (<c>TierPowerMultiplier</c>, decisão de
    /// 2026-09-27): o ki blast quebra um tier antes do soco, mas a razão do dano usa o poder de
    /// verdade. Num tier que só o multiplicador alcança, a razão fica abaixo de 1 e o golpe é mais
    /// fraco que o normal — o ataque quebra "antes da hora", com esforço. O dano ainda é pesado pelo quanto o ataque vale em
    /// socos contra inimigo (<see cref="GetAttackWeight"/>). Usar o dano do próprio ataque direto foi
    /// descartado: ele cresce com o poder cru e trazia de volta a árvore recém-liberada caindo em
    /// dois tiros.
    ///
    /// <b>Por que patch Harmony, contra a regra do projeto.</b> O dano depende do <b>alvo</b>, e o
    /// único ponto do lado de quem bate que conhece o alvo e o golpe ao mesmo tempo é o
    /// <c>Damage(HitData)</c> de cada classe. O <c>StatusEffect.ModifyAttack</c> roda antes, sem
    /// saber em quem vai bater. Os prefixes rodam na máquina de quem bate, antes do RPC que leva o
    /// golpe ao dono do objeto — o <c>m_toolTier</c> e os danos viajam no <c>HitData</c>
    /// serializado, e o dono da árvore não precisa ter o mod para nada disso.
    ///
    /// <b>Como o prefix reconhece um golpe do mod: um marcador no próprio dano.</b> O
    /// <c>Projectile</c> do jogo cria um <c>HitData</c> novo a cada impacto e copia só o
    /// <c>m_damage</c> — <c>m_toolTier</c> e todo o resto do que o mod escreveu se perdem. Então
    /// quem dispara grava o fator do ataque, multiplicado por <see cref="MarkerScale"/>, em
    /// <c>m_chop</c> e <c>m_pickaxe</c>, e o prefix troca o marcador pelo dano de verdade. O
    /// marcador é minúsculo de propósito: num alvo que não passa por aqui (criatura, por exemplo) ele
    /// vale milésimos de dano, e criatura ignora corte e picareta de qualquer forma.
    /// </summary>
    internal static class EnvironmentDamage
    {
        /// <summary>
        /// Escala do marcador. Com <c>TierPowerMultiplier</c> até 10, o marcador fica abaixo de 0,01 —
        /// nenhuma ferramenta do jogo chega perto disso, o machado de pedra dá 20.
        /// </summary>
        private const float MarkerScale = 0.001f;

        private const float MaxMarker = 10f * MarkerScale;

        /// <summary>
        /// Tier do jogo a partir do qual o alvo é de cerco: as muralhas da fortaleza das Ashlands.
        /// Nenhuma picareta do jogo chega lá, só Battering Ram, Siege Bomb e catapulta — então o
        /// soco também não chega, e só ataque de ki quebra. Decisão de 2026-09-27.
        /// </summary>
        private const int SiegeGameTier = 5;

        /// <summary>
        /// Battle power de combate que alcança cada tier; o índice é o tier.
        ///
        /// <b>Constante, não <c>.cfg</c></b> — decisão de 2026-09-27. O número é o battle power
        /// interno, que o jogador não vê: na tela está o poder de luta escaneável, outra escala. Uma
        /// chave que o jogador não tem como ler só confunde (o Henrique comparou com o número da
        /// tela e achou que era bug).
        ///
        /// Calibrado no playtest de 2026-09-27. O tier 0 nunca trava nada; o número dele só divide
        /// a razão do dano.
        /// <list type="bullet">
        /// <item>0 — Wood, Core Wood e Ashwood;</item>
        /// <item>1 — Stone, Copper, Tin e Flametal;</item>
        /// <item>2 — Fine Wood, Ancient Bark e Scrap Iron;</item>
        /// <item>3 — Silver e Obsidian;</item>
        /// <item>4 — Yggdrasil Wood, Black Marble, Soft Tissue e o Flametal de meteorito;</item>
        /// <item>5 — muralhas da fortaleza das Ashlands, só com ki.</item>
        /// </list>
        /// </summary>
        private static readonly float[] TierPower = { 100f, 150f, 300f, 800f, 1400f, 1900f };

        /// <summary>O topo da escada.</summary>
        internal static int MaxTier => TierPower.Length - 1;

        /// <summary>
        /// Marca o golpe como do mod. Chamado pelo soco (fator 1, e só quando o ki do soco foi
        /// pago) e pelo projétil de ki (o <c>TierPowerMultiplier</c> do ataque).
        /// </summary>
        internal static void Mark(HitData hit, float factor)
        {
            if (hit == null || factor <= 0f)
            {
                return;
            }

            float marker = Mathf.Min(factor * MarkerScale, MaxMarker);
            hit.m_damage.m_chop = marker;
            hit.m_damage.m_pickaxe = marker;
        }

        /// <summary>O tier de ferramenta que este poder de combate alcança.</summary>
        internal static int GetToolTier(float combatPower)
        {
            int reached = 0;
            for (int tier = 1; tier <= MaxTier; tier++)
            {
                if (combatPower >= GetTierPower(tier))
                {
                    reached = tier;
                }
            }

            return reached;
        }

        /// <summary>
        /// A razão entre o poder e o limiar de um tier. 1 é "acabou de liberar"; é ela que
        /// multiplica o <c>ChopDamage</c> e o <c>PickaxeDamage</c>.
        /// </summary>
        internal static float GetRatio(float combatPower, int targetTier)
        {
            return Mathf.Max(0f, combatPower) / GetTierPower(targetTier);
        }

        private static float GetTierPower(int tier)
        {
            tier = Mathf.Clamp(tier, 0, MaxTier);
            return TierPower[tier];
        }

        /// <summary>
        /// Troca o marcador pelo dano de verdade contra este alvo. Golpe sem marcador — machado,
        /// flecha, criatura, outro jogador — passa intocado.
        /// </summary>
        /// <param name="eligible">
        /// Falso num alvo que o golpe normal já danifica (ver <see cref="IsToolOnly"/>): aí o
        /// marcador é apagado e o golpe segue como era.
        /// </param>
        /// <param name="gameTier">O <c>m_minToolTier</c> do alvo, na numeração do jogo.</param>
        /// <param name="pickaxe">Alvo de picareta: sobe um degrau na escada do mod.</param>
        private static void Apply(HitData hit, int gameTier, bool pickaxe, bool eligible, string targetName)
        {
            if (!TryReadMarker(hit, out float factor, out Player player))
            {
                return;
            }

            hit.m_damage.m_chop = 0f;
            hit.m_damage.m_pickaxe = 0f;

            if (!eligible || !KiManager.IsEnabled)
            {
                return;
            }

            // Soco em alvo de cerco: o golpe segue vanilla (tier 0) e o jogo diz "duro demais".
            // m_ranged é o que separa o projétil de ki do soco — o Projectile marca todo impacto.
            if (gameTier >= SiegeGameTier && !hit.m_ranged)
            {
                return;
            }

            int targetTier = GetTargetTier(gameTier, pickaxe);
            // O fator do ataque multiplica o poder SÓ para o tier: soco é 1, ki blast alcança como se
            // fosse mais forte. O dano usa o poder de verdade — pedido do Henrique em 2026-09-27.
            float power = BattlePower.GetCombatRaw(player);
            float ratio = GetRatio(power, targetTier);
            int reached = GetToolTier(power * factor);

            // Quem decide é a escada do mod; o m_toolTier só traduz a decisão para o CheckToolTier
            // do jogo. -1 falha em qualquer alvo (até no tier 0, porque o alwaysAllowTierZero só
            // afrouxa a trava de nível de mundo), e o jogo mostra o "duro demais" sozinho.
            hit.m_toolTier = (short)(reached >= targetTier ? Mathf.Max(0, gameTier) : -1);

            // O CheckToolTier tambem recusa golpe de item com nivel de mundo abaixo do atual quando
            // a chave WorldLevelLockedTools esta ligada. O soco nao e' item: fica no nivel do mundo.
            hit.m_itemWorldLevel = (byte)Mathf.Clamp(Game.m_worldLevel, 0, byte.MaxValue);

            float weight = GetAttackWeight(hit, player);

            hit.m_damage.m_chop = SaiyaheimConfig.EnvironmentChopDamage.Value * ratio * weight;
            hit.m_damage.m_pickaxe = SaiyaheimConfig.EnvironmentPickaxeDamage.Value * ratio * weight;

            SaiyaheimPlugin.LogVerbose(
                $"Environment hit on {targetName} (tier {targetTier}, game {(pickaxe ? "pickaxe" : "axe")} " +
                $"tier {gameTier}): power {power:0} (x{factor:0.##} for the tier) reaches tier {reached}, " +
                $"x{ratio:0.##} of the threshold, x{weight:0.##} of a punch → {hit.m_damage.m_chop:0.#} chop / " +
                $"{hit.m_damage.m_pickaxe:0.#} pickaxe{(reached < targetTier ? " — too hard" : "")}.");
        }

        /// <summary>
        /// Quanto este golpe vale em socos contra inimigo. O soco é 1 por definição; o projétil de
        /// ki é o dano dele dividido pelo soco inteiro de agora.
        ///
        /// Existe porque o Kamehameha é feito de muitos projéteis fracos: ~1/6 de soco cada contra
        /// inimigo. Sem o peso, cada um batia na árvore como um soco e meio, e o feixe inteiro valia
        /// uns 90 socos (playtest de 2026-09-27). Com ele, o ataque bate em árvore na mesma
        /// proporção em que bate no inimigo, e recalibrar o dano do ataque arrasta o dano em árvore
        /// junto, sem número novo no <c>.cfg</c>.
        ///
        /// O dano do ataque vem do próprio golpe: o corte ou a perfuração que o
        /// <c>KiProjectile.BuildHit</c> escreveu e o <c>Projectile</c> copia para cada impacto. Os
        /// dois somados, porque cada ataque escreve só um deles — ler só o corte zerava o Kienzan em
        /// árvore quando ele virou perfuração (2026-09-28). Os dois lados da divisão leem o poder
        /// de combate, com a forma, então a forma não mexe no peso.
        /// </summary>
        private static float GetAttackWeight(HitData hit, Player player)
        {
            if (!hit.m_ranged)
            {
                return 1f;
            }

            float punch = BattlePower.GetPunchDamage(player);
            float attack = hit.m_damage.m_slash + hit.m_damage.m_pierce;
            return punch > 0f ? attack / punch : 0f;
        }

        /// <summary>O tier do alvo na escada do mod. Ver o cabeçalho da classe.</summary>
        internal static int GetTargetTier(int gameTier, bool pickaxe)
        {
            // O +1 é do atraso da picareta, e alvo de cerco não é de picareta: fica no tier do jogo.
            bool behind = pickaxe && gameTier < SiegeGameTier;
            return Mathf.Clamp(gameTier + (behind ? 1 : 0), 0, MaxTier);
        }

        /// <summary>Tier que soco nunca danifica. Ver <see cref="SiegeGameTier"/>.</summary>
        internal static bool IsKiOnly(int tier) => tier >= SiegeGameTier;

        /// <summary>
        /// Se um <c>Destructible</c> é de picareta. A classe cobre os dois — toco é de machado, a
        /// rocha do Copper antes de rachar e os restos de gigante são de picareta —, e quem diz é a
        /// resistência: o que não é imune a picareta e é imune a corte.
        /// </summary>
        private static bool IsPickaxeTarget(HitData.DamageModifiers modifiers)
        {
            return !IsImmune(modifiers.m_pickaxe) && IsImmune(modifiers.m_chop);
        }

        private static bool IsImmune(HitData.DamageModifier modifier)
        {
            return modifier == HitData.DamageModifier.Immune || modifier == HitData.DamageModifier.Ignore;
        }

        private static bool TryReadMarker(HitData hit, out float factor, out Player player)
        {
            factor = 0f;
            player = null;

            if (hit == null)
            {
                return false;
            }

            float marker = hit.m_damage.m_chop;
            if (marker <= 0f || marker > MaxMarker + 1e-6f || !Mathf.Approximately(marker, hit.m_damage.m_pickaxe))
            {
                return false;
            }

            // O Damage roda na maquina de quem bateu, entao o golpe do mod e' sempre do jogador
            // local. Golpe de outro jogador chega aqui ja' convertido, pelo RPC, e nunca com marcador.
            player = hit.GetAttacker() as Player;
            if (player == null || player != Player.m_localPlayer)
            {
                return false;
            }

            factor = marker / MarkerScale;
            return true;
        }

        /// <summary>
        /// Se o golpe normal não faz nada neste <c>Destructible</c>. A classe cobre de tudo — pedra
        /// solta, toco, a rocha do cobre antes de rachar, mas também ninho de greydwarf e arbusto —,
        /// e só o que é imune a contusão precisa do mod: o resto já toma o soco, e somar corte e
        /// picareta seria dano em dobro.
        /// </summary>
        private static bool IsToolOnly(HitData.DamageModifiers modifiers)
        {
            return IsImmune(modifiers.m_blunt);
        }

        [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.Damage))]
        private static class TreeBasePatch
        {
            private static void Prefix(TreeBase __instance, HitData hit) =>
                Apply(hit, __instance.m_minToolTier, false, true, __instance.name);
        }

        [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Damage))]
        private static class TreeLogPatch
        {
            private static void Prefix(TreeLog __instance, HitData hit) =>
                Apply(hit, __instance.m_minToolTier, false, true, __instance.name);
        }

        [HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
        private static class MineRockPatch
        {
            private static void Prefix(MineRock __instance, HitData hit) =>
                Apply(hit, __instance.m_minToolTier, true, true, __instance.name);
        }

        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
        private static class MineRock5Patch
        {
            private static void Prefix(MineRock5 __instance, HitData hit) =>
                Apply(hit, __instance.m_minToolTier, true, true, __instance.name);
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Damage))]
        private static class DestructiblePatch
        {
            private static void Prefix(Destructible __instance, HitData hit) =>
                Apply(hit, __instance.m_minToolTier, IsPickaxeTarget(__instance.m_damages),
                    IsToolOnly(__instance.m_damages), __instance.name);
        }

        /// <summary>
        /// Só pelos muros, portões e piso da fortaleza das Ashlands, que são <c>WearNTear</c> — a
        /// classe das construções. Abaixo do tier de cerco o marcador é só apagado: construção de
        /// jogador fica exatamente como era.
        /// </summary>
        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
        private static class WearNTearPatch
        {
            private static void Prefix(WearNTear __instance, HitData hit) =>
                Apply(hit, __instance.m_minToolTier, true,
                    __instance.m_minToolTier >= SiegeGameTier, __instance.name);
        }
    }
}
