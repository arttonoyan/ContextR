# Sample: BackgroundJobs

## Scenario

A web request enqueues work that executes later in a background worker.  
You need tenant/correlation/user context during job execution without relying on web pipeline objects.

## Approach

- capture `IContextSnapshot` at enqueue time
- store minimal snapshot data with job payload
- activate snapshot with `BeginScope()` in worker

## Enqueue side

```csharp
var snapshot = accessor.CreateSnapshot();
await queue.EnqueueAsync(new ProcessInvoiceCommand(invoiceId, snapshot));
```

## Worker side

```csharp
public async Task Handle(ProcessInvoiceCommand command, CancellationToken ct)
{
    using (command.Snapshot.BeginScope())
    {
        var tenant = snapshot.GetContext<UserContext>()?.TenantId;
        await processor.ProcessAsync(command.InvoiceId, tenant, ct);
    }
}
```

## Why this is better than ambient-only approach

- no dependency on request-scoped web abstractions
- deterministic context lifetime for each job
- safe parallel worker execution

## Suggested tests

- enqueued context is available in worker
- parallel jobs keep tenant boundaries isolated
- worker with empty snapshot behaves safely
