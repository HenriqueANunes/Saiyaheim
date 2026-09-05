using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Saiyaheim.Power
{
    /// <summary>
    /// O poder de luta do <b>inimigo</b>, escrito logo abaixo da barra de vida dele — o outro lado
    /// do <see cref="PowerHud"/>, que mostra o do jogador embaixo do minimapa.
    ///
    /// <b>É aqui que o número da etapa 10 vira jogo.</b> O poder de luta do jogador sozinho é um
    /// placar: sobe, e daí? O que o <see cref="PowerRating"/> foi construído para permitir é a
    /// <b>comparação</b> — os dois lados saem da mesma medida justamente para que ler 8.400 no
    /// troll contra 12.000 seus signifique "eu ganho dele". Sem este arquivo, a metade do número
    /// que decide se você entra na luta nunca aparece na tela.
    ///
    /// <b>Por que é patch Harmony</b>, contra a regra do projeto de preferir API nativa: o
    /// <c>EnemyHud</c> não tem hook nenhum. Ele mantém os huds num <c>Dictionary</c> privado,
    /// monta cada um dentro do <c>ShowHud</c> e atualiza tudo do <c>LateUpdate</c> — não há evento,
    /// não há virtual, não há <c>StatusEffect</c> equivalente. Desenhar por fora também não serve:
    /// quem faz a conta de mundo → tela e some com o hud fora do campo de visão é o próprio
    /// <c>EnemyHud</c>, e refazer isso seria reimplementar a parte difícil para não tocar na fácil.
    ///
    /// <b>O patch é de uma linha de efeito e não lê nada privado.</b> O truque está no
    /// <see cref="ShowHudPatch"/>: em vez de espiar o dicionário privado para achar o hud recém-
    /// criado, ele conta os filhos do <c>m_hudRoot</c> — que é público — antes e depois. Detalhe
    /// no <see cref="ShowHudPatch"/>.
    ///
    /// <b>Só com o ki ligado</b>, pela mesma razão que a barra de ki some junto: o número é uma
    /// leitura que o ki permite fazer, não um widget do jogo. Desligar o toggle devolve o Valheim
    /// cru, e no Valheim cru não há poder de luta para ler.
    ///
    /// <b>Não aparece em cima de jogador</b>, e isso não é gosto: o
    /// <see cref="PowerRating.GetRaw"/> de um jogador remoto lê skill (que não é replicada), o
    /// <c>SEMan</c> dele (que não tem o <c>SE_KiBody</c> nesta máquina) e a arma equipada (que
    /// chega pelo <c>VisEquipment</c>, não pelo inventário). O número sairia, e sairia <b>errado</b>
    /// — mais baixo que o real, o que é pior que não mostrar nada, porque convida o amigo a
    /// duvidar do único número que o mod pede para ele levar a sério. Mostrar poder de outro
    /// jogador exige publicá-lo no <see cref="Net.NetState"/>; é trabalho de outra etapa.
    /// </summary>
    internal static class EnemyPowerHud
    {
        private const string ObjectName = "Saiyaheim_EnemyPower";

        /// <summary>
        /// Desliga tudo depois de um erro, pelo mesmo motivo do <see cref="PowerHud"/> e do
        /// <see cref="Ki.KiHud"/>: isto pendura num caminho que roda todo frame, e uma exceção
        /// viraria dezenas de linhas por segundo no log.
        /// </summary>
        private static bool _disabled;

        /// <summary>
        /// Conta quantas vezes o <c>.cfg</c> foi recarregado. Cada rótulo guarda a geração em que
        /// escreveu pela última vez e, quando ela muda, invalida o próprio cache de valor.
        ///
        /// Sem isso, mexer no <c>EnemyPowerLabel</c> com o jogo aberto pareceria não funcionar: o
        /// texto só é reescrito quando o <b>número</b> muda, e o poder de luta de um bicho parado
        /// não muda nunca. É a mesma armadilha que o <see cref="PowerHud.OnConfigReloaded"/>
        /// resolve com uma linha — aqui precisa de contador porque os rótulos são muitos e
        /// nascem e morrem sozinhos.
        /// </summary>
        private static int _generation;

        internal static void OnConfigReloaded()
        {
            _generation++;
        }

        /// <summary>
        /// Pendura o rótulo num hud recém-criado. Chamado só do <see cref="ShowHudPatch"/>.
        /// </summary>
        private static void Attach(Character character, GameObject gui)
        {
            if (_disabled || character == null || gui == null || character.IsPlayer())
            {
                return;
            }

            // Rede de segurança para a hipótese em que o patch se engana sobre qual hud é o novo:
            // sem ela, um erro ali penduraria um segundo rótulo — com o personagem errado — num
            // hud que já tem o seu. Com ela, o pior caso vira "não aparece", que é visível e
            // inofensivo.
            if (gui.GetComponentInChildren<Label>(true) != null)
            {
                return;
            }

            try
            {
                Create(character, gui);
            }
            catch (Exception e)
            {
                _disabled = true;
                SaiyaheimPlugin.Log.LogError($"Enemy power HUD disabled after an error: {e}");
            }
        }

        /// <summary>
        /// Clona o <b>nome</b> do inimigo para escrever o número embaixo da barra.
        ///
        /// Mesma escolha de molde do <see cref="PowerHud"/>, e pelo mesmo motivo: o que se quer é
        /// um texto solto, sem caixa, com a fonte, o contorno e o alinhamento que o jogo usa
        /// <i>naquele</i> hud. Clonar o nome herda a aparência inteira de graça — e herda também o
        /// que é específico de cada hud, já que o hud de chefe tem um estilo próprio e o clone
        /// acompanha sem nenhum ramo aqui.
        ///
        /// O nome original continua intocado: o <c>EnemyHud</c> guardou a referência dele antes,
        /// e o <c>Find("Name")</c> casa por nome exato — o clone se chama <c>Saiyaheim_EnemyPower</c>.
        /// </summary>
        private static void Create(Character character, GameObject gui)
        {
            Transform name = gui.transform.Find("Name");
            if (name == null)
            {
                _disabled = true;
                SaiyaheimPlugin.Log.LogError(
                    "The enemy hud has no 'Name' child — enemy power HUD disabled.");
                return;
            }

            GameObject label = UnityEngine.Object.Instantiate(name.gameObject, gui.transform);
            label.name = ObjectName;

            TMP_Text text = label.GetComponent<TMP_Text>();
            RectTransform rect = label.GetComponent<RectTransform>();

            if (text == null || rect == null)
            {
                _disabled = true;
                SaiyaheimPlugin.Log.LogError(
                    "The cloned enemy name has no RectTransform or text — enemy power HUD disabled.");
                UnityEngine.Object.Destroy(label);
                return;
            }

            // ⚠️ Sem isto o tamanho da fonte é ignorado — foi o bug do PowerHud no playtest de
            // 2026-09-05 e o molde aqui é do mesmo tipo. O TMP com auto-sizing recalcula o corpo
            // a cada passada de layout e sobrescreve qualquer fontSize que a gente escreva.
            text.enableAutoSizing = false;

            label.AddComponent<Label>().Bind(character, text, rect,
                name is RectTransform nameRect ? nameRect.anchoredPosition : Vector2.zero);
        }

        /// <summary>
        /// A cor, parseada uma vez por valor de config em vez de uma vez por inimigo por frame.
        /// O <c>ColorUtility.TryParseHtmlString</c> é barato, mas não a ponto de rodar dez vezes
        /// por frame para devolver sempre a mesma coisa.
        /// </summary>
        private static string _colorText;
        private static Color _color = Color.white;

        private static Color GetColor()
        {
            string configured = SaiyaheimConfig.EnemyPowerColor.Value;
            if (configured != _colorText)
            {
                _colorText = configured;
                if (!ColorUtility.TryParseHtmlString(configured, out _color))
                {
                    _color = Color.white;
                }
            }

            return _color;
        }

        /// <summary>
        /// Tradução do enum da config para o do TMP. A config não expõe o
        /// <c>HorizontalAlignmentOptions</c> direto para não despejar <c>Justified</c>,
        /// <c>Flush</c> e companhia na lista de valores aceitos do <c>.cfg</c> — nenhum deles
        /// quer dizer coisa alguma numa linha de texto de meia dúzia de caracteres.
        /// </summary>
        private static HorizontalAlignmentOptions ToTmp(HudTextAlign align)
        {
            switch (align)
            {
                case HudTextAlign.Left:
                    return HorizontalAlignmentOptions.Left;
                case HudTextAlign.Right:
                    return HorizontalAlignmentOptions.Right;
                default:
                    return HorizontalAlignmentOptions.Center;
            }
        }

        /// <summary>
        /// O rótulo de <b>um</b> inimigo. É um <c>MonoBehaviour</c> no próprio objeto do texto, e
        /// essa é a peça que dispensa qualquer registro estático: o <c>EnemyHud</c> destrói o hud
        /// quando o bicho sai de vista, o filho vai junto, e este componente some sem ninguém
        /// precisar limpar lista nenhuma. Nada aqui sobrevive ao dono.
        ///
        /// De quebra, o culling sai de graça: o <c>EnemyHud</c> desativa o hud fora do campo de
        /// visão, o <c>LateUpdate</c> de um objeto desativado não roda, e o cálculo do poder de
        /// luta simplesmente não acontece para quem não está na tela.
        /// </summary>
        internal class Label : MonoBehaviour
        {
            private Character _character;
            private TMP_Text _text;
            private RectTransform _rect;

            /// <summary>Posição do nome original, guardada uma vez: o offset da config é relativo a ela.</summary>
            private Vector2 _anchor;

            private int _lastValue = int.MinValue;
            private int _generationSeen = -1;

            internal void Bind(Character character, TMP_Text text, RectTransform rect, Vector2 anchor)
            {
                _character = character;
                _text = text;
                _rect = rect;
                _anchor = anchor;
            }

            /// <summary>
            /// <c>LateUpdate</c> e não <c>Update</c> para escrever depois do <c>EnemyHud</c>, que
            /// também é <c>LateUpdate</c> — não porque haja disputa pelo mesmo campo (o texto do
            /// nome é outro objeto), mas para que ativar/desativar o hud e escrever o número
            /// aconteçam na mesma ordem todo frame.
            /// </summary>
            private void LateUpdate()
            {
                if (_disabled || _character == null || _text == null)
                {
                    return;
                }

                // enabled do texto, e não SetActive no objeto: um objeto desativado não roda
                // LateUpdate, então ele nunca voltaria sozinho quando a chave fosse religada no
                // .cfg — ou quando o ki fosse religado — com o jogo aberto.
                //
                // O ki entra na condição porque ler o poder do inimigo É uma capacidade do ki, e
                // não um enfeite de tela: com o toggle desligado o jogador está jogando Valheim,
                // e Valheim não conta quanto vale um Greydwarf. Mesma regra que a barra de ki já
                // segue (ver KiHud.ShouldBeVisible), e é a leitura local que vale — quem
                // "escaneia" é quem está na frente da tela, não o bicho.
                bool show = SaiyaheimConfig.ShowEnemyPowerOnHud.Value && Ki.KiManager.IsEnabled;
                _text.enabled = show;
                if (!show)
                {
                    return;
                }

                _rect.anchoredPosition = _anchor + new Vector2(
                    SaiyaheimConfig.EnemyPowerOffsetX.Value,
                    SaiyaheimConfig.EnemyPowerOffsetY.Value);

                // Os setters de fontSize e color do TMP já comparam antes de sujar a malha, então
                // reatribuir todo frame não custa nada e dispensa um caminho de "reaplicar".
                _text.fontSize = SaiyaheimConfig.EnemyPowerFontSize.Value;
                _text.color = GetColor();

                // horizontalAlignment e nao alignment: o segundo carrega os dois eixos num
                // inteiro so, entao escrever nele sobrescreveria tambem o alinhamento VERTICAL
                // que veio do molde. Este toca so o eixo que a config pede.
                _text.horizontalAlignment = ToTmp(SaiyaheimConfig.EnemyPowerAlign.Value);

                if (_generationSeen != _generation)
                {
                    _generationSeen = _generation;
                    _lastValue = int.MinValue;
                }

                int value = Mathf.RoundToInt(PowerRating.GetDisplay(_character));
                if (value == _lastValue)
                {
                    return;
                }

                _lastValue = value;
                _text.text = Util.HudText.Prefix(SaiyaheimConfig.EnemyPowerLabel.Value) + value;
            }
        }

        /// <summary>
        /// O único patch, e ele existe só para responder <i>"qual objeto é o hud que acabou de
        /// nascer?"</i>.
        ///
        /// <b>A resposta sai de contar filhos, não de ler o dicionário privado.</b> O
        /// <c>ShowHud</c> cria no máximo um hud por chamada, e cria com
        /// <c>Instantiate(prefab, m_hudRoot.transform)</c> — que anexa como <b>último</b> filho.
        /// Então: se o número de filhos do <c>m_hudRoot</c> subiu em um durante a chamada, o hud
        /// novo é o último, e ele é do personagem que veio no argumento.
        ///
        /// <b>Por que não ler o <c>m_huds</c>, que seria a leitura direta.</b> Ele é privado e o
        /// valor dele é de um tipo <b>aninhado privado</b> (<c>EnemyHud.HudData</c>), que nem dá
        /// para nomear em C# — seria reflexão em dois níveis, com o campo e o tipo podendo mudar
        /// de nome a cada atualização do Valheim. Aqui só se usa membro público
        /// (<c>m_hudRoot</c>) e um comportamento do Unity que não muda. O preço é depender de uma
        /// invariante que não está escrita na assinatura do método, e é por isso que o
        /// <see cref="Attach"/> ainda confere se o hud já tem rótulo antes de pendurar outro.
        ///
        /// A destruição de hud acontece no <c>UpdateHuds</c>, nunca dentro do <c>ShowHud</c>,
        /// então entre o prefixo e o posfixo a contagem só pode ficar igual ou subir em um.
        /// </summary>
        [HarmonyPatch(typeof(EnemyHud), "ShowHud", typeof(Character), typeof(bool))]
        internal static class ShowHudPatch
        {
            private static void Prefix(EnemyHud __instance, out int __state)
            {
                __state = __instance.m_hudRoot == null
                    ? -1
                    : __instance.m_hudRoot.transform.childCount;
            }

            private static void Postfix(EnemyHud __instance, Character c, int __state)
            {
                if (_disabled || __state < 0)
                {
                    return;
                }

                Transform root = __instance.m_hudRoot.transform;
                if (root.childCount != __state + 1)
                {
                    return;
                }

                Attach(c, root.GetChild(root.childCount - 1).gameObject);
            }
        }
    }
}
