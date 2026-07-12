using System;
using System.Collections.Generic;

namespace ProGPU.Samples;

public static class UIThread
{
    private static readonly object s_gate = new();
    private static Queue<Action> s_queue = new();
    private static Queue<Action> s_processingQueue = new();
    private static bool s_isProcessing;

    public static void Post(Action action)
    {
        lock (s_gate)
        {
            s_queue.Enqueue(action);
        }
    }

    public static int PendingCount
    {
        get
        {
            lock (s_gate)
            {
                return s_queue.Count;
            }
        }
    }

    public static void RunPending()
    {
        lock (s_gate)
        {
            if (s_isProcessing || s_queue.Count == 0)
            {
                return;
            }

            (s_queue, s_processingQueue) = (s_processingQueue, s_queue);
            s_isProcessing = true;
        }

        try
        {
            while (s_processingQueue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error running posted UI action: {ex.Message}");
                }
            }
        }
        finally
        {
            lock (s_gate)
            {
                s_isProcessing = false;
            }
        }
    }
}
