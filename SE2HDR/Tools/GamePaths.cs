using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Keen.VRage.Library.Diagnostics;

namespace SE2HDR.Tools;

// Locates the game's installation root.
internal static class GamePaths
{
    public const string ShaderSubDirectory = @"VRage\GameData\Engine\Shaders";

    private const int MaxLevelsUp = 3;

    public static string FindGameRoot()
    {
        foreach (var candidate in Anchors())
        {
            var dir = SafeDirectory(candidate);
            for (var level = 0; dir != null && level <= MaxLevelsUp; level++, dir = dir.Parent)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ShaderSubDirectory)))
                    return dir.FullName;
            }
        }

        Log.Default.WriteLine(LogSeverity.Error,
            $"[{Plugin.Name}] Could not locate the game installation root - no {ShaderSubDirectory} found.");
        return null;
    }

    private static IEnumerable<string> Anchors()
    {
        yield return AssemblyDirectory(typeof(Log).Assembly);
        yield return AssemblyDirectory(Assembly.GetEntryAssembly());
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();
    }

    private static string AssemblyDirectory(Assembly assembly)
    {
        // Location is empty for assemblies loaded from memory.
        var location = assembly?.Location;
        return string.IsNullOrEmpty(location) ? null : Path.GetDirectoryName(location);
    }

    private static DirectoryInfo SafeDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        try
        {
            return new DirectoryInfo(path);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
