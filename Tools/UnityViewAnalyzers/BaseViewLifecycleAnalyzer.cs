using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UnityViewAnalyzers
{
    /// <summary>
    /// 检查派生自 BaseView 的类：重写 Unity 生命周期方法（Awake/Start/Update 等）时必须调用 base 实现。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class BaseViewLifecycleAnalyzer : DiagnosticAnalyzer
    {
        internal const string MissingBaseCallId = "UIVIEW0001";
        internal const string HiddenLifecycleId = "UIVIEW0002";

        /// <summary>可用全局 AnalyzerConfig 选项 unity_view_analyzer_base_type 覆盖，多个名称用 ';' 或 ',' 分隔。</summary>
        private const string BaseTypeOptionKey = "unity_view_analyzer_base_type";
        private const string DefaultBaseTypeName = "BaseView";

        /// <summary>默认跳过的 attribute；可用 unity_view_analyzer_skip_attribute 覆盖。</summary>
        private const string SkipAttributeOptionKey = "unity_view_analyzer_skip_attribute";
        private const string DefaultSkipAttributeName = "SkipBaseCallAttribute";

        private const string Category = "Unity";

        private static readonly string[] LifecycleMethodNames =
        {
            "Awake", "OnEnable", "Start", "FixedUpdate", "Update", "LateUpdate",
            "OnDisable", "OnDestroy", "OnApplicationQuit", "OnApplicationPause", "OnApplicationFocus",
            "OnValidate", "Reset", "OnDrawGizmos", "OnDrawGizmosSelected",
            "OnBecameVisible", "OnBecameInvisible", "OnGUI",
            "OnPreCull", "OnPreRender", "OnPostRender", "OnRenderObject", "OnWillRenderObject", "OnRenderImage",
            "OnCollisionEnter", "OnCollisionStay", "OnCollisionExit",
            "OnCollisionEnter2D", "OnCollisionStay2D", "OnCollisionExit2D",
            "OnTriggerEnter", "OnTriggerStay", "OnTriggerExit",
            "OnTriggerEnter2D", "OnTriggerStay2D", "OnTriggerExit2D",
            "OnControllerColliderHit", "OnParticleCollision", "OnParticleTrigger", "OnParticleSystemStopped",
            "OnMouseDown", "OnMouseUp", "OnMouseUpAsButton", "OnMouseDrag", "OnMouseEnter", "OnMouseExit", "OnMouseOver",
            "OnJointBreak", "OnJointBreak2D", "OnAnimatorIK", "OnAnimatorMove",
            "OnTransformChildrenChanged", "OnTransformParentChanged", "OnBeforeTransformParentChanged",
            "OnAudioFilterRead",
        };

        private static readonly DiagnosticDescriptor MissingBaseCall = new DiagnosticDescriptor(
            MissingBaseCallId,
            "Unity 生命周期方法缺少 base 调用",
            "重写了 Unity 生命周期方法 '{0}'，但没有调用 base.{0}()。基类中的逻辑（引用绑定、事件注册等）会被跳过。如果这是故意的，请加上 [{1}]。",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "派生自 BaseView 的类重写 Unity 生命周期方法时必须调用 base 实现，否则基类里的引用绑定、事件注册等逻辑不会执行。也可以改为重写 OnAwake/OnStart/OnUpdate 等模板方法，或给方法加上 [SkipBaseCall] 表示这是有意为之。");

        private static readonly DiagnosticDescriptor HiddenLifecycle = new DiagnosticDescriptor(
            HiddenLifecycleId,
            "Unity 生命周期方法隐藏了基类实现",
            "'{0}' 隐藏了 {1} 中的同名 Unity 生命周期方法，基类实现不会执行。{2}",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "派生类声明了与基类同名的生命周期方法但没有使用 override。应改为 override 并调用 base 方法，或改用基类提供的可重写方法（如 OnAwake/OnStart/OnUpdate）。如果确实要跳过基类逻辑，给方法加上 [SkipBaseCall]。");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(MissingBaseCall, HiddenLifecycle);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(startContext =>
            {
                var monoBehaviour = startContext.Compilation.GetTypeByMetadataName("UnityEngine.MonoBehaviour");
                var baseTypeNames = ResolveNameList(startContext.Options, BaseTypeOptionKey, DefaultBaseTypeName);
                var skipAttributeNames = ResolveNameList(startContext.Options, SkipAttributeOptionKey, DefaultSkipAttributeName);
                var skipAttributeDisplayName = ToAttributeDisplayName(skipAttributeNames);
                var lifecycleNames = new HashSet<string>(LifecycleMethodNames, StringComparer.Ordinal);

                startContext.RegisterSymbolAction(
                    symbolContext => AnalyzeNamedType(symbolContext, baseTypeNames, skipAttributeNames, skipAttributeDisplayName, monoBehaviour, lifecycleNames),
                    SymbolKind.NamedType);
            });
        }

        private static void AnalyzeNamedType(
            SymbolAnalysisContext context,
            ImmutableArray<string> baseTypeNames,
            ImmutableArray<string> skipAttributeNames,
            string skipAttributeDisplayName,
            INamedTypeSymbol monoBehaviour,
            HashSet<string> lifecycleNames)
        {
            var type = (INamedTypeSymbol)context.Symbol;
            if (type.TypeKind != TypeKind.Class || type.IsImplicitlyDeclared)
                return;

            if (FindConfiguredBase(type, baseTypeNames, monoBehaviour) == null)
                return;

            // 类型级 attribute：这个类的所有生命周期方法都跳过检查（不向派生类继承）。
            if (HasSkipAttribute(type, skipAttributeNames))
                return;

            foreach (var member in type.GetMembers())
            {
                if (!(member is IMethodSymbol method))
                    continue;
                if (method.MethodKind != MethodKind.Ordinary || method.IsStatic)
                    continue;
                if (!lifecycleNames.Contains(method.Name))
                    continue;
                if (!method.Locations.Any(location => location.IsInSource))
                    continue;

                var declaration = GetMethodDeclaration(method);
                // abstract / extern / partial 声明没有方法体，无法调用 base。
                if (declaration == null || (declaration.Body == null && declaration.ExpressionBody == null))
                    continue;

                // 方法级 attribute：这个方法故意不调用 base。
                if (HasSkipAttribute(method, skipAttributeNames))
                    continue;

                var baseMethod = FindBaseLifecycleMethod(type, method);
                if (baseMethod == null || baseMethod.IsAbstract)
                    continue;

                var location = declaration.Identifier.GetLocation();

                if (method.IsOverride)
                {
                    if (CallsBase(declaration, method.Name))
                        continue;

                    context.ReportDiagnostic(Diagnostic.Create(
                        MissingBaseCall,
                        location,
                        method.Name,
                        skipAttributeDisplayName));
                }
                else
                {
                    var canBeOverridden = baseMethod.IsVirtual || baseMethod.IsOverride;
                    var advice = canBeOverridden
                        ? "请改为 override 重写基类虚方法，并在方法体内调用 base." + method.Name + "()。"
                        : "基类中的该方法不可重写，派生类的声明会让基类实现失效，请改用基类提供的可重写方法（OnAwake/OnStart/OnUpdate 等）。";

                    context.ReportDiagnostic(Diagnostic.Create(
                        HiddenLifecycle,
                        location,
                        method.Name,
                        baseMethod.ContainingType.Name,
                        advice));
                }
            }
        }

        /// <summary>
        /// 判断符号上是否带有跳过检查的 attribute。
        /// 只检查符号自身的声明，不做继承传递：每个重写都应显式表态。
        /// </summary>
        private static bool HasSkipAttribute(ISymbol symbol, ImmutableArray<string> skipAttributeNames)
        {
            if (skipAttributeNames.IsDefaultOrEmpty || symbol == null)
                return false;

            foreach (var attribute in symbol.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass != null && IsConfiguredBaseName(attributeClass, skipAttributeNames))
                    return true;
            }

            return false;
        }

        private static string ToAttributeDisplayName(ImmutableArray<string> skipAttributeNames)
        {
            if (skipAttributeNames.IsDefaultOrEmpty)
                return DefaultSkipAttributeName;

            var name = skipAttributeNames[0];
            var lastDot = name.LastIndexOf('.');
            if (lastDot >= 0 && lastDot + 1 < name.Length)
                name = name.Substring(lastDot + 1);

            const string suffix = "Attribute";
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
                name = name.Substring(0, name.Length - suffix.Length);

            return name;
        }

        private static INamedTypeSymbol FindConfiguredBase(
            INamedTypeSymbol type,
            ImmutableArray<string> baseTypeNames,
            INamedTypeSymbol monoBehaviour)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (!IsConfiguredBaseName(current, baseTypeNames))
                    continue;

                // BaseView 必须是 MonoBehaviour，避免误报同名普通类。
                if (monoBehaviour != null && !InheritsFrom(current, monoBehaviour))
                    return null;

                return current;
            }

            return null;
        }

        private static bool IsConfiguredBaseName(INamedTypeSymbol candidate, ImmutableArray<string> names)
        {
            var display = candidate.ToDisplayString();
            var name = candidate.Name;

            // [SkipBaseCall] 与 [SkipBaseCallAttribute] 视为同一个名字。
            const string attributeSuffix = "Attribute";
            var shortName = name.EndsWith(attributeSuffix, StringComparison.Ordinal) && name.Length > attributeSuffix.Length
                ? name.Substring(0, name.Length - attributeSuffix.Length)
                : name;

            foreach (var candidateName in names)
            {
                if (string.Equals(name, candidateName, StringComparison.Ordinal) ||
                    string.Equals(shortName, candidateName, StringComparison.Ordinal) ||
                    string.Equals(display, candidateName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol expected)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, expected))
                    return true;
            }

            return false;
        }

        private static IMethodSymbol FindBaseLifecycleMethod(INamedTypeSymbol type, IMethodSymbol method)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                foreach (var candidate in current.GetMembers(method.Name))
                {
                    if (candidate is IMethodSymbol baseMethod &&
                        baseMethod.MethodKind == MethodKind.Ordinary &&
                        !baseMethod.IsStatic &&
                        baseMethod.Arity == method.Arity &&
                        baseMethod.Parameters.Length == method.Parameters.Length)
                        return baseMethod;
                }
            }

            return null;
        }

        private static MethodDeclarationSyntax GetMethodDeclaration(IMethodSymbol method)
        {
            MethodDeclarationSyntax fallback = null;
            foreach (var reference in method.DeclaringSyntaxReferences)
            {
                if (!(reference.GetSyntax() is MethodDeclarationSyntax declaration))
                    continue;
                if (declaration.Body != null || declaration.ExpressionBody != null)
                    return declaration;
                fallback = fallback ?? declaration;
            }

            return fallback;
        }

        private static bool CallsBase(MethodDeclarationSyntax declaration, string methodName)
        {
            var body = (SyntaxNode)declaration.Body ?? declaration.ExpressionBody;
            if (body == null)
                return false;

            foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is MemberAccessExpressionSyntax access &&
                    access.Expression is BaseExpressionSyntax &&
                    string.Equals(access.Name.Identifier.ValueText, methodName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static ImmutableArray<string> ResolveNameList(AnalyzerOptions options, string optionKey, string defaultName)
        {
            var raw = defaultName;
            if (options != null &&
                options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(optionKey, out var configured) &&
                !string.IsNullOrWhiteSpace(configured))
                raw = configured;

            var names = raw
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim())
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToImmutableArray();

            return names.IsDefaultOrEmpty ? ImmutableArray.Create(defaultName) : names;
        }
    }
}
