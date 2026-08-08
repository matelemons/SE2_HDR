using System;

namespace SE2HDR.Settings.Elements;

// How one enum member appears in a Dropdown. Order is explicit because the members' numeric
// values are wire format - they are packed into the frame constant buffer and saved to the
// config file - so the list can't be reordered by renumbering the enum.
[AttributeUsage(AttributeTargets.Field)]
internal class OptionAttribute : Attribute
{
    public readonly string Label;
    public readonly int Order;
    public readonly string Description;

    public OptionAttribute(string label = null, int order = 0, string description = null)
    {
        Label = label;
        Order = order;
        Description = description;
    }
}
