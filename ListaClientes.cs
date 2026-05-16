using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Net.Sockets;

namespace Jpmsantos81.SimpleTcp;

public class ListaClientes : IDisposable
{
    internal ListaClientes() { }
    private readonly ConcurrentDictionary<string, TcpClient> _clientes = new();
    public IReadOnlyDictionary<string, TcpClient> AsReadOnly => new ReadOnlyDictionary<string, TcpClient>(_clientes);

    public bool TryAdd(string id, TcpClient client) => _clientes.TryAdd(id, client);

    public bool TryRemove(string id, out TcpClient? client) => _clientes.TryRemove(id, out client);

    public bool RemoveByClient(TcpClient cliente)
    {
        if (cliente == null) return false;
        foreach (var kvp in _clientes)
        {
            if (kvp.Value == cliente)
                return _clientes.TryRemove(kvp.Key, out _);
        }
        return false;
    }

    public bool TryGetValue(string id, out TcpClient? client) => _clientes.TryGetValue(id, out client);

    public int Count => _clientes.Count;
    public IEnumerable<string> Keys => _clientes.Keys;
    public IEnumerable<TcpClient> Values => _clientes.Values;

    public void ClearAndDisposeAll()
    {
        foreach (var c in _clientes.Values)
        {
            try { c?.Close(); c?.Dispose(); } catch { }
        }
        _clientes.Clear();
    }

    public void Dispose() => ClearAndDisposeAll();
}
