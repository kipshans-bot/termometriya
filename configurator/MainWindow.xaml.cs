using Microsoft.Win32;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Termometriya.Configurator.Models;
using Termometriya.Configurator.ViewModels;

namespace Termometriya.Configurator;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel(ServerOutputBox, () => { });
        DataContext = _vm;
        _vm.PropertyChanged += Vm_PropertyChanged;

        CulturesGrid.ItemsSource = _vm.Cultures;
        ElevatorTree.ItemsSource = _vm.Lines;
        Bkt12Tree.ItemsSource = _vm.Bkt12Lines;
        CultureCombo.ItemsSource = _vm.Cultures.Select(c => c.Name).ToList();
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.StatusText))
            StatusTextBlock.Text = _vm.StatusText;
        if (e.PropertyName == nameof(MainViewModel.IsLoading))
        {
            LoadBtn.IsEnabled = !_vm.IsLoading;
            SaveBtn.IsEnabled = !_vm.IsLoading;
        }
        if (e.PropertyName == nameof(MainViewModel.SelectedCulture)
            || e.PropertyName == nameof(MainViewModel.SelectedSilo)
            || e.PropertyName == nameof(MainViewModel.SelectedPendant))
            UpdateCultureCombo();
        if (e.PropertyName == nameof(MainViewModel.SelectedBkt12Block))
            ThermoGrid.ItemsSource = _vm.SelectedBkt12Block?.Thermopendants;
    }

    private void UpdateCultureCombo()
    {
        var names = _vm.Cultures.Select(c => c.Name).ToList();
        var prev = CultureCombo.SelectedItem;
        CultureCombo.ItemsSource = names;
        CultureCombo.SelectedItem = prev;
    }

    // === Toolbar ===

    private async void LoadBtn_Click(object sender, RoutedEventArgs e)
    {
        _vm.ServerUrl = ServerUrlBox.Text.Trim();
        await _vm.LoadElevatorConfigAsync();
        await _vm.LoadHardwareConfigAsync();
        UpdateCultureCombo();
    }

    private async void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        _vm.ServerUrl = ServerUrlBox.Text.Trim();
        await _vm.SaveElevatorConfigAsync();
        await _vm.SaveHardwareConfigAsync();
    }

    private async void LoadElevBtn_Click(object sender, RoutedEventArgs e)
    {
        _vm.ServerUrl = ServerUrlBox.Text.Trim();
        await _vm.LoadElevatorConfigAsync();
        UpdateCultureCombo();
    }

    private async void SaveElevBtn_Click(object sender, RoutedEventArgs e)
    {
        _vm.ServerUrl = ServerUrlBox.Text.Trim();
        await _vm.SaveElevatorConfigAsync();
    }

    private async void LoadHwBtn_Click(object sender, RoutedEventArgs e)
    {
        _vm.ServerUrl = ServerUrlBox.Text.Trim();
        await _vm.LoadHardwareConfigAsync();
    }

    private async void SaveHwBtn_Click(object sender, RoutedEventArgs e)
    {
        _vm.ServerUrl = ServerUrlBox.Text.Trim();
        await _vm.SaveHardwareConfigAsync();
    }

    // === Elevator CRUD ===

    private void AddCulture_Click(object sender, RoutedEventArgs e) => _vm.AddCulture();
    private void DeleteCulture_Click(object sender, RoutedEventArgs e) => _vm.DeleteCulture();

    private void CulturesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CulturesGrid.SelectedItem is CultureConfig c)
            _vm.SelectedCulture = c;
    }

    private void AddLine_Click(object sender, RoutedEventArgs e) => _vm.AddLine();
    private void DeleteLine_Click(object sender, RoutedEventArgs e) => _vm.DeleteLine();

    private void AddSilo_Click(object sender, RoutedEventArgs e) => _vm.AddSilo();
    private void DeleteSilo_Click(object sender, RoutedEventArgs e) => _vm.DeleteSilo();

    private void AddPendant_Click(object sender, RoutedEventArgs e) => _vm.AddPendant();
    private void DeletePendant_Click(object sender, RoutedEventArgs e) => _vm.DeletePendant();

    private void ElevatorTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is PendantViewModel pv)
        {
            var silo = _vm.Lines.SelectMany(l => l.Silos).FirstOrDefault(s => s.Pendants.Contains(pv));
            if (silo != null) _vm.SelectSilo(silo);
            _vm.SelectPendant(pv);
        }
        else if (e.NewValue is SiloViewModel sv)
        {
            _vm.SelectSilo(sv);
        }
        else if (e.NewValue is LineViewModel lv)
        {
            _vm.SelectedLine = lv;
        }
    }

    // === BKT-12 CRUD ===

    private void AddBkt12Line_Click(object sender, RoutedEventArgs e) => _vm.AddBkt12Line();
    private void DeleteBkt12Line_Click(object sender, RoutedEventArgs e) => _vm.DeleteBkt12Line();
    private void AddBkt12Block_Click(object sender, RoutedEventArgs e) => _vm.AddBkt12Block();
    private void DeleteBkt12Block_Click(object sender, RoutedEventArgs e) => _vm.DeleteBkt12Block();

    private void Bkt12Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is Bkt12BlockViewModel bv)
        {
            _vm.SelectedBkt12Block = bv;
            _vm.SelectedBkt12Line = _vm.Bkt12Lines.FirstOrDefault(l => l.Blocks.Contains(bv));
            ThermoGrid.ItemsSource = bv.Thermopendants;
        }
        else if (e.NewValue is Bkt12LineViewModel lv)
        {
            _vm.SelectedBkt12Line = lv;
            _vm.SelectedBkt12Block = null;
            ThermoGrid.ItemsSource = null;
        }
    }

    private void AddThermoSlot_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedBkt12Block == null) return;
        var slot = new ThermopendantSlotViewModel
        {
            PositionIndex = _vm.SelectedBkt12Block.Thermopendants.Count,
            PointCount = 30
        };
        _vm.SelectedBkt12Block.Thermopendants.Add(slot);
    }

    private void DeleteThermoSlot_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedBkt12Block == null || ThermoGrid.SelectedItem == null) return;
        _vm.SelectedBkt12Block.Thermopendants.Remove((ThermopendantSlotViewModel)ThermoGrid.SelectedItem);
    }

    // === Server control ===

    private void BrowseExe_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Выберите Termometriya.Server.exe"
        };
        if (dlg.ShowDialog() == true)
        {
            ServerPathBox.Text = dlg.FileName;
            ServerPathBox.ToolTip = dlg.FileName;
        }
    }

    private void StartServer_Click(object sender, RoutedEventArgs e)
    {
        _vm.ServerManager.ExePath = ServerPathBox.Text;
        _vm.ServerManager.Args = ServerArgsBox.Text;
        _vm.ServerManager.Start();
        UpdateServerStatus();
    }

    private void StopServer_Click(object sender, RoutedEventArgs e)
    {
        _vm.ServerManager.Stop();
        UpdateServerStatus();
    }

    private void RestartServer_Click(object sender, RoutedEventArgs e)
    {
        _vm.ServerManager.ExePath = ServerPathBox.Text;
        _vm.ServerManager.Args = ServerArgsBox.Text;
        _vm.ServerManager.Restart();
        UpdateServerStatus();
    }

    private void UpdateServerStatus()
    {
        ServerStatus.Text = _vm.ServerManager.IsRunning ? "⚫ РАБОТАЕТ" : "⚪ ОСТАНОВЛЕН";
        ServerStatus.Foreground = _vm.ServerManager.IsRunning
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen)
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
    }
}
