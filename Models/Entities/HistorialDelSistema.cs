using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaMasajes.Integracion.Models.Entities
{
    [Table("HistorialDelSistema")]
    public class HistorialDelSistema
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UsuarioId { get; set; }

        [Required]
        public string Accion { get; set; } // Ej: "Creó cliente", "Agendó cita"

        public DateTime FechaHora { get; set; } = DateTime.Now;

        // Referencia
        [ForeignKey("UsuarioId")]
        public virtual Usuario Usuario { get; set; }
    }
}
