using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SistemaMasajes.Integracion.Models.Entities
{
    [Table("CuentasPorCobrar")]
    public class CuentaPorCobrar
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int FacturaId { get; set; }

        [JsonIgnore]
        [ForeignKey("FacturaId")]
        public virtual Factura? Factura { get; set; }

        [Precision(10, 2)]
        public decimal MontoPendiente { get; set; }

        public bool Pagado { get; set; } = false;
    }
}
