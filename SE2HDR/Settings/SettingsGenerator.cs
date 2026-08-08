using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Avalonia.Controls;
using SE2HDR.Settings.Elements;

namespace SE2HDR.Settings;

internal class AttributeInfo
{
    public IElement ElementType;
    public string Name;
    public Func<object> Getter;
    public Action<object> Setter;
    public string EnabledBy;
}

internal class SettingsGenerator
{
    public string Title => Config.Current.Title;

    private readonly List<AttributeInfo> attributes;

    public SettingsGenerator()
    {
        attributes = ExtractAttributes();
    }

    public void PopulateContent(StackPanel host)
    {
        host.Children.Clear();
        foreach (var info in attributes)
        {
            var row = info.ElementType.BuildRow(info.Name, info.Getter, info.Setter);
            BindEnabled(row, info.EnabledBy);
            host.Children.Add(row);
        }
    }
    
    private static void BindEnabled(Control row, string propertyName)
    {
        if (propertyName == null)
            return;

        var property = typeof(Config).GetProperty(propertyName)
                       ?? throw new Exception($"EnabledBy target {propertyName} not found on Config");

        void Update() => row.IsEnabled = property.GetValue(Config.Current) as bool? ?? true;

        void OnPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == propertyName)
                Update();
        }

        Update();
        Config.Current.PropertyChanged += OnPropertyChanged;
        
        row.DetachedFromVisualTree += (_, _) => Config.Current.PropertyChanged -= OnPropertyChanged;
    }

    private static bool ValidateType(Type type, List<Type> typesList) =>
        typesList.Any(t => t.IsAssignableFrom(type));

    private static Delegate GetDelegate(MethodInfo methodInfo)
    {
        var methodArgs = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
        var delegateType = Expression.GetDelegateType(methodArgs.Concat(new[] { methodInfo.ReturnType }).ToArray());
        return Delegate.CreateDelegate(delegateType, Config.Current, methodInfo);
    }

    private static List<AttributeInfo> ExtractAttributes()
    {
        var config = new List<AttributeInfo>();

        foreach (var propertyInfo in typeof(Config).GetProperties())
        {
            if (!SE2HDR.Tools.RenderMode.Hdr && propertyInfo.IsDefined(typeof(HdrOnlyAttribute), false))
                continue;

            var name = propertyInfo.Name;
            foreach (var attribute in propertyInfo.GetCustomAttributes())
            {
                if (attribute is not IElement element) continue;

                if (!ValidateType(propertyInfo.PropertyType, element.SupportedTypes))
                {
                    throw new Exception(
                        $"Element {element.GetType().Name} for {name} expects "
                        + $"{string.Join("/", element.SupportedTypes)} but "
                        + $"received {propertyInfo.PropertyType.FullName}");
                }

                var info = new AttributeInfo
                {
                    ElementType = element,
                    Name = name,
                    Getter = () => propertyInfo.GetValue(Config.Current),
                    Setter = value => propertyInfo.SetValue(Config.Current, value),
                    EnabledBy = propertyInfo.GetCustomAttribute<EnabledByAttribute>()?.PropertyName,
                };
                config.Add(info);
            }
        }

        foreach (var methodInfo in typeof(Config).GetMethods())
        {
            var name = methodInfo.Name;

            foreach (var attribute in methodInfo.GetCustomAttributes())
            {
                if (attribute is not IElement element) continue;

                if (!ValidateType(typeof(Delegate), element.SupportedTypes))
                {
                    throw new Exception(
                        $"Element {element.GetType().Name} for {name} expects "
                        + $"{string.Join("/", element.SupportedTypes)} but "
                        + $"received {typeof(Delegate).FullName}");
                }

                var method = GetDelegate(methodInfo);
                var info = new AttributeInfo
                {
                    ElementType = element,
                    Name = name,
                    Getter = () => method,
                    Setter = null,
                };
                config.Add(info);
            }
        }

        return config;
    }
}
