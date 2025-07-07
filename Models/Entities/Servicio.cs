using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaMasajes.Integracion.Models.Entities
{
    [Table("Servicios")]
    public class Servicio
    {
        [Key]
        public int ServicioId { get; set; }

        [Required]
        [StringLength(100)]
        public string NombreServicio { get; set; }

        [Precision(10, 2)]
        public decimal PrecioServicio { get; set; }

        public int DuracionPromedioMinutos { get; set; }

        public bool Activo { get; set; } = true;

        public virtual ICollection<Cita>? Citas { get; set; }
    }
}
