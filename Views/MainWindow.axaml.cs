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
    }

    private void RenderGrid()
    {
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
            displayValue = "#ERROR!";
        }

        var button = new Button
        {
            Content = displayValue,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        button.Click += (_, _) => SelectCell(cellName);

        var border = new Border
        {
            Width = CellWidth,
            Height = CellHeight,
            Child = button
        };

        border.Classes.Add("cell");

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
            StatusLabel.Text = $"Помилка: {ex.Message}";
        }

        RenderGrid();
        FormulaEntry.Focus();
    }

    private GoogleDriveService GetGoogleDriveService()
    {
        return _googleDriveService ??= new GoogleDriveService();
    }

    private async void OnExportClicked(object? sender, RoutedEventArgs e)
    {
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
    }

    private async void OnImportClicked(object? sender, RoutedEventArgs e)
    {
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
    }

    private async Task<DriveFile?> SelectDriveFileAsync(IList<DriveFile> files)
    {
        var list = new ListBox
        {
            ItemsSource = files.Select(f => f.Name).ToList(),
            SelectedIndex = files.Count > 0 ? 0 : -1
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

        var panel = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "Виберіть таблицю з Google Drive:" },
                list,
                buttons
            }
        };

        var dialog = new Window
        {
            Title = "Імпорт таблиці",
            Width = 420,
            Height = 350,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
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
        RenderGrid();
        StatusLabel.Text = $"Додано рядок. Всього рядків: {_rowCount}";
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
            RenderGrid();
            StatusLabel.Text = $"Видалено рядок. Всього рядків: {_rowCount}";
        }
    }

    private void OnAddColClicked(object? sender, RoutedEventArgs e)
    {
        _colCount++;
        RenderGrid();
        StatusLabel.Text = $"Додано стовпець. Всього стовпців: {_colCount}";
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
            RenderGrid();
            StatusLabel.Text = $"Видалено стовпець. Всього стовпців: {_colCount}";
        }
    }

    private void OnClearTableClicked(object? sender, RoutedEventArgs e)
    {
        _engine.Clear();
        RenderGrid();
        StatusLabel.Text = "Таблицю очищено";
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