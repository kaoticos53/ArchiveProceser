using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.ViewModels;

public partial class NodeViewModel : ObservableObject
{
    private readonly IFlowNode _nodeInstance;

    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _title = "Node";

    [ObservableProperty]
    private string _category = "General";

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _nodeTypeName = string.Empty;

    [ObservableProperty]
    private Point _location;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private double _collapsedWidth = 200;

    [ObservableProperty]
    private double _expandedWidth = 340;

    [ObservableProperty]
    private double _width = 200;

    [ObservableProperty]
    private double _maxWidth = 600;

    [ObservableProperty]
    private string _headerColor = "#202430";

    [ObservableProperty]
    private string _accentColor = "#818CF8";

    public ObservableCollection<PortViewModel> InputPorts { get; } = [];
    public ObservableCollection<PortViewModel> OutputPorts { get; } = [];
    public ObservableCollection<NodeParameterViewModel> Parameters { get; } = [];

    public NodeViewModel(IFlowNode node, Point location)
    {
        _nodeInstance = node;
        _id = node.Id;
        _title = node.Name;
        _category = node.Category;
        _description = node.Description;
        _nodeTypeName = node.GetType().FullName ?? node.GetType().Name;
        _location = location;

        SetDefaultColorsForCategory(_category);

        foreach (var inPort in node.Inputs)
        {
            InputPorts.Add(new PortViewModel(this, inPort.Name, inPort.DisplayName, inPort.Direction, inPort.DataType));
        }

        foreach (var outPort in node.Outputs)
        {
            OutputPorts.Add(new PortViewModel(this, outPort.Name, outPort.DisplayName, outPort.Direction, outPort.DataType));
        }

        foreach (var (k, v) in node.Parameters)
        {
            Parameters.Add(new NodeParameterViewModel(k, v, nodeOwner: this));
        }

        LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    public bool IsVariableInjectorNode => NodeTypeName.Contains("VariableInjectorNode", StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    public void AddVariable()
    {
        int count = Parameters.Count + 1;
        string newKey = $"Variable_{count}";
        while (_nodeInstance.Parameters.ContainsKey(newKey) || Parameters.Any(p => p.Key.Equals(newKey, StringComparison.OrdinalIgnoreCase)))
        {
            count++;
            newKey = $"Variable_{count}";
        }

        _nodeInstance.Parameters[newKey] = "";
        var paramVM = new NodeParameterViewModel(newKey, "", nodeOwner: this);
        Parameters.Add(paramVM);
    }

    [RelayCommand]
    public void RemoveParameter(NodeParameterViewModel param)
    {
        if (param == null) return;
        Parameters.Remove(param);
        _nodeInstance.Parameters.Remove(param.Key);
    }

    public void OnParameterKeyRenamed(string oldKey, string newKey, object? value)
    {
        if (oldKey != newKey)
        {
            _nodeInstance.Parameters.Remove(oldKey);
            if (!string.IsNullOrWhiteSpace(newKey))
            {
                _nodeInstance.Parameters[newKey] = value;
            }
        }
    }

    public void OnParameterValueChanged(string key, object? newValue)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            _nodeInstance.Parameters[key] = newValue;
        }
    }

    private void OnLanguageChanged(object? sender, CultureInfo culture)
    {
        Title = _nodeInstance.Name;
        Description = _nodeInstance.Description;
        Category = _nodeInstance.Category;
    }

    [RelayCommand]
    public void ChangeColor(string colorHex)
    {
        HeaderColor = GetHeaderColorFromAccent(colorHex);
        AccentColor = colorHex;
    }

    private static readonly int[] CustomColors = new int[16];

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct CHOOSECOLOR
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public int rgbResult;
        public IntPtr lpCustColors;
        public int Flags;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool ChooseColor(ref CHOOSECOLOR cc);

    [RelayCommand]
    public void ChooseCustomColor()
    {
        var gch = GCHandle.Alloc(CustomColors, GCHandleType.Pinned);
        try
        {
            var cc = new CHOOSECOLOR();
            cc.lStructSize = Marshal.SizeOf(cc);
            cc.lpCustColors = gch.AddrOfPinnedObject();
            cc.Flags = 0x00000002 | 0x00000001; // CC_FULLOPEN | CC_RGBINIT

            if (ChooseColor(ref cc))
            {
                int rgb = cc.rgbResult;
                byte r = (byte)(rgb & 0xFF);
                byte g = (byte)((rgb >> 8) & 0xFF);
                byte b = (byte)((rgb >> 16) & 0xFF);
                string hexColor = $"#{r:X2}{g:X2}{b:X2}";
                ChangeColor(hexColor);
            }
        }
        finally
        {
            gch.Free();
        }
    }

    public void SetDefaultColorsForCategory(string category)
    {
        switch (category.ToLowerInvariant())
        {
            case "filesystem":
                HeaderColor = "#143328";
                AccentColor = "#10B981"; // Emerald
                break;
            case "archives":
                HeaderColor = "#362713";
                AccentColor = "#F59E0B"; // Amber
                break;
            case "images":
                HeaderColor = "#301438";
                AccentColor = "#A855F7"; // Purple
                break;
            default:
                HeaderColor = "#1F2433";
                AccentColor = "#818CF8"; // Indigo
                break;
        }
    }

    public static string GetHeaderColorFromAccent(string accentHex)
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(accentHex);
            byte r = (byte)(color.R * 0.25);
            byte g = (byte)(color.G * 0.25);
            byte b = (byte)(color.B * 0.25);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
        catch
        {
            return "#202430";
        }
    }

    partial void OnIsExpandedChanged(bool oldValue, bool newValue)
    {
        if (oldValue == newValue) return;

        if (newValue) // Expanding
        {
            CollapsedWidth = Width;
            Width = ExpandedWidth;
        }
        else // Collapsing
        {
            ExpandedWidth = Width;
            Width = CollapsedWidth;
        }
    }

    public void UpdateWidth(double newWidth)
    {
        Width = Math.Clamp(newWidth, 180, MaxWidth);
        if (IsExpanded)
        {
            ExpandedWidth = Width;
        }
        else
        {
            CollapsedWidth = Width;
        }
    }
}
