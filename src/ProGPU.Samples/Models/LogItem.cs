using System;
using Microsoft.UI.Xaml.Controls;

namespace ProGPU.Samples;

public class LogItem : IDataGridValueProvider
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Latency { get; set; }

    public bool TryGetDataGridValue(string propertyName, out object? value)
    {
        switch (propertyName)
        {
            case nameof(Id):
                value = Id;
                return true;
            case nameof(Name):
                value = Name;
                return true;
            case nameof(Status):
                value = Status;
                return true;
            case nameof(Latency):
                value = Latency;
                return true;
            default:
                value = null;
                return false;
        }
    }

    public bool TrySetDataGridValue(string propertyName, object? value)
    {
        switch (propertyName)
        {
            case nameof(Id) when value is int id:
                Id = id;
                return true;
            case nameof(Name) when value is string name:
                Name = name;
                return true;
            case nameof(Status) when value is string status:
                Status = status;
                return true;
            case nameof(Latency) when value is double latency:
                Latency = latency;
                return true;
            default:
                return false;
        }
    }

    public Type? GetDataGridValueType(string propertyName)
    {
        return propertyName switch
        {
            nameof(Id) => typeof(int),
            nameof(Name) => typeof(string),
            nameof(Status) => typeof(string),
            nameof(Latency) => typeof(double),
            _ => null
        };
    }
}
