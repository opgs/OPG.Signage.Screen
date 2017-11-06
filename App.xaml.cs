using System.Diagnostics;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace OPG.Signage.Screen
{
    public sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            UnhandledException += OnUnhandledException;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs exception)
        {
            Debug.WriteLine(exception.Message);
            exception.Handled = true;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
			if(args != null)
			{
				Frame rootFrame = new Frame();
				Window.Current.Content = rootFrame;
				rootFrame.Navigate(typeof(MainPage), args.Arguments);
				Window.Current.Activate();
			}
        }
    }
}

