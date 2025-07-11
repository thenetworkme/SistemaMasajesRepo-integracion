namespace SistemaMasajes.Integracion.Services.BackgroundSync
{
    public interface ISyncQueue
    {
        void Enqueue<T>(string endpoint, T data, string httpMethod);
        Task<(string Endpoint, object Data, string HttpMethod)> DequeueAsync(CancellationToken cancellationToken);
    }
}