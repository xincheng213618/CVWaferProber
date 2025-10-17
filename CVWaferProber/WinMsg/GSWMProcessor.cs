using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using System.IO;
using System.Windows;

namespace CVWaferProber.WinMsg
{
    public static class GSMessages
    {
        //切换配方
        public const int WM_GS_Tester_Select_Spec_2018 = WindowMessageProcessor.Messages.WM_USER + 2018;
        //读取mapping
        public const int WM_GS_Read_MappingData_2021 = WindowMessageProcessor.Messages.WM_USER + 2021;
        //结束测试
        public const int WM_STOP_GS_TEST_2003 = WindowMessageProcessor.Messages.WM_USER + 2003;
        //结束测试
        public const int WM_GS_SOT_MULTI_SITE_2015 = WindowMessageProcessor.Messages.WM_USER + 2015;
        //通知对方重新获取句柄
        public const int WM_GS_NewHwnd_2016 = WindowMessageProcessor.Messages.WM_USER + 2016;

        public const int WM_GS_SOT = WindowMessageProcessor.Messages.WM_USER + 2000;		//单芯START信号
        public const int WM_GS_EOT = WindowMessageProcessor.Messages.WM_USER + 2001;		//EOT信号
        public const int WM_START_GS_TEST = WindowMessageProcessor.Messages.WM_USER + 2002;		//测试开始
        public const int WM_STOP_GS_TEST = WindowMessageProcessor.Messages.WM_USER + 2003;		//结束测试
        public const int WM_CLEAR_DATA_GS = WindowMessageProcessor.Messages.WM_USER + 2004; 		//清空测试数据

        public const int WM_EXIT_GS = WindowMessageProcessor.Messages.WM_USER + 2005;		//退出测试机
        public const int WM_SET_GS_TESTER_USER = WindowMessageProcessor.Messages.WM_USER + 2006;		//设置当前用户
        public const int WM_GS_SEND_WAFER_ID = WindowMessageProcessor.Messages.WM_USER + 2007;		//发送芯片编号
        public const int WM_GS_OPERATE_PRODUCT_FILE = WindowMessageProcessor.Messages.WM_USER + 2008;		//同步产品档
        public const int WM_GS_SHOW_WINDOWN_DLG = WindowMessageProcessor.Messages.WM_USER + 2009;		//显示窗口
        public const int WM_GS_PROBER_SHOW = WindowMessageProcessor.Messages.WM_USER + 2010;		//显示探针台窗口
        public const int WM_GS_SET_DIEPOS = WindowMessageProcessor.Messages.WM_USER + 2011;		//设置背景坐标

        public const int WM_GS_SET_ESD_SPEC = WindowMessageProcessor.Messages.WM_USER + 2012;     //设置环内外打点模式
        public const int WM_SET_GS_TEST_CONDITION = WindowMessageProcessor.Messages.WM_USER + 2013;		//改变测试条件

        public const int WM_GS_EOS = WindowMessageProcessor.Messages.WM_USER + 2014;		//测试机通知探针台测试结束
        public const int WM_GS_SOT_MULTI_SITE = WindowMessageProcessor.Messages.WM_USER + 2015;		//多芯START信号，P→T
        public const int WM_GS_NewHwnd = WindowMessageProcessor.Messages.WM_USER + 2016;       //软件重启通知对方获取新句柄
        public const int WM_GS_Move_Pos = WindowMessageProcessor.Messages.WM_USER + 2017;       //测试机控制探针台移动
        public const int WM_GS_Tester_Select_Spec = WindowMessageProcessor.Messages.WM_USER + 2018;       //探针台通知测试机选择产品档
        public const int WM_GS_MULTISITE_CALIB = WindowMessageProcessor.Messages.WM_USER + 2019;       //探针台通知测试机读取AlarmKey的状态
        public const int WM_GS_SCAN_START = WindowMessageProcessor.Messages.WM_USER + 2020;       //探针台通知测试机扫描开始
        public const int WM_GS_Read_MappingData = WindowMessageProcessor.Messages.WM_USER + 2021;       // 扫描后通知测试机读取mapping资料
        public const int WM_GS_SET_OutSideRingTest = WindowMessageProcessor.Messages.WM_USER + 2022;       //设置外圈规格
        public const int WM_GS_SET_InSideRingTest = WindowMessageProcessor.Messages.WM_USER + 2023;       //设置外圈规格
        public const int WM_GS_Move_Pos_ZUp = WindowMessageProcessor.Messages.WM_USER + 2024;       //测试机控制探针台移动Zup
        public const int WM_GS_Move_Pos_Zdown = WindowMessageProcessor.Messages.WM_USER + 2025;       //测试机控制探针台移动Zdown  
        public const int WM_GS_Move_Pos_XY = WindowMessageProcessor.Messages.WM_USER + 2026;       //测试机控制探针台移动Zdown 

