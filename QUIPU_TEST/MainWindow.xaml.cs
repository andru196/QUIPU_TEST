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
		int pointer;
		int max;
		object lockerUI;
		object lockerNum;
		int total;
		bool MULTY_THREAD = true; //Константа, определяющая, будет ли программа загружать странички в нескольких потоках

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
				if (linkList.Count > 0)
				{	
					max = 0;
					total = 0;
					lockerUI = new object();
					lockerNum = new object();

					var tasks = new List<Task>();
					var prcs = Environment.ProcessorCount > 2 ? Environment.ProcessorCount - 1 : 1;
					if (prcs > linkList.Count)
						prcs = linkList.Count;
					if (MULTY_THREAD)
					{
						pointer = 0;
						
						for (var i = 0; i < prcs; i++)
							tasks.Add(SumATagsMultyThreadAsync(cts.Token));
						await Task.WhenAll(tasks.ToArray());
					}
					else
						await SumATagsSingleThreadAsync(cts.Token);
				}
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
				DisplayWhenEnd();

			}
		}

		async Task SumATagsSingleThreadAsync(CancellationToken ct)
		{
			max = 0;
			total = 0;
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
						DisplayResults(url.AbsoluteUri, count);
						if (count > max)
						{
							max = count;
							Displaymax(url.AbsoluteUri, count);
						}
					}
				}
			}
		}


		async Task SumATagsMultyThreadAsync(CancellationToken ct)
		{
			using (var client = new HttpClient())
			{
				while (Thread.VolatileRead(ref pointer) < linkList.Count)
				{
					int i;
					lock (lockerNum)
					{
						i = pointer;
						pointer =  i + 1;
					}
					if (linkList.Count <= i)
						break;

					FileNameTextBox.Background = new LinearGradientBrush
					{
						GradientStops = new GradientStopCollection
						{
							new GradientStop(Colors.Green, 0),
							new GradientStop(Colors.White, (double)Thread.VolatileRead(ref pointer) / (double)linkList.Count)
						},
						StartPoint = new Point(0, 0.5),
						EndPoint = new Point(1, 0.5)
					};

					HttpResponseMessage response = null;
					var url = linkList[i];
					try
					{
						response = await client.GetAsync(url, ct);
					}
					catch (HttpRequestException)
					{
						DisplayHttpError(url.AbsoluteUri, "не достучаться");
						continue;
					}

					if (response.Content.Headers.ContentType.MediaType.Contains("html"))
					{
						var html = new HtmlDocument();
						html.LoadHtml(await response.Content.ReadAsStringAsync());
						
						var count = html.DocumentNode.Descendants("a").Count();
						lock (lockerNum)
							total += count;
						DisplayResults(html.DocumentNode.SelectSingleNode("html/head/title")?.InnerText ?? url.AbsoluteUri, count);
						
						if (count > Thread.VolatileRead(ref max))
						{
							Thread.VolatileWrite(ref max, count);
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

		void DisplayWhenEnd()
		{
			TotalTextBlock.Text = $"Результат: {total}";
		}

		void DisplayHttpError(string url, string error)
		{
			lock (lockerUI)
				resultTextBlock.Text += string.Format("\n{2}\n{0,-60} ошибка при обращении {1}\n{2}", url, error, new string('-', 30));
		}
		void DisplayResults(string url, int count)
		{
			var displayNameOrUrl = url.Replace("http://", "").Replace("https://", "").Trim();
			lock (lockerUI)
			{
				TotalTextBlock.Text = $"Результат: {Thread.VolatileRead(ref total)}";
				resultTextBlock.Text += string.Format("\n{0,-100} {1,4}", displayNameOrUrl, count);
			}
		}

		void Displaymax(string url, int count)
		{
			lock (lockerUI)
				RecordTextBlock.Text = string.Format("Текущий макисмум\n{0,-60} {1,8}", url, count);
		}
	}
}
