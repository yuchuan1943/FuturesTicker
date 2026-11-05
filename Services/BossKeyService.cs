using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using FuturesTicker.Native;

namespace FuturesTicker.Services;

/// <summary>
/// 老板键服务：注册一个系统级全局热键，按下时隐藏所有行情窗口（浮窗、主界面、
/// 分时图、期权详情），再次按下恢复。用独立的隐藏 HwndSource 消息窗口接收 WM_HOTKEY。
/// </summary>
public sealed class BossKeyService : IDisposable
{
    public static BossKeyService? Instance { get; private set; }

    private const int HotkeyId = 0xB055; // 任意固定 id

    private readonly HwndSource _source;
    private readonly AppConfig _config = AppConfig.Current;
    private readonly List<Window> _hidden = new();
    private bool _registered;

    public BossKeyService()
    {
        Instance = this;

        // 创建隐藏消息窗口，仅用于接收 WM_HOTKEY，不参与 UI。
        var p = new HwndSourceParameters("BossKeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
        };
        _source = new HwndSource(p);
        _source.AddHook(WndProc);

        Apply();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Toggle();
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>按当前配置重新注册热键（先注销旧的）。</summary>
    public void Apply()
    {
        if (_registered)
        {
            Win32.UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }

        if (!_config.BossKeyEnabled) return;

        uint mods = (uint)_config.BossKeyModifiers;
        uint vk = (uint)_config.BossKeyKey;
        if (vk == 0) return;

        _registered = Win32.RegisterHotKey(_source.Handle, HotkeyId, mods, vk);
    }

    /// <summary>热键触发：隐藏所有可见窗口 / 恢复之前隐藏的窗口。</summary>
    private void Toggle()
    {
        if (_hidden.Count > 0)
        {
            foreach (var w in _hidden)
            {
                w.Show();
            }
            _hidden.Clear();
        }
        else
        {
            foreach (Window w in Application.Current.Windows)
            {
                if (w.IsVisible)
                {
                    w.Hide();
                    _hidden.Add(w);
                }
            }
        }
    }

    /// <summary>把修饰键位标志 + 虚拟键码格式化成可读文本（如 "Ctrl + Shift + H"）。</summary>
    public static string Format(int modifiers, int vk)
    {
        var parts = new List<string>();
        if ((modifiers & 2) != 0) parts.Add("Ctrl");
        if ((modifiers & 1) != 0) parts.Add("Alt");
        if ((modifiers & 4) != 0) parts.Add("Shift");
        if ((modifiers & 8) != 0) parts.Add("Win");

        if (vk > 0)
        {
            var key = KeyInterop.KeyFromVirtualKey(vk);
            parts.Add(KeyName(key));
        }

        return parts.Count > 0 ? string.Join(" + ", parts) : "（未设置）";
    }

    private static string KeyName(Key key) => key switch
    {
        Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3", Key.D4 => "4",
        Key.D5 => "5", Key.D6 => "6", Key.D7 => "7", Key.D8 => "8", Key.D9 => "9",
        Key.NumPad0 => "Num0", Key.NumPad1 => "Num1", Key.NumPad2 => "Num2",
        Key.NumPad3 => "Num3", Key.NumPad4 => "Num4", Key.NumPad5 => "Num5",
        Key.NumPad6 => "Num6", Key.NumPad7 => "Num7", Key.NumPad8 => "Num8",
        Key.NumPad9 => "Num9",
        Key.OemPlus => "+", Key.OemMinus => "-", Key.OemComma => ",", Key.OemPeriod => ".",
        Key.OemQuestion => "?", Key.OemOpenBrackets => "[", Key.OemCloseBrackets => "]",
        Key.OemPipe => "\\", Key.OemSemicolon => ";", Key.OemQuotes => "'",
        Key.OemTilde => "`",
        Key.Escape => "Esc", Key.Back => "Backspace", Key.Space => "Space",
        Key.Return => "Enter", Key.Tab => "Tab", Key.Capital => "CapsLock",
        Key.Left => "←", Key.Right => "→", Key.Up => "↑", Key.Down => "↓",
        Key.Delete => "Delete", Key.Insert => "Insert", Key.Home => "Home", Key.End => "End",
        Key.PageUp => "PageUp", Key.PageDown => "PageDown",
        Key.LWin => "Win", Key.RWin => "Win",
        Key.LeftCtrl => "Ctrl", Key.RightCtrl => "Ctrl",
        Key.LeftAlt => "Alt", Key.RightAlt => "Alt",
        Key.LeftShift => "Shift", Key.RightShift => "Shift",
        _ => key.ToString(),
    };

    public void Dispose()
    {
        if (_registered)
        {
            Win32.UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }
        _source.Dispose();
        if (Instance == this) Instance = null;
    }
}
