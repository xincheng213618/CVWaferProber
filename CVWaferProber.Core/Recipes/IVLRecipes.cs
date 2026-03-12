using ColorVision.UI;
using CVWaferProber.Core.ViewModels;

namespace CVWaferProber.Core.Recipes
{
    public class IVLRecipes : ViewModelBase, IConfig
    {
        public static IVLRecipes Instance => ConfigService.Instance.GetRequiredService<IVLRecipes>();

        public RecipeBase Luminance { get => _Luminance; set { _Luminance = value; OnPropertyChanged(); } }
        private RecipeBase _Luminance = new RecipeBase();
        public RecipeBase Uniformity { get => _Uniformity; set { _Uniformity = value; OnPropertyChanged(); } }
        private RecipeBase _Uniformity = new RecipeBase();

        public RecipeBase Wave { get => _Wave; set { _Wave = value; OnPropertyChanged(); } }
        private RecipeBase _Wave = new RecipeBase();

        public RecipeBase Cx { get => _Cx; set { _Cx = value; OnPropertyChanged(); } }
        private RecipeBase _Cx = new RecipeBase();

        public RecipeBase Cy { get => _Cy; set { _Cy = value; OnPropertyChanged(); } }
        private RecipeBase _Cy = new RecipeBase();

        public RecipeBase ExcitationPurity { get => _ExcitationPurity; set { _ExcitationPurity = value; OnPropertyChanged(); } }
        private RecipeBase _ExcitationPurity = new RecipeBase();

    }

}
