// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace AVTOKarta.Services
{
    public class UpdateService
    {
        private const string RepoOwner = "hedgehog200";
        private const string RepoName = "avtokarta";
        private const string ApiUrl = "https://api.github.com/repos/" + RepoOwner + "/" + RepoName + "/releases/latest";
        private const string LogFile = "updater.log";
        private static Mutex _updateMutex;

        public static Version GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }

        private static string GetLogPath()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AVTOKarta");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return Path.Combine(dir, LogFile);
            }
            catch
            {
                try
                {
                    return Path.Combine(Path.GetTempPath(), LogFile);
                }
                catch
                {
                    return LogFile;
                }
            }
        }

        public static void Log(string message)
        {
            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine;
                string path = GetLogPath();
                File.AppendAllText(path, line, System.Text.Encoding.UTF8);
            }
            catch
            {
                try
                {
                    Debug.WriteLine("[UpdateService] " + message);
                }
                catch
                {
                }
            }
        }

        public UpdateResult CheckForUpdate()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var request = (HttpWebRequest)WebRequest.Create(ApiUrl);
                request.UserAgent = "AVTOKarta-Updater";
                request.Accept = "application/vnd.github.v3+json";
                request.AllowAutoRedirect = true;
                request.MaximumAutomaticRedirections = 5;

                string json;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    json = reader.ReadToEnd();
                }

                var release = JObject.Parse(json);

                    string tagName = release["tag_name"]?.ToString() ?? "";
                    string body = release["body"]?.ToString() ?? "";
                    string publishedAt = release["published_at"]?.ToString() ?? "";

                    string cleanVersion = tagName.TrimStart('v', 'V');
                    cleanVersion = Regex.Replace(cleanVersion, @"[^\d\.]", "");

                    Version remoteVersion;
                    if (!Version.TryParse(cleanVersion, out remoteVersion))
                    {
                        return new UpdateResult
                        {
                            Success = false,
                            Message = "Не удалось определить версию: " + tagName
                        };
                    }

                    Version currentVersion = GetCurrentVersion();
                    bool hasUpdate = remoteVersion > currentVersion;

                    string downloadUrl = null;
                    string assetName = null;
                    long assetSize = 0;
                    string expectedHash = null;
                    var assets = release["assets"] as JArray;
                    if (assets != null)
                    {
                        foreach (var asset in assets)
                        {
                            string name = asset["name"]?.ToString() ?? "";
                            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = asset["browser_download_url"]?.ToString();
                                assetName = name;
                                assetSize = asset["size"] != null ? asset["size"].Value<long>() : 0;
                                string digest = asset["digest"]?.ToString() ?? "";
                                if (digest.StartsWith("sha256:"))
                                    expectedHash = digest.Substring(7);
                                break;
                            }
                        }

                        if (downloadUrl == null)
                        {
                            foreach (var asset in assets)
                            {
                                string name = asset["name"]?.ToString() ?? "";
                                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    downloadUrl = asset["browser_download_url"]?.ToString();
                                    assetName = name;
                                    assetSize = asset["size"] != null ? asset["size"].Value<long>() : 0;
                                    string digest = asset["digest"]?.ToString() ?? "";
                                    if (digest.StartsWith("sha256:"))
                                        expectedHash = digest.Substring(7);
                                    break;
                                }
                            }
                        }
                    }

                    return new UpdateResult
                    {
                        Success = true,
                        HasUpdate = hasUpdate,
                        CurrentVersion = currentVersion.ToString(3),
                        RemoteVersion = remoteVersion.ToString(3),
                        ReleaseNotes = body,
                        PublishedAt = publishedAt,
                        DownloadUrl = downloadUrl,
                        AssetName = assetName,
                        AssetSize = assetSize,
                        ExpectedHash = expectedHash
                    };
            }
            catch (WebException ex)
            {
                return new UpdateResult
                {
                    Success = false,
                    Message = "Ошибка сети: " + ex.Message
                };
            }
            catch (Exception ex)
            {
                return new UpdateResult
                {
                    Success = false,
                    Message = "Ошибка: " + ex.Message
                };
            }
        }

        public string DownloadUpdate(string downloadUrl, Action<int> progressCallback)
        {
            string tempDir = GetTempDir();
            if (!Directory.Exists(tempDir))
                Directory.CreateDirectory(tempDir);
            string fileName = Path.GetFileName(new Uri(downloadUrl).AbsolutePath);
            string filePath = Path.Combine(tempDir, fileName);

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var request = (HttpWebRequest)WebRequest.Create(downloadUrl);
            request.UserAgent = "AVTOKarta-Updater";
            request.AllowAutoRedirect = true;
            request.MaximumAutomaticRedirections = 5;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var responseStream = response.GetResponseStream())
            using (var fileStream = File.Create(filePath))
            {
                byte[] buffer = new byte[8192];
                long totalBytesRead = 0;
                long totalBytes = response.ContentLength;
                int bytesRead;

                while ((bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fileStream.Write(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    if (progressCallback != null && totalBytes > 0)
                    {
                        int progress = (int)((totalBytesRead * 100) / totalBytes);
                        progressCallback(progress);
                    }
                }
            }

            return filePath;
        }

        public bool ValidateDownload(string filePath, long expectedSize, string expectedHash)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                FileInfo fi = new FileInfo(filePath);
                if (fi.Length == 0)
                    return false;

                if (expectedSize > 0 && fi.Length != expectedSize)
                    return false;

                if (!string.IsNullOrEmpty(expectedHash))
                {
                    using (var sha = System.Security.Cryptography.SHA256.Create())
                    using (var fs = File.OpenRead(filePath))
                    {
                        byte[] hash = sha.ComputeHash(fs);
                        string hashHex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                        if (!hashHex.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                }

                if (filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    using (var fs = File.OpenRead(filePath))
                    {
                        byte[] header = new byte[4];
                        if (fs.Read(header, 0, 4) == 4)
                        {
                            if (header[0] != 0x50 || header[1] != 0x4B)
                                return false;
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string EscapeBatPath(string path)
        {
            return path
                .Replace("%", "%%")
                .Replace("!", "^!")
                .Replace("&", "^&")
                .Replace("|", "^|")
                .Replace(">", "^>")
                .Replace("<", "^<")
                .Replace("^", "^^");
        }

        public void ApplyUpdate(string downloadedFilePath)
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string updaterBat = Path.Combine(GetTempDir(), "update.bat");
            string logPath = GetLogPath();
            string escapedLogPath = EscapeBatPath(logPath);

            bool isZip = downloadedFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            bool isExe = downloadedFilePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

            string batContent;
            if (isZip)
            {
                string extractDir = Path.Combine(GetTempDir(), "extracted");

                batContent =
                    "@echo off\r\n" +
                    "chcp 65001 >nul\r\n" +
                    "setlocal EnableDelayedExpansion\r\n" +
                    "timeout /t 3 /nobreak >nul\r\n" +
                    "taskkill /f /im AVTOKarta.exe >nul 2>&1\r\n" +
                    "timeout /t 1 /nobreak >nul\r\n" +

                    "if not exist \"" + EscapeBatPath(extractDir) + "\" mkdir \"" + EscapeBatPath(extractDir) + "\"\r\n" +
                    "powershell -NoProfile -ExecutionPolicy Bypass -Command \"try { Expand-Archive -Path '" + downloadedFilePath.Replace("'", "''") + "' -DestinationPath '" + extractDir.Replace("'", "''") + "' -Force } catch { exit 1 }\"\r\n" +
                    "if errorlevel 1 (\r\n" +
                    "  echo [UPDATE] Extract failed >> \"" + escapedLogPath + "\"\r\n" +
                    "  start \"\" \"" + EscapeBatPath(Path.Combine(appDir, "AVTOKarta.exe")) + "\"\r\n" +
                    "  del \"%~f0\"\r\n" +
                    "  exit /b\r\n" +
                    ")\r\n" +

                    "xcopy /s /y /e /q \"" + EscapeBatPath(extractDir) + "\\*.*\" \"" + EscapeBatPath(appDir) + "\"\r\n" +
                    "if errorlevel 1 (\r\n" +
                    "  echo [UPDATE] Copy failed >> \"" + escapedLogPath + "\"\r\n" +
                    "  start \"\" \"" + EscapeBatPath(Path.Combine(appDir, "AVTOKarta.exe")) + "\"\r\n" +
                    "  del \"%~f0\"\r\n" +
                    "  exit /b\r\n" +
                    ")\r\n" +

                    "rmdir /s /q \"" + EscapeBatPath(GetTempDir()) + "\" 2>nul\r\n" +
                    "start \"\" \"" + EscapeBatPath(Path.Combine(appDir, "AVTOKarta.exe")) + "\"\r\n" +
                    "del \"%~f0\"\r\n";
            }
            else if (isExe)
            {
                string installLog = Path.Combine(GetTempDir(), "install.log");

                batContent =
                    "@echo off\r\n" +
                    "chcp 65001 >nul\r\n" +
                    "setlocal EnableDelayedExpansion\r\n" +
                    "timeout /t 3 /nobreak >nul\r\n" +
                    "taskkill /f /im AVTOKarta.exe >nul 2>&1\r\n" +
                    "timeout /t 1 /nobreak >nul\r\n" +

                    "echo [UPDATE] Running installer >> \"" + escapedLogPath + "\"\r\n" +
                    "\"" + downloadedFilePath + "\" /SILENT /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LOG=\"" + EscapeBatPath(installLog) + "\"\r\n" +
                    "if errorlevel 1 (\r\n" +
                    "  echo [UPDATE] Installer failed >> \"" + escapedLogPath + "\"\r\n" +
                    "  start \"\" \"" + EscapeBatPath(Path.Combine(appDir, "AVTOKarta.exe")) + "\" 2>nul\r\n" +
                    "  del \"%~f0\"\r\n" +
                    "  exit /b\r\n" +
                    ")\r\n" +

                    "rmdir /s /q \"" + EscapeBatPath(GetTempDir()) + "\" 2>nul\r\n" +
                    "start \"\" \"" + EscapeBatPath(Path.Combine(appDir, "AVTOKarta.exe")) + "\"\r\n" +
                    "del \"%~f0\"\r\n";
            }
            else
            {
                Log("Unsupported update file type: " + downloadedFilePath);
                return;
            }

            File.WriteAllText(updaterBat, batContent, System.Text.Encoding.ASCII);

            Log("Launching update: " + downloadedFilePath);

            Process.Start(new ProcessStartInfo
            {
                FileName = updaterBat,
                UseShellExecute = true,
                CreateNoWindow = true
            });

            Environment.Exit(0);
        }

        public void AutoUpdate()
        {
            try
            {
                _updateMutex = new Mutex(true, "AVTOKarta_UpdateMutex", out bool createdNew);
                if (!createdNew)
                {
                    Log("Обновление уже запущено, пропуск");
                    return;
                }

                Log("Автопроверка обновлений...");
                UpdateResult result = CheckForUpdate();

                if (result == null || !result.Success)
                {
                    Log("Проверка обновлений: " + (result != null ? result.Message : "null"));
                    return;
                }

                if (!result.HasUpdate)
                {
                    Log("Обновлений нет (текущая: " + result.CurrentVersion + ")");
                    return;
                }

                if (string.IsNullOrEmpty(result.DownloadUrl))
                {
                    Log("Обновление доступно, но URL скачивания не найден");
                    return;
                }

                Log("Найдено обновление: " + result.RemoteVersion + ", скачивание...");

                string tempDir = GetTempDir();
                CleanupTemp(tempDir);

                string fileName = Path.GetFileName(new Uri(result.DownloadUrl).AbsolutePath);
                string filePath = Path.Combine(tempDir, fileName);

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var request = (HttpWebRequest)WebRequest.Create(result.DownloadUrl);
                request.UserAgent = "AVTOKarta-Updater";
                request.AllowAutoRedirect = true;
                request.MaximumAutomaticRedirections = 5;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var responseStream = response.GetResponseStream())
                using (var fileStream = File.Create(filePath))
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        fileStream.Write(buffer, 0, bytesRead);
                    }
                }

                Log("Скачано: " + filePath);

                if (!ValidateDownload(filePath, result.AssetSize, result.ExpectedHash))
                {
                    Log("Файл повреждён или пустой, отмена обновления");
                    CleanupTemp(tempDir);
                    return;
                }

                Log("Файл проверен, применение обновления...");
                ApplyUpdate(filePath);
            }
            catch (Exception ex)
            {
                Log("Ошибка автообновления: " + ex.Message);
            }
            finally
            {
                if (_updateMutex != null)
                {
                    _updateMutex.ReleaseMutex();
                    _updateMutex.Dispose();
                    _updateMutex = null;
                }
            }
        }

        private static string GetTempDir()
        {
            return Path.Combine(Path.GetTempPath(), "AVTOKarta_Update");
        }

        private void CleanupTemp(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch
            {
            }
        }
    }

    public class UpdateResult
    {
        public bool Success { get; set; }
        public bool HasUpdate { get; set; }
        public string CurrentVersion { get; set; }
        public string RemoteVersion { get; set; }
        public string ReleaseNotes { get; set; }
        public string PublishedAt { get; set; }
        public string DownloadUrl { get; set; }
        public string AssetName { get; set; }
        public long AssetSize { get; set; }
        public string ExpectedHash { get; set; }
        public string Message { get; set; }
    }
}
