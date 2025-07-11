using SistemaMasajes.Integracion.Models.DTOs; // Asegúrate de que este namespace sea correcto si usas DTOs
using System.Threading.Tasks;

namespace SistemaMasajes.Integracion.Services.Interfaces
{
    // En la carpeta Interfaces
    public interface ICoreService
    {
        // GET: Permite obtener datos del Core y deserializarlos a un tipo T específico.
        Task<T> GetAsync<T>(string endpoint);

        // POST (versión 1): Para cuando la integración necesita el objeto de respuesta
        // del Core (ej. el objeto completo con un ID asignado por el Core).
        Task<T> PostAsync<T>(string endpoint, object data);

        // POST (versión 2): Para cuando solo necesitas enviar los datos al Core
        // y no te interesa el objeto de respuesta, solo si la operación fue exitosa.
        // Utilizada por el servicio de sincronización en segundo plano.
        Task PostAsync(string endpoint, object data);

        // PUT (versión 1): Para cuando la integración necesita el objeto de respuesta
        // del Core (ej. la entidad actualizada con los datos finales del Core).
        Task<T> PutAsync<T>(string endpoint, object data);

        // PUT (versión 2): Para cuando solo necesitas enviar los datos al Core
        // y no te interesa el objeto de respuesta, solo si la operación fue exitosa.
        // Utilizada por el servicio de sincronización en segundo plano.
        Task PutAsync(string endpoint, object data);

        // DELETE: Simplemente envía la solicitud de eliminación y no espera
        // ningún valor de retorno específico, solo la confirmación de la operación.
        // Utilizada tanto por el controlador como por el servicio de sincronización.
        Task DeleteAsync(string endpoint);
    }
}