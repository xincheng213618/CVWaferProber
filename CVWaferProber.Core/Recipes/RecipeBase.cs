using CVWaferProber.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Recipes
{
    public class RecipeBase : ViewModelBase
    {
        public RecipeBase()
        {

        }
        public RecipeBase(double min, double max)
        {
            _Min = min;
            _Max = max;
        }

        public bool IsUse { get => _IsUse; set { _IsUse = value; OnPropertyChanged(); } }
        private bool _IsUse = true;

        public double Min { get => _Min; set { _Min = value; OnPropertyChanged(); } }
        private double _Min;

        public double Max { get => _Max; set { _Max = value; OnPropertyChanged(); } }
        private double _Max;
    }
}
