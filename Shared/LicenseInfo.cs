using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AVTOKarta.Licensing
{
    [Flags]
    public enum LicenseFeatures
    {
        None = 0x0,
        Core = 0x1,
        Warehouse = 0x2,
        ExportExcel = 0x4,
        MultiSquad = 0x8,
        All = Core | Warehouse | ExportExcel | MultiSquad
    }

    public static class LicenseFeaturesExtensions
    {
        public static bool Has(this LicenseFeatures value, LicenseFeatures feature)
        {
            return (value & feature) == feature;
        }
    }

    public static class LicenseFeaturesHelper
    {
        public static string DisplayName(LicenseFeatures feature)
        {
            switch (feature)
            {
                case LicenseFeatures.Core: return "Core (базовая)";
                case LicenseFeatures.Warehouse: return "Склад ГСМ";
                case LicenseFeatures.ExportExcel: return "Экспорт в Excel";
                case LicenseFeatures.MultiSquad: return "Несколько подразделений";
                default: return feature.ToString();
            }
        }

        public static string DisplayList(LicenseFeatures features)
        {
            var names = new List<string>();
            if (features.Has(LicenseFeatures.Core)) names.Add(DisplayName(LicenseFeatures.Core));
            if (features.Has(LicenseFeatures.Warehouse)) names.Add(DisplayName(LicenseFeatures.Warehouse));
            if (features.Has(LicenseFeatures.ExportExcel)) names.Add(DisplayName(LicenseFeatures.ExportExcel));
            if (features.Has(LicenseFeatures.MultiSquad)) names.Add(DisplayName(LicenseFeatures.MultiSquad));
            return names.Count == 0 ? "нет" : string.Join(", ", names.ToArray());
        }
    }

    public class LicenseInfo
    {
        public string LicenseId { get; set; }
        public string MachineCode { get; set; }
        public string Licensee { get; set; }
        public string IssuedOn { get; set; }
        public string ExpiresOn { get; set; }
        public int StationLimit { get; set; }
        public long Features { get; set; }

        public LicenseFeatures FeatureFlags
        {
            get { return (LicenseFeatures)Features; }
        }

        public bool IsPerpetual
        {
            get { return string.IsNullOrEmpty(ExpiresOn); }
        }
    }

    public static class LicensePayload
    {
        private static readonly Regex Parser = new Regex(
            "^\\{\"licenseId\":\"(.*?)\",\"machineCode\":\"(.*?)\",\"licensee\":\"(.*?)\",\"issuedOn\":\"(.*?)\",\"expiresOn\":\"(.*?)\",\"stationLimit\":(\\d+),\"features\":(\\d+)\\}$",
            RegexOptions.Singleline);

        public static string Serialize(LicenseInfo info)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"licenseId\":\"");
            sb.Append(Escape(info.LicenseId ?? string.Empty));
            sb.Append("\",\"machineCode\":\"");
            sb.Append(Escape(info.MachineCode ?? string.Empty));
            sb.Append("\",\"licensee\":\"");
            sb.Append(Escape(info.Licensee ?? string.Empty));
            sb.Append("\",\"issuedOn\":\"");
            sb.Append(Escape(info.IssuedOn ?? string.Empty));
            sb.Append("\",\"expiresOn\":\"");
            sb.Append(Escape(info.ExpiresOn ?? string.Empty));
            sb.Append("\",\"stationLimit\":");
            sb.Append(info.StationLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"features\":");
            sb.Append(info.Features.ToString(System.Globalization.CultureInfo.InvariantCulture));
            sb.Append("}");
            return sb.ToString();
        }

        public static bool TryParse(string json, out LicenseInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(json)) return false;

            Match m = Parser.Match(json);
            if (!m.Success) return false;

            int stationLimit;
            long features;
            if (!int.TryParse(m.Groups[6].Value, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out stationLimit)) return false;
            if (!long.TryParse(m.Groups[7].Value, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out features)) return false;

            string expiresOn = Unescape(m.Groups[5].Value).Trim();
            info = new LicenseInfo
            {
                LicenseId = Unescape(m.Groups[1].Value),
                MachineCode = Unescape(m.Groups[2].Value).Trim(),
                Licensee = Unescape(m.Groups[3].Value),
                IssuedOn = Unescape(m.Groups[4].Value).Trim(),
                ExpiresOn = expiresOn,
                StationLimit = stationLimit,
                Features = features
            };
            return true;
        }

        public static byte[] GetBytes(LicenseInfo info)
        {
            return Encoding.UTF8.GetBytes(Serialize(info));
        }

        private static string Escape(string value)
        {
            var sb = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static string Unescape(string value)
        {
            if (value == null || value.IndexOf('\\') < 0) return value;
            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' && i + 1 < value.Length)
                {
                    char n = value[++i];
                    switch (n)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 < value.Length)
                            {
                                int code;
                                if (int.TryParse(value.Substring(i + 1, 4),
                                    System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture, out code))
                                {
                                    sb.Append((char)code);
                                    i += 4;
                                    break;
                                }
                            }
                            sb.Append(n);
                            break;
                        default: sb.Append(n); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }

    public static class LicenseContainer
    {
        public const string Header = "AVTOKARTA-LICENSE-1";

        public static string Build(string payloadBase64, string signatureBase64)
        {
            return Header + "\r\n" + payloadBase64 + "\r\n" + signatureBase64 + "\r\n";
        }

        public static bool TryParse(string content, out string payloadBase64, out string signatureBase64)
        {
            payloadBase64 = null;
            signatureBase64 = null;
            if (string.IsNullOrEmpty(content)) return false;

            string[] lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            if (lines.Length < 3) return false;
            if (lines[0].Trim() != Header) return false;

            payloadBase64 = lines[1].Trim();
            signatureBase64 = lines[2].Trim();
            return !string.IsNullOrEmpty(payloadBase64) && !string.IsNullOrEmpty(signatureBase64);
        }
    }

    public static class LicenseCrypto
    {
        public static RSACryptoServiceProvider CreateKey(int keySize = 2048)
        {
            var rsa = new RSACryptoServiceProvider(keySize);
            return rsa;
        }

        public static string ExportPublicKeyXml(RSACryptoServiceProvider rsa)
        {
            return rsa.ToXmlString(false);
        }

        public static string ExportPrivateKeyXml(RSACryptoServiceProvider rsa)
        {
            return rsa.ToXmlString(true);
        }

        public static RSACryptoServiceProvider ImportPublicKeyXml(string publicKeyXml)
        {
            var rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(publicKeyXml);
            return rsa;
        }

        public static RSACryptoServiceProvider ImportPrivateKeyXml(string privateKeyXml)
        {
            var rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(privateKeyXml);
            return rsa;
        }

        public static byte[] Sign(RSACryptoServiceProvider privateKey, byte[] payloadBytes)
        {
            return privateKey.SignData(payloadBytes, "SHA256");
        }

        public static bool Verify(RSACryptoServiceProvider publicKey, byte[] payloadBytes, byte[] signature)
        {
            if (signature == null || signature.Length == 0) return false;
            try
            {
                return publicKey.VerifyData(payloadBytes, "SHA256", signature);
            }
            catch (CryptographicException)
            {
                return false;
            }
        }
    }

    // Манифест целостности приложения (tamper.sig): общий для LicGen (генерация)
    // и IntegrityChecker (проверка). Формат — многострочный:
    //   AVTOKARTA-TAMPER-1
    //   <относительный путь>|<SHA-256 hex>|<base64 RSA-подпись hex-строки>
    // Подписывается каждый исполняемый компонент (*.exe, *.dll) каталога приложения.
    public sealed class TamperEntry
    {
        public string RelativePath;
        public string Sha256Hex;
        public string SignatureBase64;
    }

    public static class TamperManifest
    {
        public const string Header = "AVTOKARTA-TAMPER-1";
        private static readonly char[] Separator = { '|' };

        public static string ComputeSha256Hex(string filePath)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(filePath))
            {
                byte[] hash = sha.ComputeHash(fs);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // Относительный путь внутри каталога приложения (с разделителем '/');
        // возвращает null для служебных артефактов (каталог Confused) — их не подписываем.
        public static string GetRelativePath(string directory, string filePath)
        {
            string fullRoot = Path.GetFullPath(directory).TrimEnd('\\', '/');
            string fullFile = Path.GetFullPath(filePath);
            if (!fullFile.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return null;

            string rel = fullFile.Substring(fullRoot.Length).TrimStart('\\', '/');
            if (rel.Length == 0)
                return null;

            string first = rel.Substring(0, Math.Min(rel.Length, rel.IndexOfAny(new[] { '\\', '/' }) > 0
                ? rel.IndexOfAny(new[] { '\\', '/' })
                : rel.Length));
            if (string.Equals(first, "Confused", StringComparison.OrdinalIgnoreCase))
                return null;

            return rel.Replace('\\', '/');
        }

        public static bool IsCodeFile(string filePath)
        {
            string ext = Path.GetExtension(filePath);
            return ext != null
                && (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".dll", StringComparison.OrdinalIgnoreCase));
        }

        // Деинсталлятор Inno Setup создаёт в каталоге приложения unins000.exe / unins001.exe...
        private static bool IsUninstallerName(string fileName)
        {
            return !string.IsNullOrEmpty(fileName)
                && Regex.IsMatch(fileName, @"^unins\d{3}\.exe$", RegexOptions.IgnoreCase);
        }

        public static string[] EnumerateCodeFiles(string directory)
        {
            var files = new List<string>();
            foreach (string f in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (!IsCodeFile(f))
                    continue;
                if (IsUninstallerName(Path.GetFileName(f)))
                    continue;
                if (string.IsNullOrEmpty(GetRelativePath(directory, f)))
                    continue;
                files.Add(f);
            }
            files.Sort(StringComparer.Ordinal);
            return files.ToArray();
        }

        public static List<TamperEntry> Build(string directory, RSACryptoServiceProvider privateKey)
        {
            var entries = new List<TamperEntry>();
            foreach (string file in EnumerateCodeFiles(directory))
            {
                string rel = GetRelativePath(directory, file);
                string hash = ComputeSha256Hex(file);
                byte[] hashBytes = Encoding.ASCII.GetBytes(hash);
                byte[] signature = LicenseCrypto.Sign(privateKey, hashBytes);
                entries.Add(new TamperEntry
                {
                    RelativePath = rel,
                    Sha256Hex = hash,
                    SignatureBase64 = Convert.ToBase64String(signature)
                });
            }
            return entries;
        }

        public static string Serialize(List<TamperEntry> entries)
        {
            var sb = new StringBuilder(4096);
            sb.Append(Header).Append("\r\n");
            foreach (TamperEntry e in entries)
            {
                sb.Append(e.RelativePath).Append('|');
                sb.Append(e.Sha256Hex).Append('|');
                sb.Append(e.SignatureBase64).Append("\r\n");
            }
            return sb.ToString();
        }

        public static bool TryParse(string content, out List<TamperEntry> entries)
        {
            entries = null;
            if (string.IsNullOrWhiteSpace(content))
                return false;

            string[] lines = content.Replace("\r\n", "\n").Split('\n');
            if (lines.Length < 2)
                return false;
            if (lines[0].Trim() != Header)
                return false;

            var result = new List<TamperEntry>();
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                    continue;

                string[] parts = line.Split(Separator);
                if (parts.Length != 3)
                    return false;
                if (parts[0].Length == 0 || parts[1].Length != 64)
                    return false;

                result.Add(new TamperEntry
                {
                    RelativePath = parts[0],
                    Sha256Hex = parts[1],
                    SignatureBase64 = parts[2]
                });
            }

            if (result.Count == 0)
                return false;
            entries = result;
            return true;
        }

        public static bool VerifyEntry(TamperEntry entry, string directory, string publicKeyXml, out string reason)
        {
            reason = null;
            string fullPath = Path.Combine(directory, entry.RelativePath);
            if (!File.Exists(fullPath))
            {
                reason = "отсутствует компонент из манифеста целостности: " + entry.RelativePath;
                return false;
            }
            if (!IsCodeFile(fullPath))
            {
                reason = "компонент манифеста целостности не является исполняемым: " + entry.RelativePath;
                return false;
            }

            string hash = ComputeSha256Hex(fullPath);
            if (!string.Equals(hash, entry.Sha256Hex, StringComparison.OrdinalIgnoreCase))
            {
                reason = "файл изменён и не соответствует манифесту целостности: " + entry.RelativePath;
                return false;
            }

            byte[] hashBytes = Encoding.ASCII.GetBytes(hash);
            byte[] signature;
            try
            {
                signature = Convert.FromBase64String(entry.SignatureBase64);
            }
            catch (FormatException)
            {
                reason = "некорректная подпись в манифесте (файл: " + entry.RelativePath + ")";
                return false;
            }

            using (RSACryptoServiceProvider rsa = LicenseCrypto.ImportPublicKeyXml(publicKeyXml))
            {
                rsa.PersistKeyInCsp = false;
                if (!LicenseCrypto.Verify(rsa, hashBytes, signature))
                {
                    reason = "подпись целостности не совпадает (файл: " + entry.RelativePath + ")";
                    return false;
                }
            }
            return true;
        }

        // Поиск исполняемого файла в каталоге, отсутствующего в манифесте:
        // подкинутая DLL/EXE (DLL-planting) должна быть выявлена, даже если подписана отдельно.
        public static string FindUncoveredFile(string directory, List<TamperEntry> entries)
        {
            var manifest = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (TamperEntry e in entries)
                manifest[e.RelativePath] = true;

            foreach (string f in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (!IsCodeFile(f))
                    continue;
                if (IsUninstallerName(Path.GetFileName(f)))
                    continue;
                string rel = GetRelativePath(directory, f);
                if (string.IsNullOrEmpty(rel))
                    continue;
                if (!manifest.ContainsKey(rel))
                    return rel;
            }
            return null;
        }
    }
}