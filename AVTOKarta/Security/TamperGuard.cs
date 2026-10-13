using System;
using System.Collections.Generic;
using AVTOKarta.Licensing;

// Страховка на этапе компиляции: релизную сборку нельзя выпустить без защиты.
// Символ PROTECTION объявлен в конфигурации Release (см. AVTOKarta.csproj).
// Осознанная сборка без защиты помечается символом PROTECTION_DISABLED
// (msbuild /p:NoProtection=true) и ошибкой не считается.
#if !PROTECTION && !DEBUG && !PROTECTION_DISABLED
#error Релизная сборка собрана без символа PROTECTION: код защиты отсутствует. Соберите через build-release.ps1 или Release|x86, либо используйте /p:NoProtection=true для намеренной сборки без защиты.
#endif

namespace AVTOKarta.Security
{
    // Сторож целостности: проверяет подпись exe и наличие отладчика.
    // При обнаружении вмешательства переводит приложение в режим деградации
    // (не-базовые функции отключаются через LicenseService.HasFeature).
    //
    // ВАЖНО: вся логика проверок компилируется ТОЛЬКО при символе PROTECTION
    // (конфигурация Release|x86). В Debug сборке TamperGuard ничего не проверяет,
    // поэтому запуск из Visual Studio и исследование кода не блокируются.
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

#if !PROTECTION
            // Сборка без защиты (Debug и любые не-релизные конфигурации):
            // проверок целостности и отладчика в бинарнике нет — вмешательство не фиксируется.
            _tampered = false;
            ReasonsList.Clear();
            return;
#else
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
#endif
        }
    }
}