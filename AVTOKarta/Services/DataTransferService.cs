// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using AVTOKarta.Models;

namespace AVTOKarta.Services
{
    /// <summary>
    /// Возникает, если файл выгрузки не удалось расшифровать (неверный пароль
    /// источника либо файл повреждён). Позволяет UI предложить повторить ввод.
    /// </summary>
    public class ExportPasswordException : Exception
    {
        public ExportPasswordException(string message) : base(message) { }
        public ExportPasswordException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Возникает, если файл не является корректной выгрузкой АВТОКарта
    /// либо его содержимое повреждено.
    /// </summary>
    public class ExportValidationException : Exception
    {
        public ExportValidationException(string message) : base(message) { }
        public ExportValidationException(string message, Exception inner) : base(message, inner) { }
    }

    public class DataTransferService : IDisposable
    {
        private readonly string _dataPath;
        private readonly string _password;
        private readonly EncryptionService _localEncryption;
        private bool _disposed;

        private const string MetadataFile = "metadata.json";
        private const string SquadsFile = "squads.json";
        private const string VehiclesFile = "vehicles.json";
        private const string DriversFile = "drivers.json";
        private const string CardsDir = "cards";
        private const string WarehousePrefix = "warehouse_";

        // Магический заголовок новых файлов выгрузки. Старые файлы (без заголовка)
        // по-прежнему читаются — просто считаются «без заголовка».
        private const string HeaderMagic = "AVTOKARTA-EXPORT-1";

        private const long MaxZipEntrySize = 50 * 1024 * 1024;
        private const int MaxZipEntryCount = 20000;
        private const long MaxTotalUncompressed = 500L * 1024 * 1024;

        private static readonly JsonSerializerSettings ImportJsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None
        };

        /// <summary>Версия приложения берётся из сборки (не хардкодим).</summary>
        private static string CurrentAppVersion()
        {
            try
            {
                var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return v == null ? "?" : v.ToString(3);
            }
            catch
            {
                return "?";
            }
        }

        public DataTransferService(string password, string dataPath)
        {
            _dataPath = dataPath;
            _password = password;
            _localEncryption = new EncryptionService(password);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
            {
                _localEncryption?.Dispose();
            }
            _disposed = true;
        }

        // ------------------------------------------------------------------
        //  Выгрузка
        // ------------------------------------------------------------------

        public ExportResult ExportToFile(string filePath, string password, Action<int, int> progress = null)
        {
            ExportBuild build = CreateExportZip(progress);

            using (var exportEncryption = new EncryptionService(password))
            {
                byte[] encrypted = exportEncryption.Encrypt(build.ZipBytes);
                try
                {
                    // Формат файла намеренно НЕ меняем: другие ПК могут работать
                    // на старых сборках, и новый заголовок они не поймут.
                    EncryptionService.AtomicWriteAllBytes(filePath, encrypted);
                }
                catch (Exception ex)
                {
                    throw new IOException("Не удалось сохранить файл выгрузки: " + ex.Message, ex);
                }
            }

            return new ExportResult { Metadata = build.Metadata, SkippedFiles = build.Skipped };
        }

        private class ExportBuild
        {
            public byte[] ZipBytes;
            public ExportMetadata Metadata;
            public int Skipped;
        }

        private enum ExportKind { Squads, Vehicles, Drivers, Warehouse, Card }

        private sealed class ExportSource
        {
            public readonly string EntryName;
            public readonly string Path;
            public readonly ExportKind Kind;

            public ExportSource(string entryName, string path, ExportKind kind)
            {
                EntryName = entryName;
                Path = path;
                Kind = kind;
            }
        }

