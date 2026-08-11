using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyExtensions.Scenes
{
    /// <summary>
    /// Activation待ちSceneの所有権を表すHandle。
    ///
    /// PreloadAsync()でロードしたSceneについて、
    /// 使用するか破棄するかを一度だけ選択するためのオブジェクト。
    ///
    /// Ready状態のHandleは必ず
    /// ActivateAsync() または DiscardAsync()
    /// のどちらかでconsumeすること。
    /// </summary>
    public sealed class ScenePreloadHandle
    {
        // 実際のActivation / Discard処理を行うSceneLoader。
        private readonly SceneLoader _loader;

        // このHandleが所有しているPreload済みScene。
        private readonly Scene _scene;

        // SceneManager.LoadSceneAsyncによって作成されたOperation。
        // Preload中はallowSceneActivation=falseで停止しており、
        // ActivateまたはDiscard時に再開する。
        private readonly AsyncOperation _operation;

        /// <summary>
        /// このHandleの現在状態。
        ///
        /// 状態によって二重Activateや、
        /// Activate後のDiscardなどを防止する。
        /// </summary>
        public ScenePreloadState State { get; private set; }

        /// <summary>
        /// このHandleが所有しているScene。
        ///
        /// Ready状態ではActivation前なので
        /// Scene.isLoadedはfalseの場合がある。
        /// </summary>
        public Scene Scene => _scene;

        /// <summary>
        /// ActivateまたはDiscardをまだ実行していないか。
        /// </summary>
        public bool IsReady => State == ScenePreloadState.Ready;

        /// <summary>
        /// Preloadの進捗。
        ///
        /// UnityのSceneロードはActivation待ちでは0.9で止まるため、
        /// 0.9をPreload完了=1.0として正規化する。
        /// </summary>
        public float Progress => SceneLoader.NormalizeProgress(_operation.progress);

        /// <summary>
        /// SceneLoader.PreloadAsync()からのみ生成する。
        /// </summary>
        internal ScenePreloadHandle(
            SceneLoader loader,
            Scene scene,
            AsyncOperation operation)
        {
            _loader = loader;
            _scene = scene;
            _operation = operation;

            // 生成された時点でPreloadは完了しており、
            // ActivateまたはDiscardを選択できる。
            State = ScenePreloadState.Ready;
        }

        /// <summary>
        /// Preload済みSceneのActivationを許可し、
        /// 通常のロード済みSceneとして使用可能にする。
        /// </summary>
        public async UniTask<Scene> ActivateAsync(
            bool setActiveScene = true)
        {
            // Handleは一度だけconsumeできる。
            EnsureReady();

            // await中も二重操作されないよう、
            // 処理開始前に状態を変更する。
            State = ScenePreloadState.Activating;

            try
            {
                Scene scene =
                    await _loader.ActivatePreloadedAsync(
                        _scene,
                        _operation,
                        setActiveScene);

                State = ScenePreloadState.Activated;

                return scene;
            }
            catch
            {
                // Activationまたはそのrollbackに失敗したことを表す。
                State = ScenePreloadState.Faulted;
                throw;
            }
        }

        /// <summary>
        /// Preload済みSceneを使用せず破棄する。
        /// </summary>
        public async UniTask DiscardAsync()
        {
            // Activate済みなどのHandleを再利用できないよう確認する。
            EnsureReady();

            State = ScenePreloadState.Discarding;

            try
            {
                await _loader.DiscardPreloadedAsync(
                    _scene,
                    _operation);

                State = ScenePreloadState.Discarded;
            }
            catch
            {
                State = ScenePreloadState.Faulted;
                throw;
            }
        }

        /// <summary>
        /// Handleがまだconsumeされていないことを確認する。
        /// </summary>
        private void EnsureReady()
        {
            if (State != ScenePreloadState.Ready)
            {
                throw new InvalidOperationException(
                    $"Preload handle has already been consumed. State: {State}");
            }
        }
    }
}
