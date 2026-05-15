namespace Jpmsantos81.SimpleTcp
{
    public class GerenciadorTcp<T> : IDisposable
    {
        public readonly string Delimitador;
        private ClienteTcp<T> Cliente { get;  set; } = null!;
        private ServidorTcp<T> Servidor { get; set; } = null!;
        internal Action<T> CallBack { get; private set; }
        public string Id { get; private set; }

        public GerenciadorTcp(Action<T> callback, string delimitador = "<EOF>", string id = "")
        {
            CallBack = callback;

            Delimitador = string.IsNullOrEmpty(delimitador) ? Guid.NewGuid().ToString() : delimitador;
            Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString() : id;
        }
        public bool IniciarServidor(int porta)
        {
            if (Cliente != null) return false;
            if (Servidor != null)
            { 
                Servidor.Dispose(); 
                Servidor = null!;
            }

            Servidor = new ServidorTcp<T>(this);
            return Servidor.Iniciar(porta);
        }
        public void DesligarServidor()
        {
            Servidor?.Dispose(); Servidor = null!;
        }
        public async Task<bool> ConectarClienteAsync(string ip, int porta)
        {
            if (Servidor != null) return false;
            if (Cliente != null)
            {
                Cliente.Dispose();
                Cliente = null!;
            }

            Cliente = new ClienteTcp<T>(this);
            return await Cliente.ConectarAsync(ip, porta);
        }
        public void DesconectarCliente()
        {
            Cliente?.Dispose(); Cliente = null!;
        }
        public void Dispose()
        {
            Cliente.Dispose(); 
            Cliente = null!;

            Servidor.Dispose();
            Servidor = null!;
        }
    }
}
