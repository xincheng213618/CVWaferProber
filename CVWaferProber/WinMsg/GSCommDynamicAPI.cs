using System.Text;

namespace CVWaferProber.WinMsg
{
    public class GSDLLAPI
    {

        public const string LIBRARY_DllName = "GSCommunication.dll";

//        [DllImport(LIBRARY_JinYuan, EntryPoint = "GS_InitDll",
//     CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
//        public unsafe static extern int GS_InitDll(bool isProber);

//        [DllImport(LIBRARY_JinYuan, EntryPoint = "GS_GetProberWnd",
//CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
//        public unsafe static extern int GS_GetProberWnd();

//        [DllImport(LIBRARY_JinYuan, EntryPoint = "GS_SendWMcmdToProber",
//CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
//        public unsafe static extern int GS_SendWMcmdToProber(int WMcmd, int Lparas, int Rparsa);

//        [DllImport(LIBRARY_JinYuan, EntryPoint = "GS_SetEventStatus",
//CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
//        public unsafe static extern void GS_SetEventStatus(int nIndex, int nStatus);

//        [DllImport(LIBRARY_JinYuan, EntryPoint = "GS_GetEventStatus",
//CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
//        public unsafe static extern int GS_GetEventStatus(int nIndex);

//        [DllImport(LIBRARY_JinYuan, EntryPoint = "GS_GetMultiSiteSOT",
//CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
//        public unsafe static extern int GS_GetMultiSiteSOT(GS_MULTI_SITE_INFO[] pBuffer, ref int nCount);

//        [DllImport(LIBRARY_JinYuan, EntryPoint = "GS_GetRecipe",
//CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
//        public unsafe static extern int GS_GetRecipe(StringBuilder chr, ref int count);

//        [DllImport(LIBRARY_JinYuan, EntryPoint = "GS_SetTestBinValue",
//CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
//        public unsafe static extern int GS_SetTestBinValue(int nNeedleNum, TESTER_BIN_VALUE[] lpstuTesterBinValue);

//        [DllImport(LIBRARY_JinYuan, EntryPoint = "GS_SendEOT",
//CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
//        public unsafe static extern int GS_SendEOT();

    }
    /// <summary>
    /// 多晶测试 BIN  信息
    /// </summary>
    public struct GS_MULTI_SITE_INFO
    {
        public int nRow;                       //当前Site的行列值
        public int nCol;
        public int nDrawRow;                   //当前Site在Mapping上的显示行列值
        public int nDrawCol;
        public int nPixelRate;                 //环外抽测环内全测的Mapping缩放倍率
        public int nDiePosition;               //点在芯片上的位置

        public int nIndex;                     //当前Site在Mapping中的索引

        public int nSite;
        public int bTestOptics;               //是否测试光参数
    };

