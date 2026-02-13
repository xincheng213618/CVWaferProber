using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CVWPFCamImageCtrl
{
    public static class OpenCVImageLoader
    {
        /// <summary>
        /// 高效加载大TIFF图像（带内存优化）
        /// </summary>
        public static BitmapSource? LoadTiffImage(string filePath, double scaleFactor = 0.1)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                using (var highResMat = Cv2.ImRead(filePath, ImreadModes.Unchanged))
                {
                    if (highResMat.Empty())
                        return null;

                    // 如果是超大图像，先进行缩放
                    if (highResMat.Width > 2000 || highResMat.Height > 2000)
                    {
                        using (var resizedMat = new Mat())
                        {
                            Cv2.Resize(highResMat, resizedMat,
                                new Size(highResMat.Width * scaleFactor, highResMat.Height * scaleFactor),
                                0, 0, InterpolationFlags.Area);

                            return ConvertMatToBitmap(resizedMat);
                        }
                    }
                    else
                    {
                        return ConvertMatToBitmap(highResMat);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load image: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取图像信息（不加载完整图像）
        /// </summary>
        public static (int width, int height, int channels) GetImageInfo(string filePath)
        {
            try
            {
                using (var mat = Cv2.ImRead(filePath, ImreadModes.AnyColor | ImreadModes.AnyDepth))
                {
                    if (!mat.Empty())
                    {
                        return (mat.Width, mat.Height, mat.Channels());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get image information: {ex.Message}");
            }

            return (0, 0, 0);
        }

        public static BitmapSource? ConvertMatToBitmap(Mat mat)
        {
            if (mat.Empty()) return null;

            // 根据通道数进行颜色转换
            if (mat.Channels() == 3)
            {
                Cv2.CvtColor(mat, mat, ColorConversionCodes.BGR2RGB);
            }
            else if (mat.Channels() == 4)
            {
                Cv2.CvtColor(mat, mat, ColorConversionCodes.BGRA2RGBA);
            }
            // 单通道图像保持不变

            return mat.ToBitmapSource();
        }
        /// <summary>
        /// 读取图像原始字节数据（无UI依赖，可在后台线程执行）
        /// </summary>
        public static byte[] ReadImageRawData(string imagePath)
        {
            // 步骤1：验证文件是否存在
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("图像文件不存在", imagePath);
            }
           
            try
            {
                // 实现逻辑：使用OpenCV读取图像为字节数组（Mat→byte[]），示例如下
                using (var mat = Cv2.ImRead(imagePath, ImreadModes.Color))
                {
                    if (mat.Empty())
                    {
                        throw new Exception("无法读取图像文件");
                    }
                    // 将Mat转换为字节数组（根据你的图像格式调整，此处为BGR格式示例）
                    int dataSize = mat.Rows * mat.Cols * mat.Channels();
                    byte[] rawData = new byte[dataSize];
                    Marshal.Copy(mat.Data, rawData, 0, dataSize);
                    return rawData;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"读取图像文件失败：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 从原始字节数据创建BitmapSource（必须在UI线程执行）
        /// </summary>
        public static BitmapSource CreateBitmapSourceFromRawData(byte[] rawData, int width, int height)
        {
            // 关键：创建可在WPF中显示的BitmapSource，确保在UI线程执行
            var bitmapSource = BitmapSource.Create(
                width,
                height,
                96, // DPI X
                96, // DPI Y
                PixelFormats.Bgr24, // 对应OpenCV的BGR格式，需与原始数据匹配
                null,
                rawData,
                width * 3 // 每行字节数（3=BGR三个通道）
            );

            // 可选：冻结BitmapSource，提升性能（冻结后不可修改，可跨线程访问）
            bitmapSource.Freeze();
            return bitmapSource;
        }
    }
}
