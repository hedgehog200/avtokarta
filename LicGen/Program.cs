using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using AVTOKarta.Licensing;

namespace LicGen
{
    internal static class Program
    {
        private static string DefaultKeyDir { get { return Paths.DefaultKeyDir; } }

        private static string PrivateKeyFile { get { return Paths.PrivateKeyFile; } }

        private static string PublicKeyFile { get { return Paths.PublicKeyFile; } }

        // LicGen собирается как WinExe (окно не создаёт консоль). Командный режим
        // подключается к консоли родительского процесса, чтобы вывод был виден в терминале.
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        private static void AttachToConsole()
        {
            AttachConsole(-1);
            try
            {
                Stream stdout = Console.OpenStandardOutput();
                if (stdout != null)
                    Console.SetOut(new StreamWriter(stdout) { AutoFlush = true });
            }
            catch (Exception) { }
            try
            {
                Stream stderr = Console.OpenStandardError();
                if (stderr != null)
                    Console.SetError(new StreamWriter(stderr) { AutoFlush = true });
            }
            catch (Exception) { }
            try { Console.OutputEncoding = Encoding.UTF8; } catch (Exception) { }
            try { Console.InputEncoding = Encoding.UTF8; } catch (Exception) { }
        }

        // Без аргументов и с --gui запускается окно: база лицензий и кодов ПК.
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 0 || args[0].Equals("--gui", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Gui.MainForm());
                return 0;
            }

            AttachToConsole();

            try
            {
                if (args[0].Equals("--console", StringComparison.OrdinalIgnoreCase))
                    return RunInteractive();

                switch (args[0].ToLowerInvariant())
                {
                    case "--init": return RunInit(args);
                    case "--verify": return RunVerify(args);
                    case "--sig-issue": return RunSigIssue(args);
                    case "--sig-check": return RunSigCheck(args);
                    case "--version":
                        Console.WriteLine("LicGen 1.1 (схема лицензий AVTOKARTA + база лицензий)");
                        return 0;
                    default:
                        Console.WriteLine("Неизвестная команда: " + args[0]);
                        PrintUsage();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
                return 1;
            }
            finally
            {
                FreeConsole();
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine();
            Console.WriteLine("Использование:");
            Console.WriteLine("  LicGen.exe                      — окно: база лицензий и кодов ПК (GUI)");
            Console.WriteLine("  LicGen.exe --gui                — то же (графический интерфейс)");
            Console.WriteLine("  LicGen.exe --console            — выпуск лицензии (интерактивный режим)");
            Console.WriteLine("  LicGen.exe --init [--force]     — создание пары ключей RSA-2048");
            Console.WriteLine("  LicGen.exe --verify <file.lic>  — проверка лицензии");
            Console.WriteLine("  LicGen.exe --sig-issue <exe|dir> [--out <sig>] — подпись целостности (манифест всех exe/dll)");
            Console.WriteLine("  LicGen.exe --sig-check <exe|dir> [<sig>] — проверка подписи целостности");
            Console.WriteLine("  LicGen.exe --version            — версия");
        }

        private static int RunInit(string[] args)
        {
            string keyPath = ArgValue(args, "--key") ?? PrivateKeyFile;
            bool force = Array.IndexOf(args, "--force") >= 0;

            if (File.Exists(keyPath) && !force)
            {
                Console.WriteLine("Ключ уже существует: " + keyPath);
                Console.WriteLine("Принудительная перегенерация делается так: LicGen.exe --init --force");
                Console.WriteLine("ВНИМАНИЕ: ранее выданные лицензии станут недействительными!");
                return 1;
            }

            using (RSACryptoServiceProvider rsa = LicenseCrypto.CreateKey(2048))
            {
                rsa.PersistKeyInCsp = false;
                Directory.CreateDirectory(Path.GetDirectoryName(keyPath));
                KeyStore.SavePrivateKey(keyPath, LicenseCrypto.ExportPrivateKeyXml(rsa));
                File.WriteAllText(PublicKeyFile, LicenseCrypto.ExportPublicKeyXml(rsa), Encoding.UTF8);

                Console.WriteLine("Создана пара ключей RSA-2048.");
                Console.WriteLine("  Приватный ключ (защищён DPAPI вашей Windows-учёткой): " + keyPath);
                Console.WriteLine("  Открытый ключ (как текст): " + PublicKeyFile);
                Console.WriteLine();
                Console.WriteLine("Вставьте открытый ключ в LicenseService.cs приложения:");
                Console.WriteLine("------------------------------------------------------------");
                Console.WriteLine(LicenseCrypto.ExportPublicKeyXml(rsa));
                Console.WriteLine("------------------------------------------------------------");
                Console.WriteLine("Код текущей машины: " + MachineCode.Get());
                Console.WriteLine("(используйте его как машину для первой лицензии)");
                return 0;
            }
        }

        private static int RunVerify(string[] args)
        {
            string file = args.Length > 1 ? args[1] : null;
            if (string.IsNullOrEmpty(file))
            {
                Console.WriteLine("Укажите путь к файлу лицензии.");
                return 1;
            }
            if (!File.Exists(file))
            {
                Console.WriteLine("Файл не найден: " + file);
                return 1;
            }
            if (!File.Exists(PublicKeyFile))
            {
                Console.WriteLine("Не найден открытый ключ: " + PublicKeyFile);
                return 1;
            }

            string content = File.ReadAllText(file, Encoding.UTF8);
            string payloadBase64, signatureBase64;
            if (!LicenseContainer.TryParse(content, out payloadBase64, out signatureBase64))
            {
                Console.WriteLine("Файл имеет неизвестный формат.");
                return 1;
            }

            byte[] payloadBytes = Convert.FromBase64String(payloadBase64);
            byte[] signature = Convert.FromBase64String(signatureBase64);

            string publicKeyXml = File.ReadAllText(PublicKeyFile, Encoding.UTF8);
            bool valid;
            using (RSACryptoServiceProvider rsa = LicenseCrypto.ImportPublicKeyXml(publicKeyXml))
            {
                rsa.PersistKeyInCsp = false;
                valid = LicenseCrypto.Verify(rsa, payloadBytes, signature);
            }

            Console.WriteLine("Подпись: " + (valid ? "ДЕЙСТВИТЕЛЬНА" : "НЕ ДЕЙСТВИТЕЛЬНА"));
            if (!valid) return 1;

            LicenseInfo info;
            if (!LicensePayload.TryParse(Encoding.UTF8.GetString(payloadBytes), out info))
            {
                Console.WriteLine("Содержимое лицензии повреждено.");
                return 1;
            }

            PrintLicenseInfo(info);
            return 0;
        }

        private static int RunSigIssue(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Укажите путь к приложению (.exe) или каталогу приложения.");
                return 1;
            }
            string target = args[1];
            bool isDir = Directory.Exists(target);
            if (!isDir && !File.Exists(target))
            {
                Console.WriteLine("Путь не найден: " + target);
                return 1;
            }

            // Если передан exe — подписываем весь его каталог (exe и все соседние DLL).
            string dir = isDir ? target : Path.GetDirectoryName(target);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                Console.WriteLine("Каталог не найден: " + dir);
                return 1;
            }

