// PureCheck — 红线守卫：证明纯 C# 程序集没有沾上 UnityEngine。
//
// 为什么不能只看引用列表：如果 shim 和纯代码编进同一个程序集，引用列表里当然
// 没有 UnityEngine（shim 就在里面），检查会假通过。所以 shim 必须是独立程序集，
// 本工具同时验证三件事：
//   1. 纯程序集不引用任何真 UnityEngine/UnityEditor 程序集
//   2. 纯程序集自己不定义 UnityEngine.* 类型（= shim 没被偷偷编进来）
//   3. 允许的 shim 程序集里只有 Attribute 子类（= 没有 ScriptableObject/Object/Vector2 这类
//      带运行时语义的类型混进逻辑层；一旦有人往 shim 里加载体类型，这里就会红）
//
// 用法: PureCheck <assembly.dll> [--shim <ShimAssemblyName>]
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

static class PureCheck
{
    static readonly string[] Banned = { "UnityEngine", "UnityEditor" };
    static bool IsBanned(string n) => Banned.Any(b => n == b || n.StartsWith(b + ".", StringComparison.Ordinal));

    static int Main(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
        if (args.Length == 0) { Console.Error.WriteLine("usage: PureCheck <assembly.dll> [--shim <Name>]"); return 2; }
        var path = args[0];
        string shim = null;
        for (int i = 1; i < args.Length - 1; i++) if (args[i] == "--shim") shim = args[i + 1];
        if (!File.Exists(path)) { Console.Error.WriteLine($"not found: {path}"); return 2; }

        var failures = new List<string>();
        var notes = new List<string>();

        using (var fs = File.OpenRead(path))
        using (var pe = new PEReader(fs))
        {
            var mr = pe.GetMetadataReader();
            var self = mr.GetString(mr.GetAssemblyDefinition().Name);

            // --- 1. 引用的程序集 ---
            var refNames = new Dictionary<AssemblyReferenceHandle, string>();
            foreach (var h in mr.AssemblyReferences)
            {
                var name = mr.GetString(mr.GetAssemblyReference(h).Name);
                refNames[h] = name;
                if (IsBanned(name)) failures.Add($"引用了 Unity 程序集: {name}");
            }
            if (shim != null && refNames.Values.Contains(shim)) notes.Add($"引用 shim: {shim} (允许)");

            // --- 2. 自己定义的类型（shim 是否被编进来了）---
            foreach (var h in mr.TypeDefinitions)
            {
                var td = mr.GetTypeDefinition(h);
                var ns = mr.GetString(td.Namespace);
                if (!string.IsNullOrEmpty(ns) && IsBanned(ns))
                    failures.Add($"本程序集内定义了 Unity 命名空间类型: {ns}.{mr.GetString(td.Name)} —— shim 被编进纯程序集了");
            }

            // --- 3. 引用到的 Unity 类型必须全部来自被允许的 shim ---
            foreach (var h in mr.TypeReferences)
            {
                var tr = mr.GetTypeReference(h);
                var ns = mr.GetString(tr.Namespace);
                if (string.IsNullOrEmpty(ns) || !IsBanned(ns)) continue;
                var tn = $"{ns}.{mr.GetString(tr.Name)}";
                if (tr.ResolutionScope.Kind == HandleKind.AssemblyReference
                    && refNames.TryGetValue((AssemblyReferenceHandle)tr.ResolutionScope, out var from))
                {
                    if (from == shim) notes.Add($"  {tn}  <- {from}");
                    else failures.Add($"Unity 类型 {tn} 来自 {from}（非允许的 shim）");
                }
                else failures.Add($"Unity 类型 {tn} 的来源无法解析");
            }

            Console.WriteLine($"程序集: {self}  ({Path.GetFileName(path)})");
        }

        // --- 4. shim 自身纯度：只允许 Attribute 子类 ---
        if (shim != null)
        {
            var shimPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".", shim + ".dll");
            if (File.Exists(shimPath))
            {
                using var sfs = File.OpenRead(shimPath);
                using var spe = new PEReader(sfs);
                var smr = spe.GetMetadataReader();
                foreach (var h in smr.TypeDefinitions)
                {
                    var td = smr.GetTypeDefinition(h);
                    var name = smr.GetString(td.Name);
                    if (name == "<Module>") continue;
                    var bt = td.BaseType;
                    var baseName = bt.Kind == HandleKind.TypeReference
                        ? smr.GetString(smr.GetTypeReference((TypeReferenceHandle)bt).Name) : "?";
                    if (baseName != "Attribute")
                        failures.Add($"shim 里有非 Attribute 类型: {smr.GetString(td.Namespace)}.{name} : {baseName} " +
                                     "—— 带运行时语义的类型不得放进 shim");
                }
                Console.WriteLine($"shim:   {shim}.dll (已校验只含 Attribute)");
            }
            else notes.Add($"未找到 shim dll 以校验纯度: {shimPath}");
        }

        if (notes.Count > 0) { Console.WriteLine("--- 允许的 Unity 面 ---"); notes.ForEach(Console.WriteLine); }
        if (failures.Count == 0) { Console.WriteLine("\nPASS —— 纯 C# 层没有沾上 UnityEngine。"); return 0; }
        Console.WriteLine("\nFAIL:");
        foreach (var f in failures.Distinct()) Console.WriteLine("  " + f);
        return 1;
    }
}
