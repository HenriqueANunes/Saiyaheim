using System;
using System.Collections.Generic;
using Saiyaheim.Net;
using Saiyaheim.Util;
using UnityEngine;

namespace Saiyaheim.Transformations
{
    /// <summary>
    /// O que se vê e se ouve ao transformar: o grito, o cabelo e a aura.
    ///
    /// Mesma filosofia do <c>KiChargeEffects</c> — nada de asset novo, tudo reaproveitado do jogo,
    /// e os valores visuais em config porque quem vê a tela é o Henrique.
    ///
    /// <b>O grito é um emote oneshot</b>, não em loop: <c>Player.StartEmote</c> escreve na ZDO,
    /// então os outros jogadores veem a animação sem nenhum RPC nosso. Diferente do carregamento
    /// de ki, que segura a pose enquanto a tecla estiver pressionada, aqui é um estouro único — a
    /// transformação é o instante, não o estado.
    ///
    /// <b>O efeito de partículas acompanha o grito</b>, e pela mesma razão: estoura ao subir e
    /// some. A primeira versão o mantinha aceso enquanto a forma durava, e o playtest de
    /// 2026-08-02 recusou — prefab de estouro em loop lê como fumaça presa no personagem. Ver
    /// <see cref="SpawnBurst"/>.
    ///
    /// <b>Nada fica aceso o tempo todo além do cabelo</b>, que é a única coisa persistente do
    /// conjunto — e é ele quem responde "em que forma eu estou" durante os minutos entre um
    /// estouro e o próximo.
    ///
    /// <b>O cabelo é o ponto delicado deste arquivo.</b> Ver <see cref="SetHairColor"/>.
    /// </summary>
    internal static class TransformationEffects
    {
        /// <summary>
        /// O último estouro criado em cada jogador. Normalmente já se autodestruiu quando alguém
        /// olha — só existe para prefabs sem <c>TimedDestruction</c> não se acumularem ao longo da
        /// sessão, e para transformar duas vezes seguidas não somar dois estouros.
        ///
        /// <b>Por jogador desde a etapa 8</b>, como tudo o mais que é visual: dois amigos subindo
        /// de forma ao mesmo tempo são dois estouros, e um campo só apagaria o primeiro.
        ///
        /// As chaves de config continuam se chamando <c>TransformAura*</c> mesmo depois de o
        /// efeito ter deixado de ser uma aura. Renomear chave duplica o <c>.cfg</c> de quem já tem
        /// o arquivo — mesma razão pela qual as chaves do voo mantiveram o prefixo <c>Flight</c>.
        /// </summary>
        private static readonly Dictionary<Player, GameObject> Bursts =
            new Dictionary<Player, GameObject>();

        /// <summary>
        /// Último índice de forma visto em cada jogador. É a memória que transforma a bandeira de
        /// estado do <see cref="NetState"/> no <b>evento</b> "acabou de subir de forma".
        ///
        /// Ausente quer dizer "nunca vi este jogador": anota-se o que ele já é e não se estoura
        /// nada. Sem isso, chegar perto de alguém que está em SSJ há dez minutos dispararia o
        /// efeito de transformação na cara de quem chegou.
        /// </summary>
        private static readonly Dictionary<Player, int> LastSeenForm = new Dictionary<Player, int>();

        /// <summary>
        /// Componente que pinta o modelo do jogador. Cacheado por jogador: o
        /// <c>GetComponent</c> é o mesmo caminho que o <c>Humanoid.Awake</c> usa.
        /// </summary>
        private static VisEquipment _visEquipment;

        private static Player _trackedPlayer;

        /// <summary>True enquanto a cor do cabelo estiver trocada por nós.</summary>
        private static bool _hairTinted;

        private static bool _disabled;

