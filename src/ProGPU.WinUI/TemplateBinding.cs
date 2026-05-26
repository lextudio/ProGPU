using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Documents;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ProGPU.Layout;

namespace Microsoft.UI.Xaml.Controls;

public class TemplateBinding
{
    private static readonly Dictionary<(Type, string), Func<object, object?>> GetterCache = new();
    private static readonly Dictionary<(Type, string), Action<object, object?>> SetterCache = new();
    private static readonly object CacheLock = new object();

    private readonly WeakReference<FrameworkElement> _targetElementRef;
    private readonly string _targetPropertyName;
    private readonly WeakReference<Control> _sourceControlRef;
    private readonly string _sourcePropertyName;

    private readonly Func<object, object?>? _getter;
    private readonly Action<object, object?>? _setter;

    public TemplateBinding(FrameworkElement targetElement, string targetPropertyName, Control sourceControl, string sourcePropertyName)
    {
        _targetElementRef = new WeakReference<FrameworkElement>(targetElement ?? throw new ArgumentNullException(nameof(targetElement)));
        _targetPropertyName = targetPropertyName ?? throw new ArgumentNullException(nameof(targetPropertyName));
        _sourceControlRef = new WeakReference<Control>(sourceControl ?? throw new ArgumentNullException(nameof(sourceControl)));
        _sourcePropertyName = sourcePropertyName ?? throw new ArgumentNullException(nameof(sourcePropertyName));

        _getter = GetOrCreateGetter(sourceControl.GetType(), sourcePropertyName);
        _setter = GetOrCreateSetter(targetElement.GetType(), targetPropertyName);

        // Apply initial value immediately
        UpdateTargetValue();

        // Listen to property changes on the parent control
        sourceControl.PropertyChanged += OnSourcePropertyChanged;
    }

    public static TemplateBinding Bind(FrameworkElement targetElement, string targetPropertyName, Control sourceControl, string sourcePropertyName)
    {
        return new TemplateBinding(targetElement, targetPropertyName, sourceControl, sourcePropertyName);
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == _sourcePropertyName)
        {
            UpdateTargetValue();
        }
    }

    private void UpdateTargetValue()
    {
        if (!_targetElementRef.TryGetTarget(out var targetElement) || !_sourceControlRef.TryGetTarget(out var sourceControl))
        {
            // Unsubscribe to prevent memory leaks if either reference is dead
            if (senderControl() is Control sc)
            {
                sc.PropertyChanged -= OnSourcePropertyChanged;
            }
            return;
        }

        try
        {
            object? value;
            if (_sourcePropertyName == "Background" && sourceControl is Control ctrlBg)
            {
                value = ctrlBg.GetCurrentBackground();
            }
            else if (_sourcePropertyName == "Foreground" && sourceControl is Control ctrlFg)
            {
                value = ctrlFg.GetCurrentForeground();
            }
            else if (_sourcePropertyName == "BorderBrush" && sourceControl is Control ctrlBb)
            {
                value = ctrlBb.GetCurrentBorderBrush();
            }
            else if (_getter != null)
            {
                value = _getter(sourceControl);
            }
            else
            {
                return;
            }

            if (_setter != null)
            {
                _setter(targetElement, value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TemplateBinding] Error binding {_sourcePropertyName} to {_targetPropertyName} on {targetElement.GetType().Name}: {ex.Message}");
        }
    }

    private Control? senderControl()
    {
        _sourceControlRef.TryGetTarget(out var sc);
        return sc;
    }

    private static Func<object, object?> GetOrCreateGetter(Type type, string propertyName)
    {
        var key = (type, propertyName);
        lock (CacheLock)
        {
            if (GetterCache.TryGetValue(key, out var cached)) return cached;
            var compiled = CompileGetter(type, propertyName);
            GetterCache[key] = compiled;
            return compiled;
        }
    }

    private static Action<object, object?> GetOrCreateSetter(Type type, string propertyName)
    {
        var key = (type, propertyName);
        lock (CacheLock)
        {
            if (SetterCache.TryGetValue(key, out var cached)) return cached;
            var compiled = CompileSetter(type, propertyName);
            SetterCache[key] = compiled;
            return compiled;
        }
    }

    private static Func<object, object?> CompileGetter(Type type, string propertyName)
    {
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop == null)
        {
            prop = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
        }
        if (prop == null) return obj => null;

        var paramObj = System.Linq.Expressions.Expression.Parameter(typeof(object), "obj");
        var castObj = System.Linq.Expressions.Expression.Convert(paramObj, type);
        var propExpr = System.Linq.Expressions.Expression.Property(castObj, prop);
        var castProp = System.Linq.Expressions.Expression.Convert(propExpr, typeof(object));
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<object, object?>>(castProp, paramObj);
        return lambda.Compile();
    }

    private static Action<object, object?> CompileSetter(Type type, string propertyName)
    {
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop == null)
        {
            prop = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
        }
        if (prop == null) return (obj, val) => {};

        var paramObj = System.Linq.Expressions.Expression.Parameter(typeof(object), "obj");
        var paramVal = System.Linq.Expressions.Expression.Parameter(typeof(object), "val");
        var castObj = System.Linq.Expressions.Expression.Convert(paramObj, type);
        
        System.Linq.Expressions.Expression castValExpr;
        if (prop.PropertyType == typeof(object))
        {
            castValExpr = paramVal;
        }
        else
        {
            var convertMethod = typeof(TemplateBinding).GetMethod(nameof(ConvertValue), BindingFlags.NonPublic | BindingFlags.Static);
            var targetTypeExpr = System.Linq.Expressions.Expression.Constant(prop.PropertyType);
            var callExpr = System.Linq.Expressions.Expression.Call(convertMethod!, paramVal, targetTypeExpr);
            castValExpr = System.Linq.Expressions.Expression.Convert(callExpr, prop.PropertyType);
        }

        var propExpr = System.Linq.Expressions.Expression.Property(castObj, prop);
        var assignExpr = System.Linq.Expressions.Expression.Assign(propExpr, castValExpr);
        var lambda = System.Linq.Expressions.Expression.Lambda<Action<object, object?>>(assignExpr, paramObj, paramVal);
        return lambda.Compile();
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null) return null;
        var valType = value.GetType();
        if (targetType.IsAssignableFrom(valType)) return value;

        try
        {
            if (targetType == typeof(float)) return Convert.ToSingle(value);
            if (targetType == typeof(double)) return Convert.ToDouble(value);
            if (targetType == typeof(int)) return Convert.ToInt32(value);
            if (targetType == typeof(Thickness) && value is float fVal) return new Thickness(fVal);
            if (targetType == typeof(Thickness) && value is double dVal) return new Thickness((float)dVal);
            if (targetType == typeof(Thickness) && value is Microsoft.UI.Xaml.Thickness t) return (Thickness)t;
            if (targetType == typeof(Microsoft.UI.Xaml.Thickness) && value is Thickness pt) return (Microsoft.UI.Xaml.Thickness)pt;
            
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            return value;
        }
    }
}
