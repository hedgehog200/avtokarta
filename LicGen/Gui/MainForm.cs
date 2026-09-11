using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using AVTOKarta.Licensing;

namespace LicGen.Gui
{
    // Главное окно LicGen: база лицензий и база кодов ПК, на которых стоит программа.
    internal sealed class MainForm : Form
    {
        private readonly DataGridView _gridLicenses = new DataGridView();
        private readonly DataGridView _gridMachines = new DataGridView();
        private readonly Label _lblKey = new Label();
        private readonly Label _lblLicensesDir = new Label();
        private readonly Button _btnIssue = new Button { Text = "Выпустить лицензию", Width = 160 };
        private readonly Button _btnCreateKeys = new Button { Text = "Создать ключи RSA", Width = 130 };
        private readonly Button _btnOpenFolder = new Button { Text = "Открыть папку лицензий", Width = 160 };
        private readonly Button _btnAddMachine = new Button { Text = "Добавить код ПК", Width = 130 };
        private readonly Button _btnRefresh = new Button { Text = "Обновить", Width = 90 };
        private readonly StatusStrip _status = new StatusStrip();
        private readonly ToolStripStatusLabel _statusLabel = new ToolStripStatusLabel();

        private string _publicKeyXml;

        public MainForm()
        {
            Text = "LicGen — лицензирование АВТОКарта";
            Width = 1040;
            Height = 660;
            MinimumSize = new Size(860, 500);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9);
            BuildUi();
        }

