using System;

namespace SE2HDR.Settings.Elements;

// Hides a setting when the plugin resolved to SDR output.
[AttributeUsage(AttributeTargets.Property)]
internal class HdrOnlyAttribute : Attribute;
