// MainViewModel.cs
using CVWaferProber.Core.Models;
using CVWaferProber.Core.ViewModels;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.Text;
using OpenCvSharp.WpfExtensions;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;

namespace CVImageView.ViewModels
{
    public class CVImageViewModel : ViewModelBase
    {
        private ImageSource? _imageSource;
        private ObservableCollection<POIMarker> _poiMarkers;
        private double _zoomLevel = 1.0;
        public ObservableCollection<ImageResultViewModel> ImageResults { get; } = new ObservableCollection<ImageResultViewModel>();

        public ImageSource ImageSource
        {
            get => _imageSource;
            set => SetProperty(ref _imageSource, value);
        }

        public ObservableCollection<POIMarker> POIMarkers
        {
            get => _poiMarkers;
            set => SetProperty(ref _poiMarkers, value);
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

        public CVImageViewModel()
        {
            _imageSource = null;
            _poiMarkers = new ObservableCollection<POIMarker>();

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
                    ImageSource = image.ToBitmapSource();
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
            ImageResultViewModel imageResultViewModel = new ImageResultViewModel(id++);
            imageResultViewModel.ImageFile = name;
            ImageResults.Add(imageResultViewModel);

            ShowImage(name);
        }
        public void AddImageResult(ChipData? chipData, string serialNumber, string resultImageFile)
        {
            ImageResultViewModel imageResultViewModel = new ImageResultViewModel(id++);
            //imageResultViewModel.ImageFile = name;
            imageResultViewModel.SerialNumber = serialNumber;
            ImageResults.Add(imageResultViewModel);

            ShowImage(serialNumber, resultImageFile);
        }
        public void AddImageResult(ChipData? chipData,string serialNumber)
        {
            ImageResultViewModel imageResultViewModel = new ImageResultViewModel(id++);
            //imageResultViewModel.ImageFile = name;
            imageResultViewModel.SerialNumber = serialNumber;
            ImageResults.Add(imageResultViewModel);

            ShowImage(serialNumber);
        }
        private void ShowImage(string name, string? resultImageFile = null)
        {
            //OpenCvSharp.Mat image = new OpenCvSharp.Mat(400,500, OpenCvSharp.MatType.CV_8UC1);
            string FileName = "F:\\img\\晶圆台\\陈高\\image_caculate.tif";
            var image = OpenCvSharp.Cv2.ImRead(FileName, OpenCvSharp.ImreadModes.Color);
            // 设置文字参数
            string text = name;
            // 创建检测器
            OpenCvSharp.Point center = new OpenCvSharp.Point(image.Cols/2, image.Rows/2);
            HersheyFonts font = HersheyFonts.HersheySimplex;
            double fontScale = 4;
            int thickness = 5;

            // 计算文字尺寸
            OpenCvSharp.Size textSize = Cv2.GetTextSize(text, font, fontScale, thickness, out int baseline);
            Scalar textColor = new Scalar(255, 255, 255); // 白色文字
            // 计算文字起始位置
            OpenCvSharp.Point textOrigin = new OpenCvSharp.Point(
                center.X - textSize.Width / 2,
                center.Y + textSize.Height / 2
            );

            //// 绘制半透明背景
            //Mat overlay = image.Clone();
            //Cv2.Rectangle(overlay, bgRect, bgColor, -1);
            //Cv2.AddWeighted(overlay, 0.6, image, 0.4, 0, image);

            // 绘制文字
            Cv2.PutText(image, text, textOrigin, font, fontScale, textColor, thickness);
            try
            {
                var detector = OCRTesseract.Create();
                detector.Run(image, out string outputText, out OpenCvSharp.Rect[] components, out string[] componentTexts, out float[] confidences);

                // 绘制检测框
                foreach (OpenCvSharp.Rect rect in components)
                {
                    Cv2.Rectangle(image, rect, new Scalar(0, 255, 0), 2);
                }
            }catch (Exception ex) { }
            // 检测文字

            ImageSource = image.ToBitmapSource();
        }

        public void ClearImageResult()
        {
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