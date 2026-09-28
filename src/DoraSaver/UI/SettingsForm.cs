using DoraSaver.Core;

namespace DoraSaver.UI;

/// <summary>The dialog behind the Settings button (/c), in the saver's own language.</summary>
internal sealed class SettingsForm : Form
{
    private static readonly LayoutMode[] LayoutOrder = [LayoutMode.Fit, LayoutMode.Extend, LayoutMode.Fill, LayoutMode.Stretch];

    private readonly CheckBox _playSound;
    private readonly List<(LayoutMode Mode, RadioButton Button)> _layoutButtons = [];
    private readonly List<Label> _wrappingLabels = [];
    private readonly GroupBox _layoutGroup;

    public SettingsForm(string title, SaverInfo saver, SaverSettings current, string? warning, UiText text)
    {
        Text = string.Format(text.TitleFormat, title);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font(text.FontName, 9F);
        Padding = new Padding(12);

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };

        _playSound = new CheckBox { Text = text.PlaySound, Checked = current.PlaySound, AutoSize = true, Margin = new Padding(3, 3, 3, 9) };
        root.Controls.Add(_playSound);
        _layoutGroup = BuildLayoutGroup(saver, current, text);
        root.Controls.Add(_layoutGroup);

        if (!string.IsNullOrEmpty(warning))
        {
            root.Controls.Add(WrappingLabel(warning!, Color.Firebrick, new Padding(3, 9, 3, 3)));
        }

        root.Controls.Add(WrappingLabel(text.Footer, SystemColors.GrayText, new Padding(3, 9, 3, 9)));

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        var cancel = new Button { Text = text.Cancel, DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(80, 0) };
        var ok = new Button { Text = text.Ok, DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(80, 0) };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        root.Controls.Add(buttons);

        Controls.Add(root);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public SaverSettings Result
    {
        get
        {
            LayoutMode layout = _layoutButtons.FirstOrDefault(b => b.Button.Checked).Mode;
            return new SaverSettings(_playSound.Checked, layout);
        }
    }

    /// <summary>Long notes wrap at the width of the options box instead of widening the dialog.</summary>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        foreach (Label label in _wrappingLabels)
        {
            label.MaximumSize = new Size(_layoutGroup.Width, 0);
        }
    }

    private Label WrappingLabel(string text, Color color, Padding margin)
    {
        var label = new Label { Text = text, ForeColor = color, AutoSize = true, MaximumSize = new Size(1, 0), Margin = margin };
        _wrappingLabels.Add(label);
        return label;
    }

    private GroupBox BuildLayoutGroup(SaverInfo saver, SaverSettings current, UiText text)
    {
        var group = new GroupBox { Text = text.LayoutGroup, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, Padding = new Padding(9) };
        var list = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, WrapContents = false };
        foreach (LayoutMode mode in LayoutOrder)
        {
            string label = text.LayoutLabel(mode);
            string caption = mode == saver.DefaultLayout ? $"{label}　{text.Recommended}" : label;
            var button = new RadioButton { Text = caption, AutoSize = true, Checked = mode == current.Layout };
            _layoutButtons.Add((mode, button));
            list.Controls.Add(button);
        }

        group.Controls.Add(list);
        return group;
    }
}
