using OPG.Signage.APIServer;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Printing;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Printing;
using Windows.UI.Xaml.Shapes;

namespace OPG.Signage.Screen
{
    public sealed partial class MainPage : Page
	{
		private string url = "http://dev.opgs.org/signage/screen/";
		private string cUrl = "http://dev.opgs.org/signage/screen/";
		private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
		private static int APIVersion = 6;	
		private string hwReplyformat = "{{\"board\":\"{0}\", \"processor\":\"{1}\", \"manufacturer\":\"{2}\", \"name\":\"{3}\"}}";
        private string API4replyformat = "{{\"os\":\"{0}\", \"wifi\":\"{1}\", \"ssid\":\"{2}\", \"url\":\"{3}\"}}";
        private string API4Format = "{{\"apiversion\":\"" + APIVersion + "\",\"name\":\"" + Info.Hardware.Network.HostName + "\",\"version\":\"" + Info.APP.AppVersion + "\",\"reply\":{0}}}";
		private PrintManager printMan;
		private PrintDocument printDoc;
		private IPrintDocumentSource printDocSource;
		private Rectangle RectangleToPrint = new Rectangle();

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
                    lock (cUrl) url = cUrl;
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

			JSONServerSSL webserverssl = new JSONServerSSL(new OPGInternalCert(), Info.Hardware.Network.IP, Info.Hardware.Network.HostName, false)
			{
				RegUrl = new Uri("http://dev.opgs.org/signage/index.php"),
				ReplyFormat = API4Format
			};
			webserverssl.Get("/api", (_) => 
			{
				return new JSONReply((new StreamReader(@"APIDescriptor.html")).ReadToEnd(), "text/html");
			});
			webserverssl.Get("/api/os", (_) => 
			{
				return new JSONReply(Info.OS.Version);
			});
            webserverssl.Get("/api/5", (_) => //minimum for 5+
            {
                return new JSONReply(String.Format(API4replyformat, Info.OS.Version, Info.Hardware.Network.IsWifi() ? Info.Hardware.Network.GetWifiStrength().ToString() : "false", Info.Hardware.Network.IsWifi() ? Info.Hardware.Network.GetWifiAPSSID() : "false", cUrl));
            });
            webserverssl.Get("/api/6", (_) =>
			{
				return new JSONReply(String.Format(API4replyformat, Info.OS.Version, Info.Hardware.Network.IsWifi() ? Info.Hardware.Network.GetWifiStrength().ToString() : "false", Info.Hardware.Network.IsWifi() ? Info.Hardware.Network.GetWifiAPSSID() : "false", cUrl));
			});
            webserverssl.Get("/api/hardware", (_) => 
            {
                return new JSONReply(String.Format(hwReplyformat, Info.Hardware.Board, Info.Hardware.CPU, Info.Hardware.Manufacturer, Info.Hardware.ProductName));
            });
            webserverssl.Post("/api/url", (request) =>
			{
				if(request.KVPairs.TryGetValue("url", out string newUrl))
				{
					try
					{
						new Uri(newUrl); //check for valid url, throws exception if not
						lock (cUrl) cUrl = newUrl;
					}
					catch(Exception e)
					{
						throw new HTTPException(Windows.Web.Http.HttpStatusCode.BadRequest, e.Message);
					}
				}

				return new JSONReply(cUrl);
			});
			webserverssl.Get("/api/refresh", (_) =>
			{
				lock (cUrl) cUrl = "refresh";
				return new JSONReply("refresh - ok");
			});
			webserverssl.Post("/api/print", (request) => 
			{
    			if(request.KVPairs.TryGetValue("idcard", out string idbase64))
				{
					Task.Run(async () =>
					{
						await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => 
						{
                            try
                            {
                                PrintJpeg(idbase64);
                            }
                            catch (Exception)
                            {
                                Task.Run(async () =>
                                {
                                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                                    {
                                        await ShowDialog("Printing error", "\nSorry, printing can' t proceed at this time.\n", "OK");
                                    });
                                });
                            }
						});
					});
                }
                else
                {
                    return new JSONReply("no print job specified", "text/html", Windows.Web.Http.HttpStatusCode.BadRequest);
                }

