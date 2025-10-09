using CVCommCore;
using CVCommCore.CVImage;
using CVDB.Services.Algorithm;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.ViewModels;
using Newtonsoft.Json;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace CVWPFCamImageCtrl
{
    public class CVCamImagerViewModel : ViewModelBase
    {
        private ImageSource? _imageSource;
        private ObservableCollection<ImageItem> _imageResults;
        private ObservableCollection<POIMarker> _poiMarkers;
        private uint id = 1;
        public CVCamImagerViewModel()
        {
            _imageSource = null;
            _poiMarkers = new ObservableCollection<POIMarker>();
            _imageResults = new ObservableCollection<ImageItem>();
        }
        public ObservableCollection<POIMarker> POIMarkers
        {
            get => _poiMarkers;
            set => SetProperty(ref _poiMarkers, value);
        }
        public ObservableCollection<ImageItem> ImageResults
        {
            get => _imageResults;
            set => SetProperty(ref _imageResults, value);
        }
        public ImageSource? ImageSrc
        {
            get => _imageSource;
            set => SetProperty(ref _imageSource, value);
        }
        public void ClearImageResult()
        {
            id = 1;
            ImageSrc = null;
            _imageResults.Clear();
        }
        public void LoadImageResult(ChipData? chipData, string serialNumber)
        {
            string? resultImageFile = null;
            DateTime? TestTime = null;
            string? ImageDisplayBrightnessUniformity = null;
            var results = AlgResultService.LoadAlgResultByBatchCode(serialNumber);
            if (results == null || results.Count == 0) return;
            List<POIMarker> POIMarkers = new List<POIMarker>();
            foreach (var result in results)
            {
                AlgorithmResultType resultType = (AlgorithmResultType)result.ImgFileType;
                if (result.ImgFileType >= 42 && result.ImgFileType <= 45)
                {
                    resultImageFile = result.ImgFile;
                    TestTime = result.CreateDate;
                }
                else if (resultType == AlgorithmResultType.POI_Y)
                {
                    var details = AlgResultService.GetPOIDetailResult(result.Id);
                    foreach (var poi in details)
                    {
                        POIMarkers.Add(new POIMarker() { X = (double)poi.PoiX, Y = (double)poi.PoiY, Width = (double)poi.PoiWidth, Height = (double)poi.PoiHeight, Fill = null });
                    }
                }
                else if (resultType == AlgorithmResultType.PoiAnalysis)
                {
                    var details = AlgResultService.GetCommDetailResult(result.Id);
                    if (details != null && details.Count == 1)
                    {
                        DetailResult_CommFile_V2 detailResult_Comm = JsonConvert.DeserializeObject<DetailResult_CommFile_V2>(details[0].Result);
                        if (File.Exists(detailResult_Comm.ResultFileName))
                        {
                            ImageItem imageResultViewModel = new ImageItem(id++);

                            PoiAnalysis poiAnalysis = JsonConvert.DeserializeObject<PoiAnalysis>(File.ReadAllText(detailResult_Comm.ResultFileName));
                            chipData.DataValue = imageResultViewModel.BrightnessUniformity = poiAnalysis.result.Value;
                            ImageDisplayBrightnessUniformity = string.Format("[{0},{1}]={2:F4}", chipData.Row, chipData.Column, imageResultViewModel.BrightnessUniformity);
                            //
                            imageResultViewModel.FileName = resultImageFile;
                            //imageResultViewModel.SerialNumber = serialNumber;
                            //imageResultViewModel.TestTime = TestTime;
                            //imageResultViewModel.ResultType = "数据提取";
                            // 在UI线程更新集合
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ImageResults.Add(imageResultViewModel);
                            });

                        }
                    }
                }
                //定位
                else if (resultType == AlgorithmResultType.OLED_FindDotsArrayOutFile)
                {
                    ImageItem loc = new ImageItem(id++);
                    loc.FileName = result.ImgFile;
                    //loc.ResultType = "定位";
                    //loc.SerialNumber = serialNumber;
                    //loc.TestTime = result.CreateDate;
                    // 在UI线程更新集合
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ImageResults.Add(loc);
                    });
                }
            }

            if (!string.IsNullOrEmpty(resultImageFile))
            {
                OpenCvSharp.Mat? image = null;
                if (!CVImageFileUtil.LoadImgFile(resultImageFile, ref image)) return;
                OpenCvMatTools.PutTextToImage(ImageDisplayBrightnessUniformity, ref image, Scalar.Green);
                // 在UI线程更新集合
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var marker in POIMarkers)
                    {
                        _poiMarkers.Add(marker);
                    }
                    ImageSrc = image.ToBitmapSource();
                });
            }
        }

    }
}
