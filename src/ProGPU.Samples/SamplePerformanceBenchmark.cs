using System;
using System.Diagnostics;

namespace ProGPU.Samples;

internal static class SamplePerformanceBenchmark
{
    private const string PageVariable = "PROGPU_SAMPLE_BENCHMARK_PAGE";
    private const string WarmupFramesVariable = "PROGPU_SAMPLE_BENCHMARK_WARMUP_FRAMES";
    private const string MeasureFramesVariable = "PROGPU_SAMPLE_BENCHMARK_MEASURE_FRAMES";
    private const string VSyncVariable = "PROGPU_SAMPLE_BENCHMARK_VSYNC";

    private static readonly int s_warmupFrames = ReadPositiveInt(WarmupFramesVariable, 180);
    private static readonly int s_measureFrames = ReadPositiveInt(MeasureFramesVariable, 600);
    private static readonly Stopwatch s_wallClock = new();
    private static int s_frame;
    private static double s_deltaSeconds;
    private static double s_compileMilliseconds;
    private static double s_uploadMilliseconds;
    private static double s_renderMilliseconds;
    private static double s_compositorMilliseconds;
    private static double s_hostUpdateMilliseconds;
    private static double s_layoutMilliseconds;
    private static double s_animationMilliseconds;
    private static double s_surfaceAcquireMilliseconds;
    private static double s_presentMilliseconds;
    private static Microsoft.UI.Xaml.Window? s_window;
    private static long s_allocatedBytesAtStart;
    private static int s_lolsRenderedAtStart;
    private static int s_sceneCacheHitFrames;
    private static bool s_workloadStarted;
    private static bool s_finished;

    public static string? RequestedPage { get; } = ReadRequestedPage();

    public static void AttachWindow(Microsoft.UI.Xaml.Window window)
    {
        s_window = window;
    }

    public static void StartRequestedWorkload(string selectedPage)
    {
        if (RequestedPage is null)
        {
            return;
        }

        if (ReadOptionalBool(VSyncVariable) is { } vsync && AppState._wgpuContext is { } context)
        {
            context.VSync = vsync;
            if (context.Window != null)
            {
                context.Window.VSync = vsync;
            }
        }

        Console.WriteLine(
            $"[SampleBenchmark] page={selectedPage} warmupFrames={s_warmupFrames}" +
            $" measureFrames={s_measureFrames} vsync={AppState._wgpuContext?.VSync}");
    }

