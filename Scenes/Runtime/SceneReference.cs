using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyExtensions.Scenes
{
    /// <summary>
    /// Sceneへの参照をScriptableObjectとして保持する。
    ///
    /// Editor上ではSceneAssetを指定し、
    /// 実行時にはそのSceneのフルパスを使用する。
    /// Scene名だけでは同名Sceneを区別できないため、
    /// フルパスを実行時データとして保持する。
    /// </summary>
    [CreateAssetMenu(
        fileName = "New SceneReference",
        menuName = "Scenes/Scene Reference")]
    public sealed class SceneReference : ScriptableObject
    {
        // Build時に実際に使用する値。
        // 同名Scene対策として、名前ではなくフルパスを保存する。
        [SerializeField, HideInInspector]
        private string _scenePath = string.Empty;

#if UNITY_EDITOR
        // SceneAssetはUnityEditor型なので、ビルドには含めない。
        [SerializeField]
        private UnityEditor.SceneAsset _sceneAsset;
#endif

        /// <summary>
        /// Sceneのプロジェクト内フルパス。
        /// </summary>
        public string ScenePath => _scenePath;

        /// <summary>
        /// 拡張子とディレクトリを除いたScene名。
        ///
        /// 表示用途などで使用する。
        /// Sceneの識別そのものにはScenePathを使用する。
        /// </summary>
        public string SceneName =>
            string.IsNullOrWhiteSpace(_scenePath)
                ? string.Empty
                : System.IO.Path.GetFileNameWithoutExtension(
                    _scenePath);

        /// <summary>
        /// SceneがこのReferenceに割り当てられているか。
        /// </summary>
        public bool IsAssigned =>
            !string.IsNullOrWhiteSpace(_scenePath);

        /// <summary>
        /// 現在のBuild SettingsにおけるSceneのBuildIndex。
        /// SceneがBuild Settingsに存在しない場合は-1。
        /// </summary>
        public int BuildIndex =>
            IsAssigned
                ? SceneUtility.GetBuildIndexByScenePath(
                    _scenePath)
                : -1;

        /// <summary>
        /// Sceneが現在のBuild Settingsに含まれているか。
        /// </summary>
        public bool IsInBuild =>
            BuildIndex >= 0;

        /// <summary>
        /// ログやInspectorなどで扱いやすい文字列表現を返す。
        /// </summary>
        public override string ToString()
        {
            return IsAssigned
                ? SceneName
                : $"{name} (Unassigned)";
        }

#if UNITY_EDITOR
        /// <summary>
        /// SceneAssetから実行時用パスを再生成する。
        /// Editorコードからのみ呼び出す。
        /// </summary>
        public bool EditorSync()
        {
            // SceneAssetが未指定なら空文字列、
            // 指定済みならAssetDatabaseからパスを取得する。
            string newPath = _sceneAsset == null
                ? string.Empty
                : UnityEditor.AssetDatabase.GetAssetPath(
                    _sceneAsset);

            // 変更がなければAssetをDirtyにする必要はない。
            if (_scenePath == newPath)
            {
                return false;
            }

            _scenePath = newPath;
            return true;
        }

        /// <summary>
        /// Inspector上で値が変更されたときに呼ばれる。
        /// </summary>
        private void OnValidate()
        {
            // delayCallへ同じ処理が重複登録されないよう、
            // 一度解除してから登録する。
            // OnValidate中にAssetDatabase関連の変更を直接行わず、
            // 次のEditor更新タイミングへ処理を遅延させる。
            UnityEditor.EditorApplication.delayCall -= SyncFromAssetDelayed;
            UnityEditor.EditorApplication.delayCall += SyncFromAssetDelayed;
        }

        /// <summary>
        /// OnValidateから遅延実行され、
        /// SceneAssetと保存済みScenePathを同期する。
        /// </summary>
        private void SyncFromAssetDelayed()
        {
            // delayCall実行までの間に
            // このScriptableObjectが削除されている可能性がある。
            if (this == null) return;

            // SceneAssetから生成したパスが変化した場合だけ、
            // UnityへAssetが変更されたことを通知する。
            if (EditorSync())
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}
