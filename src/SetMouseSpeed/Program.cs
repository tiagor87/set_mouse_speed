using Microsoft.Extensions.Configuration;
using System.Runtime.InteropServices;

using MouseKeyboardActivityMonitor;
using MouseKeyboardActivityMonitor.WinApi;

namespace SetMouseSpeed;

class Options
{
    public int DefaultSpeed { get; set; }
    public bool ShowNotification { get; set; }
}

class Program
{
    private const uint SPI_GETMOUSESPEED = 0x0070;
    private const uint SPI_SETMOUSESPEED = 0x0071;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;
    private const int DEFAULT_SPEED = 10;
    private const int MIN_SPEED = 1;
    private const int MOUSE_DELTA = 30;

    [DllImport("user32.dll")]
    static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref int pvParam, uint fWinIni);

    [DllImport("user32.dll")]
    static extern bool SystemParametersInfo(uint uiAction, uint uiParam, int pvParam, uint fWinIni);

    [STAThread]
    static void Main(string[] _)
    {
        Options options = GetOptions();

        ApplicationConfiguration.Initialize();
        using var window = new HotkeyWindow(options);
        var __ = window.Handle;        
        Application.Run();
    }

    private static Options GetOptions()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();
        
        var options = new Options();
        configuration.Bind(options);
        return options;
    }

    private static void ShowNotification(int speed)
    {
        using var notification = new NotifyIcon();
        notification.Icon = SystemIcons.Information;
        notification.Visible = true;
                
        var title = "Mouse toggle";
        var body = speed == 1 ? "Modo lento" : $"Modo Normal ({speed})";

        notification.ShowBalloonTip(2000, title, body, ToolTipIcon.Info);
    }

    private static void ReduceMouseSpeed(Options options)
    {
        var currentSpeed = 0;
        SystemParametersInfo(SPI_GETMOUSESPEED, 0, ref currentSpeed, 0);

        
        if (currentSpeed == MIN_SPEED)
        {
            return;
        }
        
        var newSpeed = MIN_SPEED;
        SystemParametersInfo(SPI_SETMOUSESPEED, 0, newSpeed, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        if (options.ShowNotification)
        {
            ShowNotification(newSpeed);
        }
    }
    
    private static void ResetMouseSpeed(Options options)
    {
        var currentSpeed = 0;
        SystemParametersInfo(SPI_GETMOUSESPEED, 0, ref currentSpeed, 0);

        if (currentSpeed != MIN_SPEED)
        {
            return;
        }
        
        var newSpeed = Math.Max(options.DefaultSpeed, DEFAULT_SPEED);
        SystemParametersInfo(SPI_SETMOUSESPEED, 0, newSpeed, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        
        if (options.ShowNotification)
        {
            ShowNotification(newSpeed);
        }
    }

    private sealed class HotkeyWindow : Form
    {
        private readonly Options _options;
        private readonly KeyboardHookListener _globalKeyboardListener;
        private readonly MouseHookListener _globalMouseListener;
        private SemaphoreSlim _semaphore = new(1);
        private bool _isModifierPressed;
        private Point _lastMousePosition;
        

        public HotkeyWindow(Options options)
        {
            _options = options;
            _globalKeyboardListener = new KeyboardHookListener(new GlobalHooker());
            _globalKeyboardListener.KeyDown += OnKeyDown;
            _globalKeyboardListener.KeyUp += OnKeyUp;
            
            _globalMouseListener = new MouseHookListener(new GlobalHooker());
            _globalMouseListener.MouseMove += OnMouseMove;

            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            Visible = false;
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (_isModifierPressed)
            {
                var delta = Math.Abs(_lastMousePosition.X - e.X);
                if (delta >= MOUSE_DELTA)
                {
                    ReduceMouseSpeed(_options);
                    return;
                }
            }
            
            _lastMousePosition = e.Location;
        }
        
        private void OnKeyDown(object? sender, KeyEventArgs args)
        {
            if (_isModifierPressed)
            {
                return;
            }
            
            if (_semaphore.Wait(TimeSpan.FromMilliseconds(50)))
            {
                return;
            }
            try
            {
                if (args is { Control: false, Shift: false })
                {
                    _isModifierPressed = false;
                    return;
                }
                
                _isModifierPressed = true;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        private void OnKeyUp(object? sender, KeyEventArgs args)
        {
            if (!_isModifierPressed)
            {
                return;
            }
            
            if (!_semaphore.Wait(TimeSpan.FromMilliseconds(50)))
            {
                return;
            }
            try
            {
                _isModifierPressed = false;
                ResetMouseSpeed(_options);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _globalKeyboardListener.Start();
            _globalMouseListener.Start();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _globalKeyboardListener.Stop();
            _globalMouseListener.Stop();
            base.OnHandleDestroyed(e);
        }
    }
}
