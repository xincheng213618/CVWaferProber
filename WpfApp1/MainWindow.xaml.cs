using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace WpfApp1
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public enum AliResult
        {
            Success = 0,
            Error_InvalidHandle = -1,
            Error_InvalidJson = -2,
            Error_CalcFailed = -3,
            Error_Length = -4
        }

        private const string LIBRARY_CV_Ali = "CV_algorithm.dll";
        // 导入CV_algorithm.dll的核心接口
        [DllImport(LIBRARY_CV_Ali, EntryPoint = "CV_Ali_calcSingle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]

        public static extern AliResult CV_Ali_calcSingle(
            IntPtr handle,                // 句柄（若无需句柄可传IntPtr.Zero，需确认dll要求）
             string staticJson,  // 输入JSON字符串
             StringBuilder result, // 输出结果缓冲区
            ref int resultLength          // 缓冲区长度（输入：缓冲区大小；输出：实际结果长度）
        );
        public static AliResult CV_Ali_calcSingle(string staticJson, out string result)
        {
            // 初始缓冲区长度（可根据实际情况调整）
            int length = 512;
            StringBuilder bf = new StringBuilder(length);
            var res = CV_Ali_calcSingle(IntPtr.Zero, staticJson, bf, ref length);

            // 如果返回长度不足错误，扩容后重新调用
            if (res == AliResult.Error_Length)
            {
                bf = new StringBuilder(length);
                res = CV_Ali_calcSingle(IntPtr.Zero, staticJson, bf, ref length);
            }

            result = bf.ToString();
            return res;
        }
    

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // 1. 构建输入JSON参数（严格按照接口文档）
            var inputParams = new
            {
                type = 0,
                Optics = new
                {
                    cie_x = Math.Round(0,05, 6),  // 限制小数位数，避免精度问题
                    cie_y = Math.Round(0.05, 6)
                }
            };

            string inputJson = JsonConvert.SerializeObject(inputParams);

            // 2. 使用封装后的方法调用（核心修改）
            string resultJson;
            AliResult result = CV_Ali_calcSingle(inputJson, out resultJson);

        }
    }
}