        public const int WM_GS_Dev_Ready = WindowMessageProcessor.Messages.WM_USER + 2032;       //测试机Ready
        public const int WM_GS_Dev_Stoped = WindowMessageProcessor.Messages.WM_USER + 2033;       //测试机Stoped
    }
    
    public static class GSCommConstFile
    {
        public const string txtName_GS_Event_INDEX = "C:\\Communication\\GS_Event_INDEX.txt";
        public const string txtName_GS_MULTI_SITE_INFO = "C:\\Communication\\GS_MULTI_SITE_INFO.txt";

        public const string txtName_TESTER_BIN_VALUE = "C:\\Communication\\TESTER_BIN_VALUE.txt";
        public const string txtName_WAFER_INFO_TO_GS_TESTER = "C:\\Communication\\WAFER_INFO_TO_GS_TESTER.txt";
        public const string txtName_RecipeName = "C:\\Communication\\RecipeName.txt";
    }
    public class GSWMProcessor
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(GSWMProcessor));


        private WindowMessageProcessor _messageProcessor;
        private Window _parentWindow;
        private GSCommDynamicAPI? _gsCommDynamicAPI;
        private MeasurementInfo? curMeasurementInfo;

        public delegate void StopTestHandler(object sender);
        public delegate void SOTHandler(object sender,int row,int col);
        public event StopTestHandler? OnStopTest;
        public event SOTHandler? OnSOT;


        public GSWMProcessor(Window window)
        {
            this._messageProcessor = new WindowMessageProcessor();
            _messageProcessor.Initialized += OnMessageProcessorInitialized;
            Initialize(window);
        }

        public void Initialize(Window window)
        {
            this._parentWindow = window;
            _messageProcessor.Initialize(window);
            //
            InitGSAPI();
        }
        private void InitGSAPI()
        {
            _gsCommDynamicAPI = GSCommDynamicAPI.Load();
            if (_gsCommDynamicAPI != null)
            {
                int iR = _gsCommDynamicAPI.GS_InitDll(false);
                iR = _gsCommDynamicAPI.GS_GetProberWnd();
                iR = GS_SendWMcmdToProber(GSMessages.WM_GS_NewHwnd_2016,0,0);
            }
        }
        private void OnMessageProcessorInitialized(object? sender, EventArgs e)
        {
            // 注册自定义消息处理器
            _messageProcessor.RegisterCustomMessageHandler(GSMessages.WM_GS_Read_MappingData_2021, Handle_GS_ReadMapping_2021);
            _messageProcessor.RegisterCustomMessageHandler(GSMessages.WM_STOP_GS_TEST_2003, Handle_GS_Stop_2003);
            _messageProcessor.RegisterCustomMessageHandler(GSMessages.WM_GS_SOT_MULTI_SITE_2015, Handle_GS_Start_2015);
            _messageProcessor.RegisterCustomMessageHandler(GSMessages.WM_GS_Tester_Select_Spec_2018, Handle_GS_Tester_Select_Spec_2018);

            // 注册系统消息处理器
            _messageProcessor.RegisterSystemMessageHandlers(_parentWindow);
        }

        private void StartRecvMsg()
        {
            _messageProcessor.StartRecvMsg();
        }
        private void StopRecvMsg()
        {
            _messageProcessor.StopRecvMsg();
        }

        private IntPtr Handle_GS_ReadMapping_2021(IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if(logger.IsInfoEnabled) { logger.Info("Recv WM_GS_Read_MappingData"); }
            _parentWindow.Dispatcher.BeginInvoke(new Action(() =>
            {
                GS_SetEventStatus(21, 1);
            }));
            handled = true;
            return (IntPtr)1;
        }
        private IntPtr Handle_GS_Stop_2003(IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (logger.IsInfoEnabled) { logger.Info("Recv WM_STOP_GS_TEST"); }
            _parentWindow.Dispatcher.BeginInvoke(new Action(() =>
            {
                OnStopTest?.Invoke(this);
            }));
            handled = true;
            return (IntPtr)1;
        }
        private IntPtr Handle_GS_Start_2015(IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (logger.IsInfoEnabled) { logger.Info("Recv WM_GS_SOT_MULTI_SITE"); }
            _parentWindow.Dispatcher.BeginInvoke(new Action(() =>
            {
                BeginTestDie();
            }));
            handled = true;
            return (IntPtr)1;
        }
        private IntPtr Handle_GS_Tester_Select_Spec_2018(IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (logger.IsInfoEnabled) { logger.Info("Recv WM_GS_Tester_Select_Spec"); }
            _parentWindow.Dispatcher.BeginInvoke(new Action(() =>
            {
                GS_SetEventStatus(15, 1);
            }));
            handled = true;
            return (IntPtr)1;
        }

        private void BeginTestDie()
        {
            int nRow = int.MaxValue;
            int nCol = int.MaxValue;
            GetRowCol(ref nRow,ref nCol);
            GS_SetEventStatus(12, 1);
            OnSOT?.Invoke(this, nRow, nCol);
        }
        private bool _isFromAPI = false;
        private bool GetRowCol(ref int nRow, ref int nCol)
        {
           if(_isFromAPI) return GetRowColFromAPI(ref nRow, ref nCol);
           else return GetRowColFromFile(ref nRow, ref nCol);
        }

        private bool GetRowColFromAPI(ref int nRow, ref int nCol)
        {
            int Count = 0;
            GS_MULTI_SITE_INFO[] pBuffer = new GS_MULTI_SITE_INFO[1];
            pBuffer[0].nRow = int.MaxValue;
            pBuffer[0].nCol = int.MaxValue;
            int reuslt = GS_GetMultiSiteSOT(pBuffer, ref Count); //获取坐标信息
            nRow = pBuffer[0].nRow;
            nCol = pBuffer[0].nCol;
            return true;
        }
        private bool GetRowColFromFile(ref int nRow, ref int nCol)
        {
            // 读本地的文件坐标
            string fileName = GSCommConstFile.txtName_GS_MULTI_SITE_INFO;

            if (File.Exists(fileName))
            {
                string str1 = File.ReadAllText(fileName);
                string[] xypath = str1.Split(',');
                nRow = int.Parse(xypath[0]);
                nCol = int.Parse(xypath[1]);
                return true;
            }

            return false;
        }

        private void GS_SetEventStatus(int nIndex, int nStatus)
        {
            if (_gsCommDynamicAPI != null)
            {
                _gsCommDynamicAPI.GS_SetEventStatus(nIndex, nStatus);
            }
        }
        public int GS_SendWMcmdToProber(int WMcmd, int Lparas, int Rparsa)
        {
            if (_gsCommDynamicAPI != null)
            {
                int iR = _gsCommDynamicAPI.GS_SendWMcmdToProber(/*WindowMessageProcessor.Messages.WM_USER +*/ WMcmd, Lparas, Rparsa);
                return iR;
            }

            return -1;
        }
        private int GS_GetMultiSiteSOT(GS_MULTI_SITE_INFO[] pBuffer, ref int count)
        {
            if (_gsCommDynamicAPI != null)
            {
                return _gsCommDynamicAPI.GS_GetMultiSiteSOT(pBuffer, ref count);
            }

            return -1;
        }
        private int GS_GetEventStatus(int nIndex)
        {
            if (_gsCommDynamicAPI != null)
            {
                return _gsCommDynamicAPI.GS_GetEventStatus(nIndex);
            }

            return -1;
        }

        private int GS_SendEOT()
        {
            if (_gsCommDynamicAPI != null)
            {
                return _gsCommDynamicAPI.GS_SendEOT();
            }
            return -1;
        }

        private int GS_SetTestBinValue(int nNeedleNum, TESTER_BIN_VALUE[] lpstuTesterBinValue)
        {
            if (_gsCommDynamicAPI != null)
            {
                return _gsCommDynamicAPI.GS_SetTestBinValue(nNeedleNum, lpstuTesterBinValue);
            }
            return -1;
        }

        public void MeasurementReady()
        {
            StartRecvMsg();
            if (logger.IsInfoEnabled) logger.Info("Auto Measurement Ready");
            curMeasurementInfo = new MeasurementInfo() { StartDT = DateTime.Now, };
            GS_SendWMcmdToProber(GSMessages.WM_GS_Dev_Ready, 0, 0);
        }
        public void MeasurementStoped()
        {
            if (curMeasurementInfo == null)
            {
                if (logger.IsWarnEnabled) logger.Warn("Auto Measurement not ready");
                return;
            }

            StopRecvMsg();
            curMeasurementInfo.EndDT = DateTime.Now;
            var totalTM = curMeasurementInfo.EndDT - curMeasurementInfo.StartDT;
            GS_SendWMcmdToProber(GSMessages.WM_GS_Dev_Stoped, 0, 0);
            if (logger.IsInfoEnabled) logger.InfoFormat("Measurement Stoped => {0}", totalTM.ToString());
            curMeasurementInfo = null;
        }
        public void MeasurementProcessResult(ChipStatus status, int nRow,int nCol)
        {
            if (curMeasurementInfo == null)
            {
                if (logger.IsWarnEnabled) logger.Warn("Auto Measurement not ready");
                return;
            }
            // 将Mapping结果放回给上位机
            TESTER_BIN_VALUE[] lpstu_cur = new TESTER_BIN_VALUE[1];
            lpstu_cur[0].nRow = nRow;
            lpstu_cur[0].nCol = nCol;
            if (ChipStatus.OK == status)
            {
                lpstu_cur[0].nBin = 1; // 绿色
                if (logger.IsInfoEnabled) logger.Info("WM ===> Mapping : OK/Green");
            }
            else
            {
                lpstu_cur[0].nBin = 2; // 数据定位失败、数据提取失败、切图失败 红色
                if (logger.IsInfoEnabled) logger.Info("WM ===> Mapping: NG/Red");
            }
            GS_SetTestBinValue(1, lpstu_cur);
            //Logging("EOT前");

            Logging("GS_SetEventStatus(17,0)");
            GS_SetEventStatus(17, 0);
            Logging("GS_SendEOT");
            GS_SendEOT();
            Logging("GS_GetEventStatus(17)");
            if (1 == GS_GetEventStatus(17))
            {
                if (logger.IsInfoEnabled) logger.Info("GetEventStatus17成功");
            }
        }
        public void MeasurementProcessResult(DieViewModel die)
        {
            if (die.Status.HasValue && die.MapX.HasValue && die.MapY.HasValue)
            {
                MeasurementProcessResult(die.Status.Value, die.MapY.Value, die.MapX.Value);
            }
        }

        private void Logging(string message, int type = 0)
        {
            if(logger.IsDebugEnabled) logger.Debug(message);
        }

        public class MeasurementInfo
        {
            public DateTime StartDT { get; set; }
            public int? Row { get; set; }
            public int? Col { get; set; }
            public DateTime? EndDT { get; set; }
        }
    }
}
