using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SimpleTcp;

internal class ServidorTcp<T> : IDisposable
{
    private TcpManager<T> _tcpManager;
    private string _delimitador;
    private TcpListener _listener = null!;
    public string Id { get; set; }

    public ConcurrentDictionary<string, TcpClient> Clientes = new();
    private Action<T> _processarPacoteCallback;

    public ServidorTcp(TcpManager<T> tcpManager)
    {
        _tcpManager = tcpManager;

        Id = _tcpManager.Id;
        _delimitador = _tcpManager.Delimitador;
        _processarPacoteCallback = _tcpManager.CallBack;
    }

    public bool Iniciar(int porta)
    {
        _listener = new TcpListener(IPAddress.IPv6Any, porta);
        _listener.Server.DualMode = true;

        try
        {
            _listener.Start();
        }
        catch 
        {
            return false; 
        }
        _ = LoopAceitarClientesAsync();
        return true;
    }
    public async Task EnviarPacote(Pacote<T> pacote)
    {
        string json = JsonSerializer.Serialize(pacote) + _delimitador;
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        var stream = Clientes[pacote.IdDestino].GetStream();
        await stream.WriteAsync(bytes, 0, bytes.Length);
    }
    public async Task EnviarPacoteParaTodos(Pacote<T> pacote)
    {
        string json = JsonSerializer.Serialize(pacote) + _delimitador;
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        foreach (var cliente in Clientes.Values)
        {
            var stream = cliente.GetStream();
            await stream.WriteAsync(bytes, 0, bytes.Length);
        }
    }

    private async Task LoopAceitarClientesAsync()
    {
        while (true)
        {
            TcpClient cliente = await _listener.AcceptTcpClientAsync();
            _ = ProcessarClientesAsync(cliente);
        }
    }

    private async Task ProcessarClientesAsync(TcpClient cliente)
    {
        NetworkStream stream = cliente.GetStream();
        byte[] buffer = new byte[1024];
        var acumulador = new StringBuilder();

        while (true)
        {
            int bytesLidos = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesLidos == 0)
            {
                Clientes.TryRemove(Clientes.FirstOrDefault(c => c.Value == cliente).Key, out var _);
                cliente.Close();
                return;
            }
            string textoRecebido = Encoding.UTF8.GetString(buffer, 0, bytesLidos);
            acumulador.Append(textoRecebido);

            string textoAcumulado = acumulador.ToString();
            while (textoAcumulado.Contains(_delimitador))
            {
                int finalDoJson = textoAcumulado.IndexOf(_delimitador);
                string json = textoAcumulado[..finalDoJson];
                ProcessarPacote(json, cliente);
                textoAcumulado = textoAcumulado[(finalDoJson + _delimitador.Length)..];
            }
            acumulador.Clear();
            acumulador.Append(textoAcumulado);
        }
    }

    private void ProcessarPacote(string json, TcpClient cliente)
    {
        int idx = json.IndexOf(_delimitador);
        string jsonPronto;
        Pacote<T>? pacote;

        if (idx > 0) jsonPronto = json[..idx];
        else jsonPronto = json;

        try { pacote = JsonSerializer.Deserialize<Pacote<T>>(jsonPronto); }
        catch { return; }

        if (pacote == null) return;

        if (pacote.Tipo == Pacote<T>.Tipos.ParaServer)
        {
            switch (pacote.Subtipo)
            {
                case (Pacote<T>.Subtipos.Logar):
                    Clientes.TryAdd(pacote.IdAutor, cliente);

                    Pacote<T> pacoteResposta = new()
                    {
                        Tipo = Pacote<T>.Tipos.ParaServer,
                        Subtipo = Pacote<T>.Subtipos.Logar,
                        IdAutor = Id,
                        IdDestino = pacote.IdAutor
                    };
                    _ = EnviarPacote(pacoteResposta);
                    break;

                case (Pacote<T>.Subtipos.Deslogar):
                    Pacote<T> pacoteRespostaDeslogar = new()
                    {
                        Tipo = Pacote<T>.Tipos.ParaCliente,
                        Subtipo = Pacote<T>.Subtipos.Deslogar,
                        IdAutor = Id,
                        IdDestino = pacote.IdAutor
                    };
                    _ = EnviarPacote(pacoteRespostaDeslogar);
                    Clientes.TryRemove(pacote.IdAutor, out var _);
                    cliente.Close();
                    break;
            }
        }
        else if(pacote.Tipo == Pacote<T>.Tipos.ParaCliente)
        {
            switch (pacote.Subtipo)
            {

                case Pacote<T>.Subtipos.Comando:
                    Pacote<T> pacoteResposta = new()
                    {
                        Tipo = Pacote<T>.Tipos.ParaCliente,
                        Subtipo = Pacote<T>.Subtipos.Comando,
                        IdAutor = pacote.IdAutor,
                        IdDestino = pacote.IdDestino,
                        Conteudo = pacote.Conteudo
                    };
                    _ = EnviarPacote(pacoteResposta);
                    break;
            }
         }
        else
        {
            _processarPacoteCallback(pacote.Conteudo!);
        }
    }
    public void Dispose()
    {
        Pacote<T> pacoteRespostaDeslogar = new()
        {
            Tipo = Pacote<T>.Tipos.ParaCliente,
            Subtipo = Pacote<T>.Subtipos.Deslogar,
            IdAutor = Id
        };
        _ = EnviarPacoteParaTodos(pacoteRespostaDeslogar);
        _listener.Stop();
    }
}
