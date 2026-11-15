using System.Collections.Concurrent;

namespace backend.Accounting;

/// <summary>In-memory <see cref="IReceiptStorage"/> for tests.</summary>
public class InMemoryReceiptStorage : IReceiptStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new();

    /// <summary>The keys of the stored objects, for tests to check what exists.</summary>
    public IReadOnlyCollection<string> Keys => _objects.Keys.ToList();

    public Task PutAsync(int id, byte[] pdf, CancellationToken ct = default)
    {
        _objects[ReceiptStorageKey.For(id)] = pdf;
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_objects.TryGetValue(ReceiptStorageKey.For(id), out var pdf) ? pdf : null);

    public Task DeleteAsync(int id, CancellationToken ct = default)
    {
        _objects.TryRemove(ReceiptStorageKey.For(id), out _);
        return Task.CompletedTask;
    }
}
