using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AVTOKarta.Licensing;

namespace LicGen
{
    // Пути к данным LicGen (ключи и база лицензий хранятся рядом: ..\..\Keys / ..\..\Licenses).
    public static class Paths
    {
        public static string DefaultKeyDir
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Keys"); }
        }

        public static string PrivateKeyFile
        {
            get { return Path.Combine(DefaultKeyDir, "LicGen.key"); }
        }

        public static string PublicKeyFile
        {
            get { return Path.Combine(DefaultKeyDir, "LicGen.pub.xml"); }
        }

        public static string DefaultLicenseDir
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Licenses"); }
        }

        public static string MachinesFile
        {
            get { return Path.Combine(DefaultLicenseDir, "machines.txt"); }
        }
    }

    // Одна запись базы лицензий: подписанный файл .lic + результат его проверки.
    public sealed class LicenseRecord
    {
        public LicenseInfo Info;
        public string FilePath;
        public bool Valid;
        public string Error;

        public bool Expired
        {
            get
            {
                if (Info == null || Info.IsPerpetual) return false;
                DateTime dt;
                return DateTime.TryParse(Info.ExpiresOn, out dt) && dt.Date < DateTime.Now.Date;
            }
        }
    }

    // Код ПК, полученный из лицензий либо внесённый вручную (раздел "Коды ПК").
    public sealed class MachineRecord
    {
        public string MachineCode;
        public string Note;          // владелец/комментарий
        public int LicenseCount;     // число лицензий с этим кодом
        public bool HasActiveLicense;
        public bool FromLicenses;    // код встречается в выданных лицензиях
    }

    // База лицензий и кодов ПК.
    public static class LicenseStore
    {
        public static List<LicenseRecord> LoadAll(string licenseDir, string publicKeyXml)
        {
            var result = new List<LicenseRecord>();
            if (string.IsNullOrEmpty(licenseDir) || !Directory.Exists(licenseDir))
                return result;

            foreach (string file in Directory.GetFiles(licenseDir, "*.lic", SearchOption.TopDirectoryOnly))
            {
                LicenseRecord rec = Read(file, publicKeyXml);
                if (rec != null)
                    result.Add(rec);
            }
            result.Sort((a, b) => string.Compare(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        public static LicenseRecord Read(string file, string publicKeyXml)
        {
            var rec = new LicenseRecord { FilePath = file, Valid = false, Error = null, Info = null };
            try
            {
                string content = File.ReadAllText(file, Encoding.UTF8);
                string payloadBase64, signatureBase64;
                if (!LicenseContainer.TryParse(content, out payloadBase64, out signatureBase64))
                {
                    rec.Error = "неизвестный формат файла";
                    return rec;
                }

                byte[] payload = Convert.FromBase64String(payloadBase64);
                byte[] signature = Convert.FromBase64String(signatureBase64);

                if (string.IsNullOrEmpty(publicKeyXml))
                {
                    rec.Error = "не загружен открытый ключ";
                    return rec;
                }

                bool valid;
                using (RSACryptoServiceProvider rsa = LicenseCrypto.ImportPublicKeyXml(publicKeyXml))
                {
                    rsa.PersistKeyInCsp = false;
                    valid = LicenseCrypto.Verify(rsa, payload, signature);
                }
                if (!valid)
                {
                    rec.Error = "подпись недействительна";
                    return rec;
                }

                LicenseInfo info;
                if (!LicensePayload.TryParse(Encoding.UTF8.GetString(payload), out info))
                {
                    rec.Error = "повреждено содержимое";
                    return rec;
                }

                rec.Info = info;
                rec.Valid = true;
            }
            catch (Exception ex)
            {
                rec.Error = ex.Message;
            }
            return rec;
        }

        public static string Save(LicenseInfo info, string content, string licenseDir, string publicKeyXml)
        {
            if (!Directory.Exists(licenseDir))
                Directory.CreateDirectory(licenseDir);
            string baseName = string.IsNullOrEmpty(info.MachineCode)
                ? "license-floating"
                : "license-" + info.MachineCode.Replace("-", string.Empty).ToLowerInvariant();
            string path = Path.Combine(licenseDir, baseName + ".lic");

            // Не перезаписывать существующий файл — продублировать с суффиксом лицензии.
            int guard = 1;
            while (File.Exists(path))
            {
                path = Path.Combine(licenseDir, baseName + "-" + info.LicenseId.Replace("LIC-", "") + "-" + guard + ".lic");
                guard++;
            }

            File.WriteAllText(path, content, new UTF8Encoding(false));

            // Контроль после записи: файл должен читаться и проходить проверку подписи.
            LicenseRecord check = Read(path, publicKeyXml);
            if (check == null || !check.Valid)
                throw new IOException("Созданный файл лицензии не прошёл проверку подписи: " +
                    (check == null ? "ошибка чтения файла" : check.Error));
            return path;
        }

        // Коды ПК из лицензий (машины, на которые выписаны лицензии).
        public static List<MachineRecord> MachinesFromLicenses(List<LicenseRecord> licenses)
        {
            var map = new Dictionary<string, MachineRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (LicenseRecord rec in licenses)
            {
                if (!rec.Valid || rec.Info == null) continue;
                string code = (rec.Info.MachineCode ?? string.Empty).Trim();
                if (code.Length == 0) continue;

                MachineRecord mr;
                if (!map.TryGetValue(code, out mr))
                {
                    mr = new MachineRecord { MachineCode = code, Note = rec.Info.Licensee };
                    map[code] = mr;
                }
                mr.LicenseCount++;
                if (!rec.Expired)
                    mr.HasActiveLicense = true;
                mr.FromLicenses = true;
                if (string.IsNullOrEmpty(mr.Note))
                    mr.Note = rec.Info.Licensee;
            }
            return new List<MachineRecord>(map.Values);
        }

        // Вручную внесённые коды ПК (например, присланные с установленных машин до выдачи лицензии).
        public static List<MachineRecord> LoadManualMachines()
        {
            var result = new List<MachineRecord>();
            string file = Paths.MachinesFile;
            if (!File.Exists(file))
                return result;

            foreach (string raw in File.ReadAllLines(file, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                string[] parts = line.Split(new[] { ';' }, 2);
                string code = parts[0].Trim();
                if (code.Length == 0)
                    continue;
                result.Add(new MachineRecord
                {
                    MachineCode = code,
                    Note = parts.Length > 1 ? parts[1].Trim() : string.Empty,
                    FromLicenses = false
                });
            }
            return result;
        }

        public static void AppendManualMachine(string code, string note)
        {
            string file = Paths.MachinesFile;
            string dir = Path.GetDirectoryName(file);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.AppendAllText(file, code.Trim() + ";" + (note ?? string.Empty) + Environment.NewLine, Encoding.UTF8);
        }

        // Слияние кодов из лицензий и ручной базы (база "кодов ПК, на которых стоит программа").
        public static List<MachineRecord> AllMachines(List<LicenseRecord> licenses)
        {
            var map = new Dictionary<string, MachineRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (MachineRecord mr in MachinesFromLicenses(licenses))
                map[mr.MachineCode] = mr;

            foreach (MachineRecord mr in LoadManualMachines())
            {
                MachineRecord existing;
                if (map.TryGetValue(mr.MachineCode, out existing))
                {
                    if (!existing.HasActiveLicense)
                        existing.HasActiveLicense = false;
                    if (string.IsNullOrEmpty(existing.Note))
                        existing.Note = mr.Note;
                }
                else
                {
                    map[mr.MachineCode] = mr;
                }
            }

            var result = new List<MachineRecord>(map.Values);
            result.Sort((a, b) => string.Compare(a.MachineCode, b.MachineCode, StringComparison.OrdinalIgnoreCase));
            return result;
        }
    }
}