using Natural;

namespace Natural_Playground;

/// <summary>
/// Two operands, one operation, and an account of what the result looks like inside.
/// </summary>
internal sealed class FloatPanel : UserControl
{
    private const string DefaultA = "0.1";
    private const string DefaultB = "0.2";

    /// <summary>Well under <see cref="ApFloat.MaxPrecision"/>; above this a keystroke gets slow.</summary>
    private const int PracticalPrecisionCeiling = 65_536;

    private readonly TextBox _a = OperandBox(DefaultA);
    private readonly TextBox _b = OperandBox(DefaultB);
    private readonly NumericUpDown _precision = new()
    {
        Minimum = 1,
        Maximum = PracticalPrecisionCeiling,
        Value = 53,                  // 53 bits makes the double comparison below meaningful
        Width = 84,
        ThousandsSeparator = false,
        TextAlign = HorizontalAlignment.Right,
    };
    private readonly ComboBox _mode = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 132,
    };
    private readonly TextBox _result = ReadOnlyBox(10F);
    private readonly TextBox _detail = ReadOnlyBox(9F);

    private string _operation = FloatReport.Add;

    public FloatPanel()
    {
        Dock = DockStyle.Fill;

        _mode.DataSource = Enum.GetValues<RoundingMode>().ToList();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // operands
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // operation, precision, mode
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); // result
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // everything else

        root.Controls.Add(OperandRow(), 0, 0);
        root.Controls.Add(Controls_(), 0, 1);
        root.Controls.Add(_result, 0, 2);
        root.Controls.Add(_detail, 0, 3);
        Controls.Add(root);

        _a.TextChanged += (_, _) => Report();
        _b.TextChanged += (_, _) => Report();
        _precision.ValueChanged += (_, _) => Report();
        _mode.SelectedIndexChanged += (_, _) => Report();
        Report();
    }

    private Control OperandRow()
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.Controls.Add(new Label { Text = "a", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 9, 3, 3) }, 0, 0);
        table.Controls.Add(_a, 1, 0);
        table.Controls.Add(new Label { Text = "b", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(9, 9, 3, 3) }, 2, 0);
        table.Controls.Add(_b, 3, 0);
        return table;
    }

    private Control Controls_()
    {
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = Padding.Empty };

        flow.Controls.Add(OperationButton(FloatReport.Add, "+"));
        flow.Controls.Add(OperationButton(FloatReport.Subtract, "−"));
        flow.Controls.Add(OperationButton(FloatReport.Multiply, "×"));
        flow.Controls.Add(OperationButton(FloatReport.Divide, "÷"));
        flow.Controls.Add(Spacer());
        flow.Controls.Add(new Label { Text = "precision", AutoSize = true, Margin = new Padding(0, 7, 4, 0) });
        flow.Controls.Add(_precision);
        flow.Controls.Add(new Label { Text = "mode", AutoSize = true, Margin = new Padding(12, 7, 4, 0) });
        flow.Controls.Add(_mode);
        return flow;
    }

    private Button OperationButton(string op, string caption)
    {
        var button = new Button
        {
            Text = caption,
            Width = 44,
            Height = 30,
            Margin = new Padding(0, 0, 4, 0),
            Tag = op,
        };
        button.Click += (_, _) => { _operation = op; Report(); };
        return button;
    }

    private static Label Spacer() => new() { Width = 10, AutoSize = false, Height = 1, Margin = Padding.Empty };

    private void Report()
    {
        string text = FloatReport.Describe(_a.Text, _b.Text, _operation, (int)_precision.Value, (RoundingMode)_mode.SelectedItem!);

        // The first line is "a op b = r"; the rest is detail. Splitting keeps the
        // result box to the one line that answers the question.
        int newline = text.IndexOf('\n');
        _result.Text = newline < 0 ? text : text[..newline];
        _detail.Text = newline < 0 ? "" : text[(newline + 1)..];
    }

    private static TextBox OperandBox(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        Margin = new Padding(3, 6, 3, 3),
        Font = new Font("Consolas", 10F),
    };

    private static TextBox ReadOnlyBox(float size) => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        Multiline = true,
        WordWrap = false,
        ScrollBars = ScrollBars.Both,
        BackColor = SystemColors.Window,
        Font = new Font("Consolas", size),
        Margin = new Padding(3),
    };
}