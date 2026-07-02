using System;

namespace ProGPU.Backend;

internal static class ProGpuBackendDiagnostics
{
    private const string EnvironmentVariable = "PROGPU_BACKEND_DIAGNOSTICS";

    public static bool IsEnabled
    {
        get
        {
            string? value = Environment.GetEnvironmentVariable(EnvironmentVariable);
            return string.Equals(value, "1", StringComparison.Ordinal) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void WriteLine(string message)
    {
        if (IsEnabled)
        {
            Console.WriteLine(message);
        }
    }
}
