using System.Threading.Tasks;
using Robust.Shared.Asynchronous;

namespace Content.Server.Corvax.Discord;

public static class AHelpTaskManagerExtensions
{
    public static Task RunOnMainThreadAsync(this ITaskManager taskManager, Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        taskManager.RunOnMainThread(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        });

        return tcs.Task;
    }

    public static Task<T> RunOnMainThreadAsync<T>(this ITaskManager taskManager, Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        taskManager.RunOnMainThread(() =>
        {
            try
            {
                tcs.TrySetResult(func());
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        });

        return tcs.Task;
    }
}
