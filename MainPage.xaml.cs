using OPG.Signage.APIServer;
using OPG.Reception.Printing;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace OPG.Signage.Screen
{
	public sealed partial class MainPage : Page
	{
		private string url = "http://dev.opgs.org/signage/screen/";
		private string cUrl = "http://dev.opgs.org/signage/screen/";
		private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
		private PrintJob print;
		private static int APIVersion = 5;
		private string APIDescription = (new StreamReader(@"APIDescriptor.html")).ReadToEnd();		
		private string API4replyformat = "{{\"os\":\"{0}\", \"wifi\":\"{1}\", \"ssid\":\"{2}\", \"url\":\"{3}\"}}";
		private string API4Format = "{{\"apiversion\":\"" + APIVersion + "\",\"name\":\"" + Info.Network.HostName + "\",\"version\":\"" + Info.APP.AppVersion + "\",\"reply\":{0}}}";

		private void RefreshTick()
		{
			if (cUrl != url)
			{
				if (cUrl == "refresh")
				{
					ScreenView.Refresh();
					lock (cUrl) cUrl = url;
				}
				else
				{
					ScreenView.Navigate(new Uri(cUrl));
					localSettings.Values["url"] = cUrl;
					url = cUrl;
				}
			}
		}

		private void StartRefreshTimer()
		{
			DispatcherTimer refreshTimer = new DispatcherTimer
			{
				Interval = new TimeSpan(0, 0, 30)
			};
			refreshTimer.Tick += (_, __) => RefreshTick();
			refreshTimer.Start();
		}

		private void SetupServer()
		{
			Debug.WriteLine("Starting server");

			JSONServerSSL webserverssl = new JSONServerSSL(Info.Network.IP, false, 8081)
			{
				RegUrl = new Uri("http://dev.opgs.org/signage/index.php"),
				ReplyFormat = API4Format
			};
			webserverssl.Get("/api", (_) => 
			{
				return new JSONReply(APIDescription, "text/html");
			});
			webserverssl.Get("/api/os", (_) => 
			{
				return new JSONReply(Info.OS.Version);
			});
			webserverssl.Get("/api/5", (_) =>
			{
				return new JSONReply(String.Format(API4replyformat, Info.OS.Version, Info.Network.IsWifi() ? Info.Network.GetWifiStrength().ToString() : "false", Info.Network.IsWifi() ? Info.Network.GetWifiAPSSID() : "false", cUrl));
			});
			webserverssl.Get("/api/url", (url) =>
			{
				if (url.Length > 8)
				{
					int pos = url.IndexOf('?', 8);
					cUrl = url.Substring(pos + 2);
				}

				return new JSONReply(cUrl);
			});
			webserverssl.Get("/api/refresh", (_) =>
			{
				lock (cUrl) cUrl = "refresh";
				return new JSONReply("refresh - ok");
			});

			Task.Run(async () => 
			{
				await webserverssl.StartAsync();
			});
		}

		private void ScreenView_NewWindowRequested(WebView sender, WebViewNewWindowRequestedEventArgs args)
		{
			if (args.Uri.Query.Contains("api5"))
			{
			}
			else if (args.Uri.Query.Contains("print"))
			{
				sender.Navigate(args.Uri);
				WebViewBrush wvBrush = new WebViewBrush();
				wvBrush.SetSource(sender);
				wvBrush.Redraw();
				printRect.Fill = wvBrush;
				print = new PrintJob(printRect);
				print.Print();
				sender.Navigate(new Uri(cUrl));
				args.Handled = true;
			}
			else
			{
				sender.Navigate(args.Uri);
				args.Handled = true;
			}
		}

		public MainPage()
		{
			InitializeComponent();

			if (localSettings.Values.ContainsKey("url"))
			{
				cUrl = localSettings.Values["url"].ToString();
			} else {
				localSettings.Values["url"] = cUrl;
			}

			ScreenView.Navigate(new Uri(cUrl));
			ScreenView.NewWindowRequested += ScreenView_NewWindowRequested;

			Debug.WriteLine("Browser done, server next");

			StartRefreshTimer();
			SetupServer();
		}
	}
}
