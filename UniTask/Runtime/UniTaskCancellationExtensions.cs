using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

#if ENABLE_UNITYWEBREQUEST && (!UNITY_2019_1_OR_NEWER || UNITASK_WEBREQUEST_SUPPORT)
using UnityEngine.Networking;
#endif

namespace MyExtensions.UniTaskCancellation
{
    public static class UniTaskCancellationExtensions
    {
        // Yield / Frame

        public static UniTask Yield(
            this CancellationToken ct,
            bool cancelImmediately = false)
            => UniTask.Yield(
                ct,
                cancelImmediately);

        public static UniTask Yield(
            this CancellationToken ct,
            PlayerLoopTiming timing,
            bool cancelImmediately = false)
            => UniTask.Yield(
                timing,
                ct,
                cancelImmediately);

        public static UniTask NextFrame(
            this CancellationToken ct,
            bool cancelImmediately = false)
            => UniTask.NextFrame(
                ct,
                cancelImmediately);

        public static UniTask NextFrame(
            this CancellationToken ct,
            PlayerLoopTiming timing,
            bool cancelImmediately = false)
            => UniTask.NextFrame(
                timing,
                ct,
                cancelImmediately);

        public static UniTask WaitForFixedUpdate(
            this CancellationToken ct,
            bool cancelImmediately = false)
            => UniTask.WaitForFixedUpdate(
                ct,
                cancelImmediately);

        public static UniTask WaitForEndOfFrame(
            this CancellationToken ct)
            => UniTask.WaitForEndOfFrame(ct);

        public static UniTask WaitForEndOfFrame(
            this CancellationToken ct,
            MonoBehaviour coroutineRunner,
            bool cancelImmediately = false)
            => UniTask.WaitForEndOfFrame(
                coroutineRunner,
                ct,
                cancelImmediately);

        // Delay

        public static UniTask DelayFrame(
            this CancellationToken ct,
            int delayFrameCount,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update,
            bool cancelImmediately = false)
            => UniTask.DelayFrame(
                delayFrameCount,
                delayTiming,
                ct,
                cancelImmediately);

        public static UniTask Delay(
            this CancellationToken ct,
            int millisecondsDelay,
            bool ignoreTimeScale = false,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update,
            bool cancelImmediately = false)
            => UniTask.Delay(
                millisecondsDelay,
                ignoreTimeScale,
                delayTiming,
                ct,
                cancelImmediately);

        public static UniTask Delay(
            this CancellationToken ct,
            TimeSpan delayTimeSpan,
            bool ignoreTimeScale = false,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update,
            bool cancelImmediately = false)
            => UniTask.Delay(
                delayTimeSpan,
                ignoreTimeScale,
                delayTiming,
                ct,
                cancelImmediately);

        public static UniTask Delay(
            this CancellationToken ct,
            int millisecondsDelay,
            DelayType delayType,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update,
            bool cancelImmediately = false)
            => UniTask.Delay(
                millisecondsDelay,
                delayType,
                delayTiming,
                ct,
                cancelImmediately);

        public static UniTask Delay(
            this CancellationToken ct,
            TimeSpan delayTimeSpan,
            DelayType delayType,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update,
            bool cancelImmediately = false)
            => UniTask.Delay(
                delayTimeSpan,
                delayType,
                delayTiming,
                ct,
                cancelImmediately);

        public static UniTask WaitForSeconds(
            this CancellationToken ct,
            float duration,
            bool ignoreTimeScale = false,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update,
            bool cancelImmediately = false)
            => UniTask.WaitForSeconds(
                duration,
                ignoreTimeScale,
                delayTiming,
                ct,
                cancelImmediately);

        public static UniTask WaitForSeconds(
            this CancellationToken ct,
            int duration,
            bool ignoreTimeScale = false,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update,
            bool cancelImmediately = false)
            => UniTask.WaitForSeconds(
                duration,
                ignoreTimeScale,
                delayTiming,
                ct,
                cancelImmediately);

        // Wait

        public static UniTask WaitUntil(
            this CancellationToken ct,
            Func<bool> predicate,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            bool cancelImmediately = false)
            => UniTask.WaitUntil(
                predicate,
                timing,
                ct,
                cancelImmediately);

