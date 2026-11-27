using System.Text.Json;
using VillageCrm.Data;
using VillageCrm.Integrations;
using VillageCrm.Models;

namespace VillageCrm.Services;

public class AuditService(IRepository<AuditEntry> audit, TimeProvider time)
{
    public Task WriteAsync(string actor, string action, string entity, int entityId, string detail = "") =>
        audit.AddAsync(new AuditEntry
        {
            When = time.GetLocalNow().DateTime,
            Actor = actor,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Detail = detail
        });

    public async Task<List<AuditEntry>> RecentAsync(int take = 300) =>
        (await audit.ListAsync()).OrderByDescending(a => a.When).ThenByDescending(a => a.Id).Take(take).ToList();
}

/// <summary>Queue for Pastel and Pilot. Messages are sent by <see cref="ProcessAsync"/> and retried on failure.</summary>
public class OutboxService(IRepository<OutboxMessage> outbox, IPastelGateway pastel, IPilotGateway pilot, TimeProvider time, IntegrationActivity activity)
{
    /// <summary>Returns false if a message with this reference was already queued (idempotent).</summary>
    public async Task<bool> EnqueueAsync(OutboxTarget target, string type, string reference, object payload)
    {
        var existing = await outbox.ListAsync(m => m.Target == target && m.Reference == reference);
        if (existing.Count > 0)
        {
            return false;
        }

        var message = await outbox.AddAsync(new OutboxMessage
        {
            Target = target,
            Type = type,
            Reference = reference,
            Payload = JsonSerializer.Serialize(payload),
            Status = OutboxStatus.Pending,
            CreatedAt = time.GetLocalNow().DateTime
        });
        activity.Raise(message, IntegrationEvent.Queued);
        return true;
    }

    public async Task<(int Sent, int Failed)> ProcessAsync()
    {
        int sent = 0, failed = 0;
        foreach (var message in await outbox.ListAsync(m => m.Status != OutboxStatus.Sent))
        {
            if (await SendAsync(message))
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

    /// <summary>Sends one message. Returns false (and marks it Failed, to retry later) if the system is unavailable.</summary>
    public async Task<bool> SendAsync(OutboxMessage message)
    {
        message.Attempts++;
        try
        {
            if (message.Target == OutboxTarget.Pastel)
            {
                await pastel.SendAsync(message);
            }
            else
            {
                await pilot.SendAsync(message);
            }
            message.Status = OutboxStatus.Sent;
            message.SentAt = time.GetLocalNow().DateTime;
            message.LastError = null;
        }
        catch (IntegrationUnavailableException ex)
        {
            message.Status = OutboxStatus.Failed;
            message.LastError = ex.Message;
        }
        await outbox.UpdateAsync(message);
        activity.Raise(message, message.Status == OutboxStatus.Sent ? IntegrationEvent.Sent : IntegrationEvent.Failed);
        return message.Status == OutboxStatus.Sent;
    }

    public async Task<List<OutboxMessage>> ListAsync(OutboxStatus? status = null) =>
        (await outbox.ListAsync(m => status == null || m.Status == status))
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).ToList();
}
