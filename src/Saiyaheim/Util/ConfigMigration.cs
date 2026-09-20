using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Saiyaheim.Util
{
    /// <summary>
    /// Leva o <c>.cfg</c> de um jogador que já tem o mod instalado para os defaults novos.
    ///
    /// <b>O problema que isto resolve.</b> O <c>.cfg</c> é gerado a partir dos defaults do
    /// <see cref="SaiyaheimConfig"/> <b>uma única vez</b>: mudar um default no código não altera
    /// um arquivo que já existe. Para ajuste fino isso é o comportamento certo — ninguém quer que
    /// uma atualização apague a tecla que ele remapeou. Para um <b>rework de balanceamento</b> é o
    /// contrário: quem atualiza continua jogando com os números velhos, sem sinal nenhum de que
    /// algo mudou, e o que ele relata como "o mod está desbalanceado" é um mod que ele nunca
    /// recebeu.
    ///
    /// <b>Por que não renomear a chave</b>, que seria mais barato e força o default novo pelo
    /// simples fato de a chave não existir no arquivo do jogador: o BepInEx <b>preserva</b> a
    /// entrada órfã em vez de apagá-la, então o arquivo acumularia a chave morta ao lado da viva
    /// para sempre, e o próximo rework pediria um sufixo novo. O nome da chave é interface do
    /// jogador, não registro de histórico.
    ///
    /// <b>A regra, e a exceção.</b> O default é conservador: a chave só é reescrita se o valor
    /// atual ainda for o default <i>antigo</i> — ou seja, se o jogador nunca mexeu nela. Quem
    /// calibrou alguma coisa de propósito fica com o valor dele e recebe um aviso no log.
    ///
    /// A exceção é o <see cref="Change.Force"/>, e ela existe para os números que não fazem sentido
    /// pela metade. Um rework em que o dreno da forma despenca <b>porque</b> o custo por golpe
    /// subiu só é jogável inteiro: quem ficasse com metade dos números velhos teria uma
    /// experiência pior que a de antes e que a de depois. Aí o valor customizado é sobrescrito, e o
    /// aviso no log diz qual era para quem quiser restaurar à mão.
    /// </summary>
    internal static class ConfigMigration
    {
        /// <summary>
        /// Versão do formato de balanceamento do <c>.cfg</c>. Sobe <b>só</b> quando uma migração
        /// nova entra — não acompanha a versão do plugin, que sobe a cada release por qualquer
        /// motivo.
        /// </summary>
        internal const int CurrentVersion = 4;

        /// <summary>Uma chave que mudou de default, e o que fazer com o valor que o jogador tem.</summary>
        private readonly struct Change
        {
            internal Change(int version, ConfigEntryBase entry, object oldDefault, bool force)
            {
                Version = version;
                Entry = entry;
                OldDefault = oldDefault;
                Force = force;
            }

            /// <summary>Migração em que esta mudança entrou. Só roda para quem está abaixo dela.</summary>
            internal int Version { get; }

            internal ConfigEntryBase Entry { get; }

            /// <summary>
            /// O default que esta chave tinha <b>antes</b>. É ele que distingue "o jogador nunca
            /// mexeu" de "o jogador calibrou de propósito" — a única informação que o arquivo
            /// sozinho não carrega.
            /// </summary>
            internal object OldDefault { get; }

            /// <summary>Sobrescreve mesmo um valor customizado. Ver a nota da classe.</summary>
            internal bool Force { get; }
        }

        /// <summary>
        /// Aplica o que falta ao <c>.cfg</c> deste jogador e grava a versão nova.
        ///
        /// ⚠️ <b>Chamado no fim do <c>Init</c>, depois de todo <c>Bind</c></b>, e a lista é montada
        /// aqui dentro pelo mesmo motivo: as entradas são propriedades estáticas preenchidas
        /// durante o <c>Init</c>, e uma tabela em campo estático leria null.
        /// </summary>
        internal static void Run(ConfigFile config)
        {
            int from = SaiyaheimConfig.ConfigVersion.Value;

            if (from >= CurrentVersion)
            {
                // Maior que a atual é um .cfg que já viu uma versão mais nova do mod — downgrade.
                // Não há o que fazer a respeito (a migração não sabe desfazer), mas dizer em voz
                // alta evita uma caçada a um bug que é só a ordem das instalações.
                if (from > CurrentVersion)
                {
                    SaiyaheimPlugin.Log.LogWarning(
                        $"Config version {from} is newer than this build knows ({CurrentVersion}). " +
                        "Leaving every value as it is.");
                }

                return;
            }

            int applied = 0;
            int kept = 0;

            foreach (Change change in Changes())
            {
                if (change.Version <= from)
                {
                    continue;
                }

                switch (Apply(change))
                {
                    case Result.Updated:
                        applied++;
                        break;

                    case Result.Kept:
                        kept++;
                        break;
                }
            }

            SaiyaheimConfig.ConfigVersion.Value = CurrentVersion;
            config.Save();

            // Instalação limpa cai aqui com applied 0: o arquivo nasceu com os defaults novos e
            // nada casou com um default antigo. É o caso normal e não merece alarde.
            SaiyaheimPlugin.Log.LogInfo(
                $"Config migrated from version {from} to {CurrentVersion}: " +
                $"{applied} key(s) updated, {kept} customized key(s) kept.");
        }

        /// <summary>O que a migração fez com uma chave. Separa "preservei o teu valor" de "não havia nada a fazer".</summary>
        private enum Result
        {
            /// <summary>Já estava no valor novo. Instalação limpa cai toda aqui.</summary>
            AlreadyCurrent,

            /// <summary>Recebeu o default novo.</summary>
            Updated,

            /// <summary>Customizado e não forçado: ficou como estava.</summary>
            Kept,
        }

        /// <summary>Escreve o default novo, ou não, e conta ao jogador o que decidiu.</summary>
        private static Result Apply(Change change)
        {
            object current = change.Entry.BoxedValue;
            object updated = change.Entry.DefaultValue;

            if (SameValue(current, updated))
            {
                // Já está no valor novo: instalação limpa, ou o jogador chegou lá sozinho.
                return Result.AlreadyCurrent;
            }

            bool untouched = SameValue(current, change.OldDefault);

            if (!untouched && !change.Force)
            {
                SaiyaheimPlugin.Log.LogWarning(
                    $"{Describe(change.Entry)} is at {current} (customized), kept as is. " +
                    $"The new balance uses {updated}.");

                return Result.Kept;
            }

            change.Entry.BoxedValue = updated;

            if (untouched)
            {
                SaiyaheimPlugin.Log.LogInfo($"{Describe(change.Entry)}: {current} -> {updated}.");
            }
            else
            {
                SaiyaheimPlugin.Log.LogWarning(
                    $"{Describe(change.Entry)}: your value {current} was replaced by {updated}. " +
                    "This key is part of a balance rework that does not work in halves — " +
                    "set it back by hand if you meant it.");
            }

            return Result.Updated;
        }

        /// <summary>
        /// Igualdade tolerante para float. O valor lido do arquivo passa por texto, e comparar
        /// dois floats por <c>Equals</c> depois disso é o tipo de coisa que funciona em todo teste
        /// e falha na máquina de um jogador.
        /// </summary>
        private static bool SameValue(object a, object b)
        {
            if (a is float fa && b is float fb)
            {
                return Mathf.Abs(fa - fb) < 0.0001f;
            }

            return Equals(a, b);
        }

        private static string Describe(ConfigEntryBase entry)
        {
            return $"[{entry.Definition.Section}] {entry.Definition.Key}";
        }

        /// <summary>
        /// A tabela inteira, de todas as migrações. Cada linha é permanente: ela é o registro de
        /// que aquele default já foi outro, e é o que permite a um jogador que pulou várias
        /// versões receber todas as mudanças de uma vez.
        /// </summary>
        private static IEnumerable<Change> Changes()
        {
            // ---------- 1 (2026-09-20) — ki deixa de ser imposto de existir ----------
            //
            // O dreno da forma despenca e o custo migra para a ação. As duas metades são a mesma
            // decisão, e é por isso que TODA linha desta migração é Force: um .cfg com o dreno novo
            // e o custo de soco velho é uma forma quase gratuita que bate de graça — mais
            // desbalanceado que qualquer um dos dois conjuntos inteiros.
            //
            // Ver Melhorias, "Ki deixa de ser imposto de existir e vira combustível de combate".

            yield return new Change(1, SaiyaheimConfig.Ssj.KiDrainPerSecond, 5f, true);
            yield return new Change(1, SaiyaheimConfig.Ssj2.KiDrainPerSecond, 10f, true);
            yield return new Change(1, SaiyaheimConfig.Ssj3.KiDrainPerSecond, 15f, true);

            yield return new Change(1, SaiyaheimConfig.PunchKiCostPerDamage, 3f, true);
            yield return new Change(1, SaiyaheimConfig.BlockKiCost, 0.5f, true);
            yield return new Change(1, SaiyaheimConfig.DamageTakenKiCost, 1f, true);

            yield return new Change(1, SaiyaheimConfig.KiOnParryPunches, 2f, true);
            yield return new Change(1, SaiyaheimConfig.KiOnKillPunches, 4f, true);

            yield return new Change(1, SaiyaheimConfig.FlightKiPerSecond, 5f, true);
            yield return new Change(1, SaiyaheimConfig.FlightKiSkillCurve, 2f, true);

            // Carona na mesma migração, e a única linha dela que NÃO é Force: o power down saiu do
            // G quando a l-1.0.7 do Valheim passou a abrir o menu radial nessa tecla, e quem já
            // tinha .cfg ficou com as duas coisas na mesma tecla. Tecla é preferência, não
            // balanceamento — quem escolheu a dele fica com ela e só lê o aviso.
            yield return new Change(1, SaiyaheimConfig.PowerDownKey,
                new KeyboardShortcut(KeyCode.G), false);
            yield return new Change(1, SaiyaheimConfig.TransformStepDownKey,
                new KeyboardShortcut(KeyCode.G, KeyCode.LeftShift), false);

            // ---------- 2 (2026-09-20) — a defesa sai do desconto do soco ----------
            //
            // Primeiro playtest do rework: bloquear transformado nao custava nada. Causa em
            // BattlePower.GetDefenseKiCostFactor — apanhar e bloquear levavam o desconto de poder
            // do soco, que a forma multiplica. As duas taxas vinham infladas para compensar esse
            // desconto, entao tirar o desconto sem baixar as taxas multiplicaria o custo por
            // cinco a dezessete. Force pelo mesmo motivo da migracao 1: meio conserto e' pior que
            // nenhum. A DefenseKiCostPowerReduction e' chave nova e nao precisa de linha aqui.
            //
            // Quem vem da versao 0 passa pelas DUAS linhas de cada chave e acerta assim mesmo: a
            // linha da migracao 1 escreve o default de hoje (que ja e' o valor novo) e a linha
            // daqui encontra a chave ja em ordem.
            yield return new Change(2, SaiyaheimConfig.BlockKiCost, 1.2f, true);
            yield return new Change(2, SaiyaheimConfig.DamageTakenKiCost, 1.5f, true);

            // ---------- 3 (2026-09-20) — pairar parado deixa de ser subsidiado ----------
            //
            // O desconto de 50% para pairar estava pagando pela postura do cheese de boss que as
            // issues relatam: ficar parado no ar fora do alcance. Force porque quem atualiza com o
            // .cfg antigo continuaria com o subsidio e com a sobretaxa de combate nova por cima —
            // a metade errada do conserto. A CombatHoverMultiplier e' chave nova e nao precisa de
            // linha aqui.
            yield return new Change(3, SaiyaheimConfig.FlightHoverKiMultiplier, 0.5f, true);

            // ---------- 4 (2026-09-20) — as chaves cujo SIGNIFICADO mudou ----------
            //
            // Estas duas nao mudaram de default. Mudou o que o numero quer dizer, e um valor
            // calibrado contra a formula velha nao e' mais o que o jogador pediu:
            //
            //   KiCostPowerReduction lia o poder JA multiplicado pela forma e agora le o da forma
            //   base. Quem tinha subido essa chave — e o proprio veredito da calculadora mandava
            //   subir, para "a forma se pagar" — teria agora um desconto muito maior do que quis.
            //
            //   MasteryFormCostReduction era um termo somado dentro daquele divisor e virou a
            //   fracao da sobretaxa que a maestria devolve. A faixa caiu de 0-3 para 0-1, entao um
            //   2,5 de antes nem cabe mais: o Bind grampeia em 1 em silencio.
            //
            // Force, e voltar ao default e' o unico destino seguro: nao ha como converter um valor
            // de uma formula para a outra, e manter o numero seria manter uma intencao que a conta
            // nova le ao contrario. Para quem esta no default — a esmagadora maioria — as duas
            // linhas nao fazem nada.
            yield return new Change(4, SaiyaheimConfig.KiCostPowerReduction, 0.01f, true);
            yield return new Change(4, SaiyaheimConfig.MasteryFormCostReduction, 1f, true);
        }
    }
}
