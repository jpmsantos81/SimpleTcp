using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SimpleTcp;

public class ClienteTcp<T> : IDisposable
{
    private TcpManager<T> _tcpManager = null!;

    private TcpClient _cliente = new();
    private string _delimitador;
    public string Id { get; set; }
    private string? _idServidor;

    public ClienteTcp(TcpManager<T> tcpManager)
    {
        _tcpManager = tcpManager;

        Id = _tcpManager.Id;
        _delimitador = _tcpManager.Delimitador;
    }

    public async Task<bool> ConectarAsync(string ip, int porta)
    {
        try
        {
            await _cliente.ConnectAsync(ip, porta);
            _ = LoopEscutarAsync();

            var msg = new Pacote<T>
            {
                Tipo = Pacote<T>.Tipos.ParaServer,
                Subtipo = Pacote<T>.Subtipos.Logar,
                IdAutor = Id
            };
            await EnviarAsync(msg);
            return true;
        }
        catch
        {
            return false;
        }
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
            while (textoAcumulado.Contains(_delimitador))
            {
                int finalDoJson = textoAcumulado.IndexOf(_delimitador);
                string json = textoAcumulado[..finalDoJson];
                ProcessarPacote(json, _cliente);
                textoAcumulado = textoAcumulado[(finalDoJson + _delimitador.Length)..];
            }
            acumulador.Clear();
            acumulador.Append(textoAcumulado);
        }
    }

    private async Task EnviarAsync(Pacote<T> pacote)
    {
        if (!_cliente.Connected) return;

        string json = JsonSerializer.Serialize(pacote) + _delimitador;
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        try
        {
            await _cliente.GetStream().WriteAsync(bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao enviar Pacote:\n{ex.Message}");
        }
    }

    private void ProcessarPacote(string json, TcpClient cliente)
    {
        int idx = json.IndexOf(_delimitador);
        string jsonPronto;
        Pacote<T>? Pacote;

        if (idx > 0) jsonPronto = json[..idx];
        else jsonPronto = json;

        try { Pacote = JsonSerializer.Deserialize<Pacote<T>>(jsonPronto); }
        catch { return; }

        if (Pacote == null) return;

        if (Pacote.Tipo == Pacote<T>.Tipos.ParaCliente)
        {
            switch (Pacote.Subtipo)
            {
                case Pacote<T>.Subtipos.Logar:
                    _idServidor = Pacote.IdAutor;
                    break;

                case Pacote<T>.Subtipos.Deslogar:
                    Dispose();
                    break;

                case Pacote<T>.Subtipos.Comando:
                    _tcpManager.CallBack(Pacote.Conteudo!);
                    break;
            }
        }
    }
    public void Dispose()
    {
        if (_cliente.Connected)
        {
            Pacote<T> msg = new()
            {
                Tipo = Pacote<T>.Tipos.ParaServer,
                Subtipo = Pacote<T>.Subtipos.Deslogar,
                IdAutor = _tcpManager.Id
            };
            _ = EnviarAsync(msg);
        }
        _cliente.Close();
    }
}
