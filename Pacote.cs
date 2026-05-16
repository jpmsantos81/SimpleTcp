namespace Jpmsantos81.SimpleTcp
{
    internal class Pacote
    {
        internal enum Tipos
        {
            ParaServer = 0,
            ParaCliente = 1
        }
        internal enum Subtipos
        {
            Logar = 0,
            Deslogar = 1,
            Comando = 2
        }
        internal Tipos Tipo { get; set; }
        internal Subtipos Subtipo { get; set; }
        internal string IdAutor { get; set; } = null!;
        internal string IdDestino { get; set; } = null!;
        internal object? Conteudo { get; set; }
    }
}
