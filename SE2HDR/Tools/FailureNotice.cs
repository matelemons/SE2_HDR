using System;
using HarmonyLib;
using Keen.Game2.Client.UI.Library.Dialogs.OneOptionDialog;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Localization;

namespace SE2HDR.Tools;

// Plugins are constructed before the engine or any UI exists, so a startup failure cannot
// be reported to the player at the moment it happens. The message is queued here.
internal static class FailureNotice
{
    private const string MenuTypeName = "Keen.Game2.Client.UI.Menu.GameMenu";

    private static string pendingMessage;
    private static Harmony harmony;

    public static void Queue(string message)
    {
        pendingMessage = message;

        if (harmony != null)
            return;

        try
        {
            var menuType = typeof(OneOptionDialogDefinition).Assembly.GetType(MenuTypeName)
                           ?? throw new InvalidOperationException($"{MenuTypeName} not found");
            var target = AccessTools.Method(menuType, "UpdateButtons")
                         ?? throw new InvalidOperationException($"{MenuTypeName}.UpdateButtons not found");

            harmony = new Harmony(Plugin.Name + ".Notice");
            harmony.Patch(target, postfix: new HarmonyMethod(AccessTools.Method(typeof(FailureNotice), nameof(OnMenuBuilt))));
        }
        catch (Exception ex)
        {
            harmony = null;
            Log.Default.WriteLine(LogSeverity.Error, $"[{Plugin.Name}] Could not install the failure notice: {ex}");
        }
    }

    private static void OnMenuBuilt()
    {
        var message = pendingMessage;
        if (message == null)
            return;

        try
        {
            var sharedUi = GameAccess.GetSharedUI();
            if (sharedUi == null)
                return;

            var definition = new OneOptionDialogDefinition();
            SetLocKey(definition, "Title", $"{Plugin.Name} patch failed");
            SetLocKey(definition, "Content", message);
            SetLocKey(definition, "ConfirmOption", "Ok");

            sharedUi.ShowDialog(new OneOptionDialogViewModel(definition));
            pendingMessage = null;
        }
        catch (Exception ex)
        {
            pendingMessage = null;
            Log.Default.WriteLine(LogSeverity.Error, $"[{Plugin.Name}] Could not show the failure notice: {ex}");
        }
    }
    
    private static void SetLocKey(object definition, string propertyName, string text)
    {
        var value = LocKey.FromString(text);

        for (var type = definition.GetType(); type != null; type = type.BaseType)
        {
            var setter = AccessTools.DeclaredPropertySetter(type, propertyName);
            if (setter != null)
            {
                setter.Invoke(definition, new object[] { value });
                return;
            }

            var field = AccessTools.DeclaredField(type, $"<{propertyName}>k__BackingField");
            if (field != null)
            {
                field.SetValue(definition, value);
                return;
            }
        }

        throw new InvalidOperationException($"{definition.GetType().Name}.{propertyName} is not writable");
    }
}
