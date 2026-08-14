// UnityShim.cs — 只在纯 C# 构建里编译；Unity 侧有真的 UnityEngine.dll，本文件不参与。
// 放在 `Shim~/` 目录下：UPM/Unity 会忽略以 `~` 结尾的目录（与 `Samples~/` 同一机制），
// 所以 Unity 永远看不到它，不存在与真 UnityEngine 冲突的可能。
//
// 红线：这里**只允许 Attribute**。序列化特性在纯 C# 下没有任何运行时语义，
// 因此两侧行为不可能漂移。任何带状态/行为的类型（ScriptableObject、Object、
// Vector2、Debug……）都不得进入本文件——它们要么拆成纯 C# 类型，要么留在 Unity 侧载体里。
// PureCheck 工具会强制这条规则：发现非 Attribute 类型即失败。
using System;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    // 多态内联序列化。纯 C# 下无语义；Unity 侧由序列化器实现。
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeReference : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string header) { } }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string tooltip) { } }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TextAreaAttribute : Attribute
    {
        public TextAreaAttribute() { }
        public TextAreaAttribute(int minLines, int maxLines) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
}
