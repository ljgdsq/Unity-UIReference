using System;

/// <summary>
/// Adds a GameObject/component menu to string-to-UnityEngine.Object dictionary entries.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class ComponentReferenceSelectorAttribute : Attribute
{
}
