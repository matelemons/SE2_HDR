using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SE2HDR.Settings.Elements;

[AttributeUsage(AttributeTargets.Property)]
internal class InfoAttribute : Attribute, IElement
{
    private static readonly IBrush Foreground =
        new SolidColorBrush(Avalonia.Media.Color.FromRgb(0xB8, 0xC8, 0xD4));

    public Control BuildRow(string name, Func<object> getter, Action<object> setter) =>
        new TextBlock
        {
            Text = getter()?.ToString() ?? string.Empty,
            Foreground = Foreground,
            FontSize = 16,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 8),
        };

    public List<Type> SupportedTypes { get; } = new() { typeof(string) };
}
