using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenDimmer;

public class CustomPercentDialog : Form
{
    private readonly NumericUpDown _numericInput;
    public int SelectedPercentage => (int)_numericInput.Value;

    public CustomPercentDialog(int currentPercent)
    {
        Text = "Уровень затемнения";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(280, 130);
        Font = new Font("Segoe UI", 9.5f);

        var lblPrompt = new Label
        {
            Text = "Введите желаемый процент затемнения (0 - 100%):",
            Location = new Point(16, 14),
            Size = new Size(250, 36)
        };

        _numericInput = new NumericUpDown
        {
            Location = new Point(16, 54),
            Size = new Size(120, 26),
            Minimum = 0,
            Maximum = 100,
            Value = Math.Clamp(currentPercent, 0, 100)
        };

        var btnOk = new Button
        {
            Text = "ОК",
            DialogResult = DialogResult.OK,
            Location = new Point(106, 90),
            Size = new Size(75, 28)
        };

        var btnCancel = new Button
        {
            Text = "Отмена",
            DialogResult = DialogResult.Cancel,
            Location = new Point(188, 90),
            Size = new Size(75, 28)
        };

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.Add(lblPrompt);
        Controls.Add(_numericInput);
        Controls.Add(btnOk);
        Controls.Add(btnCancel);
    }
}
