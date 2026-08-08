using System;

namespace SE2HDR.Settings.Elements;

// Greys a setting out while the named bool property on Config is false.
[AttributeUsage(AttributeTargets.Property)]
internal class EnabledByAttribute : Attribute
{
    public readonly string PropertyName;

    public EnabledByAttribute(string propertyName)
    {
        PropertyName = propertyName;
    }
}
