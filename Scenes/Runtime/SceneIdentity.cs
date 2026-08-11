using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace MyExtensions.Scenes
{
    /// <summary>
    /// SceneManager上のSceneをhandleで識別するための補助クラス。
    ///
    /// ロード前後のScene一覧を比較することで、
    /// 今回のLoadSceneAsyncによって新しく作られたSceneを特定する。
    /// </summary>
    internal static class SceneIdentity
    {
        /// <summary>
        /// 現在SceneManagerに存在する全Sceneのhandleを保存する。
        ///
        /// Sceneロード前に呼び出しておき、
        /// ロード後との差分を調べるために使用する。
        /// </summary>
        internal static HashSet<SceneHandle> CaptureHandles()
        {
            var handles = new HashSet<SceneHandle>();

            // 現在SceneManagerが管理しているSceneをすべて走査する。
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                handles.Add(SceneManager.GetSceneAt(i).handle);
            }

            return handles;
        }

        /// <summary>
        /// CaptureHandles()で記録した時点には存在しなかったSceneを探す。
        ///
        /// 新規Sceneがちょうど1つだけ存在することを前提とし、
        /// 0個または複数個の場合はSceneを安全に特定できないため例外にする。
        /// </summary>
        internal static Scene FindCreatedScene(HashSet<SceneHandle> before)
        {
            if (before == null)
            {
                throw new ArgumentNullException(nameof(before));
            }

            int foundCount =
                FindCreatedSceneCandidate(
                    before,
                    out Scene result);

            // 今回増えたSceneが1つだけのときのみ確定できる。
            return foundCount switch
            {
                1 => result,

                0 => throw new InvalidOperationException(
                    "The scene created by the load operation could not be identified."),

                // SceneLoader以外から同時にScene操作された場合などは、
                // どのSceneが今回ロードしたものか判別できない。
                _ => throw new InvalidOperationException(
                    "Multiple new scenes were detected. " +
                    "Another SceneManager operation may have run concurrently."),
            };
        }

        /// <summary>
        /// 新しく作られたSceneを例外なしで特定する。
        ///
        /// rollback処理などで特定できないこと自体はあり得る場所で使用する。
        /// </summary>
        internal static bool TryFindCreatedScene(
            HashSet<SceneHandle> before,
            out Scene result)
        {
            result = default;

            if (before == null)
            {
                return false;
            }

            int foundCount =
                FindCreatedSceneCandidate(
                    before,
                    out result);

            // 新規Sceneが1つだけなら特定できる。
            if (foundCount == 1)
            {
                return true;
            }

            // 0個または複数個の場合、
            // 誤ったSceneを返さないようdefaultに戻す。
            result = default;
            return false;
        }

        /// <summary>
        /// ロード前には存在しなかったSceneを走査し、
        /// 検出数と候補Sceneを返す。
        /// </summary>
        private static int FindCreatedSceneCandidate(
            HashSet<SceneHandle> before,
            out Scene result)
        {
            result = default;
            int foundCount = 0;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                // ロード前から存在していたSceneは対象外。
                if (before.Contains(scene.handle)) continue;

                // ロード後に増えたSceneを記録する。
                result = scene;
                foundCount++;
            }

            return foundCount;
        }
    }
}
