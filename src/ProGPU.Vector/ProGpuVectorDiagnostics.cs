using System;

namespace ProGPU.Vector;

internal static class ProGpuVectorDiagnostics
{
    private const string EnvironmentVariable = "PROGPU_VECTOR_DIAGNOSTICS";

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
