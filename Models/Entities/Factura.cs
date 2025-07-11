using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SistemaMasajes.Integracion.Models.Entities
{
    [Table("Facturas")]
    public class Factura
    {
        [Key]
        public int FacturaId { get; set; }

        [Required]
        public int ClienteId { get; set; }

        [JsonIgnore]
        [ForeignKey("ClienteId")]
        public virtual Cliente? Cliente { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;

        [Precision(10, 2)]
        public decimal Total { get; set; }

        [Required]
        [StringLength(13)]
        public string TipoPago { get; set; } // tarjeta, efectivo, transferencia

        public virtual ICollection<FacturaDetalle> Detalles { get; set; } = new List<FacturaDetalle>();

        public virtual ICollection<CuentaPorCobrar>? CuentasPorCobrar { get; set; }
     //   public ICollection<FacturaDetalle> FacturaDetalles { get; set; } = new List<FacturaDetalle>();
    }
}
