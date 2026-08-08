using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using SE2HDR.Settings.Elements;
using SE2HDR.Tools;

namespace SE2HDR;


public enum OutputMode
{
    [Option("Automatic", order: 0,
        description: "Output HDR10 when Windows reports the display is in HDR mode, otherwise stay on "
                     + "SDR and only replace the tonemapping curve.")]
    Auto = 0,

    [Option("Force HDR", order: 1,
        description: "Always output HDR10, even when no HDR display was detected. On an SDR display it will look washed out.")]
    ForceHdr = 1,

    [Option("Force SDR", order: 2,
        description: "Don't output HDR10. You can still adjust change tonemapping curves.")]
    ForceSdr = 2,
}

public enum TonemapMode
{
    [Option("Legacy", order: 0, sdrLabel: "Hable (original)",
        description: "Encode after the game's Hable curve and its clamp. Brighter, but since values were "
                     + "already clamped, this just stretches the brightness. Not recommended.",
        sdrDescription: "The game's original curve.")]
    Legacy = 0,

    [Option("AgX", order: 3,
        description: "Replace Hable with an AgX curve. Slightly higher contrast, more muted colors. Recommended, but subjective.",
        sdrDescription: "Replace Hable with an AgX curve. Slightly higher contrast, more muted colors.")]
    AgxHdr = 1,
    
    // Not useful in SDR
    [Option("Hable Extended", order: 1, hdrOnly: true,
        description: "Keeps original Hable, but remove the clamp. Slightly better than Legacy."
                     + " Not recommended.")]
    HableExtended = 2,

    // Not useful in SDR
    [Option("Hable HDR", order: 2, hdrOnly: true,
        description: "Hable below 70% of paper white, then an HDR shoulder. "
                     + "Looks close to the original, recommended.")]
    HableHdr = 3,

    [Option("Uchimura GT", order: 4,
        description: "aka the 'Gran Turismo' curve. Deeper shadows, changes the visual style of the game somewhat. Recommended, but subjective.",
        sdrDescription: "aka the 'Gran Turismo' curve. Deeper shadows, changes the visual style of the game somewhat.")]
    UchimuraGt = 4,
}

public class Config : INotifyPropertyChanged
{
    #region Options

    private bool enabled = true;
    private OutputMode outputMode = OutputMode.Auto;
    private bool overridePeakNits;
    private int peakNits = 1000;
    private int paperWhiteNits = 200;
    private TonemapMode tonemapMode = TonemapMode.HableHdr;
    private float oversaturation;
    private bool dither = true;

    #endregion

    #region User interface

    [XmlIgnore]
    public readonly string Title = "HDR10 Plugin Settings";

    [Separator("Display")]

    [XmlIgnore]
    [Info]
    public string DisplayStatus => RenderMode.StatusText;

    [Separator("These need a game restart to take effect.")]

    [Checkbox(description: "Enable the plugin. Unticking disables the plugin. "
                           + "Takes effect after a game restart.")]
    public bool Enabled
    {
        get => enabled;
        set => SetField(ref enabled, value);
    }

    [Dropdown(description: "Whether to output HDR10 or SDR. "
                           + "Takes effect after a game restart.")]
    public OutputMode OutputMode
    {
        get => outputMode;
        set => SetField(ref outputMode, value);
    }

    [Separator("Display settings")]
    [HdrOnly]
    [Checkbox(label: "Override peak nits",
        description: "Set the peak luminance instead of using the value reported by Windows. ")]
    public bool OverridePeakNits
    {
        get => overridePeakNits;
        set => SetField(ref overridePeakNits, value);
    }

    [HdrOnly]
    [EnabledBy(nameof(OverridePeakNits))]
    [Slider(400f, 2000f, 10f, SliderAttribute.SliderType.Integer,
        description: "Peak luminance for tonemapping (in nits). Only used while the override above is "
                     + "ticked, otherwise the reported peak is used.")]
    public int PeakNits
    {
        get => peakNits;
        set => SetField(ref peakNits, value);
    }

    [HdrOnly]
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

    [HdrOnly]
    [Slider(0f, 1f, 0.05f, SliderAttribute.SliderType.Float,
        description: "0 keeps Rec.709 colours accurate. 1 reinterprets them as Rec.2020, stretching "
                     + "them across the wider gamut. Not correct, but more vivid.")]
    public float Oversaturation
    {
        get => oversaturation;
        set => SetField(ref oversaturation, value);
    }

    [Checkbox(description: "Dither the output before it is quantised, to break up banding in gradients.")]
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
