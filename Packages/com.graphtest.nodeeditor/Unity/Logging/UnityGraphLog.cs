// UnityGraphLog.cs —— 把纯层的 IGraphLog 转发到 UnityEngine.Debug。NodeEditor.Unity 程序集。
//
// 纯层不能认识 Debug（那正是 0.0.x 里 NodeRegistry 那一处 Debug.LogError 卡住脱 Unity 的原因），
// 但执行器确实需要在检测到病态图时把话说出来。于是纯层只声明 IGraphLog，
// 由这里在 Unity 侧接上真正的出口。
//
// 自动装载：[RuntimeInitializeOnLoadMethod] 在进入播放模式时把 GraphLog.Current 换成本实例，
// 因此即便调用方没有显式传 log（如第三方直接 new 一个 runner），Unity 里也照样能看到报错。
// dotnet test / 服务器侧不会执行这段，GraphLog.Current 保持为 Null 实现，控制台干净。

using UnityEngine;

namespace NodeEditor
{
    public sealed class UnityGraphLog : IGraphLog
    {
        public static readonly UnityGraphLog Instance = new UnityGraphLog();
        UnityGraphLog() { }

        public void Warning(string message) => Debug.LogWarning(message);
        public void Error(string message) => Debug.LogError(message);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() => GraphLog.Current = Instance;
    }
}
