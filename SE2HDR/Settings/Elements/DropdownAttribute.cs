using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace SE2HDR.Settings.Elements;

[AttributeUsage(AttributeTargets.Property)]
internal class DropdownAttribute : Attribute, IElement
{
    public readonly string Label;
    public readonly string Description;

    private static readonly Regex UnCamelCaseRegex1 = new(@"(\P{Ll})(\P{Ll}\p{Ll})", RegexOptions.Compiled);
    private static readonly Regex UnCamelCaseRegex2 = new(@"(\p{Ll})(\P{Ll})", RegexOptions.Compiled);

    public DropdownAttribute(string label = null, string description = null)
    {
        Label = label;
        Description = description;
    }

    private static string UnCamelCase(string str) =>
        UnCamelCaseRegex2.Replace(UnCamelCaseRegex1.Replace(str, "$1 $2"), "$1 $2");

    public Control BuildRow(string name, Func<object> getter, Action<object> setter)
    {
        var selected = getter();
        var options = OptionsOf(selected.GetType());

        var comboBox = new ComboBox
        {
            Width = 240,
            Height = SettingsLayout.ControlHeight,
            [TextElement.FontSizeProperty] = 18d,
        };

        foreach (var option in options)
            comboBox.Items.Add(new ComboBoxItem
            {
                Content = new TextBlock { Text = option.Label, FontSize = 18 },
                Tag = option.Value,
            });

        for (var i = 0; i < options.Count; i++)
        {
            if (Equals(options[i].Value, selected))
            {
                comboBox.SelectedIndex = i;
                break;
            }
        }

        Describe(options, selected);

        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedItem is not ComboBoxItem item) return;
            setter(item.Tag);
            Describe(options, item.Tag);
        };

        return RowBuilder.NewRow(Tools.Tools.GetLabelOrDefault(name, Label), Description, comboBox);
    }

    private class EnumOption
    {
        public object Value;
        public string Label;
        public int Order;
        public string Description;
    }

    // Members without an Option keep their declared name and their numeric order.
    private static List<EnumOption> OptionsOf(Type enumType)
    {
        var options = new List<EnumOption>();

        var fallbackOrder = 0;
        foreach (var memberName in Enum.GetNames(enumType))
        {
            var option = enumType.GetField(memberName)?.GetCustomAttribute<OptionAttribute>();
            options.Add(new EnumOption
            {
                Value = Enum.Parse(enumType, memberName),
                Label = option?.Label ?? UnCamelCase(memberName),
                Order = option?.Order ?? fallbackOrder,
                Description = option?.Description,
            });
            fallbackOrder++;
        }

        return options.OrderBy(o => o.Order).ToList();
    }
    
    private static void Describe(List<EnumOption> options, object value)
    {
        var option = options.FirstOrDefault(o => Equals(o.Value, value));
        if (option?.Description == null) return;

        DescriptionBox.Show(option.Label, option.Description);
    }

    public List<Type> SupportedTypes { get; } = new() { typeof(Enum) };
}
