using ST.Library.UI;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace CVWaferProber
{
    public static class AppSettingsManager
    {
        public static string CurrentLanguage => Properties.Settings.Default.AppLanguage;
       
        public static void ChangeLanguage(string language)
        {
            if (language == "Chinese" || language == "English")
            {
                Properties.Settings.Default.AppLanguage = language;
                Properties.Settings.Default.Save();
                ApplyLanguage(language);
            }
        }

        private static void ApplyLanguage(string language)
        {
            var app = Application.Current;
            if (app?.Resources?.MergedDictionaries == null) return;

            app.Resources.MergedDictionaries.Clear();

            // 修正资源字典路径（根据实际文件存放位置调整）
            // 示例：资源字典放在项目的"Language"文件夹下
            var dictionaryPath = language == "English"
                ? "Language/English.xaml"  // 英文资源文件路径
                : "Language/Chinese.xaml"; // 中文资源文件路径

            try
            {
                var newDictionary = new ResourceDictionary
                {
                    Source = new Uri(dictionaryPath, UriKind.Relative)
                };
                app.Resources.MergedDictionaries.Add(newDictionary);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load language resources：{ex.Message}");
            }
            //FlowDirection ST node
            if(language== "Chinese")
            {
                Lang.SetLanguage("zh-CN");
            }
            else
            {
                Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
            }
        }

        public static void InitializeLanguage()
        {
            ApplyLanguage(CurrentLanguage);
        }
    }
}
