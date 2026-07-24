using System;
using System.Runtime.CompilerServices;
using System.Threading;
using R3;
using UnityEngine.InputSystem;
using UnityInputSystem = UnityEngine.InputSystem.InputSystem;

namespace MyExtensions.InputSystem.R3
{
    /// <summary>
    /// Provides polling-based observables for values that must be read continuously.
    /// </summary>
    public static class InputActionPollingObservableExtensions
    {
        /// <summary>
        /// Reads and publishes the current action value after every Input System update.
        /// </summary>
        /// <typeparam name="TValue">The value type read from the action.</typeparam>
        /// <param name="action">The action whose current value is polled.</param>
        /// <returns>An observable that repeatedly publishes the current action value while the action is enabled.</returns>
        /// <remarks>The same value is published repeatedly while it is held.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public static Observable<TValue> ReadValueAsObservable<TValue>(
            this InputAction action)
            where TValue : struct
        {
            return Create<
                TValue,
                ReadValueSelector<TValue>,
                AlwaysPredicate>(action);
        }

        /// <summary>
        /// Publishes once after every Input System update while the action is pressed.
        /// </summary>
        /// <param name="action">The action whose pressed state is polled.</param>
        /// <returns>An observable that publishes <see cref="Unit"/> while the action remains pressed and enabled.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public static Observable<Unit> WhilePressedAsObservable(
            this InputAction action)
        {
            return Create<
                Unit,
                UnitSelector,
                IsPressedPredicate>(action);
        }

        /// <summary>
        /// Publishes the current pressed state after every Input System update.
        /// </summary>
        /// <param name="action">The action whose pressed state is polled.</param>
        /// <returns>An observable that publishes the current pressed state while the action is enabled.</returns>
        /// <remarks>Apply <c>DistinctUntilChanged</c> when only state transitions are required.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public static Observable<bool> IsPressedAsObservable(
            this InputAction action)
        {
            return Create<
                bool,
                IsPressedSelector,
                AlwaysPredicate>(action);
        }

        /// <summary>
        /// Reads and publishes the referenced action value after every Input System update.
        /// </summary>
        /// <typeparam name="TValue">The value type read from the action.</typeparam>
        /// <param name="reference">The reference containing the action to poll.</param>
        /// <returns>An observable that repeatedly publishes the current action value while the action is enabled.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">The reference does not contain an action.</exception>
        public static Observable<TValue> ReadValueAsObservable<TValue>(
            this InputActionReference reference)
            where TValue : struct
        {
            return GetAction(reference)
                .ReadValueAsObservable<TValue>();
        }

        /// <summary>
        /// Publishes after every Input System update while the referenced action is pressed.
        /// </summary>
        /// <param name="reference">The reference containing the action to poll.</param>
        /// <returns>An observable that publishes <see cref="Unit"/> while the action remains pressed and enabled.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">The reference does not contain an action.</exception>
        public static Observable<Unit> WhilePressedAsObservable(
            this InputActionReference reference)
        {
            return GetAction(reference)
                .WhilePressedAsObservable();
        }

        /// <summary>
        /// Publishes the referenced action's pressed state after every Input System update.
        /// </summary>
        /// <param name="reference">The reference containing the action to poll.</param>
        /// <returns>An observable that publishes the current pressed state while the action is enabled.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">The reference does not contain an action.</exception>
        public static Observable<bool> IsPressedAsObservable(
            this InputActionReference reference)
        {
            return GetAction(reference)
                .IsPressedAsObservable();
        }

        private static InputAction GetAction(
            InputActionReference reference)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            if (reference.action == null)
            {
                throw new InvalidOperationException(
                    "The InputAction is not set in the InputActionReference.");
            }

            return reference.action;
        }

        private static Observable<T> Create<T, TSelector, TPredicate>(
            InputAction action)
            where TSelector : struct, IInputActionSelector<T>
            where TPredicate : struct, IInputActionPredicate
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            return new InputActionPollingObservable<
                T,
                TSelector,
                TPredicate>(action);
        }

        private interface IInputActionSelector<T>
        {
            T Select(InputAction action);
        }

        private interface IInputActionPredicate
        {
            bool ShouldNotify(InputAction action);
        }

        private readonly struct ReadValueSelector<TValue>
            : IInputActionSelector<TValue>
            where TValue : struct
        {
            /// <inheritdoc />
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public TValue Select(InputAction action)
            {
                return action.ReadValue<TValue>();
            }
        }

        private readonly struct UnitSelector
            : IInputActionSelector<Unit>
        {
            /// <inheritdoc />
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Unit Select(InputAction action)
            {
                return Unit.Default;
            }
        }

        private readonly struct IsPressedSelector
            : IInputActionSelector<bool>
        {
            /// <inheritdoc />
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Select(InputAction action)
            {
                return action.IsPressed();
            }
        }

        private readonly struct AlwaysPredicate
            : IInputActionPredicate
        {
            /// <inheritdoc />
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool ShouldNotify(InputAction action)
            {
                return true;
            }
        }

        private readonly struct IsPressedPredicate
            : IInputActionPredicate
        {
            /// <inheritdoc />
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool ShouldNotify(InputAction action)
            {
                return action.IsPressed();
            }
        }

        private sealed class InputActionPollingObservable<
            T,
            TSelector,
            TPredicate> : Observable<T>
            where TSelector : struct, IInputActionSelector<T>
            where TPredicate : struct, IInputActionPredicate
        {
            private readonly InputAction _action;

            /// <summary>
            /// Initializes a polling observable for the specified action.
            /// </summary>
            /// <param name="action">The action to poll.</param>
            public InputActionPollingObservable(InputAction action)
            {
                _action = action;
            }

            /// <inheritdoc />
            protected override IDisposable SubscribeCore(
                Observer<T> observer)
            {
                return new Subscription(_action, observer);
            }

            private sealed class Subscription : IDisposable
            {
                private readonly InputAction _action;
                private readonly Observer<T> _observer;
                private readonly Action _handler;

                private int _disposed;

                /// <summary>
                /// Initializes a subscription and attaches it to the Input System update callback.
                /// </summary>
                /// <param name="action">The action to poll.</param>
                /// <param name="observer">The observer receiving polled values.</param>
                public Subscription(
                    InputAction action,
                    Observer<T> observer)
                {
                    _action = action;
                    _observer = observer;
                    _handler = OnAfterUpdate;

                    UnityInputSystem.onAfterUpdate += _handler;
                }

                private void OnAfterUpdate()
                {
                    if (Volatile.Read(ref _disposed) != 0 ||
                        !_action.enabled)
                    {
                        return;
                    }

                    try
                    {
                        var predicate = default(TPredicate);

                        if (!predicate.ShouldNotify(_action))
                        {
                            return;
                        }

                        var selector = default(TSelector);
                        _observer.OnNext(selector.Select(_action));
                    }
                    catch (Exception exception)
                    {
                        _observer.OnErrorResume(exception);
                    }
                }

                /// <inheritdoc />
                public void Dispose()
                {
                    if (Interlocked.Exchange(ref _disposed, 1) != 0)
                    {
                        return;
                    }

                    UnityInputSystem.onAfterUpdate -= _handler;
                }
            }
        }
    }
}
