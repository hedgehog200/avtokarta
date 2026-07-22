using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AVTOKarta.Models;

namespace AVTOKarta.Services
{
    public class DataService
    {
        private readonly EncryptionService _encryption;
        private readonly string _dataPath;
        private const string SquadsFile = "squads.json";
        private const string OldSquadFile = "squad.json";

        public DataService(string password, string dataPath)
        {
            _encryption = new EncryptionService(password);
            _dataPath = dataPath;
            EnsureDirectories();
            MigrateOldSquad();
        }

        private void EnsureDirectories()
        {
            if (!Directory.Exists(_dataPath))
                Directory.CreateDirectory(_dataPath);

            string cardsPath = Path.Combine(_dataPath, "cards");
            if (!Directory.Exists(cardsPath))
                Directory.CreateDirectory(cardsPath);
        }

        private void MigrateOldSquad()
        {
            string oldPath = Path.Combine(_dataPath, OldSquadFile);
            string newPath = Path.Combine(_dataPath, SquadsFile);
            if (File.Exists(oldPath) && !File.Exists(newPath))
            {
                try
                {
                    var old = ReadEncrypted<Squad>(oldPath);
                    if (old != null)
                    {
                        if (string.IsNullOrEmpty(old.Id))
                            old.Id = Guid.NewGuid().ToString("N").Substring(0, 8);
                        WriteEncrypted(newPath, new List<Squad> { old });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Migration failed: " + ex.Message);
                }
            }
        }

        public List<Squad> LoadSquads()
        {
            string path = Path.Combine(_dataPath, SquadsFile);
            if (!File.Exists(path))
                return new List<Squad>();

            return ReadEncrypted<List<Squad>>(path) ?? new List<Squad>();
        }

        public void SaveSquads(List<Squad> squads)
        {
            string path = Path.Combine(_dataPath, SquadsFile);
            WriteEncrypted(path, squads);
        }

        public Task SaveSquadsAsync(List<Squad> squads)
        {
            return Task.Run(() => SaveSquads(new List<Squad>(squads)));
        }

        public Squad LoadSquad()
        {
            var squads = LoadSquads();
            return squads.Count > 0 ? squads[0] : null;
        }

        public void SaveSquad(Squad squad)
        {
            var squads = LoadSquads();
            int idx = squads.FindIndex(s => s.Id == squad.Id);
            if (idx >= 0)
                squads[idx] = squad;
            else
                squads.Add(squad);
            SaveSquads(squads);
        }

        public List<Vehicle> LoadVehicles()
        {
            string path = Path.Combine(_dataPath, "vehicles.json");
            if (!File.Exists(path))
                return new List<Vehicle>();

            return ReadEncrypted<List<Vehicle>>(path) ?? new List<Vehicle>();
        }

        public void SaveVehicles(List<Vehicle> vehicles)
        {
            string path = Path.Combine(_dataPath, "vehicles.json");
            WriteEncrypted(path, vehicles);
        }

        public Task SaveVehiclesAsync(List<Vehicle> vehicles)
        {
            return Task.Run(() => SaveVehicles(new List<Vehicle>(vehicles)));
        }

        public MonthlyCard LoadCard(string licensePlate, int year, int month)
        {
            string path = GetCardPath(licensePlate, year, month);
            if (!File.Exists(path))
                return null;

            return ReadEncrypted<MonthlyCard>(path);
        }

        public void SaveCard(MonthlyCard card, int year, int month)
        {
            string path = GetCardPath(card.VehicleLicensePlate, year, month);
            WriteEncrypted(path, card);
        }

        public Task SaveCardAsync(MonthlyCard card, int year, int month)
        {
            return Task.Run(() => SaveCard(card, year, month));
        }

        public List<MonthlyCard> LoadAllCards(string licensePlate)
        {
            var cards = new List<MonthlyCard>();
            string vehicleDir = Path.Combine(_dataPath, "cards", SanitizeFileName(licensePlate));

            if (!Directory.Exists(vehicleDir))
                return cards;

            foreach (string yearDir in Directory.GetDirectories(vehicleDir))
            {
                if (int.TryParse(Path.GetFileName(yearDir), out int year))
                {
                    foreach (string cardFile in Directory.GetFiles(yearDir, "*.json"))
                    {
                        string monthStr = Path.GetFileNameWithoutExtension(cardFile);
                        if (int.TryParse(monthStr, out int month))
                        {
                            MonthlyCard card = LoadCard(licensePlate, year, month);
                            if (card != null)
                                cards.Add(card);
                        }
                    }
                }
            }

            return cards;
        }

        public List<WarehouseItem> LoadWarehouseItems(string squadId)
        {
            string path = GetWarehousePath(squadId);
            if (!File.Exists(path))
                return new List<WarehouseItem>();

            return ReadEncrypted<List<WarehouseItem>>(path) ?? new List<WarehouseItem>();
        }

        public void SaveWarehouseItems(string squadId, List<WarehouseItem> items)
        {
            string path = GetWarehousePath(squadId);
            WriteEncrypted(path, items);
        }

        private string GetWarehousePath(string squadId)
        {
            return Path.Combine(_dataPath, "warehouse_" + SanitizeFileName(squadId) + ".json");
        }

        public void BackupCard(string licensePlate, int year, int month)
        {
            string path = GetCardPath(licensePlate, year, month);
            if (File.Exists(path))
            {
                string backupPath = path + ".bak";
                File.Copy(path, backupPath, true);
            }
        }

        private string GetCardPath(string licensePlate, int year, int month)
        {
            return Path.Combine(_dataPath, "cards",
                SanitizeFileName(licensePlate),
                year.ToString(),
                month.ToString() + ".json");
        }

        private static readonly JsonSerializerSettings SafeJsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None
        };

        private string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            name = name.Replace('.', '_');
            if (name == ".." || name == ".")
                name = "_";
            while (name.StartsWith(".."))
                name = name.Substring(1);
            return name;
        }

        private T ReadEncrypted<T>(string path) where T : class
        {
            byte[] encrypted = File.ReadAllBytes(path);
            byte[] decrypted = _encryption.Decrypt(encrypted);
            string json = Encoding.UTF8.GetString(decrypted);
            return JsonConvert.DeserializeObject<T>(json, SafeJsonSettings);
        }

        private void WriteEncrypted<T>(string path, T data)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = JsonConvert.SerializeObject(data, Formatting.Indented, SafeJsonSettings);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            byte[] encrypted = _encryption.Encrypt(jsonBytes);
            File.WriteAllBytes(path, encrypted);
        }
    }
}
