using CVWaferProber.Core.ViewModels;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace CVWPFCamImageCtrl
{
    public class ImageItem : ViewModelBase
    {
        private int _id;
        private string _imagePath;
        private string _fileName;
        private string _status;
        private double _fileSizeMB;
        private double _brightnessUniformity;

        public ImageItem(uint id) : this((int)id)
        {
        } 
        public ImageItem(int id) 
        {
            _id = (int)id;
        }

        public double BrightnessUniformity 
        { 
            get => _brightnessUniformity;
            set
            {
                SetProperty(ref _brightnessUniformity, value);
            }
        }
        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string ImagePath
        {
            get => _imagePath;
            set { _imagePath = value; OnPropertyChanged(); }
        }

        public string FileName
        {
            get => _fileName;
            set { _fileName = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public double FileSizeMB
        {
            get => _fileSizeMB;
            set { _fileSizeMB = value; OnPropertyChanged(); }
        }

        private static uint id = 1;
        public static ImageItem CreateFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return null;
            var fileInfo = new FileInfo(filePath);
            var imageData = new ImageItem(id++)
            {
                FileName = Path.GetFileName(filePath),
                ImagePath = filePath,
                FileSizeMB = fileInfo.Length/1024,
                //FileSize = FormatFileSize(fileInfo.Length),
                //Format = Path.GetExtension(filePath).ToUpper().TrimStart('.')
            };

            // 对于大文件，我们延迟加载图像尺寸信息
            //imageData.ImageSize = "加载中...";

            return imageData;
        }
    }
}
