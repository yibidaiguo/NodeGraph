// Vec2.cs — 纯 C# 的二维坐标，替换数据模型里唯一一处真实的 UnityEngine.Vector2 依赖
//（NodeData.position，节点在画布上的位置）。Runtime 程序集（纯层）。
//
// 为什么可以无痛替换：字段名与顺序和 Vector2 完全一致（x, y），所以 Unity 序列化出的 YAML
// 逐字节相同——`position: {x: 20, y: 20}`。已烘好的 .asset 不需要任何迁移。
// 编辑器侧要跟 UnityEngine.Vector2 互转时用下面两个显式转换（放在 Unity 层的扩展里，
// 纯层不认识 Vector2）。

using System;

namespace NodeEditor
{
    [Serializable]
    public struct Vec2 : IEquatable<Vec2>
    {
        public float x;
        public float y;

        public Vec2(float x, float y) { this.x = x; this.y = y; }

        public static Vec2 Zero => new Vec2(0f, 0f);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.x + b.x, a.y + b.y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.x - b.x, a.y - b.y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.x * s, a.y * s);

        public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);
        public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);

        // 位置用精确相等即可：它是创作数据（编辑器写入的字面值），不是计算结果，
        // 不存在需要容差的浮点累积误差。
        public bool Equals(Vec2 other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => (x.GetHashCode() * 397) ^ y.GetHashCode();
        public override string ToString() => $"({x}, {y})";
    }
}
