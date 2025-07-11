using Microsoft.AspNetCore.Mvc;

using SistemaMasajes.Integracion.Models.Entities;

using SistemaMasajes.Integracion.Services.Interfaces;

using SistemaMasajes.Integracion.Data;

using Microsoft.EntityFrameworkCore;

using System.Collections.Generic;

using System.Threading.Tasks;

using System.Net.Http; // Necesario para HttpRequestException

using System;

using SistemaMasajes.Integracion.Services.BackgroundSync; // Asegúrate de tener este using para ISyncQueue



namespace SistemaMasajes.Integracion.Controllers

{

    [ApiController]

    [Route("api/[controller]")]

    public class CitaController : ControllerBase

    {

        private readonly ICoreService _coreService;

        private readonly SistemaMasajesContext _context;

        private readonly ISyncQueue _syncQueue; // Inyecta ISyncQueue
        private readonly ILogger<CitaController> _logger;


        public CitaController(ICoreService coreService, SistemaMasajesContext context, ISyncQueue syncQueue, ILogger<CitaController> logger) // Agrega ISyncQueue al constructor

        {

            _coreService = coreService;

            _context = context;

            _syncQueue = syncQueue; // Asigna ISyncQueue

            _logger = logger;

        }



        [HttpGet]

        public async Task<ActionResult<IEnumerable<Cita>>> GetCitas()

        {

            try

            {

                var citas = await _coreService.GetAsync<List<Cita>>("Cita");

                Console.WriteLine("Citas obtenidas del servicio Core.");

                return Ok(citas);

            }

            catch (HttpRequestException ex)

            {

                Console.WriteLine($"Error al conectar con el servicio Core para GetCitas. Obteniendo de BD local: {ex.Message}");

                var citasLocal = await _context.Citas.ToListAsync(); // Fallback a BD local

                return Ok(citasLocal);

            }

            catch (Exception ex)

            {

                return StatusCode(500, $"Error interno al obtener citas: {ex.Message}");

            }

        }



        [HttpGet("{id}")]

        public async Task<ActionResult<Cita>> GetCita(int id)

        {

            try

            {

                var cita = await _coreService.GetAsync<Cita>($"Cita/{id}");



                if (cita == null)

                {

                    Console.WriteLine($"Core no devolvió cita con ID {id}. Verificando en BD local.");

                    var citaLocalFallback = await _context.Citas.FindAsync(id); // Fallback a BD local

                    if (citaLocalFallback == null)

                    {

                        return NotFound($"No se encontró la cita con ID {id} ni en Core ni en BD local.");

                    }

                    return Ok(citaLocalFallback);

                }

                Console.WriteLine($"Cita con ID {id} obtenida del servicio Core.");

                return Ok(cita);

            }

            catch (HttpRequestException ex)

            {

                Console.WriteLine($"Error al conectar con el servicio Core para GetCita/{id}. Obteniendo de BD local: {ex.Message}");

                var citaLocal = await _context.Citas.FindAsync(id); // Fallback a BD local

                if (citaLocal == null)

                    return NotFound($"No se encontró la cita con ID {id} en la BD local.");

                return Ok(citaLocal);

            }

            catch (Exception ex)

            {

                return StatusCode(500, $"Error interno al obtener cita con ID {id}: {ex.Message}");

            }

        }



