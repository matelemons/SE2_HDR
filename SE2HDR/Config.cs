using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using SE2HDR.Settings.Elements;

namespace SE2HDR;


public enum TonemapMode
{
    [Option("Legacy", order: 0,
        description: "Encode after the game's Hable curve and its clamp. Brighter, but since values were "
                     + "already clamped, this just stretches the brightness. Not recommended.")]
    Legacy = 0,

    [Option("AgX", order: 3,
        description: "Replace Hable with an HDR AgX curve. Changes the visual style of the game, " +
                     "tends to give it more contrast."
                     + " Recommended, but subjective.")]
    AgxHdr = 1,

    [Option("Hable Extended", order: 1,
        description: "Keeps original Hable, but remove the clamp. Slightly better than Legacy."
                     + " Not recommended.")]
    HableExtended = 2,

    [Option("Hable HDR", order: 2,
        description: "Hable below 70% of paper white, then an HDR shoulder. "
                     + "Looks close to the original, recommended.")]
    HableHdr = 3,

    [Option("Uchimura GT", order: 4,
        description: "aka the 'Gran Turismo' curve. Changes the visual style of the game somewhat."
                     + " Recommended, but subjective.")]
    UchimuraGt = 4,
}

public class Config : INotifyPropertyChanged
{
    #region Options

    private bool enabled = true;
    private int peakNits = 1000;
    private int paperWhiteNits = 200;
    private TonemapMode tonemapMode = TonemapMode.HableHdr;
    private float oversaturation;
    private bool dither = true;

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

    [Separator("Display settings")]
    
    [Slider(400f, 4000f, 50f, SliderAttribute.SliderType.Integer,
        description: "Peak luminance for tonemapping (in nits). Set it to your display's peak brightness.")]
    public int PeakNits
    {
        get => peakNits;
        set => SetField(ref peakNits, value);
    }

    [Slider(80f, 400f, 5f, SliderAttribute.SliderType.Integer,
        description: "Luminance of the UI, HUD and of the game's own \"white\" (in nits).")]
    public int PaperWhiteNits
    {
        get => paperWhiteNits;
        set => SetField(ref paperWhiteNits, value);
    }

    // Config files written before the rename call this UiNits. The Specified flag is only set by
    // deserialization, so reading one keeps working while saving never writes the old element.
    [XmlElement("UiNits")]
    public int LegacyUiNits
    {
        get => paperWhiteNits;
        set => paperWhiteNits = value;
    }

    [XmlIgnore]
    public bool LegacyUiNitsSpecified { get; set; }

    [Separator("Tonemapping settings")]

    [Dropdown(description:
        "Tonemapping curve to apply.")]
    public TonemapMode TonemapMode
    {
        get => tonemapMode;
        set => SetField(ref tonemapMode, value);
    }

    [Slider(0f, 1f, 0.05f, SliderAttribute.SliderType.Float,
        description: "0 keeps Rec.709 colours accurate. 1 reinterprets them as Rec.2020, stretching "
                     + "them across the wider gamut. Not correct, but more vivid.")]
    public float Oversaturation
    {
        get => oversaturation;
        set => SetField(ref oversaturation, value);
    }

    [Checkbox(description: "Dither the output before it is quantised to 10 bits.")]
    public bool Dither
    {
        get => dither;
        set => SetField(ref dither, value);
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
