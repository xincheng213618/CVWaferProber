using CVWaferProber.Core.ViewModels;

namespace CVImageView.ViewModels
{
    public class ImageResultViewModel : ViewModelBase
    {
        public ImageResultViewModel(uint id)
        {
            Id = id;
            TestTime = DateTime.Now;
        }

        public uint Id { get; set; }
        public string? SerialNumber { get; set; }
        public string? ImageInfo { get; set; }
        public string? ImageFile { get; set; }
        public DateTime? TestTime { get; set; }
    }
}
