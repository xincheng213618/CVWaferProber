using log4net;
using log4net.Config;
using System.Reflection;
using System.Windows;

namespace CVWaferProber
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));
        protected override void OnStartup(StartupEventArgs e)
        {
            // 初始化 log4net
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());

            // 使用 App.config 配置
            //XmlConfigurator.Configure(logRepository);

            // 或者使用单独的配置文件
            var configFile = new System.IO.FileInfo("log4net.config");
            XmlConfigurator.Configure(logRepository, configFile);

           

            log.Info("Application starting...");

            base.OnStartup(e);
            // 初始化语言（读取Settings中的默认语言）
            AppSettingsManager.InitializeLanguage();
        }
    }

}
