using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Jpmsantos81.SimpleTcp;

public class ServidorTcp
{
    private event Action<string>? AoLogaremEvent;
    private event Action<string>? AoDeslogaremEvent;
    private event Action<string, object>? AoReceberComandoEvent;

    private EventController _events = new();

    private TcpListener _listener = null!;
    public ListaClientes Clientes { get; } = new();
    public string Delimitador { get; }
    public string Id { get; }
    public int Porta { get; private set; }

    public ServidorTcp(string id = "", string delimitador = "<EOF>")
    {
        Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString() : id;
        Delimitador = string.IsNullOrEmpty(delimitador) ? "<EOF>" : delimitador;
    }

    public bool Iniciar(int porta)
    {
        if (porta < 0 || porta > 65535 || _listener != null) return false;

        _listener = new TcpListener(IPAddress.IPv6Any, porta);
        _listener.Server.DualMode = true;

        try
        {
            _listener.Start();
            _ = LoopAceitarClientesAsync();
        }
        catch (Exception ex)
        {
            _listener?.Stop();
            _listener?.Dispose();
            _listener = null!;
            throw new InvalidOperationException($"Não foi possível iniciar o servidor na porta {porta}. Verifique se a porta está disponível e tente novamente.", ex);
        }
        Porta = porta;
        return true;
    }
    public async Task<bool> EnviarParaClienteAsync(string idCliente, object conteudo)
    {
        var pacote = new Pacote
        {
            Tipo = Pacote.Tipos.ParaCliente,
            Subtipo = Pacote.Subtipos.Comando,
            IdAutor = Id,
            IdDestino = idCliente,
            Conteudo = conteudo
        };
        return await EnviarPacoteAsync(pacote);
    }

    public async Task EnviarParaTodosAsync(object conteudo)
    {
        var pacote = new Pacote
        {
            Tipo = Pacote.Tipos.ParaCliente,
            Subtipo = Pacote.Subtipos.Comando,
            IdAutor = Id,
            Conteudo = conteudo
        };
        await EnviarPacoteParaTodosAsync(pacote);
    }

    public void AoLogarem(Action<string> idAoLogar) => _events.Vincular(
        () => { AoLogaremEvent += idAoLogar; },
        () => { AoLogaremEvent -= idAoLogar; }
    );
    public void AoDeslogarem(Action<string> idAoDeslogar) => _events.Vincular(
        () => { AoDeslogaremEvent += idAoDeslogar; },
        () => { AoDeslogaremEvent -= idAoDeslogar; }
    );
    public void AoReceber(Action<string, object> objRecebido) => _events.Vincular(
        () => { AoReceberComandoEvent += objRecebido; },
        () => { AoReceberComandoEvent -= objRecebido; }
    );
    public async Task DisposeAsync()
    {
        Pacote pacoteRespostaDeslogar = new()
        {
            Tipo = Pacote.Tipos.ParaCliente,
            Subtipo = Pacote.Subtipos.Deslogar,
            IdAutor = Id
        };
        await EnviarPacoteParaTodosAsync(pacoteRespostaDeslogar);
        Clientes.ClearAndDisposeAll();
        _events.Desvincular();
        _listener.Stop();
        _listener = null!;
    }

    internal async Task<bool> EnviarPacoteAsync(Pacote pacote)
    {
        string json = JsonSerializer.Serialize(pacote) + Delimitador;
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        Clientes.TryGetValue(pacote.IdDestino, out TcpClient? cliente);
        if (cliente == null || !cliente.Connected) return false;

        var stream = cliente.GetStream();
        await stream.WriteAsync(bytes, 0, bytes.Length);
        return true;
    }
    internal async Task EnviarPacoteParaTodosAsync(Pacote pacote)
    {
        string json = JsonSerializer.Serialize(pacote) + Delimitador;
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        foreach (var cliente in Clientes.Values)
        {
            if (!cliente.Connected)
                continue;

            var stream = cliente.GetStream();
            await stream.WriteAsync(bytes, 0, bytes.Length);
        }
    }

