using DoraSaver.Core;

namespace DoraSaver.UI;

/// <summary>
/// One borderless, topmost black window per monitor. The primary monitor's window also hosts the player.
/// </summary>
internal sealed class FullScreenForm : Form
{
    private readonly PlayerView? _player;

    public FullScreenForm(Screen screen, bool hostsPlayer)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = screen.Bounds;
        BackColor = Color.Black;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        Text = "DoraSaver";

        if (hostsPlayer)
        {
            _player = new PlayerView();
            _player.WakeRequested += (_, reason) => WakeRequested?.Invoke(this, $"page {reason}");
            Controls.Add(_player);
        }
    }

    /// <summary>Input seen by this window or its page; a backup for the global hooks.</summary>
    public event EventHandler<string>? WakeRequested;

    public async Task StartPlayerAsync(string assetFolder, SaverInfo saver, SaverSettings settings)
    {
        if (_player is null)
        {
            return;
        }

        try
        {
            await _player.StartAsync(assetFolder, saver, settings.Layout, muted: !settings.PlaySound);
        }
        catch (Exception ex)
        {
            // Keep the black screen; the user can still dismiss it normally.
            Log.Error("Starting the player failed", ex);
            _player.Visible = false;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        WakeRequested?.Invoke(this, "form key");
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        WakeRequested?.Invoke(this, "form click");
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Cursor.Hide();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Cursor.Show();
    }
}