            string privateKeyXml;
            if (!KeyStore.TryLoadPrivateKey(PrivateKeyFile, out privateKeyXml))
            {
                Console.WriteLine("Не удалось загрузить приватный ключ. Сначала выполните: LicGen.exe --init");
                return 1;
            }

            string outPath = ArgValue(args, "--out");
            if (string.IsNullOrEmpty(outPath))
                outPath = Path.Combine(dir, "tamper.sig");

            using (RSACryptoServiceProvider rsa = LicenseCrypto.ImportPrivateKeyXml(privateKeyXml))
            {
                rsa.PersistKeyInCsp = false;
                List<TamperEntry> entries = TamperManifest.Build(dir, rsa);
                File.WriteAllText(outPath, TamperManifest.Serialize(entries), new UTF8Encoding(false));
            }

            Console.WriteLine("Приложение подписано.");
            Console.WriteLine("  Каталог:   " + Path.GetFullPath(dir));
            Console.WriteLine("  Подпись:   " + Path.GetFullPath(outPath));
            return 0;
        }

        private static int RunSigCheck(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Укажите путь к приложению (.exe) или каталогу приложения.");
                return 1;
            }
            string target = args[1];
            bool isDir = Directory.Exists(target);
            if (!isDir && !File.Exists(target))
            {
                Console.WriteLine("Путь не найден: " + target);
                return 1;
            }
            if (!File.Exists(PublicKeyFile))
            {
                Console.WriteLine("Не найден открытый ключ: " + PublicKeyFile);
                return 1;
            }

            string dir = isDir ? target : Path.GetDirectoryName(target);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                Console.WriteLine("Каталог не найден: " + dir);
                return 1;
            }

            string sigPath = args.Length > 2
                ? args[2]
                : Path.Combine(dir, "tamper.sig");
            if (!File.Exists(sigPath))
            {
                Console.WriteLine("Подпись не найдена: " + sigPath);
                return 1;
            }

            string content = File.ReadAllText(sigPath, Encoding.ASCII);
            List<TamperEntry> entries;
            if (!TamperManifest.TryParse(content, out entries))
            {
                Console.WriteLine("Манифест целостности некорректен.");
                return 1;
            }

            string publicKeyXml = File.ReadAllText(PublicKeyFile, Encoding.UTF8);
            foreach (TamperEntry entry in entries)
            {
                string reason;
                if (!TamperManifest.VerifyEntry(entry, dir, publicKeyXml, out reason))
                {
                    Console.WriteLine("Подпись приложения: НЕ ДЕЙСТВИТЕЛЬНА");
                    Console.WriteLine("  Причина: " + reason);
                    return 1;
                }
            }

            string uncovered = TamperManifest.FindUncoveredFile(dir, entries);
            if (uncovered != null)
            {
                Console.WriteLine("Подпись приложения: НЕ ДЕЙСТВИТЕЛЬНА");
                Console.WriteLine("  Неподписанный компонент: " + uncovered);
                return 1;
            }

            Console.WriteLine("Подпись приложения: ДЕЙСТВИТЕЛЬНА (" + entries.Count + " компонентов: exe/dll)");
            return 0;
        }

        private static int RunInteractive()
        {
            Console.WriteLine("=== Генератор лицензий AVTOKARTA ===");
            Console.WriteLine();

            string keyPath = Ask("Путь к приватному ключу", PrivateKeyFile);
            string privateKeyXml;
            if (!KeyStore.TryLoadPrivateKey(keyPath, out privateKeyXml))
            {
                Console.WriteLine("Не удалось загрузить приватный ключ. Сначала выполните: LicGen.exe --init");
                return 1;
            }

            using (RSACryptoServiceProvider rsa = LicenseCrypto.ImportPrivateKeyXml(privateKeyXml))
            {
                rsa.PersistKeyInCsp = false;

                Console.WriteLine("Введите код машины (пусто = лицензия для любого ПК; или 'this' = для этого ПК):");
                string machineCode = Console.ReadLine().Trim();
                if (machineCode.Equals("this", StringComparison.OrdinalIgnoreCase))
                    machineCode = MachineCode.Get();

                Console.WriteLine("Введите организацию / владельца (например, ГУ МЧС России):");
                string licensee = Console.ReadLine().Trim();

                Console.WriteLine("Срок действия в днях от сегодня (0 = бессрочно):");
                int days = ReadInt(0);

                Console.WriteLine("Возможности (через запятую; пусто = Core; all = всё):");
                Console.WriteLine("  1 — Core (базовая)");
                Console.WriteLine("  2 — Склад ГСМ");
                Console.WriteLine("  4 — Экспорт в Excel");
                Console.WriteLine("  8 — Несколько подразделений");
                Console.WriteLine("  Пример: 2,4 или Warehouse, ExportExcel");
                LicenseFeatures features = ReadFeatures();

                string id = "LIC-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

                var info = new LicenseInfo
                {
                    LicenseId = id,
                    MachineCode = machineCode,
                    Licensee = licensee,
                    IssuedOn = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ExpiresOn = days > 0
                        ? DateTime.UtcNow.AddDays(days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : string.Empty,
                    StationLimit = 1,
                    Features = (long)features
                };

                string content = Signer.CreateLicenseFile(rsa, info);

                string defaultName = string.IsNullOrEmpty(info.MachineCode)
                    ? "license-floating.lic"
                    : "license-" + info.MachineCode.Replace("-", string.Empty).ToLowerInvariant() + ".lic";
                string outPath = Ask("Файл лицензии", defaultName);
                File.WriteAllText(outPath, content, new UTF8Encoding(false));

                Console.WriteLine();
                Console.WriteLine("Готово! Создан файл: " + outPath);
                PrintLicenseInfo(info);
                return 0;
            }
        }

        private static void PrintLicenseInfo(LicenseInfo info)
        {
            Console.WriteLine("----------------------------------------");
            Console.WriteLine("  Лицензия:      " + info.LicenseId);
            Console.WriteLine("  Владелец:      " + (string.IsNullOrEmpty(info.Licensee) ? "(не указан)" : info.Licensee));
            Console.WriteLine("  Машина:        " + (string.IsNullOrEmpty(info.MachineCode) ? "(любой ПК)" : info.MachineCode));
            Console.WriteLine("  Выдана:        " + info.IssuedOn);
            Console.WriteLine("  Действует до:  " + (info.IsPerpetual ? "бессрочно" : info.ExpiresOn));
            Console.WriteLine("  Возможности:   " + FeaturesText(info.FeatureFlags));
            Console.WriteLine("----------------------------------------");
        }

        private static string FeaturesText(LicenseFeatures features)
        {
            return LicenseFeaturesHelper.DisplayList(features);
        }

        private static LicenseFeatures ReadFeatures()
        {
            string input = Console.ReadLine().Trim();
            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine("  Выбрано по умолчанию: Core");
                return LicenseFeatures.Core;
            }

            long value = 0;
            foreach (string part in input.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string token = part.Trim();
                if (token.Equals("all", StringComparison.OrdinalIgnoreCase))
                    return LicenseFeatures.All;

                long num;
                if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out num))
                {
                    value |= num;
                    continue;
                }

                LicenseFeatures f;
                if (Enum.TryParse(token, true, out f))
                {
                    value |= (long)f;
                    continue;
                }

                Console.WriteLine("  Неизвестная возможность пропущена: " + token);
            }

            if (value == 0) value = (long)LicenseFeatures.Core;
            return (LicenseFeatures)value;
        }

        private static int ReadInt(int defaultValue)
        {
            string input = Console.ReadLine().Trim();
            int result;
            return int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
                ? result
                : defaultValue;
        }

        private static string Ask(string label, string defaultValue)
        {
            Console.Write(label + " [" + defaultValue + "]: ");
            string input = Console.ReadLine().Trim();
            return string.IsNullOrEmpty(input) ? defaultValue : input;
        }

        private static string ArgValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }
    }
}