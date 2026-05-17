using System.Text.Json.Serialization;

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
        [JsonInclude]
        internal Tipos Tipo { get; set; }
        
        [JsonInclude]
        internal Subtipos Subtipo { get; set; }
        [JsonInclude]
        internal string IdAutor { get; set; } = null!;
        [JsonInclude]
        internal string IdDestino { get; set; } = null!;
        [JsonInclude]
        internal object? Conteudo { get; set; }
    }
}
