﻿using System.Diagnostics;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.LockScreen;
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
			Debug.WriteLine(exception.Exception.StackTrace);
			exception.Handled = true;
		}

		protected override void OnLaunched(LaunchActivatedEventArgs args)
		{
			LockApplicationHost host = LockApplicationHost.GetForCurrentView();
			Frame rootFrame = new Frame();
			Window.Current.Content = rootFrame;
			rootFrame.Navigate(typeof(MainPage), args.Arguments);
			Window.Current.Activate();
		}
	}
}

