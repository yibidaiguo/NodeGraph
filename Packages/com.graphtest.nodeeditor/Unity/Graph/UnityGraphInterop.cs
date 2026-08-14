// UnityGraphInterop.cs —— 纯层类型与 Unity 类型之间的转换。NodeEditor.Unity 程序集。
//
// 为什么不做成 Vec2 的隐式转换运算符：那要求 Vec2 认识 UnityEngine.Vector2，
// 而 Vec2 住在纯层，一认识就破了红线（PureCheck 会直接报红）。运算符也没法从类型外部追加，
// partial 又要求同程序集。所以转换只能以扩展方法的形式住在 Unity 这一侧——
// 方向是对的：纯层不知道 Unity 存在，Unity 知道怎么跟纯层打交道。

using UnityEngine;

namespace NodeEditor
{
    public static class UnityGraphInterop
    {
        public static Vector2 ToVector2(this Vec2 v) => new Vector2(v.x, v.y);
        public static Vec2 ToVec2(this Vector2 v) => new Vec2(v.x, v.y);

        public static Vector3 ToVector3(this Vec2 v) => new Vector3(v.x, v.y, 0f);
        public static Vec2 ToVec2(this Vector3 v) => new Vec2(v.x, v.y);
    }
}
