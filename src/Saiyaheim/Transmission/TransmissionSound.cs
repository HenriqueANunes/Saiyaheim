using System.Collections.Generic;
using Saiyaheim.Net;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Transmission
{
    /// <summary>
    /// O som do Instant Transmission (etapa 16), ouvido por todo mundo por perto.
    ///
    /// <b>Quem ouve o quê:</b>
    /// <list type="bullet">
    /// <item><b>quem teleporta</b>: um som só, preso ao corpo, no clique — quando a tela dele
    /// começa a escurecer. Dois sons seriam repetição;</item>
    /// <item><b>os outros</b>: a ida parada no ponto de partida e a chegada parada no destino. Presa
    /// ao corpo, a ida dos outros iria junto para o destino num salto curto e se somaria à
    /// chegada.</item>
    /// </list>
    ///
    /// <b>O clip é do mod, o resto é da vanilla.</b> O <c>instant_transmission</c> vem do
    /// <c>saiyaheim_fx</c> como <c>AudioClip</c> solto, e toca dentro de um clone de um <c>sfx_</c>
    /// do jogo: assim herda o mixer de efeitos (o volume das opções vale), a atenuação por
    /// distância e o reverb de caverna, sem nada disso montado à mão. O clone é feito uma vez,
    /// debaixo de um objeto inativo, para o <c>ZSFX.Awake</c> do molde não tocar nada.
    ///
    /// <b>Pela rede, no desenho do disparo do ki blast:</b> o dono soma um contador na ZDO e grava
    /// o instante do clique (<see cref="NetState.PublishTransmission"/>). Quem vê o contador subir
    /// toca a ida e agenda a chegada. Quem está no destino não viu o contador antes — para ele o
    /// jogador é novo —, e é o instante que avisa que aquele jogador acabou de chegar.
    /// </summary>
    internal static class TransmissionSound
    {
        private const string ClipName = "instant_transmission";

        // Só empresta mixer, atenuação e reverb: os clipes, o tom e o volume dele são trocados.
        // Um som 3D curto e sem loop, que o mod já usa no soco.
        private const string BaseSfx = "sfx_unarmed_swing";

        /// <summary>
        /// Por quanto tempo depois do instante previsto a chegada ainda toca. Cobre a ZDO
        /// chegando atrasada e o jogador que surge na máquina de quem está no destino só depois da
        /// tela de carregamento dele. Passou disso, quem apareceu não está "chegando".
        /// </summary>
        private const double ArrivalWindow = 6.0;

        /// <summary>
        /// Quanto o jogador precisa ter saído do ponto de partida para a chegada tocar onde ele
        /// está. Antes disso a máquina daqui ainda não recebeu a posição nova, e o som sairia na
        /// origem, em cima da ida.
        /// </summary>
        private const float ArrivalMinMove = 5f;

        private static GameObject _template;
        private static bool _failed;

        private static readonly Dictionary<Player, int> LastSeen = new Dictionary<Player, int>();
        private static readonly Dictionary<Player, int> LastSeenBlink = new Dictionary<Player, int>();
        private static readonly List<Player> Gone = new List<Player>();

        private struct PendingArrival
        {
            public Player Player;

            /// <summary>Instante de mundo em que a chegada pode tocar.</summary>
            public double At;

            /// <summary>Onde ele estava ao anunciar; null quando quem olha só o viu chegar.</summary>
            public Vector3? Origin;
        }

        private static readonly List<PendingArrival> Pending = new List<PendingArrival>();

        /// <summary>
        /// Vê se algum jogador carregado teleportou desde o último frame e toca as chegadas que
        /// venceram. Chamado do <c>Update</c> do plugin, depois da publicação do estado.
        /// </summary>
        internal static void Update()
        {
            if (ZNet.instance == null)
            {
                return;
            }

            double now = ZNet.instance.GetTimeSeconds();
            List<Player> players = Player.GetAllPlayers();

            for (int i = 0; i < players.Count; i++)
            {
                Player player = players[i];
                if (player != null)
                {
                    Observe(player, now);
                    ObserveBlink(player);
                }
            }

            UpdatePending(now);
            Forget();
        }

        private static void Observe(Player player, double now)
        {
            int count = NetState.GetTransmissionCount(player);
            bool local = player == Player.m_localPlayer;

            if (!LastSeen.TryGetValue(player, out int seen))
            {
                LastSeen[player] = count;

                // Primeira vez que este cliente vê o jogador. Se ele teleportou há pouco, ele está
                // chegando aqui: é quem está no destino, e a ida tocou longe. O dono nunca passa
                // por isto — para ele a ida já é a chegada.
                double at = ArrivalTime(player);
                if (!local && at > 0.0 && now <= at + ArrivalWindow)
                {
                    Schedule(player, at, null);
                }

                return;
            }

            if (count == seen)
            {
                return;
            }

            LastSeen[player] = count;

            // Menor que o visto: o jogador reentrou no mundo e o contador reiniciou.
            if (count < seen)
            {
                return;
            }

            // O dono ouve no clique: é quando a tela dele começa a escurecer, em todo teleporte
            // (o Hud.UpdateBlackScreen olha o IsTeleporting, distante ou não). Pedido do Henrique
            // em 2026-10-15: o som no meio da tela preta lia como atrasado.
            if (local)
            {
                Play(player.transform.position, player.transform, 0f);
                return;
            }

            // Os outros não veem tela nenhuma: veem o jogador parado e, 2 s depois, sumindo. A ida
            // deles se alinha com o sumiço.
            Vector3 origin = player.transform.position;
            Play(origin, null, Mathf.Max(0f, SaiyaheimConfig.TransmissionSoundDelay.Value));

            double arrival = ArrivalTime(player);
            Schedule(player, arrival > 0.0 ? arrival : now + ArrivalDelay(), origin);
        }

        /// <summary>
        /// O teleporte curto: um som só, preso ao jogador, para todo mundo. A posição nova e o
        /// contador chegam na mesma atualização da ZDO, então o som nasce onde ele aparece — que é
        /// o que se vê. Com 30 m de salto, ida e chegada separadas seriam o mesmo som duas vezes.
        /// </summary>
        private static void ObserveBlink(Player player)
        {
            int count = NetState.GetBlinkCount(player);

            if (!LastSeenBlink.TryGetValue(player, out int seen))
            {
                LastSeenBlink[player] = count;
                return;
            }

            if (count == seen)
            {
                return;
            }

            LastSeenBlink[player] = count;

            if (count > seen)
            {
                Play(player.transform.position, player.transform, 0f);
            }
        }

        private static void UpdatePending(double now)
        {
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                PendingArrival pending = Pending[i];

                if (pending.Player == null || now > pending.At + ArrivalWindow)
                {
                    Pending.RemoveAt(i);
                    continue;
                }

                if (now < pending.At)
                {
                    continue;
                }

                Vector3 position = pending.Player.transform.position;
                if (pending.Origin.HasValue &&
                    InstantTransmission.HorizontalDistance(position, pending.Origin.Value) < ArrivalMinMove)
                {
                    continue;
                }

                Play(position, null, 0f);
                Pending.RemoveAt(i);
            }
        }

        private static void Schedule(Player player, double at, Vector3? origin)
        {
            // Um por jogador: dois teleportes seguidos não acumulam chegadas da primeira.
            Pending.RemoveAll(p => p.Player == player);
            Pending.Add(new PendingArrival { Player = player, At = at, Origin = origin });
        }

        /// <summary>Quando a chegada daquele teleporte toca, em segundos de mundo. Negativo se não houve.</summary>
        private static double ArrivalTime(Player player)
        {
            double clicked = NetState.GetTransmissionTime(player);

            return clicked < 0.0 ? -1.0 : clicked + ArrivalDelay();
        }

        private static float ArrivalDelay()
        {
            return Mathf.Max(0f, SaiyaheimConfig.TransmissionArrivalSoundDelay.Value);
        }

        // Jogador que saiu do mundo vira null do lado do Unity; sem a limpeza o dicionário
        // cresceria a cada entrada e saída.
        private static void Forget()
        {
            Gone.Clear();
            foreach (Player player in LastSeen.Keys)
            {
                if (player == null)
                {
                    Gone.Add(player);
                }
            }

            foreach (Player player in Gone)
            {
                LastSeen.Remove(player);
                LastSeenBlink.Remove(player);
            }
        }

        /// <summary>
        /// Toca o som em <paramref name="position"/>, preso a <paramref name="parent"/> se houver,
        /// começando depois de <paramref name="delay"/> segundos. Quem atrasa é o próprio
        /// <c>ZSFX</c> (<c>m_minDelay</c> = <c>m_maxDelay</c>), sem relógio nosso.
        /// </summary>
        private static void Play(Vector3 position, Transform parent, float delay)
        {
            GameObject template = Template();
            if (template == null)
            {
                return;
            }

            ZSFX sfx = template.GetComponent<ZSFX>();

            // Escritos no molde a cada vez, e não só ao criá-lo: o .cfg é relido com o jogo aberto.
            sfx.m_minVol = sfx.m_maxVol = Mathf.Max(0f, SaiyaheimConfig.TransmissionSoundVolume.Value);
            sfx.m_minDelay = sfx.m_maxDelay = delay;

            // Alcance próprio, e não o herdado do soco, que se ouve a poucos metros. Queda linear
            // até zero no alcance: com a curva padrão do Unity o som nunca chega a sumir.
            float range = Mathf.Max(1f, SaiyaheimConfig.TransmissionSoundRange.Value);
            foreach (AudioSource source in template.GetComponentsInChildren<AudioSource>(true))
            {
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = Mathf.Min(5f, range);
                source.maxDistance = range;
            }

            TimedDestruction timed = template.GetComponent<TimedDestruction>();
            timed.m_timeout = delay + sfx.m_audioClips[0].length + 0.5f;

            bool previousDisableInit = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;
            try
            {
                Object.Instantiate(template, position, Quaternion.identity, parent);
            }
            finally
            {
                ZNetView.m_forceDisableInit = previousDisableInit;
            }
        }

        private static GameObject Template()
        {
            if (_template != null || _failed || ZNetScene.instance == null)
            {
                return _template;
            }

            _failed = true;

            AudioClip clip = CustomEffects.GetAudioClip(ClipName);
            GameObject baseSfx = ZNetScene.instance.GetPrefab(BaseSfx);
            if (clip == null || baseSfx == null || baseSfx.GetComponent<ZSFX>() == null)
            {
                SaiyaheimPlugin.Log.LogWarning(
                    $"Instant Transmission sound is off: clip '{ClipName}' or base '{BaseSfx}' is missing.");
                return null;
            }

            // Pai inativo: o Instantiate abaixo não roda o Awake do molde, e o molde nunca toca.
            var holder = new GameObject("Saiyaheim_SfxTemplates");
            holder.SetActive(false);
            Object.DontDestroyOnLoad(holder);

            bool previousDisableInit = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;
            try
            {
                _template = Object.Instantiate(baseSfx, holder.transform);
            }
            finally
            {
                ZNetView.m_forceDisableInit = previousDisableInit;
            }

            _template.name = "sfx_saiyaheim_instant_transmission";

            ZSFX sfx = _template.GetComponent<ZSFX>();
            sfx.m_audioClips = new[] { clip };
            sfx.m_playOnAwake = true;
            sfx.m_minPitch = sfx.m_maxPitch = 1f;
            sfx.m_randomPan = false;
            sfx.m_fadeInDuration = 0f;
            sfx.m_fadeOutOnAwake = false;
            sfx.m_closedCaptionToken = "";
            sfx.m_secondaryCaptionToken = "";
            sfx.m_useVibration = false;

            // Hash próprio: o ZSFX limita sons simultâneos por hash, e o herdado do soco faria um
            // soco por perto calar o teleporte.
            sfx.GenerateHash();

            foreach (AudioSource source in _template.GetComponentsInChildren<AudioSource>(true))
            {
                source.loop = false;
                source.playOnAwake = false;
            }

            // Um relógio só, o nosso: o do soco foi pensado para o clip dele.
            foreach (TimedDestruction old in _template.GetComponentsInChildren<TimedDestruction>(true))
            {
                Object.DestroyImmediate(old);
            }

            TimedDestruction timed = _template.AddComponent<TimedDestruction>();
            timed.m_triggerOnAwake = true;

            _failed = false;
            return _template;
        }
    }
}
