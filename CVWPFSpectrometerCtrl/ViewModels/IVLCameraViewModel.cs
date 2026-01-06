using ColorVision.Core.Entities;
using CVCommCore;
using CVCommCore.CVImage;
using CVDB.Services.Algorithm;
using CVWaferProber.Core.ViewModels;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CVWPFSpectrometerCtrl.ViewModels
{
    public class IVLCameraViewModel : ViewModelBase
    {
        private ObservableCollection<IVLCameraMeasurement> _measurements;
        private IVLCameraMeasurement _selectedMeasurement;
        public ObservableCollection<IVLCameraMeasurement> Measurements
        {
            get => _measurements;
            set
            {
                _measurements = value;
                OnPropertyChanged(nameof(Measurements));
            }
        }
        public IVLCameraMeasurement SelectedMeasurement
        {
            get => _selectedMeasurement;
            set
            {
                if (SetProperty(ref _selectedMeasurement, value))
                {
                    ResetAndUpdateImage();
                }
            }
        }
        private BitmapSource _imageSrc;
        public BitmapSource ImageSrc
        {
            get => _imageSrc;
            set
            {
                if (SetProperty(ref _imageSrc, value))
                {

                }

            }
        }
        public IVLCameraViewModel()
        {
            Measurements = new ObservableCollection<IVLCameraMeasurement>();
        }

        public void Clear()
        {
            No = 1;
            Measurements.Clear();
        }
        int No = 1;
        public void LoadData(string serialNumber)
        {
            var results = AlgResultService.LoadAlgResultByBatchCode(serialNumber);
            LoadData(results,null);
        }
        public void LoadData(List<VScgdAlgorithmResultMaster> results, List<float> il_results)
        {
            Clear();

            if (results == null || results.Count == 0) return;
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                AlgorithmResultType resultType = (AlgorithmResultType)result.ImgFileType;
                IVLCameraMeasurement loc = new IVLCameraMeasurement(No++);
                loc.ImageFile = result.ImgFile;
                //loc.ResultType = "IVL";
                loc.SerialNumber = result.BatchCode;
                loc.Timestamp = result.CreateDate;
                loc.V = result.VResult;
                loc.I = result.IResult;
                if(il_results!=null && il_results.Count > i) loc.Luminance = il_results[i];
                // 在UI线程更新集合
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Measurements.Add(loc);
                });
            }
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (Measurements.Any())
                {
                    SelectedMeasurement = Measurements.First();
                }
            });
        }

        public void ResetAndUpdateImage()
        {
            if (SelectedMeasurement == null) return;
            CVCIEFileInfo cieFile = new CVCIEFileInfo();
            if (CVImageFileUtil.LoadImgFile(SelectedMeasurement.ImageFile, ref cieFile))
            {
                if (!string.IsNullOrEmpty(cieFile.srcFileName))
                {
                    string srcImage = Path.Combine(Path.GetDirectoryName(SelectedMeasurement.ImageFile), cieFile.srcFileName);
                    OpenCvSharp.Mat img = new OpenCvSharp.Mat();
                    if (CVImageFileUtil.LoadImgFile(srcImage, ref img))
                    {
                        OpenCvMatTools.PutTextToImage(string.Format("L={0:F4}", SelectedMeasurement.Luminance), ref img, Scalar.Red, 6, 10);
                        ImageSrc = img.ToBitmapSource();
                    }
                }
            }
        }

        // 从字节数组加载图片（适用于网络流或数据库）
        public void UpdateImageFromBytes(byte[] imageData)
        {
            using (var stream = new MemoryStream(imageData))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze(); // 跨线程时可能需要

                ImageSrc = bitmap;
            }
        }
    }
}
