using Avalonia.Controls;

namespace SE2HDR.Settings;

internal static class DescriptionBox
{
    private static TextBlock titleTarget;
    private static TextBlock bodyTarget;

    public static void Attach(TextBlock title, TextBlock body)
    {
        titleTarget = title;
        bodyTarget = body;
        Show(null, null);
    }

    public static void Show(string title, string body)
    {
        if (titleTarget != null) titleTarget.Text = title ?? "";
        if (bodyTarget != null) bodyTarget.Text = body ?? "";
    }
}
