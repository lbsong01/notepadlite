using System.Windows;

namespace NotepadLite.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	/// <summary>
	/// Starts the application, restores the previous session, and opens any files supplied by the shell.
	/// </summary>
	/// <param name="e">Application startup arguments.</param>
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		var mainWindow = new MainWindow();
		MainWindow = mainWindow;
		mainWindow.Show();

		foreach (var arg in e.Args)
		{
			mainWindow.OpenDocumentFromPath(arg);
		}
	}
}