        /// <summary>
        /// Entrou numa forma <b>subindo</b>: grita e pinta o cabelo.
        ///
        /// <b>O estouro não sai daqui desde a etapa 8</b> — quem o dispara é o
        /// <see cref="Observe"/>, vendo o índice da forma subir no canal, e em toda máquina de uma
        /// vez. As duas coisas que sobraram aqui têm em comum o fato de <b>já</b> replicarem
        /// sozinhas e de só poderem ser feitas pelo dono: o emote escreve na ZDO do jogador e o
        /// <c>VisEquipment.SetHairColor</c> também. Estourar daqui seria o terceiro caminho, e o
        /// único que pararia na tela de quem transformou.
        /// </summary>
        internal static void OnPowerUp(Player player, Transformation form)
        {
            Run(() =>
            {
                PlayEmote(player, SaiyaheimConfig.TransformEmote.Value);
                SetHairColor(player, form);
            });
        }

        /// <summary>
        /// Olha o canal deste jogador e estoura o efeito se ele acabou de subir de forma.
        ///
        /// Chamado pelo <see cref="RemoteEffects"/> para <b>todo</b> jogador carregado, o local
        /// inclusive. Barato no caso comum: uma leitura de ZDO e uma comparação de inteiros.
        ///
        /// <b>Só subida estoura.</b> Descer um degrau e voltar à base repintam o cabelo e mais
        /// nada — um estouro ali leria como transformar de novo, que é o oposto do que aconteceu.
        /// A regra é a mesma que o <see cref="OnStepDown"/> já aplicava; agora ela vale para quem
        /// está olhando de fora também.
        /// </summary>
        internal static void Observe(Player player)
        {
            if (player == null)
            {
                return;
            }

            int index = NetState.GetFormIndex(player);

            if (!LastSeenForm.TryGetValue(player, out int seen))
            {
                LastSeenForm[player] = index;
                return;
            }

            if (index == seen)
            {
                return;
            }

            LastSeenForm[player] = index;

            if (index > seen)
            {
                Run(() => SpawnBurst(player, TransformationRegistry.At(index)));
            }
        }

        /// <summary>Este jogador deixou de existir: descarta o que era lembrado dele.</summary>
        internal static void Forget(Player player)
        {
            Bursts.Remove(player);
            LastSeenForm.Remove(player);
        }

        /// <summary>
        /// Trocou de forma <b>descendo</b> um degrau: só repinta o cabelo, sem grito e sem estouro.
        ///
        /// Descer é aliviar o dreno, não um novo estouro de poder — tanto o grito quanto o efeito
        /// leriam como transformar de novo, que é o oposto do que aconteceu. O cabelo troca porque
        /// carrega a identidade do degrau, e continuar com a cor de cima seria mentir sobre onde o
        /// jogador está.
        /// </summary>
        internal static void OnStepDown(Player player, Transformation form)
        {
            Run(() => SetHairColor(player, form));
        }

        /// <summary>
        /// Voltou à base, por tecla ou por ki no zero: devolve o cabelo.
        ///
        /// <b>Não mata o estouro.</b> Ele já acabou sozinho na prática, e cortá-lo aqui só teria
        /// efeito no caso de transformar e sair no mesmo segundo — onde o certo é deixar a
        /// animação terminar, não picotá-la.
        /// </summary>
        internal static void OnPowerDown(Player player)
        {
            Run(() => RestoreHairColor(player));
        }

        /// <summary>
        /// Repinta o cabelo com a forma que o jogador já tem, se tiver alguma.
        ///
        /// Existe porque o jogo apaga a nossa tinta sozinho a cada mudança de equipamento — ver
        /// <see cref="HairColorPatch"/>, que é quem chama. Não é um evento do mod: é um conserto
        /// atrás de um evento do jogo, e por isso não estoura nada nem toca no emote.
        ///
        /// <b>Sem forma ativa não faz nada</b>, de propósito. A cor que o jogo acabou de escrever
        /// é a original, que é exatamente a certa para quem está na base — chamar o
        /// <see cref="RestoreHairColor"/> aqui só reescreveria o mesmo valor.
        ///
        /// A pergunta vai ao <c>SEMan</c> e não ao <c>NetState</c>: o <c>SetupVisEquipment</c> roda
        /// no meio de uma mudança de equipamento, que pode cair no mesmo frame em que a forma mudou
        /// e antes de o estado ter sido publicado. O <c>SEMan</c> é a autoridade na máquina do
        /// dono, que é a única onde a escrita de ZDO vale.
        /// </summary>
        internal static void ReapplyHairColor(Player player)
        {
            Transformation form = TransformationRegistry.GetActive(player);
            if (form == null)
            {
                return;
            }

            Run(() => SetHairColor(player, form));
        }

