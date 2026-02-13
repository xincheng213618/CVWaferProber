using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CVWaferProber.WinMessageCtrl
{
    public enum LoadLibraryFlags : uint
    {
        DONT_RESOLVE_DLL_REFERENCES = 0x00000001,

        LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010,

        LOAD_LIBRARY_AS_DATAFILE = 0x00000002,

        LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE = 0x00000040,

        LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020,

        LOAD_LIBRARY_SEARCH_APPLICATION_DIR = 0x00000200,

        LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000,

        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100,

        LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800,

        LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400,

        LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008
    }
    public abstract class BaseDynamicAPI
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BaseDynamicAPI));

        [DllImport("kernel32.dll", SetLastError = true)]
        protected static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, LoadLibraryFlags dwFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        protected static extern IntPtr GetProcAddress(IntPtr hModule, string lProcName);
        [DllImport("kernel32.dll", CallingConvention = CallingConvention.StdCall)]
        protected static extern bool FreeLibrary(IntPtr hModule);
        // 添加 DLL 所在目录到搜索路径
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);

        protected string m_sDllFileName;
        protected IntPtr m_hModule;

        public IntPtr LibModule { get => m_hModule; }

        public virtual bool LoadDllLib(string dllFileName)
        {
            IntPtr func = IntPtr.Zero;
            m_sDllFileName = dllFileName;
            PrintDllVersion(dllFileName);
            // 使用前调用
            SetDllDirectory(Path.GetDirectoryName(dllFileName));
            m_hModule = LoadLibraryEx(dllFileName, IntPtr.Zero, LoadLibraryFlags.LOAD_WITH_ALTERED_SEARCH_PATH);
            if (m_hModule == IntPtr.Zero)
            {
                //var err = Marshal.GetLastWin32Error(); //只有SetLastError = true时，才能获取到Error Code
                //if(logger.IsErrorEnabled) logger.ErrorFormat("Load dll error.{1} => {0}", dllFileName, err);
                return false;
            }
            return InitFunc();
        }

        public static void PrintDllVersion(string dllFileName)
        {
            if (logger.IsInfoEnabled)
            {
                if (File.Exists(dllFileName)) logger.InfoFormat("Load Lib Dll => {0}/{1}\r\n{2}", Path.GetFileName(dllFileName), new FileInfo(dllFileName).LastWriteTime.ToString("yyyy-MM-dd"), FileVersionInfo.GetVersionInfo(dllFileName));
                else logger.ErrorFormat("Lib Dll not Exists!!!=>{0}", dllFileName);
            }
        }


        protected abstract bool InitFunc();

        public bool InitFuncMan()
        {
            return InitFunc();
        }


        protected bool InitFunc_Address<T>(string procName, ref T funcPoint) where T : class
        {
            IntPtr func = IntPtr.Zero;
            func = GetProcAddress(m_hModule, procName);
            if (func == IntPtr.Zero)
            {
                var err = Marshal.GetLastWin32Error(); //只有SetLastError = true时，才能获取到Error Code
                logger.ErrorFormat("Get Function API {0} error.{1}", procName, err);
                //Dispose();
                return false;
            }

            Type t = typeof(T);
            funcPoint = Marshal.GetDelegateForFunctionPointer(func, t) as T;
            return true;
        }

        public void Dispose()
        {
            if (m_hModule != IntPtr.Zero)
            {
                logger.InfoFormat("Dispose Dll => {0}", m_sDllFileName);
                FreeLibrary(m_hModule);
            }
        }
    }
}
