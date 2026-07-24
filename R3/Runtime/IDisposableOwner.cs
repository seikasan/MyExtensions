using System;
using System.Threading;

namespace MyExtensions.R3
{
    /// <summary>
    /// Represents an object that owns disposable resources and exposes its disposal lifetime.
    /// </summary>
    public interface IDisposableOwner
    {
        /// <summary>
        /// Gets whether the owner has been disposed.
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Gets a token that is canceled when the owner is disposed.
        /// </summary>
        CancellationToken DisposeCancellationToken { get; }

        /// <summary>
        /// Adds a disposable resource to the owner's lifetime.
        /// </summary>
        /// <param name="disposable">The resource to dispose with the owner.</param>
        void Add(IDisposable disposable);
    }
}
