using System.Globalization;
using DoraSaver.Core;
using DoraSaver.Setup.Engine;

namespace DoraSaver.Setup;

internal sealed class SetupForm : Form, IReporter
{
    private static readonly (UiLanguage Language, string Label)[] Languages =
        [(UiLanguage.Chinese, "中文"), (UiLanguage.Japanese, "日本語"), (UiLanguage.English, "English")];

    private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Right };
    private readonly Label _intro = new() { AutoSize = true, Margin = new Padding(3, 6, 3, 9) };
    private readonly GroupBox _saversBox = new() { Dock = DockStyle.Fill, Padding = new Padding(9) };
    private readonly CheckedListBox _savers = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, BorderStyle = BorderStyle.None, FormattingEnabled = true };
    private readonly GroupBox _namesBox = new() { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(9) };
    private readonly Dictionary<UiLanguage, CheckBox> _nameChecks = [];
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill };
    private readonly TextBox _log = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = SystemColors.Window };
    private readonly Button _install = new() { AutoSize = true };
    private readonly Button _uninstall = new() { AutoSize = true };
    private readonly Button _openSettings = new() { AutoSize = true };
    private readonly Button _close = new() { AutoSize = true };
    private SetupText _text;
    private CancellationTokenSource? _cancel;
    private bool _busy;
    private bool _committing;

    public SetupForm(UiLanguage language)
    {
        _text = SetupText.For(language);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        Controls.Add(BuildLayout());
        _savers.Format += FormatSaver;
        foreach (SaverInfo saver in SaverCatalog.All.OrderBy(s => s.Year))
        {
            _savers.Items.Add(saver, isChecked: true);
        }

        _language.Items.AddRange(Languages.Select(l => (object)l.Label).ToArray());
        _language.SelectedIndex = Array.FindIndex(Languages, l => l.Language == language);
        _language.SelectedIndexChanged += (_, _) => ApplyText(SetupText.For(Languages[_language.SelectedIndex].Language));
        _nameChecks[language].Checked = true;

        _install.Click += async (_, _) => await RunInstallAsync();
        _uninstall.Click += (_, _) => RunUninstall();
        _openSettings.Click += (_, _) => SystemIntegration.OpenScreenSaverSettings();
        _close.Click += (_, _) => OnCloseOrCancel();
        FormClosing += (_, e) =>
        {
            if (_busy)
            {
                e.Cancel = true;
                RequestCancel();
            }
        };
        ApplyText(_text);
        _uninstall.Enabled = SystemIntegration.IsInstalled();
    }

    /// <summary>
    /// .NET Framework does not rescale explicit pixel sizes for per-monitor DPI, and the monitor's DPI
    /// is only known once the window handle exists, so size everything here.
    /// </summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Size client = Scaled(680, 780);
        Rectangle area = Screen.FromHandle(Handle).WorkingArea;
        ClientSize = new Size(Math.Min(client.Width, area.Width - Scale(40)), Math.Min(client.Height, area.Height - Scale(60)));
        MinimumSize = Scaled(560, 560);
        Padding = new Padding(Scale(12));
        _intro.MaximumSize = new Size(ClientSize.Width - Scale(40), 0);
        _language.Width = Scale(120);
        _progress.Height = Scale(18);
        foreach (Button button in new[] { _install, _uninstall, _openSettings })
        {
            button.MinimumSize = Scaled(110, 30);
        }

        _close.MinimumSize = Scaled(90, 30);
        CenterToScreen();
    }

    private float DpiScale => (IsHandleCreated ? GetDpiForWindow(Handle) : 96) / 96f;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    private int Scale(int value) => (int)Math.Round(value * DpiScale);

    private Size Scaled(int width, int height) => new(Scale(width), Scale(height));

    public void Log(string message) => OnUi(() => _log.AppendText(message + Environment.NewLine));

    public void EnteringCommitPhase() => OnUi(() =>
    {
        _committing = true;
        _close.Enabled = false;
    });

    public void Progress(int value, int maximum) => OnUi(() =>
    {
        _progress.Maximum = Math.Max(1, maximum);
        _progress.Value = Math.Min(value, _progress.Maximum);
    });

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(_language);
        root.Controls.Add(_intro);
        _saversBox.Controls.Add(_savers);
        root.Controls.Add(_saversBox);

        var names = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        foreach ((UiLanguage language, string label) in Languages)
        {
            var check = new CheckBox { Text = label, AutoSize = true, Margin = new Padding(3, 3, 24, 3) };
            _nameChecks[language] = check;
            names.Controls.Add(check);
        }

        _namesBox.Controls.Add(names);
        root.Controls.Add(_namesBox);
        root.Controls.Add(_progress);
        root.Controls.Add(_log);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.AddRange([_close, _openSettings, _uninstall, _install]);
        root.Controls.Add(buttons);
        return root;
    }

    private void ApplyText(SetupText text)
    {
        _text = text;
        Font = new Font(text.FontName, 9F);
        Text = text.Title;
        _intro.Text = text.Intro;
        _saversBox.Text = text.SaversLabel;
        _namesBox.Text = text.NamesLabel;
        _install.Text = text.Install;
        _uninstall.Text = text.Uninstall;
        _openSettings.Text = text.OpenSettings;
        _close.Text = text.Close;
        RefreshSaverNames();
    }

    /// <summary>Opened from Apps &amp; Features: go straight to the uninstall confirmation.</summary>
    public void UninstallWhenShown() => Shown += (_, _) => BeginInvoke((Action)RunUninstall);

    private void RefreshSaverNames()
    {
        bool[] checkedStates = Enumerable.Range(0, _savers.Items.Count).Select(_savers.GetItemChecked).ToArray();
        object[] items = _savers.Items.Cast<object>().ToArray();
        _savers.BeginUpdate();
        _savers.Items.Clear();
        for (int i = 0; i < items.Length; i++)
        {
            _savers.Items.Add(items[i], checkedStates[i]);
        }

        _savers.EndUpdate();
        if (_savers.Items.Count > 0)
        {
            _savers.TopIndex = 0;
        }
    }

    private void FormatSaver(object? sender, ListControlConvertEventArgs e)
    {
        if (e.ListItem is SaverInfo saver)
        {
            e.Value = $"{saver.Year}　{saver.NameIn(_text.Language)}";
        }
    }

    private async Task RunInstallAsync()
    {
        List<SaverInfo> savers = _savers.CheckedItems.Cast<SaverInfo>().ToList();
        List<UiLanguage> languages = Languages.Select(l => l.Language).Where(l => _nameChecks[l].Checked).ToList();
        if (savers.Count == 0 || languages.Count == 0)
        {
            MessageBox.Show(this, _text.PickSomething, _text.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SetBusy(true);
        _log.Clear();
        _cancel = new CancellationTokenSource();
        try
        {
            var installer = new Installer(_text, this);
            CancellationToken token = _cancel.Token;
            InstallReport report = await Task.Run(() => installer.InstallAsync(new InstallRequest(savers, languages), token));
            Log(string.Empty);
            Log(Summarize(_text, report));
        }
        catch (OperationCanceledException)
        {
            Log(_text.NothingInstalled);
        }
        catch (Exception ex)
        {
            Log(string.Format(_text.Error, ex.Message));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RunUninstall()
    {
        if (MessageBox.Show(this, _text.ConfirmUninstall, _text.Title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }

        SetBusy(true);
        _log.Clear();
        try
        {
            new Installer(_text, this).Uninstall();
        }
        catch (Exception ex)
        {
            Log(string.Format(_text.Error, ex.Message));
        }
        finally
        {
            SetBusy(false);
        }
    }

    internal static string Summarize(SetupText text, InstallReport report)
    {
        var lines = new List<string>();
        if (report.Installed.Count == 0)
        {
            lines.Add(text.NothingInstalled);
        }
        else if (report.Failed.Count == 0)
        {
            lines.Add(string.Format(text.Done, report.Installed.Count));
        }
        else
        {
            string names = string.Join(", ", report.Failed.Select(f => f.Saver.NameIn(text.Language)));
            lines.Add(string.Format(text.DoneWithFailures, report.Installed.Count, report.Failed.Count, names));
        }

        if (!string.IsNullOrEmpty(report.ActiveScreensaver))
        {
            lines.Add(string.Format(text.ActiveChanged, Path.GetFileNameWithoutExtension(report.ActiveScreensaver)));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void OnCloseOrCancel()
    {
        if (_busy)
        {
            RequestCancel();
        }
        else
        {
            Close();
        }
    }

    /// <summary>Cancelling is only honoured while downloading; once files are being replaced it must finish.</summary>
    private void RequestCancel()
    {
        if (!_committing)
        {
            _cancel?.Cancel();
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _committing = false;
        _close.Text = busy ? _text.Cancel : _text.Close;
        _close.Enabled = true;
        _install.Enabled = !busy;
        _uninstall.Enabled = !busy && SystemIntegration.IsInstalled();
        _savers.Enabled = !busy;
        _namesBox.Enabled = !busy;
        _language.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void OnUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(action);
        }
        else
        {
            action();
        }
    }
}
