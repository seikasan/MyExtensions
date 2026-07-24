using System;
using System.Threading;
using R3;

namespace MyExtensions.InputSystem.R3
{
    /// <summary>
    /// Specifies how a single-slot input buffer handles additional values before consumption.
    /// </summary>
    public enum InputBufferPolicy
    {
        /// <summary>
        /// Keeps the first buffered value and ignores later values until it is consumed or expires.
        /// </summary>
        First,

        /// <summary>
        /// Replaces the buffered value with the most recently received value and restarts its lifetime.
        /// </summary>
        Latest
    }

    /// <summary>
    /// Provides R3 operators for retaining input values until an execution condition becomes true.
    /// </summary>
    public static class InputBufferExtensions
    {
        private static readonly TimerCallback ExpiryCallback = OnExpiryTimer;

        private static void OnExpiryTimer(object state)
        {
            ((IExpiryTimerTarget)state).OnExpiryTimer();
        }

        private interface IExpiryTimerTarget
        {
            void OnExpiryTimer();
        }

        /// <summary>
        /// Emits source values immediately when execution is allowed, or temporarily buffers one value
        /// until execution becomes allowed.
        /// </summary>
        /// <typeparam name="T">The type of input value.</typeparam>
        /// <param name="source">The source observable containing input values.</param>
        /// <param name="canExecuteNow">A synchronous function that returns whether a value can be emitted now.</param>
        /// <param name="canExecuteChanged">An observable that notifies changes to the execution condition.</param>
        /// <param name="lifetime">The maximum duration for which a value remains buffered.</param>
        /// <param name="policy">The policy used when another value arrives while a value is buffered.</param>
        /// <param name="timeProvider">The provider used to measure and schedule the buffer lifetime. When omitted, the Unity update time provider is used.</param>
        /// <returns>An observable that emits immediately executable values and valid buffered values.</returns>
        /// <remarks>
        /// The buffer stores at most one value. A buffered value is emitted once when
        /// <paramref name="canExecuteChanged"/> publishes <see langword="true"/> and
        /// <paramref name="canExecuteNow"/> also returns <see langword="true"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="source"/>, <paramref name="canExecuteNow"/>, or
        /// <paramref name="canExecuteChanged"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="lifetime"/> is not positive, or <paramref name="policy"/> is invalid.
        /// </exception>
        public static Observable<T> BufferUntil<T>(
            this Observable<T> source,
            Func<bool> canExecuteNow,
            Observable<bool> canExecuteChanged,
            TimeSpan lifetime,
            InputBufferPolicy policy = InputBufferPolicy.Latest,
            TimeProvider timeProvider = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (canExecuteNow == null)
            {
                throw new ArgumentNullException(nameof(canExecuteNow));
            }

            if (canExecuteChanged == null)
            {
                throw new ArgumentNullException(
                    nameof(canExecuteChanged));
            }

            if (lifetime <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lifetime),
                    lifetime,
                    "The input buffer time must be greater than 0.");
            }

