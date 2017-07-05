using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace OPG.RPiIOT
{
	public sealed partial class MainPage : Page
	{
		private string url;
		private string cUrl = "http://signage.opgs.org";

		public MainPage()
		{
			InitializeComponent();

			url = cUrl;

			ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
			if (!localSettings.Values.ContainsKey("url"))
			{
				localSettings.Values["url"] = cUrl;
			} else {
				cUrl = localSettings.Values["url"].ToString();
			}

			ScreenView.DOMContentLoaded += (s, e) => { ScreenView.Visibility = Visibility.Visible; };
			ScreenView.Navigate(new Uri(cUrl));

			DispatcherTimer refreshTimer = new DispatcherTimer
			{
				Interval = new TimeSpan(0, 0, 1)
			};
			refreshTimer.Tick += (s, e) => {
				if (cUrl != url)
				{
					ScreenView.Visibility = Visibility.Visible;
					if (cUrl == "refresh")
					{
						ScreenView.Refresh();
						cUrl = url;
					}
					else
					{
						ScreenView.Navigate(new Uri(cUrl));
						localSettings.Values["url"] = cUrl;
						url = cUrl;
					}
				}
			};
			refreshTimer.Start();

			APIServer webserver = new APIServer(8081, Info.Network.GetLocalIp(), "https://dev.opgs.org/signage/index.php", "{{\"Name\":\"" + Info.Network.HostName + "\",\"Version\":\"" + Info.APP.AppVersion + "\",\"Reply\":\"{0}\"}}");

			webserver.Get("/api/os", (url) => { return Info.OS.Version; });
			webserver.Get("/api/wifi", (url) => { return (Info.Network.IsWifi() ? Info.Network.GetWifiStrength().ToString() : "false"); });
			webserver.Get("/api/url", (url) =>
			{
				if (url.Length > 8)
				{
					int pos = url.IndexOf('?', 8);
					cUrl = url.Substring(pos + 2);
				}

				return cUrl;
			});
			webserver.Get("/api/refresh", (url) =>
			{
				cUrl = "refresh";
				return "refresh - ok";
			});

			Task.Run(async () =>
			{
				await webserver.StartAsync();
			});
		}
	}
}
