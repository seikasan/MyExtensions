using System;

namespace MyExtensions.R3
{
    public interface IDisposableOwner
    {
        bool IsDisposed { get; }

        void Add(IDisposable disposable);
    }
}