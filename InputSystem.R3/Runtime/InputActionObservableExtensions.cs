using System;
using System.Runtime.CompilerServices;
using System.Threading;
using R3;
using UnityEngine.InputSystem;

namespace MyExtensions.InputSystem.R3
{
    public static class InputActionObservableExtensions
    {
        public static Observable<Unit> StartedAsObservable(
            this InputAction action)
        {
            return Create<Unit, UnitSelector>(
                action,
                InputActionPhase.Started);
        }

        public static Observable<Unit> PerformedAsObservable(
            this InputAction action)
        {
            return Create<Unit, UnitSelector>(
                action,
                InputActionPhase.Performed);
        }

        public static Observable<Unit> CanceledAsObservable(
            this InputAction action)
        {
            return Create<Unit, UnitSelector>(
                action,
                InputActionPhase.Canceled);
        }

        public static Observable<TValue> StartedAsObservable<TValue>(
            this InputAction action)
            where TValue : struct
        {
            return Create<TValue, ValueSelector<TValue>>(
                action,
                InputActionPhase.Started);
        }

        public static Observable<TValue> PerformedAsObservable<TValue>(
            this InputAction action)
            where TValue : struct
        {
            return Create<TValue, ValueSelector<TValue>>(
                action,
                InputActionPhase.Performed);
        }

        public static Observable<TValue> CanceledAsObservable<TValue>(
            this InputAction action)
            where TValue : struct
        {
            return Create<TValue, ValueSelector<TValue>>(
                action,
                InputActionPhase.Canceled);
        }

        public static Observable<Unit> StartedAsObservable(
            this InputActionReference reference)
        {
            return GetAction(reference).StartedAsObservable();
        }

        public static Observable<Unit> PerformedAsObservable(
            this InputActionReference reference)
        {
            return GetAction(reference).PerformedAsObservable();
        }

        public static Observable<Unit> CanceledAsObservable(
            this InputActionReference reference)
        {
            return GetAction(reference).CanceledAsObservable();
        }

        public static Observable<TValue> StartedAsObservable<TValue>(
            this InputActionReference reference)
            where TValue : struct
        {
            return GetAction(reference)
                .StartedAsObservable<TValue>();
        }

        public static Observable<TValue> PerformedAsObservable<TValue>(
            this InputActionReference reference)
            where TValue : struct
        {
            return GetAction(reference)
                .PerformedAsObservable<TValue>();
        }

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

            public InputActionPhaseObservable(
                InputAction action,
                InputActionPhase phase)
            {
                _action = action;
                _phase = phase;
            }

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
