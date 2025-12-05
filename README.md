# SE2 HDR
Space Engineers 2 mod which attempts to implement HDR output to the game.

# Disclaimer
This is mostly a proof of concept and it's possible that this mod could cause crashes or not work properly. In case you spot reproducible issues caused by this mod, feel free to report them in [Issues](https://github.com/Matusson/SE2_HDR/issues). 

Also, do note that currently the mod does not check for HDR capabilities on your system. I assume that if you're installing this mod, you have an HDR capable display and you have HDR enabled in Windows. You should also be using a recent version of Windows 10/11. 
Mod last updated for SE2 `2.0.2.33` 

# Installing
1. Download SE2HDR.dll and 0Harmony.dll from [Releases](https://github.com/Matusson/SE2_HDR/releases)
2. Navigate to `C:\Users\[your-windows-username]\AppData\Roaming\SpaceEngineers2` (replace `[your-windows-username]` with your Windows username)
3. Create a `Plugins` directory (if you don't already have one)
4. Move both DLLs downloaded in the first step into the Plugins folder
5. On Steam, go to Space Engineers 2 -> Properties, and in the "Launch Options", put this in: `-plugins:C:\Users\[your-windows-username]\AppData\Roaming\SpaceEngineers2\Plugins\SE2HDR.dll` (this should be the path to your Plugins folder + the SE2HDR dll)
6. Launch the game. The output should be in HDR.

# Uninstalling
1. Remove the `-plugins` launch option from Steam.
2. Open the game's installation directory (on Steam: Space Engineers 2 -> Manage -> Browse Local Files)
3. Navigate to `VRage` -> `GameData` -> `Engine`
4. Remove the `Shaders` directory, then rename `Shaders.backup` to `Shaders` 
5. Launch the game. The output should be back to SDR.

# Technical details
The game's rendering pipeline is already mostly in HDR, so the changes required to get HDR output are rather minimal. Tonemapping from HDR to SDR only happens near the end of the pipeline, so we only have to patch stuff there.

This mod has two components:
1. Code patches use Harmony. Most of these patches are around the swapchain methods and a bit of drawing code. Most of them simply swap the requested format to an HDR one.
2. Shader patches use string substitution in the original shader source files. The tonemapping shader is modified to include a Rec709 -> Rec2020 conversion and a PQ curve. Multiple UI shaders are also modified, since UI is drawn after tonemapping. This is not ideal, but it does seem to work. The game automatically recompiles the shaders if the source files change. Before the mod modifies any shaders, a backup of the entire Shaders folder is created (though it can always be restored by verifying file integrity on Steam).

Note that all rendering is in Rec709 color space, as far as I'm aware. This means it's not possible to (easily) get accurate wide gamut output. You can only get the benefits of 10-bit signal and the increased brightness range with this mod. The differences are likely to be most noticeable on a MiniLED display rather than OLED, especially on planets which get bright.
