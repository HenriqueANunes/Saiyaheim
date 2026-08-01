using Saiyaheim.Ki;
using Saiyaheim.Power;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Flight
{
    /// <summary>
    /// Liga e desliga o <see cref="SE_Flight"/>: tecla, condições de decolagem e as razões para
    /// cair.
    ///
    /// Roda no <c>Update</c> do plugin, como o <c>KiBodyManager</c>. Não é o mesmo lugar do
    /// <c>SE_Flight.UpdateStatusEffect</c> de propósito: <c>SEMan.RemoveStatusEffect</c> mexe na
    /// lista que o <c>SEMan.Update</c> está iterando com o <c>Count</c> cacheado. Tirar o voo de
    /// dentro do próprio efeito estouraria índice.
    /// </summary>
    internal static class FlightManager
    {
        /// <summary>
        /// Template. O <c>SEMan.AddStatusEffect</c> guarda um <c>Clone()</c> dele, não a
        /// instância — este objeto nunca é o efeito ativo, só o molde.
        /// </summary>
        private static SE_Flight _template;

        /// <summary>
        /// O jogador já saiu do chão neste voo.
        ///
        /// Sem isto o pouso automático seria imediato: decola-se <b>de pé no chão</b>, e o
        /// <c>IsOnGround()</c> continua verdadeiro até o jogador subir. Estado do voo local, então
        /// mora aqui e não no efeito — o <c>FlightManager</c> só existe para o jogador local.
        /// </summary>
        private static bool _leftGround;

        /// <summary>
        /// Instante do último toque no botão de pulo, para o toque duplo. Só é alimentado
        /// <b>fora</b> do voo — no ar o pulo é o comando de subir, e contar aqueles toques
        /// deixaria um instante recente pendurado para o momento em que o jogador pousasse.
        /// </summary>
        private static float _lastJumpTapTime = float.NegativeInfinity;

        internal static bool IsFlying(Player player)
        {
            SEMan seman = player == null ? null : player.GetSEMan();
            return seman != null && seman.HaveStatusEffect(SE_Flight.NameHashValue);
        }

        internal static void Update(Player player)
        {
            if (player == null)
            {
                return;
            }

            SEMan seman = player.GetSEMan();
            if (seman == null)
            {
                return;
            }

            bool flying = seman.HaveStatusEffect(SE_Flight.NameHashValue);

            if (flying)
            {
                if (!player.IsOnGround())
                {
                    _leftGround = true;
                }

                string stopReason = GetStopReason(player);
                if (stopReason != null)
                {
                    Stop(player, seman, stopReason);
                    return;
                }
            }

            if (!InputGuard.AcceptsInput())
            {
                return;
            }

            // Toque duplo só decola, nunca pousa — ver ConsumeDoubleJump.
            if (!flying && ConsumeDoubleJump())
            {
                TryStart(player, seman);
                return;
            }

            if (!SaiyaheimConfig.ToggleFlightKey.Value.IsDown())
            {
                return;
            }

            if (flying)
            {
                Stop(player, seman, null);
            }
            else
            {
                TryStart(player, seman);
            }
        }

        /// <summary>
        /// Dois toques no botão de pulo dentro da janela configurada.
        ///
        /// <b>Só decola, nunca pousa.</b> Voando, o pulo é o comando de subir: um toque duplo lá em
        /// cima brigaria com o controle que o jogador já está usando, e ainda seria disparado sem
        /// querer por quem só quer ganhar altitude rápido. Pousar é a <c>ToggleFlightKey</c> e
        /// encostar no chão.
        ///
        /// Funciona no chão e no ar: pular e decolar no meio do salto — ou se segurar numa queda —
        /// é o gesto certo do gênero, e o <see cref="TryStart"/> não exige chão.
        ///
        /// <c>ZInput.GetButtonDown</c> é consulta pura, não consome o evento: o pulo do jogo
        /// continua funcionando normalmente em paralelo. E é <c>ZInput</c>, não
        /// <c>UnityEngine.Input</c>, então respeita rebind e gamepad.
        /// </summary>
        private static bool ConsumeDoubleJump()
        {
            if (!SaiyaheimConfig.FlightTakeOffOnDoubleJump.Value)
            {
                _lastJumpTapTime = float.NegativeInfinity;
                return false;
            }

            if (!ZInput.GetButtonDown("Jump") && !ZInput.GetButtonDown("JoyJump"))
            {
                return false;
            }

            float now = Time.time;
            bool isDoubleTap = now - _lastJumpTapTime <= SaiyaheimConfig.FlightDoubleJumpWindow.Value;

            // Zerar no toque duplo impede que um terceiro toque encadeie outro disparo: cada
            // decolagem exige um par novo.
            _lastJumpTapTime = isDoubleTap ? float.NegativeInfinity : now;

            return isDoubleTap;
        }

        /// <summary>
        /// Motivo para o voo acabar sozinho, ou null para continuar voando.
        ///
        /// O caso central é o ki no zero: o design escolheu queda, não planeio. É a tensão
        /// inteira do voo — subir alto é uma aposta contra a barra.
        /// </summary>
        private static string GetStopReason(Player player)
        {
            if (!KiManager.IsEnabled)
            {
                // Desligar o toggle no ar herda o comportamento de ki zerado. Consistência dura:
                // o ki é a fonte do voo, e desligá-lo é problema de quem desligou.
                return "Ki off — you are falling!";
            }

            if (KiManager.Current <= 0f)
            {
                return "Out of ki — you are falling!";
            }

            if (player.IsDead() || player.IsSleeping() || player.IsTeleporting() || player.InCutscene())
            {
                return "";
            }

            // Nadar tem prioridade sobre voar dentro do próprio UpdateMotion: o voo continuaria
            // ligado sem fazer nada e o ki iria embora à toa.
            if (player.IsSwimming() || player.IsAttached() || player.InBed())
            {
                return "";
            }

            // Pousar. O _leftGround é o que impede que decolar de pé no chão pouse na mesma hora:
            // com a gravidade desligada o jogador paira à altura do chão até apertar Jump, e o
            // IsOnGround() continua verdadeiro esse tempo todo.
            if (_leftGround && SaiyaheimConfig.FlightAutoLandOnGround.Value && player.IsOnGround())
            {
                return "";
            }

            return null;
        }

        private static void TryStart(Player player, SEMan seman)
        {
            if (!KiManager.IsEnabled)
            {
                Message(player, "Turn ki on to fly.");
                return;
            }

            if (KiManager.Current <= 0f)
            {
                Message(player, "Not enough ki to fly.");
                return;
            }

            float required = SaiyaheimConfig.FlightMinBattlePower.Value;
            if (required > 0f && PowerSkill.GetLevel(player) < required)
            {
                Message(player, $"Battle Power {required:0} required to fly.");
                return;
            }

            if (player.IsDead() || player.IsSleeping() || player.IsTeleporting() || player.InCutscene()
                || player.IsSwimming() || player.IsAttached() || player.InBed())
            {
                return;
            }

            if (_template == null)
            {
                _template = SE_Flight.CreateTemplate();
            }

            _leftGround = false;
            _lastJumpTapTime = float.NegativeInfinity;
            seman.AddStatusEffect(_template);
            SaiyaheimPlugin.LogVerbose("Flight started.");
        }

        /// <summary>
        /// <paramref name="message"/> vazio ou null não mostra nada: pousar de propósito não
        /// precisa de aviso, ficar sem ki a 40 metros do chão precisa.
        /// </summary>
        private static void Stop(Player player, SEMan seman, string message)
        {
            _leftGround = false;

            // Sem isto, o último toque de subida antes de pousar ficaria valendo como primeiro
            // toque do próximo par e um único pulo depois do pouso decolaria de novo.
            _lastJumpTapTime = float.NegativeInfinity;

            seman.RemoveStatusEffect(SE_Flight.NameHashValue, quiet: true);
            SaiyaheimPlugin.LogVerbose($"Flight stopped. {message}");

            Message(player, message);
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
