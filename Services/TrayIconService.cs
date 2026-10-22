using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Application = System.Windows.Application;
using FuturesTicker.Models;

namespace FuturesTicker.Services;

/// <summary>
/// System-tray (notification area) icon. Provides a stable entry point to the
/// app even when the floating widget is mouse-transparent or hidden.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    public static TrayIconService? Instance { get; private set; }

    private readonly NotifyIcon _notify;
    private readonly AppConfig _config = AppConfig.Current;
    private readonly ToolStripMenuItem _showWidgetItem;
    private readonly ToolStripMenuItem _mouseThroughItem;
    private readonly ToolStripMenuItem _showColorItem;
    private readonly ToolStripMenuItem _priceOnlyItem;

    public TrayIconService()
    {
        Instance = this;

        _showWidgetItem = new ToolStripMenuItem("显示悬浮窗")
        {
            Checked = _config.ShowWidget,
            CheckOnClick = true,
        };
        _showWidgetItem.CheckedChanged += (_, _) => SetShowWidget(_showWidgetItem.Checked);

        _mouseThroughItem = new ToolStripMenuItem("鼠标穿透（点击直达下层）")
        {
            Checked = _config.MouseThrough,
            CheckOnClick = true,
        };
        _mouseThroughItem.CheckedChanged += (_, _) => SetMouseThrough(_mouseThroughItem.Checked);

        _showColorItem = new ToolStripMenuItem("涨跌颜色")
        {
            Checked = _config.ShowColor,
            CheckOnClick = true,
        };
        _showColorItem.CheckedChanged += (_, _) => SetShowColor(_showColorItem.Checked);

        _priceOnlyItem = new ToolStripMenuItem("只显示价格")
        {
            Checked = _config.PriceOnly,
            CheckOnClick = true,
        };
        _priceOnlyItem.CheckedChanged += (_, _) => SetPriceOnly(_priceOnlyItem.Checked);

        var menu = new ContextMenuStrip();
        var open = new ToolStripMenuItem("打开主界面");
        open.Click += (_, _) => MainControlWindow.ShowWindow();
        menu.Items.Add(open);
        menu.Items.Add(_showWidgetItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_mouseThroughItem);
        menu.Items.Add(_showColorItem);
        menu.Items.Add(_priceOnlyItem);
        menu.Items.Add(new ToolStripSeparator());
        var exit = new ToolStripMenuItem("退出");
        exit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(exit);

        _notify = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "期货行情助手",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notify.DoubleClick += (_, _) => MainControlWindow.ShowWindow();
    }

    /// <summary>刷新托盘菜单勾选状态（供主界面/悬浮窗改动开关后同步）。</summary>
    public void SyncFromConfig()
    {
        _showWidgetItem.Checked = _config.ShowWidget;
        _mouseThroughItem.Checked = _config.MouseThrough;
        _showColorItem.Checked = _config.ShowColor;
        _priceOnlyItem.Checked = _config.PriceOnly;
    }

    private void SetShowWidget(bool enabled)
    {
        _config.ShowWidget = enabled;
        ConfigStore.Save(_config);
        MainWindow.Instance?.ApplyWidgetVisibility();
    }

    private void SetMouseThrough(bool enabled)
    {
        _config.MouseThrough = enabled;
        ConfigStore.Save(_config);
        MainWindow.Instance?.ApplyMouseThrough();
    }

    private void SetShowColor(bool enabled)
    {
        _config.ShowColor = enabled;
        ConfigStore.Save(_config);
        MainWindow.Instance?.ApplyDisplaySettings();
    }

    private void SetPriceOnly(bool enabled)
    {
        _config.PriceOnly = enabled;
        ConfigStore.Save(_config);
        MainWindow.Instance?.ApplyDisplaySettings();
    }

    /// <summary>Generate a subtle gray "¥" (RMB) glyph icon at runtime (avoids shipping a .ico asset).</summary>
    private static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            // 低调的灰色人民币符号，透明底，不抢眼。
            using var font = new Font("Microsoft YaHei", 20f, FontStyle.Regular, GraphicsUnit.Pixel);
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            using var brush = new SolidBrush(Color.FromArgb(128, 128, 128));
            g.DrawString("¥", font, brush, new RectangleF(0, 0, 32, 32), sf);
        }

        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
        if (Instance == this) Instance = null;
    }
}
