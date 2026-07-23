using System;
using System.Threading;
using R3;

namespace MyExtensions.InputSystem.R3
{
    public enum InputBufferPolicy
    {
        First,
        Latest
    }

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

                public void OnConditionChanged(bool value)
                {
                    if (value)
                    {
                        TryConsume();
                    }
                }

                public void OnErrorResume(Exception exception)
                {
                    _downstream.OnErrorResume(exception);
                }

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

                    public SourceObserver(Coordinator owner)
                    {
                        _owner = owner;
                    }

                    protected override void OnNextCore(T value)
                    {
                        _owner.OnSourceNext(value);
                    }

                    protected override void OnErrorResumeCore(
                        Exception error)
                    {
                        _owner.OnErrorResume(error);
                    }

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

                    public ConditionObserver(Coordinator owner)
                    {
                        _owner = owner;
                    }

                    protected override void OnNextCore(bool value)
                    {
                        _owner.OnConditionChanged(value);
                    }

                    protected override void OnErrorResumeCore(
                        Exception error)
                    {
                        _owner.OnErrorResume(error);
                    }

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
