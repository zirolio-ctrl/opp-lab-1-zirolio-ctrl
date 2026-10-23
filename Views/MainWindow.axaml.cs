using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using opp_lab_1_zirolio_ctrl.Models;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace opp_lab_1_zirolio_ctrl.Views;


public partial class MainWindow : Window
{
    private int _rowCount = 10;
    private int _colCount = 10;

    private const double RowHeaderWidth = 40;
    private const double CellWidth = 100;
    private const double HeaderHeight = 32;
    private const double CellHeight = 32;

    private string _selectedCell = "A1";

    private GoogleDriveService? _googleDriveService;
    private readonly SpreadsheetEngine _engine = new();

    public MainWindow()
    {
        InitializeComponent();
        RenderGrid();

        this.Closing += OnWindowClosing;
    }

    private void RenderGrid()
    {
        StatusLabel.Classes.Remove("error");

        SpreadsheetGrid.RowDefinitions.Clear();
        SpreadsheetGrid.ColumnDefinitions.Clear();
        SpreadsheetGrid.Children.Clear();

        SpreadsheetGrid.ColumnSpacing = 0;
        SpreadsheetGrid.RowSpacing = 0;

        SpreadsheetGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(RowHeaderWidth)));
        SpreadsheetGrid.RowDefinitions.Add(new RowDefinition(new GridLength(HeaderHeight)));

        var corner = CreateHeader("");
        Grid.SetRow(corner, 0);
        Grid.SetColumn(corner, 0);
        SpreadsheetGrid.Children.Add(corner);

        for (int c = 1; c <= _colCount; c++)
        {
            SpreadsheetGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(CellWidth)));

            var columnHeader = CreateHeader(GetColumnName(c));
            Grid.SetRow(columnHeader, 0);
            Grid.SetColumn(columnHeader, c);
            SpreadsheetGrid.Children.Add(columnHeader);
        }

        for (int r = 1; r <= _rowCount; r++)
        {
            SpreadsheetGrid.RowDefinitions.Add(new RowDefinition(new GridLength(CellHeight)));

            var rowHeader = CreateHeader(r.ToString());
            Grid.SetRow(rowHeader, r);
            Grid.SetColumn(rowHeader, 0);
            SpreadsheetGrid.Children.Add(rowHeader);

            for (int c = 1; c <= _colCount; c++)
            {
                var cell = CreateCell($"{GetColumnName(c)}{r}");
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                SpreadsheetGrid.Children.Add(cell);
            }
        }
    }

    private Border CreateHeader(string text)
    {
        var border = new Border
        {
            Width = text.Length == 0 ? RowHeaderWidth : (int.TryParse(text, out _) ? RowHeaderWidth : CellWidth),
            Height = HeaderHeight,
            Child = new TextBlock
            {
                Text = text,
            }
        };

        border.Classes.Add("header-cell");

        return border;
    }

    private Border CreateCell(string cellName)
    {
        bool error = false;
        string displayValue = "";

        try
        {
            string val = _engine.EvaluateCell(cellName);
            string rawExpr = _engine.GetExpression(cellName);

            if (!string.IsNullOrWhiteSpace(rawExpr))
            {
                displayValue = val;
            }
        }
        catch
        {
            error = true;
            displayValue = "#ERROR!";
        }

        var button = new Button
        {
            Content = displayValue,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        if (error)
        {
            button.Classes.Add("error");
        }

        button.Click += (_, _) => SelectCell(cellName);

        var border = new Border
        {
            Width = CellWidth,
            Height = CellHeight,
            Child = button
        };

        border.Classes.Add("cell");
        if (error) border.Classes.Add("error");

        return border;
    }

    private void SelectCell(string cellName)
    {
        _selectedCell = cellName;
        SelectedCellLabel.Text = $"{cellName}:";

        string expr = _engine.GetExpression(cellName);
        FormulaEntry.Text = expr;

        FormulaEntry.Focus();
        FormulaEntry.CaretIndex = FormulaEntry.Text?.Length ?? 0;
    }

    private void ApplyFormula()
    {
        if (string.IsNullOrEmpty(_selectedCell))
            return;

        string input = FormulaEntry.Text ?? string.Empty;

        try
        {
            _engine.SetExpression(_selectedCell, input);
            _engine.EvaluateCell(_selectedCell);

            StatusLabel.Text = $"Клітинка {_selectedCell} оновлена: {input}";
        }
        catch (Exception ex)
        {
            StatusLabel.Classes.Add("error");
            StatusLabel.Text = $"Помилка: {ex.Message}";
        }

        RenderGrid();
        FormulaEntry.Focus();
    }

    private async Task<bool> ConfirmSaveBeforeActionAsync()
    {
        var yesButton = new Button { Content = "Зберегти", Width = 95, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        var noButton = new Button { Content = "Ні", Width = 80, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        var cancelButton = new Button { Content = "Скасувати", Width = 100, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center };

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, noButton, yesButton }
        };

        var grid = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("*, Auto"),
            RowSpacing = 20
        };

        var messageBlock = new TextBlock
        {
            Text = "У вас є незбережені зміни. Бажаєте зберегти таблицю перед продовженням?",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 14
        };

        Grid.SetRow(messageBlock, 0);
        Grid.SetRow(buttonsPanel, 1);

        grid.Children.Add(messageBlock);
        grid.Children.Add(buttonsPanel);

        var dialog = new Window
        {
            Title = "Підтвердження",
            Width = 420,
            Height = 170,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = grid
        };

        string result = "Cancel";

        yesButton.Click += (_, _) => { result = "Yes"; dialog.Close(); };
        noButton.Click += (_, _) => { result = "No"; dialog.Close(); };
        cancelButton.Click += (_, _) => { result = "Cancel"; dialog.Close(); };

        await dialog.ShowDialog(this);

        if (result == "Yes")
        {
            OnExportClicked(this, new RoutedEventArgs());
            return true;
        }
        
        return result == "No";
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        e.Cancel = true;

        bool proceed = await ConfirmSaveBeforeActionAsync();
        if (proceed)
        {
            Closing -= OnWindowClosing;
            Close();
        }
    }

    private GoogleDriveService GetGoogleDriveService()
    {
        return _googleDriveService ??= new GoogleDriveService();
    }

    private async void OnExportClicked(object? sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        btn?.IsEnabled = false;

        try
        {
            var file = new SpreadsheetFile
            {
                Rows = _rowCount,
                Columns = _colCount
            };

            for (int r = 1; r <= _rowCount; r++)
            {
                for (int c = 1; c <= _colCount; c++)
                {
                    string cellName = $"{GetColumnName(c)}{r}";
                    string expression = _engine.GetExpression(cellName);

                    if (!string.IsNullOrWhiteSpace(expression))
                        file.Cells[cellName] = expression;
                }
            }

            string json = JsonSerializer.Serialize(
                file,
                new JsonSerializerOptions { WriteIndented = true });

            string fileName = $"table-{DateTime.Now:yyyyMMdd-HHmmss}.json";

            await GetGoogleDriveService().UploadJsonAsync(fileName, json);

            StatusLabel.Text = $"Таблицю збережено на Google Drive: {fileName}";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Помилка експорту: {ex.Message}";
        }
        finally
        {
            btn?.IsEnabled = true;
        }
    }

    private async void OnImportClicked(object? sender, RoutedEventArgs e)
    {
        bool proceed = await ConfirmSaveBeforeActionAsync();
        if (!proceed)
        {
            StatusLabel.Text = "Імпорт скасовано.";
            return;
        }

        var btn = sender as Button;
        btn?.IsEnabled = false;

        try
        {
            StatusLabel.Text = "Завантаження списку файлів...";

            var service = GetGoogleDriveService();
            var files = await service.GetJsonFilesAsync();

            if (files.Count == 0)
            {
                StatusLabel.Text = "На Google Drive немає JSON-файлів таблиць.";
                return;
            }

            var selectedFile = await SelectDriveFileAsync(files);

            if (selectedFile is null)
            {
                StatusLabel.Text = "Імпорт скасовано.";
                return;
            }

            string json = await service.DownloadJsonAsync(selectedFile.Id);
            var data = JsonSerializer.Deserialize<SpreadsheetFile>(json);

            if (data is null || data.Rows < 1 || data.Columns < 1)
                throw new InvalidOperationException("Некоректний файл таблиці.");

            for (int r = 1; r <= Math.Max(_rowCount, data.Rows); r++)
            {
                for (int c = 1; c <= Math.Max(_colCount, data.Columns); c++)
                {
                    _engine.SetExpression($"{GetColumnName(c)}{r}", "");
                }
            }

            _rowCount = data.Rows;
            _colCount = data.Columns;

            foreach (var cell in data.Cells)
            {
                _engine.SetExpression(cell.Key, cell.Value);
            }

            RenderGrid();

            StatusLabel.Text = $"Імпортовано таблицю: {selectedFile.Name}";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Помилка імпорту: {ex.Message}";
        }
        finally
        {
            btn?.IsEnabled = true;
        }
    }

    private async Task<DriveFile?> SelectDriveFileAsync(IList<DriveFile> files)
    {
        var list = new ListBox
        {
            ItemsSource = files.Select(f => f.Name).ToList(),
            SelectedIndex = files.Count > 0 ? 0 : -1
        };

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = list
        };

        var selectButton = new Button
        {
            Content = "Завантажити",
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelButton = new Button
        {
            Content = "Скасувати",
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, selectButton }
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *, Auto"),
            RowSpacing = 12,
            Margin = new Thickness(12)
        };

        var titleBlock = new TextBlock 
        { 
            Text = "Виберіть таблицю з Google Drive:",
            VerticalAlignment = VerticalAlignment.Center
        };

        Grid.SetRow(titleBlock, 0);
        Grid.SetRow(scrollViewer, 1);
        Grid.SetRow(buttons, 2);

        grid.Children.Add(titleBlock);
        grid.Children.Add(scrollViewer);
        grid.Children.Add(buttons);

        var dialog = new Window
        {
            Title = "Імпорт таблиці",
            Width = 420,
            Height = 350,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = grid
        };

        selectButton.Click += (_, _) =>
        {
            if (list.SelectedIndex >= 0)
                dialog.Close(files[list.SelectedIndex]);
        };

        cancelButton.Click += (_, _) => dialog.Close(null);

        return await dialog.ShowDialog<DriveFile?>(this);
    }

    private void OnFormulaSubmitted(object sender, RoutedEventArgs e)
    {
        ApplyFormula();
    }

    private void OnFormulaKeyed(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyFormula();
            e.Handled = true;
        }
    }

    private void OnAddRowClicked(object? sender, RoutedEventArgs e)
    {
        _rowCount++;
        StatusLabel.Text = $"Додано рядок. Всього рядків: {_rowCount}";
        RenderGrid();
    }

    private void OnRemoveRowClicked(object? sender, RoutedEventArgs e)
    {
        if (_rowCount > 1)
        {
            for (int c = 1; c <= _colCount; c++)
            {
                string cellName = $"{GetColumnName(c)}{_rowCount}";
                _engine.SetExpression(cellName, string.Empty);
            }

            _rowCount--;
            StatusLabel.Text = $"Видалено рядок. Всього рядків: {_rowCount}";
            RenderGrid();
        }
    }

    private void OnAddColClicked(object? sender, RoutedEventArgs e)
    {
        _colCount++;
        StatusLabel.Text = $"Додано стовпець. Всього стовпців: {_colCount}";
        RenderGrid();
    }

    private void OnRemoveColClicked(object? sender, RoutedEventArgs e)
    {
        if (_colCount > 1)
        {
            string colName = GetColumnName(_colCount);
            for (int r = 1; r <= _rowCount; r++)
            {
                string cellName = $"{colName}{r}";
                _engine.SetExpression(cellName, string.Empty);
            }

            _colCount--;
            StatusLabel.Text = $"Видалено стовпець. Всього стовпців: {_colCount}";
            RenderGrid();
        }
    }

    private void OnClearTableClicked(object? sender, RoutedEventArgs e)
    {
        _engine.Clear();
        StatusLabel.Text = "Таблицю очищено";
        RenderGrid();
    }

    private async void OnHelpClicked(object? sender, RoutedEventArgs e)
    {
        var helpText = new TextBlock
        {
            Text = 
                "ДОДАТОК: ТАБЛИЧНИЙ ПРОЦЕСОР\n\n" +
                "1. Робота з клітинками:\n" +
                "   • Натисніть на будь-яку клітинку, щоб вибрати її та редагувати значення у полі введення зверху.\n" +
                "   • Для введення формул використовуйте символ '=' на початку (наприклад: = A1 + B2 * 5).\n" +
                "   • Підтримуються базові операції, пріоритети та степені (^).\n\n" +
                "2. Обробка помилок:\n" +
                "   • Порожні або неіснуючі клітинки, використані у формулах, викликають помилку (#ERROR!).\n" +
                "   • Циклічні посилання (наприклад, A1 посилається на A2, а A2 на A1) блокуються системою.\n\n" +
                "3. Структура таблиці:\n" +
                "   • Ви можете додавати або видаляти рядки й стовпці за допомогою кнопок на панелі керування.\n\n" +
                "4. Google Drive:\n" +
                "   • Ви можете імпортувати раніше збережені таблиці з хмари або експортувати поточну роботу на Google Drive.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E0E0E0"))
        };

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = helpText
        };

        var closeButton = new Button
        {
            Content = "Зрозуміло",
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Right,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        var grid = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("*, Auto"),
            RowSpacing = 16
        };

        Grid.SetRow(scrollViewer, 0);
        Grid.SetRow(closeButton, 1);

        grid.Children.Add(scrollViewer);
        grid.Children.Add(closeButton);

        var dialog = new Window
        {
            Title = "Довідка",
            Width = 480,
            Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = grid
        };

        closeButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
    }

    private string GetColumnName(int colIndex)
    {
        string columnName = "";

        while (colIndex > 0)
        {
            int remainder = (colIndex - 1) % 26;
            columnName = (char)('A' + remainder) + columnName;
            colIndex = (colIndex - 1) / 26;
        }

        return columnName;
    }
}