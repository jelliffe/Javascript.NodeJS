using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jering.Javascript.NodeJS
{
    /// <summary>
    /// An implementation of <see cref="INodeJSService"/> that uses Http for inter-process communication with a pool of NodeJS processes.
    /// </summary>
    public class HttpNodeJSPoolService : INodeJSService
    {
        private readonly int _maxIndex;
        private readonly object _httpNodeJSServicesLock = new object();
        private readonly ReadOnlyCollection<HttpNodeJSService> _httpNodeJSServices;

        private bool _disposed;
        private int _nextIndex;

        /// <summary>
        /// Gets the size of the <see cref="HttpNodeJSPoolService"/>.
        /// </summary>
        public int Size { get; }

        /// <summary>
        /// Creates a <see cref="HttpNodeJSPoolService"/> instance.
        /// </summary>
        public HttpNodeJSPoolService(ReadOnlyCollection<HttpNodeJSService> httpNodeJSServices)
        {
            _httpNodeJSServices = httpNodeJSServices;
            Size = httpNodeJSServices.Count;
            _maxIndex = Size - 1;
        }

        /// <inheritdoc />
        public Task<T> InvokeFromFileAsync<T>(string modulePath, string exportName = null, object[] args = null, CancellationToken cancellationToken = default)
        {
            return GetHttpNodeJSService().InvokeFromFileAsync<T>(modulePath, exportName, args, cancellationToken);
        }

        /// <inheritdoc />
        public Task<T> InvokeFromStringAsync<T>(string moduleString, string newCacheIdentifier = null, string exportName = null, object[] args = null, CancellationToken cancellationToken = default)
        {
            return GetHttpNodeJSService().InvokeFromStringAsync<T>(moduleString, newCacheIdentifier, exportName, args, cancellationToken);
        }

        /// <inheritdoc />
        public Task<T> InvokeFromStreamAsync<T>(Stream moduleStream, string newCacheIdentifier = null, string exportName = null, object[] args = null, CancellationToken cancellationToken = default)
        {
            return GetHttpNodeJSService().InvokeFromStreamAsync<T>(moduleStream, newCacheIdentifier, exportName, args, cancellationToken);
        }

        /// <inheritdoc />
        public Task<(bool, T)> TryInvokeFromCacheAsync<T>(string moduleCacheIdentifier, string exportName = null, object[] args = null, CancellationToken cancellationToken = default)
        {
            return GetHttpNodeJSService().TryInvokeFromCacheAsync<T>(moduleCacheIdentifier, exportName, args, cancellationToken);
        }

        internal HttpNodeJSService GetHttpNodeJSService()
        {
            int index = 0;
            lock (_httpNodeJSServicesLock)
            {
                if (_nextIndex > _maxIndex)
                {
                    _nextIndex = 0;
                }

                index = _nextIndex++;
            }

            return _httpNodeJSServices[index];
        }

        /// <summary>
        /// Disposes this instance. This method is not thread-safe. It should only be called after all other calls to this instance's methods have returned.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Disposes the instance. This method is not thread-safe. It should only be called after all other calls to this instance's methods have returned.
        /// </summary>
        /// <param name="disposing">True if the object is disposing or false if it is finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                foreach (HttpNodeJSService httpNodeJSService in _httpNodeJSServices)
                {
                    httpNodeJSService.Dispose();
                }
            }

            _disposed = true;
        }
    }
}
