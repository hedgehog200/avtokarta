using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AVTOKarta.Licensing;

namespace AVTOKarta.Security
{
    // Проверка целостности приложения по RSA-подписанному манифесту (tamper.sig).
    // Манифест создаётся утилитой LicGen (команда --sig-issue) закрытым ключом на этапе
    // публикации и покрывает ВСЕ исполняемые компоненты (*.exe, *.dll) каталога приложения.
    //
    // ВАЖНО: проверка подписи манифеста компилируется ТОЛЬКО при символе PROTECTION
    // (конфигурация Release|x86). В остальных сборках VerifyAppIntegrity всегда успешна,
    // поэтому tamper.sig не требуется и запуск из Visual Studio не блокируется.
    public static class IntegrityChecker
    {
        public static string ApplicationExePath
        {
            get
            {
                string location = Assembly.GetExecutingAssembly().Location;
                // Обфускация с anti-tamper (ConfuserEx) может отдавать пустой Location —
                // тогда каталог берём у предела домена приложения.
                if (!string.IsNullOrEmpty(location))
                    return location;

                string main = Assembly.GetEntryAssembly() == null
                    ? null
                    : Assembly.GetEntryAssembly().Location;
                if (!string.IsNullOrEmpty(main))
                    return main;

                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AVTOKarta.exe");
            }
        }

        public static string ApplicationDirectory
        {
            get
            {
                string dir = Path.GetDirectoryName(ApplicationExePath);
                if (!string.IsNullOrEmpty(dir))
                    return dir;
                return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            }
        }

        public static string SignatureFilePath
        {
            get
            {
                return Path.Combine(ApplicationDirectory, "tamper.sig");
            }
        }

        public static bool VerifyExeIntegrity(out string reason)
        {
            return VerifyAppIntegrity(out reason);
        }

        // Полная проверка всего каталога приложения по манифесту целостности.
        public static bool VerifyAppIntegrity(out string reason)
        {
            reason = null;
#if !PROTECTION
            // Сборка без защиты: проверка целостности не выполняется.
            return true;
#else
            return VerifyDirectory(ApplicationDirectory, SignatureFilePath,
                LicenseService.PublisherPublicKeyXml, out reason);
#endif
        }

        // Проверка каталога по манифесту: каждый компонент из манифеста должен существовать
        // и совпадать по хэшу с ожидаемой подписью, а каждый найденный исполняемый файл
        // должен присутствовать в манифесте (защита от подкидывания несанкционированных DLL).
        public static bool VerifyDirectory(string directory, string signaturePath, string publicKeyXml, out string reason)
        {
            reason = null;
            try
            {
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    reason = "каталог приложения отсутствует";
                    return false;
                }
                if (string.IsNullOrEmpty(signaturePath) || !File.Exists(signaturePath))
                {
                    reason = "отсутствует подпись целостности (tamper.sig)";
                    return false;
                }
                if (string.IsNullOrEmpty(publicKeyXml))
                {
                    reason = "не настроен открытый ключ лицензирования";
                    return false;
                }

                string content;
                try
                {
                    content = File.ReadAllText(signaturePath, Encoding.ASCII);
                }
                catch (Exception ex)
                {
                    reason = "не удалось прочитать tamper.sig: " + ex.Message;
                    return false;
                }

                List<TamperEntry> entries;
                if (!TamperManifest.TryParse(content, out entries))
                {
                    reason = "манифест целостности (tamper.sig) некорректен";
                    return false;
                }

                foreach (TamperEntry entry in entries)
                {
                    string entryReason;
                    if (!TamperManifest.VerifyEntry(entry, directory, publicKeyXml, out entryReason))
                    {
                        reason = entryReason;
                        return false;
                    }
                }

                string uncovered = TamperManifest.FindUncoveredFile(directory, entries);
                if (uncovered != null)
                {
                    reason = "обнаружен неподписанный исполняемый компонент: " + uncovered;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                reason = "ошибка проверки целостности: " + ex.Message;
                return false;
            }
        }

        // Проверка одного файла (совместимость с прежним форматом одиночной подписи).
        public static bool VerifyFile(string exePath, string signaturePath, string publicKeyXml, out string reason)
        {
            reason = null;
            try
            {
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    reason = "файл программы отсутствует";
                    return false;
                }
                if (string.IsNullOrEmpty(signaturePath) || !File.Exists(signaturePath))
                {
                    reason = "отсутствует подпись целостности (tamper.sig)";
                    return false;
                }
                if (string.IsNullOrEmpty(publicKeyXml))
                {
                    reason = "не настроен открытый ключ лицензирования";
                    return false;
                }

                string hash = TamperManifest.ComputeSha256Hex(exePath);
                byte[] hashBytes = Encoding.ASCII.GetBytes(hash);

                string sigBase64 = File.ReadAllText(signaturePath, Encoding.ASCII).Trim();
                byte[] signature = Convert.FromBase64String(sigBase64);

                using (RSACryptoServiceProvider rsa = LicenseCrypto.ImportPublicKeyXml(publicKeyXml))
                {
                    rsa.PersistKeyInCsp = false;
                    bool ok = LicenseCrypto.Verify(rsa, hashBytes, signature);
                    if (!ok)
                        reason = "подпись целостности не совпадает с файлом программы";
                    return ok;
                }
            }
            catch (Exception ex)
            {
                reason = "ошибка проверки целостности: " + ex.Message;
                return false;
            }
        }
    }
}