using System;
using System.Collections.Generic;
using AVTOKarta.Licensing;

namespace AVTOKarta.Security
{
    // Сторож целостности: проверяет подпись exe и наличие отладчика.
    // При обнаружении вмешательства переводит приложение в режим деградации
    // (не-базовые функции отключаются через LicenseService.HasFeature).
    public static class TamperGuard
    {
        private static readonly object Sync = new object();
        private static bool _checked;
        private static bool _tampered;
        private static bool _devBypass;
        private static bool _eventRaised;
        private static readonly List<string> ReasonsList = new List<string>();

        public static event EventHandler TamperDetected;

        public static void SetDevBypass(bool enabled)
        {
            lock (Sync)
            {
                _devBypass = enabled;
                _tampered = false;
                _checked = false;
                _eventRaised = false;
                ReasonsList.Clear();
            }
        }

        public static IList<string> Reasons
        {
            get
            {
                lock (Sync)
                    return ReasonsList.ToArray();
            }
        }

        public static bool IsTampered()
        {
            lock (Sync)
            {
                if (_devBypass)
                    return false;
                if (!_checked)
                    RefreshInternal();
                return _tampered;
            }
        }

        public static bool Refresh()
        {
            bool current;
            lock (Sync)
            {
                if (_devBypass)
                {
                    _tampered = false;
                    current = false;
                }
                else
                {
                    RefreshInternal();
                    current = _tampered;
                }
            }

            if (current)
            {
                bool shouldRaise;
                lock (Sync)
                {
                    shouldRaise = !_eventRaised;
                    if (shouldRaise)
                        _eventRaised = true;
                }
                if (shouldRaise)
                {
                    EventHandler handler = TamperDetected;
                    if (handler != null)
                        handler(null, EventArgs.Empty);
                }
            }

            return current;
        }

        private static void RefreshInternal()
        {
            _checked = true;
            if (_devBypass)
            {
                _tampered = false;
                ReasonsList.Clear();
                return;
            }
            bool tampered = false;
            var reasons = new List<string>();

            string reason;
            if (!IntegrityChecker.VerifyAppIntegrity(out reason))
            {
                tampered = true;
                if (!string.IsNullOrEmpty(reason))
                    reasons.Add(reason);
            }

            IList<string> antiDebug = AntiDebug.Detect();
            if (antiDebug.Count > 0)
            {
                tampered = true;
                reasons.AddRange(antiDebug);
            }

            if (!LicenseService.ValidatePublisherKeyFingerprint())
            {
                tampered = true;
                reasons.Add("изменён открытый ключ лицензирования");
            }

            _tampered = tampered;
            ReasonsList.Clear();
            ReasonsList.AddRange(reasons);
        }
    }
}