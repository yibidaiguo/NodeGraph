// InteractionSupport.cs — 子层 5b（editor-interaction，编辑器交互）的支撑类型。
// 由添加节点对话框和变量面板使用的定位器（Locator）与弹窗（popup）。
// 归属于 5b，因为 5b 使用它们（依赖方向：5d 组装 5b，而非反过来）。
// 命名空间 NodeEditor.EditorUI。

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using NodeEditor;

namespace NodeEditor.EditorUI
{
    // 为某个 NodeDefinition 子类类型解析其定义资产（被 AddNodeSearchWindow 使用）。
    public static class NodeDefinitionLocator
    {
        public static NodeDefinition ForType(Type defType)
        {
            if (defType == null || !typeof(NodeDefinition).IsAssignableFrom(defType)) return null;
            var registry = NodeRegistryLocator.Find();
            if (registry == null) return null;
            var matches = registry.universal.Concat(registry.projectDomain)
                .Where(definition => definition != null && definition.GetType() == defType)
                .Distinct()
                .ToArray();
            if (matches.Length > 1)
            {
                Debug.LogError($"NodeEditor: registry contains {matches.Length} {defType.Name} definition assets. " +
                               "Keep exactly one registered definition per concrete type before continuing.");
                return null;
            }
            var candidate = matches.FirstOrDefault();
            return candidate != null && ReferenceEquals(registry.Find(candidate.Id), candidate) ? candidate : null;
        }
    }

    // 用于创建黑板变量的弹窗（绑定到 VariablePane 的 "+ Variable" 按钮）。创建逻辑
    // （TryCreate）是一个纯粹、不依赖 UI 的静态方法，因此可做单元测试；Open() 是编辑器入口，
    // 负责显示这个小窗口并委托给它。
    public static class VariableCreatePopup
    {
        // UI 中提供的、可在黑板上编写的基本类型。
        public static readonly string[] TypeNames = { "Bool", "Int", "Float", "String" };

        public static TypeRef TypeForName(string name) => name switch
        {
            "Bool"   => TypeRef.Bool,
            "Int"    => TypeRef.Int,
            "Float"  => TypeRef.Float,
            "String" => TypeRef.String,
            _        => null,
        };

        // 校验并追加一个变量到给定黑板。作用域不在此选择：变量的作用域 = 这块黑板所在的档（全局/模块/组）。
        // 当黑板缺失、名称为空、类型无法识别，或本块黑板内已存在同名变量时，返回 false 并附带原因。
        public static bool TryCreate(BlackboardAsset blackboard, string name, TypeRef type, out string error)
        {
            error = null;
            if (blackboard == null) { error = Localizer.UI("ui.errNoBlackboard", "No blackboard asset."); return false; }
            name = name?.Trim();
            if (string.IsNullOrEmpty(name)) { error = Localizer.UI("ui.errVarNameRequired", "Variable name is required."); return false; }
            if (type == null) { error = Localizer.UI("ui.errPickType", "Pick a type."); return false; }
            if (blackboard.Has(name)) { error = string.Format(Localizer.UI("ui.errVarExists", "A variable named '{0}' already exists in this blackboard."), name); return false; }
            Undo.RegisterCompleteObjectUndo(blackboard, "Create Blackboard Variable");
            blackboard.AddVariable(name, type);
            EditorUtility.SetDirty(blackboard);
            return true;
        }

        public static void Open(BlackboardAsset blackboard, Action onCreated)
        {
            if (blackboard == null)
            {
                EditorUtility.DisplayDialog(Localizer.UI("ui.newVariable", "New Variable"),
                    Localizer.UI("ui.noBlackboard", "No blackboard asset is loaded. Run your module's Setup Assets (or create a Blackboard) first."), "OK");
                return;
            }
            VariableCreateWindow.Show(blackboard, onCreated);
        }
    }

    // 小型浮动窗口：name + type + scope + Create。内部类——入口是 VariableCreatePopup.Open。
    class VariableCreateWindow : EditorWindow
    {
        BlackboardAsset m_Blackboard;
        Action m_OnCreated;

        public static void Show(BlackboardAsset blackboard, Action onCreated)
        {
            var w = CreateInstance<VariableCreateWindow>();
            w.m_Blackboard = blackboard;
            w.m_OnCreated = onCreated;
            w.titleContent = new GUIContent(Localizer.UI("ui.newVariable", "New Variable"));
            w.minSize = w.maxSize = new Vector2(320, 140);
            w.ShowUtility();
        }

        void CreateGUI()
        {
            EditorUi.ConfigureWindow(rootVisualElement);
            var root = rootVisualElement;
            root.style.paddingTop = 8; root.style.paddingBottom = 8; root.style.paddingLeft = 8; root.style.paddingRight = 8;

            var nameField = new TextField(Localizer.UI("ui.varName", "Name"));
            // 有限固定值 → 走共享 EnumDropdownField（原生枚举下拉，规范 §1）。
            var typeField = new EnumDropdownField(Localizer.UI("ui.type", "Type"),
                VariableCreatePopup.TypeNames.ToList(), VariableCreatePopup.TypeNames.First());

            var error = new Label();
            error.AddToClassList(EditorUi.FormErrorClass);
            error.style.whiteSpace = WhiteSpace.Normal;
            error.style.display = DisplayStyle.None;

            var create = new Button(() =>
            {
                if (VariableCreatePopup.TryCreate(m_Blackboard, nameField.value, VariableCreatePopup.TypeForName(typeField.value), out var err))
                {
                    AssetDatabase.SaveAssets();
                    m_OnCreated?.Invoke();
                    Close();
                }
                else { error.text = err; error.style.display = DisplayStyle.Flex; }
            }) { text = Localizer.UI("ui.create", "Create") };

            root.Add(nameField);
            root.Add(typeField);
            root.Add(error);
            root.Add(create);
            nameField.Focus();
        }
    }
}
