using System.Collections.Generic;
using UnityEngine;

namespace Saiyaheim.Util
{
    /// <summary>
    /// O espaço de músculos do humanoide da Unity, resolvido por nome.
    ///
    /// <b>O que é.</b> <see cref="HumanPoseHandler"/> expõe a pose como ~95 <b>músculos</b>
    /// nomeados em escala normalizada [-1, 1], onde os extremos são os limites que o próprio
    /// avatar declara. Não é grau nem radiano — e é por isso que os mesmos números funcionam em
    /// qualquer rig humanoide, sem saber nada da bind pose do esqueleto do Valheim.
    ///
    /// <b>⚠️ A convenção de sinal do nome.</b> O nome do músculo é
    /// <i>"direção negativa - direção positiva"</i>: em <c>"Left Arm Down-Up"</c>, −1 é o braço
    /// para baixo e +1 para cima. A regra vale mesmo quando o resultado surpreende — em
    /// <c>"Left Shoulder Down-Up"</c> encolher o ombro é <b>positivo</b>, e em
    /// <c>"Chest Front-Back"</c> inclinar para frente é <b>negativo</b>.
    ///
    /// Duas confirmações vindas de pose já calibrada na tela, em <c>FlightPose</c>: o braço caído
    /// é negativo em <c>Down-Up</c>, e o pé esticado (<c>ToePoint</c>) é positivo em
    /// <c>"Left Foot Up-Down"</c>. As duas obedecem a regra.
    ///
    /// <b>Idempotência.</b> Escrever músculo é seguro de repetir: <see cref="Blend"/> interpola em
    /// direção a um alvo <i>absoluto</i>, então rodar duas vezes no mesmo frame não dobra o efeito.
    /// Quem <b>não</b> é seguro é <c>bodyRotation</c>/<c>bodyPosition</c> — ver o aviso no
    /// <c>FlightPose.PitchForward</c>.
    /// </summary>
    internal static class HumanMuscles
    {
        private static Dictionary<string, int> _index;

        /// <summary>Índice do músculo, ou −1 se este rig não tem esse nome.</summary>
        internal static int IndexOf(string name)
        {
            if (_index == null)
            {
                Build();
            }

            return _index.TryGetValue(name, out int index) ? index : -1;
        }

        /// <summary>
        /// Escreve o músculo interpolado com o que o animator acabou de produzir. O peso é o que
        /// permite a pose entrar <b>por cima</b> da animação vanilla em vez de substituí-la de uma
        /// vez — peso 1 é a pose inteira, 0 é o animator intocado.
        /// </summary>
        internal static void Blend(float[] muscles, string name, float target, float weight)
        {
            Blend(muscles, IndexOf(name), target, weight);
        }

        /// <inheritdoc cref="Blend(float[],string,float,float)"/>
        /// <remarks>
        /// Sobrecarga por índice para quem escreve os mesmos músculos todo frame e já resolveu o
        /// nome uma vez — o punho cerrado toca 30 músculos de dedo por mão.
        /// </remarks>
        internal static void Blend(float[] muscles, int index, float target, float weight)
        {
            if (index < 0 || index >= muscles.Length)
            {
                return;
            }

            muscles[index] = Mathf.Lerp(muscles[index], target, weight);
        }

        /// <summary>
        /// Avisa uma vez sobre nomes que este rig não conhece.
        ///
        /// Um nome que não resolve degrada para "esse músculo não é tocado", o que é uma pose pior
        /// e não um crash. Mas vale saber que aconteceu: numa atualização do jogo que mexa no rig,
        /// é este aviso que explica a pose ter ficado estranha.
        /// </summary>
        internal static void WarnMissing(string owner, params string[] names)
        {
            foreach (string name in names)
            {
                if (IndexOf(name) < 0)
                {
                    SaiyaheimPlugin.Log.LogWarning(
                        $"Muscle '{name}' not found in this rig. {owner} will ignore it.");
                }
            }
        }

        private static void Build()
        {
            _index = new Dictionary<string, int>();

            string[] names = HumanTrait.MuscleName;
            for (int i = 0; i < names.Length; i++)
            {
                _index[names[i]] = i;
            }
        }
    }
}
