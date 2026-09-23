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

        /// <summary>
        /// Prazo para uma decolagem na água sair do nado. Não é balanceamento: com o empurrão
        /// padrão o jogador sai em menos de um segundo. Só estoura com algo em cima — casco de
        /// barco, teto de caverna alagada —, e aí o voo desiste em vez de drenar ki parado.
        /// </summary>
        private const float WaterTakeOffTimeout = 2f;

        /// <summary>
        /// Instante em que a decolagem na água começou, ou <c>NaN</c> fora dela. Enquanto vale,
        /// o <see cref="WaterTakeOffPatch"/> empurra o jogador para cima e nadar não derruba o
        /// voo.
        /// </summary>
        private static float _waterTakeOffStart = float.NaN;

        /// <summary>O jogador local está subindo da água para o voo.</summary>
        internal static bool IsTakingOffFromWater => !float.IsNaN(_waterTakeOffStart);

        /// <summary>
        /// Modo do voo, salvo no personagem. Estado e não preferência, então fica fora do .cfg: o
        /// <c>m_customData</c> é serializado pelo próprio jogo junto do save, como o ki do
        /// <c>KiState</c>. Ausente é o modo clássico.
        /// </summary>
        private const string KeySteerByAim = "saiyaheim.flightSteerByAim";

        /// <summary>Voo guiado pela mira ligado neste personagem. Ver <see cref="SE_Flight"/>.</summary>
        internal static bool SteersByAim(Player player)
        {
            return player != null && player.m_customData != null
                   && player.m_customData.TryGetValue(KeySteerByAim, out string raw) && raw == "1";
        }

        private static void SetSteersByAim(Player player, bool steerByAim)
        {
            if (player == null || player.m_customData == null)
            {
                return;
            }

            player.m_customData[KeySteerByAim] = steerByAim ? "1" : "0";
        }

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

            // O efeito pode sair sem passar pelo Stop (morte limpa os efeitos, por exemplo).
            if (!flying)
            {
                _waterTakeOffStart = float.NaN;
            }

            if (flying)
            {
                // O voo sobrevive ao teleporte (portal, entrada de dungeon) e chega do outro lado
                // como uma decolagem nova: o destino costuma ser no chão, e sem zerar isto o pouso
                // automático derrubaria o jogador no primeiro passo depois da tela de carregamento.
                if (player.IsTeleporting())
                {
                    _leftGround = false;
                }
                else if (!player.IsOnGround())
                {
                    _leftGround = true;
                }

                // Fim da decolagem na água: a partir daqui o UpdateMotion já cai no UpdateFlying.
                if (IsTakingOffFromWater && !player.IsSwimming())
                {
                    _waterTakeOffStart = float.NaN;
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

            // No chão também: escolher o modo antes de decolar é o caso comum.
            if (Hotkey.IsDown(SaiyaheimConfig.ToggleFlightAimKey))
            {
                bool steerByAim = !SteersByAim(player);
                SetSteersByAim(player, steerByAim);
                Message(player, steerByAim ? "Flight: aim mode" : "Flight: classic mode");
                return;
            }

            // Toque duplo só decola, nunca pousa — ver ConsumeDoubleJump.
            if (!flying && ConsumeDoubleJump())
            {
                TryStart(player, seman);
                return;
            }

            if (!Hotkey.IsDown(SaiyaheimConfig.ToggleFlightKey))
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

            // Teleporte não entra, pelo mesmo motivo da forma: o jogador chega do outro lado do portal
            // ou da dungeon como estava (2026-09-23). O dreno pausa na tela de carregamento — ver
            // SE_Flight.UpdateStatusEffect.
            if (player.IsDead() || player.IsSleeping() || player.InCutscene())
            {
                return "";
            }

            if (IsTakingOffFromWater && Time.time - _waterTakeOffStart > WaterTakeOffTimeout)
            {
                return "";
            }

            // Nadar tem prioridade sobre voar dentro do próprio UpdateMotion: o voo continuaria
            // ligado sem fazer nada e o ki iria embora à toa. É também o que impede voar debaixo
            // d'água — descer voando até a água derruba o voo. A exceção é a decolagem na água,
            // que ainda está nadando por definição.
            if ((player.IsSwimming() && !IsTakingOffFromWater) || player.IsAttached() || player.InBed())
            {
                return "";
            }

            // Pousar. O _leftGround é o que impede que decolar de pé no chão pouse na mesma hora:
            // com a gravidade desligada o jogador paira à altura do chão até apertar Jump, e o
            // IsOnGround() continua verdadeiro esse tempo todo.
            if (_leftGround && player.IsOnGround() && !IsStandingOnCreature(player))
            {
                return "";
            }

            return null;
        }

        /// <summary>
        /// Criatura não é chão: passar rasante por cima de um inimigo não pode desligar o voo.
        ///
        /// O <c>IsOnGround()</c> vale para qualquer contato de baixo. Para bicho o
        /// <c>Character.UpdateGroundContact</c> descarta colisor do mesmo layer, mas para o jogador
        /// não — o colisor da criatura fica registrado como chão. <c>GetComponentInParent</c>
        /// porque o colisor costuma estar num filho do objeto que carrega o <c>Character</c>.
        /// Cadáver e ragdoll não têm <c>Character</c>, então cair em cima de um ainda pousa.
        /// </summary>
        private static bool IsStandingOnCreature(Player player)
        {
            Collider ground = player.GetLastGroundCollider();
            return ground != null && ground.GetComponentInParent<Character>() != null;
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

            float required = SaiyaheimConfig.FlightMinPowerLevel.Value;
            if (required > 0f && PowerSkill.GetLevel(player) < required)
            {
                Message(player, $"Power Level {required:0} required to fly.");
                return;
            }

            // Nadar não impede: decolar da água é o WaterTakeOffPatch empurrando para cima.
            if (player.IsDead() || player.IsSleeping() || player.IsTeleporting() || player.InCutscene()
                || player.IsAttached() || player.InBed())
            {
                return;
            }

            if (_template == null)
            {
                _template = SE_Flight.CreateTemplate();
            }

            _leftGround = false;
            _lastJumpTapTime = float.NegativeInfinity;
            _waterTakeOffStart = player.IsSwimming() ? Time.time : float.NaN;
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
            _waterTakeOffStart = float.NaN;

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