        /// <summary>
        /// Zera o estado sem tocar no jogador — ele deixou de existir (morte, saída do mundo).
        ///
        /// Não há nada a restaurar nem a destruir nesse caminho: a cor trocada vive na ZDO do
        /// jogador e a aura é filha do transform dele, e as duas já morreram junto. Só as
        /// referências ficaram. Ver <see cref="SetHairColor"/>.
        /// </summary>
        internal static void Reset()
        {
            _visEquipment = null;
            _trackedPlayer = null;
            _hairTinted = false;
            Bursts.Clear();
            LastSeenForm.Clear();
        }

        /// <summary>
        /// Estoura o efeito da forma no jogador.
        ///
        /// <b>É um estouro, não uma aura</b>, e a diferença foi paga em playtest (2026-08-02): o
        /// mesmo prefab mantido aceso enquanto a forma durava leu como fumaça colada no
        /// personagem. Como estouro ele diz "aconteceu alguma coisa agora", que é a única coisa
        /// que um efeito de ativação precisa dizer — o resto do tempo quem carrega a forma é o
        /// cabelo.
        ///
        /// <b>Quem apaga é o timer que nós impomos</b>, não o do prefab — o do prefab pode nunca
        /// disparar, e foi isso que deixou o efeito aceso a forma inteira. Ver
        /// <c>AttachedEffect.PrepareForBurst</c> e a chave <c>TransformAuraDuration</c>.
        ///
        /// O <see cref="Bursts"/> ainda existe para o caso de transformar duas vezes dentro da
        /// duração do estouro: o anterior sai na hora em vez de os dois se somarem.
        /// </summary>
        private static void SpawnBurst(Player player, Transformation form)
        {
            if (Bursts.TryGetValue(player, out GameObject previous))
            {
                if (previous != null)
                {
                    UnityEngine.Object.Destroy(previous);
                }

                Bursts.Remove(player);
            }

            if (form == null)
            {
                return;
            }

            Bursts[player] = AttachedEffect.Spawn(
                player,
                SaiyaheimConfig.TransformAuraPrefab.Value,
                form.Config.AuraColor.Value,
                SaiyaheimConfig.TransformAuraScale.Value,
                SaiyaheimConfig.TransformAuraForceLoop.Value,
                SaiyaheimConfig.TransformAuraLightIntensity.Value,
                SaiyaheimConfig.TransformAuraDuration.Value);
        }

        private static void PlayEmote(Player player, string emote)
        {
            if (player == null || string.IsNullOrEmpty(emote))
            {
                return;
            }

            // oneshot: true = o animator recebe um Trigger e a animação toca uma vez. O
            // StartEmote recusa sozinho se o jogador estiver no meio de um ataque ou preso em
            // algo — e recusar é o certo ali, então o retorno não vira erro nem mensagem.
            player.StartEmote(emote, oneshot: true);
        }

