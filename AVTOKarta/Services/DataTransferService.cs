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
    public class DataTransferService
    {
        private readonly string _dataPath;
        private readonly EncryptionService _localEncryption;
        private const string MetadataFile = "metadata.json";
        private const string SquadsFile = "squads.json";
        private const string VehiclesFile = "vehicles.json";
        private const string CardsDir = "cards";

        public DataTransferService(string password, string dataPath)
        {
            _dataPath = dataPath;
            _localEncryption = new EncryptionService(password);
        }

        public void ExportToFile(string filePath, string password)
        {
            byte[] zipBytes = CreateZipBytes();

            var exportEncryption = new EncryptionService(password);
            byte[] encrypted = exportEncryption.Encrypt(zipBytes);
            File.WriteAllBytes(filePath, encrypted);
        }

        public ImportResult ImportFromFile(string archivePath, string sourcePassword, string targetDataPath, string localPassword)
        {
            var result = new ImportResult();

            byte[] fileBytes = File.ReadAllBytes(archivePath);
            var sourceEncryption = new EncryptionService(sourcePassword);
            byte[] zipBytes;

            try
            {
                zipBytes = sourceEncryption.Decrypt(fileBytes);
            }
            catch (CryptographicException)
            {
                throw new InvalidOperationException("Неверный пароль или файл повреждён.");
            }

            using (var stream = new MemoryStream(zipBytes))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var metaEntry = archive.GetEntry(MetadataFile);
                if (metaEntry != null)
                {
                    string metaJson = ReadEntry(metaEntry);
                    result.Metadata = JsonConvert.DeserializeObject<ExportMetadata>(metaJson);
                }

                var targetEncryption = new EncryptionService(localPassword);

                var squadsEntry = archive.GetEntry(SquadsFile);
                if (squadsEntry != null)
                {
                    string json = ReadEntry(squadsEntry);
                    json = SanitizeImportedJson(json);
                    string destPath = Path.Combine(targetDataPath, SquadsFile);
                    EncryptAndWrite(destPath, json, targetEncryption);
                    result.SquadsImported = true;
                }

                var vehiclesEntry = archive.GetEntry(VehiclesFile);
                if (vehiclesEntry != null)
                {
                    string json = ReadEntry(vehiclesEntry);
                    json = SanitizeImportedJson(json);
                    string destPath = Path.Combine(targetDataPath, VehiclesFile);
                    EncryptAndWrite(destPath, json, targetEncryption);
                    result.VehiclesImported = true;
                }

                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.StartsWith(CardsDir + "/") && entry.FullName.EndsWith(".json"))
                    {
                        string relativePath = entry.FullName.Substring(CardsDir.Length + 1);
                        string[] parts = relativePath.Split('/');
                        for (int i = 0; i < parts.Length; i++)
                            parts[i] = SanitizeId(parts[i]);
                        relativePath = string.Join("/", parts);

                        string destPath = Path.Combine(targetDataPath, CardsDir, relativePath);

                        string normalizedDest = Path.GetFullPath(destPath);
                        string normalizedBase = Path.GetFullPath(Path.Combine(targetDataPath, CardsDir));
                        if (!normalizedDest.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Zip contains path traversal — import rejected.");

                        string json = ReadEntry(entry);
                        EncryptAndWrite(destPath, json, targetEncryption);
                        result.CardsImported++;
                    }
                }
            }

            return result;
        }

        public string CreateBackup(string password)
        {
            byte[] zipBytes = CreateBackupZipBytes();
            var encryption = new EncryptionService(password);
            byte[] encrypted = encryption.Encrypt(zipBytes);

            string backupDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string randomPart = Path.GetRandomFileName().Replace(".", "").Substring(0, 8);
            string backupPath = Path.Combine(backupDir, "AVTOKarta_Backup_" + randomPart + ".avto");
            File.WriteAllBytes(backupPath, encrypted);

            return backupPath;
        }

        private byte[] CreateZipBytes()
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create))
                {
                    var metadata = new ExportMetadata
                    {
                        ExportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        AppVersion = "1.0",
                        SquadCount = 0,
                        VehicleCount = 0,
                        CardCount = 0
                    };

                    string squadsPath = Path.Combine(_dataPath, SquadsFile);
                    if (File.Exists(squadsPath))
                    {
                        string json = ReadEncryptedFile(squadsPath);
                        AddEntry(archive, SquadsFile, json);
                        var squads = JsonConvert.DeserializeObject<List<Squad>>(json);
                        if (squads != null) metadata.SquadCount = squads.Count;
                    }

                    string vehiclesPath = Path.Combine(_dataPath, VehiclesFile);
                    if (File.Exists(vehiclesPath))
                    {
                        string json = ReadEncryptedFile(vehiclesPath);
                        AddEntry(archive, VehiclesFile, json);
                        var vehicles = JsonConvert.DeserializeObject<List<Vehicle>>(json);
                        if (vehicles != null) metadata.VehicleCount = vehicles.Count;
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
                                    string json = ReadEncryptedFile(cardFile);
                                    AddEntry(archive, CardsDir + "/" + entryPath, json);
                                    metadata.CardCount++;
                                }
                            }
                        }
                    }

                    string metaJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                    AddEntry(archive, MetadataFile, metaJson);
                }

                return memoryStream.ToArray();
            }
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
                        AppVersion = "1.0",
                        IsBackup = true
                    };
                    string metaJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                    AddEntry(archive, MetadataFile, metaJson);
                }

                return memoryStream.ToArray();
            }
        }

        private string ReadEncryptedFile(string path)
        {
            byte[] encrypted = File.ReadAllBytes(path);
            byte[] decrypted = _localEncryption.Decrypt(encrypted);
            return Encoding.UTF8.GetString(decrypted);
        }

        private void AddEntry(ZipArchive archive, string entryName, string content)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), Encoding.UTF8))
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

        private string ReadEntry(ZipArchiveEntry entry)
        {
            using (var stream = entry.Open())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private string SanitizeImportedJson(string json)
        {
            var squads = JsonConvert.DeserializeObject<List<Squad>>(json);
            if (squads != null)
            {
                foreach (var squad in squads)
                {
                    if (squad.Id != null)
                        squad.Id = SanitizeId(squad.Id);
                    if (squad.Name != null)
                        squad.Name = SanitizeText(squad.Name);
                }
                return JsonConvert.SerializeObject(squads, Formatting.Indented);
            }

            var vehicles = JsonConvert.DeserializeObject<List<Vehicle>>(json);
            if (vehicles != null)
            {
                foreach (var vehicle in vehicles)
                {
                    if (vehicle.LicensePlate != null)
                        vehicle.LicensePlate = SanitizeId(vehicle.LicensePlate);
                }
                return JsonConvert.SerializeObject(vehicles, Formatting.Indented);
            }

            return json;
        }

        private static string SanitizeId(string id)
        {
            id = id.Replace("..", "_");
            foreach (char c in Path.GetInvalidFileNameChars())
                id = id.Replace(c, '_');
            return id;
        }

        private static string SanitizeText(string text)
        {
            return text.Replace("..", "_");
        }

        private void EncryptAndWrite(string filePath, string json, EncryptionService encryption)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            byte[] encrypted = encryption.Encrypt(jsonBytes);
            File.WriteAllBytes(filePath, encrypted);
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

    public class ImportResult
    {
        public ExportMetadata Metadata { get; set; }
        public bool SquadsImported { get; set; }
        public bool VehiclesImported { get; set; }
        public int CardsImported { get; set; }
    }
}
