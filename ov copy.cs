using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading.Tasks;

public class TransparentOverlay : Form
{
    private LowLevelMouseProc _mouseProc;
    private IntPtr _hookID = IntPtr.Zero;
    private Point _lastClickPosition;
    private float _currentOpacity = 0f;
    private bool _isAnimating = false;
    private Timer _animationTimer;

    private SolidBrush _mouseBrush;
    private Pen _mousePen;

    public TransparentOverlay()
    {
        // ウィンドウの基本設定
        this.FormBorderStyle = FormBorderStyle.None;
        this.TopMost = true;
        this.WindowState = FormWindowState.Maximized;
        this.BackColor = Color.Magenta;
        this.TransparencyKey = Color.Magenta;
        this.Opacity = 0.5;
        this.ShowInTaskbar = true;
        this.DoubleBuffered = true;

        // アニメーション用タイマー
        _animationTimer = new Timer();
        _animationTimer.Interval = 16; // 約60FPS
        _animationTimer.Tick += AnimationTick;

        _mouseBrush = new SolidBrush(Color.FromArgb(0, 255, 0, 0));
        _mousePen = new Pen(Color.Red, 2);

        // マウス透過設定
        SetClickThrough(this.Handle);

        // マウスフックを設定
        _mouseProc = HookCallback;
        _hookID = SetHook(_mouseProc);

        // ESCキーで終了
        this.KeyPreview = true;
        this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_isAnimating && _currentOpacity > 0)
        {
            // 現在の透明度を設定
            _mouseBrush.Color = Color.FromArgb((int)(_currentOpacity * 255), 255, 0, 0);
            _mousePen.Color = Color.FromArgb((int)(_currentOpacity * 255), 200, 0, 0);

            // マウス形状を図形で描画
            DrawMouseIcon(e.Graphics, _lastClickPosition);
        }
    }

    private void DrawMouseIcon(Graphics g, Point position)
    {
        // マウスの本体（円）
        g.FillEllipse(_mouseBrush, position.X - 15, position.Y - 15, 30, 30);
        g.DrawEllipse(_mousePen, position.X - 15, position.Y - 15, 30, 30);

        // マウスのボタン（2つの円弧）
        g.DrawArc(_mousePen, position.X - 10, position.Y - 10, 20, 20, 30, 120);
        g.DrawArc(_mousePen, position.X - 10, position.Y - 10, 20, 20, 210, 120);

        // マウスの矢印（線）
        Point[] arrowPoints = {
            new Point(position.X + 20, position.Y + 20),
            new Point(position.X + 30, position.Y + 30),
            new Point(position.X + 25, position.Y + 25),
            new Point(position.X + 30, position.Y + 20),
            new Point(position.X + 20, position.Y + 30)
        };
        g.DrawLines(_mousePen, arrowPoints);
    }

    private async void AnimationTick(object sender, EventArgs e)
    {
        if (!_isAnimating) return;

        // フェードイン (0 → 1)
        if (_currentOpacity < 1f)
        {
            _currentOpacity += 0.1f;
            if (_currentOpacity > 1f) _currentOpacity = 1f;
            this.Invalidate();
            return;
        }

        // 2秒間表示
        await Task.Delay(2000);

        // フェードアウト (1 → 0)
        while (_currentOpacity > 0f)
        {
            _currentOpacity -= 0.05f;
            if (_currentOpacity < 0f) _currentOpacity = 0f;
            this.Invalidate();
            await Task.Delay(16);
        }

        // アニメーション終了
        _isAnimating = false;
        _animationTimer.Stop();
    }

    private void StartAnimation(Point position)
    {
        _lastClickPosition = position;
        _currentOpacity = 0f;
        _isAnimating = true;
        _animationTimer.Start();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            if (wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                Point clickPos = new Point(hookStruct.pt.x, hookStruct.pt.y);
                
                // UIスレッドでアニメーションを開始
                this.BeginInvoke((MethodInvoker)delegate {
                    StartAnimation(clickPos);
                });
            }
        }
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    // マウスフックの設定
    private static IntPtr SetHook(LowLevelMouseProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    // マウスを通す（クリック無視）
    private void SetClickThrough(IntPtr handle)
    {
        int extendedStyle = GetWindowLong(handle, GWL_EXSTYLE);
        SetWindowLong(handle, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _animationTimer.Stop();
        _animationTimer.Dispose();
        UnhookWindowsHookEx(_hookID);
        _mouseBrush.Dispose();
        _mousePen.Dispose();
        base.OnFormClosed(e);
    }

    // その他の定義 (構造体、定数、DLLインポート) は元のコードと同じ
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_MOUSEWHEEL = 0x020A;
    
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new TransparentOverlay());
    }
}