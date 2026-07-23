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

            private sealed class Coordinator : IDisposable
            {
                private static readonly TimerCallback ExpiryCallback = OnExpiryTimer;

                private readonly object _sync = new();

                private readonly Observer<T> _downstream;
                private readonly Observable<T> _source;
                private readonly Func<bool> _canExecuteNow;
                private readonly Observable<bool> _canExecuteChanged;
                private readonly TimeSpan _lifetime;
                private readonly InputBufferPolicy _policy;
                private readonly TimeProvider _timeProvider;

                private IDisposable _sourceSubscription;
                private IDisposable _conditionSubscription;
                private ITimer _expiryTimer;

                private T _bufferedValue;
                private bool _hasBufferedValue;
                private long _bufferVersion;
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
                }

                public void Start()
                {
                    var condition = _canExecuteChanged.Subscribe(
                        new ConditionObserver(this));

                    AttachConditionSubscription(condition);

                    var input = _source.Subscribe(
                        new SourceObserver(this));

                    AttachSourceSubscription(input);
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
                    if (!value)
                    {
                        return;
                    }

                    TryConsume();
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
                    ITimer previousTimer;
                    long version;

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

                        _bufferedValue = value;
                        _hasBufferedValue = true;
                        version = ++_bufferVersion;

                        previousTimer = _expiryTimer;
                        _expiryTimer = null;
                    }

                    if (previousTimer != null)
                    {
                        previousTimer.Dispose();
                    }

                    ITimer newTimer;

                    try
                    {
                        newTimer = _timeProvider.CreateTimer(
                            ExpiryCallback,
                            new ExpiryState(this, version),
                            _lifetime,
                            Timeout.InfiniteTimeSpan);
                    }
                    catch
                    {
                        lock (_sync)
                        {
                            if (_bufferVersion == version)
                            {
                                _hasBufferedValue = false;
                                _bufferedValue = default(T);
                                _bufferVersion++;
                            }
                        }

                        throw;
                    }

                    var keepTimer = false;

                    lock (_sync)
                    {
                        if (_disposed == 0 &&
                            _hasBufferedValue &&
                            _bufferVersion == version)
                        {
                            _expiryTimer = newTimer;
                            keepTimer = true;
                        }
                    }

                    if (!keepTimer)
                    {
                        newTimer.Dispose();
                    }
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
                    ITimer timer;

                    lock (_sync)
                    {
                        if (_disposed != 0 || !_hasBufferedValue)
                        {
                            return;
                        }

                        value = _bufferedValue;

                        _hasBufferedValue = false;
                        _bufferedValue = default(T);
                        _bufferVersion++;

                        timer = _expiryTimer;
                        _expiryTimer = null;
                    }

                    if (timer != null)
                    {
                        timer.Dispose();
                    }

                    _downstream.OnNext(value);
                }

                private void Expire(long version)
                {
                    ITimer timer;

                    lock (_sync)
                    {
                        if (_disposed != 0 ||
                            !_hasBufferedValue ||
                            _bufferVersion != version)
                        {
                            return;
                        }

                        _hasBufferedValue = false;
                        _bufferedValue = default(T);
                        _bufferVersion++;

                        timer = _expiryTimer;
                        _expiryTimer = null;
                    }

                    if (timer != null)
                    {
                        timer.Dispose();
                    }
                }

                private void ClearBufferedValue()
                {
                    ITimer timer;

                    lock (_sync)
                    {
                        _hasBufferedValue = false;
                        _bufferedValue = default(T);
                        _bufferVersion++;

                        timer = _expiryTimer;
                        _expiryTimer = null;
                    }

                    if (timer != null)
                    {
                        timer.Dispose();
                    }
                }

                private void AttachSourceSubscription(
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
                            _sourceSubscription = subscription;
                        }
                    }

                    if (disposeImmediately)
                    {
                        subscription.Dispose();
                    }
                }

                private void AttachConditionSubscription(
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
                            _conditionSubscription = subscription;
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
                        _bufferedValue = default(T);
                        _bufferVersion++;
                    }

                    if (timer != null)
                    {
                        timer.Dispose();
                    }

                    if (input != null)
                    {
                        input.Dispose();
                    }

                    if (condition != null)
                    {
                        condition.Dispose();
                    }
                }

                private static void OnExpiryTimer(object state)
                {
                    var expiryState = (ExpiryState)state;

                    expiryState.Owner.Expire(
                        expiryState.Version);
                }

                private sealed class ExpiryState
                {
                    public readonly Coordinator Owner;
                    public readonly long Version;

                    public ExpiryState(
                        Coordinator owner,
                        long version)
                    {
                        Owner = owner;
                        Version = version;
                    }
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
