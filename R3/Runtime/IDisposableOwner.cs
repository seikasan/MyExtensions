using System;
using System.Threading;

namespace MyExtensions.R3
{
    public interface IDisposableOwner
    {
        bool IsDisposed { get; }

        CancellationToken DisposeCancellationToken { get; }
        void Add(IDisposable disposable);
    }
}