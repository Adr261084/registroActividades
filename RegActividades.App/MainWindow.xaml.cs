using System.Collections.ObjectModel;
using System.Collections;
using System.Globalization;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace RegActividades.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string StartupRegistryValueName = "RegActividades.App";
    private readonly string _dbPath;
    private readonly ObservableCollection<ActivityEntry> _entries = new();
    private readonly ICollectionView _entriesView;
    private Forms.NotifyIcon? _notifyIcon;
    private bool _isExiting;
    private bool _isLoadingStartupSetting;

    public MainWindow()
    {
        InitializeComponent();

        _dbPath = BuildDatabasePath();
        _entriesView = CollectionViewSource.GetDefaultView(_entries);
        _entriesView.Filter = FilterEntry;
        EntriesDataGrid.ItemsSource = _entriesView;

        EnsureDatabase();
        LoadEntries();
        InitializeTrayIcon();
        LoadStartupPreference();

        StateChanged += MainWindow_StateChanged;
    }

    private static string BuildDatabasePath()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RegActividades");

        Directory.CreateDirectory(appDataFolder);
        return Path.Combine(appDataFolder, "actividades.db");
    }

    private void EnsureDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        const string sql = @"
CREATE TABLE IF NOT EXISTS Entradas (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Texto TEXT NOT NULL,
    FechaHora TEXT NOT NULL
);";

        connection.Execute(sql);
    }

    private void LoadEntries()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var data = connection.Query<EntryRow>(
            "SELECT Id, Texto, FechaHora FROM Entradas ORDER BY Id DESC LIMIT 300;");

        _entries.Clear();
        foreach (var item in data)
        {
            _entries.Add(new ActivityEntry(
                item.Id,
                item.Texto,
                DateTime.ParseExact(item.FechaHora, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
        }

        _entriesView.Refresh();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveEntry();
    }

    private void SaveEntry()
    {
        var text = InputTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            System.Windows.MessageBox.Show("Debes escribir un texto antes de guardar.", "Validacion");
            return;
        }

        var now = DateTime.Now;

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        const string insertSql = @"
INSERT INTO Entradas (Texto, FechaHora)
VALUES (@Texto, @FechaHora);
SELECT last_insert_rowid();";

        var newId = connection.ExecuteScalar<long>(insertSql, new
        {
            Texto = text,
            FechaHora = now.ToString("yyyy-MM-dd HH:mm:ss")
        });

        _entries.Insert(0, new ActivityEntry(newId, text, now));
        _entriesView.Refresh();
        InputTextBox.Clear();
        InputTextBox.Focus();
    }

    private bool FilterEntry(object item)
    {
        if (item is not ActivityEntry entry)
        {
            return false;
        }

        var searchText = SearchTextBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(searchText) &&
            !entry.Texto.Contains(searchText, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        var fromDate = FromDatePicker.SelectedDate;
        if (fromDate.HasValue && entry.FechaHora < fromDate.Value.Date)
        {
            return false;
        }

        var toDate = ToDatePicker.SelectedDate;
        if (toDate.HasValue && entry.FechaHora >= toDate.Value.Date.AddDays(1))
        {
            return false;
        }

        return true;
    }

    private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _entriesView.Refresh();
    }

    private void DateFilter_SelectedDateChanged(object? sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _entriesView.Refresh();
    }

    private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Clear();
        FromDatePicker.SelectedDate = null;
        ToDatePicker.SelectedDate = null;
        _entriesView.Refresh();
    }

    private void EntriesDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (EntriesDataGrid.SelectedItem is not ActivityEntry selectedEntry)
        {
            return;
        }

        InputTextBox.Text = selectedEntry.Texto;
        InputTextBox.CaretIndex = InputTextBox.Text.Length;
        InputTextBox.SelectAll();
        InputTextBox.Focus();
    }

    private void InputTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveEntry();
            e.Handled = true;
        }
    }

    private void UpdateSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (EntriesDataGrid.SelectedItem is not ActivityEntry selectedEntry)
        {
            System.Windows.MessageBox.Show("Selecciona un registro antes de actualizarlo.", "Actualizar registro");
            return;
        }

        var newText = InputTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(newText))
        {
            System.Windows.MessageBox.Show("El texto no puede quedar vacío.", "Actualizar registro");
            return;
        }

        if (string.Equals(selectedEntry.Texto, newText, StringComparison.CurrentCulture))
        {
            return;
        }

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        const string updateSql = @"
UPDATE Entradas
SET Texto = @Texto
WHERE Id = @Id;";

        var affectedRows = connection.Execute(updateSql, new
        {
            Texto = newText,
            Id = selectedEntry.Id
        });

        if (affectedRows == 0)
        {
            System.Windows.MessageBox.Show("No se encontró el registro para actualizar.", "Actualizar registro");
            return;
        }

        ReplaceEntry(selectedEntry with { Texto = newText });
        EntriesDataGrid.SelectedItem = _entries.FirstOrDefault(entry => entry.Id == selectedEntry.Id);
        _entriesView.Refresh();
    }

    private void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (EntriesDataGrid.SelectedItem is not ActivityEntry selectedEntry)
        {
            System.Windows.MessageBox.Show("Selecciona un registro antes de eliminarlo.", "Eliminar registro");
            return;
        }

        var confirmation = System.Windows.MessageBox.Show(
            $"¿Eliminar el registro #{selectedEntry.Id}?\n\n{selectedEntry.Texto}",
            "Eliminar registro",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        const string deleteSql = @"
DELETE FROM Entradas
WHERE Id = @Id;";

        var affectedRows = connection.Execute(deleteSql, new
        {
            Id = selectedEntry.Id
        });

        if (affectedRows == 0)
        {
            System.Windows.MessageBox.Show("No se encontró el registro para eliminar.", "Eliminar registro");
            return;
        }

        var entryIndex = _entries.IndexOf(selectedEntry);
        if (entryIndex >= 0)
        {
            _entries.RemoveAt(entryIndex);
        }

        EntriesDataGrid.SelectedItem = null;
        InputTextBox.Clear();
        _entriesView.Refresh();
    }

    private void CancelEditButton_Click(object sender, RoutedEventArgs e)
    {
        EntriesDataGrid.SelectedItem = null;
        InputTextBox.Clear();
        InputTextBox.Focus();
    }

    private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
    {
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"actividades_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            Filter = "CSV (*.csv)|*.csv",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (saveDialog.ShowDialog() != true)
        {
            return;
        }

        var filteredEntries = _entriesView.Cast<ActivityEntry>().ToList();
        if (filteredEntries.Count == 0)
        {
            System.Windows.MessageBox.Show("No hay datos para exportar con los filtros actuales.", "Exportar CSV");
            return;
        }

        var csv = new StringBuilder();
        csv.AppendLine("Id,Texto,FechaHora");

        foreach (var entry in filteredEntries)
        {
            csv.Append(entry.Id.ToString(CultureInfo.InvariantCulture));
            csv.Append(',');
            csv.Append(EscapeCsv(entry.Texto));
            csv.Append(',');
            csv.Append(EscapeCsv(entry.FechaHora.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
            csv.AppendLine();
        }

        File.WriteAllText(saveDialog.FileName, csv.ToString(), new UTF8Encoding(true));

        System.Windows.MessageBox.Show(
            $"Se exportaron {filteredEntries.Count} registros en:\n{saveDialog.FileName}",
            "Exportar CSV");
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private void LoadStartupPreference()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
        var configuredValue = runKey?.GetValue(StartupRegistryValueName) as string;

        _isLoadingStartupSetting = true;
        RunAtStartupCheckBox.IsChecked = !string.IsNullOrWhiteSpace(configuredValue);
        _isLoadingStartupSetting = false;
    }

    private void RunAtStartupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingStartupSetting)
        {
            return;
        }

        try
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (RunAtStartupCheckBox.IsChecked == true)
            {
                runKey?.SetValue(StartupRegistryValueName, BuildStartupCommand());
            }
            else
            {
                runKey?.DeleteValue(StartupRegistryValueName, false);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"No se pudo actualizar el inicio con Windows.\n{ex.Message}", "Inicio con Windows");
            LoadStartupPreference();
        }
    }

    private static string BuildStartupCommand()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("No fue posible resolver la ruta del ejecutable actual.");
        }

        var commandArgs = Environment.GetCommandLineArgs();
        if (processPath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase) && commandArgs.Length > 1)
        {
            return $"\"{processPath}\" \"{commandArgs[1]}\"";
        }

        return $"\"{processPath}\"";
    }

    private void InitializeTrayIcon()
    {
        var trayIcon = GetApplicationIcon();

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = trayIcon,
            Visible = true,
            Text = "Registro de Actividades"
        };

        _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();

        var contextMenu = new Forms.ContextMenuStrip();
        contextMenu.Items.Add("Abrir", null, (_, _) => RestoreFromTray());
        contextMenu.Items.Add("Salir", null, (_, _) => ExitApplication());
        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private static System.Drawing.Icon GetApplicationIcon()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var associatedIcon = System.Drawing.Icon.ExtractAssociatedIcon(processPath);
            if (associatedIcon is not null)
            {
                return associatedIcon;
            }
        }

        return System.Drawing.SystemIcons.Application;
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            _notifyIcon?.ShowBalloonTip(
                1500,
                "Registro de Actividades",
                "La aplicacion sigue activa en la bandeja del sistema.",
                Forms.ToolTipIcon.Info);
        }
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ReplaceEntry(ActivityEntry updatedEntry)
    {
        var index = _entries.FindIndex(entry => entry.Id == updatedEntry.Id);
        if (index < 0)
        {
            return;
        }

        _entries[index] = updatedEntry;
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _notifyIcon?.Dispose();
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _notifyIcon?.Dispose();
        base.OnClosed(e);
    }
}

public sealed record ActivityEntry(long Id, string Texto, DateTime FechaHora);

internal sealed record EntryRow(long Id, string Texto, string FechaHora);

internal static class ObservableCollectionExtensions
{
    public static int FindIndex<T>(this ObservableCollection<T> collection, Predicate<T> match)
    {
        for (var index = 0; index < collection.Count; index++)
        {
            if (match(collection[index]))
            {
                return index;
            }
        }

        return -1;
    }
}