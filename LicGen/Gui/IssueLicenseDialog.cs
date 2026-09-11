using System;
using System.Drawing;
using System.Windows.Forms;
using AVTOKarta.Licensing;

namespace LicGen.Gui
{
    // Диалог выпуска лицензии: код ПК, владелец, срок и возможности.
    internal sealed class IssueLicenseDialog : Form
    {
        private readonly TextBox _txtMachine = new TextBox();
        private readonly TextBox _txtLicensee = new TextBox();
        private readonly NumericUpDown _numDays = new NumericUpDown();
        private readonly CheckBox _chkCore = new CheckBox();
        private readonly CheckBox _chkWarehouse = new CheckBox();
        private readonly CheckBox _chkExport = new CheckBox();
        private readonly CheckBox _chkMulti = new CheckBox();

        internal string MachineCode { get { return _txtMachine.Text.Trim(); } }
        internal string Licensee { get { return _txtLicensee.Text.Trim(); } }
        internal int Days { get { return (int)_numDays.Value; } }
        internal LicenseFeatures Features
        {
            get
            {
                LicenseFeatures features = LicenseFeatures.Core;
                if (_chkWarehouse.Checked) features |= LicenseFeatures.Warehouse;
                if (_chkExport.Checked) features |= LicenseFeatures.ExportExcel;
                if (_chkMulti.Checked) features |= LicenseFeatures.MultiSquad;
                return features;
            }
        }
        internal bool IsForThisMachine { get; private set; }

        internal IssueLicenseDialog()
        {
            Text = "Выпуск лицензии";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(460, 330);
            Font = new Font("Segoe UI", 9);

            int y = 18;
            Label lblMachine = new Label { Text = "Код ПК (пусто = любой ПК):", Location = new Point(18, y), AutoSize = true };
            _txtMachine.Location = new Point(18, y + 20);
            _txtMachine.Width = 300;

            Button btnThis = new Button { Text = "Этот ПК", Width = 80, Location = new Point(330, y + 18) };
            btnThis.Click += delegate { _txtMachine.Text = AVTOKarta.Licensing.MachineCode.Get(); IsForThisMachine = true; };
            Button btnAny = new Button { Text = "Любой", Width = 60, Location = new Point(414, y + 18) };
            btnAny.Click += delegate { _txtMachine.Text = string.Empty; };
            y += 54;

            Label lblLicensee = new Label { Text = "Организация / владелец:", Location = new Point(18, y), AutoSize = true };
            _txtLicensee.Location = new Point(18, y + 20);
            _txtLicensee.Width = 420;
            y += 54;

            Label lblDays = new Label { Text = "Срок действия (дней, 0 = бессрочно):", Location = new Point(18, y), AutoSize = true };
            _numDays.Location = new Point(230, y);
            _numDays.Width = 80;
            _numDays.Maximum = 36500;
            y += 44;

            Label lblFeatures = new Label { Text = "Возможности:", Location = new Point(18, y), AutoSize = true };
            y += 26;
            _chkCore.Text = "Core (базовая) — всегда";
            _chkCore.Checked = true;
            _chkCore.Enabled = false;
            _chkCore.Location = new Point(36, y);
            _chkWarehouse.Text = "Склад ГСМ";
            _chkWarehouse.Location = new Point(180, y);
            _chkExport.Text = "Экспорт в Excel";
            _chkExport.Location = new Point(300, y);
            y += 30;
            _chkMulti.Text = "Несколько подразделений";
            _chkMulti.Location = new Point(36, y);
            y += 36;

            Button btnOk = new Button { Text = "Выпустить лицензию", DialogResult = DialogResult.OK, Width = 170, Location = new Point(120, y) };
            Button btnCancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Width = 90, Location = new Point(310, y) };
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[]
            {
                lblMachine, _txtMachine, btnThis, btnAny,
                lblLicensee, _txtLicensee,
                lblDays, _numDays,
                lblFeatures, _chkCore, _chkWarehouse, _chkExport, _chkMulti,
                btnOk, btnCancel
            });
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _txtMachine.Focus();
        }
    }
}