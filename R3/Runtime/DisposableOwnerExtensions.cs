using System;
using System.Threading;

namespace MyExtensions.R3
{
    /// <summary>
    /// Provides convenience methods for resources owned by an <see cref="IDisposableOwner"/>.
    /// </summary>
    public static class DisposableOwnerExtensions
    {
        /// <summary>
        /// Adds a disposable resource to an owner's lifetime and returns the same resource.
        /// </summary>
        /// <typeparam name="TDisposable">The concrete disposable type.</typeparam>
        /// <param name="disposable">The resource to add.</param>
        /// <param name="owner">The owner that controls the resource lifetime.</param>
        /// <returns>The same <paramref name="disposable"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="disposable"/> or <paramref name="owner"/> is <see langword="null"/>.</exception>
        public static TDisposable AddTo<TDisposable>(
            this TDisposable disposable,
            IDisposableOwner owner)
            where TDisposable : IDisposable
        {
            if (disposable == null) throw new ArgumentNullException(nameof(disposable));
            if (owner == null) throw new ArgumentNullException(nameof(owner));

            owner.Add(disposable);
            return disposable;
        }

        /// <summary>
        /// Gets the cancellation token that is canceled when the owner is disposed.
        /// </summary>
        /// <param name="owner">The disposable owner.</param>
        /// <returns>The owner's disposal cancellation token.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="owner"/> is <see langword="null"/>.</exception>
        public static CancellationToken GetCancellationTokenOnDispose(
            this IDisposableOwner owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            return owner.DisposeCancellationToken;
        }
    }
}