using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading.Tasks;

public class TransparentOverlay : Form
{
    private LowLevelMouseProc _mouseProc;
    private IntPtr _hookID = IntPtr.Zero;
    private readonly List<ClickEffect> _activeEffects = new List<ClickEffect>();
    private readonly object _lockObj = new object();
    private readonly Bitmap _leftClickIcon;
    private readonly Bitmap _rightClickIcon;
    private readonly Bitmap _wheelClickIcon;
    private readonly Timer _animationTimer;

    public TransparentOverlay()
    {
        // ウィンドウの基本設定
        this.FormBorderStyle = FormBorderStyle.None;
        this.TopMost = true;
        this.WindowState = FormWindowState.Maximized;
        this.BackColor = Color.Magenta;
        this.TransparencyKey = Color.Magenta;
        this.Opacity = 0.85;
        this.ShowInTaskbar = true;
        this.DoubleBuffered = true;

        // アイコン画像の読み込み
        _leftClickIcon = CreateClickIcon(highlightLeft: true);
        _rightClickIcon = CreateClickIcon(highlightRight: true);
        _wheelClickIcon = CreateClickIcon(highlightWheel: true);

        // マウス透過設定
        SetClickThrough(this.Handle);

        // マウスフックを設定
        _mouseProc = HookCallback;
        _hookID = SetHook(_mouseProc);

        // アニメーション用タイマー
        _animationTimer = new Timer { Interval = 50 }; // ~60FPS
        _animationTimer.Tick += (s, e) => this.Invalidate();
        _animationTimer.Start();
    }

    private Bitmap CreateClickIcon(bool highlightLeft = false, bool highlightRight = false, bool highlightWheel = false, bool scrollUp = false, bool scrollDown = false)
    {
        var bmp = new Bitmap(64, 108);
        using (var g = Graphics.FromImage(bmp))
        {           
            // 角丸長方形のパスを作成
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int radius = 25; // 角の半径
            Rectangle baseRect = new Rectangle(8, 24, 48, 48);

            // 角丸長方形のパスを構築
            path.AddArc(baseRect.X, baseRect.Y, radius, radius, 180, 90);
            path.AddArc(baseRect.X + baseRect.Width - radius, baseRect.Y, radius, radius, 270, 90);
            path.AddArc(baseRect.X + baseRect.Width - radius, baseRect.Y + baseRect.Height - radius, radius, radius, 0, 90);
            path.AddArc(baseRect.X, baseRect.Y + baseRect.Height - radius, radius, radius, 90, 90);
            path.CloseAllFigures();

            // マウス本体の描画（角丸）
            g.FillPath(Brushes.LightGray, path);
            g.DrawPath(Pens.DarkGray, path);

            // 左ボタン（角丸）
            var leftButtonRect = new Rectangle(8, 24, 20, 28);
            var leftButtonPath = new System.Drawing.Drawing2D.GraphicsPath();
            leftButtonPath.AddArc(leftButtonRect.X, leftButtonRect.Y, radius, radius, 180, 90);
            leftButtonPath.AddLine(leftButtonRect.X + leftButtonRect.Width, leftButtonRect.Y, leftButtonRect.X + leftButtonRect.Width, leftButtonRect.Y + leftButtonRect.Height);
            leftButtonPath.AddLine(leftButtonRect.X + leftButtonRect.Width, leftButtonRect.Y + leftButtonRect.Height, leftButtonRect.X, leftButtonRect.Y + leftButtonRect.Height);
            leftButtonPath.CloseAllFigures();

            g.FillPath(highlightLeft ? Brushes.DeepPink : Brushes.White, leftButtonPath);
            g.DrawPath(Pens.DarkGray, leftButtonPath);

            // 右ボタン（角丸）
            var rightButtonRect = new Rectangle(36, 24, 20, 28);
            var rightButtonPath = new System.Drawing.Drawing2D.GraphicsPath();
            rightButtonPath.AddLine(rightButtonRect.X, rightButtonRect.Y, rightButtonRect.X + rightButtonRect.Width - radius, rightButtonRect.Y);
            rightButtonPath.AddArc(rightButtonRect.X + rightButtonRect.Width - radius, rightButtonRect.Y, radius, radius, 270, 90);
            rightButtonPath.AddLine(rightButtonRect.X + rightButtonRect.Width, rightButtonRect.Y + radius, rightButtonRect.X + rightButtonRect.Width, rightButtonRect.Y + rightButtonRect.Height);
            rightButtonPath.AddLine(rightButtonRect.X + rightButtonRect.Width, rightButtonRect.Y + rightButtonRect.Height, rightButtonRect.X, rightButtonRect.Y + rightButtonRect.Height);
            rightButtonPath.CloseAllFigures();

            g.FillPath(highlightRight ? Brushes.DeepPink : Brushes.White, rightButtonPath);
            g.DrawPath(Pens.DarkGray, rightButtonPath);

            // ホイール
            var wheelBrush = highlightWheel ? Brushes.DeepPink : 
                            scrollUp ? Brushes.Orange :
                            scrollDown ? Brushes.Cyan : 
                            Brushes.White;

            g.FillEllipse(wheelBrush, 24, 30, 16, 10);
            g.DrawEllipse(Pens.DarkGray, 24, 30, 16, 10);

            // スクロール方向を示す矢印
            if (scrollUp)
            {
                g.DrawString("⏫", new Font("Yu Gothic UI", 30, FontStyle.Bold), Brushes.Orange, 20, 26);
            }
            else if (scrollDown)
            {
                g.DrawString("⏬", new Font("Yu Gothic UI", 30, FontStyle.Bold), Brushes.Cyan, 20, 26);
            }
        }
        return bmp;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        lock (_lockObj)
        {
            foreach (var effect in _activeEffects)
            {
                if (effect.IsCircle)
                {
                    // 輪の描画（半透明の青い円）
                    using (var pen = new Pen(Color.FromArgb(150, 0, 120, 255), 3))
                    {
                        e.Graphics.DrawEllipse(pen, 
                            effect.Location.X - effect.Radius,
                            effect.Location.Y - effect.Radius,
                            effect.Radius * 2,
                            effect.Radius * 2);
                    }
                }
                else
                {
                    // アイコンの描画
                    e.Graphics.DrawImage(effect.Icon, effect.Location);
                }
            }
        }
    }