        private ExportBuild CreateExportZip(Action<int, int> progress)
        {
            var metadata = new ExportMetadata
            {
                ExportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                AppVersion = CurrentAppVersion(),
                SquadCount = 0,
                VehicleCount = 0,
                CardCount = 0
            };
            int skipped = 0;

            // Сначала собираем список файлов — нужно для отчёта о прогрессе
            // и для подсчёта итогов.
            var sources = new List<ExportSource>();

            string squadsPath = Path.Combine(_dataPath, SquadsFile);
            if (File.Exists(squadsPath))
                sources.Add(new ExportSource(SquadsFile, squadsPath, ExportKind.Squads));

            string vehiclesPath = Path.Combine(_dataPath, VehiclesFile);
            if (File.Exists(vehiclesPath))
                sources.Add(new ExportSource(VehiclesFile, vehiclesPath, ExportKind.Vehicles));

            string driversPath = Path.Combine(_dataPath, DriversFile);
            if (File.Exists(driversPath))
                sources.Add(new ExportSource(DriversFile, driversPath, ExportKind.Drivers));

            try
            {
                foreach (string warehouseFile in Directory.GetFiles(_dataPath, WarehousePrefix + "*.json"))
                    sources.Add(new ExportSource(Path.GetFileName(warehouseFile), warehouseFile, ExportKind.Warehouse));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Export: warehouse scan failed: " + ex.Message);
            }

            string cardsPath = Path.Combine(_dataPath, CardsDir);
            if (Directory.Exists(cardsPath))
            {
                foreach (string vehicleDir in Directory.GetDirectories(cardsPath))
                {
                    string plateDir = Path.GetFileName(vehicleDir);
                    foreach (string yearDir in Directory.GetDirectories(vehicleDir))
                    {
                        string year = Path.GetFileName(yearDir);
                        foreach (string cardFile in Directory.GetFiles(yearDir, "*.json"))
                        {
                            string month = Path.GetFileNameWithoutExtension(cardFile);
                            string entryName = string.Format(CardsDir + "/{0}/{1}/{2}.json", plateDir, year, month);
                            sources.Add(new ExportSource(entryName, cardFile, ExportKind.Card));
                        }
                    }
                }
            }

            using (var memoryStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create))
                {
                    int total = sources.Count;
                    if (progress != null)
                        progress(0, total);

                    for (int i = 0; i < sources.Count; i++)
                    {
                        ExportSource src = sources[i];
                        string json;
                        if (TryReadLocalJson(src.Path, out json))
                        {
                            AddEntry(archive, src.EntryName, json);
                            switch (src.Kind)
                            {
                                case ExportKind.Squads:
                                    var squads = SafeDeserialize<List<Squad>>(json);
                                    metadata.SquadCount = squads != null ? squads.Count : 0;
                                    break;
                                case ExportKind.Vehicles:
                                    var vehicles = SafeDeserialize<List<Vehicle>>(json);
                                    metadata.VehicleCount = vehicles != null ? vehicles.Count : 0;
                                    break;
                                case ExportKind.Card:
                                    metadata.CardCount++;
                                    break;
                            }
                        }
                        else
                        {
                            skipped++;
                        }

                        if (progress != null)
                            progress(i + 1, total);
                    }

                    string metaJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                    AddEntry(archive, MetadataFile, metaJson);
                }

