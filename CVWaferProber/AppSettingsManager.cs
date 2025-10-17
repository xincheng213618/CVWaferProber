using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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
            if (app?.Resources?.MergedDictionaries?.Count > 0)
            {
                app.Resources.MergedDictionaries.Clear();

                var dictionaryPath = language == "English"
                    ? "Language/English.xaml"
                    : "Language/Chinese.xaml";

                var newDictionary = new ResourceDictionary
                {
                    Source = new Uri(dictionaryPath, UriKind.Relative)
                };
                app.Resources.MergedDictionaries.Add(newDictionary);
            }
        }

        public static void InitializeLanguage()
        {
            ApplyLanguage(CurrentLanguage);
        }
    }
}