    public struct TESTER_BIN_VALUE
    {
        public int nRow;
        public int nCol;
        public int nBin;
    };
    public class GSCommDynamicAPI : BaseDynamicAPI
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(GSCommDynamicAPI));

        public delegate int LPGS_InitDll(bool isProber);
        private LPGS_InitDll? pGS_InitDll;

        public delegate int LPGS_GetProberWnd();
        private LPGS_GetProberWnd? pGS_GetProberWnd;

        public delegate int LPGS_SendWMcmdToProber(int WMcmd, int Lparas, int Rparsa);
        private LPGS_SendWMcmdToProber? pGS_SendWMcmdToProber;

        public delegate void LPGS_SetEventStatus(int nIndex, int nStatus);
        private LPGS_SetEventStatus? pGS_SetEventStatus;

        public delegate int LPGS_GetEventStatus(int nIndex);
        private LPGS_GetEventStatus? pGS_GetEventStatus;

        public delegate int LPGS_GetMultiSiteSOT(GS_MULTI_SITE_INFO[] pBuffer, ref int nCount);
        private LPGS_GetMultiSiteSOT? pGS_GetMultiSiteSOT;

        public delegate int LPGS_GetRecipe(StringBuilder chr, ref int count);
        private LPGS_GetRecipe? pGS_GetRecipe;

        public delegate int LPGS_SetTestBinValue(int nNeedleNum, TESTER_BIN_VALUE[] lpstuTesterBinValue);
        private LPGS_SetTestBinValue? pGS_SetTestBinValue;

        public delegate int LPGS_SendEOT();
        private LPGS_SendEOT? pGS_SendEOT;

        public static GSCommDynamicAPI? Load()
        {
            string dllFile = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string MainAppPath = System.IO.Path.GetDirectoryName(dllFile);
            return Load(MainAppPath);
        }
        public static GSCommDynamicAPI? Load(string MainAppPath)
        {
            GSCommDynamicAPI? api = null;
            string TPDllPath = System.IO.Path.Combine(MainAppPath, GSDLLAPI.LIBRARY_DllName);
            if (System.IO.File.Exists(TPDllPath))
            {
                try
                {
                    api = new GSCommDynamicAPI(TPDllPath);
                }catch (Exception ex)
                {

                }
            }
            else logger.ErrorFormat("GSComm dll not exist. => {0}", TPDllPath);
            return api;
        }
        public GSCommDynamicAPI(string DllPath)
        {
            if (!this.LoadDllLib(DllPath)) { if (logger.IsErrorEnabled) logger.ErrorFormat("GSComm dll load failed. => {0}", DllPath); throw new Exception("API Dll load failed"); }
            else { if (logger.IsInfoEnabled) logger.InfoFormat("GSComm dll load ok. => {0}", DllPath); }
        }
        public GSCommDynamicAPI() : this(IntPtr.Zero) { }
        public GSCommDynamicAPI(IntPtr hModule)
        {
            this.m_hModule = hModule;
        }
        protected override bool InitFunc()
        {
            bool ret = true;
            ret = ret && InitFunc_Address("GS_InitDll", ref pGS_InitDll);
            ret = ret && InitFunc_Address("GS_GetProberWnd", ref pGS_GetProberWnd);
            ret = ret && InitFunc_Address("GS_SendWMcmdToProber", ref pGS_SendWMcmdToProber);
            ret = ret && InitFunc_Address("GS_SetEventStatus", ref pGS_SetEventStatus);
            ret = ret && InitFunc_Address("GS_GetEventStatus", ref pGS_GetEventStatus);
            ret = ret && InitFunc_Address("GS_GetMultiSiteSOT", ref pGS_GetMultiSiteSOT);
            ret = ret && InitFunc_Address("GS_GetRecipe", ref pGS_GetRecipe);
            ret = ret && InitFunc_Address("GS_SetTestBinValue", ref pGS_SetTestBinValue);
            ret = ret && InitFunc_Address("GS_SendEOT", ref pGS_SendEOT);

            return ret;
        }

        public int GS_InitDll(bool isProber)
        {
            if (pGS_InitDll != null) return pGS_InitDll(isProber);
            logger.Error("GS_InitDll func address is null.");
            throw new NotImplementedException();
        } 
        public int GS_GetProberWnd()
        {
            if (pGS_GetProberWnd != null) return pGS_GetProberWnd();
            logger.Error("GS_GetProberWnd func address is null.");
            throw new NotImplementedException();
        } 
        public int GS_SendWMcmdToProber(int WMcmd, int Lparas, int Rparsa)
        {
            if (pGS_SendWMcmdToProber != null) return pGS_SendWMcmdToProber(WMcmd, Lparas, Rparsa);
            logger.Error("GS_SendWMcmdToProber func address is null.");
            throw new NotImplementedException();
        }
        public void GS_SetEventStatus(int nIndex, int nStatus)
        {
            if (pGS_SetEventStatus != null)
            {
                pGS_SetEventStatus(nIndex, nStatus);
                return;
            }
            logger.Error("GS_SetEventStatus func address is null.");
            throw new NotImplementedException();
        }
        public int GS_GetEventStatus(int nIndex)
        {
            if (pGS_GetEventStatus != null) return pGS_GetEventStatus(nIndex);
            logger.Error("GS_GetEventStatus func address is null.");
            throw new NotImplementedException();
        }

        public int GS_GetMultiSiteSOT(GS_MULTI_SITE_INFO[] pBuffer, ref int count)
        {
            if (pGS_GetMultiSiteSOT != null) return pGS_GetMultiSiteSOT(pBuffer,ref count);
            logger.Error("GS_GetMultiSiteSOT func address is null.");
            throw new NotImplementedException();
        }

        public int GS_SendEOT()
        {
            if (pGS_SendEOT != null) return pGS_SendEOT();
            logger.Error("GS_SendEOT func address is null.");
            throw new NotImplementedException();
        }
        public int GS_SetTestBinValue(int nNeedleNum, TESTER_BIN_VALUE[] lpstuTesterBinValue)
        {
            if (pGS_SetTestBinValue != null) return pGS_SetTestBinValue(nNeedleNum, lpstuTesterBinValue);
            logger.Error("GS_SetTestBinValue func address is null.");
            throw new NotImplementedException();
        }
    }
}