        public static UniTask WaitUntil<T>(
            this CancellationToken ct,
            T state,
            Func<T, bool> predicate,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            bool cancelImmediately = false)
            => UniTask.WaitUntil(
                state,
                predicate,
                timing,
                ct,
                cancelImmediately);

        public static UniTask WaitWhile(
            this CancellationToken ct,
            Func<bool> predicate,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            bool cancelImmediately = false)
            => UniTask.WaitWhile(
                predicate,
                timing,
                ct,
                cancelImmediately);

        public static UniTask WaitWhile<T>(
            this CancellationToken ct,
            T state,
            Func<T, bool> predicate,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            bool cancelImmediately = false)
            => UniTask.WaitWhile(
                state,
                predicate,
                timing,
                ct,
                cancelImmediately);

        public static UniTask<U> WaitUntilValueChanged<T, U>(
            this CancellationToken ct,
            T target,
            Func<T, U> monitorFunction,
            PlayerLoopTiming monitorTiming = PlayerLoopTiming.Update,
            IEqualityComparer<U> equalityComparer = null,
            bool cancelImmediately = false)
            where T : class
            => UniTask.WaitUntilValueChanged(
                target,
                monitorFunction,
                monitorTiming,
                equalityComparer,
                ct,
                cancelImmediately);

        // Thread

        public static SwitchToMainThreadAwaitable SwitchToMainThread(
            this CancellationToken ct)
            => UniTask.SwitchToMainThread(ct);

        public static SwitchToMainThreadAwaitable SwitchToMainThread(
            this CancellationToken ct,
            PlayerLoopTiming timing)
            => UniTask.SwitchToMainThread(
                timing,
                ct);

        public static ReturnToMainThread ReturnToMainThread(
            this CancellationToken ct)
            => UniTask.ReturnToMainThread(ct);

        public static ReturnToMainThread ReturnToMainThread(
            this CancellationToken ct,
            PlayerLoopTiming timing)
            => UniTask.ReturnToMainThread(
                timing,
                ct);

        public static SwitchToSynchronizationContextAwaitable
            SwitchToSynchronizationContext(
                this CancellationToken ct,
                SynchronizationContext synchronizationContext)
            => UniTask.SwitchToSynchronizationContext(
                synchronizationContext,
                ct);

        public static ReturnToSynchronizationContext
            ReturnToSynchronizationContext(
                this CancellationToken ct,
                SynchronizationContext synchronizationContext)
            => UniTask.ReturnToSynchronizationContext(
                synchronizationContext,
                ct);

        public static ReturnToSynchronizationContext
            ReturnToCurrentSynchronizationContext(
                this CancellationToken ct,
                bool dontPostWhenSameContext = true)
            => UniTask.ReturnToCurrentSynchronizationContext(
                dontPostWhenSameContext,
                ct);

        // ThreadPool

        public static UniTask RunOnThreadPool(
            this CancellationToken ct,
            Action action,
            bool configureAwait = true)
            => UniTask.RunOnThreadPool(
                action,
                configureAwait,
                ct);

        public static UniTask RunOnThreadPool(
            this CancellationToken ct,
            Action<object> action,
            object state,
            bool configureAwait = true)
            => UniTask.RunOnThreadPool(
                action,
                state,
                configureAwait,
                ct);

        public static UniTask RunOnThreadPool(
            this CancellationToken ct,
            Func<UniTask> action,
            bool configureAwait = true)
            => UniTask.RunOnThreadPool(
                action,
                configureAwait,
                ct);

        public static UniTask RunOnThreadPool(
            this CancellationToken ct,
            Func<object, UniTask> action,
            object state,
            bool configureAwait = true)
            => UniTask.RunOnThreadPool(
                action,
                state,
                configureAwait,
                ct);

        public static UniTask<T> RunOnThreadPool<T>(
            this CancellationToken ct,
            Func<T> func,
            bool configureAwait = true)
            => UniTask.RunOnThreadPool(
                func,
                configureAwait,
                ct);

