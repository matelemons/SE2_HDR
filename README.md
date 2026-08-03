# SE2 HDR10

Space Engineers 2 plugin which implements HDR10 output for the game.

# Disclaimer
This plugin is under development and the amount of testing I can do is limited. In case you spot reproducible issues caused by the plugin, feel free to report them in [Issues](https://github.com/Matusson/SE2_HDR/issues).
SE2 is being actively developed, and rendering code may change, breaking the plugin. If this happens, report it in Issues and it will be fixed.

Also, do note that the plugin does not check for HDR capabilities on your system. I assume that if you're installing this, you have an HDR capable display and you have HDR enabled in Windows. **You will not see any visual improvements without an HDR display.** You should also be using a recent version of Windows 10/11.

Plugin last updated for SE2 `2.3.0.2798`


# Installing
1. Install [Pulsar](https://github.com/SpaceGT/Pulsar)
2. Open the plugin list, find **HDR10**, enable it.
3. Restart the game. The output should be in HDR.


# Configuring
Click the plugin's settings button in Pulsar's plugin list:

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | on | Controls if the plugin is enabled. |
| `PeakNits` | 1000 | Peak luminance used in HDR tonemapping. Set it to your display's peak brightness. |
| `UiNits` | 200 | Luminance for the UI and HUD, also known as "Paper White" brightness. |



# Technical details
The game's rendering pipeline is already mostly in HDR, so the changes required to get HDR output are rather minimal. Tonemapping from HDR to SDR only happens near the end of the pipeline, so we only have to patch stuff there.

This plugin has two components:
1. Code patches use Harmony. Most of these patches are around the swapchain methods and a bit of drawing code. Most of them simply swap the requested format to an HDR one.
2. Shader patches use string substitution on the shader source, applied in memory. The tonemapping shader gets a Rec709 -> Rec2020 conversion and a PQ curve. Multiple UI shaders are also modified, since UI is drawn after tonemapping. This is not ideal, but it does seem to work.

Config values are adjustable at runtime by injecting the extra values to existing buffers. Tonemapping uses an unusued padding int, while Slug shaders get widened by one float4.
This plugin might not be compatible with other plugins that modify rendering in a similar way.

Note that all rendering and assets are in Rec709 color space, as far as I'm aware. This means it's not possible to (easily) get accurate wide gamut output. You can only get the benefits of 10-bit signal, higher highlight details and the increased brightness range with this plugin. The differences are likely to be most noticeable on a MiniLED display rather than OLED.