// BlackboardSet.cs —— 把分层的多块 BlackboardAsset 合并成一个只读视图。子层 4a（节点数据），Runtime 程序集。
//
// 一张图的「有效黑板」= 全局 ⊕ 模块 ⊕ 组（由泛到专，由外到内）。本类按这个顺序持有各档 asset，
// 并对外提供合并后的查询：
//   · 同名 key 就近覆盖（更专的层级胜出）——Find 从最专一档往外找，第一个命中即返回；
//   · All() 去重后每个 key 只保留其最专一档的定义（运行时据此播种默认值）。
// 纯数据、无 editor 依赖：编辑器侧的 BlackboardLocator 负责按 module/group 标签找出适用各档并按序传入；
// 运行时（player 构建）由调用方（如 DialoguePlayer）直接持有各档引用传入。
// 单块场景（测试 / 仅全局）可用 new BlackboardSet(asset) 包一层，接口完全一致。

using System.Collections.Generic;
using System.Linq;

namespace NodeEditor
{
    public sealed class BlackboardSet
    {
        // 由外到内排列：全局在前、组在后。null 档会被剔除，因此缺某一档（如某图没有组黑板）也安全。
        readonly List<BlackboardAsset> m_Layers;

        public BlackboardSet(params BlackboardAsset[] layers) : this((IEnumerable<BlackboardAsset>)layers) { }
        public BlackboardSet(IEnumerable<BlackboardAsset> layers)
        {
            m_Layers = (layers ?? Enumerable.Empty<BlackboardAsset>()).Where(a => a != null).ToList();
        }

        // 由外到内的各档 asset（全局在前）。编辑器据此知道某一档对应哪块 SO（去哪写入 / 是否缺该档）。
        public IReadOnlyList<BlackboardAsset> Layers => m_Layers;

        // 就近覆盖：从最专一档往外找，第一个命中即返回（更专的层级遮蔽更泛的）。
        public VariableDef Find(string key)
        {
            for (int i = m_Layers.Count - 1; i >= 0; i--)
            {
                var v = m_Layers[i].Find(key);
                if (v != null) return v;
            }
            return null;
        }

        public bool Has(string key) => Find(key) != null;

        // 合并后的全部变量：每个 key 仅保留其最专一档的定义。供运行时播种与「图参数总览」遍历。
        public IEnumerable<VariableDef> All()
        {
            var seen = new HashSet<string>();
            for (int i = m_Layers.Count - 1; i >= 0; i--)   // 从最专一档起，先到先得 = 就近覆盖
                foreach (var v in m_Layers[i].Variables)
                    if (v != null && v.key != null && seen.Add(v.key))
                        yield return v;
        }

        // 合并后、与 expected 类型兼容的 key（检视面板的「键」下拉用）。
        public IEnumerable<string> KeysOfType(TypeRef expected) =>
            All().Where(v => expected == null || TypeRefCompat.Compatible(v.type, expected)).Select(v => v.key);
    }
}
