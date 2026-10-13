using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AVTOKarta.Security;

namespace AVTOKarta.Licensing
{
    public static class LicenseService
    {
        // Открытый ключ лицензирования (из LicGen.pub.xml после LicGen.exe --init).
        // Хранится фрагментами и сверяется по контрольному отпечатку SHA-256,
        // чтобы простая замена одной строки в бинарнике не отключала проверку.
        private const string PublicKeyModulusChunk1 =
            "z5C9fu/T3PczTmre4ri15pqXbIOoxJ2EMzNq+Xedrxc1UOZATyg6G4+Lrg+VANZ0pbS5972rEs1ByDv3ooQtbY2EQxZosRio1kchw6YSJQmhiBX/gY9EnAsjjkV0r67LUwB+iQn2CPXjcXd3fvQq2Wj5gD+nZIKO4Gg5Hs7z++D6";
        private const string PublicKeyModulusChunk2 =
            "FIxZInrbCGIVoNlOFC2t6woMXhLCimr+WiRc1AsEskygXN24IDA274LtqHq8r+EDtJthOkhbEscaWQJJfj354AedtgkpVYXO2IztxyImxnyZy0aTrpduOScnleDF1Gq7TA6hPEt9ETngV05/OMZW/rC6z7CC3jj0mDe9QePkMQ==";
        private const string PublicKeyExponent = "AQAB";
        private const string PublicKeyFingerprint =
            "8a9ef4466a5cbde7910a932fd2a80f1d7175e59bcb22438ad8514518f27ee021";

        private const string LicenseFileName = "license.lic";
        private const string LicenseDirName = "Licenses";
        private const string RollbackMarkerFileName = "last_seen.dat";

        private const double RefreshIntervalSeconds = 30;
        private static DateTime _lastRefreshUtc = DateTime.MinValue;

        private static LicenseFeatures _currentFeatures = LicenseFeatures.Core;
        private static string _resolvedPublicKeyXml;

        // Открытый публичный ключ — общий для лицензий и подписи целостности (tamper.sig).
        public static string PublisherPublicKeyXml
        {
            get
            {
                if (_resolvedPublicKeyXml == null)
                    _resolvedPublicKeyXml = BuildPublisherPublicKeyXml();
                return _resolvedPublicKeyXml;
            }
        }

        // Сверка контрольного отпечатка ключа. Используется TamperGuard'ом:
        // подмена фрагментов ключа в бинарнике трактуется как вмешательство.
        public static bool ValidatePublisherKeyFingerprint()
        {
            try
            {
                string xml = PublisherPublicKeyXml;
                if (string.IsNullOrEmpty(xml))
                    return false;
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(xml));
                    var sb = new StringBuilder(64);
                    foreach (byte b in bytes)
                        sb.Append(b.ToString("x2"));
                    return string.Equals(sb.ToString(), PublicKeyFingerprint, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string BuildPublisherPublicKeyXml()
        {
            var sb = new StringBuilder(512);
            sb.Append("<RSAKeyValue><Modulus>");
            sb.Append(PublicKeyModulusChunk1);
            sb.Append(PublicKeyModulusChunk2);
            sb.Append("</Modulus><Exponent>");
            sb.Append(PublicKeyExponent);
            sb.Append("</Exponent></RSAKeyValue>");
            return sb.ToString();
        }

        public static LicenseFeatures Features
        {
            get { return _currentFeatures; }
        }

        public static bool HasFeature(LicenseFeatures feature)
        {
            try
            {
                EnsureFresh();
            }
            catch
            {
            }

            if (feature == LicenseFeatures.Core)
                return _currentFeatures.Has(LicenseFeatures.Core);

#if PROTECTION
            // Ограничение расширенных функций при вмешательстве — только в релизных сборках.
            if (TamperGuard.IsTampered())
                return false;
#endif

            return _currentFeatures.Has(feature);
        }

        // Периодически перечитывает и перепроверяет файл лицензии,
        // чтобы подмена/удаление лицензии "на горячую" вступала в силу.
        public static void EnsureFresh()
        {
            double elapsed = (DateTime.UtcNow - _lastRefreshUtc).TotalSeconds;
            if (elapsed < RefreshIntervalSeconds)
                return;

            _lastRefreshUtc = DateTime.UtcNow;

            LicenseInfo info;
            string message;
            bool ok = IsLicensed(out info, out message);
            if (!ok)
                _currentFeatures = LicenseFeatures.Core;
        }

        public static string LicenseDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AVTOKarta", LicenseDirName);
            }
        }

        public static string LicenseFilePath
        {
            get { return Path.Combine(LicenseDirectory, LicenseFileName); }
        }

