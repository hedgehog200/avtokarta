using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using AVTOKarta.Helpers;
using AVTOKarta.Services;

namespace AVTOKarta.ViewModels
{
    public class UpdateViewModel : BaseViewModel
    {
        private readonly UpdateService _updateService = new UpdateService();

        private string _currentVersion;
        private string _remoteVersion;
        private string _statusText;
        private string _releaseNotes;
        private string _publishedAt;
        private bool _hasUpdate;
        private bool _isChecking;
        private bool _isDownloading;
        private int _downloadProgress;
        private string _downloadProgressText;

        public string CurrentVersion
        {
            get => _currentVersion;
            set { _currentVersion = value; OnPropertyChanged(); }
        }

        public string RemoteVersion
        {
            get => _remoteVersion;
            set { _remoteVersion = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public string ReleaseNotes
        {
            get => _releaseNotes;
            set { _releaseNotes = value; OnPropertyChanged(); }
        }

        public string PublishedAt
        {
            get => _publishedAt;
            set { _publishedAt = value; OnPropertyChanged(); }
        }

        public bool HasUpdate
        {
            get => _hasUpdate;
            set { _hasUpdate = value; OnPropertyChanged(); }
        }

        public bool IsChecking
        {
            get => _isChecking;
            set { _isChecking = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanCheck)); }
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set { _isDownloading = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanCheck)); }
        }

        public bool CanCheck => !IsChecking && !IsDownloading;

        public int DownloadProgress
        {
            get => _downloadProgress;
            set { _downloadProgress = value; OnPropertyChanged(); }
        }

        public string DownloadProgressText
        {
            get => _downloadProgressText;
            set { _downloadProgressText = value; OnPropertyChanged(); }
        }

        public ICommand CheckUpdateCommand { get; }
        public ICommand DownloadUpdateCommand { get; }

        public UpdateViewModel()
        {
            CurrentVersion = UpdateService.GetCurrentVersion().ToString(3);
            StatusText = "Нажмите «Проверить обновления»";

            CheckUpdateCommand = new RelayCommand(_ => CheckForUpdate());
            DownloadUpdateCommand = new RelayCommand(_ => DownloadAndInstall());
        }

        private void CheckForUpdate()
        {
            IsChecking = true;
            StatusText = "Проверка...";
            HasUpdate = false;
            ReleaseNotes = null;
            RemoteVersion = null;
            PublishedAt = null;

            BackgroundWorker worker = new BackgroundWorker();
            UpdateResult result = null;

            worker.DoWork += (s, e) =>
            {
                result = _updateService.CheckForUpdate();
            };

            worker.RunWorkerCompleted += (s, e) =>
            {
                IsChecking = false;

                if (result == null)
                {
                    StatusText = "Неизвестная ошибка";
                    return;
                }

                if (!result.Success)
                {
                    StatusText = result.Message;
                    return;
                }

                RemoteVersion = result.RemoteVersion;
                HasUpdate = result.HasUpdate;

                if (!string.IsNullOrEmpty(result.PublishedAt))
                {
                    DateTime dt;
                    if (DateTime.TryParse(result.PublishedAt, out dt))
                        PublishedAt = dt.ToString("dd.MM.yyyy HH:mm");
                }

                if (result.HasUpdate)
                {
                    StatusText = "Доступно обновление: " + result.RemoteVersion;
                    ReleaseNotes = result.ReleaseNotes;
                }
                else
                {
                    StatusText = "Установлена последняя версия (" + result.CurrentVersion + ")";
                }
            };

            worker.RunWorkerAsync();
        }

        private void DownloadAndInstall()
        {
            IsDownloading = true;
            StatusText = "Скачивание обновления...";
            DownloadProgress = 0;
            DownloadProgressText = null;

            BackgroundWorker dlWorker = new BackgroundWorker();
            dlWorker.WorkerReportsProgress = true;
            UpdateResult checkResult = null;
            string downloadedPath = null;
            bool downloadOk = false;

            dlWorker.DoWork += (s, e) =>
            {
                try
                {
                    UpdateService.Log("Ручная проверка обновления...");
                    checkResult = _updateService.CheckForUpdate();

                    if (checkResult == null || !checkResult.Success || !checkResult.HasUpdate)
                        return;

                    if (string.IsNullOrEmpty(checkResult.DownloadUrl))
                        return;

                    downloadedPath = _updateService.DownloadUpdate(checkResult.DownloadUrl, null);

                    if (!_updateService.ValidateDownload(downloadedPath, checkResult.AssetSize))
                    {
                        UpdateService.Log("Файл повреждён после скачивания");
                        downloadedPath = null;
                        return;
                    }

                    downloadOk = true;
                }
                catch (Exception ex)
                {
                    UpdateService.Log("Ошибка скачивания: " + ex.Message);
                }
            };

            dlWorker.ProgressChanged += (s, e) =>
            {
                DownloadProgress = e.ProgressPercentage;
                DownloadProgressText = e.ProgressPercentage + "%";
            };

            dlWorker.RunWorkerCompleted += (s, e) =>
            {
                IsDownloading = false;
                DownloadProgressText = null;

                if (!downloadOk)
                {
                    if (checkResult == null || !checkResult.Success)
                        StatusText = "Обновлений нет";
                    else if (checkResult != null && !checkResult.HasUpdate)
                        StatusText = "Установлена последняя версия";
                    else
                        StatusText = "Ошибка скачивания. Проверьте журнал updater.log";
                    return;
                }

                StatusText = "Установка обновления...";
                UpdateService.Log("Применение обновления...");

                try
                {
                    _updateService.ApplyUpdate(downloadedPath);
                }
                catch (Exception ex)
                {
                    UpdateService.Log("Ошибка установки: " + ex.Message);
                    StatusText = "Ошибка установки. Проверьте журнал updater.log";
                }
            };

            dlWorker.RunWorkerAsync();
        }
    }
}
