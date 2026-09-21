using System;
using System.IO;
using BepInEx.Configuration;

namespace Saiyaheim.Util
{
    /// <summary>
    /// Recarrega o <c>.cfg</c> quando ele muda em disco, conferindo a data de escrita num tick fixo.
    ///
    /// Substitui o <c>ConfigFileWatcher</c> do Jotunn, que usa <c>FileSystemWatcher</c> e <b>não
    /// dispara no servidor dedicado</b> (container, conferido em 2026-09-21: nenhum descritor
    /// inotify aberto e nenhum "Reloading" no log depois de editar o arquivo). No cliente
    /// funcionava, por isso o problema passou despercebido.
    ///
    /// No servidor, recarregar é o que faz a mudança chegar aos jogadores: o <c>Reload</c> dispara
    /// o <c>ConfigReloaded</c>, o <c>SynchronizationManager</c> do Jotunn compara com o que já
    /// mandou e envia a diferença para todos os clientes conectados. Sem reload, editar o
    /// <c>.cfg</c> do servidor só valia depois de reiniciar.
    ///
    /// Polling em vez de evento funciona em qualquer filesystem, e o custo é um <c>stat</c> por tick.
    /// </summary>
    internal sealed class ConfigReloader
    {
        /// <summary>Intervalo entre conferências. Não é balanceamento, é latência de edição.</summary>
        private const float CheckInterval = 2f;

        private readonly ConfigFile _config;
        private readonly Action _onReloaded;
        private DateTime _lastWrite;
        private float _timer;

        internal ConfigReloader(ConfigFile config, Action onReloaded)
        {
            _config = config;
            _onReloaded = onReloaded;
            _lastWrite = ReadLastWrite();
        }

        internal void Update(float dt)
        {
            _timer += dt;
            if (_timer < CheckInterval)
            {
                return;
            }
            _timer = 0f;

            DateTime lastWrite = ReadLastWrite();
            if (lastWrite == _lastWrite)
            {
                return;
            }
            _lastWrite = lastWrite;

            // Sem salvar durante o reload: o Save reescreveria o arquivo, mudaria a data e o
            // próximo tick recarregaria de novo.
            bool saveOnSet = _config.SaveOnConfigSet;
            try
            {
                _config.SaveOnConfigSet = false;
                _config.Reload();
                SaiyaheimPlugin.Log.LogInfo("Config reloaded from disk.");
                _onReloaded?.Invoke();
            }
            catch (Exception e)
            {
                // Arquivo pego no meio da escrita ou com erro de digitação. A próxima gravação
                // muda a data e tenta de novo.
                SaiyaheimPlugin.Log.LogError($"Could not reload config: {e.Message}");
            }
            finally
            {
                _config.SaveOnConfigSet = saveOnSet;
            }
        }

        private DateTime ReadLastWrite()
        {
            try
            {
                return File.GetLastWriteTimeUtc(_config.ConfigFilePath);
            }
            catch (Exception)
            {
                return _lastWrite;
            }
        }
    }
}
