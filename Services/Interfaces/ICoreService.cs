using SistemaMasajes.Integracion.Models.DTOs;

namespace SistemaMasajes.Integracion.Services.Interfaces
{
    // En la carpeta Interfaces
    public interface ICoreService
    {
        Task<T> GetAsync<T>(string endpoint);
        Task<T> PostAsync<T>(string endpoint, object data);
        Task<T> PutAsync<T>(string endpoint, object data);
        Task<bool> DeleteAsync(string endpoint);
    }
}