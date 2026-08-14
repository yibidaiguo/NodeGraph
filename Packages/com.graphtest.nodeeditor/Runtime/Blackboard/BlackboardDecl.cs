// BlackboardDecl.cs —— 黑板「声明」的纯 C# 契约与实现。Runtime 程序集（纯层）。
//
// 0.0.x 里 BlackboardSet 直接持有 List<BlackboardAsset>（ScriptableObject），把黑板分层
// 钉死在 Unity 上。这里抽出 IBlackboardDecl：BlackboardSet 只认接口，
// Unity 侧的 BlackboardAsset 实现它（它本来就有这几个成员，只加了一个接口声明），
// 于是既有调用点 `new BlackboardSet(blackboardAssets)` 一字不改仍然编译通过。

using System.Collections.Generic;
using System.Linq;

namespace NodeEditor
{
    // 一档黑板声明（全局 / 模块 / 组中的某一档）。只读——运行期的值存在 RuntimeBlackboard，
    // 声明与存储正交（准则 #15：声明分层 与 运行每实例存储 是两件事）。
    public interface IBlackboardDecl
    {
        // 作用域标签：module=="" && group=="" → 全局；module=="X" → 模块级；module+group → 组级。
        string Module { get; }
        string Group { get; }
        IReadOnlyList<VariableDef> Variables { get; }
        VariableDef Find(string key);
    }

    // 纯 C# 的一档黑板声明。JSON 载入、dotnet test、服务器侧都用它；
    // Unity 侧则由 BlackboardAsset 承担同一角色。
    public sealed class BlackboardDecl : IBlackboardDecl
    {
        readonly List<VariableDef> m_Variables;

        public BlackboardDecl(string module = "", string group = "", IEnumerable<VariableDef> variables = null)
        {
            Module = module ?? "";
            Group = group ?? "";
            m_Variables = variables?.Where(v => v != null).ToList() ?? new List<VariableDef>();
        }

        public string Module { get; }
        public string Group { get; }
        public IReadOnlyList<VariableDef> Variables => m_Variables;
        public VariableDef Find(string key) => m_Variables.FirstOrDefault(v => v.key == key);

        public BlackboardDecl Add(string key, TypeRef type, string defaultJson = null)
        {
            m_Variables.Add(new VariableDef { key = key, type = type, defaultJson = defaultJson });
            return this;
        }
    }
}
