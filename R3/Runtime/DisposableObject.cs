using System;
using System.Threading;
using R3;

namespace MyExtensions.R3
{
    public abstract class DisposableObject : IDisposableOwner, IDisposable
    {
        private readonly object _gate = new();
        private readonly CancellationDisposable _cancellation = new();
        private CompositeDisposable _disposables = new();
        private bool _disposed;

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

        public CancellationToken DisposeCancellationToken => _cancellation.Token;

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

        protected virtual void OnDisposed()
        {
        }
    }
}