                return new ExportBuild
                {
                    ZipBytes = memoryStream.ToArray(),
                    Metadata = metadata,
                    Skipped = skipped
                };
            }
        }

        /// <summary>
        /// Читает локальный зашифрованный JSON. Понимает и старый формат (без HMAC),
        /// заодно «лечит» такой файл на диске. Никогда не бросает исключение.
        /// </summary>
        private bool TryReadLocalJson(string path, out string json)
        {
            json = null;
            try
            {
                byte[] encrypted = File.ReadAllBytes(path);
                bool usedLegacy;
                byte[] decrypted = EncryptionService.DecryptAuto(_password, encrypted, out usedLegacy);
                json = Encoding.UTF8.GetString(decrypted);

                if (usedLegacy)
                {
                    try
                    {
                        byte[] reEncrypted = _localEncryption.Encrypt(decrypted);
                        EncryptionService.AtomicWriteAllBytes(path, reEncrypted);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Local self-migration failed: " + ex.Message);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Export: skipped unreadable file "
                    + path + ": " + ex.Message);
                return false;
            }
        }

        // ------------------------------------------------------------------
        //  Предпросмотр (проверка пароля до любых изменений)
        // ------------------------------------------------------------------

        public ExportPreview PreviewExport(string archivePath, string sourcePassword, string localPassword)
        {
            ValidatedExport validated = LoadAndValidate(archivePath, sourcePassword, localPassword);
            var metadata = validated.Metadata ?? new ExportMetadata();

            if (string.IsNullOrEmpty(metadata.ExportDate))
                metadata.ExportDate = "неизвестно";
            metadata.SquadCount = validated.SquadsJson != null ? CountJsonArray(validated.SquadsJson) : 0;
            metadata.VehicleCount = validated.VehiclesJson != null ? CountJsonArray(validated.VehiclesJson) : 0;
            metadata.CardCount = validated.Cards.Count;

            return new ExportPreview
            {
                Metadata = metadata,
                CardCount = validated.Cards.Count,
                HasSquads = validated.SquadsJson != null,
                HasVehicles = validated.VehiclesJson != null,
                HasDrivers = validated.DriversJson != null
            };
        }

        // ------------------------------------------------------------------
        //  Загрузка
        // ------------------------------------------------------------------

        public ImportResult ImportFromFile(string archivePath, string sourcePassword,
            string targetDataPath, string localPassword, Action<int, int> progress = null)
        {
            ValidatedExport validated = LoadAndValidate(archivePath, sourcePassword, localPassword);

            var result = new ImportResult
            {
                Metadata = validated.Metadata,
                Warnings = validated.Warnings
            };

            // Снимок текущих данных: если запись оборвётся, вернём всё как было.
            string snapshotDir = CreateSnapshot(targetDataPath);
            // Список файлов, существовавших ДО импорта: чтобы при откате удалить
            // вновь созданные (иначе остаются «осиротевшие» warehouse_*.json).
            HashSet<string> filesBefore = ListRelativeFiles(targetDataPath);

            int totalWrites = (validated.SquadsJson != null ? 1 : 0)
                + (validated.VehiclesJson != null ? 1 : 0)
                + (validated.DriversJson != null ? 1 : 0)
                + validated.WarehouseFiles.Count
                + validated.Cards.Count;
            int done = 0;
            if (progress != null)
                progress(0, totalWrites);

            try
            {
                using (var targetEncryption = new EncryptionService(localPassword))
                {
                    if (validated.SquadsJson != null)
                    {
                        EncryptAndWrite(Path.Combine(targetDataPath, SquadsFile),
                            validated.SquadsJson, targetEncryption);
                        result.SquadsImported = true;
                        if (progress != null) progress(++done, totalWrites);
                    }

                    if (validated.VehiclesJson != null)
                    {
                        EncryptAndWrite(Path.Combine(targetDataPath, VehiclesFile),
                            validated.VehiclesJson, targetEncryption);
                        result.VehiclesImported = true;
                        if (progress != null) progress(++done, totalWrites);
                    }

                    if (validated.DriversJson != null)
                    {
                        EncryptAndWrite(Path.Combine(targetDataPath, DriversFile),
                            validated.DriversJson, targetEncryption);
                        result.DriversImported = true;
                        if (progress != null) progress(++done, totalWrites);
                    }

                    foreach (var warehouse in validated.WarehouseFiles)
                    {
                        EncryptAndWrite(Path.Combine(targetDataPath, warehouse.Key),
                            warehouse.Value, targetEncryption);
                        result.WarehouseFilesImported++;
                        if (progress != null) progress(++done, totalWrites);
                    }

                    string normalizedBase = Path.GetFullPath(Path.Combine(targetDataPath, CardsDir));
                    foreach (var card in validated.Cards)
                    {
                        string plateDir = DataService.SanitizeFileName(card.LicensePlate);
                        string destPath = Path.Combine(targetDataPath, CardsDir, plateDir,
                            card.Year.ToString(), card.Month.ToString() + ".json");

                        string normalizedDest = Path.GetFullPath(destPath);
                        if (!normalizedDest.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
                            throw new ExportValidationException(
                                "Файл выгрузки содержит небезопасный путь — импорт отменён.");

                        EncryptAndWrite(destPath, card.Json, targetEncryption);
                        result.CardsImported++;
                        if (progress != null) progress(++done, totalWrites);
                    }
                }
            }
            catch
            {
                RestoreSnapshot(snapshotDir, targetDataPath);
                RemoveFilesNotPresentBefore(targetDataPath, filesBefore);
                throw;
            }
            finally
            {
                DeleteDirectoryQuiet(snapshotDir);
            }

            return result;
        }

        // ------------------------------------------------------------------
        //  Резервная копия
        // ------------------------------------------------------------------

        public string CreateBackup(string password)
        {
            byte[] zipBytes = CreateBackupZipBytes();
            using (var encryption = new EncryptionService(password))
            {
                byte[] encrypted = encryption.Encrypt(zipBytes);

                string backupDir = GetBackupDirectory();
                string randomPart = Path.GetRandomFileName().Replace(".", "");
                if (randomPart.Length > 8) randomPart = randomPart.Substring(0, 8);
                string backupPath = Path.Combine(backupDir,
                    "AVTOKarta_Backup_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")
                    + "_" + randomPart + ".avto");
                try
                {
                    EncryptionService.AtomicWriteAllBytes(backupPath, encrypted);
                }
                catch (Exception ex)
                {
                    throw new IOException("Не удалось создать резервную копию: " + ex.Message, ex);
                }

                return backupPath;
            }
        }

        /// <summary>
        /// Надёжная папка для бэкапов: сначала «Документы», при недоступности
        /// (OneDrive/redirect/нет прав) — локальная папка приложения.
        /// </summary>
        private static string GetBackupDirectory()
        {
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(docs))
                {
                    string dir = Path.Combine(docs, "AVTOKarta_Backups");
                    Directory.CreateDirectory(dir);

                    // Проверяем реальную возможность записи.
                    string probe = Path.Combine(dir, ".write_test");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    return dir;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Documents backup dir unavailable: " + ex.Message);
            }

            string fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AVTOKarta", "Backups");
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        private byte[] CreateBackupZipBytes()
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create))
                {
                    string squadsPath = Path.Combine(_dataPath, SquadsFile);
                    if (File.Exists(squadsPath))
                        AddRawEntry(archive, SquadsFile, squadsPath);

                    string vehiclesPath = Path.Combine(_dataPath, VehiclesFile);
                    if (File.Exists(vehiclesPath))
                        AddRawEntry(archive, VehiclesFile, vehiclesPath);

                    string driversPath = Path.Combine(_dataPath, DriversFile);
                    if (File.Exists(driversPath))
                        AddRawEntry(archive, DriversFile, driversPath);

                    try
                    {
                        foreach (string warehouseFile in Directory.GetFiles(_dataPath, WarehousePrefix + "*.json"))
                            AddRawEntry(archive, Path.GetFileName(warehouseFile), warehouseFile);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Backup: warehouse scan failed: " + ex.Message);
                    }

                    string cardsPath = Path.Combine(_dataPath, CardsDir);
                    if (Directory.Exists(cardsPath))
                    {
                        foreach (string vehicleDir in Directory.GetDirectories(cardsPath))
                        {
                            string plateDir = Path.GetFileName(vehicleDir);
                            foreach (string yearDir in Directory.GetDirectories(vehicleDir))
                            {
                                string year = Path.GetFileName(yearDir);
                                foreach (string cardFile in Directory.GetFiles(yearDir, "*.json"))
                                {
                                    string month = Path.GetFileNameWithoutExtension(cardFile);
                                    string entryPath = string.Format("{0}/{1}/{2}.json", plateDir, year, month);
                                    AddRawEntry(archive, CardsDir + "/" + entryPath, cardFile);
                                }
                            }
                        }
                    }

                    var metadata = new ExportMetadata
                    {
                        ExportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        AppVersion = CurrentAppVersion(),
                        IsBackup = true
                    };
                    string metaJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                    AddEntry(archive, MetadataFile, metaJson);
                }

                return memoryStream.ToArray();
            }
        }

        // ------------------------------------------------------------------
        //  Чтение и проверка выгрузки
        // ------------------------------------------------------------------

        private class ValidatedExport
        {
            public ExportMetadata Metadata;
            public string SquadsJson;
            public string VehiclesJson;
            public string DriversJson;
            public readonly List<KeyValuePair<string, string>> WarehouseFiles =
                new List<KeyValuePair<string, string>>();
            public readonly List<CardEntry> Cards = new List<CardEntry>();
            public readonly List<string> Warnings = new List<string>();
        }

        private class CardEntry
        {
            public string LicensePlate;
            public int Year;
            public int Month;
            public string Json;
        }

        private ValidatedExport LoadAndValidate(string archivePath,
            string sourcePassword, string localPassword)
        {
            byte[] fileBytes;
            try
            {
                fileBytes = File.ReadAllBytes(archivePath);
            }
            catch (Exception ex)
            {
                throw new IOException("Не удалось прочитать файл выгрузки: " + ex.Message, ex);
            }

            byte[] zipBytes = DecryptExport(fileBytes, sourcePassword, localPassword);
            var result = new ValidatedExport();

            try
            {
                using (var stream = new MemoryStream(zipBytes))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    if (archive.Entries.Count > MaxZipEntryCount)
                        throw new ExportValidationException("Файл выгрузки повреждён: слишком много записей.");

                    long total = 0;
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.Length > MaxZipEntrySize)
                            throw new ExportValidationException("Файл выгрузки повреждён: запись слишком большая.");
                        if (entry.Length > 0)
                        {
                            total += entry.Length;
                            if (total > MaxTotalUncompressed)
                                throw new ExportValidationException("Файл выгрузки повреждён: слишком большой объём данных.");
                        }
                    }

                    var metaEntry = archive.GetEntry(MetadataFile);
                    if (metaEntry != null)
                    {
                        try
                        {
                            result.Metadata = JsonConvert.DeserializeObject<ExportMetadata>(ReadEntry(metaEntry));
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine("Metadata parse failed: " + ex.Message);
                        }
                    }

                    var squadsEntry = archive.GetEntry(SquadsFile);
                    if (squadsEntry != null)
                        result.SquadsJson = NormalizeSquads(ReadEntry(squadsEntry));

                    var vehiclesEntry = archive.GetEntry(VehiclesFile);
                    if (vehiclesEntry != null)
                        result.VehiclesJson = NormalizeVehicles(ReadEntry(vehiclesEntry));

                    var driversEntry = archive.GetEntry(DriversFile);
                    if (driversEntry != null)
                        result.DriversJson = NormalizeDrivers(ReadEntry(driversEntry));

                    foreach (var entry in archive.Entries)
                    {
                        string name = entry.FullName.Replace('\\', '/');

                        if (name.StartsWith(CardsDir + "/", StringComparison.OrdinalIgnoreCase)
                            && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            CardEntry card = ParseCardEntry(name, ReadEntry(entry), result.Warnings);
                            if (card != null)
                                result.Cards.Add(card);
                            continue;
                        }

                        string fileName = Path.GetFileName(name);
                        if (fileName.StartsWith(WarehousePrefix, StringComparison.OrdinalIgnoreCase)
                            && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            string normalized = NormalizeWarehouse(ReadEntry(entry));
                            if (normalized != null)
                            {
                                // Имя должно совпадать с DataService.GetWarehousePath:
                                // "warehouse_" + SanitizeFileName(squadId) + ".json".
                                // Санитизируем только идентификатор части: полное имя файла
                                // санитизировать нельзя — SanitizeFileName затирает ".json".
                                string squadPart = Path.GetFileNameWithoutExtension(fileName);
                                if (squadPart.Length >= WarehousePrefix.Length)
                                    squadPart = squadPart.Substring(WarehousePrefix.Length);
                                else
                                    squadPart = string.Empty;
                                string safeName = WarehousePrefix
                                    + DataService.SanitizeFileName(squadPart) + ".json";
                                result.WarehouseFiles.Add(new KeyValuePair<string, string>(
                                    safeName, normalized));
                            }
                        }
                    }
                }
            }
            catch (InvalidDataException ex)
            {
                throw new ExportValidationException(
                    "Файл выгрузки повреждён (не удалось прочитать архив).", ex);
            }

            return result;
        }

        private static byte[] DecryptExport(byte[] fileBytes, string sourcePassword, string localPassword)
        {
            byte[] encrypted = StripHeader(fileBytes);

            if (encrypted.Length < 1)
                throw new ExportValidationException("Файл выгрузки пуст или повреждён.");

            foreach (string candidate in BuildPasswordCandidates(sourcePassword, localPassword))
            {
                try
                {
                    bool usedLegacy;
                    return EncryptionService.DecryptAuto(candidate, encrypted, out usedLegacy);
                }
                catch (CryptographicException)
                {
                    // пробуем следующий вариант пароля
                }
            }

            throw new ExportPasswordException(
                "Не удалось открыть файл выгрузки.\n\n" +
                "Проверьте пароль источника. Если файл выгружали без пароля — оставьте поле пустым.\n" +
                "Если пароль точно верный, файл мог быть повреждён при копировании.");
        }

        private static List<string> BuildPasswordCandidates(string sourcePassword, string localPassword)
        {
            var candidates = new List<string>();

            if (!string.IsNullOrEmpty(sourcePassword))
                candidates.Add(sourcePassword);

            // Пустой пароль = детерминированный seed («выгрузка без пароля»).
            if (!candidates.Contains(string.Empty))
                candidates.Add(string.Empty);

            if (!string.IsNullOrEmpty(localPassword) && !candidates.Contains(localPassword))
                candidates.Add(localPassword);

            return candidates;
        }

        private static string NormalizeSquads(string json)
        {
            var squads = JsonConvert.DeserializeObject<List<Squad>>(json, ImportJsonSettings);
            if (squads == null)
                throw new ExportValidationException("В файле выгрузки повреждён список частей (squads.json).");

            foreach (var squad in squads)
            {
                if (squad.Name == null) squad.Name = string.Empty;
            }
            return JsonConvert.SerializeObject(squads, Formatting.Indented, ImportJsonSettings);
        }

        private static string NormalizeVehicles(string json)
        {
            var vehicles = JsonConvert.DeserializeObject<List<Vehicle>>(json, ImportJsonSettings);
            if (vehicles == null)
                throw new ExportValidationException("В файле выгрузки повреждён список автомобилей (vehicles.json).");

            foreach (var vehicle in vehicles)
            {
                if (vehicle.LicensePlate == null) vehicle.LicensePlate = string.Empty;
                if (vehicle.SquadId == null) vehicle.SquadId = string.Empty;
                if (vehicle.FuelNorms == null) vehicle.FuelNorms = new FuelNorm();
            }
            return JsonConvert.SerializeObject(vehicles, Formatting.Indented, ImportJsonSettings);
        }

        private static string NormalizeDrivers(string json)
        {
            var drivers = JsonConvert.DeserializeObject<List<Driver>>(json, ImportJsonSettings);
            if (drivers == null)
                throw new ExportValidationException("В файле выгрузки повреждён список водителей (drivers.json).");

            foreach (var driver in drivers)
            {
                if (driver.FullName == null) driver.FullName = string.Empty;
            }
            return JsonConvert.SerializeObject(drivers, Formatting.Indented, ImportJsonSettings);
        }

        private static string NormalizeWarehouse(string json)
        {
            var items = JsonConvert.DeserializeObject<List<WarehouseItem>>(json, ImportJsonSettings);
            if (items == null)
                return null;
            return JsonConvert.SerializeObject(items, Formatting.Indented, ImportJsonSettings);
        }

        private static CardEntry ParseCardEntry(string entryName, string json, List<string> warnings)
        {
            string relative = entryName.Substring(CardsDir.Length + 1);
            string[] parts = relative.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                warnings.Add("Пропущена карточка с неверным путём: " + entryName);
                return null;
            }

            int year;
            if (!int.TryParse(parts[1], out year))
            {
                warnings.Add("Пропущена карточка с неверным годом: " + entryName);
                return null;
            }

            string monthStr = Path.GetFileNameWithoutExtension(parts[2]);
            int month;
            if (!int.TryParse(monthStr, out month) || month < 1 || month > 12)
            {
                warnings.Add("Пропущена карточка с неверным месяцем: " + entryName);
                return null;
            }

            try
            {
                var card = JsonConvert.DeserializeObject<MonthlyCard>(json, ImportJsonSettings);
                if (card == null)
                {
                    warnings.Add("Пропущена пустая карточка: " + entryName);
                    return null;
                }

                return new CardEntry
                {
                    LicensePlate = DataService.SanitizeFileName(parts[0]),
                    Year = year,
                    Month = month,
                    Json = JsonConvert.SerializeObject(card, Formatting.Indented, ImportJsonSettings)
                };
            }
            catch (Exception ex)
            {
                warnings.Add("Пропущена повреждённая карточка " + entryName + ": " + ex.Message);
                return null;
            }
        }

        private static int CountJsonArray(string json)
        {
            try
            {
                return Newtonsoft.Json.Linq.JArray.Parse(json).Count;
            }
            catch
            {
                return 0;
            }
        }

        // ------------------------------------------------------------------
        //  Снимок / откат
        // ------------------------------------------------------------------

        private static string CreateSnapshot(string dataPath)
        {
            try
            {
                if (!Directory.Exists(dataPath))
                    return null;

                string snapshotDir = Path.Combine(Path.GetTempPath(),
                    "AVTOKarta_Snapshot_" + Guid.NewGuid().ToString("N"));
                CopyDirectory(dataPath, snapshotDir);
                return snapshotDir;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Snapshot failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>Относительные пути всех файлов под каталогом данных.</summary>
        private static HashSet<string> ListRelativeFiles(string root)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!Directory.Exists(root))
                    return set;

                foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                    set.Add(file.Substring(root.Length).TrimStart('\\', '/'));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ListRelativeFiles failed: " + ex.Message);
            }
            return set;
        }

        /// <summary>
        /// Удаляет файлы, которых не было до импорта (остатки неудачного импорта),
        /// затем подчищает пустые каталоги.
        /// </summary>
        private static void RemoveFilesNotPresentBefore(string dataPath, HashSet<string> before)
        {
            if (before == null || !Directory.Exists(dataPath))
                return;

            try
            {
                foreach (string file in Directory.GetFiles(dataPath, "*", SearchOption.AllDirectories))
                {
                    string rel = file.Substring(dataPath.Length).TrimStart('\\', '/');
                    if (before.Contains(rel))
                        continue;

                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Rollback: не удалось удалить " + file + ": " + ex.Message);
                    }
                }

                // Пустые каталоги (например, cards/<номер>/<год>) после отката.
                var dirs = Directory.GetDirectories(dataPath, "*", SearchOption.AllDirectories);
                Array.Sort(dirs);
                Array.Reverse(dirs);
                foreach (string dir in dirs)
                {
                    try
                    {
                        if (Directory.GetFileSystemEntries(dir).Length == 0)
                            Directory.Delete(dir);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Rollback: не удалось удалить каталог " + dir + ": " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RemoveFilesNotPresentBefore failed: " + ex.Message);
            }
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);

            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);

            foreach (string dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }

        private static void RestoreSnapshot(string snapshotDir, string dataPath)
        {
            if (string.IsNullOrEmpty(snapshotDir) || !Directory.Exists(snapshotDir))
                return;

            try
            {
                // Убираем возможные «полузаписанные» карточки, затем возвращаем снимок.
                DeleteDirectoryQuiet(Path.Combine(dataPath, CardsDir));
                CopyDirectory(snapshotDir, dataPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Restore snapshot failed: " + ex.Message);
            }
        }

        private static void DeleteDirectoryQuiet(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DeleteDirectoryQuiet failed: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------
        //  Низкоуровневые помощники
        // ------------------------------------------------------------------

        private static byte[] StripHeader(byte[] fileBytes)
        {
            byte[] header = Encoding.UTF8.GetBytes(HeaderMagic + "\n");
            if (fileBytes.Length >= header.Length)
            {
                bool match = true;
                for (int i = 0; i < header.Length; i++)
                {
                    if (fileBytes[i] != header[i])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    byte[] rest = new byte[fileBytes.Length - header.Length];
                    Buffer.BlockCopy(fileBytes, header.Length, rest, 0, rest.Length);
                    return rest;
                }
            }
            return fileBytes;
        }

        private static T SafeDeserialize<T>(string json) where T : class
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json, ImportJsonSettings);
            }
            catch
            {
                return null;
            }
        }

        private void AddEntry(ZipArchive archive, string entryName, string content)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }

        private void AddRawEntry(ZipArchive archive, string entryName, string sourceFile)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var entryStream = entry.Open())
            using (var fileStream = File.OpenRead(sourceFile))
            {
                fileStream.CopyTo(entryStream);
            }
        }

        private static string ReadEntry(ZipArchiveEntry entry)
        {
            using (var stream = entry.Open())
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                return reader.ReadToEnd();
            }
        }

        private void EncryptAndWrite(string filePath, string json, EncryptionService encryption)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            byte[] encrypted = encryption.Encrypt(jsonBytes);
            try
            {
                EncryptionService.AtomicWriteAllBytes(filePath, encrypted);
            }
            catch (Exception ex)
            {
                throw new IOException("Не удалось записать файл данных: " + ex.Message, ex);
            }
        }
    }

    public class ExportMetadata
    {
        public string ExportDate { get; set; }
        public string AppVersion { get; set; }
        public int SquadCount { get; set; }
        public int VehicleCount { get; set; }
        public int CardCount { get; set; }
        public bool IsBackup { get; set; }
    }

    public class ExportResult
    {
        public ExportMetadata Metadata { get; set; }
        public int SkippedFiles { get; set; }
    }

    public class ExportPreview
    {
        public ExportMetadata Metadata { get; set; }
        public int CardCount { get; set; }
        public bool HasSquads { get; set; }
        public bool HasVehicles { get; set; }
        public bool HasDrivers { get; set; }
    }

    public class ImportResult
    {
        public ExportMetadata Metadata { get; set; }
        public bool SquadsImported { get; set; }
        public bool VehiclesImported { get; set; }
        public bool DriversImported { get; set; }
        public int CardsImported { get; set; }
        public int WarehouseFilesImported { get; set; }
        public List<string> Warnings { get; set; }

        public ImportResult()
        {
            Warnings = new List<string>();
        }
    }
}
