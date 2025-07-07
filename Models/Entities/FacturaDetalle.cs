using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SistemaMasajes.Integracion.Models.Entities
{
    [Table("FacturaDetalles")]
    public class FacturaDetalle
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int FacturaId { get; set; }

        [JsonIgnore]
        [ForeignKey("FacturaId")]
        public virtual Factura? Factura { get; set; }

        [Required]
        [StringLength(8)]
        public string Tipo { get; set; } // "Servicio" o "Producto"

        [Required]
        [StringLength(100)]
        public string NombreItem { get; set; }

        [Precision(10, 2)]
        public decimal PrecioUnitario { get; set; }

        [Precision(10, 2)]
        public int Cantidad { get; set; }

        [Precision(10, 2)]
        public decimal Subtotal { get; set; }
    }
}
