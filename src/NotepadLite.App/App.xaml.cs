using System.Windows;

namespace NotepadLite.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	/// <summary>
	/// Starts the application and opens an initial file when one is supplied by the shell.
	/// </summary>
	/// <param name="e">Application startup arguments.</param>
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		var mainWindow = new MainWindow();
		MainWindow = mainWindow;
		mainWindow.Show();

		if (e.Args.Length > 0)
		{
			mainWindow.OpenDocumentFromPath(e.Args[0]);
		}
	}
}

