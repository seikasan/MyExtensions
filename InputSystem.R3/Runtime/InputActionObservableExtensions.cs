using System;
using System.Runtime.CompilerServices;
using System.Threading;
using R3;
using UnityEngine.InputSystem;

namespace MyExtensions.InputSystem.R3
{
    /// <summary>
    /// Provides event-based R3 observable conversions for Unity Input System actions.
    /// </summary>
    /// <remarks>
    /// These methods publish Input System phase callbacks. They do not poll an action
    /// continuously while a control is held.
    /// </remarks>
    public static class InputActionObservableExtensions
    {
        /// <summary>
        /// Creates an observable that publishes when the action enters the started phase.
        /// </summary>
        /// <param name="action">The action whose started callbacks are observed.</param>
        /// <returns>An observable that publishes one <see cref="Unit"/> per started callback.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public static Observable<Unit> StartedAsObservable(
            this InputAction action)
        {
            return Create<Unit, UnitSelector>(
                action,
                InputActionPhase.Started);
        }

        /// <summary>
        /// Creates an observable that publishes when the action enters the performed phase.
        /// </summary>
        /// <param name="action">The action whose performed callbacks are observed.</param>
        /// <returns>An observable that publishes one <see cref="Unit"/> per performed callback.</returns>
        /// <remarks>
        /// This method does not publish continuously while the input is held. Use
        /// <see cref="InputActionPollingObservableExtensions.WhilePressedAsObservable(InputAction)"/>
        /// for continuous pressed notifications.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public static Observable<Unit> PerformedAsObservable(
            this InputAction action)
        {
            return Create<Unit, UnitSelector>(
                action,
                InputActionPhase.Performed);
        }

        /// <summary>
        /// Creates an observable that publishes when the action enters the canceled phase.
        /// </summary>
        /// <param name="action">The action whose canceled callbacks are observed.</param>
        /// <returns>An observable that publishes one <see cref="Unit"/> per canceled callback.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public static Observable<Unit> CanceledAsObservable(
            this InputAction action)
        {
            return Create<Unit, UnitSelector>(
                action,
                InputActionPhase.Canceled);
        }

        /// <summary>
        /// Creates an observable that reads and publishes the action value for each started callback.
        /// </summary>
        /// <typeparam name="TValue">The value type read from the action.</typeparam>
        /// <param name="action">The action whose started callbacks are observed.</param>
        /// <returns>An observable containing values read during started callbacks.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public static Observable<TValue> StartedAsObservable<TValue>(
            this InputAction action)
            where TValue : struct
        {
            return Create<TValue, ValueSelector<TValue>>(
                action,
                InputActionPhase.Started);
        }

        /// <summary>
        /// Creates an observable that reads and publishes the action value for each performed callback.
        /// </summary>
        /// <typeparam name="TValue">The value type read from the action.</typeparam>
        /// <param name="action">The action whose performed callbacks are observed.</param>
        /// <returns>An observable containing values read during performed callbacks.</returns>
        /// <remarks>
        /// This method only publishes performed callbacks. Use
        /// <see cref="InputActionPollingObservableExtensions.ReadValueAsObservable{TValue}(InputAction)"/>
        /// to read the current value after every Input System update.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public static Observable<TValue> PerformedAsObservable<TValue>(
            this InputAction action)
            where TValue : struct
        {
            return Create<TValue, ValueSelector<TValue>>(
                action,
                InputActionPhase.Performed);
        }

        /// <summary>
        /// Creates an observable that reads and publishes the action value for each canceled callback.
        /// </summary>
        /// <typeparam name="TValue">The value type read from the action.</typeparam>
        /// <param name="action">The action whose canceled callbacks are observed.</param>
        /// <returns>An observable containing values read during canceled callbacks.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public static Observable<TValue> CanceledAsObservable<TValue>(
            this InputAction action)
            where TValue : struct
        {
            return Create<TValue, ValueSelector<TValue>>(
                action,
                InputActionPhase.Canceled);
        }

        /// <summary>
        /// Creates an observable that publishes when the referenced action enters the started phase.
        /// </summary>
        /// <param name="reference">The reference containing the action to observe.</param>
        /// <returns>An observable that publishes one <see cref="Unit"/> per started callback.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">The reference does not contain an action.</exception>
        public static Observable<Unit> StartedAsObservable(
            this InputActionReference reference)
        {
            return GetAction(reference).StartedAsObservable();
        }

        /// <summary>
        /// Creates an observable that publishes when the referenced action enters the performed phase.
        /// </summary>
        /// <param name="reference">The reference containing the action to observe.</param>
        /// <returns>An observable that publishes one <see cref="Unit"/> per performed callback.</returns>
        /// <remarks>This method does not publish continuously while the input is held.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">The reference does not contain an action.</exception>
        public static Observable<Unit> PerformedAsObservable(
            this InputActionReference reference)
        {
            return GetAction(reference).PerformedAsObservable();
        }

        /// <summary>
        /// Creates an observable that publishes when the referenced action enters the canceled phase.
        /// </summary>
        /// <param name="reference">The reference containing the action to observe.</param>
        /// <returns>An observable that publishes one <see cref="Unit"/> per canceled callback.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">The reference does not contain an action.</exception>
        public static Observable<Unit> CanceledAsObservable(
            this InputActionReference reference)
        {
            return GetAction(reference).CanceledAsObservable();
        }

        /// <summary>
        /// Creates an observable that reads and publishes the referenced action value for each started callback.
        /// </summary>
        /// <typeparam name="TValue">The value type read from the action.</typeparam>
        /// <param name="reference">The reference containing the action to observe.</param>
        /// <returns>An observable containing values read during started callbacks.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">The reference does not contain an action.</exception>
        public static Observable<TValue> StartedAsObservable<TValue>(
            this InputActionReference reference)
            where TValue : struct
        {
            return GetAction(reference)
                .StartedAsObservable<TValue>();
        }

        /// <summary>
        /// Creates an observable that reads and publishes the referenced action value for each performed callback.
        /// </summary>
        /// <typeparam name="TValue">The value type read from the action.</typeparam>
        /// <param name="reference">The reference containing the action to observe.</param>
        /// <returns>An observable containing values read during performed callbacks.</returns>
        /// <remarks>This method only publishes performed callbacks and does not poll continuously.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">The reference does not contain an action.</exception>
        public static Observable<TValue> PerformedAsObservable<TValue>(
            this InputActionReference reference)
            where TValue : struct
        {
            return GetAction(reference)
                .PerformedAsObservable<TValue>();
        }

        /// <summary>
        /// Creates an observable that reads and publishes the referenced action value for each canceled callback.
        /// </summary>
        /// <typeparam name="TValue">The value type read from the action.</typeparam>
        /// <param name="reference">The reference containing the action to observe.</param>
        /// <returns>An observable containing values read during canceled callbacks.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">The reference does not contain an action.</exception>
        public static Observable<TValue> CanceledAsObservable<TValue>(
            this InputActionReference reference)
            where TValue : struct
        {
            return GetAction(reference)
                .CanceledAsObservable<TValue>();
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

        private static Observable<T> Create<T, TSelector>(
            InputAction action,
            InputActionPhase phase)
            where TSelector : struct, ICallbackSelector<T>
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            return new InputActionPhaseObservable<T, TSelector>(
                action,
                phase);
        }

        private static void AddHandler(
            InputAction action,
            InputActionPhase phase,
            Action<InputAction.CallbackContext> handler)
        {
            switch (phase)
            {
                case InputActionPhase.Started:
                    action.started += handler;
                    break;

                case InputActionPhase.Performed:
                    action.performed += handler;
                    break;

                case InputActionPhase.Canceled:
                    action.canceled += handler;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(phase));
            }
        }

        private static void RemoveHandler(
            InputAction action,
            InputActionPhase phase,
            Action<InputAction.CallbackContext> handler)
        {
            switch (phase)
            {
                case InputActionPhase.Started:
                    action.started -= handler;
                    break;

                case InputActionPhase.Performed:
                    action.performed -= handler;
                    break;

                case InputActionPhase.Canceled:
                    action.canceled -= handler;
                    break;
            }
        }

        private enum InputActionPhase
        {
            Started,
            Performed,
            Canceled
        }

        private interface ICallbackSelector<T>
        {
            T Select(InputAction.CallbackContext context);
        }

        private readonly struct UnitSelector
            : ICallbackSelector<Unit>
        {
            /// <inheritdoc />
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Unit Select(InputAction.CallbackContext context)
            {
                return Unit.Default;
            }
        }

        private readonly struct ValueSelector<TValue>
            : ICallbackSelector<TValue>
            where TValue : struct
        {
            /// <inheritdoc />
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public TValue Select(InputAction.CallbackContext context)
            {
                return context.ReadValue<TValue>();
            }
        }

        private sealed class InputActionPhaseObservable<T, TSelector>
            : Observable<T>
            where TSelector : struct, ICallbackSelector<T>
        {
            private readonly InputAction _action;
            private readonly InputActionPhase _phase;

            /// <summary>
            /// Initializes a phase observable for the specified action.
            /// </summary>
            /// <param name="action">The action to observe.</param>
            /// <param name="phase">The action phase to observe.</param>
            public InputActionPhaseObservable(
                InputAction action,
                InputActionPhase phase)
            {
                _action = action;
                _phase = phase;
            }

            /// <inheritdoc />
            protected override IDisposable SubscribeCore(
                Observer<T> observer)
            {
                return new Subscription(
                    _action,
                    _phase,
                    observer);
            }

            private sealed class Subscription : IDisposable
            {
                private readonly InputAction _action;
                private readonly InputActionPhase _phase;
                private readonly Observer<T> _observer;
                private readonly Action<InputAction.CallbackContext> _handler;

                private int _disposed;

                /// <summary>
                /// Initializes a subscription and attaches its Input System callback.
                /// </summary>
                /// <param name="action">The action to observe.</param>
                /// <param name="phase">The phase callback to attach.</param>
                /// <param name="observer">The observer receiving selected callback values.</param>
                public Subscription(
                    InputAction action,
                    InputActionPhase phase,
                    Observer<T> observer)
                {
                    _action = action;
                    _phase = phase;
                    _observer = observer;
                    _handler = OnCallback;

                    AddHandler(_action, _phase, _handler);
                }

                private void OnCallback(
                    InputAction.CallbackContext context)
                {
                    if (Volatile.Read(ref _disposed) != 0)
                    {
                        return;
                    }

                    T value;

                    try
                    {
                        var selector = default(TSelector);
                        value = selector.Select(context);
                    }
                    catch (Exception exception)
                    {
                        _observer.OnErrorResume(exception);
                        return;
                    }

                    _observer.OnNext(value);
                }

                /// <inheritdoc />
                public void Dispose()
                {
                    if (Interlocked.Exchange(ref _disposed, 1) != 0)
                    {
                        return;
                    }

                    RemoveHandler(_action, _phase, _handler);
                }
            }
        }
    }
}
