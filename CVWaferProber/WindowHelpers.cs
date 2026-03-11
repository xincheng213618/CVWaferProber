using System.Windows;

namespace CVWaferProber
{
    public static class WindowHelpers
    {

        public static Window? GetActiveWindow(this System.Windows.Application application)
        {
            foreach (Window window in application.Windows)
                if (window.IsActive) return window;
            return System.Windows.Application.Current.MainWindow;
        }
    }
}
