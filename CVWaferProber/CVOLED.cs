#pragma warning disable

using System;
using System.Runtime.InteropServices;

namespace CVWaferProber
{
    public static class CVOLED
    {
        private const string LIBRARY_CVCAMERA = "cvOled.dll";

        #region Enums / Native aliases

        // 如果你项目里已有定义，可删除这里，改为使用项目现有枚举。
        public enum CVOLED_COLOR : int
        {
            Unknown = 0,
            R = 1,
            G = 2,
            B = 3
        }

        // 若项目里已有 CVOLED_ERROR，请删除这里并使用现有定义。
        public enum CVOLED_ERROR : int
        {
            Unknown = 0
        }

        #endregion

        #region Pixel positioning

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findDotsArrayImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findDotsArrayImp(
            string img_path,
            string cfg_json,
            string position_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findDotsArrayMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findDotsArrayMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            CVOLED_COLOR color,
            string cfg_json,
            string position_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findDotsArrayMemReturnMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findDotsArrayMemReturnMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            CVOLED_COLOR color,
            string cfg_json,
            ref int col,
            ref int row,
            ref float x_interval,
            ref float y_interval,
            ref float angle,
            [Out] double[] pX,
            [Out] double[] pY);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findDotsArrayMemReturnMemWithPositionImgImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findDotsArrayMemReturnMemWithPositionImgImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            CVOLED_COLOR color,
            string cfg_json,
            string input_json,
            ref int col,
            ref int row,
            ref float x_interval,
            ref float y_interval,
            ref float angle,
            [Out] double[] pX,
            [Out] double[] pY);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findDotsArrayByCornerPtsImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findDotsArrayByCornerPtsImp(
            string img_path,
            string cfg_json,
            string position_path,
            [Out] float[] pX,
            [Out] float[] pY);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findDotsArrayByCornerPtsMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findDotsArrayByCornerPtsMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            CVOLED_COLOR color,
            string cfg_json,
            string position_path,
            [Out] float[] pX,
            [Out] float[] pY);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findDotsArrayByCornerPtsMemReturnMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findDotsArrayByCornerPtsMemReturnMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            CVOLED_COLOR color,
            string cfg_json,
            [Out] float[] cX,
            [Out] float[] cY,
            ref int col,
            ref int row,
            ref float x_interval,
            ref float y_interval,
            ref float angle,
            [Out] double[] pX,
            [Out] double[] pY);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findDotsArrayByCornerPtsMemWithPositionImgImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findDotsArrayByCornerPtsMemWithPositionImgImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            CVOLED_COLOR color,
            string cfg_json,
            string input_json,
            [Out] float[] cX,
            [Out] float[] cY,
            ref int col,
            ref int row,
            ref float x_interval,
            ref float y_interval,
            ref float angle,
            [Out] double[] pX,
            [Out] double[] pY);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findDotsArrayLowPitchImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findDotsArrayLowPitchImp(
            string img_path,
            string cfg_json,
            string position_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findDotsArrayLowPitchMemPtsMemReturnMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findDotsArrayLowPitchMemPtsMemReturnMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            CVOLED_COLOR color,
            string cfg_json,
            ref int col,
            ref int row,
            ref float x_interval,
            ref float y_interval,
            ref float angle,
            [Out] double[] pX,
            [Out] double[] pY);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findBluePositionsByRed", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findBluePositionsByRed(
            string img_path,
            string cfg_json,
            string position_path_r);

        #endregion

        #region Rebuild pixels

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "rebuildPixelsImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR rebuildPixelsImp(
            string img_path,
            string cfg_json,
            string position_path,
            string result_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "rebuildPixelsMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR rebuildPixelsMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            CVOLED_COLOR color,
            string cfg_json,
            string position_path,
            ref uint res_w,
            ref uint res_h,
            [Out] byte[] resdata);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "rebuildPixelsWhithRadiusMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR rebuildPixelsWhithRadiusMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            CVOLED_COLOR color,
            string cfg_json,
            float radius_factor,
            string position_path,
            ref uint res_w,
            ref uint res_h,
            [Out] byte[] resdata);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "rebuildPixelsByAAMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR rebuildPixelsByAAMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            CVOLED_COLOR color,
            string cfg_json,
            ref uint res_w,
            ref uint res_h,
            [Out] byte[] resdata);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "rebuildPixelsPosMemReturnMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR rebuildPixelsPosMemReturnMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            CVOLED_COLOR color,
            string cfg_json,
            int col,
            int row,
            float x_interval,
            float y_interval,
            float angle,
            double[] pX,
            double[] pY,
            ref uint res_w,
            ref uint res_h,
            [Out] byte[] resdata);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "rebuildPixelsWithRadiusFactorPosMemReturnMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR rebuildPixelsWithRadiusFactorPosMemReturnMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            CVOLED_COLOR color,
            string cfg_json,
            float radius_factor,
            int col,
            int row,
            float x_interval,
            float y_interval,
            float angle,
            double[] pX,
            double[] pY,
            ref uint res_w,
            ref uint res_h,
            [Out] byte[] resdata);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "rebuildPixelsForCvcieImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR rebuildPixelsForCvcieImp(
            string img_path,
            string cfg_json,
            string position_path,
            string result_path);

        #endregion

        #region Image combination

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "combineSpacingDataImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR combineSpacingDataImp(
            string img_path_list,
            string cfg_json,
            string result_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "combineSpacingDataMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR combineSpacingDataMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            ulong len,
            int n_pics,
            int spacing_x,
            int spacing_y,
            ref uint w_comb,
            ref uint h_comb,
            [Out] byte[] rst);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "combineQuaterImagesMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR combineQuaterImagesMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            ulong len,
            [Out] byte[] rst);

        #endregion

        #region Pixel defects

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findPixelDefectsForRebuildPicImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findPixelDefectsForRebuildPicImp(
            string img_path,
            string cfg_json,
            string path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findPixelDefectsForRebuildPicMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findPixelDefectsForRebuildPicMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            CVOLED_COLOR color,
            string cfg_json,
            string path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findPixelDefectsForRebuildPicGradingImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findPixelDefectsForRebuildPicGradingImp(
            string img_path,
            string cfg_json,
            string path,
            string result_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findPixelDefectsForRebuildPicGradingMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findPixelDefectsForRebuildPicGradingMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            CVOLED_COLOR color,
            string cfg_json,
            string path,
            string result_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findPixelDefectsForRebuildPicGradingMethod2Imp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findPixelDefectsForRebuildPicGradingMethod2Imp(
            string img_path,
            string sn,
            string cfg_json,
            string path,
            string result_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findPixelDefectsForRebuildPicGradingMethod2MemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findPixelDefectsForRebuildPicGradingMethod2MemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            string sn,
            string cfg_json,
            string path,
            string result_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findPixelDefectsForQuardImgMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int findPixelDefectsForQuardImgMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            float th,
            int num_th,
            int index,
            string predix_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findPixelDefectsForQuardImgMemWithConfigImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int findPixelDefectsForQuardImgMemWithConfigImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            string cfg_json,
            string predix_path);

        #endregion

        #region Black screen bright pixels

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "detectBrightPixelsForBlackScreenImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR detectBrightPixelsForBlackScreenImp(
            string img_path,
            string sn,
            string cfg_json,
            string path,
            string result_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "detectBrightPixelsForBlackScreenMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR detectBrightPixelsForBlackScreenMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            string sn,
            string cfg_json,
            string path,
            string result_path);

        #endregion

        #region Particles / dust

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findParticlesForRebuildPicImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findParticlesForRebuildPicImp(
            string img_path,
            string cfg_json,
            string mask_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findParticlesBlackMemForRebuildPicImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findParticlesBlackMemForRebuildPicImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            string cfg_json,
            string mask_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findParticlesSelfCheckMemForRebuildPicImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findParticlesSelfCheckMemForRebuildPicImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            string cfg_json,
            string mask_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "fillParticlesImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR fillParticlesImp(
            string img_path,
            string cfg_json,
            string mask_path,
            string result_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "fillParticlesBlackMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR fillParticlesBlackMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            string cfg_json,
            string mask_path,
            [Out] byte[] des_data);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "fillParticlesSelfCheckMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR fillParticlesSelfCheckMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            string cfg_json,
            string mask_path,
            [Out] byte[] des_data);

        #endregion

        #region Mura / Line / VHLine / AOI

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findMuraImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findMuraImp(
            string img_path,
            string cfg_json,
            string result_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findMuraMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findMuraMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            string cfg_json,
            string result_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findLineImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findLineImp(
            string img_path,
            string cfg_path,
            string result_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findLineMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findLineMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            CVOLED_COLOR color,
            string cfg_path,
            string result_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findVHLineMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findVHLineMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            string cfg_json,
            string result_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "findVHLine1MemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR findVHLine1MemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int src_type,
            string cfg_json,
            string result_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "AOIDetectALLMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR AOIDetectALLMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            int src_type,
            string cfg_json,
            string dynamic_json,
            string result_json);

        #endregion

        #region Temporary / misc

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "interpPositionImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR interpPositionImp(
            string img_path,
            string position_path,
            string position_ext_path,
            int spacing_x,
            int spacing_y,
            int whole_w,
            int whole_h,
            int skip_col,
            int skip_row,
            CVOLED_COLOR color);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvRebuildLcdBlockImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR cvRebuildLcdBlockImp(
            string img_path,
            string position_path,
            string result_path,
            CVOLED_COLOR color);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "calDemuraCsv", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR calDemuraCsv(
            [In] string[] img_lists,
            int img_num,
            [Out] float[] outThs,
            [In] float[] gray_levels,
            [In] float[] exp);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "saveTifFromCsvImf", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int saveTifFromCsvImf(
            [MarshalAs(UnmanagedType.LPStr)] string csv_path,
            [MarshalAs(UnmanagedType.LPStr)] string tif_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "saveCvrawToTif", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int saveCvrawToTif(
            string cvraw_path,
            string tif_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "calVasByFile", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern void calVasByFile(
            string img_path,
            float exp,
            float cut_off,
            ref double v_jnd,
            ref double h_jnd);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvGetCaliItemPositionsByFile", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool cvGetCaliItemPositionsByFile(
            string img_path,
            float th,
            int min_w,
            int min_h);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvGetCaliItemPositions", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool cvGetCaliItemPositions(
            uint w,
            uint h,
            byte[] imgdata,
            int src_type,
            float th,
            int min_w,
            int min_h,
            [Out] float[] corner_pts_x,
            [Out] float[] corner_pts_y,
            ref int num,
            [Out] float[] c_x,
            [Out] float[] c_y,
            ref int c_num);

        #endregion

        #region Morie

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "MorieType2MaskGen", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int MorieType2MaskGen(
            uint w,
            uint h,
            int type,
            byte[] imgdata,
            int ref_blur_size,
            int pre_blur_size,
            double thh_lcr,
            double thh_mask_ratio,
            string mask_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "MorieFilterMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern CVOLED_ERROR MorieFilterMemImp(
            uint w,
            uint h,
            byte[] imgdata,
            string cfg_json,
            [Out] byte[] resultdata,
            int type,
            string des_path);

        #endregion

        #region BV WTC

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWtestImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern void bvWtestImp(
            string img_path,
            string cfg_json);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcInit", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcInit(
            string cfg_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcInitImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr bvWwtcInitImp(
            string cfg_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcRelease", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern void bvWwtcRelease();

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcReleaseImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern void bvWwtcReleaseImp(
            IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcLoadcfg", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcLoadcfg(
            string cfg_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcLoadcfgImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcLoadcfgImp(
            IntPtr handle,
            string cfg_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcGetFilter", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcGetFilter(
            string img_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcGetFilterImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcGetFilterImp(
            IntPtr handle,
            string img_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcGetFilterMem", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcGetFilterMem(
            uint w,
            uint h,
            int type,
            byte[] imgdata);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcGetFilterMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcGetFilterMemImp(
            IntPtr handle,
            uint w,
            uint h,
            int type,
            byte[] imgdata);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcDebayerMem", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcDebayerMem(
            uint w,
            uint h,
            int type,
            byte[] imgdata,
            double edge_flatten_level,
            ref uint d_w,
            ref uint d_h,
            [Out] byte[] pR,
            [Out] byte[] pG,
            [Out] byte[] pB);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcDebayerMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcDebayerMemImp(
            IntPtr handle,
            uint w,
            uint h,
            int type,
            byte[] imgdata,
            double edge_flatten_level,
            ref uint d_w,
            ref uint d_h,
            [Out] byte[] pR,
            [Out] byte[] pG,
            [Out] byte[] pB);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcDebayer", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcDebayer(
            string img_path,
            double edge_flatten_level,
            string r_path,
            string g_path,
            string b_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcDebayerImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcDebayerImp(
            IntPtr handle,
            string img_path,
            double edge_flatten_level,
            string r_path,
            string g_path,
            string b_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcDebayerCuda", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcDebayerCuda(
            string img_path,
            double edge_flatten_level,
            string r_path,
            string g_path,
            string b_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcDebayerCudaImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcDebayerCudaImp(
            IntPtr handle,
            string img_path,
            double edge_flatten_level,
            string r_path,
            string g_path,
            string b_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcDebayerWithCtcMem", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcDebayerWithCtcMem(
            uint w,
            uint h,
            int type,
            byte[] imgdata,
            double edge_flatten_level,
            string ctc_csv,
            ref uint d_w,
            ref uint d_h,
            [Out] byte[] pR,
            [Out] byte[] pG,
            [Out] byte[] pB);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcDebayerCudaWithCtcMem", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcDebayerCudaWithCtcMem(
            uint w,
            uint h,
            int type,
            byte[] imgdata,
            double edge_flatten_level,
            string ctc_csv,
            ref uint d_w,
            ref uint d_h,
            [Out] byte[] pR,
            [Out] byte[] pG,
            [Out] byte[] pB);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcDebayerWithCtcMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcDebayerWithCtcMemImp(
            IntPtr handle,
            uint w,
            uint h,
            int type,
            byte[] imgdata,
            double edge_flatten_level,
            string ctc_csv,
            ref uint d_w,
            ref uint d_h,
            [Out] byte[] pR,
            [Out] byte[] pG,
            [Out] byte[] pB);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcDebayerCudaWithCtcMemImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcDebayerCudaWithCtcMemImp(
            IntPtr handle,
            uint w,
            uint h,
            int type,
            byte[] imgdata,
            double edge_flatten_level,
            string ctc_csv,
            ref uint d_w,
            ref uint d_h,
            [Out] byte[] pR,
            [Out] byte[] pG,
            [Out] byte[] pB);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcDebayerWithCtc", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcDebayerWithCtc(
            string img_path,
            double edge_flatten_level,
            string ctc_csv,
            string r_path,
            string g_path,
            string b_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcDebayerWithCtcImp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcDebayerWithCtcImp(
            IntPtr handle,
            string img_path,
            double edge_flatten_level,
            string ctc_csv,
            string r_path,
            string g_path,
            string b_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcCtcMem", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcCtcMem(
            uint w,
            uint h,
            int type,
            byte[] imgdata_r,
            byte[] imgdata_g,
            byte[] imgdata_b,
            string ctc_matrix,
            [Out] byte[] pR,
            [Out] byte[] pG,
            [Out] byte[] pB);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcCtc", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcCtc(
            string r_in,
            string g_in,
            string b_in,
            string ctc_matrix,
            string r_path,
            string g_path,
            string b_path);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "bvWwtcDebayerWithCtcMemALL", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int bvWwtcDebayerWithCtcMemALL(
            uint w,
            uint h,
            byte[] imgdata,
            string cfg_path,
            double edge_flatten_level,
            string ctc_matrix,
            int type,
            ref uint d_w,
            ref uint d_h,
            [Out] byte[] pR,
            [Out] byte[] pG,
            [Out] byte[] pB);

        #endregion

        #region Debayer

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "DebayerRGB", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int DebayerRGB(
            uint w,
            uint h,
            byte[] imgdata,
            int type,
            int color,
            uint crop_x,
            uint crop_y,
            uint crop_w,
            uint crop_h,
            [Out] byte[] resdata);

        #endregion
    }
}