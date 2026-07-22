using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AVTOKarta.Services
{
    public class CacheService
    {
        private Timer _autoSaveTimer;
        private Action _saveAction;
        private bool _isDirty;
        private readonly object _lock = new object();

        public event Action AutoSaved;

        public CacheService()
        {
            _isDirty = false;
        }

        public void StartAutoSave(Action saveAction, int intervalMs = 60000)
        {
            _saveAction = saveAction;
            _autoSaveTimer = new Timer(AutoSaveCallback, null, intervalMs, intervalMs);
        }

        public void StopAutoSave()
        {
            _autoSaveTimer?.Dispose();
            _autoSaveTimer = null;
        }

        private void AutoSaveCallback(object state)
        {
            bool shouldSave = false;
            lock (_lock)
            {
                if (_isDirty && _saveAction != null)
                {
                    shouldSave = true;
                }
            }

            if (shouldSave)
            {
                try
                {
                    _saveAction.Invoke();
                    lock (_lock) { _isDirty = false; }
                    AutoSaved?.Invoke();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Auto-save error: " + ex.Message);
                }
            }
        }

        public void MarkDirty()
        {
            lock (_lock)
            {
                _isDirty = true;
            }
        }

        public bool IsDirty
        {
            get { lock (_lock) { return _isDirty; } }
        }
    }
}