        public static UniTask<T> RunOnThreadPool<T>(
            this CancellationToken ct,
            Func<UniTask<T>> func,
            bool configureAwait = true)
            => UniTask.RunOnThreadPool(
                func,
                configureAwait,
                ct);

        public static UniTask<T> RunOnThreadPool<T>(
            this CancellationToken ct,
            Func<object, T> func,
            object state,
            bool configureAwait = true)
            => UniTask.RunOnThreadPool(
                func,
                state,
                configureAwait,
                ct);

        public static UniTask<T> RunOnThreadPool<T>(
            this CancellationToken ct,
            Func<object, UniTask<T>> func,
            object state,
            bool configureAwait = true)
            => UniTask.RunOnThreadPool(
                func,
                state,
                configureAwait,
                ct);

        // ToUniTask: AsyncOperation

        public static UniTask ToUniTask(
            this AsyncOperation operation,
            CancellationToken ct,
            IProgress<float> progress = null,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            bool cancelImmediately = false)
            => operation.ToUniTask(
                progress,
                timing,
                ct,
                cancelImmediately);

        // ToUniTask: ResourceRequest

        public static UniTask<Object> ToUniTask(
            this ResourceRequest operation,
            CancellationToken ct,
            IProgress<float> progress = null,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            bool cancelImmediately = false)
            => operation.ToUniTask(
                progress,
                timing,
                ct,
                cancelImmediately);

        // ToUniTask: AssetBundle

#if UNITASK_ASSETBUNDLE_SUPPORT

    public static UniTask<UnityEngine.Object> ToUniTask(
        this AssetBundleRequest operation,
        CancellationToken ct,
        IProgress<float> progress = null,
        PlayerLoopTiming timing = PlayerLoopTiming.Update,
        bool cancelImmediately = false)
        => operation.ToUniTask(
            progress,
            timing,
            ct,
            cancelImmediately);

    public static UniTask<AssetBundle> ToUniTask(
        this AssetBundleCreateRequest operation,
        CancellationToken ct,
        IProgress<float> progress = null,
        PlayerLoopTiming timing = PlayerLoopTiming.Update,
        bool cancelImmediately = false)
        => operation.ToUniTask(
            progress,
            timing,
            ct,
            cancelImmediately);

#endif

        // ToUniTask: UnityWebRequest

#if ENABLE_UNITYWEBREQUEST && (!UNITY_2019_1_OR_NEWER || UNITASK_WEBREQUEST_SUPPORT)

    public static UniTask<UnityWebRequest> ToUniTask(
        this UnityWebRequestAsyncOperation operation,
        CancellationToken ct,
        IProgress<float> progress = null,
        PlayerLoopTiming timing = PlayerLoopTiming.Update,
        bool cancelImmediately = false)
        => operation.ToUniTask(
            progress,
            timing,
            ct,
            cancelImmediately);

#endif

        // ToUniTask: AsyncGPUReadback

        public static UniTask<AsyncGPUReadbackRequest> ToUniTask(
            this AsyncGPUReadbackRequest operation,
            CancellationToken ct,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            bool cancelImmediately = false)
            => operation.ToUniTask(
                timing,
                ct,
                cancelImmediately);

        // ToUniTask: IEnumerator

        public static UniTask ToUniTask(
            this IEnumerator enumerator,
            CancellationToken ct,
            PlayerLoopTiming timing = PlayerLoopTiming.Update)
            => enumerator.ToUniTask(
                timing,
                ct);

        // ToUniTask: AsyncInstantiateOperation

        public static UniTask<Object[]> ToUniTask(
            this AsyncInstantiateOperation operation,
            CancellationToken ct,
            IProgress<float> progress = null,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            bool cancelImmediately = false)
            => operation.ToUniTask(
                progress,
                timing,
                ct,
                cancelImmediately);

        public static UniTask<T[]> ToUniTask<T>(
            this AsyncInstantiateOperation<T> operation,
            CancellationToken ct,
            IProgress<float> progress = null,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            bool cancelImmediately = false)
            where T : Object
            => operation.ToUniTask(
                progress,
                timing,
                ct,
                cancelImmediately);
    }
}