    public static void ObserveFrame(double deltaSeconds)
    {
        if (RequestedPage is null || s_finished)
        {
            return;
        }

        if (!s_workloadStarted)
        {
            if (string.Equals(RequestedPage, "LOL/s Benchmark", StringComparison.OrdinalIgnoreCase))
            {
                if (!LolsPage.IsReady)
                {
                    return;
                }

                LolsPage.Start();
            }

            s_workloadStarted = true;
        }

        s_frame++;
        if (s_frame <= s_warmupFrames)
        {
            return;
        }

        if (s_frame == s_warmupFrames + 1)
        {
            s_wallClock.Restart();
            s_allocatedBytesAtStart = GC.GetTotalAllocatedBytes(precise: false);
            s_lolsRenderedAtStart = LolsPage.TotalRenderedCount;
        }

        s_deltaSeconds += deltaSeconds;
        if (s_window is { } window)
        {
            var frameMetrics = window.FrameMetrics;
            s_layoutMilliseconds += frameMetrics.LayoutTimeMs;
            s_animationMilliseconds += frameMetrics.AnimationTimeMs;
            s_surfaceAcquireMilliseconds += frameMetrics.SurfaceAcquireTimeMs;
            s_presentMilliseconds += frameMetrics.PresentTimeMs;
        }
        if (AppState._screenCompositor is { } compositor)
        {
            var metrics = compositor.Metrics;
            s_compileMilliseconds += metrics.VisualTreeCompileTimeMs;
            s_uploadMilliseconds += metrics.GpuUploadTimeMs;
            s_renderMilliseconds += metrics.RenderPassTimeMs;
            s_compositorMilliseconds += metrics.FrameTimeMs;
            if (metrics.SceneCacheHit) s_sceneCacheHitFrames++;
        }

        int measuredFrames = s_frame - s_warmupFrames;
        if (measuredFrames < s_measureFrames)
        {
            return;
        }

        s_wallClock.Stop();
        s_finished = true;
        LolsPage.Stop();

        double deltaFps = s_deltaSeconds > 0d ? measuredFrames / s_deltaSeconds : 0d;
        double wallFps = s_wallClock.Elapsed.TotalSeconds > 0d
            ? measuredFrames / s_wallClock.Elapsed.TotalSeconds
            : 0d;
        double divisor = Math.Max(1, measuredFrames);
        long allocatedBytes = Math.Max(0, GC.GetTotalAllocatedBytes(precise: false) - s_allocatedBytesAtStart);
        var finalMetrics = AppState._screenCompositor?.Metrics;
        string workloadDetails = string.Empty;
        if (string.Equals(RequestedPage, "LOL/s Benchmark", StringComparison.OrdinalIgnoreCase))
        {
            int renderedLols = Math.Max(0, LolsPage.TotalRenderedCount - s_lolsRenderedAtStart);
            double lolsPerSecond = s_wallClock.Elapsed.TotalSeconds > 0d
                ? renderedLols / s_wallClock.Elapsed.TotalSeconds
                : 0d;
            workloadDetails =
                $" lolsPerSecond={lolsPerSecond:F0}" +
                $" renderedLols={renderedLols}" +
                $" activeElements={LolsPage.ActiveElementCount}/{LolsPage.MaximumElementCount}" +
                $" pendingActions={UIThread.PendingCount}";
        }

        Console.WriteLine(
            $"[SampleBenchmark] RESULT page=\"{RequestedPage}\" frames={measuredFrames}" +
            $" deltaFps={deltaFps:F2} wallFps={wallFps:F2}" +
            $" compileMs={s_compileMilliseconds / divisor:F4}" +
            $" uploadMs={s_uploadMilliseconds / divisor:F4}" +
            $" renderMs={s_renderMilliseconds / divisor:F4}" +
            $" compositorMs={s_compositorMilliseconds / divisor:F4}" +
            $" hostUpdateMs={s_hostUpdateMilliseconds / divisor:F4}" +
            $" layoutMs={s_layoutMilliseconds / divisor:F4}" +
            $" animationMs={s_animationMilliseconds / divisor:F4}" +
            $" acquireMs={s_surfaceAcquireMilliseconds / divisor:F4}" +
            $" presentMs={s_presentMilliseconds / divisor:F4}" +
            $" allocatedBytesPerFrame={allocatedBytes / divisor:F0}" +
            $" sceneCacheHits={s_sceneCacheHitFrames}/{measuredFrames}" +
            $" sceneCacheMiss=\"{finalMetrics?.SceneCacheMissReason ?? "none"}\"" +
            workloadDetails +
            $" draws={finalMetrics?.DrawCallsCount ?? 0}" +
            $" vectorVertices={finalMetrics?.VectorVerticesCount ?? 0}" +
            $" textVertices={finalMetrics?.TextVerticesCount ?? 0}");

        AppState._window?.Close();
    }

    public static void RecordHostUpdate(TimeSpan elapsed)
    {
        if (RequestedPage is not null && !s_finished && s_frame > s_warmupFrames)
        {
            s_hostUpdateMilliseconds += elapsed.TotalMilliseconds;
        }
    }

    private static string? ReadRequestedPage()
    {
        string? value = Environment.GetEnvironmentVariable(PageVariable);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int ReadPositiveInt(string name, int fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out int parsed) && parsed > 0 ? parsed : fallback;
    }

    private static bool? ReadOptionalBool(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return bool.TryParse(value, out bool parsed) ? parsed : null;
    }
}
