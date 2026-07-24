using System;
using System.Threading;
using R3;

namespace MyExtensions.R3
{
    /// <summary>
    /// Provides a thread-safe base class that owns disposable resources and a disposal cancellation token.
    /// </summary>
    public abstract class DisposableObject : IDisposableOwner, IDisposable
    {
        private readonly object _gate = new();
        private readonly CancellationDisposable _cancellation = new();
        private CompositeDisposable _disposables = new();
        private bool _disposed;

        /// <summary>
        /// Gets whether this object has been disposed.
        /// </summary>
        public bool IsDisposed
        {
            get
            {
                lock (_gate)
                {
                    return _disposed;
                }
            }
        }

        /// <summary>
        /// Gets a token that is canceled when this object is disposed.
        /// </summary>
        public CancellationToken DisposeCancellationToken => _cancellation.Token;

        /// <summary>
        /// Adds a disposable resource to this object's lifetime.
        /// </summary>
        /// <param name="disposable">The resource to dispose with this object.</param>
        /// <remarks>If this object is already disposed, the resource is disposed immediately.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="disposable"/> is <see langword="null"/>.</exception>
        public void Add(IDisposable disposable)
        {
            if (disposable == null) throw new ArgumentNullException(nameof(disposable));

            lock (_gate)
            {
                if (_disposed)
                {
                    disposable.Dispose();
                    return;
                }

                _disposables!.Add(disposable);
            }
        }

        /// <summary>
        /// Cancels the disposal token, disposes all owned resources, and invokes <see cref="OnDisposed"/>.
        /// </summary>
        public void Dispose()
        {
            CompositeDisposable disposables;

            lock (_gate)
            {
                if (_disposed) return;

                _disposed = true;
                disposables = _disposables;
                _disposables = null;
            }

            _cancellation.Dispose();
            disposables?.Dispose();
            OnDisposed();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Runs after the disposal token and owned resources have been disposed.
        /// </summary>
        /// <remarks>Derived classes can override this method to release additional resources.</remarks>
        protected virtual void OnDisposed()
        {
        }
    }
}