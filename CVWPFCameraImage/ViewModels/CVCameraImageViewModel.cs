using CVCommCore;
using CVCommCore.CVImage;
using CVDB.Services.Algorithm;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.ViewModels;
using CVWPFCameraImage.Models;
using Microsoft.Win32;
using Newtonsoft.Json;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;

namespace CVWPFCameraImage.ViewModels
{
    public class CVCameraImageViewModel : ViewModelBase
    {
        private ImageSource? _imageSource;
        private ObservableCollection<POIMarker> _poiMarkers;
        private double _zoomLevel = 1.0;
        public ObservableCollection<CVImageResultViewModel> _imageResults;

        public ImageSource? ImageSrc
        {
            get => _imageSource;
            set => SetProperty(ref _imageSource, value);
        }

        public ObservableCollection<POIMarker> POIMarkers
        {
            get => _poiMarkers;
            set => SetProperty(ref _poiMarkers, value);
        }
        public ObservableCollection<CVImageResultViewModel> ImageResults
        {
            get => _imageResults;
            set => SetProperty(ref _imageResults, value);
        }
        public double ZoomLevel
        {
            get => _zoomLevel;
            set => SetProperty(ref _zoomLevel, Math.Max(0.1, Math.Min(10.0, value)));
        }
        public ICommand LoadImageCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ResetZoomCommand { get; }
        public ICommand AddPOICommand { get; }
        public ICommand ClearPOIsCommand { get; }

        public CVCameraImageViewModel()
        {
            _imageSource = null;
            _poiMarkers = new ObservableCollection<POIMarker>();
            _imageResults = new ObservableCollection<CVImageResultViewModel>();

            LoadImageCommand = new CVImgRelayCommand(LoadImage);
            ZoomInCommand = new CVImgRelayCommand(() => ZoomLevel *= 1.2);
            ZoomOutCommand = new CVImgRelayCommand(() => ZoomLevel /= 1.2);
            ResetZoomCommand = new CVImgRelayCommand(() => ZoomLevel = 1.0);
            AddPOICommand = new CVImgRelayCommand<System.Windows.Point>(AddPOI);
            ClearPOIsCommand = new CVImgRelayCommand(ClearPOIs);
        }
        private void LoadImage()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp|All files|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var image = OpenCvSharp.Cv2.ImRead(openFileDialog.FileName, OpenCvSharp.ImreadModes.Unchanged);
                    ImageSrc = image.ToBitmapSource();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading image: {ex.Message}");
                }
            }
        }
        private uint id = 1;
        public void AddImageResult(string name)
        {
            CVImageResultViewModel imageResultViewModel = new CVImageResultViewModel(id++);
            imageResultViewModel.ImageFile = name;
            ImageResults.Add(imageResultViewModel);

            ShowImage(name);
        }
        public void AddImageResult(ChipData? chipData, string serialNumber, string resultImageFile)
        {
            CVImageResultViewModel imageResultViewModel = new CVImageResultViewModel(id++);
            //imageResultViewModel.ImageFile = name;
            imageResultViewModel.SerialNumber = serialNumber;
            ImageResults.Add(imageResultViewModel);

            ShowImage(serialNumber, resultImageFile);
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
                            CVImageResultViewModel imageResultViewModel = new CVImageResultViewModel(id++);

                            PoiAnalysis poiAnalysis = JsonConvert.DeserializeObject<PoiAnalysis>(File.ReadAllText(detailResult_Comm.ResultFileName));
                            chipData.DataValue = imageResultViewModel.BrightnessUniformity = poiAnalysis.result.Value;
                            ImageDisplayBrightnessUniformity = string.Format("[{0},{1}]={2:F4}", chipData.Row, chipData.Column, imageResultViewModel.BrightnessUniformity);
                            //
                            imageResultViewModel.ImageFile = resultImageFile;
                            imageResultViewModel.SerialNumber = serialNumber;
                            imageResultViewModel.TestTime = TestTime;
                            imageResultViewModel.ResultType = "数据提取";
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
                    CVImageResultViewModel loc = new CVImageResultViewModel(id++);
                    loc.ImageFile = result.ImgFile;
                    loc.ResultType = "定位";
                    loc.SerialNumber = serialNumber;
                    loc.TestTime = result.CreateDate;
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
                //ShowImage(ImageDisplayBrightnessUniformity, resultImageFile);
            }
        }

        private void ShowImage(string? name, OpenCvSharp.Mat? image)
        {
            //try
            //{
            //    var detector = OCRTesseract.Create();
            //    detector.Run(image, out string outputText, out OpenCvSharp.Rect[] components, out string[] componentTexts, out float[] confidences);

            //    // 绘制检测框
            //    foreach (OpenCvSharp.Rect rect in components)
            //    {
            //        Cv2.Rectangle(image, rect, new Scalar(0, 255, 0), 2);
            //    }
            //}
            //catch (Exception ex) { }
            // 在UI线程更新集合
       }
        private void ShowImage(string? name, string? resultImageFile = null)
        {
            string? FileName = resultImageFile;
            OpenCvSharp.Mat? image = null;
            if (!CVImageFileUtil.LoadImgFile(FileName, ref image)) return;
            //
            OpenCvMatTools.PutTextToImage(name, ref image, Scalar.Green);
            // 在UI线程更新集合
            Application.Current.Dispatcher.Invoke(() =>
            {
                ImageSrc = image.ToBitmapSource();
            });
        }

        public void ClearImageResult()
        {
            ImageSrc = null;
            _poiMarkers.Clear();
            ImageResults.Clear();
            id = 1;
        }

        private void AddPOI(System.Windows.Point position)
        {
            var marker = new POIMarker
            {
                X = position.X - 5, // 居中
                Y = position.Y - 5,
                Fill = System.Windows.Media.Brushes.Red,
                Stroke = System.Windows.Media.Brushes.White,
                StrokeThickness = 2
            };

            POIMarkers.Add(marker);
        }

        private void ClearPOIs()
        {
            POIMarkers.Clear();
        }
    }
}
