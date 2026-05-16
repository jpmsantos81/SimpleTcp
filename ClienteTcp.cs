using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Jpmsantos81.SimpleTcp;

public class ClienteTcp : IDisposable
{ 
    private TcpClient _cliente = new();
    public string Ip {  get; private set; } = null!;
    public int Porta { get; private set; }
    public string Delimitador { get;}
    public string Id { get; }
    public string? IdServidor { get; private set; }
    public Action<object> CallBack { get; }

    public ClienteTcp(Action<object> callBack, string id = "", string delimitador = "<EOF>")
    {
        CallBack = callBack;
        Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString() : id;
        Delimitador = string.IsNullOrEmpty(delimitador) ? "<EOF>" : delimitador;
    }

    public async Task<bool> ConectarAsync(string ip, int porta)
    {
        try 
        {
            await _cliente.ConnectAsync(ip, porta);
            _ = LoopEscutarAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Não foi possível conectar ao servidor.", ex);
        }

        Pacote msg = new()
        {
            Tipo = Pacote.Tipos.ParaServer,
            Subtipo = Pacote.Subtipos.Logar,
            IdAutor = Id
        };

        await EnviarAsync(msg);
        Ip = ip;
        Porta = porta;
        return true;
    }
    public Task<bool> EnviarParaServidorAsync(object conteudo)
    {
        var pacote = new Pacote
        {
            Tipo = Pacote.Tipos.ParaServer,
            Subtipo = Pacote.Subtipos.Comando,
            IdAutor = Id,
            Conteudo = conteudo
        };
        return EnviarAsync(pacote);
    }
    public Task<bool> EnviarParaClienteAsync(string idCliente, object conteudo)
    {
        var pacote = new Pacote
        {
            Tipo = Pacote.Tipos.ParaCliente,
            Subtipo = Pacote.Subtipos.Comando,
            IdAutor = Id,
            IdDestino = idCliente,
            Conteudo = conteudo
        };
        return EnviarAsync(pacote);
    }
    public void Dispose()
    {
        if (_cliente.Connected)
        {
            Pacote msg = new()
            {
                Tipo = Pacote.Tipos.ParaServer,
                Subtipo = Pacote.Subtipos.Deslogar,
                IdAutor = Id
            };
            _ = EnviarAsync(msg);
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
                ProcessarPacote(json, _cliente);
                textoAcumulado = textoAcumulado[(finalDoJson + Delimitador.Length)..];
            }
            acumulador.Clear();
            acumulador.Append(textoAcumulado);
        }
    }

    internal async Task<bool> EnviarAsync(Pacote pacote)
    {
        if (!_cliente.Connected) return false;

        string json = JsonSerializer.Serialize(pacote) + Delimitador;
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        await _cliente.GetStream().WriteAsync(bytes, 0, bytes.Length);
        return true;
    }

    private void ProcessarPacote(string json, TcpClient cliente)
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
                    break;

                case Pacote.Subtipos.Deslogar:
                    Dispose();
                    break;

                case Pacote.Subtipos.Comando:
                    CallBack(Pacote.Conteudo!);
                    break;
            }
        }
    }
}
