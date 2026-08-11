using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace MyExtensions.Scenes
{
    /// <summary>
    /// Sceneのロード、Preload、アンロードを行うサービス。
    /// </summary>
    public interface ISceneLoader
    {
        /// <summary>
        /// Scene操作中かどうか。
        ///
        /// Preload成功後は、ActivateAsync() または DiscardAsync() が完了するまでtrue。
        /// </summary>
        bool IsBusy { get; }

        /// <summary>
        /// Sceneをロードし、完了したSceneを返す。
        /// </summary>
        /// <param name="sceneReference">
        /// ロードするScene。
        /// </param>
        /// <param name="mode">
        /// Sceneのロードモード。
        /// </param>
        /// <param name="progress">
        /// ロード進捗を0.0～1.0で通知する。
        /// 1.0はSceneロードが完了した時点で通知される。
        /// </param>
        /// <param name="setActiveScene">
        /// Additiveロード時に、ロードしたSceneをActive Sceneにするか。
        /// </param>
        UniTask<Scene> LoadAsync(
            SceneReference sceneReference,
            LoadSceneMode mode = LoadSceneMode.Single,
            IProgress<float> progress = null,
            bool setActiveScene = true);

        /// <summary>
        /// Additive SceneをActivation直前まで先読みする。
        ///
        /// 返されたHandleは必ず ActivateAsync() または DiscardAsync() のどちらかでconsumeすること。
        /// </summary>
        /// <param name="sceneReference">
        /// PreloadするScene。
        /// </param>
        /// <param name="progress">
        /// Preload進捗を0.0～1.0で通知する。
        /// 1.0はSceneがActivation可能な状態までPreloadされた時点で通知される。
        /// </param>
        UniTask<ScenePreloadHandle> PreloadAsync(
            SceneReference sceneReference,
            IProgress<float> progress = null);

        /// <summary>
        /// ロード済みのSceneをアンロードする。
        /// </summary>
        /// <param name="scene">
        /// アンロードするScene。
        /// </param>
        UniTask UnloadAsync(Scene scene);
    }
}