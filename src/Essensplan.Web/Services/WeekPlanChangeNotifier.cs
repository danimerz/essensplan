namespace Essensplan.Web.Services;

/// <summary>
/// Singleton that notifies all active Blazor circuits in the same household
/// when the week plan changes, enabling live-sync without polling.
/// </summary>
public class WeekPlanChangeNotifier
{
    private readonly Dictionary<int, List<Func<Task>>> _subscribers = new();
    private readonly Lock _lock = new();

    public void Subscribe(int householdId, Func<Task> callback)
    {
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(householdId, out var list))
            {
                list = new List<Func<Task>>();
                _subscribers[householdId] = list;
            }
            list.Add(callback);
        }
    }

    public void Unsubscribe(int householdId, Func<Task> callback)
    {
        lock (_lock)
        {
            if (_subscribers.TryGetValue(householdId, out var list))
                list.Remove(callback);
        }
    }

    public async Task NotifyAsync(int householdId)
    {
        List<Func<Task>> callbacks;
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(householdId, out var list)) return;
            callbacks = list.ToList();
        }

        foreach (var cb in callbacks)
        {
            try { await cb(); }
            catch { /* circuit may have disconnected */ }
        }
    }
}
