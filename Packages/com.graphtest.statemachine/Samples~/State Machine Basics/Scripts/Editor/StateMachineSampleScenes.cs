// StateMachineSampleScenes.cs —— 在 NodeGraph Manager 中注册 3D/2D 样例场景生成动作。
// 幂等：场景文件已存在则直接打开（不重建、不覆盖手工改动）；缺则新建、装配、存盘到 Sample/Scenes/。
// 场景引用的三张样例图/两个样例组黑板随已导入样例提供；产品 Setup 不生成或依赖样例。
// 本类只做「场景装配」：
// 图按名字+module 定位，注册表经 NodeRegistryLocator，黑板按「全局→模块→组」经 BlackboardLocator 取
//（分层黑板准则#15：StateMachinePlayer.blackboards 由外到内排列，同名 key 就近覆盖）。
// Dialogue Sample 没有 editor 脚本可照抄（其场景是手工存的静态资产），故本域把场景装配做成
// 只含 Editor 平台的独立程序集（StateMachine.Sample.Editor）——Runtime 构建绝不带菜单代码（红线 §6）。
// 静态菜单类非 SO/MonoBehaviour，一文件即可（硬规则 A1 不涉及）。仅编辑器期可用。

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NodeEditor;
using NodeEditor.EditorUI;   // NodeRegistryLocator + BlackboardLocator

namespace StateMachine.Sample.EditorUI
{
    [InitializeOnLoad]
    public static class StateMachineSampleScenes
    {
        const string Module = "statemachine";

        // 场景目录由本脚本自身的导入位置推导（<样例根>/Scenes）——不把包 displayName/版本烘进路径
        //（1.0 曾硬编码 "Assets/Samples/<displayName>/<version>/…"，包改名/升版即断；package identity 单一所有权）。
        // 脚本位于 <样例根>/Scripts/Editor/StateMachineSampleScenes.cs → 上溯两级到样例根。
        static string ScenesDir
        {
            get
            {
                foreach (var guid in AssetDatabase.FindAssets($"{nameof(StateMachineSampleScenes)} t:MonoScript"))
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                    if (Path.GetFileNameWithoutExtension(p) != nameof(StateMachineSampleScenes)) continue;
                    var editorDir = Path.GetDirectoryName(p)?.Replace('\\', '/');
                    var scriptsDir = Path.GetDirectoryName(editorDir)?.Replace('\\', '/');
                    var sampleRoot = Path.GetDirectoryName(scriptsDir)?.Replace('\\', '/');
                    if (!string.IsNullOrEmpty(sampleRoot)) return $"{sampleRoot}/Scenes";
                }
                return null;
            }
        }

        static StateMachineSampleScenes() => EditorApplication.delayCall += RegisterActions;

        static void RegisterActions()
        {
            var actions = new[]
            {
                new NodeGraphModuleAction("create-sample-3d", "Create Sample Scene (3D)", Create3D),
                new NodeGraphModuleAction("create-sample-2d", "Create Sample Scene (2D)", Create2D),
            };
            if (!NodeGraphModules.Registry.TryAddActions("com.graphtest.statemachine", actions, out var error))
                Debug.LogError(error);
        }

        public static void Create3D()
        {
            var scenesDir = ScenesDir;
            if (scenesDir == null) { WarnNoSampleRoot(); return; }
            var scenePath = scenesDir + "/StateMachineSample3D.unity";
            if (OpenIfExists(scenePath)) return;
            var graph = FindGraph("SamplePlayer3D");
            if (graph == null) { WarnMissingData("SamplePlayer3D"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // DefaultGameObjects = 主相机 + 平行光。
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 地面。
            var ground = GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);

            // 玩家胶囊：CharacterController（本身就是碰撞体，去掉原生 CapsuleCollider 防双碰撞体打架）
            // + StateMachinePlayer + 输入注入 + 3D 电机 + HUD。
            var player = GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Capsule);
            player.name = "Player";
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.transform.position = new Vector3(0f, 1.1f, 0f);
            player.AddComponent<CharacterController>();
            var smp = player.AddComponent<StateMachinePlayer>();
            smp.graph = graph;
            smp.registry = NodeRegistryLocator.Find();
            smp.blackboards = Layers("SamplePlayer3D");   // 全局→模块→组
            player.AddComponent<SampleBlackboardInputWriter>();
            player.AddComponent<SamplePlayerMotor3D>();
            var hud = player.AddComponent<SampleMachineHud>();
            hud.watchKeys = new[] { "moveX", "moveZ", "jumpPressed", "isGrounded", "moveSpeed" };

            // 主相机：跟随适配器（订阅 onMachineEvent 切近/远档——待机态抛 camera.near、移动态抛 camera.far）。
            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.gameObject.AddComponent<SampleCameraFollowAdapter>();
                follow.player = smp;
                follow.target = player.transform;
                cam.transform.position = player.transform.position + follow.defaultOffset;
                cam.transform.LookAt(player.transform.position + Vector3.up * 1.5f);
            }

