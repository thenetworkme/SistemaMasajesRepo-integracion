using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SistemaMasajes.Integracion.Services.BackgroundSync
{
    public class SyncQueue : ISyncQueue
    {
        private readonly ConcurrentQueue<(string Endpoint, object Data, string HttpMethod)> _queue = new ConcurrentQueue<(string Endpoint, object Data, string HttpMethod)>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);

        public void Enqueue<T>(string endpoint, T data, string httpMethod)
        {
            _queue.Enqueue((endpoint, data, httpMethod));
            _signal.Release(); // Señala que hay un elemento en la cola
        }

        public async Task<(string Endpoint, object Data, string HttpMethod)> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken); // Espera hasta que haya un elemento
            _queue.TryDequeue(out var item);
            return item;
        }
    }
}