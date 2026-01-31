using System.Diagnostics;
using System.Reflection;
using Application = System.Windows.Application;

namespace CVWaferProber.Language
{
    public class LanguageManager
    {
        private static LanguageManager? _instance;
        public static LanguageManager Instance => _instance ??= new LanguageManager();
        public LanguageManager()
        {
            // 注册到全局资源
            if (Application.Current != null)
            {
                Application.Current.Resources["LanguageManager"] = this;
            }
        }

        // 获取格式化版权信息
        public string GetCopyrightNotice()
        {
            try
            {
                // 从EXE文件获取公司信息
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                var versionInfo = FileVersionInfo.GetVersionInfo(assemblyPath);

                string companyName = !string.IsNullOrEmpty(versionInfo.CompanyName)
                    ? versionInfo.CompanyName
                    : GetString("CompanyName");

                string currentYear = DateTime.Now.Year.ToString();

                // 使用资源字符串格式化
                string format = GetString("CopyrightNotice");
                return string.Format(format, currentYear, companyName);
            }
            catch
            {
                // 回退到静态资源
                return string.Format(GetString("CopyrightNotice"),
                    GetString("CurrentYear"),
                    GetString("CompanyName"));
            }
        }

        // 获取动态年份
        public string GetDynamicYear()
        {
            return DateTime.Now.Year.ToString();
        }

        // 获取公司名称（优先从EXE文件）
        public string GetDynamicCompanyName()
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                var versionInfo = FileVersionInfo.GetVersionInfo(assemblyPath);

                return !string.IsNullOrEmpty(versionInfo.CompanyName)
                    ? versionInfo.CompanyName
                    : GetString("CompanyName");
            }
            catch
            {
                return GetString("CompanyName");
            }
        }
        public string GetString(string key)
        {
            return Application.Current.TryFindResource(key) as string ?? key;
        }
    }
}
