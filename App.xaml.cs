using Windows.ApplicationModel.Activation;
using Windows.Foundation.Diagnostics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace OPG.RPiIOT
{
    sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            UnhandledException += App_UnhandledException;
        }

        private void App_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = new Frame();
            Window.Current.Content = rootFrame;
            rootFrame.Navigate(typeof(MainPage), e.Arguments);
            Window.Current.Activate();
        }
    }
}

