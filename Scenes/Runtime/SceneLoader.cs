using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyExtensions.Scenes
{
    /// <summary>
    /// SceneManagerを使ったScene操作の低レベルサービス。
    ///
    /// アプリケーション内では単一インスタンスとして扱い、
    /// SceneManager.LoadSceneAsync / UnloadSceneAsync を
    /// 他の場所から直接実行しないことを前提とする。
    ///
    /// Scene操作をここへ集約することで、
    /// 同時ロードやScene特定の競合を防止する。
    /// </summary>
    public sealed class SceneLoader : ISceneLoader
    {
        // Unityの仕様上、Progressが0.9に到達したときロードが完了したとみなす。
        private const float ActivationReadyProgress = 0.9f;

        // Scene操作中かどうか。
        // SceneIdentityはロード前後の差分からSceneを特定するため、
        // 複数のScene操作を同時実行させないことが重要。
        private bool _isBusy;

        /// <summary>
        /// SceneLoaderがScene操作の所有権を持っているか。
        ///
        /// Preload成功後も、ActivateまたはDiscardが終わるまではtrue。
        /// </summary>
        public bool IsBusy => _isBusy;

        /// <summary>
        /// Sceneをロードし、完了したSceneを返す。
        /// </summary>
        public async UniTask<Scene> LoadAsync(
            SceneReference sceneReference,
            LoadSceneMode mode = LoadSceneMode.Single,
            IProgress<float> progress = null,
            bool setActiveScene = true)
        {
            // SceneReferenceが有効か確認し、
            // SceneManagerに渡せるBuildIndexを取得する。
            int buildIndex =
                GetBuildIndex(sceneReference);

            // エラー表示用として先にパスを保持しておく。
            string scenePath =
                sceneReference.ScenePath;

            // 他のScene操作との同時実行を禁止する。
            BeginOperation();

            HashSet<SceneHandle> before = null;
            AsyncOperation operation = null;
            Scene loadedScene = default;

            try
            {
                // ロード前に存在していたSceneを記録する。
                // ロード完了後、この一覧との差分から新しいSceneを特定する。
                before = SceneIdentity.CaptureHandles();

                // Unity標準の非同期Sceneロードを開始する。
                operation = SceneManager.LoadSceneAsync(
                    buildIndex,
                    mode);

                // 通常は返ってくるが、
                // 開始自体に失敗した場合を明示的に扱う。
                if (operation == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to start scene load: {scenePath}");
                }

                // Progress通知はSceneロード本体と分離して監視する。
                // Progress側の例外によってSceneロードを失敗させない。
                if (progress != null)
                {
                    ObserveProgressAsync(
                        operation,
                        progress).Forget();
                }

                // AsyncOperationを直接awaitし、
                // Unityネイティブの復帰タイミングを維持する。
                await operation;

                // 完了時は必ず100%を通知する。
                SafeReport(progress, 1f);

                // awaitを挟まず、今回生成されたSceneを確定する。
                // ここで別のScene操作が走ると差分判定が壊れるため、
                // SceneLoaderでは同時操作を禁止している。
                loadedScene = SceneIdentity.FindCreatedScene(before);

                // Additiveロードでは、必要なら今回ロードしたSceneを
                // Active Sceneとして設定する。
                // Singleの場合はロードされたSceneが自動的にActiveになるため、
                // ここではAdditiveの場合だけ明示設定する。
                if (setActiveScene &&
                    mode == LoadSceneMode.Additive &&
                    !SceneManager.SetActiveScene(loadedScene))
                {
                    throw new InvalidOperationException(
                        $"Failed to set active scene: {scenePath}");
                }

                return loadedScene;
            }
            catch (Exception loadException)
            {
                // Singleロードは旧Sceneが既に破棄されているため、
                // 一般的なrollbackはできない。
                // また、LoadSceneAsyncの開始自体に失敗している場合も
                // rollback対象が存在しないため、そのまま例外を返す。
                if (mode != LoadSceneMode.Additive ||
                    operation == null)
                {
                    throw;
                }

                Exception rollbackException = null;

                try
                {
                    // Sceneの特定処理より前に例外が発生した場合でも、
                    // LoadSceneAsync自体が完了済みなら、
                    // handle差分からロード済みSceneを回収できる可能性がある。
                    if ((!loadedScene.IsValid() ||
                         !loadedScene.isLoaded) &&
                        operation.isDone &&
                        SceneIdentity.TryFindCreatedScene(
                            before,
                            out Scene identifiedScene))
                    {
                        loadedScene = identifiedScene;
                    }

                    // Additiveロードに途中まで成功していた場合は、
                    // 不要なSceneを残さないようアンロードする。
                    await UnloadIfLoadedAsync(loadedScene);
                }
                catch (Exception ex)
                {
                    // 本来のロード失敗とは別に、
                    // rollback自体も失敗したことを記録する。
                    rollbackException = ex;
                }

                // 元のロード失敗とrollback失敗の両方を失わないよう、
                // AggregateExceptionとしてまとめて通知する。
                if (rollbackException != null)
                {
                    throw new AggregateException(
                        $"Scene load and rollback both failed: {scenePath}",
                        loadException,
                        rollbackException);
                }

                // rollbackに成功した場合は、
                // 元々発生したロード側の例外をそのまま再送出する。
                throw;
            }
            finally
            {
                // 成功・失敗のどちらでも通常ロードの所有権を解放する。
                EndOperation();
            }
        }

        /// <summary>
        /// Additive SceneをActivation直前まで先読みする。
        ///
        /// UnityのSceneロードはallowSceneActivation=falseの場合、
        /// progress=0.9付近で停止する。
        ///
        /// 返されたHandleは必ず
        /// ActivateAsync() または DiscardAsync()
        /// のどちらかでconsumeすること。
        /// </summary>
        public async UniTask<ScenePreloadHandle> PreloadAsync(
            SceneReference sceneReference,
            IProgress<float> progress = null)
        {
            int buildIndex =
                GetBuildIndex(sceneReference);

            string scenePath =
                sceneReference.ScenePath;

            // Preload開始からActivate/Discard完了まで、
            // SceneLoaderを他のScene操作から占有する。
            BeginOperation();

            AsyncOperation operation = null;
            HashSet<SceneHandle> before = null;

            try
            {
                // Preloadによって追加されるSceneを識別するため、
                // ロード前のScene一覧を保存する。
                before = SceneIdentity.CaptureHandles();

                // Preloadは既存Sceneを残す必要があるため、
                // 必ずAdditiveでロードする。
                operation = SceneManager.LoadSceneAsync(
                    buildIndex,
                    LoadSceneMode.Additive);

                if (operation == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to start scene preload: {scenePath}");
                }

                // SceneのActivationを止め、
                // データロードだけを進める。
                operation.allowSceneActivation = false;

                // UnityのSceneロードはActivationを止めている場合、
                // progressが0.9に到達すると待機状態になる。
                while (operation.progress < ActivationReadyProgress)
                {
                    // Unityの0.0～0.9を、
                    // 利用側には0.0～1.0として通知する。
                    SafeReport(
                        progress,
                        NormalizeProgress(operation.progress));

                    await UniTask.NextFrame();
                }

                // Activation待ちまで到達した時点を
                // Preloadとしての100%とみなす。
                SafeReport(progress, 1f);

                // Activation前でもSceneManager上に現れた
                // SceneHandle差分から今回のSceneを特定する。
                Scene scene =
                    SceneIdentity.FindCreatedScene(before);

                // ここではEndOperation()を呼ばない。
                // SceneはまだActivation待ちであり、
                // この時点で別のSceneManager操作を許可すると、
                // SceneIdentityによるScene特定やUnityのロードキューに
                // 影響する可能性がある。
                // ここからScenePreloadHandleが所有権を引き継ぎ、
                // ActivateAsync()またはDiscardAsync()の完了時に
                // SceneLoaderの_busyを解除する。
                return new ScenePreloadHandle(
                    this,
                    scene,
                    operation);
            }
            catch (Exception preloadException)
            {
                Exception rollbackException = null;

                try
                {
                    if (operation != null)
                    {
                        // Preload途中で失敗した場合、
                        // LoadSceneAsyncを中途半端な状態で残さない。
                        await RollbackPreloadAsync(
                            before,
                            operation);
                    }
                }
                catch (Exception ex)
                {
                    rollbackException = ex;
                }
                finally
                {
                    // Handleへ所有権を渡せなかったため、
                    // SceneLoader自身がここで占有状態を解除する。
                    EndOperation();
                }

                if (rollbackException != null)
                {
                    throw new AggregateException(
                        $"Scene preload and rollback both failed: {scenePath}",
                        preloadException,
                        rollbackException);
                }

                throw;
            }
        }

        /// <summary>
        /// ロード済みのSceneをアンロードする。
        /// </summary>
        public async UniTask UnloadAsync(Scene scene)
        {
            BeginOperation();

            try
            {
                await UnloadIfLoadedAsync(scene);
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>
        /// Preload済みSceneのActivationを再開する。
        ///
        /// ScenePreloadHandleからのみ呼び出す内部処理。
        /// </summary>
        internal async UniTask<Scene> ActivatePreloadedAsync(
            Scene scene,
            AsyncOperation operation,
            bool setActiveScene)
        {
            try
            {
                // PreloadAsyncで停止させていたActivationを許可する。
                operation.allowSceneActivation = true;

                // Activationを含めたLoadSceneAsync全体の完了を待つ。
                await operation;

                // Operationが完了してもSceneが正しくロードされていなければ
                // 正常なActivationとはみなさない。
                if (!scene.IsValid() ||
                    !scene.isLoaded)
                {
                    throw new InvalidOperationException(
                        "The preloaded scene did not complete loading correctly.");
                }

                // 必要ならActivation完了後のSceneをActive Sceneにする。
                if (setActiveScene &&
                    !SceneManager.SetActiveScene(scene))
                {
                    throw new InvalidOperationException(
                        $"Failed to set active scene: {scene.path}");
                }

                return scene;
            }
            catch (Exception activationException)
            {
                Exception rollbackException = null;

                try
                {
                    // Activationに失敗したSceneを残さないよう回収する。
                    await RollbackKnownPreloadedSceneAsync(
                        scene,
                        operation);
                }
                catch (Exception ex)
                {
                    rollbackException = ex;
                }

                if (rollbackException != null)
                {
                    throw new AggregateException(
                        "Scene activation and rollback both failed.",
                        activationException,
                        rollbackException);
                }

                throw;
            }
            finally
            {
                // PreloadAsyncから維持していたSceneLoaderの所有権を
                // Activation終了時にここで解放する。
                EndOperation();
            }
        }

        /// <summary>
        /// PreloadしたSceneを使用せず破棄する。
        ///
        /// ScenePreloadHandleからのみ呼び出す内部処理。
        /// </summary>
        internal async UniTask DiscardPreloadedAsync(
            Scene scene,
            AsyncOperation operation)
        {
            try
            {
                // UnityのLoadSceneAsyncは途中キャンセルできないため、
                // 必要なら一度ロードを完了させてからアンロードする。
                await RollbackKnownPreloadedSceneAsync(
                    scene,
                    operation);
            }
            finally
            {
                 // PreloadAsyncから維持していた所有権を解放する。
                EndOperation();
            }
        }

        /// <summary>
        /// SceneReferenceを検証し、
        /// SceneManagerで使用するBuildIndexを取得する。
        /// </summary>
        private static int GetBuildIndex(
            SceneReference sceneReference)
        {
            if (sceneReference == null)
            {
                throw new ArgumentNullException(
                    nameof(sceneReference));
            }

            // SceneReference自体は存在していても、
            // SceneAssetが設定されていない状態を拒否する。
            if (!sceneReference.IsAssigned)
            {
                throw new InvalidOperationException(
                    $"SceneReference '{sceneReference.name}' has no scene assigned.");
            }

            int buildIndex =
                sceneReference.BuildIndex;

            // Sceneファイルが存在していても、
            // Build Settingsに含まれていなければ実行時ロードできない。
            if (buildIndex < 0)
            {
                throw new InvalidOperationException(
                    $"Scene is not included in the current build: {sceneReference.ScenePath}");
            }

            return buildIndex;
        }

        /// <summary>
        /// 特定済みのPreload Sceneを安全に破棄する。
        /// </summary>
        private static async UniTask RollbackKnownPreloadedSceneAsync(
            Scene scene,
            AsyncOperation operation)
        {
            await CompleteLoadOperationAsync(operation);
            await UnloadIfLoadedAsync(scene);
        }

        /// <summary>
        /// Preload開始途中で失敗した場合のrollback。
        ///
        /// この時点ではSceneをまだ確定できていない可能性があるため、
        /// ロード前後のhandle差分からSceneを探す。
        /// </summary>
        private static async UniTask RollbackPreloadAsync(
            HashSet<SceneHandle> before,
            AsyncOperation operation)
        {
            // allowSceneActivation=falseのままLoadSceneAsyncを残さないため、
            // 必ずロード処理そのものを完了させる。
            await CompleteLoadOperationAsync(operation);

            if (before == null) return;

            if (!SceneIdentity.TryFindCreatedScene(
                    before,
                    out Scene scene))
            {
                throw new InvalidOperationException(
                    "The loaded scene could not be uniquely identified during rollback.");
            }

            await UnloadIfLoadedAsync(scene);
        }

        /// <summary>
        /// SceneManager.UnloadSceneAsyncを実行する共通処理。
        ///
        /// _isBusyの管理は呼び出し元が行う。
        /// </summary>
        private static async UniTask UnloadSceneCoreAsync(
            Scene scene)
        {
            AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);

            if (operation == null)
            {
                throw new InvalidOperationException(
                    $"Failed to start scene unload: {scene.path}");
            }

            await operation;
        }

        /// <summary>
        /// AsyncOperationの進捗を毎フレーム監視して通知する。
        ///
        /// この監視処理の失敗はSceneロード本体へ伝播させない。
        /// </summary>
        private static async UniTask ObserveProgressAsync(
            AsyncOperation operation,
            IProgress<float> progress)
        {
            try
            {
                while (!operation.isDone)
                {
                    float normalizedProgress =
                        NormalizeProgress(operation.progress);

                    // 1.0はSceneロード完了後にのみ通知する。
                    if (normalizedProgress < 1f)
                    {
                        SafeReport(
                            progress,
                            normalizedProgress);
                    }

                    await UniTask.NextFrame();
                }
            }
            catch (Exception ex)
            {
                // Progress consumerや監視処理の失敗を
                // Sceneロード本体の成否から分離する。
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Activation待ちのLoadSceneAsyncを必要なら再開し、
        /// Operationそのものを完了させる。
        /// </summary>
        private static async UniTask CompleteLoadOperationAsync(
            AsyncOperation operation)
        {
            if (operation.isDone) return;

            operation.allowSceneActivation = true;
            await operation;
        }

        /// <summary>
        /// Sceneが有効かつロード済みの場合のみアンロードする。
        /// </summary>
        private static async UniTask UnloadIfLoadedAsync(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            await UnloadSceneCoreAsync(scene);
        }

        /// <summary>
        /// Progress.Report()を安全に呼び出す。
        ///
        /// UI側などのProgress consumerが例外を投げても、
        /// Scene操作そのものは継続する。
        /// </summary>
        private static void SafeReport(
            IProgress<float> progress,
            float value)
        {
            if (progress == null) return;

            try
            {
                progress.Report(value);
            }
            catch (Exception ex)
            {
                // UI等のProgress consumerの失敗によって
                // Scene操作そのものを失敗させない。
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// UnityのSceneロード進捗0.0～0.9を、
        /// 利用側向けの0.0～1.0へ変換する。
        /// </summary>
        internal static float NormalizeProgress(float progress) =>
            Mathf.Clamp01(progress / ActivationReadyProgress);

        /// <summary>
        /// Scene操作の開始を宣言する。
        ///
        /// すでに別操作中なら、Scene特定の競合を避けるため例外にする。
        /// </summary>
        private void BeginOperation()
        {
            if (_isBusy)
            {
                throw new InvalidOperationException(
                    "SceneLoader is already processing another operation.");
            }

            _isBusy = true;
        }

        /// <summary>
        /// Scene操作の所有権を解放する。
        /// </summary>
        private void EndOperation()
        {
            _isBusy = false;
        }
    }
}
