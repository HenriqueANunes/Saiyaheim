using System.Collections.Generic;
using Saiyaheim.Ki;
using Saiyaheim.Util;

namespace Saiyaheim.Transformations
{
    /// <summary>
    /// Anda na escada de formas: as duas teclas, as condições de entrada e as razões para cair.
    ///
    /// <b>Quatro teclas, em dois pares.</b> As formas são uma escada, e uma tecla só de ciclo
    /// obrigaria a passar por SSJ2 e SSJ3 para voltar à base no meio de uma luta. O par direto
    /// (T/G) resolve o caso comum num toque — <i>máximo que dá</i> e <i>sai da forma</i> — e o par
    /// de degraus (Shift+T/Shift+G) percorre a escada uma casa por vez, para quando o dreno de uma
    /// forma intermediária é o que se quer pagar. Com uma forma só os dois pares fazem a mesma
    /// coisa, mas o desenho já é o da escada.
    ///
    /// Roda no <c>Update</c> do plugin, como o <c>FlightManager</c> e o <c>KiBodyManager</c>, e
    /// pelo mesmo motivo que o voo: <c>SEMan.RemoveStatusEffect</c> mexe na lista que o
    /// <c>SEMan.Update</c> está iterando com o <c>Count</c> cacheado, então tirar a forma de dentro
    /// do próprio efeito estouraria índice.
    ///
    /// <b>A forma não é persistida.</b> Sair do mundo transformado e voltar transformado
    /// significaria a barra sendo comida durante o carregamento, antes de o jogador ter mão no
    /// teclado. Entra-se no mundo sempre em forma base — que é também o comportamento do gênero.
    /// </summary>
    internal static class TransformationManager
    {
        /// <summary>
        /// Um template por forma, criado na primeira transformação. O <c>SEMan.AddStatusEffect</c>
        /// guarda um <c>Clone()</c>, não a instância — estes objetos nunca são o efeito ativo.
        /// </summary>
        private static readonly Dictionary<string, SE_Transformation> Templates =
            new Dictionary<string, SE_Transformation>();

        internal static bool IsTransformed(Player player)
        {
            return TransformationRegistry.GetActive(player) != null;
        }

        internal static void Update(Player player)
        {
            if (player == null)
            {
                // Morte ou saída do mundo: a cor do cabelo vive na ZDO do jogador e morreu junto,
                // mas o estado interno dos efeitos precisa acompanhar.
                TransformationEffects.Reset();
                return;
            }

            SEMan seman = player.GetSEMan();
            if (seman == null)
            {
                return;
            }

            Transformation active = TransformationRegistry.GetActive(player);

            if (active != null)
            {
                string stopReason = GetStopReason(player);
                if (stopReason != null)
                {
                    // Direto para a base, e não um degrau abaixo: com a barra em zero não há com o
                    // que segurar forma nenhuma, e descer para SSJ só para cair de novo no tick
                    // seguinte seria ruído.
                    Stop(player, seman, stopReason);
                    return;
                }
            }

            if (!InputGuard.AcceptsInput())
            {
                return;
            }

            // Os atalhos com Shift vêm primeiro: só por segurança de leitura, já que o Hotkey
            // exige os modificadores exatos e T nunca dispara com Shift segurado.
            if (Hotkey.IsDown(SaiyaheimConfig.TransformStepUpKey))
            {
                StepUp(player, seman, active);
            }
            else if (Hotkey.IsDown(SaiyaheimConfig.TransformStepDownKey))
            {
                StepDown(player, seman, active);
            }
            else if (Hotkey.IsDown(SaiyaheimConfig.TransformKey))
            {
                TransformToHighest(player, seman, active);
            }
            else if (Hotkey.IsDown(SaiyaheimConfig.PowerDownKey))
            {
                PowerDown(player, seman, active);
            }
        }

        /// <summary>
        /// Vai direto à forma mais alta que o jogador destravou, pulando o que houver no meio.
        ///
        /// É o caminho comum: na maior parte das vezes o que se quer da tecla de transformar é
        /// <b>o máximo que dá</b>, e atravessar a escada degrau a degrau para chegar lá seria um
        /// toque por forma no meio da luta. Parar num degrau intermediário existe (é o Shift), mas
        /// é a exceção.
        /// </summary>
        private static void TransformToHighest(Player player, SEMan seman, Transformation active)
        {
            Transformation highest = TransformationRegistry.HighestUnlocked(player);

            if (highest == null)
            {
                // Nada destravado: o TryStart no primeiro degrau é quem sabe dizer o que falta —
                // ficar em silêncio aqui deixaria a tecla parecendo quebrada.
                TryStart(player, seman, TransformationRegistry.Next(null));
                return;
            }

            // Já no topo, ou acima dele por alguma razão: nada a fazer. Esta tecla só sobe — quem
            // desce é o PowerDownKey, e transformar não pode tirar poder de ninguém por acidente.
            if (TransformationRegistry.IndexOf(active) >= TransformationRegistry.IndexOf(highest))
            {
                return;
            }

            TryStart(player, seman, highest);
        }

        /// <summary>
        /// Sobe um degrau. No topo do que está destravado, não faz nada — pressionar "subir" onde
        /// não há acima é o mesmo que andar contra a parede, e não merece mensagem na tela.
        /// </summary>
        private static void StepUp(Player player, SEMan seman, Transformation active)
        {
            Transformation next = TransformationRegistry.Next(active);
            if (next == null)
            {
                return;
            }

            TryStart(player, seman, next);
        }

