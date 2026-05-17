using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Jpmsantos81.SimpleTcp;

public class ClienteTcp
{ 
    private TcpClient _cliente = new();
    private EventController _events = new();
    public string Ip {  get; private set; } = null!;
    public int Porta { get; private set; }
    public string Delimitador { get;}
    public string Id { get; }
    public string? IdServidor { get; private set; }
    private event Action<string, object>? AoReceberComandoEvent;
    private event Action<string>? AoSeDeslogarEvent;
    private event Action<string>? AoSeLogarEvent;
    public ClienteTcp(string id = "", string delimitador = "<EOF>")
    {
        Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString() : id;
        Delimitador = string.IsNullOrEmpty(delimitador) ? "<EOF>" : delimitador;
    }

    public async Task<bool> ConectarAsync(string ip, int porta)
    {
        try 
        {
            await _cliente.ConnectAsync(ip, porta);
            if (!_cliente.Connected)
            {
                throw new InvalidOperationException("Não foi possível conectar ao servidor.");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Não foi possível conectar ao servidor.", ex);
        }
        _ = LoopEscutarAsync();

        Pacote msg = new()
        {
            Tipo = Pacote.Tipos.ParaServer,
            Subtipo = Pacote.Subtipos.Logar,
            IdAutor = Id
        };

        await EnviarPacoteAsync(msg);
        Ip = ip;
        Porta = porta;
        return true;
    }
    public async Task<bool> EnviarParaServidorAsync(object conteudo)
    {
        var pacote = new Pacote
        {
            Tipo = Pacote.Tipos.ParaServer,
            Subtipo = Pacote.Subtipos.Comando,
            IdAutor = Id,
            Conteudo = conteudo
        };
        return await EnviarPacoteAsync(pacote);
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
    public void AoReceber(Action<string, object> objRecebido) => _events.Vincular(
       () => { AoReceberComandoEvent += objRecebido; },
       () => { AoReceberComandoEvent -= objRecebido; }
    );
    public void AoSeLogar(Action<string> objLogar) => _events.Vincular(
        () => { AoSeLogarEvent += objLogar; },
        () => { AoSeLogarEvent -= objLogar; }
    );
    public void AoSeDeslogar(Action<string> objDeslogar) => _events.Vincular(
        () => { AoSeDeslogarEvent += objDeslogar; },
        () => { AoSeDeslogarEvent -= objDeslogar; }
    );
    public async Task DisposeAsync()
    {
        if (_cliente.Connected)
        {
            Pacote msg = new()
            {
                Tipo = Pacote.Tipos.ParaServer,
                Subtipo = Pacote.Subtipos.Deslogar,
                IdAutor = Id,
                IdDestino = Id
            };
            await EnviarPacoteAsync(msg);
            AoSeDeslogarEvent?.Invoke(IdServidor!);
        }
        _cliente?.Close();
    }
    private async Task LoopEscutarAsync()
    {
        var stream = _cliente.GetStream();
        byte[] buffer = new byte[1024];
        var acumulador = new StringBuilder();

        while (true)
        {
            int bytesLidos = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesLidos == 0)
            {
                _cliente.Close();
                return;
            }
            string textoRecebido = Encoding.UTF8.GetString(buffer, 0, bytesLidos);
            acumulador.Append(textoRecebido);

            string textoAcumulado = acumulador.ToString();
            while (textoAcumulado.Contains(Delimitador))
            {
                int finalDoJson = textoAcumulado.IndexOf(Delimitador);
                string json = textoAcumulado[..finalDoJson];
                await ProcessarPacote(json, _cliente);
                textoAcumulado = textoAcumulado[(finalDoJson + Delimitador.Length)..];
            }
            acumulador.Clear();
            acumulador.Append(textoAcumulado);
        }
    }

    internal async Task<bool> EnviarPacoteAsync(Pacote pacote)
    {
        if (!_cliente.Connected) return false;

        string json = JsonSerializer.Serialize(pacote) + Delimitador;
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        await _cliente.GetStream().WriteAsync(bytes, 0, bytes.Length);
        return true;
    }

    private async Task ProcessarPacote(string json, TcpClient cliente)
    {
        int idx = json.IndexOf(Delimitador);
        string jsonPronto;
        Pacote? Pacote;

        if (idx > 0) jsonPronto = json[..idx];
        else jsonPronto = json;

        try { Pacote = JsonSerializer.Deserialize<Pacote>(jsonPronto); }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"O cliente recebeu um pacote com formato inválido: '{jsonPronto}'. Verifique se o servidor está enviando os dados corretamente e tente novamente.",
                ex
            );
        }

        if (Pacote == null) return;

        if (Pacote.Tipo == Pacote.Tipos.ParaCliente)
        {
            switch (Pacote.Subtipo)
            {
                case Pacote.Subtipos.Logar:
                    IdServidor = Pacote.IdAutor;
                    AoSeLogarEvent?.Invoke(IdServidor);
                    break;

                case Pacote.Subtipos.Deslogar:
                    await DisposeAsync();
                    break;

                case Pacote.Subtipos.Comando:
                    AoReceberComandoEvent?.Invoke(Pacote.IdAutor, Pacote.Conteudo!);
                    break;
            }
        }
    }
}
