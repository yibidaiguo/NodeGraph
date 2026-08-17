using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NodeEditor.EditorUI
{
    // Length-prefixed canonical stream: no JSON formatting, locale, Unity metadata or revision fields.
    internal static class GraphAuthoringSemanticHash
    {
        public static string Graph(GraphAuthoringDocument document)
        {
            var w = new Writer();
            w.Bool(document != null && document.authoringKeysPersisted);
            w.Text(document?.graphId);
            w.Text(document?.module);
            w.Text(document?.group);
            w.Int((int)(document?.graphType ?? default));
            w.Int((int)(document?.orientation ?? default));
            w.List(document?.entries, (x, v) => x.Text(v));
            w.List(document?.nodes, Node);
            w.List(document?.edges, Edge);
            return w.Finish();
        }

        public static string Blackboard(GraphAuthoringBlackboardLayer layer)
        {
            var w = new Writer();
            w.Text(layer?.module);
            w.Text(layer?.group);
            w.List(layer?.variables, Variable);
            return w.Finish();
        }

        static void Node(Writer w, GraphAuthoringNode value)
        {
            w.Null(value);
            if (value == null) return;
            w.Text(value.authoringKey); w.Text(value.instanceId); w.Text(value.definitionId);
            w.Float(value.positionX); w.Float(value.positionY); w.Text(value.displayName);
            w.Text(value.note); w.Bool(value.pinned);
            w.List(value.parameters, (x, v) => { x.Null(v); if (v != null) { x.Text(v.paramName); x.Text(v.valueJson); } });
            w.List(value.graphRefs, (x, v) => { x.Null(v); if (v != null) { x.Text(v.paramName); x.Text(v.graphId); } });
            w.List(value.unitSlots, (x, v) => { x.Null(v); if (v != null) { x.Text(v.paramName); Unit(x, v.unit); } });
        }

        static void Edge(Writer w, GraphAuthoringEdge value)
        {
            w.Null(value);
            if (value == null) return;
            w.Text(value.from); w.Text(value.fromPort); w.Text(value.to); w.Text(value.toPort);
        }

        static void Unit(Writer w, GraphAuthoringUnit value)
        {
            w.Null(value);
            if (value == null) return;
            w.Text(value.typeId);
            w.List(value.fields, (x, field) =>
            {
                x.Null(field);
                if (field == null) return;
                x.Text(field.name); x.Int((int)field.kind); x.Bool(field.isNull); x.Text(field.value);
                Unit(x, field.unit); x.List(field.units, Unit);
            });
        }

        static void Variable(Writer w, GraphAuthoringBlackboardVariableData value)
        {
            w.Null(value);
            if (value == null) return;
            w.Text(value.key); Type(w, value.type); w.Text(value.defaultJson);
        }

        static void Type(Writer w, GraphAuthoringTypeRef value)
        {
            w.Null(value);
            if (value == null) return;
            w.Int((int)value.kind); w.Int((int)value.primitive); w.Text(value.enumOrObjectName); Type(w, value.element);
        }

        sealed class Writer
        {
            readonly StringBuilder m_Value = new();
            public void Null(object value) => m_Value.Append(value == null ? "N;" : "V;");
            public void Bool(bool value) => m_Value.Append(value ? "B1;" : "B0;");
            public void Int(int value) => Text(value.ToString(CultureInfo.InvariantCulture));
            public void Float(float value) => Text(value.ToString("R", CultureInfo.InvariantCulture));
            public void Text(string value)
            {
                if (value == null) { m_Value.Append("S-;"); return; }
                m_Value.Append('S').Append(value.Length).Append(':').Append(value).Append(';');
            }
            public void List<T>(IReadOnlyList<T> values, Action<Writer, T> write)
            {
                if (values == null) { m_Value.Append("L-;"); return; }
                m_Value.Append('L').Append(values.Count).Append(';');
                for (int i = 0; i < values.Count; i++) write(this, values[i]);
            }
            public string Finish()
            {
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(m_Value.ToString()));
                var result = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }
    }
}