        private static string RollbackMarkerPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AVTOKarta", RollbackMarkerFileName);
            }
        }

        // Защита от продления срока откатом системных часов: дата последнего
        // корректного запуска сохраняется, и если часы ушли назад — лицензия
        // признаётся недействительной до восстановления правильной даты.
        private static bool IsClockSane(out string message)
        {
            message = null;
            try
            {
                DateTime maxSeen = DateTime.MinValue;
                string path = RollbackMarkerPath;
                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path, Encoding.UTF8).Trim();
                    if (!DateTime.TryParseExact(text, "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out maxSeen))
                        maxSeen = DateTime.MinValue;
                }

                if (maxSeen.Date > DateTime.Today)
                {
                    message = "Системные часы переведены назад относительно последнего запуска ("
                            + maxSeen.ToString("yyyy-MM-dd") + ")."
                            + Environment.NewLine
                            + "Восстановите правильную системную дату и запустите программу повторно.";
                    return false;
                }

                string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, today, new UTF8Encoding(false));
                return true;
            }
            catch (Exception)
            {
                return true;
            }
        }

        public static string GetMachineCode()
        {
            return MachineCode.Get();
        }

        public static bool IsLicensed(out LicenseInfo info, out string message)
        {
            info = null;
            message = null;

            string publicKeyXml = PublisherPublicKeyXml;
            if (string.IsNullOrEmpty(publicKeyXml) || publicKeyXml.Contains("REPLACE-WITH-PUBLIC-KEY"))
            {
                message = "Открытый ключ лицензирования не настроен в этом экземпляре ПО.";
                return false;
            }

            if (!File.Exists(LicenseFilePath))
            {
                message = "Файл лицензии не найден. Требуется активация.";
                return false;
            }

            string content;
            try
            {
                content = File.ReadAllText(LicenseFilePath, Encoding.UTF8);
            }
            catch (Exception)
            {
                message = "Не удалось прочитать файл лицензии.";
                return false;
            }

            return TryValidate(content, out info, out message);
        }

        public static bool TryValidateLicenseFile(string path, out LicenseInfo info, out string message)
        {
            info = null;
            message = null;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                message = "Указанный файл лицензии не найден.";
                return false;
            }

            string content;
            try
            {
                content = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception)
            {
                message = "Не удалось прочитать файл лицензии.";
                return false;
            }

            return TryValidate(content, out info, out message);
        }

        private static bool TryValidate(string content, out LicenseInfo info, out string message)
        {
            info = null;
            message = null;

            string payloadBase64, signatureBase64;
            if (!LicenseContainer.TryParse(content, out payloadBase64, out signatureBase64))
            {
                message = "Файл лицензии повреждён или имеет неизвестный формат.";
                return false;
            }

            byte[] payloadBytes;
            byte[] signature;
            try
            {
                payloadBytes = Convert.FromBase64String(payloadBase64);
                signature = Convert.FromBase64String(signatureBase64);
            }
            catch (FormatException)
            {
                message = "Файл лицензии повреждён (неверные кодировки).";
                return false;
            }

            bool signatureOk;
            try
            {
                using (RSACryptoServiceProvider rsa = LicenseCrypto.ImportPublicKeyXml(PublisherPublicKeyXml))
                {
                    rsa.PersistKeyInCsp = false;
                    signatureOk = LicenseCrypto.Verify(rsa, payloadBytes, signature);
                }
            }
            catch (Exception)
            {
                message = "Не удалось проверить подпись лицензии (некорректный открытый ключ).";
                return false;
            }

            if (!signatureOk)
            {
                message = "Подпись лицензии недействительна. Файл подделан или повреждён.";
                return false;
            }

            LicenseInfo parsed;
            if (!LicensePayload.TryParse(Encoding.UTF8.GetString(payloadBytes), out parsed))
            {
                message = "Содержимое лицензии повреждено.";
                return false;
            }
            info = parsed;

            string clockMessage;
            if (!IsClockSane(out clockMessage))
            {
                message = clockMessage;
                return false;
            }

            string machineCode = GetMachineCode();
            if (!string.IsNullOrEmpty(info.MachineCode)
                && !string.Equals(info.MachineCode, machineCode, StringComparison.OrdinalIgnoreCase))
            {
                message = "Лицензия выдана для другого компьютера (код машины не совпадает)." + Environment.NewLine
                        + "Код этой машины: " + machineCode;
                return false;
            }

            if (!info.IsPerpetual)
            {
                DateTime expires;
                if (!DateTime.TryParseExact(info.ExpiresOn, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out expires))
                {
                    message = "Лицензия содержит некорректную дату окончания.";
                    return false;
                }

                if (expires.Date < DateTime.Today)
                {
                    message = "Срок действия лицензии истёк " + info.ExpiresOn + ".";
                    return false;
                }
            }

            if (!info.FeatureFlags.Has(LicenseFeatures.Core))
            {
                message = "Лицензия не включает базовый функционал (Core).";
                return false;
            }

            _currentFeatures = info.FeatureFlags;
            return true;
        }

        public static bool TryInstallLicenseFile(string sourcePath, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                error = "Указанный файл лицензии не найден.";
                return false;
            }

            string content;
            try
            {
                content = File.ReadAllText(sourcePath, Encoding.UTF8);
            }
            catch (Exception)
            {
                error = "Не удалось прочитать файл лицензии.";
                return false;
            }

            string payloadBase64, signatureBase64;
            if (!LicenseContainer.TryParse(content, out payloadBase64, out signatureBase64))
            {
                error = "Файл лицензии повреждён или имеет неизвестный формат.";
                return false;
            }

            byte[] payloadBytes;
            try
            {
                payloadBytes = Convert.FromBase64String(payloadBase64);
                Convert.FromBase64String(signatureBase64);
            }
            catch (FormatException)
            {
                error = "Файл лицензии повреждён (неверные кодировки).";
                return false;
            }

            try
            {
                Directory.CreateDirectory(LicenseDirectory);
                string temp = LicenseFilePath + ".tmp";
                File.WriteAllText(temp, content, Encoding.UTF8);
                File.Copy(temp, LicenseFilePath, true);
                File.Delete(temp);
            }
            catch (Exception)
            {
                error = "Не удалось сохранить файл лицензии.";
                return false;
            }

            return true;
        }
    }
}