            if (policy != InputBufferPolicy.First &&
                policy != InputBufferPolicy.Latest)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(policy));
            }

            timeProvider ??= UnityTimeProvider.Update;

            return new InputBufferObservable<T>(
                source,
                canExecuteNow,
                canExecuteChanged,
                lifetime,
                policy,
                timeProvider);
        }

        private sealed class InputBufferObservable<T>
            : Observable<T>
        {
            private readonly Observable<T> _source;
            private readonly Func<bool> _canExecuteNow;
            private readonly Observable<bool> _canExecuteChanged;
            private readonly TimeSpan _lifetime;
            private readonly InputBufferPolicy _policy;
            private readonly TimeProvider _timeProvider;

            /// <summary>
            /// Initializes an input-buffer observable.
            /// </summary>
            /// <param name="source">The source input observable.</param>
            /// <param name="canExecuteNow">The synchronous execution-condition reader.</param>
            /// <param name="canExecuteChanged">The execution-condition change stream.</param>
            /// <param name="lifetime">The maximum buffered duration.</param>
            /// <param name="policy">The replacement policy for buffered values.</param>
            /// <param name="timeProvider">The provider used for timing.</param>
            public InputBufferObservable(
                Observable<T> source,
                Func<bool> canExecuteNow,
                Observable<bool> canExecuteChanged,
                TimeSpan lifetime,
                InputBufferPolicy policy,
                TimeProvider timeProvider)
            {
                _source = source;
                _canExecuteNow = canExecuteNow;
                _canExecuteChanged = canExecuteChanged;
                _lifetime = lifetime;
                _policy = policy;
                _timeProvider = timeProvider;
            }

            /// <inheritdoc />
            protected override IDisposable SubscribeCore(
                Observer<T> observer)
            {
                var coordinator = new Coordinator(
                    observer,
                    _source,
                    _canExecuteNow,
                    _canExecuteChanged,
                    _lifetime,
                    _policy,
                    _timeProvider);

                coordinator.Start();
                return coordinator;
            }

            private sealed class Coordinator
                : IDisposable, IExpiryTimerTarget
            {
                private readonly object _sync = new();

                private readonly Observer<T> _downstream;
                private readonly Observable<T> _source;
                private readonly Func<bool> _canExecuteNow;
                private readonly Observable<bool> _canExecuteChanged;
                private readonly TimeSpan _lifetime;
                private readonly InputBufferPolicy _policy;
                private readonly TimeProvider _timeProvider;
                private readonly bool _usesUnityTimestampTicks;

                private IDisposable _sourceSubscription;
                private IDisposable _conditionSubscription;
                private ITimer _expiryTimer;

                private T _bufferedValue;
                private bool _hasBufferedValue;
                private long _bufferedAtTimestamp;
                private int _disposed;

                /// <summary>
                /// Initializes the coordinator that owns source, condition, and timer subscriptions.
                /// </summary>
                /// <param name="downstream">The downstream observer.</param>
                /// <param name="source">The source input observable.</param>
                /// <param name="canExecuteNow">The synchronous execution-condition reader.</param>
                /// <param name="canExecuteChanged">The execution-condition change stream.</param>
                /// <param name="lifetime">The maximum buffered duration.</param>
                /// <param name="policy">The replacement policy for buffered values.</param>
                /// <param name="timeProvider">The provider used for timing.</param>
                public Coordinator(
                    Observer<T> downstream,
                    Observable<T> source,
                    Func<bool> canExecuteNow,
                    Observable<bool> canExecuteChanged,
                    TimeSpan lifetime,
                    InputBufferPolicy policy,
                    TimeProvider timeProvider)
                {
                    _downstream = downstream;
                    _source = source;
                    _canExecuteNow = canExecuteNow;
                    _canExecuteChanged = canExecuteChanged;
                    _lifetime = lifetime;
                    _policy = policy;
                    _timeProvider = timeProvider;
                    _usesUnityTimestampTicks = timeProvider is UnityTimeProvider;
                }

                /// <summary>
                /// Starts subscriptions to the execution condition and source observable.
                /// </summary>
                public void Start()
                {
                    try
                    {
                        var condition = _canExecuteChanged.Subscribe(
                            new ConditionObserver(this));

                        AttachSubscription(
                            ref _conditionSubscription,
                            condition);

                        var input = _source.Subscribe(
                            new SourceObserver(this));

                        AttachSubscription(
                            ref _sourceSubscription,
                            input);
                    }
                    catch
                    {
                        Dispose();
                        throw;
                    }
                }

                /// <summary>
                /// Handles an input value received from the source observable.
                /// </summary>
                /// <param name="value">The received input value.</param>
                public void OnSourceNext(T value)
                {
                    if (Volatile.Read(ref _disposed) != 0)
                    {
                        return;
                    }

                    bool canExecute;

                    try
                    {
                        canExecute = _canExecuteNow();
                    }
                    catch (Exception exception)
                    {
                        _downstream.OnErrorResume(exception);
                        return;
                    }

                    if (canExecute)
                    {
                        // 実行可能な新規入力を優先し、
                        // 古い保存入力は破棄する。
                        ClearBufferedValue();
                        _downstream.OnNext(value);
                        return;
                    }

                    Buffer(value);
                }

                /// <summary>
                /// Handles a change to the execution condition.
                /// </summary>
                /// <param name="value">The latest execution-condition value.</param>
                public void OnConditionChanged(bool value)
                {
                    if (value)
                    {
                        TryConsume();
                    }
                }

                /// <summary>
                /// Forwards a recoverable source error to the downstream observer.
                /// </summary>
                /// <param name="exception">The error to forward.</param>
                public void OnErrorResume(Exception exception)
                {
                    _downstream.OnErrorResume(exception);
                }

                /// <summary>
                /// Completes the downstream observer and releases owned resources.
                /// </summary>
                /// <param name="result">The source completion result.</param>
                public void OnSourceCompleted(Result result)
                {
                    ClearBufferedValue();
                    _downstream.OnCompleted(result);
                    Dispose();
                }

                private void Buffer(T value)
                {
                    ITimer timerToDispose = null;

                    lock (_sync)
                    {
                        if (_disposed != 0)
                        {
                            return;
                        }

                        if (_hasBufferedValue &&
                            _policy == InputBufferPolicy.First)
                        {
                            return;
                        }

                        try
                        {
                            var timestamp = _timeProvider.GetTimestamp();

                            _bufferedValue = value;
                            _hasBufferedValue = true;
                            _bufferedAtTimestamp = timestamp;

                            if (_expiryTimer == null ||
                                !_expiryTimer.Change(
                                    _lifetime,
                                    Timeout.InfiniteTimeSpan))
                            {
                                timerToDispose = _expiryTimer;
                                _expiryTimer = CreateExpiryTimerLocked();

                                if (!_expiryTimer.Change(
                                        _lifetime,
                                        Timeout.InfiniteTimeSpan))
                                {
                                    throw new InvalidOperationException(
                                        "The input buffer timer could not be scheduled.");
                                }
                            }
                        }
                        catch
                        {
                            var timer = _expiryTimer;

                            _expiryTimer = null;
                            _hasBufferedValue = false;
                            _bufferedValue = default;
                            _bufferedAtTimestamp = 0;

                            timer?.Dispose();

                            if (!ReferenceEquals(timer, timerToDispose))
                            {
                                timerToDispose?.Dispose();
                            }

                            throw;
                        }
                    }

                    timerToDispose?.Dispose();
                }

                private ITimer CreateExpiryTimerLocked()
                {
                    var timer = _timeProvider.CreateTimer(
                        ExpiryCallback,
                        this,
                        Timeout.InfiniteTimeSpan,
                        Timeout.InfiniteTimeSpan);

                    if (timer == null)
                    {
                        throw new InvalidOperationException(
                            "The time provider returned a null timer.");
                    }

                    return timer;
                }

                private void TryConsume()
                {
                    if (Volatile.Read(ref _disposed) != 0)
                    {
                        return;
                    }

                    bool canExecute;

                    try
                    {
                        canExecute = _canExecuteNow();
                    }
                    catch (Exception exception)
                    {
                        _downstream.OnErrorResume(exception);
                        return;
                    }

                    if (!canExecute)
                    {
                        return;
                    }

                    T value;

                    lock (_sync)
                    {
                        if (_disposed != 0 || !_hasBufferedValue)
                        {
                            return;
                        }

                        value = _bufferedValue;
                        ClearBufferedValueLocked();
                    }

                    _downstream.OnNext(value);
                }

                private void Expire()
                {
                    lock (_sync)
                    {
                        if (_disposed != 0 || !_hasBufferedValue)
                        {
                            return;
                        }

                        var elapsed = GetElapsedTimeLocked();

                        if (elapsed < _lifetime)
                        {
                            var remaining = _lifetime - elapsed;

                            if (_expiryTimer == null ||
                                !_expiryTimer.Change(
                                    remaining,
                                    Timeout.InfiniteTimeSpan))
                            {
                                ClearBufferedValueLocked();
                            }

                            return;
                        }

                        _hasBufferedValue = false;
                        _bufferedValue = default;
                        _bufferedAtTimestamp = 0;
                    }
                }

                private TimeSpan GetElapsedTimeLocked()
                {
                    var currentTimestamp = _timeProvider.GetTimestamp();

                    if (_usesUnityTimestampTicks)
                    {
                        return TimeSpan.FromTicks(
                            currentTimestamp - _bufferedAtTimestamp);
                    }

                    return _timeProvider.GetElapsedTime(
                        _bufferedAtTimestamp,
                        currentTimestamp);
                }

                private void ClearBufferedValue()
                {
                    lock (_sync)
                    {
                        if (!_hasBufferedValue)
                        {
                            return;
                        }

                        ClearBufferedValueLocked();
                    }
                }

                private void ClearBufferedValueLocked()
                {
                    _hasBufferedValue = false;
                    _bufferedValue = default;
                    _bufferedAtTimestamp = 0;

                    _expiryTimer?.Change(
                        Timeout.InfiniteTimeSpan,
                        Timeout.InfiniteTimeSpan);
                }

                private void AttachSubscription(
                    ref IDisposable target,
                    IDisposable subscription)
                {
                    var disposeImmediately = false;

                    lock (_sync)
                    {
                        if (_disposed != 0)
                        {
                            disposeImmediately = true;
                        }
                        else
                        {
                            target = subscription;
                        }
                    }

                    if (disposeImmediately)
                    {
                        subscription.Dispose();
                    }
                }

                /// <inheritdoc />
                public void Dispose()
                {
                    if (Interlocked.Exchange(ref _disposed, 1) != 0)
                    {
                        return;
                    }

                    IDisposable input;
                    IDisposable condition;
                    ITimer timer;

                    lock (_sync)
                    {
                        input = _sourceSubscription;
                        condition = _conditionSubscription;
                        timer = _expiryTimer;

                        _sourceSubscription = null;
                        _conditionSubscription = null;
                        _expiryTimer = null;

                        _hasBufferedValue = false;
                        _bufferedValue = default;
                        _bufferedAtTimestamp = 0;
                    }

                    timer?.Dispose();
                    input?.Dispose();
                    condition?.Dispose();
                }

                void IExpiryTimerTarget.OnExpiryTimer()
                {
                    Expire();
                }

                private sealed class SourceObserver : Observer<T>
                {
                    private readonly Coordinator _owner;

                    /// <summary>
                    /// Initializes an observer that forwards source notifications to its coordinator.
                    /// </summary>
                    /// <param name="owner">The owning coordinator.</param>
                    public SourceObserver(Coordinator owner)
                    {
                        _owner = owner;
                    }

                    /// <inheritdoc />
                    protected override void OnNextCore(T value)
                    {
                        _owner.OnSourceNext(value);
                    }

                    /// <inheritdoc />
                    protected override void OnErrorResumeCore(
                        Exception error)
                    {
                        _owner.OnErrorResume(error);
                    }

                    /// <inheritdoc />
                    protected override void OnCompletedCore(
                        Result result)
                    {
                        _owner.OnSourceCompleted(result);
                    }
                }

                private sealed class ConditionObserver
                    : Observer<bool>
                {
                    private readonly Coordinator _owner;

                    /// <summary>
                    /// Initializes an observer that forwards condition changes to its coordinator.
                    /// </summary>
                    /// <param name="owner">The owning coordinator.</param>
                    public ConditionObserver(Coordinator owner)
                    {
                        _owner = owner;
                    }

                    /// <inheritdoc />
                    protected override void OnNextCore(bool value)
                    {
                        _owner.OnConditionChanged(value);
                    }

                    /// <inheritdoc />
                    protected override void OnErrorResumeCore(
                        Exception error)
                    {
                        _owner.OnErrorResume(error);
                    }

                    /// <inheritdoc />
                    protected override void OnCompletedCore(
                        Result result)
                    {
                        // 条件Observableの完了だけでは
                        // 入力Observableを完了させない。
                    }
                }
            }
        }
    }
}
