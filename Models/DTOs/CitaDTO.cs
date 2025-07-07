namespace SistemaMasajes.Integracion.Models.DTOs
{
    public class CitaDTO
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public DateTime FechaHora { get; set; }
        public string TipoMasaje { get; set; }
        public decimal Precio { get; set; }
        public string Estado { get; set; }
    }
}