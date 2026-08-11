namespace MyExtensions.Scenes
{
    /// <summary>
    /// ScenePreloadHandleのライフサイクルを表す。
    ///
    /// 基本的な遷移は
    /// Ready → Activating → Activated
    /// または
    /// Ready → Discarding → Discarded
    /// となる。
    /// Activation / Discard中に例外が発生した場合はFaultedへ遷移する。
    /// </summary>
    public enum ScenePreloadState
    {
        /// <summary>
        /// Preload完了。ActivateまたはDiscardを選択できる状態。
        /// </summary>
        Ready,

        /// <summary>
        /// Activation処理中。
        /// </summary>
        Activating,

        /// <summary>
        /// Activationが正常完了した状態。
        /// </summary>
        Activated,

        /// <summary>
        /// PreloadしたSceneの破棄処理中。
        /// </summary>
        Discarding,

        /// <summary>
        /// Sceneの破棄が正常完了した状態。
        /// </summary>
        Discarded,

        /// <summary>
        /// ActivateまたはDiscard処理中に回復できない例外が発生した状態。
        /// </summary>
        Faulted,
    }
}