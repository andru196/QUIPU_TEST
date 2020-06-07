using HtmlAgilityPack;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Linq;

namespace QUIPU_TEST
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		CancellationTokenSource cts;
		List<Uri> linkList;

		public MainWindow()
		{
			ServicePointManager.ServerCertificateValidationCallback = (a, b, c, d) => true; 
			InitializeComponent();
			FileNameTextBox.Text = Environment.CurrentDirectory;
		}

		void SelectFileButton_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new OpenFileDialog
			{
				Filter = "Text files(*.txt)|*.txt|All files(*.*)|*.*"
			};
			if (dialog.ShowDialog() == true)
			{
				FileNameTextBox.Text = dialog.FileName;
			}
		}

		async Task GetLinksFromFileAsync(string fileName)
		{
			if (!File.Exists(fileName))
				throw new Exception($"Файл {fileName} не существет");
			using (var stream = new StreamReader(fileName))
			{
				var line = "";
				do
				{
					line = (await stream.ReadLineAsync())?.Trim()?.Replace('\t', ' ');
					if (string.IsNullOrEmpty(line))
						continue;
					foreach (var uri in line.Split(' '))
						if (Uri.IsWellFormedUriString(uri, UriKind.Absolute) && uri.ToLower().StartsWith("http"))
							try
							{
								linkList.Add(new Uri(uri));
							}
							catch { }
				} while (line != null);
			}
		}

		async void ScanButton_Click(object sender, RoutedEventArgs e)
		{
			linkList = new List<Uri>();
			var file = FileNameTextBox.Text;
			resultTextBlock.Text = "";
			cts = new CancellationTokenSource();
			//cts.CancelAfter(10000);
			ScanButton.Content = "Отмена";
			ScanButton.Click -= ScanButton_Click;
			ScanButton.Click += ButtonClickCancel;

			try
			{
				//Допускаем что нам сразу дали одну ссыылку, вместо файла
				if (Uri.IsWellFormedUriString(file, UriKind.Absolute))
					linkList.Add(new Uri(file));
				else //Идём по предусмотренному пути
					await GetLinksFromFileAsync(file);
				await SumATagsAsync(cts.Token);
			}
			catch (OperationCanceledException)
			{
				resultTextBlock.Text += "\r\nDownload canceled.\r\n";
			}
			catch (Exception ex)
			{
				resultTextBlock.Text = ex.Message;
			}
			finally
			{
				ScanButton.Click -= ButtonClickCancel;
				ScanButton.Click += ScanButton_Click;
				ScanButton.Content = "Начать";
				FileNameTextBox.Background = Brushes.White;
			}
		}

		async Task SumATagsAsync(CancellationToken ct)
		{
			var max = 0;
			var total = 0;
			using (var client = new HttpClient())
			{
				var i = 0;
				foreach (var url in linkList)
				{
					FileNameTextBox.Background = new LinearGradientBrush
					{
						GradientStops = new GradientStopCollection
						{
							new GradientStop(Colors.Green, 0),
							new GradientStop(Colors.White, (double)i++ / (double)linkList.Count)
						},
						StartPoint = new Point(0, 0.5),
						EndPoint = new Point(1, 0.5)
					};
					HttpResponseMessage response = null;
					try
					{
						response = await client.GetAsync(url, ct);
					}
					catch (HttpRequestException)
					{
						DisplayHttpError(url.AbsoluteUri, "не достучаться");
						continue;
					}


					if (response.Content.Headers.ContentType.MediaType.Contains("text/html"))
					{
						var html = new HtmlDocument();
						html.LoadHtml(await response.Content.ReadAsStringAsync());
						var count = html.DocumentNode.Descendants("a").Count();
						total += count;
						foreach (var h in html.DocumentNode.ChildNodes) ;
						DisplayResults(url.AbsoluteUri, count, total);
						if (count > max)
						{
							max = count;
							Displaymax(url.AbsoluteUri, count);
						}
					}
				}
			}
		}

		void ButtonClickCancel(object sender, RoutedEventArgs e)
		{
			if (cts != null)
			{
				cts.Cancel();
				ScanButton.Click -= ButtonClickCancel;
				ScanButton.Click += ScanButton_Click;
				ScanButton.Content = "Начать";
			}
		}

		void DisplayHttpError(string url, string error)
		{
			resultTextBlock.Text += string.Format("\n{0,-60} ошибка при обращении {1}", url, error);
		}
			void DisplayResults(string url, int count, int total)
		{
			var displayURL = url.Replace("http://", "").Replace("https://", "");
			TotalTextBlock.Text = $"Результат: {total}";
			resultTextBlock.Text += string.Format("\n{0,-60} {1,8}", displayURL, count);
		}

		void Displaymax(string url, int count)
		{
			RecordTextBlock.Text = string.Format("Текущий макисмум\n{0,-60} {1,8}", url, count);
		}
	}
}
