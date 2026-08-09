# SE2 Tonemapping + HDR
Space Engineers 2 plugin which implements HDR10 output for the game, plus a choice of alternative tonemapping curves that also work on an SDR display.


# HDR and SDR
On startup the plugin checks if you have an HDR-capable display and automatically selects the correct mode. 

In SDR mode, the plugin is limited to only modifying the tonemapping curve (AgX / Uchimura GT).
In HDR mode, the plugin patches code related to the game's output buffers to make them 10-bit. In addition to tonemapping curves (AgX / Uchimura GT / modified HDR Hable), color transformations and PQ encoding are added. HDR parameters are auto-detected from your display's capabilities.


# Installing
1. Install [Pulsar](https://github.com/SpaceGT/Pulsar)
2. Open the plugin list, find **Tonemapping + HDR**, enable it.
3. Restart the game.


# Configuring
Click the plugin's settings button in Pulsar's plugin list:

| Setting | Default   | Meaning                                                                                                                                 |
|---|-----------|-----------------------------------------------------------------------------------------------------------------------------------------|
| `Enabled` | on        | Controls if the plugin is enabled.                                                                                                      |
| `OutputMode` | Automatic | Whether to output HDR10. `Automatic` is based on display support, `Force HDR` and `Force SDR` override it.                                      |
| `Override peak nits` | off       | HDR only. Sets the peak luminance from the slider below instead of from what the display reports.                                       |
| `PeakNits` | 1000      | HDR only. Peak luminance used in tonemapping. Only used while the override is ticked.                                    |
| `PaperWhiteNits` | 200       | HDR only. Luminance for the UI, HUD and the white point.                                                                                |
| `TonemapMode` | Hable HDR | Tonemapper to use. Multiple are available.                                                                                              |
| `Oversaturation` | 0         | HDR only. 0 keeps Rec.709 colours accurate. 1 reinterprets them as Rec.2020, stretching them across the wider gamut - not correct, but more vivid. |
| `Dither` | on        | Dithers the output before it is quantised. Removes banding in smooth gradients.                                                         |

Everything except `Enabled` and `OutputMode` applies immediately.


# Compatibility
This plugin modifies tonemapping, swapchain, and UI shaders. Other plugins that modify the same areas will likely not be compatible, although UI shader changes are minor and could continue to work.
SE2 is under active development and may break plugins with future updates. This is expected. If this happens, report it in [Issues](https://github.com/matelemons/SE2_HDR/issues) so I can fix it.

Plugin last updated for SE2 `2.3.0.2798`.
In order for HDR functionality to work, you should be using a recent version of Windows 10/11.

In case you spot reproducible issues or incompatibilities caused by the plugin, feel free to report them in [Issues](https://github.com/matelemons/SE2_HDR/issues) as well.

# Technical details
The game's rendering pipeline is already mostly in HDR, so the changes required to get HDR output are rather minimal. Tonemapping from HDR to SDR only happens near the end of the pipeline, so we only have to patch stuff there.

This plugin has two components:
1. Code patches use Harmony. Most of these patches are around the swapchain methods and a bit of drawing code. Most of them simply swap the requested format to an HDR one.
2. Shader patches use string substitution on the shader source, applied in memory. The tonemapping shader gets a Rec709 -> Rec2020 conversion and a PQ curve. Multiple UI shaders are also modified, since UI is drawn after tonemapping. This is not ideal, but it does seem to work.

In SDR neither of those is needed beyond the tonemapping shader itself. Every format patch is skipped, and so are the UI shader edits. The tonemapping curves are shared between the two modes.

Config values are adjustable at runtime by injecting the extra values to existing buffers. Tonemapping uses an unusued padding int, while Slug shaders get widened by one float4.
This plugin might not be compatible with other plugins that modify rendering in a similar way.

Note that all rendering and assets in the game are in Rec.709 color space, as far as I'm aware. This means it's not possible to (easily) get accurate wide gamut output. You can only get the benefits of 10-bit signal, more highlight details and the increased brightness range with this plugin.

# Acknowledgements
[HdrRender2](https://github.com/lolifamily/HdrRender2) is a different plugin which achieves a similar goal. While it takes a different approach and was developed independently, it was still useful to cross-check implementations.
[allenwp curve](https://allenwp.com/blog/2025/05/29/allenwp-tonemapping-curve/) for the curve used for AgX.