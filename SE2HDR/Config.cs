using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using SE2HDR.Settings.Elements;

namespace SE2HDR;

public class Config : INotifyPropertyChanged
{
    #region Options

    private bool enabled = true;
    private int peakNits = 1000;
    private int uiNits = 200;

    #endregion

    #region User interface

    [XmlIgnore]
    public readonly string Title = "HDR10 Plugin Settings";

    [Separator("Enabling or disabling needs a game restart.")]

    [Checkbox(description: "Enable HDR output. Unticking disables the plugin. Takes effect after a game restart.")]
    public bool Enabled
    {
        get => enabled;
        set => SetField(ref enabled, value);
    }

    [Slider(400f, 4000f, 50f, SliderAttribute.SliderType.Integer,
        description: "Peak luminance for tonemapping (in nits). Set it to your display's peak brightness.")]
    public int PeakNits
    {
        get => peakNits;
        set => SetField(ref peakNits, value);
    }

    [Slider(80f, 400f, 5f, SliderAttribute.SliderType.Integer,
        description: "Peak luminance for the UI and HUD (in nits). Also known as Paper White brightness.")]
    public int UiNits
    {
        get => uiNits;
        set => SetField(ref uiNits, value);
    }

    #endregion

    #region Property change notification boilerplate

    public static readonly Config Default = new Config();
    public static Config Current = ConfigStorage.Load();

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
