# MyExtensions

自分用の拡張メソッド集。気分で更新していきます。

## R3

MonoBehaviour 以外でも `AddTo(this)` したいので `AddTo(this)` できるようにしました。

`DisposableObject` を継承することで、pure C# でも `AddTo(this)` が使えます。

### 導入方法

1. Window > Package ManagerからPackage Managerを開く
2. 「+」ボタン > Add package from git URL
3. 以下のURLを入力する

```
https://github.com/seikasan/MyExtensions.git?path=R3
```

## InputSystem.R3

入力イベントと継続値を Observable に変換したり、入力バッファに溜めたりできます。

`PerformedAsObservable()` は、Input Action の `performed` が発生した瞬間だけ通知します。ジャンプや決定などの単発入力に使用します。押している間の連続処理や移動値の継続取得には、ポーリング用の API を使用してください。

```csharp
// 押した瞬間に一度だけ通知
jumpAction
    .PerformedAsObservable()
    .Subscribe(_ => Jump());

// 押している間、Input System の更新ごとに通知
fireAction
    .WhilePressedAsObservable()
    .Subscribe(_ => Fire());

// 同じ値を保持している間も、現在値を継続して通知
moveAction
    .ReadValueAsObservable<Vector2>()
    .Subscribe(Move);

// 押下状態を継続して通知。状態変化だけなら DistinctUntilChanged を追加
guardAction
    .IsPressedAsObservable()
    .DistinctUntilChanged()
    .Subscribe(SetGuarding);
```

### 導入方法

1. Window > Package ManagerからPackage Managerを開く
2. 「+」ボタン > Add package from git URL
3. 以下のURLを入力する

```
https://github.com/seikasan/MyExtensions.git?path=InputSystem.R3
```

## Scenes

Scene を string で管理したくなかったので、SceneReference を使って Scene を管理します。

`Create -> Scenes -> Scene Reference` から ScriptableObject を作り、それを `SceneReference` として使用します。

VContainer 等で `ISceneLoader` として `SceneLoader` を受け取ると、`LoadAsync()` や `UnloadAsync()` などの非同期メソッドを使用できます。

普通に作ることもできる。

```csharp
ISceneLoader sceneLoader;

bool isBusy = sceneLoader.IsBusy;

Scene scene = await sceneLoader.LoadAsync(sceneReference);
await sceneLoader.UnloadAsync(scene);

// handle
ScenePreloadHandle handle = await sceneLoader.PreloadAsync(sceneReference);

ScenePreloadState state = handle.State;
Scene scene = handle.Scene;
bool isReady = handle.IsReady;
float progress = handle.Progress;

// どっちか
Scene scene = await handle.ActivateAsync();
await handle.DiscardAsync();
```

### 導入方法

1. Window > Package ManagerからPackage Managerを開く
2. 「+」ボタン > Add package from git URL
3. 以下のURLを入力する

```
https://github.com/seikasan/MyExtensions.git?path=Scenes
```

## Logger

使い方↓

```
// "[クラス名] Debug.Log() 相当" と出る。
Logger.Log(this, "Debug.Log() 相当");

// "[クラス名] Debug.LogWarning() 相当" と出る。
Logger.LogWarning(this, "Debug.LogWarning() 相当");

// "[クラス名] Debug.LogError() 相当" と出る。
Logger.LogError(this, "Debug.LogError() 相当");
```

### 導入方法

コピペしてください。
