using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Termometriya.Configurator.Models;
using Termometriya.Configurator.Services;

namespace Termometriya.Configurator.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ApiClient _api = new();

    // === Server tab ===
    public ServerProcessManager ServerManager { get; }

    // === Elevator config ===
    public ObservableCollection<CultureConfig> Cultures { get; } = [];
    public ObservableCollection<LineViewModel> Lines { get; } = [];

    private CultureConfig? _selectedCulture;
    public CultureConfig? SelectedCulture
    {
        get => _selectedCulture;
        set { _selectedCulture = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanDeleteCulture)); }
    }

    private LineViewModel? _selectedLine;
    public LineViewModel? SelectedLine
    {
        get => _selectedLine;
        set { _selectedLine = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanDeleteLine)); SelectSilo(null); }
    }

    private SiloViewModel? _selectedSilo;
    public SiloViewModel? SelectedSilo
    {
        get => _selectedSilo;
        set { _selectedSilo = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanDeleteSilo)); SelectPendant(null); }
    }

    private PendantViewModel? _selectedPendant;
    public PendantViewModel? SelectedPendant
    {
        get => _selectedPendant;
        set { _selectedPendant = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanDeletePendant)); }
    }

    public bool CanDeleteCulture => SelectedCulture != null;
    public bool CanDeleteLine => SelectedLine != null;
    public bool CanDeleteSilo => SelectedSilo != null;
    public bool CanDeletePendant => SelectedPendant != null;

    // === BKT-12 config ===
    public ObservableCollection<Bkt12LineViewModel> Bkt12Lines { get; } = [];

    private Bkt12LineViewModel? _selectedBkt12Line;
    public Bkt12LineViewModel? SelectedBkt12Line
    {
        get => _selectedBkt12Line;
        set { _selectedBkt12Line = value; OnPropertyChanged(); SelectBlock(null); }
    }

    private Bkt12BlockViewModel? _selectedBkt12Block;
    public Bkt12BlockViewModel? SelectedBkt12Block
    {
        get => _selectedBkt12Block;
        set { _selectedBkt12Block = value; OnPropertyChanged(); }
    }

    // === Common ===
    private string _statusText = "Готов";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public string ServerUrl
    {
        get => _api.BaseUrl;
        set => _api.BaseUrl = value;
    }

    public MainViewModel(ListBox outputBox, Action onRefreshNeeded)
    {
        ServerManager = new ServerProcessManager(outputBox);
    }

    // === Commands ===

    public async Task LoadElevatorConfigAsync()
    {
        IsLoading = true;
        try
        {
            var cfg = await _api.GetElevatorConfigAsync();
            if (cfg == null) return;
            Cultures.Clear();
            foreach (var c in cfg.Cultures) Cultures.Add(c);
            Lines.Clear();
            foreach (var l in cfg.Lines) Lines.Add(LineViewModel.FromModel(l));
            StatusText = "Конфигурация элеватора загружена";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally { IsLoading = false; }
    }

    public async Task SaveElevatorConfigAsync()
    {
        IsLoading = true;
        try
        {
            var cfg = new ElevatorConfig
            {
                Cultures = Cultures.ToList(),
                Lines = Lines.Select(l => l.ToModel()).ToList()
            };
            await _api.SaveElevatorConfigAsync(cfg);
            StatusText = "Конфигурация элеватора сохранена";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally { IsLoading = false; }
    }

    public async Task LoadHardwareConfigAsync()
    {
        IsLoading = true;
        try
        {
            var cfg = await _api.GetHardwareConfigAsync();
            if (cfg == null) return;
            Bkt12Lines.Clear();
            foreach (var l in cfg.Lines) Bkt12Lines.Add(Bkt12LineViewModel.FromModel(l));
            StatusText = "Конфигурация БКТ-12 загружена";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally { IsLoading = false; }
    }

    public async Task SaveHardwareConfigAsync()
    {
        IsLoading = true;
        try
        {
            var cfg = new Bkt12HardwareConfig
            {
                Lines = Bkt12Lines.Select(l => l.ToModel()).ToList()
            };
            await _api.SaveHardwareConfigAsync(cfg);
            StatusText = "Конфигурация БКТ-12 сохранена";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally { IsLoading = false; }
    }

    // === Elevator CRUD ===

    public void AddCulture()
    {
        var c = new CultureConfig { Name = "Новая культура" };
        Cultures.Add(c);
        SelectedCulture = c;
    }

    public void DeleteCulture()
    {
        if (SelectedCulture == null) return;
        Cultures.Remove(SelectedCulture);
        SelectedCulture = null;
    }

    public void AddLine()
    {
        var l = new LineViewModel
        {
            Name = $"Линия {Lines.Count + 1}",
            DisplayOrder = Lines.Count + 1
        };
        Lines.Add(l);
        SelectedLine = l;
    }

    public void DeleteLine()
    {
        if (SelectedLine == null) return;
        Lines.Remove(SelectedLine);
        SelectedLine = null;
    }

    public void AddSilo()
    {
        if (SelectedLine == null) return;
        var s = new SiloViewModel
        {
            Number = (SelectedLine.Silos.Count > 0 ? SelectedLine.Silos.Max(x => x.Number) : 0) + 1,
            Capacity = 1000
        };
        SelectedLine.Silos.Add(s);
        SelectSilo(s);
    }

    public void DeleteSilo()
    {
        if (SelectedSilo == null || SelectedLine == null) return;
        SelectedLine.Silos.Remove(SelectedSilo);
        SelectSilo(null);
    }

    public void AddPendant()
    {
        if (SelectedSilo == null) return;
        var p = new PendantViewModel
        {
            PositionIndex = SelectedSilo.Pendants.Count,
            PointCount = 16
        };
        SelectedSilo.Pendants.Add(p);
        SelectPendant(p);
    }

    public void DeletePendant()
    {
        if (SelectedPendant == null || SelectedSilo == null) return;
        SelectedSilo.Pendants.Remove(SelectedPendant);
        SelectPendant(null);
    }

    // === BKT-12 CRUD ===

    public void AddBkt12Line()
    {
        var l = new Bkt12LineViewModel { LineNumber = Bkt12Lines.Count + 1 };
        Bkt12Lines.Add(l);
        SelectedBkt12Line = l;
    }

    public void DeleteBkt12Line()
    {
        if (SelectedBkt12Line == null) return;
        Bkt12Lines.Remove(SelectedBkt12Line);
        SelectedBkt12Line = null;
    }

    public void AddBkt12Block()
    {
        if (SelectedBkt12Line == null) return;
        var b = new Bkt12BlockViewModel { SlaveId = 1, SimulationPort = 1501 };
        SelectedBkt12Line.Blocks.Add(b);
        SelectedBkt12Block = b;
    }

    public void DeleteBkt12Block()
    {
        if (SelectedBkt12Block == null || SelectedBkt12Line == null) return;
        SelectedBkt12Line.Blocks.Remove(SelectedBkt12Block);
        SelectedBkt12Block = null;
    }

    // === Selection helpers ===

    public void SelectSilo(SiloViewModel? s)
    {
        _selectedSilo = s;
        OnPropertyChanged(nameof(SelectedSilo));
        OnPropertyChanged(nameof(CanDeleteSilo));
        SelectPendant(null);
    }

    public void SelectPendant(PendantViewModel? p)
    {
        _selectedPendant = p;
        OnPropertyChanged(nameof(SelectedPendant));
        OnPropertyChanged(nameof(CanDeletePendant));
    }

    public void SelectBlock(Bkt12BlockViewModel? b)
    {
        _selectedBkt12Block = b;
        OnPropertyChanged(nameof(SelectedBkt12Block));
    }

    public void Dispose() => _api.Dispose();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// === Elevator sub-viewmodels ===

public class LineViewModel : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public int DisplayOrder { get; set; }
    public ObservableCollection<SiloViewModel> Silos { get; set; } = [];

    public LineConfig ToModel() => new()
    {
        Name = Name, DisplayOrder = DisplayOrder,
        Silos = Silos.Select(s => s.ToModel()).ToList()
    };

    public static LineViewModel FromModel(LineConfig m)
    {
        var vm = new LineViewModel { Name = m.Name, DisplayOrder = m.DisplayOrder };
        foreach (var s in m.Silos) vm.Silos.Add(SiloViewModel.FromModel(s));
        return vm;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class SiloViewModel : INotifyPropertyChanged
{
    public int Number { get; set; }
    public double FillLevel { get; set; }
    public double Capacity { get; set; } = 1000;
    public string CultureName { get; set; } = "";
    public ObservableCollection<PendantViewModel> Pendants { get; set; } = [];

    public SiloConfig ToModel() => new()
    {
        Number = Number, FillLevel = FillLevel, Capacity = Capacity,
        CultureName = CultureName,
        Pendants = Pendants.Select(p => p.ToModel()).ToList()
    };

    public static SiloViewModel FromModel(SiloConfig m)
    {
        var vm = new SiloViewModel
        {
            Number = m.Number, FillLevel = m.FillLevel,
            Capacity = m.Capacity, CultureName = m.CultureName
        };
        foreach (var p in m.Pendants) vm.Pendants.Add(PendantViewModel.FromModel(p));
        return vm;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class PendantViewModel : INotifyPropertyChanged
{
    public int PositionIndex { get; set; }
    public int PointCount { get; set; } = 16;
    private bool _isCentral;

    public bool IsCentral
    {
        get => _isCentral;
        set { _isCentral = value; OnPropertyChanged(); }
    }

    public PendantConfig ToModel() => new() { PositionIndex = PositionIndex, PointCount = PointCount, IsCentral = IsCentral };

    public static PendantViewModel FromModel(PendantConfig m)
        => new() { PositionIndex = m.PositionIndex, PointCount = m.PointCount, IsCentral = m.IsCentral };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

// === BKT-12 sub-viewmodels ===

public class Bkt12LineViewModel : INotifyPropertyChanged
{
    public int LineNumber { get; set; }
    public ObservableCollection<Bkt12BlockViewModel> Blocks { get; set; } = [];

    public Bkt12Line ToModel() => new() { LineNumber = LineNumber, Blocks = Blocks.Select(b => b.ToModel()).ToList() };

    public static Bkt12LineViewModel FromModel(Bkt12Line m)
    {
        var vm = new Bkt12LineViewModel { LineNumber = m.LineNumber };
        foreach (var b in m.Blocks) vm.Blocks.Add(Bkt12BlockViewModel.FromModel(b));
        return vm;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class Bkt12BlockViewModel : INotifyPropertyChanged
{
    public int SlaveId { get; set; }
    public int SimulationPort { get; set; }
    public string ConnectionType { get; set; } = "simulation";
    public string? Host { get; set; }
    public int Port { get; set; } = 502;
    public string? PortName { get; set; }
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string SiloIdsStr { get; set; } = "";
    public ushort BaseRegister { get; set; } = 15;
    public ushort RegistersPerPendant { get; set; } = 30;
    public ObservableCollection<ThermopendantSlotViewModel> Thermopendants { get; set; } = [];

    public List<int> SiloIdsList => SiloIdsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0).Where(id => id > 0).ToList();

    public Bkt12Block ToModel()
    {
        var block = new Bkt12Block
        {
            SlaveId = SlaveId,
            SimulationPort = SimulationPort,
            Connection = ConnectionType == "simulation" ? null : new ModbusConnectionConfig
            {
                Type = ConnectionType, Host = Host, Port = Port, PortName = PortName,
                BaudRate = BaudRate, DataBits = DataBits, Parity = Parity, StopBits = StopBits
            },
            SiloIds = SiloIdsList
        };
        if (Thermopendants.Count > 0)
        {
            block.RegisterMap = new RegisterMapConfig
            {
                BaseRegister = BaseRegister,
                RegistersPerPendant = RegistersPerPendant,
                Thermopendants = Thermopendants.Select(t => t.ToModel()).ToList()
            };
        }
        return block;
    }

    public static Bkt12BlockViewModel FromModel(Bkt12Block m)
    {
        var vm = new Bkt12BlockViewModel
        {
            SlaveId = m.SlaveId,
            SimulationPort = m.SimulationPort,
            SiloIdsStr = string.Join(", ", m.SiloIds),
        };
        if (m.Connection != null)
        {
            vm.ConnectionType = m.Connection.Type;
            vm.Host = m.Connection.Host;
            vm.Port = m.Connection.Port;
            vm.PortName = m.Connection.PortName;
            vm.BaudRate = m.Connection.BaudRate;
            vm.DataBits = m.Connection.DataBits;
            vm.Parity = m.Connection.Parity;
            vm.StopBits = m.Connection.StopBits;
        }
        if (m.RegisterMap != null)
        {
            vm.BaseRegister = m.RegisterMap.BaseRegister;
            vm.RegistersPerPendant = m.RegisterMap.RegistersPerPendant;
            foreach (var t in m.RegisterMap.Thermopendants)
                vm.Thermopendants.Add(ThermopendantSlotViewModel.FromModel(t));
        }
        return vm;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class ThermopendantSlotViewModel : INotifyPropertyChanged
{
    public int PositionIndex { get; set; }
    public int PointCount { get; set; } = 30;

    public ThermopendantSlotConfig ToModel() => new() { PositionIndex = PositionIndex, PointCount = PointCount };

    public static ThermopendantSlotViewModel FromModel(ThermopendantSlotConfig m)
        => new() { PositionIndex = m.PositionIndex, PointCount = m.PointCount };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
