using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Presenter.Helper
{
    /// <summary>
    /// Class providing functionality around switching and restoring theme settings
    /// </summary>
    public static class ThemeHelper
    {
        // the key thats holds the selected theme in the application settings (AppData Local)
        private const string SelectedAppThemeKey = "SelectedAppTheme";
        private static Window CurrentApplicationWindow;
        // Keep reference so it does not get optimized/garbage collected
        private static UISettings uiSettings;
        /// <summary>
        /// Gets the current actual theme of the app based on the requested theme of the
        /// root element, or if that value is Default, the requested theme of the Application (System).
        /// </summary>
        public static ElementTheme AppTheme
        {
            get
            {
                if (Window.Current.Content is FrameworkElement rootElement)
                {
                    if (rootElement.RequestedTheme != ElementTheme.Default)
                    {
                        return rootElement.RequestedTheme;
                    }
                }

                // when system mode is selected or rootElement does not exist in Window.Current.Content
                return Presenter.App.GetEnum<ElementTheme>(App.Current.RequestedTheme.ToString());
            }
        }

        /// <summary>
        /// Gets or sets (with LocalSettings persistence) the RequestedTheme of the root element.
        /// </summary>
        public static ElementTheme Theme
        {
            get
            {
                if (Window.Current.Content is FrameworkElement rootElement)
                {
                    return rootElement.RequestedTheme;
                }

                // In case rootElement is not the Content of the Window
                // Return System mode
                return ElementTheme.Default;
            }
            set
            {
                // Set the Theme mode to the rootElement of the Window
                if (Window.Current.Content is FrameworkElement rootElement)
                {
                    // This triggers the main internal theme change process
                    rootElement.RequestedTheme = value;
                }

                // Save the Theme to the AppData Local settings
                ApplicationData.Current.LocalSettings.Values[SelectedAppThemeKey] = value.ToString();
                UpdateThemeButton(value);
            }
        }

        public static void Initialize()
        {
            // Save reference as this might be null when the user is in another app
            CurrentApplicationWindow = Window.Current;

            // Get the Theme by the given data in AppData Local settings
            string savedTheme = ApplicationData.Current.LocalSettings.Values[SelectedAppThemeKey]?.ToString();

            if (savedTheme != null)
            {
                // Set saved theme
                Theme = Presenter.App.GetEnum<ElementTheme>(savedTheme);
            }

            // Registering to color changes, thus we notice when user changes theme system wide
            uiSettings = new UISettings();
            uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;

        }

        private static void UiSettings_ColorValuesChanged(UISettings sender, object args)
        {
            // Make sure we have a reference to our window so we dispatch a UI change
            if (CurrentApplicationWindow != null)
            {
                // Dispatch on UI thread so that we have a current appbar to access and change
                CurrentApplicationWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                {
                    // Convert ApplicationTheme (System) to ElementTheme (App Element)
                    // System mode only
                    // fires twice when changed
                    if(Theme == ElementTheme.Default)
                        UpdateThemeButton(Application.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light);
                });
            }
        }

        public static bool IsDarkTheme()
        {
            // check if in system mode
            if (Theme == ElementTheme.Default)
            {
                // Check if system theme is set to dark mode
                return Application.Current.RequestedTheme == ApplicationTheme.Dark;
            }
            // Return Theme of the UI Element
            return Theme == ElementTheme.Dark;
        }

        public static void UpdateThemeButton(ElementTheme theme)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Sync ThemeButton on NavigationView
            if (rootFrame != null)
            {
                if (rootFrame.Content.GetType().Equals(typeof(MainPage)))
                {
                    MainPage mainPage = rootFrame.Content as MainPage;
                    mainPage.UpdateThemeButton(theme);
                }
            }

            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;

            // ThemeResources are not applied when changing themes
            // therefore it must be set manually

            if (ThemeHelper.IsDarkTheme())
            {
                titleBar.ButtonForegroundColor = Colors.White;
                ((SolidColorBrush)Application.Current.Resources["AppTitleBarForegroundBrush"]).Color = Colors.White;
            }
            else
            {
                titleBar.ButtonForegroundColor = Colors.Black;
                ((SolidColorBrush)Application.Current.Resources["AppTitleBarForegroundBrush"]).Color = Colors.Black;
            }
        }
    }
}
