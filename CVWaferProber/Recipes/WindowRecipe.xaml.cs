using ColorVision.UI;
using CVWaferProber.Core.Recipes;
using CVWaferProber.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CVWaferProber.Recipes
{
    public static class PropertyEditorHelper
    {
        public static Binding CreateTwoWayBinding(object source, string path)
        {
            return new Binding(path)
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
        }
        public static TextBlock CreateLabel(PropertyInfo property)
        {
            var desc = property.GetCustomAttribute<DescriptionAttribute>()?.Description;
            var tb = new TextBlock
            {
                Text = GetDisplayName(property),
                MinWidth = 60,
                ToolTip = string.IsNullOrWhiteSpace(desc) ? null : desc,
                Foreground = Brushes.Black

            };
            return tb;
        }
        public static string GetDisplayName(PropertyInfo prop, string? overrideName = null)
        {
            var displayNameAttr = prop.GetCustomAttribute<DisplayNameAttribute>();
            var raw = overrideName ?? displayNameAttr?.DisplayName ?? prop.Name;
            return raw;
        }
        public static TextBox CreateSmallTextBox(Binding binding)
        {
            var tb = new TextBox
            {
                Margin = new Thickness(5, 0, 0, 0),
            };
            tb.SetBinding(TextBox.TextProperty, binding);
            return tb;
        }
        public static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Use WPF focus traversal instead of simulating a tab key press
            if (e.Key == Key.Enter)
            {
                if (sender is UIElement uie)
                {
                    uie.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    e.Handled = true;
                }
            }
        }

    }

    /// <summary>
    /// WindowRecipe.xaml 的交互逻辑
    /// </summary>
    public partial class WindowRecipe : Window
    {
        public WindowRecipe()
        {
            InitializeComponent();
        }
        AOIRecipes AOIRecipe { get; set; }
        IVLRecipes IVLRecipe { get; set; }
        EQERecipes EQERecipe { get; set; }
        VAMRecipes VAMRecipe { get; set; }

        private void Window_Initialized(object sender, EventArgs e)
        {
            AOIRecipe = AOIRecipes.Instance;
            IVLRecipe = IVLRecipes.Instance;
            EQERecipe = EQERecipes.Instance;
            VAMRecipe = VAMRecipes.Instance;

            Gen(AOIRecipeInfo, AOIRecipe);
            Gen(IVLRecipeInfo, IVLRecipe);
            Gen(EQERecipeInfo, EQERecipe);
            Gen(VAMRecipeInfo, VAMRecipe);

        }

        public void Gen(StackPanel stackPanel,object obj)
        {
            var t = obj.GetType();

            // 1. 获取属性
            var allProps = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.CanRead && p.CanWrite);

            foreach (var item in allProps)
            {
                if (item.PropertyType == typeof(RecipeBase))
                {
                    DockPanel dockPanel = GenProperties(item, obj);
                    if (dockPanel != null)
                    {
                        stackPanel.Children.Add(dockPanel);
                    }
                }
            }

        }

        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            if (property.GetValue(obj) is not RecipeBase recipeBase) return null;

            var dockPanel = new DockPanel();

            UniformGrid uniformGrid = new UniformGrid() { Columns = 2, HorizontalAlignment = HorizontalAlignment.Right, Width = 200 };
            Binding bindingMin = PropertyEditorHelper.CreateTwoWayBinding(recipeBase, "Min");
            bindingMin.UpdateSourceTrigger = UpdateSourceTrigger.Default;
            bindingMin.StringFormat = "0.0################";
            var textboxMin = PropertyEditorHelper.CreateSmallTextBox(bindingMin);
            textboxMin.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;
            uniformGrid.Children.Add(textboxMin);

            Binding bindingMax = PropertyEditorHelper.CreateTwoWayBinding(recipeBase, "Max");
            bindingMax.UpdateSourceTrigger = UpdateSourceTrigger.Default;
            bindingMax.StringFormat = "0.0################";
            var textbox = PropertyEditorHelper.CreateSmallTextBox(bindingMax);
            textbox.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;
            uniformGrid.Children.Add(textbox);

            DockPanel.SetDock(uniformGrid, Dock.Right);
            dockPanel.Children.Add(uniformGrid);

            var textBlock = PropertyEditorHelper.CreateLabel(property);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }


        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ConfigHandler.GetInstance().SaveConfigs();
            this.Close();
        }

        private void ResetAOI_Click(object sender, RoutedEventArgs e)
        {
            ViewModeBaseExtensions.Reset(AOIRecipe);
            ConfigHandler.GetInstance().SaveConfigs();
            this.Close();
        }

        private void ResetIVL_Click(object sender, RoutedEventArgs e)
        {
            ViewModeBaseExtensions.Reset(IVLRecipe);
            ConfigHandler.GetInstance().SaveConfigs();
            this.Close();
        }

        private void ResetVAM_Click(object sender, RoutedEventArgs e)
        {
            ViewModeBaseExtensions.Reset(VAMRecipe);
            ConfigHandler.GetInstance().SaveConfigs();
            this.Close();
        }

        private void ResetEQE_Click(object sender, RoutedEventArgs e)
        {
            ViewModeBaseExtensions.Reset(EQERecipe);
            ConfigHandler.GetInstance().SaveConfigs();
            this.Close();
        }
    }
}
