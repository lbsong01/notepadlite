using ICSharpCode.AvalonEdit;
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
/// Hosts the multi-tab editor shell and orchestrates file, session, and language-definition workflows.
/// </summary>
public partial class MainWindow : Window
{
    private readonly DocumentFileService documentFileService;
    private readonly SessionService sessionService;
    private readonly string builtInDefinitionsPath;
    private readonly string userDefinitionsPath;
    private readonly string sessionFilePath;
    private readonly List<DocumentTab> openTabs = [];
    private LanguageDefinition currentLanguage;
    private IReadOnlyList<LanguageDefinition> availableLanguages;
    private bool suppressTabSelectionChanged;

    /// <summary>
    /// Initializes the editor window, loads language definitions, and restores the previous session.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        documentFileService = new DocumentFileService();
        sessionService = new SessionService();
        builtInDefinitionsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Languages");
        userDefinitionsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NotepadLite", "Languages");
        sessionFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NotepadLite", "Session", "session.json");
        currentLanguage = LanguageDefinition.CreatePlainText();
        availableLanguages = [];

        CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (_, _) => NewDocument()));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (_, _) => OpenDocument()));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (_, _) => SaveDocument()));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.SaveAs, (_, _) => SaveDocumentAs()));
        InputBindings.Add(new KeyBinding(ApplicationCommands.New, new KeyGesture(Key.N, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Open, new KeyGesture(Key.O, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Save, new KeyGesture(Key.S, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(ApplicationCommands.SaveAs, new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift)));

        var closeTabCommand = new RoutedCommand("CloseTab", typeof(MainWindow));
        CommandBindings.Add(new CommandBinding(closeTabCommand, (_, _) => CloseActiveTab()));
        InputBindings.Add(new KeyBinding(closeTabCommand, new KeyGesture(Key.W, ModifierKeys.Control)));

        Directory.CreateDirectory(userDefinitionsPath);
        ReloadDefinitions();
        RestoreSession();
    }

    // ── Active tab helpers ──────────────────────────────────────────────

    /// <summary>
    /// Gets the currently active tab, or <see langword="null"/> when no tabs are open.
    /// </summary>
    private DocumentTab? ActiveTab =>
        TabBar.SelectedIndex >= 0 && TabBar.SelectedIndex < openTabs.Count
            ? openTabs[TabBar.SelectedIndex]
            : null;

    /// <summary>
    /// Gets the <see cref="TextEditor"/> for the active tab, or <see langword="null"/>.
    /// </summary>
    private TextEditor? ActiveEditor =>
        TabBar.SelectedItem is TabItem { Content: Border { Child: TextEditor editor } } ? editor : null;

    // ── Menu click handlers ─────────────────────────────────────────────

    /// <summary>
    /// Creates a new untitled document tab.
    /// </summary>
    private void NewDocumentClick(object sender, RoutedEventArgs e) => NewDocument();

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
    /// Closes the active tab.
    /// </summary>
    private void CloseTabClick(object sender, RoutedEventArgs e) => CloseActiveTab();

    /// <summary>
    /// Closes the editor window after persisting the session.
    /// </summary>
    private void ExitClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Reloads language definitions from disk.
    /// </summary>
    private void ReloadDefinitionsClick(object sender, RoutedEventArgs e) => ReloadDefinitions();

    /// <summary>
    /// Creates a new tab from the tab-strip add button.
    /// </summary>
    private void AddTabButtonClick(object sender, RoutedEventArgs e) => NewDocument();

    // ── Tab lifecycle ───────────────────────────────────────────────────

    /// <summary>
    /// Creates a new empty tab and activates it.
    /// </summary>
    private void NewDocument()
    {
        var tab = DocumentTab.CreateEmpty();
        AddTab(tab);
    }

    /// <summary>
    /// Adds a tab to the tab bar and activates it.
    /// </summary>
    private void AddTab(DocumentTab tab)
    {
        openTabs.Add(tab);
        var tabItem = CreateTabItem(tab);
        TabBar.Items.Add(tabItem);
        TabBar.SelectedItem = tabItem;
    }

    /// <summary>
    /// Creates a <see cref="TabItem"/> with a styled header and an AvalonEdit editor.
    /// </summary>
    private TabItem CreateTabItem(DocumentTab tab)
    {
        var editor = new TextEditor
        {
            Background = (System.Windows.Media.Brush)FindResource("SurfaceBrush"),
            Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
            ShowLineNumbers = true,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 14,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = tab.Document.Text,
        };

        editor.TextChanged += EditorTextChanged;

        var border = new Border
        {
            Background = (System.Windows.Media.Brush)FindResource("SurfaceBrush"),
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D7D9DE")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(0, 0, 8, 8),
            Child = editor,
        };

        var tabItem = new TabItem
        {
            Header = BuildTabHeader(tab),
            Content = border,
            Tag = tab.Id,
        };

        return tabItem;
    }

    /// <summary>
    /// Builds the header panel for a tab containing the display name and a close button.
    /// </summary>
    private FrameworkElement BuildTabHeader(DocumentTab tab)
    {
        var dirtyMarker = tab.Document.IsDirty ? "● " : "";
        var headerText = new TextBlock
        {
            Text = $"{dirtyMarker}{tab.Document.DisplayName}",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 4, 4, 4),
        };

        var closeButton = new Button
        {
            Content = "✕",
            FontSize = 10,
            Width = 20,
            Height = 20,
            Padding = new Thickness(0),
            Margin = new Thickness(4, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Focusable = false,
            Tag = tab.Id,
        };

        closeButton.Click += CloseTabButtonClick;

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
        };

        panel.Children.Add(headerText);
        panel.Children.Add(closeButton);
        return panel;
    }

    /// <summary>
    /// Handles the close button click on a tab header.
    /// </summary>
    private void CloseTabButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid tabId })
        {
            CloseTabById(tabId);
        }
    }

    /// <summary>
    /// Closes the currently active tab.
    /// </summary>
    private void CloseActiveTab()
    {
        if (ActiveTab is not null)
        {
            CloseTabById(ActiveTab.Id);
        }
    }

    /// <summary>
    /// Closes the tab with the given identifier after confirming unsaved changes.
    /// </summary>
    private void CloseTabById(Guid tabId)
    {
        var index = openTabs.FindIndex(t => t.Id == tabId);
        if (index < 0)
        {
            return;
        }

        var tab = openTabs[index];
        if (tab.Document.IsDirty && !ConfirmDiscardChanges(tab))
        {
            return;
        }

        // Detach editor event handler.
        if (TabBar.Items[index] is TabItem { Content: Border { Child: TextEditor editor } })
        {
            editor.TextChanged -= EditorTextChanged;
        }

        suppressTabSelectionChanged = true;
        openTabs.RemoveAt(index);
        TabBar.Items.RemoveAt(index);
        suppressTabSelectionChanged = false;

        if (openTabs.Count == 0)
        {
            NewDocument();
        }
        else
        {
            TabBar.SelectedIndex = Math.Min(index, openTabs.Count - 1);
            RefreshActiveTabPresentation();
        }
    }

    /// <summary>
    /// Handles tab selection changes — refreshes the status bar, title, and language.
    /// </summary>
    private void TabBarSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressTabSelectionChanged)
        {
            return;
        }

        RefreshActiveTabPresentation();
    }

    // ── Editor events ──────────────────────────────────────────────────

    /// <summary>
    /// Tracks editor text changes in the corresponding tab's document model.
    /// </summary>
    private void EditorTextChanged(object? sender, EventArgs e)
    {
        if (sender is not TextEditor editor)
        {
            return;
        }

        var tabIndex = FindTabIndexForEditor(editor);
        if (tabIndex < 0)
        {
            return;
        }

        var tab = openTabs[tabIndex];
        if (tab.Document.Text == editor.Text)
        {
            return;
        }

        openTabs[tabIndex] = tab.WithDocument(tab.Document.WithText(editor.Text));
        UpdateTabHeader(tabIndex);

        if (tabIndex == TabBar.SelectedIndex)
        {
            RefreshWindowTitle();
            UpdateStatus("Edited");
        }
    }

    /// <summary>
    /// Finds the tab index that owns the given editor instance.
    /// </summary>
    private int FindTabIndexForEditor(TextEditor editor)
    {
        for (var i = 0; i < TabBar.Items.Count; i++)
        {
            if (TabBar.Items[i] is TabItem { Content: Border { Child: TextEditor tabEditor } }
                && ReferenceEquals(tabEditor, editor))
            {
                return i;
            }
        }

        return -1;
    }

    // ── Window close & session persistence ──────────────────────────────

    /// <summary>
    /// Persists the session state before the window closes.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        SaveSession();
        base.OnClosing(e);
    }

    /// <summary>
    /// Saves the current session to disk.
    /// </summary>
    private void SaveSession()
    {
        try
        {
            sessionService.Save(sessionFilePath, openTabs, ActiveTab?.Id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort; do not block shutdown.
        }
    }

    /// <summary>
    /// Restores tabs from the previous session, or creates a blank tab if no session exists.
    /// </summary>
    internal void RestoreSession()
    {
        var (tabs, activeTabId) = sessionService.Load(sessionFilePath);

        if (tabs.Count == 0)
        {
            NewDocument();
            return;
        }

        suppressTabSelectionChanged = true;
        foreach (var tab in tabs)
        {
            openTabs.Add(tab);
            TabBar.Items.Add(CreateTabItem(tab));
        }

        suppressTabSelectionChanged = false;

        var activeIndex = activeTabId is not null
            ? openTabs.FindIndex(t => t.Id == activeTabId.Value)
            : 0;

        TabBar.SelectedIndex = activeIndex >= 0 ? activeIndex : 0;
        RefreshActiveTabPresentation();
    }

    // ── Language definitions ────────────────────────────────────────────

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
            ?? DetectLanguageForActiveTab();

        ApplyLanguage(languageToApply);
    }

    // ── File operations ─────────────────────────────────────────────────

    /// <summary>
    /// Opens a document selected by the user in a new tab.
    /// </summary>
    private void OpenDocument()
    {
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
    /// Opens a document from a specific path in a new tab, or activates the existing tab.
    /// </summary>
    /// <param name="filePath">The file path to open.</param>
    /// <returns><see langword="true"/> when the document was loaded; otherwise, <see langword="false"/>.</returns>
    public bool OpenDocumentFromPath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);

        // Activate existing tab if the file is already open.
        var existingIndex = openTabs.FindIndex(t =>
            t.Document.FilePath is not null
            && string.Equals(Path.GetFullPath(t.Document.FilePath), fullPath, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            TabBar.SelectedIndex = existingIndex;
            return true;
        }

        try
        {
            var document = documentFileService.Load(fullPath);
            var tab = DocumentTab.FromDocument(document);
            AddTab(tab);
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
    /// Saves the active document to its existing path or prompts for one.
    /// </summary>
    private void SaveDocument()
    {
        if (ActiveTab is null)
        {
            return;
        }

        if (ActiveTab.Document.FilePath is null)
        {
            SaveDocumentAs();
            return;
        }

        var index = TabBar.SelectedIndex;
        var saved = documentFileService.Save(ActiveTab.Document);
        openTabs[index] = ActiveTab.WithDocument(saved);
        UpdateTabHeader(index);
        RefreshActiveTabPresentation();
        UpdateStatus("Saved document");
    }

    /// <summary>
    /// Saves the active document to a user-selected path.
    /// </summary>
    private void SaveDocumentAs()
    {
        if (ActiveTab is null)
        {
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            AddExtension = true,
            FileName = ActiveTab.Document.GetSuggestedFileName(),
            Filter = "Text files|*.txt|Batch files|*.cmd;*.bat|PowerShell files|*.ps1|All files|*.*",
            Title = "Save document",
        };

        if (saveFileDialog.ShowDialog(this) is not true)
        {
            return;
        }

        var index = TabBar.SelectedIndex;
        var saved = documentFileService.Save(ActiveTab.Document, saveFileDialog.FileName);
        openTabs[index] = ActiveTab.WithDocument(saved);
        UpdateTabHeader(index);
        RefreshActiveTabPresentation();
        UpdateStatus("Saved document");
    }

    /// <summary>
    /// Returns whether it is safe to discard unsaved changes for a specific tab.
    /// </summary>
    private bool ConfirmDiscardChanges(DocumentTab tab)
    {
        if (!tab.Document.IsDirty)
        {
            return true;
        }

        var result = MessageBox.Show(
            this,
            $"'{tab.Document.DisplayName}' has unsaved changes. Discard them?",
            "Unsaved changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    // ── Tab / UI refresh helpers ────────────────────────────────────────

    /// <summary>
    /// Updates the tab header text for the tab at the given index.
    /// </summary>
    private void UpdateTabHeader(int index)
    {
        if (index >= 0 && index < openTabs.Count && TabBar.Items[index] is TabItem tabItem)
        {
            tabItem.Header = BuildTabHeader(openTabs[index]);
        }
    }

    /// <summary>
    /// Refreshes the editor presentation, status bar, title, and language for the active tab.
    /// </summary>
    private void RefreshActiveTabPresentation()
    {
        var tab = ActiveTab;
        if (tab is null)
        {
            return;
        }

        DocumentPathTextBlock.Text = tab.Document.FilePath ?? "Unsaved document";
        RefreshWindowTitle();

        var languageToApply = tab.LanguageName is not null
            ? availableLanguages.FirstOrDefault(l => string.Equals(l.Name, tab.LanguageName, StringComparison.OrdinalIgnoreCase))
              ?? DetectLanguageForActiveTab()
            : DetectLanguageForActiveTab();

        ApplyLanguage(languageToApply);
    }

    /// <summary>
    /// Applies syntax highlighting for the supplied language definition.
    /// </summary>
    private void ApplyLanguage(LanguageDefinition definition)
    {
        currentLanguage = FindMatchingAvailableLanguage(definition) ?? definition;

        if (ActiveEditor is { } editor)
        {
            editor.SyntaxHighlighting = HighlightingDefinitionBuilder.Build(currentLanguage);
        }

        if (ActiveTab is not null)
        {
            var index = TabBar.SelectedIndex;
            openTabs[index] = ActiveTab.WithLanguage(currentLanguage.Name);
        }

        LanguageStatusTextBlock.Text = $"Language: {currentLanguage.Name}";
        UpdateLanguageMenuSelection();
        UpdateStatus($"Language: {currentLanguage.Name}");
    }

    /// <summary>
    /// Detects the most suitable language definition for the active tab.
    /// </summary>
    private LanguageDefinition DetectLanguageForActiveTab()
    {
        var filePath = ActiveTab?.Document.FilePath;
        if (filePath is null)
        {
            return availableLanguages.FirstOrDefault() ?? LanguageDefinition.CreatePlainText();
        }

        var extension = Path.GetExtension(filePath);
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
    /// Refreshes the window title with the active tab's dirty state.
    /// </summary>
    private void RefreshWindowTitle()
    {
        var tab = ActiveTab;
        if (tab is null)
        {
            Title = "NotepadLite";
            return;
        }

        var dirtyMarker = tab.Document.IsDirty ? "*" : string.Empty;
        Title = $"{dirtyMarker}{tab.Document.DisplayName} - NotepadLite";
    }

    /// <summary>
    /// Updates the status bar message.
    /// </summary>
    private void UpdateStatus(string message)
    {
        StatusTextBlock.Text = message;
    }
}