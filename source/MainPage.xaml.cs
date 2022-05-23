using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;
using Windows.System;
using Presenter.Views;
using Windows.UI;
using System.Diagnostics;
using Presenter.Helper;

using muxc = Microsoft.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Presenter.Extensions;
using Presenter.Controls;
// Die Elementvorlage "Leere Seite" wird unter https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x407 dokumentiert.
namespace Presenter
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
#pragma warning disable CS1998 // suppress WebView2 prerelease warning
    public sealed partial class MainPage : Page
    {
        public static MainPage Current;
        public static Frame MainFrame = null;

        // Binding for XAML AdaptiveTrigger
        //private double NavigationViewControlCompactModeThresholdWidth { get { return NavigationViewControl.CompactModeThresholdWidth; } }

        // Custom Title Bar
        private CoreApplicationViewTitleBar coreTitleBar;

        public muxc.NavigationView NavigationView
        {
            get { return NavigationViewControl; }
        }

        // Visual Tree Search eg. for Header from the NavigationViewControl
        public PageHeader PageHeader
        {
            get
            {
                return UIHelper.GetDescendantsOfType<PageHeader>(NavigationViewControl).FirstOrDefault();
            }
        }

        public MainPage()
        {
            this.InitializeComponent();

            #region Custom Title Bar
            // Hide default title bar.
            coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            // Set caption buttons background to transparent.
            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;

            // Set XAML element as a drag region.
            Window.Current.SetTitleBar(AppTitleBar);

            // Register a handler for when the size of the overlaid caption control changes.
            coreTitleBar.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;

            // Register a handler for when the title bar visibility changes.
            // For example, when the title bar is invoked in full screen mode.
            coreTitleBar.IsVisibleChanged += CoreTitleBar_IsVisibleChanged;

            // Register a handler for when the window activation changes.
            Window.Current.CoreWindow.Activated += CoreWindow_Activated;
            #endregion

            ContentFrame.Focus(FocusState.Keyboard);

            Current = this;

            Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated += WindowKeyboardHook;

            this.Loaded += MainPage_Loaded;
        }

        private void WindowKeyboardHook(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (args.EventType.ToString().Contains("Down"))
            {
                // Capture STRG + F Keystrokes on the DOWN Event
                var strg = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                if (strg.HasFlag(CoreVirtualKeyStates.Down))
                {
                    switch (args.VirtualKey)
                    {
                        case VirtualKey.F:
                            CtrlF_Invoked();
                            break;
                    }
                }
            }
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTitleBarDragArea();
        }

        #region Custom Title Bar Events / Functions
        private void CoreWindow_Activated(CoreWindow sender, WindowActivatedEventArgs args)
        {
            // Change App Bar Background and Foreground color when window loses focus | match system colors
            UISettings settings = new UISettings();
            if (args.WindowActivationState == CoreWindowActivationState.Deactivated)
            {
                AppTitle.Foreground = (SolidColorBrush)Application.Current.Resources["AppTitleBarInactiveForegroundBrush"];
                AppTitlePreview.Foreground = (SolidColorBrush)Application.Current.Resources["AppTitleBarPreviewInactiveForegroundBrush"];
            }
            else
            {
                AppTitle.Foreground = (SolidColorBrush)Application.Current.Resources["AppTitleBarForegroundBrush"];
                AppTitlePreview.Foreground = (SolidColorBrush)Application.Current.Resources["AppTitleBarPreviewForegroundBrush"];
            }
        }

        private void CoreTitleBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
        {
            if (sender.IsVisible)
            {
                AppTitleBar.Visibility = Visibility.Visible;
            }
            else
            {
                AppTitleBar.Visibility = Visibility.Collapsed;
            }
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            UpdateTitleBarDragArea();
            // Get the size of the caption controls and set padding.
            //LeftPaddingColumn.Width = new GridLength(coreTitleBar.SystemOverlayLeftInset);
            //RightPaddingColumn.Width = new GridLength(coreTitleBar.SystemOverlayRightInset);
        }

        private void UpdateTitleBarDragArea()
        {
            if (MainPage.Current.PageHeader != null)
            {
                if (NavigationViewControl.DisplayMode == muxc.NavigationViewDisplayMode.Minimal)
                {
                    //Resources["NavigationViewHeaderMargin"] = new Thickness(30, 15, 0, 0);
                    MainPage.Current.AppTitleBar.Margin = new Thickness(80, 0, 0, 0);
                }
                else
                {
                    //Resources["NavigationViewHeaderMargin"] = new Thickness(0, 15, 0, 0);
                    MainPage.Current.AppTitleBar.Margin = new Thickness(48, 0, 0, 0);
                    //MainPage.Current.PageHeader.Margin = new Thickness(0,15,0,0);
                }
            }
        }
        #endregion

        #region Navigation Events / Functions
        private void ContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        // List of ValueTuple holding the Navigation Tag and the relative Navigation Page
        private readonly List<(string Tag, Type Page)> _pages = new List<(string Tag, Type Page)>
        {
            ("slides_page", typeof(SlidesPage)),
            ("bible_page", typeof(BiblePage)),
            ("song_page", typeof(SongPage)),
            ("picture_page", typeof(PicturePage)),
            ("powerpoint_page", typeof(PowerPointPage)),
        };

        private void NavigationViewControl_Loaded(object sender, RoutedEventArgs e)
        {
            // NavigationViewControl doesn't load any page by default, so load home page.
            NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[0];
            // If navigation occurs on SelectionChanged, this isn't needed.
            // Because we use ItemInvoked to navigate, we need to call Navigate
            // here to load the slides page.
            NavigationViewControl_Navigate("slides_page", new Windows.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());

            // Listen to the window directly so the app responds
            // to accelerator keys regardless of which element has focus.
            Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated +=
                CoreDispatcher_AcceleratorKeyActivated;

            Window.Current.CoreWindow.PointerPressed += CoreWindow_PointerPressed;

            SystemNavigationManager.GetForCurrentView().BackRequested += System_BackRequested;
        }

        private void NavigationViewControl_ItemInvoked(muxc.NavigationView sender, muxc.NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked == true)
            {
                NavigationViewControl_Navigate("settings_page", args.RecommendedNavigationTransitionInfo);
            }
            else if (args.InvokedItemContainer != null)
            {
                // Is null when NavItem is grouped together
                if (args.InvokedItemContainer.Tag != null)
                {
                    var navItemTag = args.InvokedItemContainer.Tag.ToString();
                    NavigationViewControl_Navigate(navItemTag, args.RecommendedNavigationTransitionInfo);
                }
                else
                {
                    // possible grouped NavItems
                }
            }
        }

        private void NavigationViewControl_Navigate(string navItemTag, NavigationTransitionInfo transitionInfo)
        {
            Type _page = null;
            if (navItemTag == "settings_page")
            {
                _page = typeof(SettingsPage);
            }
            else
            {
                var item = _pages.FirstOrDefault(p => p.Tag.Equals(navItemTag));
                _page = item.Page;
            }
            // Get the page type before navigation so you can prevent duplicate
            // entries in the backstack.
            var preNavPageType = ContentFrame.CurrentSourcePageType;

            // Only navigate if the selected page isn't currently loaded.
            if (!(_page is null) && !Type.Equals(preNavPageType, _page))
            {
                ContentFrame.Navigate(_page, null, transitionInfo);
            }
        }

        private void NavigationViewControl_BackRequested(muxc.NavigationView sender, muxc.NavigationViewBackRequestedEventArgs args)
        {
            TryGoBack();
        }

        private void CoreDispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs e)
        {
            // When Alt+Left are pressed navigate back
            if (e.EventType == CoreAcceleratorKeyEventType.SystemKeyDown
                && e.VirtualKey == VirtualKey.Left
                && e.KeyStatus.IsMenuKeyDown == true
                && !e.Handled)
            {
                e.Handled = TryGoBack();
            }
        }

        private void System_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = TryGoBack();
            }
        }

        private void CoreWindow_PointerPressed(CoreWindow sender, PointerEventArgs e)
        {
            // Handle mouse back button.
            if (e.CurrentPoint.Properties.IsXButton1Pressed)
            {
                e.Handled = TryGoBack();
            }
        }

        private bool TryGoBack()
        {
            if (!ContentFrame.CanGoBack)
                return false;

            // Don't go back if the nav pane is overlayed.
            if (NavigationViewControl.IsPaneOpen &&
                (NavigationViewControl.DisplayMode == muxc.NavigationViewDisplayMode.Compact ||
                 NavigationViewControl.DisplayMode == muxc.NavigationViewDisplayMode.Minimal))
                return false;

            ContentFrame.GoBack();
            return true;
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            NavigationViewControl.IsBackEnabled = ContentFrame.CanGoBack;

            if (ContentFrame.SourcePageType == typeof(SettingsPage))
            {
                // SettingsItem is not part of NavigationViewControl.MenuItems, and doesn't have a Tag.
                NavigationViewControl.SelectedItem = null;
                NavigationViewControl.Header = (string)Application.Current.Resources["NavItemSettings"];
            }
            else if (ContentFrame.SourcePageType != null)
            {
                // Unselect SettingsButton if previously selected
                if (SettingsButton.IsChecked == true) SettingsButton.IsChecked = false;

                var item = _pages.FirstOrDefault(p => p.Page == e.SourcePageType);

                NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems
                    .OfType<muxc.NavigationViewItem>()
                    .First(n => n.Tag.Equals(item.Tag));

                NavigationViewControl.Header =
                    ((muxc.NavigationViewItem)NavigationViewControl.SelectedItem)?.Content?.ToString();
            }
        }

        private void NavigationViewControl_DisplayModeChanged(muxc.NavigationView sender, muxc.NavigationViewDisplayModeChangedEventArgs args)
        {
            var navigationView = sender;
            Debug.WriteLine(sender.DisplayMode);
            MainPage.Current.AppTitle.Visibility = navigationView.DisplayMode == muxc.NavigationViewDisplayMode.Minimal ? Visibility.Collapsed : Visibility.Visible;
            MainPage.Current.AppTitlePreview.Visibility = MainPage.Current.AppTitle.Visibility;

            UpdateTitleBarDragArea();
            UpdateSettingsThemeStack();
        }

        private void NavigationViewControl_PaneClosing(muxc.NavigationView sender, muxc.NavigationViewPaneClosingEventArgs args)
        {
            UpdateSettingsThemeStack();
        }

        private void NavigationViewControl_PaneOpening(muxc.NavigationView sender, object args)
        {
            UpdateSettingsThemeStack();
        }

        private void UpdateSettingsThemeStack()
        {
            // Change the orientation of ThemeButtonStack depending on the pane status
            if (NavigationViewControl.IsPaneOpen)
            {
                // if Pane open
                SettingsThemeStack.Orientation = Orientation.Horizontal;
                ThemeButtonStack.Orientation = Orientation.Horizontal;
                ThemeButtonStack.Spacing = 7;

                SettingsThemeStack.Spacing = 18;

                // Reverse children order in StackPanel if first children is the ThemeButton
                if (SettingsThemeStack.Children[0].GetType() != typeof(LockableToggleButton))
                    SettingsThemeStack.Children.ReverseChildren();

                // Optimize for Horizontal Layout
                SettingsButton.Margin = new Thickness(5, 0, 15, 5);
                SettingsButton.Padding = new Thickness(10, 7, 15, 7);

                ThemeButton.Margin = new Thickness(4, 0, 15, 5);
                ThemeButton.Padding = new Thickness(7, 6, 7, 6);

                // Prevent button shrinkage
                ThemeButton.MinWidth = 82;
            }
            else
            {
                // if Pane closed
                SettingsThemeStack.Orientation = Orientation.Vertical;
                ThemeButtonStack.Orientation = Orientation.Vertical;
                ThemeButtonStack.Spacing = 4;

                SettingsThemeStack.Spacing = 3;

                // Reverse children order in StackPanel if first children is the SettingsButton
                if (SettingsThemeStack.Children[0].GetType() == typeof(LockableToggleButton))
                    SettingsThemeStack.Children.ReverseChildren();

                // Optimize for Vertical Layout
                SettingsButton.Margin = new Thickness(5, 0, 5, 5);
                SettingsButton.Padding = new Thickness(10, 7, 0, 7);

                ThemeButton.Margin = new Thickness(5, 0, 5, 5);
                ThemeButton.Padding = new Thickness(5, 6, 5, 6);

                // Prevent button shrinkage
                ThemeButton.MinWidth = 43;
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Collapse NavigationViewControl Pane if DisplayMode is Minimal or Compact
            if (NavigationViewControl.DisplayMode != muxc.NavigationViewDisplayMode.Expanded)
            {
                if (NavigationViewControl.IsPaneOpen) NavigationViewControl.IsPaneOpen = false;
            }

            // Feature: go back to previous page when clicking again on the SettingsButton
            if (SettingsButton.IsChecked == true)
            {
                // check if Navigation can go back -> if true unselect SettingsButton
                if (TryGoBack() == true)
                {
                    SettingsButton.IsChecked = false;
                    return;
                }
                // if not select the SettingsButton
                else SettingsButton.IsChecked = true;
            }
            else
            {
                SettingsButton.IsChecked = true;
            }

            NavigationViewControl_Navigate("settings_page", new Windows.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
        }

        private void ThemeButtonFlyout_Click(object sender, RoutedEventArgs e)
        {
            var menuFlyoutItem = (MenuFlyoutItem)sender;

            switch (menuFlyoutItem?.Tag?.ToString())
            {
                case "dark_mode": { ThemeHelper.Theme = ElementTheme.Dark; break; }
                case "light_mode": { ThemeHelper.Theme = ElementTheme.Light; break; }
                case "system_mode": { ThemeHelper.Theme = ElementTheme.Default; break; }
            }
        }

        public void UpdateThemeButton(ElementTheme theme)
        {
            switch (theme)
            {
                case ElementTheme.Light:
                    {
                        ThemeDetailText.Text = "Hell";
                        ThemeFontIcon.Glyph = "\ue706";
                        break;
                    }

                case ElementTheme.Dark:
                    {
                        ThemeDetailText.Text = "Dunkel";
                        ThemeFontIcon.Glyph = "\ue708";
                        break;
                    }

                case ElementTheme.Default:
                    {
                        // Get System Theme information
                        if(ThemeHelper.AppTheme == ElementTheme.Dark)
                        {
                            // Dark mode - set by system
                            ThemeFontIcon.Glyph = "\ue708";
                        }
                        else
                        {
                            // System mode - set by system
                            ThemeFontIcon.Glyph = "\ue706";
                        }
                        ThemeDetailText.Text = "System";
                        break;
                    }
            }
        }

        public void CtrlF_Invoked()
        {
            // Open the NavigationViewControl Pane if it is collapsed
            NavigationViewControl.IsPaneOpen = true;

            this.GlobalSearch.Focus(FocusState.Programmatic);
        }
        #endregion
    }
}