        /// <summary>
        /// Desce um degrau, trocando poder por dreno menor sem sair da escada. Do primeiro degrau,
        /// volta à base — é por isso que o <c>Previous</c> devolver null aqui é resposta e não erro.
        /// </summary>
        private static void StepDown(Player player, SEMan seman, Transformation active)
        {
            if (active == null)
            {
                return;
            }

            Transformation previous = TransformationRegistry.Previous(active);

            if (previous == null)
            {
                Stop(player, seman, $"{active.DisplayName} off");
                return;
            }

            // Sem passar pelo TryStart: descer é sempre permitido. Exigir ki ou nível para voltar
            // a uma forma mais fraca poderia deixar o jogador preso numa forma que ele não
            // consegue sustentar, que é o oposto do que a tecla existe para fazer.
            RemoveAll(seman);
            seman.AddStatusEffect(GetTemplate(previous));
            TransformationEffects.OnStepDown(player, previous);
            Message(player, previous.DisplayName);
            SaiyaheimPlugin.LogVerbose($"Stepped down from {active.DisplayName} to {previous.DisplayName}.");
        }

        /// <summary>
        /// Volta à forma base, de qualquer degrau. Quem desce um degrau de cada vez é o
        /// <see cref="StepDown"/>.
        ///
        /// No meio de uma luta o que se quer da tecla de sair é parar o dreno <b>agora</b>, e não
        /// atravessar SSJ2 e SSJ1 no caminho. Sair nunca é negado: não há checagem de ki nem de
        /// nível aqui, porque a única coisa pior que não conseguir entrar numa forma é não
        /// conseguir sair dela.
        /// </summary>
        private static void PowerDown(Player player, SEMan seman, Transformation active)
        {
            if (active == null)
            {
                return;
            }

            Stop(player, seman, $"{active.DisplayName} off");
        }

        /// <summary>
        /// Motivo para a forma acabar sozinha, ou null para continuar transformado.
        ///
        /// O caso central é o ki no zero, e ele é a mecânica inteira: o dreno é a única coisa que
        /// a forma custa, então ficar sem ki <b>é</b> o limite de quanto tempo ela dura. A maestria
        /// existe para empurrar esse limite.
        /// </summary>
        private static string GetStopReason(Player player)
        {
            if (!KiManager.IsEnabled)
            {
                // Desligar o toggle transformado herda o comportamento de ki zerado, igual ao voo:
                // o ki é a fonte da forma, e desligá-lo é problema de quem desligou.
                return "Ki off — you power down.";
            }

            if (KiManager.Current <= 0f)
            {
                return "Out of ki — you power down.";
            }

            if (player.IsDead() || player.IsSleeping() || player.IsTeleporting() || player.InCutscene())
            {
                return "";
            }

            return null;
        }

        private static void TryStart(Player player, SEMan seman, Transformation form)
        {
            if (form == null || !form.IsRegistered)
            {
                return;
            }

            if (!KiManager.IsEnabled)
            {
                Message(player, "Turn ki on to transform.");
                return;
            }

            // Sem custo de ativação, mas com um piso: entrar numa forma com a barra vazia seria
            // transformar e destransformar no mesmo frame, porque o dreno já derruba no primeiro
            // tick. Barulho sem mecânica.
            if (KiManager.Current <= 0f)
            {
                Message(player, "Not enough ki to transform.");
                return;
            }

            if (!form.IsUnlocked(player))
            {
                Message(player,
                    $"Battle Power {form.Config.MinBattlePower.Value:0} required for {form.DisplayName}.");
                return;
            }

            if (player.IsDead() || player.IsSleeping() || player.IsTeleporting() || player.InCutscene())
            {
                return;
            }

            // Uma forma de cada vez: subir um degrau tira o de baixo. Sem isto, SSJ e SSJ2 ativos
            // ao mesmo tempo multiplicariam o poder duas vezes — e o bug seria silencioso, porque
            // o registry devolve a primeira forma que encontrar.
            RemoveAll(seman);

            seman.AddStatusEffect(GetTemplate(form));

            TransformationEffects.OnPowerUp(player, form);

            Message(player, $"{form.DisplayName}!");
            SaiyaheimPlugin.LogVerbose(
                $"Transformed into {form.DisplayName}: x{form.GetPowerMultiplier():0.##} power, " +
                $"{form.GetKiDrainPerSecond(player):0.##} ki/s drain " +
                $"(mastery level {form.GetSkillLevel(player):0.#}).");
        }

        /// <summary>
        /// <paramref name="message"/> vazio ou null não mostra nada: morrer transformado não
        /// precisa de aviso, ficar sem ki no meio da luta precisa.
        /// </summary>
        private static void Stop(Player player, SEMan seman, string message)
        {
            RemoveAll(seman);
            TransformationEffects.OnPowerDown(player);
            SaiyaheimPlugin.LogVerbose($"Powered down. {message}");

            Message(player, message);
        }

        private static void RemoveAll(SEMan seman)
        {
            foreach (int hash in TransformationRegistry.AllNameHashes())
            {
                // quiet: sem mensagem na tela vinda do próprio efeito. O feedback é daqui, onde
                // se sabe o motivo — "you power down" e "Ki off" dizem coisas diferentes.
                seman.RemoveStatusEffect(hash, quiet: true);
            }
        }

        private static SE_Transformation GetTemplate(Transformation form)
        {
            if (!Templates.TryGetValue(form.Id, out SE_Transformation template) || template == null)
            {
                template = SE_Transformation.CreateTemplate(form);
                Templates[form.Id] = template;
            }

            return template;
        }

        private static void Message(Player player, string message)
        {
            if (player == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            player.Message(MessageHud.MessageType.Center, message);
        }
    }
}