        [HttpPost]
        public async Task<IActionResult> PostCita([FromBody] Cita cita)
        {
            if (cita == null)
            {
                _logger.LogWarning("POST Cita: Datos de cita inválidos (cita es null).");
                return BadRequest("Datos de cita inválidos");
            }

            // Generate a unique request ID to track
            var requestId = Guid.NewGuid().ToString();
            _logger.LogInformation("Inicio de POST Cita. Request ID: {RequestId}", requestId);

            // Start a database transaction for local operations
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Guardar en BD local primero
                _context.Citas.Add(cita);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Cita con ID {CitaId} guardada localmente. Request ID: {RequestId}", cita.CitaId, requestId);

                // 2. Intentar enviar al servicio Core
                try
                {
                    _logger.LogInformation("Intentando enviar cita al servicio Core. Request ID: {RequestId}", requestId);
                    var resultadoCore = await _coreService.PostAsync<Cita>("Cita", cita);
                    _logger.LogInformation("Cita enviada y confirmada por el servicio Core. Request ID: {RequestId}", requestId);

                    // If Core returns an updated Cita (e.g., with new ID), update local entity and save changes
                    // IMPORTANT: If Core assigns a new ID, you MUST update the local CitaId before committing
                    // If CitaId is identity on both sides, and Core's ID is the source of truth,
                    // you might need to find the local entity again or update its ID.
                    // For simplicity, assuming Core uses the same ID or generates one if 0.
                    // If Core assigns a *new* ID and your local ID is auto-generated, this part needs careful thought
                    // to prevent duplicate local records. A common strategy is to use a "CorrelationId" or "ExternalId"
                    // to link the local record to the Core record.
                    if (resultadoCore != null && resultadoCore.CitaId != cita.CitaId && resultadoCore.CitaId != 0)
                    {
                        _logger.LogInformation("Core asignó un nuevo CitaId: {CoreCitaId}. Actualizando entidad local. Request ID: {RequestId}", resultadoCore.CitaId, requestId);
                        // This part is tricky. If your local DB also auto-generates CitaId,
                        // you might need to mark the original 'cita' entity as detached,
                        // then create a *new* entity with the Core ID, and save that.
                        // Or, if local ID is not identity and you control it, assign Core's ID.
                        // For now, assuming if Core assigns an ID, you might need to handle it.
                        // Simplest: If Core's ID is the "true" ID, use it for future syncs.
                        // For *this* post, assuming the local CitaId is sufficient for a first save.
                        // If Core provides a *different* ID for the same logical record, you have a design decision:
                        // 1. Update the local record's ID to match Core's (if your DB allows changing primary keys, usually not).
                        // 2. Add an 'ExternalCitaId' column to your local Cita entity to store Core's ID.
                        // For the current problem (double POST), this is less critical, but important for data integrity.
                        // Let's assume for now your local and Core CitaId align or you have a strategy for this.
                    }

                    await transaction.CommitAsync(); // Commit local transaction only if Core successful
                    _logger.LogInformation("Transacción local para Cita con ID {CitaId} confirmada. Request ID: {RequestId}", cita.CitaId, requestId);
                    return Ok(new { mensaje = "Cita creada correctamente", data = resultadoCore });
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Advertencia: Error al enviar cita al servicio Core. Guardado solo localmente. Request ID: {RequestId}. Mensaje: {Message}", requestId, ex.Message);
                    _syncQueue.Enqueue<Cita>("Cita", cita, "POST"); // Encolar para sincronización
                    _logger.LogInformation("Cita con ID {CitaId} encolada para sincronización. Request ID: {RequestId}", cita.CitaId, requestId);
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    _logger.LogInformation("Transacción local para Cita con ID {CitaId} confirmada a pesar de error Core. Request ID: {RequestId}", cita.CitaId, requestId);
                    return Ok(new { mensaje = "Cita procesada. Guardada localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Advertencia: Error inesperado al enviar cita al servicio Core. Guardado solo localmente. Request ID: {RequestId}. Mensaje: {Message}", requestId, ex.Message);
                    _syncQueue.Enqueue<Cita>("Cita", cita, "POST"); // Encolar para sincronización (error inesperado)
                    _logger.LogInformation("Cita con ID {CitaId} encolada para sincronización (error inesperado). Request ID: {RequestId}", cita.CitaId, requestId);
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    _logger.LogInformation("Transacción local para Cita con ID {CitaId} confirmada a pesar de error Core. Request ID: {RequestId}", cita.CitaId, requestId);
                    return StatusCode(200, new { mensaje = "Cita procesada. Guardada localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(); // Rollback if local save or transaction management fails
                _logger.LogError(ex, "Error interno al crear cita. Transacción revertida. Request ID: {RequestId}. Mensaje: {Message}", requestId, ex.Message);
                return StatusCode(500, $"Error interno al crear cita: {ex.Message}");
            }
        }



        [HttpPut("{id}")]

        public async Task<IActionResult> PutCita(int id, [FromBody] Cita cita)

        {

            if (cita == null || id != cita.CitaId)

                return BadRequest("ID de cita inválido o no coincide");



            try

            {

                // 1. Verificar y actualizar en BD local

                var citaExistente = await _context.Citas.FindAsync(id);

                if (citaExistente == null)

                {

                    return NotFound($"No se encontró la cita con ID {id} en BD local para actualizar.");

                }



                _context.Entry(citaExistente).CurrentValues.SetValues(cita);

                await _context.SaveChangesAsync();

                Console.WriteLine($"Cita con ID {id} actualizada localmente.");



                // 2. Intentar enviar al servicio Core

                try

                {

                    await _coreService.PutAsync<Cita>($"Cita/{id}", cita);

                    Console.WriteLine("Cita actualizada y confirmada por el servicio Core.");

                }

                catch (HttpRequestException ex)

                {

                    Console.WriteLine($"Advertencia: Error al actualizar cita en el servicio Core. Actualizada solo localmente. {ex.Message}");

                    _syncQueue.Enqueue<Cita>($"Cita/{id}", cita, "PUT"); // Encolar para sincronización

                    Console.WriteLine($"Cita con ID {id} encolada para sincronización.");

                }

                catch (Exception ex)

                {

                    Console.WriteLine($"Advertencia: Error inesperado al actualizar cita en el servicio Core. Actualizada solo localmente. {ex.Message}");

                    _syncQueue.Enqueue<Cita>($"Cita/{id}", cita, "PUT"); // Encolar para sincronización (error inesperado)

                    Console.WriteLine($"Cita con ID {id} encolada para sincronización (error inesperado).");

                }



                return Ok(new { mensaje = "Cita procesada. Actualizada localmente, sincronización con Core intentada." });

            }

            catch (Exception ex)

            {

                return StatusCode(500, $"Error interno al actualizar cita: {ex.Message}");

            }

        }



        [HttpDelete("{id}")]

        public async Task<IActionResult> DeleteCita(int id)

        {

            try

            {

                // 1. Verificar y eliminar de BD local

                var citaExistente = await _context.Citas.FindAsync(id);

                if (citaExistente == null)

                {

                    return NotFound($"No se encontró la cita con ID {id} en BD local para eliminar.");

                }



                _context.Citas.Remove(citaExistente);

                await _context.SaveChangesAsync();

                Console.WriteLine($"Cita con ID {id} eliminada localmente.");



                // 2. Intentar eliminar del servicio Core

                try

                {

                    await _coreService.DeleteAsync($"Cita/{id}");

                    Console.WriteLine("Cita eliminada y confirmada por el servicio Core.");

                }

                catch (HttpRequestException ex)

                {

                    Console.WriteLine($"Advertencia: Error al eliminar cita del servicio Core. Eliminada solo localmente. {ex.Message}");

                    _syncQueue.Enqueue<object>($"Cita/{id}", null, "DELETE"); // Encolar para sincronización (usando object para null)

                    Console.WriteLine($"Cita con ID {id} encolada para sincronización.");

                }

                catch (Exception ex)

                {

                    Console.WriteLine($"Advertencia: Error inesperado al eliminar cita del servicio Core. Eliminada solo localmente. {ex.Message}");

                    _syncQueue.Enqueue<object>($"Cita/{id}", null, "DELETE"); // Encolar para sincronización (error inesperado)

                    Console.WriteLine($"Cita con ID {id} encolada para sincronización (error inesperado).");

                }



                return Ok(new { mensaje = "Cita procesada. Eliminada localmente, sincronización con Core intentada." });

            }

            catch (Exception ex)

            {

                return StatusCode(500, $"Error interno al eliminar cita: {ex.Message}");

            }

        }



        [HttpGet("local")]

        public async Task<ActionResult<IEnumerable<Cita>>> GetCitasLocal()

        {

            try

            {

                var citas = await _context.Citas

                  .Include(c => c.Cliente)

                  .ToListAsync();

                return Ok(citas);

            }

            catch (Exception ex)

            {

                return StatusCode(500, $"Error al obtener citas de BD local: {ex.Message}");

            }

        }



        [HttpGet("local/cliente/{clienteId}")]

        public async Task<ActionResult<IEnumerable<Cita>>> GetCitasByClienteLocal(int clienteId)

        {

            try

            {

                var citas = await _context.Citas

                  .Include(c => c.Cliente)

                  .Where(c => c.ClienteId == clienteId)

                  .ToListAsync();

                return Ok(citas);

            }

            catch (Exception ex)

            {

                return StatusCode(500, $"Error al obtener citas del cliente desde BD local: {ex.Message}");

            }

        }

    }

}