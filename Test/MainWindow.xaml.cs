using Newtonsoft.Json;
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

namespace Test
{


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public enum CV_AliResType : int
        {
            /*        算法整体返回值说明：*/
            SUCCESS = 1,            //完全成功;
            FAILED = 0,             //失败;
            PART_SUCCESS = 2,       //部分成功（如计算不同类型的畸变）;
            ERR_LENGTH = -1,        //接收的内存长度不够;
            ERR_FILE = -2,          //结果存文件失败;
            ERR_JSON = -3           //JSON格式异常
        };


        private const string LIBRARY_CV_Ali = "CV_algorithm.dll";
        // 导入CV_algorithm.dll的核心接口
        [DllImport(LIBRARY_CV_Ali, EntryPoint = "CV_Ali_calcSingle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]

        public static extern CV_AliResType CV_Ali_calcSingle(
            IntPtr handle,                // 句柄（若无需句柄可传IntPtr.Zero，需确认dll要求）
            [MarshalAs(UnmanagedType.LPStr)] string staticJson,  // 输入JSON字符串
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder result, // 输出结果缓冲区
            ref int resultLength          // 缓冲区长度（输入：缓冲区大小；输出：实际结果长度）
        );
        public static CV_AliResType CV_Ali_calcSingle(string staticJson, out string result)
        {
            // 初始缓冲区长度（可根据实际情况调整）
            int length = 1025;
            StringBuilder bf = new StringBuilder(length);
            var res = CV_Ali_calcSingle(IntPtr.Zero, staticJson, bf, ref length);

            result = bf.ToString();
            return res;
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private class ExcitationPurityResult
        {
            public PurityResultDetail result { get; set; }
        }

        private class PurityResultDetail
        {
            [JsonProperty("ExcitationPurity")]
            public double? ExcitationPurity { get; set; }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            double cieX = 0.5; // 示例值，实际使用时应根据需要设置
            double cieY = 0.5;
            // 1. 构建输入JSON参数（严格按照接口文档）
            var inputParams = new
            {
                type = 0,
                Optics = new
                {
                    cie_x = Math.Round(cieX, 6),  // 限制小数位数，避免精度问题
                    cie_y = Math.Round(cieY, 6)
                }
            };

            string inputJson = JsonConvert.SerializeObject(inputParams);

            // 2. 使用封装后的方法调用（核心修改）
            string resultJson;
            CV_AliResType result = CV_Ali_calcSingle(inputJson, out resultJson);
            MessageBox.Show($"调用结果: {result}\n返回JSON: {resultJson}");

            double excitationPurity;

            // 3. 处理调用结果
            if (result ==CV_AliResType.SUCCESS)
            {

                // 按照接口文档的格式解析JSON
                var purityResult = JsonConvert.DeserializeObject<ExcitationPurityResult>(resultJson);

                if (purityResult?.result?.ExcitationPurity != null)
                {
                    excitationPurity = purityResult.result.ExcitationPurity.Value;
                }
            }
            else
            {
            }
        }
    }
}