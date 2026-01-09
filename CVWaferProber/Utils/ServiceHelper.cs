using System;
using System.Management;

namespace CVWaferProber.Utils
{

    public static class ServiceHelper
    {
        public static bool IsEnglishMode = false;
        public static string GetServiceExecutablePath(string serviceName)
        {
            try
            {
                string query = $"SELECT PathName FROM Win32_Service WHERE Name = '{serviceName}'";

                using (var searcher = new ManagementObjectSearcher(query))
                using (var collection = searcher.Get())
                {
                    foreach (ManagementObject service in collection)
                    {
                        if (service["PathName"] != null)
                        {
                            string path = service["PathName"].ToString();

                            // 清理路径（移除参数和引号）
                            return CleanExecutablePath(path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{(IsEnglishMode? "Error" : "错误")}: {ex.Message}");
            }

            return null;
        }

        private static string CleanExecutablePath(string rawPath)
        {
            string path = rawPath;

            // 处理带引号的路径
            if (path.StartsWith("\""))
            {
                int endQuote = path.IndexOf('"', 1);
                if (endQuote > 0)
                {
                    path = path.Substring(1, endQuote - 1);
                }
            }
            else
            {
                // 无引号时，取第一个空格前的部分
                int firstSpace = path.IndexOf(' ');
                if (firstSpace > 0)
                {
                    path = path.Substring(0, firstSpace);
                }
            }

            return path.Trim();
        }
    }
}
