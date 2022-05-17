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

        private double NavigationViewControlCompactModeThresholdWidth { get { return NavigationViewControl.CompactModeThresholdWidth; } }

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

            this.Loaded += MainPage_Loaded;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTitleBarDragArea();
        }

        #region Custom Title Bar Events
        private void CoreWindow_Activated(CoreWindow sender, WindowActivatedEventArgs args)
        {
            // Change App Bar Background and Foreground color when window loses focus | match system colors
            UISettings settings = new UISettings();
            if (args.WindowActivationState == CoreWindowActivationState.Deactivated)
            {
                AppTitle.Foreground =
                   new SolidColorBrush(settings.UIElementColor(UIElementType.GrayText));
                //AppTitleBar.Background = new SolidColorBrush(settings.UIElementColor(UIElementType.Window));
            }
            else
            {
                AppTitle.Foreground =
                   new SolidColorBrush(settings.UIElementColor(UIElementType.WindowText));
                
                AppTitleBar.Background = new SolidColorBrush(Colors.Transparent);
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
        #endregion

        #region Navigation Event/ Methods

        private void OnPaneDisplayModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            var navigationView = sender as muxc.NavigationView;
            MainPage.Current.AppTitleBar.Visibility = navigationView.PaneDisplayMode == muxc.NavigationViewPaneDisplayMode.Top ? Visibility.Collapsed : Visibility.Visible;
        }


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
            // Add handler for ContentFrame navigation.
            ContentFrame.Navigated += On_Navigated;

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

        private void On_Navigated(object sender, NavigationEventArgs e)
        {
            NavigationViewControl.IsBackEnabled = ContentFrame.CanGoBack;

            if (ContentFrame.SourcePageType == typeof(SettingsPage))
            {
                // SettingsItem is not part of NavigationViewControl.MenuItems, and doesn't have a Tag.
                NavigationViewControl.SelectedItem = (muxc.NavigationViewItem)NavigationViewControl.SettingsItem;
                NavigationViewControl.Header = (string)Application.Current.Resources["SettingsHeader"];
            }
            else if (ContentFrame.SourcePageType != null)
            {
                var item = _pages.FirstOrDefault(p => p.Page == e.SourcePageType);

                NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems
                    .OfType<muxc.NavigationViewItem>()
                    .First(n => n.Tag.Equals(item.Tag));

                NavigationViewControl.Header =
                    ((muxc.NavigationViewItem)NavigationViewControl.SelectedItem)?.Content?.ToString();
            }
        }
        #endregion

        private void CtrlF_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            // Open the Pane in case the navwie is collapsed
            NavigationViewControl.IsPaneOpen = true;

            this.GlobalSearch.Focus(FocusState.Programmatic);
        }

        private void NavigationViewControl_DisplayModeChanged(muxc.NavigationView sender, muxc.NavigationViewDisplayModeChangedEventArgs args)
        {
            var navigationView = sender;
            Debug.WriteLine(sender.DisplayMode);
            MainPage.Current.AppTitle.Visibility = navigationView.DisplayMode == muxc.NavigationViewDisplayMode.Minimal ? Visibility.Collapsed : Visibility.Visible;

            UpdateTitleBarDragArea();
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
    }
}
