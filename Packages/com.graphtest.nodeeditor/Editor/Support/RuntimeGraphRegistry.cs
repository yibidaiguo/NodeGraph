// RuntimeGraphRegistry.cs — 运行时↔编辑器桥的注册表缝（领域 Editor 桥经 [InitializeOnLoad] 转发幂等 Register/Unregister）。
// 拆自 EditorSupport.cs（B3 内聚拆分：一类型一文件；类型代码逐字未改）。

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using NodeEditor;          // 第 4 层数据/运行时类型（NodeDefinition、NodeGraphAsset 等）

namespace NodeEditor.EditorUI
{

    // 全部 live runtime 的幂等集合。领域 Editor bridge 负责注册/注销，窗口按图所有权选择。
    // Current 仅保留给旧消费者；新窗口逻辑不得退回 last-writer-wins。
    public static class RuntimeGraphRegistry
    {
        static readonly List<IRuntimeGraph> s_Live = new();
        static int s_Version;   // 集合变化触发旧运行器回退探测；显式 ActiveGraph 则按帧读取。
        public static IReadOnlyList<IRuntimeGraph> Live => s_Live;
        public static int Version => s_Version;
        public static IRuntimeGraph Current => s_Live.Count > 0 ? s_Live[s_Live.Count - 1] : null;
        public static void Register(IRuntimeGraph g)
        {
            if (g == null || s_Live.Contains(g)) return;
            s_Live.Add(g);
            s_Version++;
        }
        public static void Unregister(IRuntimeGraph g)
        {
            if (g != null && s_Live.Remove(g)) s_Version++;
        }
    }
}