    private async Task LoopAceitarClientesAsync()
    {
        if (_listener == null) throw new InvalidOperationException("O servidor não foi iniciado. Chame Iniciar(porta) antes de aceitar clientes.");
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
            try
            {
                int bytesLidos = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesLidos == 0)
                {
                    Clientes.RemoveByClient(cliente);
                    return;
                }
                string textoRecebido = Encoding.UTF8.GetString(buffer, 0, bytesLidos);
                acumulador.Append(textoRecebido);

                string textoAcumulado = acumulador.ToString();
                while (textoAcumulado.Contains(Delimitador))
                {
                    int finalDoJson = textoAcumulado.IndexOf(Delimitador);
                    string json = textoAcumulado[..finalDoJson];
                    await ProcessarPacote(json, cliente);
                    textoAcumulado = textoAcumulado[(finalDoJson + Delimitador.Length)..];
                }
                acumulador.Clear();
                acumulador.Append(textoAcumulado);
            }
            catch
            {
                Clientes.RemoveByClient(cliente);
                return;
            }
        }
    }

    private async Task ProcessarPacote(string json, TcpClient cliente)
    {
        int idx = json.IndexOf(Delimitador);
        string jsonPronto;
        Pacote? pacote;

        if (idx > 0) jsonPronto = json[..idx];
        else jsonPronto = json;

        try { pacote = JsonSerializer.Deserialize<Pacote>(jsonPronto); }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"O servidor recebeu um pacote com formato inválido: '{jsonPronto}'. Verifique se o cliente está enviando os dados corretamente e tente novamente.",
                ex
            );
        }

        if (pacote == null) return;

        if (pacote.Tipo == Pacote.Tipos.ParaServer)
        {
            switch (pacote.Subtipo)
            {
                case (Pacote.Subtipos.Logar):
                    bool sucesso = Clientes.TryAdd(pacote.IdAutor, cliente);
                    if (!sucesso)
                    {
                        throw new InvalidOperationException("erro ao adicionar cliente");
                    }

                    Pacote pacoteResposta = new()
                    {
                        Tipo = Pacote.Tipos.ParaCliente,
                        Subtipo = Pacote.Subtipos.Logar,
                        IdAutor = Id,
                        IdDestino = pacote.IdAutor
                    };
                    await EnviarPacoteAsync(pacoteResposta);
                    AoLogaremEvent?.Invoke(pacote.IdAutor!);
                    break;

                case (Pacote.Subtipos.Deslogar):
                    AoDeslogaremEvent?.Invoke(pacote.IdDestino);

                    Pacote pacoteRespostaDeslogar = new()
                    {
                        Tipo = Pacote.Tipos.ParaCliente,
                        Subtipo = Pacote.Subtipos.Deslogar,
                        IdAutor = Id,
                        IdDestino = pacote.IdAutor
                    };
                    await EnviarPacoteAsync(pacoteRespostaDeslogar);

                    Clientes.TryRemove(pacote.IdAutor, out var _);
                    cliente?.Close();
                    break;

                case (Pacote.Subtipos.Comando):
                    AoReceberComandoEvent?.Invoke(pacote.IdAutor, pacote.Conteudo!);
                    break;
            }
        }
        else if (pacote.Tipo == Pacote.Tipos.ParaCliente)
        {
            switch (pacote.Subtipo)
            {
                case Pacote.Subtipos.Comando:
                    Pacote pacoteResposta = new()
                    {
                        Tipo = Pacote.Tipos.ParaCliente,
                        Subtipo = Pacote.Subtipos.Comando,
                        IdAutor = pacote.IdAutor,
                        IdDestino = pacote.IdDestino,
                        Conteudo = pacote.Conteudo
                    };
                    await EnviarPacoteAsync(pacoteResposta);
                    break;
            }
        }
    }
}