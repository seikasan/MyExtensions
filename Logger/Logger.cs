using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

// using なしで使えるように名前空間は敢えて付けません。
public static class Logger
{
    /// <summary>
    /// 通常のログメッセージを出力します。
    /// Debug.Log() 相当。
    /// リリースビルド時に負荷軽減のため消滅します。
    /// </summary>
    /// <param name="source">
    /// ログの発生元です。
    /// Unityオブジェクトの場合は、オブジェクト名や型名の表示、およびログのコンテキストとして使用されます。
    /// </param>
    /// <param name="message">出力するメッセージです。</param>
    [HideInCallstack]
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object source, object message)
    {
        // "[name] piyopiyo" みたいになる。
        string text = $"{GetPrefix(source)} {message}";

        // source が UnityEngine.Object の場合は、ログに source 情報を結びつける。
        var context = source as Object;

        Debug.Log(text, context);
    }

    /// <summary>
    /// 警告ログメッセージを出力します。
    /// Debug.LogWarning() 相当。
    /// リリースビルド時に負荷軽減のため消滅します。
    /// </summary>
    /// <param name="source">
    /// ログの発生元です。
    /// Unityオブジェクトの場合は、オブジェクト名や型名の表示、およびログのコンテキストとして使用されます。
    /// </param>
    /// <param name="message">出力するメッセージです。</param>
    [HideInCallstack]
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(object source, object message)
    {
        string text = $"{GetPrefix(source)} {message}";
        var context = source as Object;
        Debug.LogWarning(text, context);
    }

    /// <summary>
    /// エラーログメッセージを出力します。
    /// Debug.LogError() 相当。
    /// リリースビルドでも残ります。
    /// </summary>
    /// <param name="source">
    /// ログの発生元です。
    /// Unityオブジェクトの場合は、オブジェクト名や型名の表示、およびログのコンテキストとして使用されます。
    /// </param>
    /// <param name="message">出力するメッセージです。</param>
    [HideInCallstack]
    public static void LogError(object source, object message)
    {
        string text = $"{GetPrefix(source)} {message}";
        var context = source as Object;
        Debug.LogError(text, context);
    }

    private static string GetPrefix(object source)
    {
        // UnityEngine.Object の特殊 null はここでは弾かれない。
        if (source is null)
        {
            return "[null]";
        }

        if (source is Object unityObject)
        {
            // null 判定で既に破壊されたとする。
            if (unityObject == null)
            {
                return "[destroyed]";
            }

            if (unityObject is Component component)
            {
                return $"[{component.gameObject.name}/{component.GetType().Name}]";
            }

            if (unityObject is GameObject gameObject)
            {
                return $"[{gameObject.name}/GameObject]";
            }

            return $"[{unityObject.name}/{unityObject.GetType().Name}]";
        }

        return $"[{source.GetType().Name}]";
    }
}
