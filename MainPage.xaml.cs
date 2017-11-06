using OPG.Signage.APIServer;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace OPG.Signage.Screen
{
    public sealed partial class MainPage : Page
	{
		private string url = "http://signage.opgs.org";
		private string cUrl = "http://signage.opgs.org";
        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

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
            JSONServer webserver = new JSONServer(8081, Info.Network.IP)
            {
                RegUrl = new Uri("https://dev.opgs.org/signage/index.php"),
                ReplyFormat = "{{\"Name\":\"" + Info.Network.HostName + "\",\"Version\":\"" + Info.APP.AppVersion + "\",\"Reply\":\"{0}\"}}"
            };
            webserver.Get("/api/os", (_) => new JSONReply(Info.OS.Version));
            webserver.Get("/api/wifi", (_) => new JSONReply(Info.Network.IsWifi() ? Info.Network.GetWifiStrength().ToString() : "false"));
			webserver.Get("/api/wifi/ssid", (_) => new JSONReply(Info.Network.IsWifi() ? Info.Network.GetWifiAPSSID() : "false"));
            webserver.Get("/api/url", (innerurl) =>
            {
                if (innerurl.Length > 8)
                {
                    lock (cUrl) cUrl = innerurl.Substring(innerurl.IndexOf('?', 8) + 2);
                }

                return new JSONReply(cUrl);
            });
            webserver.Get("/api/refresh", (_) =>
            {
                lock (cUrl) cUrl = "refresh";
                return new JSONReply("refresh - ok");
            });

            Task.Run(async () => await webserver.StartAsync().ConfigureAwait(false));
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

            StartRefreshTimer();
            SetupServer();
		}
	}
}
