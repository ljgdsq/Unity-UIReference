using System;

/// <summary>
/// 标记在 Unity 生命周期方法（或类型）上，声明这是**故意**不调用基类同名实现的写法，
/// 关闭 UIVIEW0001 / UIVIEW0002 警告。
/// </summary>
/// <example>
/// <code>
/// protected override void Awake()
/// {
///     // 故意不调用 base.Awake()：这个界面不需要 ReferenceHolder 绑定
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SkipBaseCallAttribute : Attribute
{
    public SkipBaseCallAttribute()
    {
    }

    public SkipBaseCallAttribute(string reason)
    {
        Reason = reason;
    }

    /// <summary>不调用 base 的原因，仅供阅读，分析器不会使用。</summary>
    public string Reason { get; }
}
