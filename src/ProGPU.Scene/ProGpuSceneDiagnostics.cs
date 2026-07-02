using System;

namespace ProGPU.Scene;

internal static class ProGpuSceneDiagnostics
{
    private const string EnvironmentVariable = "PROGPU_SCENE_DIAGNOSTICS";

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