        private void BuildUi()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 86, Padding = new Padding(12, 8, 12, 0) };
            _lblKey.AutoSize = true;
            _lblKey.Location = new Point(12, 10);
            _lblLicensesDir.AutoSize = true;
            _lblLicensesDir.Location = new Point(12, 32);

            int btnY = 52;
            _btnIssue.Location = new Point(12, btnY);
            _btnCreateKeys.Location = new Point(182, btnY);
            _btnOpenFolder.Location = new Point(322, btnY);
            _btnAddMachine.Location = new Point(492, btnY);
            _btnRefresh.Location = new Point(632, btnY);

            _btnIssue.Click += delegate { IssueLicense(); };
            _btnCreateKeys.Click += delegate { CreateKeys(); };
            _btnOpenFolder.Click += delegate { OpenFolder(Paths.DefaultLicenseDir); };
            _btnAddMachine.Click += delegate { AddMachine(); };
            _btnRefresh.Click += delegate { RefreshAll(); };

            top.Controls.AddRange(new Control[] { _lblKey, _lblLicensesDir, _btnIssue, _btnCreateKeys, _btnOpenFolder, _btnAddMachine, _btnRefresh });

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildLicensesPage());
            tabs.TabPages.Add(BuildMachinesPage());

            _status.Items.Add(_statusLabel);
            _status.SizingGrip = false;

            Controls.Add(tabs);
            Controls.Add(top);
            Controls.Add(_status);
        }

        private TabPage BuildLicensesPage()
        {
            var page = new TabPage("Лицензии") { Padding = new Padding(8) };
            _gridLicenses.Dock = DockStyle.Fill;
            _gridLicenses.ReadOnly = true;
            _gridLicenses.AllowUserToAddRows = false;
            _gridLicenses.AllowUserToDeleteRows = false;
            _gridLicenses.RowHeadersVisible = false;
            _gridLicenses.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridLicenses.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            AddGridColumn(_gridLicenses, "colId", "Лицензия");
            AddGridColumn(_gridLicenses, "colLicensee", "Владелец");
            AddGridColumn(_gridLicenses, "colMachine", "Код ПК");
            AddGridColumn(_gridLicenses, "colIssued", "Выдана", 84);
            AddGridColumn(_gridLicenses, "colExpires", "Действует до", 96);
            AddGridColumn(_gridLicenses, "colFeatures", "Возможности");
            AddGridColumn(_gridLicenses, "colStatus", "Статус", 120);
            AddGridColumn(_gridLicenses, "colFile", "Файл");
            DataGridViewTextBoxColumn pathCol = AddGridColumn(_gridLicenses, "colPath", "Путь");
            pathCol.Visible = false;
            _gridLicenses.Click += delegate
            {
                if (_gridLicenses.SelectedRows.Count > 0 && _gridLicenses.SelectedCells.Count > 0 &&
                    _gridLicenses.SelectedCells[0].RowIndex >= 0 &&
                    _gridLicenses.SelectedCells[0].ColumnIndex != _gridLicenses.Columns["colPath"].Index)
                {
                    string path = GetSelectedLicensePath();
                    if (path != null) OpenFolder(Path.GetDirectoryName(path));
                }
            };
            page.Controls.Add(_gridLicenses);
            return page;
        }

        private TabPage BuildMachinesPage()
        {
            var page = new TabPage("Коды ПК (установки)") { Padding = new Padding(8) };
            var note = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = "Коды ПК, на которых используется АВТОКарта: машины из выданных лицензий и коды, внесённые вручную (файл licenses\\machines.txt).",
                ForeColor = Color.DimGray
            };
            _gridMachines.Dock = DockStyle.Fill;
            _gridMachines.ReadOnly = true;
            _gridMachines.AllowUserToAddRows = false;
            _gridMachines.AllowUserToDeleteRows = false;
            _gridMachines.RowHeadersVisible = false;
            _gridMachines.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridMachines.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            AddGridColumn(_gridMachines, "colCode", "Код ПК");
            AddGridColumn(_gridMachines, "colNote", "Владелец / комментарий");
            AddGridColumn(_gridMachines, "colCount", "Лицензий", 70);
            AddGridColumn(_gridMachines, "colState", "Состояние", 110);
            AddGridColumn(_gridMachines, "colSource", "Источник", 90);
            page.Controls.Add(_gridMachines);
            page.Controls.Add(note);
            return page;
        }

        private static DataGridViewTextBoxColumn AddGridColumn(DataGridView grid, string name, string header, int width = 0)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header
            };
            if (width > 0)
                col.MinimumWidth = width;
            grid.Columns.Add(col);
            return col;
        }

        private void RefreshAll()
        {
            Directory.CreateDirectory(Paths.DefaultLicenseDir);

            bool haveKeys = File.Exists(Paths.PrivateKeyFile) && File.Exists(Paths.PublicKeyFile);
            _btnIssue.Enabled = haveKeys;
            _btnCreateKeys.Text = haveKeys ? "Ключи созданы" : "Создать ключи RSA";

            if (haveKeys)
            {
                _publicKeyXml = File.ReadAllText(Paths.PublicKeyFile, Encoding.UTF8);
                _lblKey.Text = "Ключи: " + Paths.DefaultKeyDir;
                _lblKey.ForeColor = Color.ForestGreen;
            }
            else
            {
                _publicKeyXml = null;
                _lblKey.Text = "Ключи не созданы — нажмите «Создать ключи RSA» (" + Paths.DefaultKeyDir + ")";
                _lblKey.ForeColor = Color.IndianRed;
            }
            _lblLicensesDir.Text = "Каталог лицензий: " + Paths.DefaultLicenseDir;

            List<LicenseRecord> licenses = LicenseStore.LoadAll(Paths.DefaultLicenseDir, _publicKeyXml);

            _gridLicenses.Rows.Clear();
            int validCount = 0;
            foreach (LicenseRecord rec in licenses)
            {
                if (rec.Valid) validCount++;
                string status = StatusText(rec);
                _gridLicenses.Rows.Add(
                    rec.Valid && rec.Info != null ? rec.Info.LicenseId : "—",
                    rec.Valid && rec.Info != null ? rec.Info.Licensee : string.Empty,
                    rec.Valid && rec.Info != null ? (string.IsNullOrEmpty(rec.Info.MachineCode) ? "(любой ПК)" : rec.Info.MachineCode) : string.Empty,
                    rec.Valid && rec.Info != null ? rec.Info.IssuedOn : string.Empty,
                    rec.Valid && rec.Info != null ? (rec.Info.IsPerpetual ? "бессрочно" : rec.Info.ExpiresOn) : string.Empty,
                    rec.Valid && rec.Info != null ? LicenseFeaturesHelper.DisplayList(rec.Info.FeatureFlags) : "—",
                    status,
                    Path.GetFileName(rec.FilePath),
                    rec.FilePath);
            }

            _gridMachines.Rows.Clear();
            List<MachineRecord> machines = LicenseStore.AllMachines(licenses);
            foreach (MachineRecord mr in machines)
            {
                _gridMachines.Rows.Add(
                    mr.MachineCode,
                    mr.Note,
                    mr.LicenseCount > 0 ? mr.LicenseCount.ToString(CultureInfo.InvariantCulture) : "—",
                    mr.HasActiveLicense ? "активна" : (mr.FromLicenses ? "истекла / не выдана" : "внесён вручную"),
                    mr.FromLicenses ? "из лицензий" : "вручную");
            }

            _statusLabel.Text = string.Format(CultureInfo.InvariantCulture,
                "Лицензий: {0} (действит.: {1}) | Кодов ПК: {2}",
                licenses.Count, validCount, machines.Count);
        }

        private static string StatusText(LicenseRecord rec)
        {
            if (!rec.Valid)
                return "недействительна: " + (rec.Error ?? "ошибка");
            if (rec.Info == null)
                return "недействительна";
            if (rec.Expired)
                return "ИСТЕКЛА";
            return "действует";
        }

        private string GetSelectedLicensePath()
        {
            if (_gridLicenses.SelectedRows.Count == 0)
                return null;
            DataGridViewRow row = _gridLicenses.SelectedRows[0];
            if (row.Cells[_gridLicenses.Columns["colPath"].Index].Value == null)
                return null;
            return row.Cells[_gridLicenses.Columns["colPath"].Index].Value as string;
        }

        private void IssueLicense()
        {
            string privateKeyXml;
            if (!KeyStore.TryLoadPrivateKey(Paths.PrivateKeyFile, out privateKeyXml))
            {
                MessageBox.Show(this, "Не удалось загрузить приватный ключ.\nСначала создайте ключи: «Создать ключи RSA».",
                    "LicGen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dlg = new IssueLicenseDialog())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    string id = "LIC-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
                    var info = new LicenseInfo
                    {
                        LicenseId = id,
                        MachineCode = dlg.MachineCode,
                        Licensee = dlg.Licensee,
                        IssuedOn = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        ExpiresOn = dlg.Days > 0
                            ? DateTime.UtcNow.AddDays(dlg.Days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                            : string.Empty,
                        StationLimit = 1,
                        Features = (long)dlg.Features
                    };

                    using (RSACryptoServiceProvider rsa = LicenseCrypto.ImportPrivateKeyXml(privateKeyXml))
                    {
                        rsa.PersistKeyInCsp = false;
                        string content = Signer.CreateLicenseFile(rsa, info);
                        LicenseStore.Save(info, content, Paths.DefaultLicenseDir, _publicKeyXml);
                    }

                    string target = string.IsNullOrEmpty(info.MachineCode) ? "(любой ПК)" : info.MachineCode;
                    MessageBox.Show(this,
                        "Лицензия выпущена:\n  " + info.LicenseId + "\n  Машина: " + target + "\n  Владелец: " +
                        (string.IsNullOrEmpty(info.Licensee) ? "(не указан)" : info.Licensee) + "\n  Файл: " +
                        Paths.DefaultLicenseDir,
                        "LicGen", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshAll();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Ошибка выпуска лицензии: " + ex.Message,
                        "LicGen", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void CreateKeys()
        {
            if (File.Exists(Paths.PrivateKeyFile))
            {
                MessageBox.Show(this, "Ключи уже существуют в " + Paths.DefaultKeyDir,
                    "LicGen", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshAll();
                return;
            }

            DialogResult confirm = MessageBox.Show(this,
                "Будет создана пара ключей RSA-2048 в каталоге:\n" + Paths.DefaultKeyDir +
                "\n\nОткрытый ключ надо указать в LicenseService.cs приложения.\nПродолжить?",
                "Создание ключей", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                using (RSACryptoServiceProvider rsa = LicenseCrypto.CreateKey(2048))
                {
                    rsa.PersistKeyInCsp = false;
                    Directory.CreateDirectory(Paths.DefaultKeyDir);
                    KeyStore.SavePrivateKey(Paths.PrivateKeyFile, LicenseCrypto.ExportPrivateKeyXml(rsa));
                    File.WriteAllText(Paths.PublicKeyFile, LicenseCrypto.ExportPublicKeyXml(rsa), Encoding.UTF8);
                    _publicKeyXml = File.ReadAllText(Paths.PublicKeyFile, Encoding.UTF8);
                }
                MessageBox.Show(this,
                    "Ключи созданы: " + Paths.DefaultKeyDir +
                    "\n\nОткрытый ключ скопирован в открытый XML-файл:\n" + Paths.PublicKeyFile +
                    "\n\nВставьте его значение в LicenseService.cs приложения (BuildPublisherPublicKeyXml).",
                    "LicGen", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Ошибка создания ключей: " + ex.Message,
                    "LicGen", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddMachine()
        {
            using (var dlg = new MachineInputDialog())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                if (dlg.MachineCode.Length == 0)
                {
                    MessageBox.Show(this, "Укажите код ПК.", "LicGen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                LicenseStore.AppendManualMachine(dlg.MachineCode, dlg.Note);
                RefreshAll();
            }
        }

        private static void OpenFolder(string dir)
        {
            if (string.IsNullOrEmpty(dir))
                return;
            if (Directory.Exists(dir))
            {
                try { System.Diagnostics.Process.Start("explorer.exe", "\"" + Path.GetFullPath(dir) + "\""); }
                catch (Exception) { }
            }
        }
    }

    // Мини-диалог ручного добавления кода ПК в базу установок.
    internal sealed class MachineInputDialog : Form
    {
        private readonly TextBox _txtCode = new TextBox();
        private readonly TextBox _txtNote = new TextBox();

        internal string MachineCode { get { return _txtCode.Text.Trim(); } }
        internal string Note { get { return _txtNote.Text.Trim(); } }

        internal MachineInputDialog()
        {
            Text = "Добавить код ПК";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 120);
            Font = new Font("Segoe UI", 9);

            Label lblCode = new Label { Text = "Код ПК:", Location = new Point(12, 12), AutoSize = true };
            _txtCode.Location = new Point(120, 10);
            _txtCode.Width = 260;
            Label lblNote = new Label { Text = "Комментарий:", Location = new Point(12, 42), AutoSize = true };
            _txtNote.Location = new Point(120, 40);
            _txtNote.Width = 260;

            Button btnOk = new Button { Text = "Добавить", DialogResult = DialogResult.OK, Width = 100, Location = new Point(120, 76) };
            Button btnCancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Width = 90, Location = new Point(230, 76) };
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[] { lblCode, _txtCode, lblNote, _txtNote, btnOk, btnCancel });
        }
    }
}