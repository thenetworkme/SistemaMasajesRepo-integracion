using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaMasajes.Integracion.Models.Entities
{
    [Table("Citas")]
    public class Cita
    {
        public int CitaId { get; set; }

        public int ClienteId { get; set; }
        public virtual Cliente? Cliente { get; set; } // opcional en el POST

        public int ServicioId { get; set; }
        public virtual Servicio? Servicio { get; set; } // opcional en el POST

        public int? EmpleadoId { get; set; }
        public virtual Empleado? Empleado { get; set; } // opcional

        public DateTime FechaHoraCita { get; set; }

        public DateTime FechaHoraIngresado { get; set; }

        [Required]
        [StringLength(9)]
        public string Estado { get; set; } = "Reservada"; // Reservada, Cancelada, Realizada

        [StringLength(300)]
        public string? Observaciones { get; set; }
    }
}
