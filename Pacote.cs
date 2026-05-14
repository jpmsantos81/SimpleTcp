namespace SimpleTcp
{
    internal class Pacote<T>
    {
        public enum Tipos
        {
            ParaServer = 0,
            ParaCliente = 1,
            ParaSistema = 2
        }
        public enum Subtipos
        {
            Logar = 0,
            Deslogar = 1,
            Comando = 2
        }
        public Tipos Tipo { get; set; }
        public Subtipos Subtipo { get; set; }
        public string IdAutor { get; set; } = null!;
        public string IdDestino { get; set; } = null!;
        public T? Conteudo { get; set; }
    }
}
