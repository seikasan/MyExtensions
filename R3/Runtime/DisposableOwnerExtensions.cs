using System;
using System.Threading;

namespace MyExtensions.R3
{
    public static class DisposableOwnerExtensions
    {
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

        public static CancellationToken GetCancellationTokenOnDispose(
            this IDisposableOwner owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            return owner.DisposeCancellationToken;
        }
    }
}