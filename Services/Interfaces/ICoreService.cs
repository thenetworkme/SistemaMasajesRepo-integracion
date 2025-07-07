using SistemaMasajes.Integracion.Models.DTOs;

namespace SistemaMasajes.Integracion.Services.Interfaces
{
    public interface ICoreService
    {
        Task<ClienteDTO> ObtenerClienteAsync(int id);
        Task<IEnumerable<ClienteDTO>> ObtenerClientesAsync();
        Task<ClienteDTO> CrearClienteAsync(ClienteDTO cliente);
        Task<CitaDTO> CrearCitaAsync(CitaDTO cita);
        Task<IEnumerable<CitaDTO>> ObtenerCitasAsync();
    }
}