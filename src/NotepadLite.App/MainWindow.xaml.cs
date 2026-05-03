using ICSharpCode.AvalonEdit;
using Microsoft.Win32;
using NotepadLite.Core;
using NotepadLite.Core.Formatting;
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
    private readonly DocumentFormattingService documentFormattingService;
    private readonly SessionService sessionService;
    private readonly string builtInDefinitionsPath;
    private readonly string userDefinitionsPath;
    private readonly string sessionFilePath;
    private readonly List<DocumentTab> openTabs = [];
    private readonly Dictionary<Guid, SearchHighlightRenderer> tabRenderers = [];
    private LanguageDefinition currentLanguage;
    private IReadOnlyList<LanguageDefinition> availableLanguages;
    private bool suppressTabSelectionChanged;
    private int currentZoomPercent = DefaultZoomPercent;
    private IReadOnlyList<SearchMatch> currentMatches = Array.Empty<SearchMatch>();
    private int currentMatchIndex = -1;

    private const double BaseFontSize = 14.0;
    private const int DefaultZoomPercent = 100;
    private const int MinZoomPercent = 50;
    private const int MaxZoomPercent = 400;
    private const int ZoomStepPercent = 10;

    /// <summary>
    /// Initializes the editor window, loads language definitions, and restores the previous session.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        documentFileService = new DocumentFileService();
        documentFormattingService = new DocumentFormattingService();
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

        var zoomInCommand = new RoutedCommand("ZoomIn", typeof(MainWindow));
        CommandBindings.Add(new CommandBinding(zoomInCommand, (_, _) => ZoomIn()));
        InputBindings.Add(new KeyBinding(zoomInCommand, new KeyGesture(Key.OemPlus, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(zoomInCommand, new KeyGesture(Key.Add, ModifierKeys.Control)));

        var zoomOutCommand = new RoutedCommand("ZoomOut", typeof(MainWindow));
        CommandBindings.Add(new CommandBinding(zoomOutCommand, (_, _) => ZoomOut()));
        InputBindings.Add(new KeyBinding(zoomOutCommand, new KeyGesture(Key.OemMinus, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(zoomOutCommand, new KeyGesture(Key.Subtract, ModifierKeys.Control)));

        var resetZoomCommand = new RoutedCommand("ResetZoom", typeof(MainWindow));
        CommandBindings.Add(new CommandBinding(resetZoomCommand, (_, _) => ResetZoom()));
        InputBindings.Add(new KeyBinding(resetZoomCommand, new KeyGesture(Key.D0, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(resetZoomCommand, new KeyGesture(Key.NumPad0, ModifierKeys.Control)));

        var showFindCommand = new RoutedCommand("ShowFind", typeof(MainWindow));
        CommandBindings.Add(new CommandBinding(showFindCommand, (_, _) => ShowFindReplace(showReplace: false)));
        InputBindings.Add(new KeyBinding(showFindCommand, new KeyGesture(Key.F, ModifierKeys.Control)));

        var showReplaceCommand = new RoutedCommand("ShowReplace", typeof(MainWindow));
        CommandBindings.Add(new CommandBinding(showReplaceCommand, (_, _) => ShowFindReplace(showReplace: true)));
        InputBindings.Add(new KeyBinding(showReplaceCommand, new KeyGesture(Key.H, ModifierKeys.Control)));

        var findNextCommand = new RoutedCommand("FindNext", typeof(MainWindow));
        CommandBindings.Add(new CommandBinding(findNextCommand, (_, _) => FindNext()));
        InputBindings.Add(new KeyBinding(findNextCommand, new KeyGesture(Key.F3)));

        var findPreviousCommand = new RoutedCommand("FindPrevious", typeof(MainWindow));
        CommandBindings.Add(new CommandBinding(findPreviousCommand, (_, _) => FindPrevious()));
        InputBindings.Add(new KeyBinding(findPreviousCommand, new KeyGesture(Key.F3, ModifierKeys.Shift)));

        var formatDocumentCommand = new RoutedCommand("FormatDocument", typeof(MainWindow));
        CommandBindings.Add(new CommandBinding(formatDocumentCommand, (_, _) => FormatDocument(), FormatDocumentCanExecute));
        InputBindings.Add(new KeyBinding(formatDocumentCommand, new KeyGesture(Key.F, ModifierKeys.Shift | ModifierKeys.Alt)));

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

    /// <summary>
    /// Increases the editor zoom level.
    /// </summary>
    private void ZoomInClick(object sender, RoutedEventArgs e) => ZoomIn();

    /// <summary>
    /// Decreases the editor zoom level.
    /// </summary>
    private void ZoomOutClick(object sender, RoutedEventArgs e) => ZoomOut();

    /// <summary>
    /// Resets the editor zoom level to the default.
    /// </summary>
    private void ResetZoomClick(object sender, RoutedEventArgs e) => ResetZoom();

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
        editor.PreviewMouseWheel += EditorPreviewMouseWheel;
        ApplyZoomToEditor(editor);

        var renderer = new SearchHighlightRenderer();
        editor.TextArea.TextView.BackgroundRenderers.Add(renderer);
        tabRenderers[tab.Id] = renderer;

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
            editor.PreviewMouseWheel -= EditorPreviewMouseWheel;
            if (tabRenderers.TryGetValue(tab.Id, out var renderer))
            {
                editor.TextArea.TextView.BackgroundRenderers.Remove(renderer);
            }
        }

        tabRenderers.Remove(tab.Id);

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
        RecomputeHighlights();
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
            if (FindReplacePanel.Visibility == Visibility.Visible)
            {
                RecomputeHighlights();
            }
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
            sessionService.Save(sessionFilePath, openTabs, ActiveTab?.Id, currentZoomPercent);
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
        var (tabs, activeTabId, zoomLevel) = sessionService.Load(sessionFilePath);

        currentZoomPercent = ClampZoom(zoomLevel);

        if (tabs.Count == 0)
        {
            NewDocument();
            RefreshZoomStatus();
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
        RefreshZoomStatus();
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

    // ── Zoom ───────────────────────────────────────────────────────────

    /// <summary>
    /// Increases the zoom level by one step.
    /// </summary>
    private void ZoomIn() => SetZoom(currentZoomPercent + ZoomStepPercent);

    /// <summary>
    /// Decreases the zoom level by one step.
    /// </summary>
    private void ZoomOut() => SetZoom(currentZoomPercent - ZoomStepPercent);

    /// <summary>
    /// Resets the zoom level to <see cref="DefaultZoomPercent"/>.
    /// </summary>
    private void ResetZoom() => SetZoom(DefaultZoomPercent);

    /// <summary>
    /// Sets the zoom level (clamped) and re-applies it to every open editor.
    /// </summary>
    private void SetZoom(int newZoomPercent)
    {
        var clamped = ClampZoom(newZoomPercent);
        if (clamped == currentZoomPercent)
        {
            return;
        }

        currentZoomPercent = clamped;

        foreach (var item in TabBar.Items.OfType<TabItem>())
        {
            if (item.Content is Border { Child: TextEditor editor })
            {
                ApplyZoomToEditor(editor);
            }
        }

        RefreshZoomStatus();
        UpdateStatus($"Zoom: {currentZoomPercent}%");
    }

    /// <summary>
    /// Applies the current zoom factor to a single editor.
    /// </summary>
    private void ApplyZoomToEditor(TextEditor editor)
    {
        editor.FontSize = BaseFontSize * currentZoomPercent / 100.0;
    }

    /// <summary>
    /// Refreshes the zoom indicator in the status bar.
    /// </summary>
    private void RefreshZoomStatus()
    {
        ZoomStatusTextBlock.Text = $"Zoom: {currentZoomPercent}%";
    }

    /// <summary>
    /// Clamps the requested zoom percentage to the supported range, snapping to the nearest step.
    /// </summary>
    private static int ClampZoom(int value)
    {
        if (value < MinZoomPercent)
        {
            return MinZoomPercent;
        }

        return value > MaxZoomPercent ? MaxZoomPercent : value;
    }

    /// <summary>
    /// Handles Ctrl+MouseWheel on an editor to drive zoom.
    /// </summary>
    private void EditorPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        e.Handled = true;
        if (e.Delta > 0)
        {
            ZoomIn();
        }
        else if (e.Delta < 0)
        {
            ZoomOut();
        }
    }

    // ── Find & Replace ──────────────────────────────────────────────────

    private const int MaxHighlightedMatches = 5000;

    private void ShowFindClick(object sender, RoutedEventArgs e) => ShowFindReplace(showReplace: false);

    private void ShowReplaceClick(object sender, RoutedEventArgs e) => ShowFindReplace(showReplace: true);

    private void HideFindReplaceClick(object sender, RoutedEventArgs e) => HideFindReplace();

    private void FindNextClick(object sender, RoutedEventArgs e) => FindNext();

    private void FindPreviousClick(object sender, RoutedEventArgs e) => FindPrevious();

    private void FormatDocumentClick(object sender, RoutedEventArgs e) => FormatDocument();

    private void FormatDocumentCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ActiveEditor is not null;
    }

    private void FormatDocument()
    {
        if (ActiveEditor is not { } editor)
        {
            return;
        }

        var languageName = ActiveTab?.LanguageName;
        var extension = ActiveTab?.Document.FilePath is { } path ? Path.GetExtension(path) : null;

        var result = documentFormattingService.Format(editor.Text, languageName, extension);
        if (!result.Success)
        {
            UpdateStatus("Format failed");
            MessageBox.Show(
                this,
                $"Unable to format document.{Environment.NewLine}{Environment.NewLine}{result.ErrorMessage}",
                "Format document failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (string.Equals(result.FormattedText, editor.Text, StringComparison.Ordinal))
        {
            UpdateStatus("Document already formatted");
            return;
        }

        var caretOffset = editor.CaretOffset;
        var verticalOffset = editor.VerticalOffset;
        var horizontalOffset = editor.HorizontalOffset;

        editor.Document.BeginUpdate();
        try
        {
            editor.Document.Replace(0, editor.Document.TextLength, result.FormattedText);
        }
        finally
        {
            editor.Document.EndUpdate();
        }

        editor.CaretOffset = Math.Min(caretOffset, editor.Document.TextLength);
        editor.ScrollToVerticalOffset(verticalOffset);
        editor.ScrollToHorizontalOffset(horizontalOffset);
        UpdateStatus("Formatted document");
    }

    private void ReplaceClick(object sender, RoutedEventArgs e) => ReplaceCurrent();

    private void ReplaceAllClick(object sender, RoutedEventArgs e) => ReplaceAll();

    private void FindTextBoxChanged(object sender, TextChangedEventArgs e) => RecomputeHighlights();

    private void FindOptionChanged(object sender, RoutedEventArgs e) => RecomputeHighlights();

    private void FindReplaceTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideFindReplace();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                FindPrevious();
            }
            else
            {
                FindNext();
            }

            e.Handled = true;
        }
    }

    private void ShowFindReplace(bool showReplace)
    {
        FindReplacePanel.Visibility = Visibility.Visible;
        ReplaceTextBox.Visibility = Visibility.Visible;
        ReplaceButton.Visibility = Visibility.Visible;
        ReplaceAllButton.Visibility = Visibility.Visible;

        // Pre-fill with the current single-line selection if any.
        if (ActiveEditor is { } editor && editor.SelectionLength > 0)
        {
            var selected = editor.SelectedText;
            if (!selected.Contains('\n') && !selected.Contains('\r'))
            {
                FindTextBox.Text = selected;
            }
        }

        RecomputeHighlights();

        if (showReplace)
        {
            ReplaceTextBox.Focus();
        }
        else
        {
            FindTextBox.Focus();
            FindTextBox.SelectAll();
        }
    }

    private void HideFindReplace()
    {
        FindReplacePanel.Visibility = Visibility.Collapsed;
        ClearHighlights();
        ActiveEditor?.Focus();
    }

    private SearchOptions CurrentSearchOptions() => new(
        FindTextBox?.Text ?? string.Empty,
        MatchCaseCheckBox?.IsChecked == true,
        WholeWordCheckBox?.IsChecked == true);

    private void RecomputeHighlights()
    {
        if (ActiveEditor is not { } editor)
        {
            currentMatches = Array.Empty<SearchMatch>();
            currentMatchIndex = -1;
            return;
        }

        if (FindReplacePanel?.Visibility != Visibility.Visible)
        {
            ClearHighlights();
            return;
        }

        var options = CurrentSearchOptions();
        if (string.IsNullOrEmpty(options.Pattern))
        {
            currentMatches = Array.Empty<SearchMatch>();
            currentMatchIndex = -1;
            ApplyHighlightsToActiveEditor();
            UpdateFindStatus(0, -1, capped: false);
            return;
        }

        var all = TextSearchEngine.FindAll(editor.Text, options);
        var capped = false;
        if (all.Count > MaxHighlightedMatches)
        {
            capped = true;
            var trimmed = new List<SearchMatch>(MaxHighlightedMatches);
            for (var i = 0; i < MaxHighlightedMatches; i++)
            {
                trimmed.Add(all[i]);
            }

            currentMatches = trimmed;
        }
        else
        {
            currentMatches = all;
        }

        currentMatchIndex = LocateCurrentIndex(editor.SelectionStart);
        ApplyHighlightsToActiveEditor();
        UpdateFindStatus(all.Count, currentMatchIndex, capped);
    }

    private int LocateCurrentIndex(int caretOffset)
    {
        for (var i = 0; i < currentMatches.Count; i++)
        {
            if (currentMatches[i].Offset >= caretOffset)
            {
                return i;
            }
        }

        return currentMatches.Count > 0 ? 0 : -1;
    }

    private void ApplyHighlightsToActiveEditor()
    {
        if (ActiveTab is null || ActiveEditor is not { } editor)
        {
            return;
        }

        if (!tabRenderers.TryGetValue(ActiveTab.Id, out var renderer))
        {
            return;
        }

        renderer.SetMatches(currentMatches, currentMatchIndex);
        editor.TextArea.TextView.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Selection);
    }

    private void ClearHighlights()
    {
        currentMatches = Array.Empty<SearchMatch>();
        currentMatchIndex = -1;

        foreach (var (_, renderer) in tabRenderers)
        {
            renderer.Clear();
        }

        if (ActiveEditor is { } editor)
        {
            editor.TextArea.TextView.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Selection);
        }

        if (FindStatusTextBlock is not null)
        {
            FindStatusTextBlock.Text = string.Empty;
        }
    }

    private void UpdateFindStatus(int totalMatches, int activeIndex, bool capped)
    {
        if (FindStatusTextBlock is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(FindTextBox.Text))
        {
            FindStatusTextBlock.Text = string.Empty;
            return;
        }

        if (totalMatches == 0)
        {
            FindStatusTextBlock.Text = "No matches";
            return;
        }

        var prefix = activeIndex >= 0 ? $"{activeIndex + 1} of {totalMatches}" : $"{totalMatches} matches";
        FindStatusTextBlock.Text = capped ? $"{prefix} (showing first {MaxHighlightedMatches})" : prefix;
    }

    private void FindNext()
    {
        if (ActiveEditor is not { } editor)
        {
            return;
        }

        EnsureFindPanelVisibleForKeyboard();
        var options = CurrentSearchOptions();
        if (string.IsNullOrEmpty(options.Pattern))
        {
            return;
        }

        var startIndex = editor.SelectionStart + editor.SelectionLength;
        var match = TextSearchEngine.FindNext(editor.Text, startIndex, options);
        var wrapped = false;
        if (match is null)
        {
            match = TextSearchEngine.FindNext(editor.Text, 0, options);
            wrapped = true;
        }

        if (match is null)
        {
            UpdateStatus("No matches");
            return;
        }

        SelectMatch(editor, match.Value);
        if (wrapped)
        {
            UpdateStatus("Wrapped to top");
        }
    }

    private void FindPrevious()
    {
        if (ActiveEditor is not { } editor)
        {
            return;
        }

        EnsureFindPanelVisibleForKeyboard();
        var options = CurrentSearchOptions();
        if (string.IsNullOrEmpty(options.Pattern))
        {
            return;
        }

        var startIndex = editor.SelectionStart;
        var match = TextSearchEngine.FindPrevious(editor.Text, startIndex, options);
        var wrapped = false;
        if (match is null)
        {
            match = TextSearchEngine.FindPrevious(editor.Text, editor.Text.Length, options);
            wrapped = true;
        }

        if (match is null)
        {
            UpdateStatus("No matches");
            return;
        }

        SelectMatch(editor, match.Value);
        if (wrapped)
        {
            UpdateStatus("Wrapped to bottom");
        }
    }

    private void EnsureFindPanelVisibleForKeyboard()
    {
        if (FindReplacePanel.Visibility != Visibility.Visible)
        {
            ShowFindReplace(showReplace: false);
        }
    }

    private void SelectMatch(TextEditor editor, SearchMatch match)
    {
        editor.Select(match.Offset, match.Length);
        var line = editor.Document.GetLineByOffset(match.Offset);
        editor.ScrollToLine(line.LineNumber);
        editor.TextArea.Caret.BringCaretToView();

        currentMatchIndex = LocateCurrentIndex(match.Offset);
        // Re-anchor: prefer the exact offset match if present.
        for (var i = 0; i < currentMatches.Count; i++)
        {
            if (currentMatches[i].Offset == match.Offset)
            {
                currentMatchIndex = i;
                break;
            }
        }

        ApplyHighlightsToActiveEditor();
        UpdateFindStatus(currentMatches.Count, currentMatchIndex, capped: false);
    }

    private void ReplaceCurrent()
    {
        if (ActiveEditor is not { } editor)
        {
            return;
        }

        var options = CurrentSearchOptions();
        if (string.IsNullOrEmpty(options.Pattern))
        {
            return;
        }

        var replacement = ReplaceTextBox.Text ?? string.Empty;
        var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        // If the current selection equals the pattern (under options) at a valid match boundary, replace it.
        if (editor.SelectionLength == options.Pattern.Length
            && string.Equals(editor.SelectedText, options.Pattern, comparison)
            && (!options.WholeWord || IsWholeWordSelection(editor)))
        {
            var offset = editor.SelectionStart;
            editor.Document.Replace(offset, editor.SelectionLength, replacement);
            editor.Select(offset + replacement.Length, 0);
            RecomputeHighlights();
        }

        FindNext();
    }

    private static bool IsWholeWordSelection(TextEditor editor)
    {
        var text = editor.Text;
        var start = editor.SelectionStart;
        var end = start + editor.SelectionLength;
        var leftOk = start == 0 || !IsWordChar(text[start - 1]);
        var rightOk = end >= text.Length || !IsWordChar(text[end]);
        return leftOk && rightOk;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private void ReplaceAll()
    {
        if (ActiveEditor is not { } editor)
        {
            return;
        }

        var options = CurrentSearchOptions();
        if (string.IsNullOrEmpty(options.Pattern))
        {
            return;
        }

        var replacement = ReplaceTextBox.Text ?? string.Empty;
        var matches = TextSearchEngine.FindAll(editor.Text, options);
        if (matches.Count == 0)
        {
            UpdateStatus("No matches to replace");
            return;
        }

        var source = editor.Text;
        var builder = new System.Text.StringBuilder(source.Length);
        var cursor = 0;
        foreach (var m in matches)
        {
            if (m.Offset > cursor)
            {
                builder.Append(source, cursor, m.Offset - cursor);
            }

            builder.Append(replacement);
            cursor = m.Offset + m.Length;
        }

        if (cursor < source.Length)
        {
            builder.Append(source, cursor, source.Length - cursor);
        }

        editor.Document.Replace(0, source.Length, builder.ToString());
        UpdateStatus($"Replaced {matches.Count} occurrence(s)");
        RecomputeHighlights();
    }
}