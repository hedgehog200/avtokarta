// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Globalization;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows;

namespace AVTOKarta
{
    public partial class App : Application
    {
        private static void Log(string msg)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData), "AVTOKarta");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    var di = new DirectoryInfo(dir);
                    var acl = di.GetAccessControl();
                    acl.SetAccessRuleProtection(true, false);
                    var rule = new FileSystemAccessRule(
                        WindowsIdentity.GetCurrent().Name,
                        FileSystemRights.FullControl,
                        AccessControlType.Allow);
                    acl.AddAccessRule(rule);
                    di.SetAccessControl(acl);
                }
                File.AppendAllText(Path.Combine(dir, "crash.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + msg + Environment.NewLine);
            }
            catch { }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Log("=== OnStartup START ===");

            DispatcherUnhandledException += (s, args) =>
            {
                Log("DispatcherUnhandledException: " + args.Exception);
                MessageBox.Show("Произошла непредвиденная ошибка. Обратитесь к администратору.\n\nДетали: " + args.Exception.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Log("UnhandledException: " + args.ExceptionObject);
            };

            var culture = new CultureInfo("ru-RU");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            try
            {
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                Log("StartupException: " + ex);
                MessageBox.Show("Ошибка запуска приложения. Обратитесь к администратору.\n\nДетали: " + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Log("=== OnStartup END ===");
        }
    }
}
