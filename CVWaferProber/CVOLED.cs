#pragma warning disable

using System.Runtime.InteropServices;
using System.Text;
using static CVWaferProber.Services.CVAlgorithmNative;

namespace CVWaferProber
{
    public class CVOLED
    {
        private const string LIBRARY_CVCAMERA = "cvOled.dll";
        //打开
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findParticlesBlackMemForRebuildPicImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int findParticlesBlackMemForRebuildPicImp(int w, int h, byte[] imgdata, int type, string cfg_json, string mask_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findPixelDefectsForRebuildPicGradingMethod2MemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int findPixelDefectsForRebuildPicGradingMethod2MemImp(int w, int h, byte[] imgdata, int type, string cfg_json, string path, string result_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "detectBrightPixelsForBlackScreenMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int detectBrightPixelsForBlackScreenMemImp(int w, int h, byte[] imgdata, int type, string sn, string cfg_json, string path, string result_path);



    }
}


