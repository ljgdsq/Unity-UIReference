# Unity View Analyzers

检查 `BaseView` 派生类的 Unity 生命周期方法重写是否正确（是否调用了 `base.xxx()`）。

## 规则

| 规则 ID | 严重级别 | 说明 |
| --- | --- | --- |
| `UIVIEW0002` | Warning | 派生类声明了与基类同名的生命周期方法但没有 `override`，基类实现不会执行 |
| `UIVIEW0001` | Warning | 使用了 `override` 重写生命周期方法，但方法体内没有调用 `base.xxx()` |

覆盖的 Unity 生命周期方法：`Awake`、`OnEnable`、`Start`、`FixedUpdate`、`Update`、`LateUpdate`、
`OnDisable`、`OnDestroy`、`OnApplicationQuit/Pause/Focus`、`OnValidate`、`Reset`、`OnDrawGizmos`、
`OnGUI`、碰撞/触发、鼠标、粒子、渲染相关回调等。

`BaseView` 自身以及 `abstract`/`extern`/没有方法体的声明不会被检查。

## 豁免：故意的写法

有些方法就是故意不调用父类，用 `[SkipBaseCall]` 标记即可关闭这两条规则：

```csharp
public class LoginView : BaseView
{
    // 方法级：只豁免这一个方法
    [SkipBaseCall("登录界面不需要 ReferenceHolder 绑定")]
    protected override void Awake()
    {
        Debug.Log("LoginView Awake");
    }
}

// 类型级：整个类的所有生命周期方法都豁免
[SkipBaseCall]
public class PopupView : BaseView
{
    protected override void Awake() { }
    private void Update() { }
}
```

设计约定：

- **不继承**（`Inherited = false`）。`[SkipBaseCall]` 只对标记它的那个方法/类型生效，
  派生类再次重写时要自己再标一次——避免警告在继承链上被静默吞掉。
- 可带一个 `reason` 字符串，纯文档用途，分析器不会用它做判断。
- attribute 类型名可用 `unity_view_analyzer_skip_attribute` 换成你自己的（见下文）。

也可以使用 Roslyn 原生的 `[SuppressMessage]`，不需要改分析器：

```csharp
[SuppressMessage("Unity", "UIVIEW0001", Justification = "故意跳过")]
protected override void Awake() { }
```

`Category`（构造 descriptor 时传的 `"Unity"`）和规则 ID 必须与这里一致。它一条只能关一个规则，
写法也更长，所以日常推荐 `[SkipBaseCall]`。

## 配置项

| 选项 | 默认值 | 说明 |
| --- | --- | --- |
| `unity_view_analyzer_base_type` | `BaseView` | 要检查的基类名，多个用 `;` 或 `,` 分隔（写全名或 `ToDisplayString()` 结果都可以） |
| `unity_view_analyzer_skip_attribute` | `SkipBaseCallAttribute` | 豁免用的 attribute 名，多个用 `;` 或 `,` 分隔 |

在项目的 `.globalconfig` 或 `.editorconfig` 中配置：

```ini
[/Assets/**/*.cs]
unity_view_analyzer_base_type = ViewBase;BaseView
```

**什么时候用 attribute，什么时候用配置：**

- 单个方法/类故意不调父类 → 用 `[SkipBaseCall]`。例外是逐处的事实，配置无法表达，
  而且写在使用处能自带理由，方便 review。
- 全局基类名、全项目禁用某条规则 → 用配置 / `.editorconfig` 的 `dotnet_diagnostic.UIVIEW0001.severity = none`。

## 构建

分析器只需要在修改源码后重新构建，Unity 会自动重新导入 DLL：

```powershell
cd Tools\UnityViewAnalyzers
dotnet build -c Release
```

构建输出会自动复制到 `Assets/Plugins/RoslynAnalyzers/UnityViewAnalyzers.dll`。

注意事项：

- 目标框架必须是 `netstandard2.0`，且 `Microsoft.CodeAnalysis` 版本不能高于 Unity 自带的版本
  （Unity 2022.3 是 Roslyn 4.3）。
- **不要**把 `Microsoft.CodeAnalysis.dll`、`System.Collections.Immutable.dll` 拷进 Assets，
  Unity 编译器已自带，重复拷贝会导致程序集冲突、分析器被判定为无法加载。
- DLL 的 `.meta` 必须带 `labels: - RoslynAnalyzer`（大小写敏感），并且所有平台都禁用
  （`enabled: 0`），否则 Unity 会把它当普通插件加载。

## 验证

编译输出里应能看到：

```
Assets\Scripts\Roslyn\ErrorBaseView.cs(8,30): warning UIVIEW0001: 重写了 Unity 生命周期方法 'Awake'，但没有调用 base.Awake()…
```
