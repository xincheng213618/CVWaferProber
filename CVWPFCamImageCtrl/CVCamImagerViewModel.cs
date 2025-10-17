using CVCommCore;
using CVCommCore.CVImage;
using CVDB.Services.Algorithm;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.ViewModels;
using Newtonsoft.Json;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Collections.ObjectModel;
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
            if (_imageDisplay != null) _imageDisplay.CurrentImage = null;
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
                        if(poi.PoiType == 0) POIMarkers.Add(new CircleMarker() { Label = poi.PoiName, X = (double)poi.PoiX, Y = (double)poi.PoiY, Width = (double)poi.PoiWidth, Height = (double)poi.PoiHeight, Fill = null });
                        else if(poi.PoiType == 1) POIMarkers.Add(new RectangleMarker() { Label = poi.PoiName, X = (double)poi.PoiX, Y = (double)poi.PoiY, Width = (double)poi.PoiWidth, Height = (double)poi.PoiHeight, Fill = null });
                    }
                }
                else if (resultType == AlgorithmResultType.PoiAnalysis)
                {
                    var details = AlgResultService.GetCommDetailResult(result.Id);
                    if (details != null && details.Count == 1)
                    {
                        DetailResult_CommFile_V2 detailResult_Comm = JsonConvert.DeserializeObject<DetailResult_CommFile_V2>(details[0].Result);
                        if (System.IO.File.Exists(detailResult_Comm.ResultFileName))
                        {
                            ImageItem imageResultViewModel = new ImageItem(id++);

                            PoiAnalysis poiAnalysis = JsonConvert.DeserializeObject<PoiAnalysis>(System.IO.File.ReadAllText(detailResult_Comm.ResultFileName));
                            chipData.DataValue = imageResultViewModel.BrightnessUniformity = poiAnalysis.result.Value;
                            ImageDisplayBrightnessUniformity = string.Format("[{0},{1}]={2:F4}", chipData.Row, chipData.Column, imageResultViewModel.BrightnessUniformity);
                            //
                            imageResultViewModel.FileName = System.IO.Path.GetFileName(resultImageFile);
                            imageResultViewModel.ImagePath = resultImageFile;
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
                    loc.FileName = System.IO.Path.GetFileName(result.ImgFile);
                    loc.ImagePath = result.ImgFile;
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
                image = OpenCvMatTools.ConvertImageTo8UC3(image);
                PutPOIToImage(ref image, POIMarkers);
                OpenCvMatTools.PutTextToImage(ImageDisplayBrightnessUniformity, ref image, Scalar.Green);
                // 在UI线程更新集合
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ImageSrc = image.ToBitmapSource();
                    if (_imageDisplay != null)
                    {
                        _imageDisplay.CurrentImage = ImageSrc;
                        //_imageDisplay.POIMarkers = _poiMarkers;
                    }
                });
            }
        }

        private void DrawCircleToImage(ref Mat image, CircleMarker poi)
        {
            // 将WPF Point转换为OpenCV Point
            OpenCvSharp.Point cvPoint = new OpenCvSharp.Point(poi.X, poi.Y);

            // 绘制圆形标记点
            Cv2.Circle(image, cvPoint,(int)poi.Radius,
                      poi.Color,
                      1);  // -1表示实心圆

            //// 绘制外圈边框
            //Cv2.Circle(image,
            //          new OpenCvSharp.Point((int)cvPoint.X, (int)cvPoint.Y),
            //          (int)poi.Radius,
            //          new Scalar(0, 0, 0),
            //          2);   // 2像素黑色边框

            // 如果有标签文本，绘制文本
            if (!string.IsNullOrEmpty(poi.Label))
            {
                // 计算文本尺寸
                var textSize = Cv2.GetTextSize(poi.Label, HersheyFonts.HersheySimplex, 0.5, 1, out int baseline);

                // 文本位置（标记点下方）
                OpenCvSharp.Point textOrg = new OpenCvSharp.Point(
                    (int)(cvPoint.X - textSize.Width / 2),
                    (int)(cvPoint.Y + poi.Radius + textSize.Height + 5)
                );

                // 绘制文本背景
                //Cv2.Rectangle(image,
                //             new OpenCvSharp.Rect(textOrg.X - 2, textOrg.Y - textSize.Height - 2,
                //                     textSize.Width + 4, textSize.Height + 4),
                //             new Scalar(0, 0, 0),
                //             -1);

                // 绘制文本
                Cv2.PutText(image,
                           poi.Label,
                           textOrg,
                           HersheyFonts.HersheySimplex,
                           0.5,
                           new Scalar(255, 255, 255),
                           1);
            }
        }
        private void DrawRectToImage(ref Mat image, RectangleMarker poi)
        {
            // 将WPF Point转换为OpenCV Point
            OpenCvSharp.Point cvPoint = new OpenCvSharp.Point(poi.X - poi.Width / 2, poi.Y - poi.Height / 2);
            OpenCvSharp.Point cvPoint2 = new OpenCvSharp.Point(poi.X + poi.Width / 2, poi.Y + poi.Height / 2);

            // 绘制矩形标记点
            Cv2.Rectangle(image, cvPoint, cvPoint2, poi.Color, 1);  //

            // 如果有标签文本，绘制文本
            if (!string.IsNullOrEmpty(poi.Label))
            {
                // 计算文本尺寸
                var textSize = Cv2.GetTextSize(poi.Label, HersheyFonts.HersheySimplex, 0.5, 1, out int baseline);

                // 文本位置（标记点下方）
                OpenCvSharp.Point textOrg = new OpenCvSharp.Point(
                    (int)(poi.X - textSize.Width / 2),
                    (int)(cvPoint2.Y + textSize.Height + 5)
                );

                // 绘制文本背景
                //Cv2.Rectangle(image,
                //             new OpenCvSharp.Rect(textOrg.X - 2, textOrg.Y - textSize.Height - 2,
                //                     textSize.Width + 4, textSize.Height + 4),
                //             new Scalar(0, 0, 0),
                //             -1);

                // 绘制文本
                Cv2.PutText(image,
                           poi.Label,
                           textOrg,
                           HersheyFonts.HersheySimplex,
                           0.5,
                           new Scalar(255, 255, 255),
                           1);
            }
        }
        private void PutPOIToImage(ref Mat image, List<POIMarker> poiMarkers)
        {
            if (image == null || image.Empty())
                return;

            if (poiMarkers == null || poiMarkers.Count == 0)
                return;

            // 确保图像是彩色图像（标记点需要彩色）
            if (image.Channels() == 1)
            {
                Cv2.CvtColor(image, image, ColorConversionCodes.GRAY2BGR);
            }

            foreach (var poi in poiMarkers)
            {
                if (poi == null) continue;
                if (poi is CircleMarker poiC) DrawCircleToImage(ref image, poiC);
                else if(poi is RectangleMarker poiR) DrawRectToImage(ref image, poiR);
            }
        }

        private CVImager _imageDisplay;
        public void SetImageCtrl(CVImager imageDisplay)
        {
            _imageDisplay = imageDisplay;
        }
    }
}