    private class ClickEffect
    {
        public Bitmap Icon { get; set; }
        public Point Location { get; set;}
        public int Radius { get; set; }
        public bool IsCircle { get; set;}

        public ClickEffect(Bitmap icon, Point location)
        {
            Icon = icon;
            Location = location;
            IsCircle = false;
        }
        
        public ClickEffect(Point location)
        {
            Location = location;
            Radius = 16;
            IsCircle = true;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (wParam == (IntPtr)WM_MOUSEMOVE) return CallNextHookEx(_hookID, nCode, wParam, lParam);
        if (nCode >= 0)
        {
            MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            Point clickPos = new Point(hookStruct.pt.x, hookStruct.pt.y);

            // 輪の表示 (すべてのクリックタイプで表示)
            var circleEffect = new ClickEffect(clickPos);
            lock (_lockObj)
            {
                _activeEffects.Add(circleEffect);
            }
            
            Task.Delay(200).ContinueWith(t => 
            {
                lock (_lockObj)
                {
                    _activeEffects.Remove(circleEffect);
                }
                this.Invalidate();
            }, TaskScheduler.FromCurrentSynchronizationContext());

            // クリックタイプに応じたアイコン表示
            Bitmap icon = null;
            if (wParam == (IntPtr)WM_LBUTTONDOWN) icon = _leftClickIcon;
            else if (wParam == (IntPtr)WM_RBUTTONDOWN) icon = _rightClickIcon;
            else if (wParam == (IntPtr)WM_MBUTTONDOWN) icon = _wheelClickIcon;
            else if (wParam == (IntPtr)WM_MOUSEWHEEL)
            {
                int delta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                icon = CreateClickIcon(scrollUp: delta > 0, scrollDown: delta < 0);
            }

            if (icon != null)
            {
                var iconEffect = new ClickEffect(icon, new Point(clickPos.X + 10, clickPos.Y + 10));
                    
                lock (_lockObj)
                {
                    _activeEffects.Add(iconEffect);
                }

                Task.Delay(800).ContinueWith(t => 
                {
                    lock (_lockObj)
                    {
                        _activeEffects.Remove(iconEffect);
                    }
                    this.Invalidate();
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _animationTimer.Stop();
        UnhookWindowsHookEx(_hookID);
        base.OnFormClosed(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            this.Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private static IntPtr SetHook(LowLevelMouseProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    private void SetClickThrough(IntPtr handle)
    {
        int extendedStyle = GetWindowLong(handle, GWL_EXSTYLE);
        SetWindowLong(handle, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public int mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const int WM_MOUSEMOVE = 0x0200;
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TransparentOverlay());
    }
}
