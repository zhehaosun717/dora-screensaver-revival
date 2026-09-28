using DoraSaver.Core;
using static DoraSaver.Native.NativeMethods;

namespace DoraSaver.UI;

/// <summary>
/// Child window inside the little monitor of the Screen Saver Settings dialog (/p HWND). Always muted.
/// </summary>
internal sealed class PreviewForm : Form
{
    private readonly IntPtr _parent;
    private readonly System.Windows.Forms.Timer _parentWatch;
    private readonly PlayerView _player;

    public PreviewForm(IntPtr parent)
    {
        _parent = parent;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        BackColor = Color.Black;
        Text = "DoraSaver preview";
        Bounds = ParentClientBounds();

        _player = new PlayerView();
        Controls.Add(_player);

        _parentWatch = new System.Windows.Forms.Timer { Interval = 500 };
        _parentWatch.Tick += (_, _) => FollowParent();
    }

    /// <summary>
    /// Re-parent into the dialog's preview window and turn into a child window. WinForms ignores a
    /// WS_CHILD style supplied through CreateParams on a Form, so this is done after the handle exists.
    /// </summary>
    public void AttachToParent()
    {
        if (SetParent(Handle, _parent) == IntPtr.Zero)
        {
            Log.Error($"SetParent failed (Win32 error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()})");
        }

        long style = GetWindowLongPtr(Handle, GWL_STYLE).ToInt64();
        style = (style | WS_CHILD | WS_CLIPCHILDREN) & ~WS_POPUP;
        SetWindowLongPtr(Handle, GWL_STYLE, new IntPtr(style));
        Bounds = ParentClientBounds();
    }

    public async Task StartPlayerAsync(string assetFolder, SaverInfo saver, SaverSettings settings)
    {
        _parentWatch.Start();
        try
        {
            await _player.StartAsync(assetFolder, saver, settings.Layout, muted: true);
        }
        catch (Exception ex)
        {
            Log.Error("Starting the preview failed", ex);
            _player.Visible = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _parentWatch.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// The dialog destroys our window (as its child) when the preview changes; WinForms does not
    /// treat that as a close, so end the message loop explicitly.
    /// </summary>
    protected override void OnHandleDestroyed(EventArgs e)
    {
        base.OnHandleDestroyed(e);
        if (!RecreatingHandle)
        {
            Application.ExitThread();
        }
    }

    private void FollowParent()
    {
        if (!IsWindow(_parent) || !IsWindowVisible(_parent))
        {
            Application.ExitThread();
            return;
        }

        Rectangle wanted = ParentClientBounds();
        if (wanted.Size != Size)
        {
            Bounds = wanted;
        }
    }

    private Rectangle ParentClientBounds()
    {
        return GetClientRect(_parent, out RECT r)
            ? new Rectangle(0, 0, Math.Max(1, r.Right - r.Left), Math.Max(1, r.Bottom - r.Top))
            : new Rectangle(0, 0, 152, 112);
    }
}
