using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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


/*
	Необходимо разработать клиентское приложение, которое:
·         Читает из файла список Url
·         Загружает соответствующие html страницы по Url
·         Находит на страницах все тэги <a> и считает их количество
·         После завершения обработки выводит список прочитанных Url  и количество тэгов <a>
	Обязательные требования:
·         Приложение должно быть написано на WPF
·         Приложение должно поддерживать запуск и отмену операции подсчёта количества тэгов
·         Приложение должно оставаться отзывчивым во время работы. Оно должно каким-либо способом показывать пользователю о том, что процесс выполняется
·         Приложение должно каким-либо образом визуально выделить тот Url, по которому было насчитано наибольшее количество тэгов.
 */
namespace QUIPU_TEST
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}
	}
}