        /// <summary>
        /// Pinta o cabelo com a cor da forma.
        ///
        /// ⚠️ <b>Por que não <c>Player.SetHairColor</c>.</b> Aquele método escreve em
        /// <c>Player.m_hairColor</c>, que é serializado no <b>perfil do personagem</b>
        /// (o <c>.fch</c>, junto de troféus, comidas e nome do cabelo). Um save enquanto
        /// transformado — logout, autosave — gravaria o amarelo como a cor de verdade do
        /// personagem, e ela sobreviveria ao mod ser desinstalado. Corromper a aparência de um
        /// personagem por um efeito temporário é inaceitável.
        ///
        /// O <c>VisEquipment.SetHairColor</c> só escreve na <b>ZDO</b>, que é estado de sessão e
        /// não entra no perfil. O <c>VisEquipment.UpdateColors</c> lê a ZDO todo frame, então a
        /// troca aparece na hora e <b>replica para os outros jogadores de graça</b> — eles veem o
        /// cabelo amarelo sem RPC nosso. E se o jogo fechar com o jogador transformado, a ZDO some
        /// e o cabelo volta ao normal sozinho no próximo login: o pior caso se conserta só.
        ///
        /// O <c>Player.m_hairColor</c> intocado ainda serve de fonte da cor original — é por isso
        /// que <see cref="RestoreHairColor"/> não precisa cachear nada.
        /// </summary>
        private static void SetHairColor(Player player, Transformation form)
        {
            if (form == null || !TryParseHairColor(form, out Vector3 color))
            {
                // Cor inválida ou desligada nesta forma: se havia tinta de uma forma anterior,
                // ela precisa sair — senão o SSJ2 sem cor configurada herdaria o amarelo do SSJ.
                RestoreHairColor(player);
                return;
            }

            VisEquipment vis = GetVisEquipment(player);
            if (vis == null)
            {
                return;
            }

            vis.SetHairColor(color);
            _hairTinted = true;
        }

        private static void RestoreHairColor(Player player)
        {
            if (!_hairTinted)
            {
                return;
            }

            VisEquipment vis = GetVisEquipment(player);
            if (vis == null)
            {
                _hairTinted = false;
                return;
            }

            // A cor original sai do próprio Player, que nunca foi tocado. Nada de cache: um valor
            // guardado aqui ficaria errado se o jogador trocasse de cabelo no espelho transformado.
            vis.SetHairColor(player.GetHairColor());
            _hairTinted = false;
        }

        /// <summary>
        /// A cor da forma como o <c>VisEquipment</c> a quer: um <c>Vector3</c> RGB que pode passar
        /// de 1.
        ///
        /// O hex sozinho não alcança o loiro de anime — ele satura em <c>#FFFFFF</c>, e o shader
        /// do jogo multiplica a textura do cabelo por este valor. Estourar acima de 1 é o que
        /// arde. Daí a intensidade separada: o hex escolhe o <i>tom</i>, a intensidade escolhe
        /// quanto ele queima. É o mesmo par que o criador de personagem do Valheim expõe.
        /// </summary>
        private static bool TryParseHairColor(Transformation form, out Vector3 color)
        {
            color = Vector3.one;

            string raw = form.Config.HairColor.Value;
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }

            if (!ColorUtility.TryParseHtmlString(raw, out Color parsed))
            {
                SaiyaheimPlugin.Log.LogWarning(
                    $"HairColor '{raw}' of {form.DisplayName} is not a valid color. Use the #RRGGBB format.");
                return false;
            }

            float intensity = Mathf.Max(0f, form.Config.HairColorIntensity.Value);
            color = new Vector3(parsed.r, parsed.g, parsed.b) * intensity;

            return true;
        }

        private static VisEquipment GetVisEquipment(Player player)
        {
            if (player == null)
            {
                return null;
            }

            if (!ReferenceEquals(player, _trackedPlayer) || _visEquipment == null)
            {
                _trackedPlayer = player;

                // Mesmo caminho do Humanoid.Awake — o campo m_visEquipment dele é protected, mas
                // o componente está no próprio GameObject do jogador e não precisa de reflexão.
                _visEquipment = player.GetComponent<VisEquipment>();
            }

            return _visEquipment;
        }

        /// <summary>
        /// Efeito visual nunca pode derrubar a mecânica: se algo estourar aqui, desliga os efeitos
        /// e a transformação segue funcionando. Mesmo contrato do <c>KiChargeEffects</c>.
        /// </summary>
        private static void Run(Action action)
        {
            if (_disabled)
            {
                return;
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                _disabled = true;
                Reset();
                SaiyaheimPlugin.Log.LogError($"Transformation effects disabled after an error: {ex}");
            }
        }
    }
}
