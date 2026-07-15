// ConnectionRules.cs — 连接规则钩子（通用机制，零领域语义）。与 GraphValidator.RegisterExtension 平行：
// 领域层（如 dialogue）在此注册“哪种节点能接哪种节点”的规则；框架的两个消费方共享同一规则源——
//   · 拖拽实时过滤：GraphCanvasView.GetCompatiblePorts（被拒的端口直接连不上）；
//   · 事后校验兜底：GraphValidator.CheckConnectionRules（复制粘贴/老图/重连留下的非法边照样报错）。
// 单一规则源 => 实时拦截与事后报错永远一致。
// 仅依赖 4a 数据类型（NodeDefinition）。命名空间 NodeEditor。Editor 程序集（连线属创作期关注点）。

using System;
using System.Collections.Generic;

namespace NodeEditor
{
    // 一条待判定的有向边：from 节点的 fromPort 输出 -> to 节点的 toPort 输入。
    public struct ConnectionContext
    {
        public NodeDefinition fromDef; public string fromPort;
        public NodeDefinition toDef;   public string toPort;
    }

    // 规则的判定结果。reason 仅在 !allowed 时有意义：给用户看的“为什么不能连”。
    public struct ConnectionVerdict
    {
        public bool allowed;
        public string reason;
        public static ConnectionVerdict Allow => new ConnectionVerdict { allowed = true };
        public static ConnectionVerdict Deny(string why) => new ConnectionVerdict { allowed = false, reason = why };
    }

    // 连接规则注册表（缝，B10）。空表 = 全部允许（默认不改变既有行为；只有领域层注册规则后才开始拦截）。
    // 用私有有序表封装而非裸 public List：① 保留注册顺序 → Evaluate 有序短路（首个否决即止）；
    // ② 按稳定 id 去重，同 id 覆盖并告警；③ 提供 Register/Unregister 显式 API（测试用 Unregister 隔离临时规则）。
    public static class ConnectionRules
    {
        static readonly List<(string id, Func<ConnectionContext, ConnectionVerdict> rule)> s_Rules = new();

        // 领域层按稳定 id 注册规则（如 "dialogue"）。同 id 原位覆盖（保持顺序）并告警。
        public static void RegisterRule(string id, Func<ConnectionContext, ConnectionVerdict> rule)
        {
            if (string.IsNullOrEmpty(id) || rule == null) return;
            int at = s_Rules.FindIndex(e => e.id == id);
            if (at >= 0)
            {
                UnityEngine.Debug.LogWarning($"NodeEditor: connection rule '{id}' already registered; overwriting.");
                s_Rules[at] = (id, rule);
                return;
            }
            s_Rules.Add((id, rule));
        }

        // 按 id 注销（主要供测试隔离临时规则）。
        public static void UnregisterRule(string id)
        {
            int at = s_Rules.FindIndex(e => e.id == id);
            if (at >= 0) s_Rules.RemoveAt(at);
        }

        // 任一规则否决即否决；全部放行才允许。按注册顺序短路。fromDef/toDef 可能为 null（未解析的定义）——
        // 交由各规则自行决定（领域规则对非自己的节点一律放行）。
        public static ConnectionVerdict Evaluate(NodeDefinition fromDef, string fromPort, NodeDefinition toDef, string toPort)
        {
            var ctx = new ConnectionContext { fromDef = fromDef, fromPort = fromPort, toDef = toDef, toPort = toPort };
            foreach (var entry in s_Rules)
            {
                var v = entry.rule(ctx);
                if (!v.allowed) return v;
            }
            return ConnectionVerdict.Allow;
        }
    }
}
