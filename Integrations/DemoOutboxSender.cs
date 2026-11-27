using VillageCrm.Models;
using VillageCrm.Services;

namespace VillageCrm.Integrations;

/// <summary>
/// Demo only: plays the part of the background worker that empties the outbox. Sends one message at a time
/// with a short pause, so the flow can be watched. A real system would run this on a schedule on a server.
/// </summary>
public class DemoOutboxSender(OutboxService outbox, IntegrationActivity activity)
{
    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(800);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<(int Sent, int Failed)> SendAsync(bool retryFailed)
    {
        await _gate.WaitAsync();
        try
        {
            int sent = 0, failed = 0;
            var waiting = (await outbox.ListAsync())
                .Where(m => m.Status == OutboxStatus.Pending || retryFailed && m.Status == OutboxStatus.Failed)
                .OrderBy(m => m.Id);
            foreach (var message in waiting)
            {
                activity.Raise(message, IntegrationEvent.Sending);
                await Task.Delay(Pause);
                if (await outbox.SendAsync(message))
                {
                    sent++;
                }
                else
                {
                    failed++;
                }
            }
            return (sent, failed);
        }
        finally
        {
            _gate.Release();
        }
    }
}
