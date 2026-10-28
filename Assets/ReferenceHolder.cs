using UnityEngine;

public class ReferenceHolder : MonoBehaviour
{
    [SerializeField, ComponentReferenceSelector]
    private SerializableDictionary<string, Object> references =
        new SerializableDictionary<string, Object>();

    public T Get<T>(string name) where T : Object
    {
        if (TryGet<T>(name, out var value))
            return value;

        Debug.LogError(
            $"{nameof(ReferenceHolder)}: 引用 '{name}' 不存在、已销毁，" +
            $"或类型与 {typeof(T).Name} 不匹配。",
            this);

        return null;
    }

    public bool TryGet<T>(string name, out T value) where T : Object
    {
        value = null;

        if (string.IsNullOrEmpty(name) ||
            references == null ||
            !references.TryGetValue(name, out var reference) ||
            reference == null)
        {
            return false;
        }

        value = reference as T;
        return value != null;
    }
}
