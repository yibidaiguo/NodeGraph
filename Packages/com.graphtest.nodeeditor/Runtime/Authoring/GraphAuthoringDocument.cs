// GraphAuthoringDocument.cs —— 图的版本化、纯 C# 创作交换模型。
//
// 这里只描述语义数据，不负责 JSON、Unity 资产定位或提交。节点/入口/边使用 authoringKey
// 寻址，同时保留 instanceId，使人工作图与 AI 文档始终能回到同一份 GraphData。

using System;
using System.Collections.Generic;

namespace NodeEditor
{
    [Serializable]
    public sealed class GraphAuthoringDocument
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public bool authoringKeysPersisted = true;
        public GraphAuthoringRevisionVector revisionVector = new();

        public string graphId = "";
        public string module = "";
        public string group = "";
        public GraphType graphType;
        public GraphOrientation orientation = GraphOrientation.Inherit;

        public List<string> entries = new();
        public List<GraphAuthoringNode> nodes = new();
        public List<GraphAuthoringEdge> edges = new();
        // 当前图实际拥有/继承的黑板资产完整快照。列表顺序即作用域叠加顺序；
        // ownerPath 让编辑器事务能把每层写回原资产，而不是创造第二份数据源。
        public List<GraphAuthoringBlackboardLayer> blackboards = new();
    }

    [Serializable]
    public sealed class GraphAuthoringBlackboardLayer
    {
        public string ownerPath;
        public string module = "";
        public string group = "";
        public List<GraphAuthoringBlackboardVariableData> variables = new();
    }

    [Serializable]
    public sealed class GraphAuthoringBlackboardVariableData
    {
        public string key;
        public GraphAuthoringTypeRef type;
        public string defaultJson;
    }

    [Serializable]
    public sealed class GraphAuthoringTypeRef
    {
        public TypeKind kind;
        public PrimitiveType primitive;
        public string enumOrObjectName;
        public GraphAuthoringTypeRef element;
    }

    [Serializable]
    public sealed class GraphAuthoringRevisionVector
    {
        public List<GraphAuthoringRevisionOwner> owners = new();
    }

    public enum GraphAuthoringExpectedState { Exists, MustNotExist }

    [Serializable]
    public sealed class GraphAuthoringRevisionOwner
    {
        public string ownerId;
        public string ownerPath;
        public string contentHash;
        public GraphAuthoringExpectedState expectedState;
    }

    [Serializable]
    public sealed class GraphAuthoringNode
    {
        public string authoringKey;
        public string instanceId;
        public string definitionId;
        public float positionX;
        public float positionY;
        public string displayName;
        public string note;
        public bool pinned;
        public List<GraphAuthoringParam> parameters = new();
        public List<GraphAuthoringGraphRef> graphRefs = new();
        public List<GraphAuthoringUnitSlot> unitSlots = new();
    }

    [Serializable]
    public sealed class GraphAuthoringEdge
    {
        public string from;
        public string fromPort;
        public string to;
        public string toPort;
    }

    [Serializable]
    public sealed class GraphAuthoringParam
    {
        public string paramName;
        public string valueJson;
    }

    [Serializable]
    public sealed class GraphAuthoringGraphRef
    {
        public string paramName;
        public string graphId;
    }

    [Serializable]
    public sealed class GraphAuthoringUnitSlot
    {
        public string paramName;
        public GraphAuthoringUnit unit;
    }

    [Serializable]
    public sealed class GraphAuthoringUnit
    {
        // 始终输出 UnitAuthoringIdAttribute 声明的稳定 id；旧 CLR 名仅可作为输入别名。
        public string typeId;
        public List<GraphAuthoringUnitField> fields = new();
    }

    public enum GraphAuthoringUnitFieldKind { Scalar, Unit, UnitList }

    [Serializable]
    public sealed class GraphAuthoringUnitField
    {
        public string name;
        public GraphAuthoringUnitFieldKind kind;
        // 区分 null 字符串/列表与空值；Unit 的 null 也显式记录，避免形状含混。
        public bool isNull;
        public string value;
        public GraphAuthoringUnit unit;
        public List<GraphAuthoringUnit> units;
    }

    public enum GraphAuthoringDiagnosticSeverity { Error }

    [Serializable]
    public sealed class GraphAuthoringDiagnostic
    {
        public GraphAuthoringDiagnosticSeverity severity;
        public string code;
        public string path;
        public string message;

        public GraphAuthoringDiagnostic(
            string code,
            string path,
            string message,
            GraphAuthoringDiagnosticSeverity severity = GraphAuthoringDiagnosticSeverity.Error)
        {
            this.code = code;
            this.path = path;
            this.message = message;
            this.severity = severity;
        }
    }

    public sealed class GraphAuthoringExportResult
    {
        public GraphAuthoringDocument Document { get; }
        public IReadOnlyList<GraphAuthoringDiagnostic> Diagnostics { get; }
        public bool Succeeded => Document != null && Diagnostics.Count == 0;

        internal GraphAuthoringExportResult(
            GraphAuthoringDocument document,
            IReadOnlyList<GraphAuthoringDiagnostic> diagnostics)
        {
            Document = document;
            Diagnostics = diagnostics;
        }
    }

    public sealed class GraphAuthoringImportResult
    {
        public GraphData Data { get; }
        public IReadOnlyList<GraphAuthoringDiagnostic> Diagnostics { get; }
        public bool Succeeded => Data != null && Diagnostics.Count == 0;

        internal GraphAuthoringImportResult(
            GraphData data,
            IReadOnlyList<GraphAuthoringDiagnostic> diagnostics)
        {
            Data = data;
            Diagnostics = diagnostics;
        }
    }
}
