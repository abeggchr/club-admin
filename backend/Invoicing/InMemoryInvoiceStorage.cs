using System.Collections.Concurrent;

namespace backend.Invoicing;

/// <summary>In-memory <see cref="IInvoiceStorage"/> for tests.</summary>
public class InMemoryInvoiceStorage : IInvoiceStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new();

    public Task PutAsync(int number, byte[] pdf, CancellationToken ct = default)
    {
        _objects[InvoiceStorageKey.For(number)] = pdf;
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(int number, CancellationToken ct = default) =>
        Task.FromResult(_objects.TryGetValue(InvoiceStorageKey.For(number), out var pdf) ? pdf : null);
}