            SaveScene(scene, scenePath);
        }

        public static void Create2D()
        {
            var scenesDir = ScenesDir;
            if (scenesDir == null) { WarnNoSampleRoot(); return; }
            var scenePath = scenesDir + "/StateMachineSample2D.unity";
            if (OpenIfExists(scenePath)) return;
            var graph = FindGraph("SampleEnemy2D");
            if (graph == null) { WarnMissingData("SampleEnemy2D"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 相机转正交（2D 俯视 XY 平面）。
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 7f;
                cam.transform.position = new Vector3(0f, 0f, -10f);
                cam.transform.rotation = Quaternion.identity;
            }

            // 目标方块：在 Scene 视图里沿 X 拖动它，观察敌人 巡逻→追击→攻击→脱战。
            var target = GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
            target.name = "PlayerTarget";
            Object.DestroyImmediate(target.GetComponent<BoxCollider>());   // 纯视觉标记，不参与物理
            target.transform.position = new Vector3(15f, 0f, 0f);          // 起始在脱战阈值（12）+巡逻幅度之外 → 敌人自己巡逻不会误触发战斗

            // Rigidbody2D 敌人：FixedUpdate 驱动模式与 2D 物理配套（感知/电机也都在 FixedUpdate 写读黑板）。
            var enemy = GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
            enemy.name = "Enemy";
            Object.DestroyImmediate(enemy.GetComponent<BoxCollider>());
            enemy.transform.position = Vector3.zero;
            var rb = enemy.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;        // 俯视样例无重力
            rb.freezeRotation = true;
            var smp = enemy.AddComponent<StateMachinePlayer>();
            smp.graph = graph;
            smp.registry = NodeRegistryLocator.Find();
            smp.blackboards = Layers("SampleEnemy2D");   // 全局→模块→组（子机 SampleEnemyCombat 同组共用）
            smp.updateMode = StateMachineUpdateMode.FixedUpdate;
            var perception = enemy.AddComponent<SampleEnemyPerception2D>();
            perception.target = target.transform;
            var motor = enemy.AddComponent<SampleEnemyMotor2D>();
            motor.target = target.transform;
            var hud = enemy.AddComponent<SampleMachineHud>();
            hud.watchKeys = new[] { "playerDistance", "moveSpeed", "chasePlayer", "stunned" };

            SaveScene(scene, scenePath);
        }

        // ---- 共用小助手 ----

        // 幂等入口：场景已存在 → 打开（询问保存当前改动）并返回 true。
        static bool OpenIfExists(string scenePath)
        {
            if (!File.Exists(scenePath)) return false;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(scenePath);
            return true;
        }

        // 按名字 + module 标签定位样例图（不硬编码目录——图落点由 StateMachineAssetPaths SO 决定，准则#14）。
        static NodeGraphAsset FindGraph(string graphName) =>
            AssetDatabase.FindAssets($"{graphName} t:{nameof(NodeGraphAsset)}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(a => a != null && a.name == graphName && a.module == Module);

        // 分层黑板引用（全局→模块→组，由外到内；缺档跳过——BlackboardSet 语义按标签合并、就近覆盖）。
        static BlackboardAsset[] Layers(string group) =>
            new[] { BlackboardLocator.FindGlobal(), BlackboardLocator.FindLayer(Module, ""), BlackboardLocator.FindLayer(Module, group) }
                .Where(b => b != null).ToArray();

        static void WarnMissingData(string graphName) =>
            Debug.LogError($"StateMachineSampleScenes: 找不到样例图「{graphName}」（module=\"{Module}\"）。" +
                           "请确认已导入样例的 Data 目录完整；产品 Setup 不会生成样例数据。");

        static void WarnNoSampleRoot() =>
            Debug.LogError("StateMachineSampleScenes: 无法定位样例根目录（本脚本的资产路径未找到）。请重新导入样例。");

        static void SaveScene(UnityEngine.SceneManagement.Scene scene, string scenePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));   // 已导入样例的 Scenes/ 首次生成时还不存在
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();
            Debug.Log($"StateMachineSampleScenes: 样例场景已生成并打开：{scenePath}（Play 即可试玩，见样例根目录 README.md）。");
        }
    }
}
