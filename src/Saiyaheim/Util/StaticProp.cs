using System.Collections.Generic;
using UnityEngine;

namespace Saiyaheim.Util
{
    /// <summary>
    /// Um prefab do jogo transformado em <b>enfeite</b>: o visual dele, preso onde se mandar, sem o
    /// comportamento que o fazia ser um objeto do jogo.
    ///
    /// <b>Para que serve.</b> A bola de ki que junta na mão durante o carregamento não existe como
    /// efeito no Valheim — o que existe é o <i>projétil</i>, que já é uma esfera de energia bem
    /// desenhada. O que falta a ele é ficar parado. Então a bola de carregamento é um projétil sem
    /// o <c>Projectile</c>.
    ///
    /// <b>Por que não dá para usar o <see cref="AttachedEffect"/>.</b> Ele instancia com
    /// <c>ZNetView.m_forceDisableInit</c>, e isso faz o <c>ZNetView.Awake</c> chamar
    /// <c>Destroy(this)</c> em si mesmo. Num prefab de efeito não há problema. Num prefab de
    /// projétil há: o <c>Projectile.Awake</c> roda no mesmo <c>Instantiate</c>, guarda esse
    /// <c>ZNetView</c> em vias de morrer, registra RPCs nele e chama <c>UpdateVisual</c> — que
    /// espera uma ZDO que nunca vai existir. É comportamento indefinido no melhor caso.
    ///
    /// <b>A saída: nascer desligado.</b> Um <c>GameObject</c> instanciado dentro de um pai inativo
    /// <b>não roda <c>Awake</c></b>. Com o objeto ainda frio dá para arrancar os componentes de
    /// comportamento e só então acender — e aí não existe mais nenhum <c>Awake</c> problemático
    /// para rodar. É o padrão que a própria Unity documenta para este caso.
    ///
    /// <b><c>DestroyImmediate</c>, e não <c>Destroy</c></b>: o <c>Destroy</c> só acontece no fim do
    /// frame, e acender o objeto antes disso faria o <c>Awake</c> do componente marcado para morrer
    /// rodar assim mesmo — que é exatamente o que se está evitando. Imediato é seguro aqui porque
    /// estes são clones em cena, nunca o asset do prefab.
    /// </summary>
    internal static class StaticProp
    {
        /// <summary>
        /// Põe o visual de <paramref name="prefabName"/> preso a <paramref name="anchor"/>, parado.
        /// Devolve null se o prefab não existe — o que não é erro, é uma chave de config vazia ou
        /// escrita errada, e quem chama decide se isso merece aviso.
        /// </summary>
        /// <param name="strip">
        /// Nomes de emissores a tirar do clone — a fumaça, tipicamente. Um prefab de projétil traz
        /// o rastro que ele deixava ao voar, e um rastro num objeto <b>parado</b> vira uma nuvem
        /// crescendo na mão do personagem. Ver <see cref="StrippedEffect"/>.
        /// </param>
        internal static GameObject Spawn(
            Transform anchor, string prefabName, string colorHex, Vector3 localOffset,
            string[] strip = null)
        {
            if (anchor == null || string.IsNullOrEmpty(prefabName) || ZNetScene.instance == null)
            {
                return null;
            }

            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                SaiyaheimPlugin.Log.LogWarning(
                    $"Prefab '{prefabName}' does not exist. Check the name against the list in the docs.");
                return null;
            }

            // O berço frio. Enquanto o clone estiver dentro dele, nenhum Awake roda.
            GameObject crib = new GameObject("SaiyaheimStaticProp");
            crib.SetActive(false);

            GameObject instance;
            try
            {
                instance = Object.Instantiate(prefab, crib.transform);

                // O nome sem o "(Clone)": ele reaparece no log da próxima sessão como se fosse
                // outro prefab, e este log existe justamente para ser copiado para o .cfg.
                instance.name = prefab.name;

                Strip(instance);

                // Antes de tirar, e não depois: a lista serve para escolher o que tirar, e uma
                // lista já filtrada esconderia justamente a linha que o jogador precisa copiar.
                string available = StrippedEffect.DescribeEmitters(instance);
                List<string> removed = StrippedEffect.StripEmitters(instance, strip);

                SaiyaheimPlugin.LogVerbose(
                    $"Static prop '{prefab.name}': {available}" +
                    $"{(removed.Count == 0 ? "" : $"; stripped {string.Join(", ", removed.ToArray())}")}.");

                // worldPositionStays: false — o que se quer é o offset em espaço do osso, e não
                // manter a posição de mundo que o clone tinha dentro do berço.
                instance.transform.SetParent(anchor, false);
                instance.transform.localPosition = localOffset;
                instance.transform.localRotation = Quaternion.identity;
            }
            finally
            {
                Object.Destroy(crib);
            }

            // Só agora. Daqui para frente o que sobrou é renderer, partícula, luz e som.
            instance.SetActive(true);

            // Depois de acender, e não antes: o Play() de um sistema de partículas num objeto
            // inativo não faz nada. E é necessário porque um prefab de projétil foi feito para uma
            // vida curta — quem o mantinha vivo era o Projectile, que acabou de ser arrancado.
            AttachedEffect.PrepareForSustainedUse(instance);

            AttachedEffect.ApplyTint(instance, colorHex);

            return instance;
        }

        /// <summary>
        /// Arranca tudo que faz o objeto <i>agir</i>, deixando só o que o faz <i>aparecer</i>.
        ///
        /// <list type="bullet">
        /// <item><c>Projectile</c> é o que o faria voar, acertar e morrer no tempo dele.</item>
        /// <item><c>ZNetView</c> e <c>ZSyncTransform</c> o fariam objeto de rede — e a bola de
        /// carregamento é local, criada em cada máquina a partir da bandeira da ZDO, como todo
        /// efeito do mod.</item>
        /// <item><c>Rigidbody</c> e <c>Collider</c> o fariam empurrar o jogador em cuja mão ele
        /// está, ou cair no chão.</item>
        /// </list>
        ///
        /// ⚠️ <c>true</c> em todos os <c>GetComponentsInChildren</c>: o objeto está inativo, e sem
        /// isso a busca não devolveria nada.
        /// </summary>
        private static void Strip(GameObject instance)
        {
            foreach (Projectile projectile in instance.GetComponentsInChildren<Projectile>(true))
            {
                Object.DestroyImmediate(projectile);
            }

            foreach (ZSyncTransform sync in instance.GetComponentsInChildren<ZSyncTransform>(true))
            {
                Object.DestroyImmediate(sync);
            }

            // Depois do ZSyncTransform, de propósito: ele guarda o ZNetView e tirar o dono primeiro
            // deixaria o outro com uma referência morta caso alguma coisa ainda o tocasse.
            foreach (ZNetView view in instance.GetComponentsInChildren<ZNetView>(true))
            {
                Object.DestroyImmediate(view);
            }

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                Object.DestroyImmediate(body);
            }
        }
    }
}
