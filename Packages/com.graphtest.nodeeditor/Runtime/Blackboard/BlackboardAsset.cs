// BlackboardAsset.cs —— 子层 4a（节点数据）ScriptableObject。
// 必须放在与类同名的独立文件中：Unity 只会把 MonoScript 绑定到名称与文件名匹配的类型，
// 而没有绑定 MonoScript 的 ScriptableObject 在创建每个 .asset 时都会序列化成
// m_Script: {fileID: 0} —— 这是损坏的，无法通过 AssetDatabase.FindAssets("t:BlackboardAsset") 找到。
// 命名空间 NodeEditor。Runtime/ 程序集（可在构建中使用，并非仅限编辑器）。
//
// 分层作用域：作用域 = 这块 asset 在层级里的位置，由两个标签字段表达——
//   · module=="" && group=="" → 全局（项目级，每项目一块）；
//   · module=="X" && group=="" → 模块「X」的黑板；
//   · module=="X" && group=="Y" → 模块「X」下分组「Y」的黑板。
// 不再有每变量的 scope 字段：在哪块 SO 里编辑就是哪个作用域。运行/校验时由 BlackboardSet 按标签把
// 适用各档合并（同名 key 就近覆盖）。标签是普通 [SerializeField] 字符串，默认 Inspector 即可编辑。

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NodeEditor
{
    [CreateAssetMenu(menuName = "NodeEditor/Blackboard")]
    public class BlackboardAsset : ScriptableObject
    {
        // 作用域标签（见文件头）。空模块=全局；带模块、空组=模块级；模块+组=组级。
        // 框架只认这两个字符串、不认任何领域语义；"我属于哪个模块/组"由创作者直接在 Inspector 里填，
        // 或由领域 Setup 播种。非法组合（module 空但 group 非空）由校验暴露。
        [SerializeField] private string module = "";
        [SerializeField] private string group = "";
        public string Module => module;
        public string Group => group;

        [SerializeField] private List<VariableDef> variables = new();
        public IReadOnlyList<VariableDef> Variables => variables;
        public bool Has(string key) => variables.Any(v => v.key == key);
        public VariableDef Find(string key) => variables.FirstOrDefault(v => v.key == key);
        public IEnumerable<string> KeysOfType(TypeRef expected) =>
            variables.Where(v => expected == null || TypeRefCompat.Compatible(v.type, expected)).Select(v => v.key);
        public IEnumerable<VariableDef> All() => variables;
        public void AddVariable(string key, TypeRef type) =>
            variables.Add(new VariableDef { key = key, type = type });
    }
}
