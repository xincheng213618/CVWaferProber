using CVWaferProber.Core.ViewModels;

namespace CVWPFCameraImage.ViewModels
{
    public class CVImageResultViewModel : ViewModelBase
    {
        public CVImageResultViewModel(uint id)
        {
            Id = id;
            TestTime = DateTime.Now;
        }

        public uint Id { get; set; }
        public string? SerialNumber { get; set; }
        /// <summary>
        /// 亮度均匀性
        /// </summary>
        public double? BrightnessUniformity { get; set; }
        public string? ResultType { get; set; }
        public string? ImageInfo { get; set; }
        public string? ImageFile { get; set; }
        public DateTime? TestTime { get; set; }
    }
}
