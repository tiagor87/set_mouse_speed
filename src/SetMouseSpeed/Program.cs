using Microsoft.Extensions.Configuration;
using System.Runtime.InteropServices;

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
    
    [DllImport("user32.dll")]
    static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref int pvParam, uint fWinIni);

    [DllImport("user32.dll")]
    static extern bool SystemParametersInfo(uint uiAction, uint uiParam, int pvParam, uint fWinIni);

    static async Task Main(string[] _)
    {
        Options options = GetOptions();

        var currentSpeed = 0;

        SystemParametersInfo(SPI_GETMOUSESPEED, 0, ref currentSpeed, 0);

        if (currentSpeed != MIN_SPEED)
        {
            SystemParametersInfo(SPI_SETMOUSESPEED, 0, MIN_SPEED, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            if (options.ShowNotification) await ShowNotificationAsync(MIN_SPEED);
            return;
        }

        SystemParametersInfo(SPI_SETMOUSESPEED, 0, Math.Max(options.DefaultSpeed, DEFAULT_SPEED), SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

        if (options.ShowNotification) await ShowNotificationAsync(options.DefaultSpeed);
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

    private static async ValueTask ShowNotificationAsync(int speed)
    {
        using var notification = new NotifyIcon();
        notification.Icon = SystemIcons.Information;
        notification.Visible = true;
                
        var title = "Mouse toggle";
        var body = speed == 1 ? "Modo lento" : $"Modo Normal ({speed})";

        notification.ShowBalloonTip(2000, title, body, ToolTipIcon.Info);

        await Task.Delay(500);
    }
}
