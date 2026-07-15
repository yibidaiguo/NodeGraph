// RuntimeBlackboard.cs —— 每实例运行时黑板存储（机制，领域无关）。
// 过去 Dialogue/StateMachine/Task 三个领域各自复制一份「Dictionary + BlackboardSet 播种 + Get/Set/GetF/SetF」
//（近乎逐行相同）——现收敛为本类；领域黑板改为薄继承（保留各自类型名以兼容既有 API/存档代码）。
// 语义：
//   · 构造时由这张图的「有效黑板」（BlackboardSet：全局⊕模块⊕组合并、同名 key 就近覆盖）声明的默认值播种，
//     字符串默认值 → 类型化值经框架 UnitValues.To（与可组合单元同一套值语义）；
//   · 每个运行实例一份存储，绝非全局可变单例（准则#15 / 红线 §6：声明分层 与 运行每实例存储 正交）；
//   · set 为 null（或空集）产出一块空（但可用）的板——没有声明任何变量的图依然能运行，
//     未知的 key 在被写入前读回 null/0；
//   · GetF/SetF 是同一存储的浮点快速路径：GetF 经 UnitValues.Number 把存储的任意值（int/bool/字符串数字）归约，
//     SetF 把 float 装箱写回共享字典，Get 与 GetF 观察到的是同一个一致的值。
// Runtime 程序集 —— 无 editor-only 依赖。

using System.Collections.Generic;

namespace NodeEditor
{
    public class RuntimeBlackboard : IScopedBlackboard
    {
        readonly Dictionary<string, object> m_Values = new();

        // 这张图的「有效黑板」声明视图（各档合并、就近覆盖）。Runner 的 Capture/Restore
        // 靠它枚举声明变量、并按声明的 TypeRef 把快照字符串值往返定型。
        public BlackboardSet Declared { get; }

        public RuntimeBlackboard(BlackboardSet set)
        {
            Declared = set;
            if (set == null) return;
            foreach (var v in set.All())
                m_Values[v.key] = UnitValues.To(v.type, v.defaultJson);
        }

        public object Get(string key) => m_Values.TryGetValue(key, out var v) ? v : null;
        public void Set(string key, object value) => m_Values[key] = value;
        public float GetF(string key) => (float)UnitValues.Number(Get(key));
        public void SetF(string key, float value) => m_Values[key] = value;
    }
}
