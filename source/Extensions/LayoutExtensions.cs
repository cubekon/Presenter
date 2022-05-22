using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Presenter.Extensions
{
    public static class LayoutExtensions
    {
        public static UIElementCollection ReverseChildren(this UIElementCollection childrens)
        {
            // Reverse the StackPanel children order
            var tempChildrenCollection = childrens.Cast<UIElement>().ToArray().Reverse();

            childrens.Clear();

            foreach (UIElement child in tempChildrenCollection)
            {
                childrens.Add(child);
            }

            return childrens;
        }
    }
}
