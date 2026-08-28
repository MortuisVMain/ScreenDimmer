using System;
using System.Threading;
using System.Windows.Forms;

namespace ScreenDimmer;

internal static class Program
{
    private const string AppMutexName = "ScreenDimmer_App_SingleInstance_Mutex";

    [STAThread]
    private static void Main(string[] args)
    {
        // Включаем поддержку Per-Monitor V2 DPI awareness для идеального позиционирования на экранах ноутбуков с масштабированием
        try
        {
            NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        catch { }

        // Генерация иконки при запуске с ключом --make-icon или если файл отсутствует
        if (args.Length > 0 && args[0] == "--make-icon")
        {
            IconMaker.GenerateAppIcon("app.ico");
            Console.WriteLine("Icon generated successfully.");
            return;
        }

        using var mutex = new Mutex(true, AppMutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            MessageBox.Show(
                "Приложение Screen Dimmer уже запущено в системном трее (возле часов).",
                "Screen Dimmer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
