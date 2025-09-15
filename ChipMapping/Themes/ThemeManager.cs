using System.Windows;
using System.Windows.Media;

namespace ChipMapping.Themes
{
    public static class ThemeManager
    {
        public static void ApplyWhiteTheme()
        {
            // 白色主题的颜色配置
            Application.Current.Resources["BackgroundColor"] = Brushes.White;
            Application.Current.Resources["ForegroundColor"] = new SolidColorBrush(Color.FromRgb(51, 51, 51));
            Application.Current.Resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(204, 204, 204));
        }

        public static void ApplyDarkTheme()
        {
            // 深色主题的颜色配置
            Application.Current.Resources["BackgroundColor"] = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            Application.Current.Resources["ForegroundColor"] = Brushes.White;
            Application.Current.Resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(64, 64, 64));
        }
    }
}