				return new JSONReply("ok");
			});

			Task.Run(async () => 
			{
				await webserverssl.StartAsync();
			});
		}

		private void ScreenView_PermissionRequested(WebView sender, WebViewPermissionRequestedEventArgs args)
		{
			if (args.PermissionRequest.PermissionType == WebViewPermissionType.Media)
			{
				args.PermissionRequest.Allow();
			}
		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			RectangleToPrint = printRect;
			RectangleToPrint.Height = 240; /*bitmapImage.PixelHeight;*/
			RectangleToPrint.Width = 340; /*bitmapImage.PixelWidth;*/

			// Register for PrintTaskRequested event
			printMan = PrintManager.GetForCurrentView();
			printMan.PrintTaskRequested += PrintTaskRequested;

			// Build a PrintDocument and register for callbacks
			printDoc = new PrintDocument();
			printDocSource = printDoc.DocumentSource;
			printDoc.Paginate += Paginate;
			printDoc.GetPreviewPage += GetPreviewPage;
			printDoc.AddPages += AddPages;
		}

		public BitmapImage AsBitmapImage(byte[] byteArray)
		{
			if (byteArray != null)
			{
				using (var stream = new InMemoryRandomAccessStream())
				{
					stream.WriteAsync(byteArray.AsBuffer()).GetResults();
					// I made this one synchronous on the UI thread;
					// this is not a best practice.
					var image = new BitmapImage();
					stream.Seek(0);
					image.SetSource(stream);
					return image;
				}
			}

			return null;
		}

		public void PrintJpeg(string dataIn)
		{
			Debug.WriteLine("Start print....");
			Byte[] binData = Convert.FromBase64String(Regex.Match(dataIn, @"data:image/(?<type>.+?),(?<data>.+)").Groups["data"].Value);
			Debug.WriteLine("Converted....");
			BitmapImage bitmapImage = AsBitmapImage(binData);
			Debug.WriteLine("Source set....");
			ImageBrush ib = new ImageBrush() { ImageSource = bitmapImage };
			RectangleToPrint.Fill = ib;
			Debug.WriteLine("Brush set....");
			PrintButtonClick(null, null);
		}

        private async Task<ContentDialog> ShowDialog(string title, string content, string button = "OK")
        {
            ContentDialog dialog = new ContentDialog()
            {
                Title = title,
                Content = content,
                PrimaryButtonText = button
            };
            await dialog.ShowAsync();
            return dialog;
        }

        private async Task ShowPrintingErrorDialog()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await ShowDialog("Printing error", "\nSorry, printing can' t proceed at this time.\n");
            });
        }

		private async void PrintButtonClick(object sender, RoutedEventArgs e)
		{
			if (PrintManager.IsSupported())
			{
				try
				{
					// Show print UI
					await PrintManager.ShowPrintUIAsync();
				}
				catch (Exception)
				{
                    // Printing cannot proceed at this time
                    await ShowPrintingErrorDialog();
				}
			}
			else
			{
                // Printing is not supported on this device
                await ShowPrintingErrorDialog();
            }
		}

		private void PrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
		{
			// Create the PrintTask.
			// Defines the title and delegate for PrintTaskSourceRequested
			var printTask = args.Request.CreatePrintTask("Print", PrintTaskSourceRequrested);

			// Handle PrintTask.Completed to catch failed print jobs
			printTask.Completed += PrintTaskCompleted;
		}

		private void PrintTaskSourceRequrested(PrintTaskSourceRequestedArgs args)
		{
			// Set the document source.
			args.SetSource(printDocSource);
		}

		private void Paginate(object sender, PaginateEventArgs e)
		{
			// As I only want to print one Rectangle, so I set the count to 1
			printDoc.SetPreviewPageCount(1, PreviewPageCountType.Final);
		}

		private void GetPreviewPage(object sender, GetPreviewPageEventArgs e)
		{
			// Provide a UIElement as the print preview.
			printDoc.SetPreviewPage(e.PageNumber, RectangleToPrint);
		}

		private void AddPages(object sender, AddPagesEventArgs e)
		{
			printDoc.AddPage(RectangleToPrint);

			// Indicate that all of the print pages have been provided
			printDoc.AddPagesComplete();
		}

		private async void PrintTaskCompleted(PrintTask sender, PrintTaskCompletedEventArgs args)
		{
			// Notify the user when the print operation fails.
			if (args.Completion == PrintTaskCompletion.Failed)
			{
				await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
				{
					ContentDialog noPrintingDialog = new ContentDialog()
					{
						Title = "Printing error",
						Content = "\nSorry, failed to print.",
						PrimaryButtonText = "OK"
					};
					await noPrintingDialog.ShowAsync();
				});
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
			ScreenView.PermissionRequested += ScreenView_PermissionRequested;

			Debug.WriteLine("Browser done, server next");

			StartRefreshTimer();
			SetupServer();
		}
	}
}
