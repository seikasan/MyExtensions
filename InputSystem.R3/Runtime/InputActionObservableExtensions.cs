using System;
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
            return Create(
                action,
                InputActionPhase.Started,
                _ => Unit.Default);
        }

        public static Observable<Unit> PerformedAsObservable(
            this InputAction action)
        {
            return Create(
                action,
                InputActionPhase.Performed,
                _ => Unit.Default);
        }

        public static Observable<Unit> CanceledAsObservable(
            this InputAction action)
        {
            return Create(
                action,
                InputActionPhase.Canceled,
                _ => Unit.Default);
        }

        public static Observable<TValue> StartedAsObservable<TValue>(
            this InputAction action)
            where TValue : struct
        {
            return Create(
                action,
                InputActionPhase.Started,
                context => context.ReadValue<TValue>());
        }

        public static Observable<TValue> PerformedAsObservable<TValue>(
            this InputAction action)
            where TValue : struct
        {
            return Create(
                action,
                InputActionPhase.Performed,
                context => context.ReadValue<TValue>());
        }

        public static Observable<TValue> CanceledAsObservable<TValue>(
            this InputAction action)
            where TValue : struct
        {
            return Create(
                action,
                InputActionPhase.Canceled,
                context => context.ReadValue<TValue>());
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

        private static Observable<T> Create<T>(
            InputAction action,
            InputActionPhase phase,
            Func<InputAction.CallbackContext, T> selector)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            return new InputActionPhaseObservable<T>(
                action,
                phase,
                selector);
        }

        private enum InputActionPhase
        {
            Started,
            Performed,
            Canceled
        }

        private sealed class InputActionPhaseObservable<T>
            : Observable<T>
        {
            private readonly InputAction _action;
            private readonly InputActionPhase _phase;
            private readonly Func<InputAction.CallbackContext, T> _selector;

            public InputActionPhaseObservable(
                InputAction action,
                InputActionPhase phase,
                Func<InputAction.CallbackContext, T> selector)
            {
                _action = action;
                _phase = phase;
                _selector = selector;
            }

            protected override IDisposable SubscribeCore(
                Observer<T> observer)
            {
                return new Subscription(
                    _action,
                    _phase,
                    _selector,
                    observer);
            }

            private sealed class Subscription : IDisposable
            {
                private readonly InputAction _action;
                private readonly InputActionPhase _phase;
                private readonly Func<InputAction.CallbackContext, T> _selector;
                private readonly Observer<T> _observer;
                private readonly Action<InputAction.CallbackContext> _handler;

                private int _disposed;

                public Subscription(
                    InputAction action,
                    InputActionPhase phase,
                    Func<InputAction.CallbackContext, T> selector,
                    Observer<T> observer)
                {
                    _action = action;
                    _phase = phase;
                    _selector = selector;
                    _observer = observer;
                    _handler = OnCallback;

                    AddHandler();
                }

                private void AddHandler()
                {
                    switch (_phase)
                    {
                        case InputActionPhase.Started:
                            _action.started += _handler;
                            break;

                        case InputActionPhase.Performed:
                            _action.performed += _handler;
                            break;

                        case InputActionPhase.Canceled:
                            _action.canceled += _handler;
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                private void RemoveHandler()
                {
                    switch (_phase)
                    {
                        case InputActionPhase.Started:
                            _action.started -= _handler;
                            break;

                        case InputActionPhase.Performed:
                            _action.performed -= _handler;
                            break;

                        case InputActionPhase.Canceled:
                            _action.canceled -= _handler;
                            break;
                    }
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
                        value = _selector(context);
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

                    RemoveHandler();
                }
            }
        }
    }
}
