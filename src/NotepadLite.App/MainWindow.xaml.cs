using Microsoft.Win32;
using NotepadLite.Core;
using NotepadLite.Syntax;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NotepadLite.App;

/// <summary>
/// Hosts the first editor shell and orchestrates file and language-definition workflows.
/// </summary>
public partial class MainWindow : Window
{
    private readonly DocumentFileService documentFileService;
    private readonly string builtInDefinitionsPath;
    private readonly string userDefinitionsPath;
    private EditorDocument currentDocument;
    private LanguageDefinition currentLanguage;
    private IReadOnlyList<LanguageDefinition> availableLanguages;

    /// <summary>
    /// Initializes the editor window and loads the initial language definitions.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        documentFileService = new DocumentFileService();
        builtInDefinitionsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Languages");
        userDefinitionsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NotepadLite", "Languages");
        currentDocument = EditorDocument.CreateEmpty();
        currentLanguage = LanguageDefinition.CreatePlainText();
        availableLanguages = [];

        Editor.TextChanged += EditorTextChanged;

        CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (_, _) => OpenDocument()));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (_, _) => SaveDocument()));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.SaveAs, (_, _) => SaveDocumentAs()));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Open, new KeyGesture(Key.O, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Save, new KeyGesture(Key.S, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(ApplicationCommands.SaveAs, new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift)));

        Directory.CreateDirectory(userDefinitionsPath);
        ReloadDefinitions();
        RefreshDocumentPresentation();
    }

    /// <summary>
    /// Prompts the user to open a text document.
    /// </summary>
    private void OpenDocumentClick(object sender, RoutedEventArgs e) => OpenDocument();

    /// <summary>
    /// Saves the current document.
    /// </summary>
    private void SaveDocumentClick(object sender, RoutedEventArgs e) => SaveDocument();

    /// <summary>
    /// Prompts the user for a save target.
    /// </summary>
    private void SaveDocumentAsClick(object sender, RoutedEventArgs e) => SaveDocumentAs();

    /// <summary>
    /// Closes the editor window after confirming unsaved changes.
    /// </summary>
    private void ExitClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Reloads language definitions from disk.
    /// </summary>
    private void ReloadDefinitionsClick(object sender, RoutedEventArgs e) => ReloadDefinitions();

    /// <summary>
    /// Tracks editor text changes in the current document model.
    /// </summary>
    private void EditorTextChanged(object? sender, EventArgs e)
    {
        if (currentDocument.Text == Editor.Text)
        {
            return;
        }

        currentDocument = currentDocument.WithText(Editor.Text);
        RefreshWindowTitle();
        UpdateStatus("Edited");
    }

    /// <summary>
    /// Confirms pending changes before the window closes.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    /// <summary>
    /// Loads language definitions from built-in and user folders.
    /// </summary>
    private void ReloadDefinitions()
    {
        var builtInResult = LanguageCatalog.LoadFromDirectory(builtInDefinitionsPath);
        var userResult = LanguageCatalog.LoadFromDirectory(userDefinitionsPath);

        availableLanguages = builtInResult.Definitions
            .Concat(userResult.Definitions)
            .OrderBy(static language => language.Name, StringComparer.OrdinalIgnoreCase)
            .Prepend(LanguageDefinition.CreatePlainText())
            .ToArray();

        RebuildLanguageMenuItems();

        var languageToApply = FindMatchingAvailableLanguage(currentLanguage)
            ?? DetectLanguageForCurrentDocument();

        ApplyLanguage(languageToApply);
    }

    /// <summary>
    /// Opens a document selected by the user.
    /// </summary>
    private void OpenDocument()
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        var openFileDialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "Text files|*.txt;*.cmd;*.bat;*.ps1;*.json;*.xml;*.md;*.log|All files|*.*",
            Title = "Open document",
        };

        if (openFileDialog.ShowDialog(this) is not true)
        {
            return;
        }

        OpenDocumentFromPath(openFileDialog.FileName);
    }

    /// <summary>
    /// Opens a document from a specific path.
    /// </summary>
    /// <param name="filePath">The file path to open.</param>
    /// <returns><see langword="true"/> when the document was loaded; otherwise, <see langword="false"/>.</returns>
    public bool OpenDocumentFromPath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            currentDocument = documentFileService.Load(filePath);
            RefreshDocumentPresentation();
            UpdateStatus("Opened document");
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                $"Unable to open '{filePath}'.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                "Open document failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }
    }

    /// <summary>
    /// Saves the current document to its existing path or prompts for one.
    /// </summary>
    private void SaveDocument()
    {
        if (currentDocument.FilePath is null)
        {
            SaveDocumentAs();
            return;
        }

        currentDocument = documentFileService.Save(currentDocument);
        RefreshDocumentPresentation();
        UpdateStatus("Saved document");
    }

    /// <summary>
    /// Saves the current document to a user-selected path.
    /// </summary>
    private void SaveDocumentAs()
    {
        var saveFileDialog = new SaveFileDialog
        {
            AddExtension = true,
            FileName = currentDocument.GetSuggestedFileName(),
            Filter = "Text files|*.txt|Batch files|*.cmd;*.bat|PowerShell files|*.ps1|All files|*.*",
            Title = "Save document",
        };

        if (saveFileDialog.ShowDialog(this) is not true)
        {
            return;
        }

        currentDocument = documentFileService.Save(currentDocument, saveFileDialog.FileName);
        RefreshDocumentPresentation();
        UpdateStatus("Saved document");
    }

    /// <summary>
    /// Returns whether it is safe to discard unsaved changes.
    /// </summary>
    private bool ConfirmDiscardChanges()
    {
        if (!currentDocument.IsDirty)
        {
            return true;
        }

        var result = MessageBox.Show(
            this,
            "The current document has unsaved changes. Discard them?",
            "Unsaved changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    /// <summary>
    /// Refreshes the editor, path, title, and selected language after a document change.
    /// </summary>
    private void RefreshDocumentPresentation()
    {
        Editor.Text = currentDocument.Text;
        DocumentPathTextBlock.Text = currentDocument.FilePath ?? "Unsaved document";
        RefreshWindowTitle();

        var detectedLanguage = DetectLanguageForCurrentDocument();
        ApplyLanguage(detectedLanguage);
    }

    /// <summary>
    /// Applies syntax highlighting for the supplied language definition.
    /// </summary>
    private void ApplyLanguage(LanguageDefinition definition)
    {
        currentLanguage = FindMatchingAvailableLanguage(definition) ?? definition;
        Editor.SyntaxHighlighting = HighlightingDefinitionBuilder.Build(currentLanguage);
        LanguageStatusTextBlock.Text = $"Language: {currentLanguage.Name}";
        UpdateLanguageMenuSelection();
        UpdateStatus($"Language: {currentLanguage.Name}");
    }

    /// <summary>
    /// Detects the most suitable language definition for the current document.
    /// </summary>
    private LanguageDefinition DetectLanguageForCurrentDocument()
    {
        if (currentDocument.FilePath is null)
        {
            return availableLanguages.FirstOrDefault() ?? LanguageDefinition.CreatePlainText();
        }

        var extension = Path.GetExtension(currentDocument.FilePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return availableLanguages.FirstOrDefault() ?? LanguageDefinition.CreatePlainText();
        }

        return availableLanguages.FirstOrDefault(language => language.SupportsExtension(extension))
            ?? availableLanguages.FirstOrDefault()
            ?? LanguageDefinition.CreatePlainText();
    }

    /// <summary>
    /// Rebuilds the language selection items under the Language menu.
    /// </summary>
    private void RebuildLanguageMenuItems()
    {
        var separatorIndex = LanguageMenu.Items.IndexOf(LanguageMenuSeparator);
        while (LanguageMenu.Items.Count > separatorIndex + 1)
        {
            LanguageMenu.Items.RemoveAt(LanguageMenu.Items.Count - 1);
        }

        foreach (var language in availableLanguages)
        {
            var menuItem = new MenuItem
            {
                Header = language.Name,
                IsCheckable = true,
                StaysOpenOnClick = true,
                Tag = language,
            };

            menuItem.Click += LanguageMenuItemClick;
            LanguageMenu.Items.Add(menuItem);
        }
    }

    /// <summary>
    /// Applies the language chosen from the Language menu.
    /// </summary>
    private void LanguageMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: LanguageDefinition selectedLanguage })
        {
            ApplyLanguage(selectedLanguage);
        }
    }

    /// <summary>
    /// Updates the checked state of language items in the Language menu.
    /// </summary>
    private void UpdateLanguageMenuSelection()
    {
        foreach (var menuItem in LanguageMenu.Items.OfType<MenuItem>())
        {
            menuItem.IsChecked = ReferenceEquals(menuItem.Tag, currentLanguage);
        }
    }

    /// <summary>
    /// Resolves a language definition to the current in-memory catalog entry when possible.
    /// </summary>
    private LanguageDefinition? FindMatchingAvailableLanguage(LanguageDefinition definition)
    {
        return availableLanguages.FirstOrDefault(language =>
            string.Equals(language.SourcePath, definition.SourcePath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(language.Name, definition.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Refreshes the window title with the current dirty state.
    /// </summary>
    private void RefreshWindowTitle()
    {
        var dirtyMarker = currentDocument.IsDirty ? "*" : string.Empty;
        Title = $"{dirtyMarker}{currentDocument.DisplayName} - NotepadLite";
    }

    /// <summary>
    /// Updates the status bar message.
    /// </summary>
    private void UpdateStatus(string message)
    {
        StatusTextBlock.Text = message;
    }
}