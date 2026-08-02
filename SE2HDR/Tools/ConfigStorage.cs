using System;
using System.IO;
using System.Xml.Serialization;
using Keen.VRage.Library.Diagnostics;

namespace SE2HDR;

public static class ConfigStorage
{
    private static string ConfigFilePath => Path.Combine(Plugin.Instance.DataDir, "Config.xml");

    public static void Save(Config config)
    {
        var path = ConfigFilePath;
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        using var writer = File.CreateText(path);
        new XmlSerializer(typeof(Config)).Serialize(writer, config);
    }

    public static Config Load()
    {
        var path = ConfigFilePath;
        if (!File.Exists(path))
        {
            var config = Config.Default;
            Save(config);
            return config;
        }

        try
        {
            using var reader = File.OpenText(path);
            return new XmlSerializer(typeof(Config)).Deserialize(reader) as Config ?? Config.Default;
        }
        catch (Exception)
        {
            Log.Default.WriteLine(LogSeverity.Warning, $"Failed to read config file: {path}");
        }

        return Config.Default;
    }
}
