using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaMasajes.Integracion.Models.Entities
{
    [Table("Citas")]
    public class Cita
    {
        [Key]
        public int CitaId { get; set; }

        [Required]
        public int ClienteId { get; set; }

        [ForeignKey("ClienteId")]
        public virtual Cliente? Cliente { get; set; } // opcional en el POST

        [Required]
        public int ServicioId { get; set; }

        [ForeignKey("ServicioId")]
        public virtual Servicio? Servicio { get; set; } // opcional en el POST

        public int? EmpleadoId { get; set; }

        [ForeignKey("EmpleadoId")]
        public virtual Empleado? Empleado { get; set; } // opcional

        [Required]
        public DateTime FechaHoraCita { get; set; }

        [Required]
        public DateTime FechaHoraIngresado { get; set; } = DateTime.Now;

        [Required]
        [StringLength(9)]
        public string Estado { get; set; } = "Reservada"; // Reservada, Cancelada, Realizada

        [StringLength(300)]
        public string? Observaciones { get; set; }
    }
}
