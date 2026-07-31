using System.Windows;
using System.IO;

namespace RegActividades.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
	private static readonly string LogPath = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"RegActividades",
		"startup.log");

	private void Application_Startup(object sender, StartupEventArgs e)
	{
		try
		{
			var mainWindow = new MainWindow();
			MainWindow = mainWindow;
			mainWindow.Show();
			mainWindow.WindowState = WindowState.Normal;
			mainWindow.Activate();
			mainWindow.Topmost = true;
			mainWindow.Topmost = false;
			mainWindow.Focus();
		}
		catch (Exception ex)
		{
			LogException("Startup", ex);
			System.Windows.MessageBox.Show(
				$"La aplicacion no pudo iniciar correctamente.\n\n{ex.Message}\n\nRevisa el archivo de log:\n{LogPath}",
				"RegActividades",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
			Shutdown(-1);
		}
	}

	private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
	{
		LogException("UnhandledException", e.Exception);
		System.Windows.MessageBox.Show(
			$"Ocurrio un error inesperado.\n\n{e.Exception.Message}\n\nRevisa el archivo de log:\n{LogPath}",
			"RegActividades",
			MessageBoxButton.OK,
			MessageBoxImage.Error);
		e.Handled = true;
		Shutdown(-1);
	}

	private static void LogException(string source, Exception exception)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
		File.AppendAllText(
			LogPath,
			$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {exception}\n\n");
	}
}

