// GraphLog.cs —— 纯层的日志出口。Runtime 程序集（纯层）。
//
// 替换 0.0.x 里散落的 UnityEngine.Debug.LogError。纯层不能认识 Debug，但执行器确实需要在
// 检测到病态图（如 Jump<->Label 的即时节点环）时把话说出来，而不是静默吞掉或直接抛异常。
//
// 默认实现什么都不做（Null 对象），这样 dotnet test / 服务器侧不会往控制台喷东西；
// Unity 侧在启动时把 Current 换成转发到 Debug 的实现（见 Unity 层 UnityGraphLog）。
// 注入点是静态属性而非构造参数：执行器有三个、调用点分散，静态默认 + 可替换是这里的
// 最小侵入解；需要按实例隔离日志时，执行器也接受构造期传入的 IGraphLog（优先于 Current）。

namespace NodeEditor
{
    public interface IGraphLog
    {
        void Warning(string message);
        void Error(string message);
    }

    // 全局默认出口。Unity 侧在 [RuntimeInitializeOnLoadMethod] 里替换成转发到 Debug 的实现。
    public static class GraphLog
    {
        public static IGraphLog Current { get; set; } = NullGraphLog.Instance;

        public static void Warning(string message) => Current?.Warning(message);
        public static void Error(string message) => Current?.Error(message);
    }

    public sealed class NullGraphLog : IGraphLog
    {
        public static readonly NullGraphLog Instance = new NullGraphLog();
        NullGraphLog() { }
        public void Warning(string message) { }
        public void Error(string message) { }
    }

    // 把日志收进内存列表——测试用：断言"病态图确实报了错"，而不是只断言它没崩。
    public sealed class CollectingGraphLog : IGraphLog
    {
        public System.Collections.Generic.List<string> Warnings { get; } = new();
        public System.Collections.Generic.List<string> Errors { get; } = new();
        public void Warning(string message) => Warnings.Add(message);
        public void Error(string message) => Errors.Add(message);
    }
}
