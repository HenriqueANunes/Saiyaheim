using System;
using BepInEx.Configuration;

namespace Saiyaheim
{
    /// <summary>
    /// Metadados lidos por reflexão (pelo nome do tipo) tanto pelo ConfigurationManager
    /// quanto pelo SynchronizationManager do Jotunn. Cada mod define a sua própria cópia —
    /// não existe assembly compartilhada para isso.
    ///
    /// O campo que importa aqui é <see cref="IsAdminOnly"/>: no multiplayer (etapa 8) ele
    /// marca quais entradas o servidor impõe aos clientes.
    /// </summary>
    [AttributeUsage(AttributeTargets.All)]
    public sealed class ConfigurationManagerAttributes : Attribute
    {
        /// <summary>Só admin do servidor pode alterar; o valor do servidor vence no cliente.</summary>
        public bool? IsAdminOnly;

        /// <summary>Escondido atrás do "Advanced settings" no ConfigurationManager.</summary>
        public bool? IsAdvanced;

        /// <summary>Ordem de exibição dentro da seção (maior aparece primeiro).</summary>
        public int? Order;

        /// <summary>Sobrescreve o nome da seção na UI.</summary>
        public string Category;

        /// <summary>Desenho customizado da entrada na UI do ConfigurationManager.</summary>
        public Action<ConfigEntryBase> CustomDrawer;
    }
}
