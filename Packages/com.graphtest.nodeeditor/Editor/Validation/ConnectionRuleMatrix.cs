// ConnectionRuleMatrix.cs — 连接规则「矩阵引擎」（通用机制，零领域语义）。
// 领域层过去各自复制一份 Rule/MostSpecific/Permits/消息 解释器（Dialogue/Task/StateMachine 三份近乎逐字相同，
// 且消息键漂移出 val.task.conn* 分叉）——现收敛为本泛型引擎：领域只提供「s_Rules 矩阵数据 + Kind 取法」，
// 引擎负责出向/入向判定、端口专属优先、Include/Exclude 语义与本地化拒绝消息（统一 val.conn* 键，框架种子）。
// 用法（领域侧）：
//   static readonly ConnectionRuleMatrix<XNodeDefinition, XNodeKind> s_Matrix =
//       new(new[] { new ConnectionRuleEntry<XNodeKind> { node = ..., port = "...", side = ConnectSide.Out,
//                                                        mode = ConnectMode.Include, kinds = new[]{ ... } } },
//           def => def.Kind);
//   static XConnectionRules() => ConnectionRules.RegisterRule("x", s_Matrix.Evaluate);
// 仅依赖 4a 数据类型 + Editor 侧 Localizer。命名空间 NodeEditor（与 ConnectionRules.cs 同侧，领域经既有 using 即达）。

using System;
using System.Collections.Generic;
using System.Linq;
using NodeEditor.EditorUI;   // Localizer（本地化拒绝消息）

namespace NodeEditor
{
    // 两种模式：Include=仅允许列出的种类；Exclude=禁止列出的种类（其余放行）。
    public enum ConnectMode { Include, Exclude }
    // 规则作用在哪一侧：Out=约束“本节点的输出能连到谁”；In=约束“本节点的输入只接受谁”。
    public enum ConnectSide { Out, In }

    // 一条矩阵规则。port 为 null = 该侧所有端口；kinds = 对端允许/禁止的种类集合。
    public struct ConnectionRuleEntry<TKind> where TKind : struct, Enum
    {
        public TKind node;
        public string port;
        public ConnectSide side;
        public ConnectMode mode;
        public TKind[] kinds;
    }

    // 泛型矩阵引擎：TDef=领域节点定义基类（两端都不是 TDef 时不插手别的域），TKind=领域节点种类枚举。
    public sealed class ConnectionRuleMatrix<TDef, TKind>
        where TDef : NodeDefinition
        where TKind : struct, Enum
    {
        readonly ConnectionRuleEntry<TKind>[] m_Rules;
        readonly Func<TDef, TKind> m_KindOf;

        public ConnectionRuleMatrix(ConnectionRuleEntry<TKind>[] rules, Func<TDef, TKind> kindOf)
        {
            m_Rules = rules ?? Array.Empty<ConnectionRuleEntry<TKind>>();
            m_KindOf = kindOf ?? throw new ArgumentNullException(nameof(kindOf));
        }

        // 判定一条边：先看源节点出向规则（该 fromPort 能否连到 toKind），再看目标节点入向规则
        //（该 toPort 是否接受 fromKind）。任一否决即否决。仅当两端都是本域节点时才插手。
        public ConnectionVerdict Evaluate(ConnectionContext ctx)
        {
            var fromDef = ctx.fromDef as TDef;
            var toDef = ctx.toDef as TDef;
            if (fromDef == null || toDef == null) return ConnectionVerdict.Allow;
            var fromKind = m_KindOf(fromDef);
            var toKind = m_KindOf(toDef);

            var outRule = MostSpecific(fromKind, ctx.fromPort, ConnectSide.Out);
            if (outRule.HasValue && !Permits(outRule.Value, toKind))
                return ConnectionVerdict.Deny(OutMessage(fromDef, ctx.fromPort, toDef, outRule.Value));

            var inRule = MostSpecific(toKind, ctx.toPort, ConnectSide.In);
            if (inRule.HasValue && !Permits(inRule.Value, fromKind))
                return ConnectionVerdict.Deny(InMessage(toDef, ctx.toPort, fromDef, inRule.Value));

            return ConnectionVerdict.Allow;
        }

        // 取该节点该侧最具体的规则：端口专属（port==指定端口）优先于整节点（port==null）。无匹配返回 null。
        ConnectionRuleEntry<TKind>? MostSpecific(TKind node, string port, ConnectSide side)
        {
            ConnectionRuleEntry<TKind>? portSpecific = null, nodeWide = null;
            var eq = EqualityComparer<TKind>.Default;
            foreach (var r in m_Rules)
            {
                if (!eq.Equals(r.node, node) || r.side != side) continue;
                if (r.port == port) portSpecific = r;
                else if (r.port == null) nodeWide = r;
            }
            return portSpecific ?? nodeWide;
        }

        static bool Permits(ConnectionRuleEntry<TKind> r, TKind other)
        {
            bool listed = r.kinds != null && r.kinds.Contains(other);
            return r.mode == ConnectMode.Include ? listed : !listed;
        }

        // —— 本地化诊断消息：点名两端（按当前编辑器语言取节点显示名）+ 端口 + 允许/禁止集合。
        // 键为框架统一的 val.conn*（框架种子模块播种中文；Task 早期的 val.task.conn* 分叉键已废弃）。——
        string OutMessage(TDef from, string fromPort, TDef to, ConnectionRuleEntry<TKind> r)
        {
            string f = Localizer.NodeName(from), t = Localizer.NodeName(to), set = KindList(r.kinds);
            return r.mode == ConnectMode.Include
                ? L("val.connOutInclude", "'{0}.{1}' can only connect to: {2} (not '{3}')", f, fromPort, set, t)
                : L("val.connOutExclude", "'{0}.{1}' must not connect to: {2} (got '{3}')", f, fromPort, set, t);
        }

        string InMessage(TDef to, string toPort, TDef from, ConnectionRuleEntry<TKind> r)
        {
            string t = Localizer.NodeName(to), f = Localizer.NodeName(from), set = KindList(r.kinds);
            return r.mode == ConnectMode.Include
                ? L("val.connInInclude", "'{0}.{1}' only accepts connections from: {2} (not '{3}')", t, toPort, set, f)
                : L("val.connInExclude", "'{0}.{1}' rejects connections from: {2} (got '{3}')", t, toPort, set, f);
        }

        // 集合里展示种类名，按当前语言本地化（kind.<Kind> 键由各领域 Setup 播种；缺种子回退枚举名）。
        static string KindList(TKind[] kinds) =>
            kinds == null ? "" : string.Join(", ", kinds.Select(k => Localizer.UI($"kind.{k}", k.ToString())));

        // 校验/诊断文案本地化（与框架 GraphValidator 同 idiom）：命中表则用，否则回退内联英文；{0} 占位用 string.Format。
        static string L(string key, string enFormat, params object[] args) => string.Format(Localizer.UI(key, enFormat), args);
    }
}
