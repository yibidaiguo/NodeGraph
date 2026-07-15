using NodeEditor;

namespace TaskEditor
{
    [NodeMenu("Task/Steps/Wait Event")]
    [NodeDoc(Language.English, "Wait Event", "Waits for an external gameplay event before continuing.")]
    [NodeDoc(Language.Chinese, "等待事件", "等待外部游戏事件后继续。")]
    [ParamDoc(Language.English, "eventId", "Event Id", "External event id listened for by the task runner.")]
    [ParamDoc(Language.Chinese, "eventId", "事件 ID", "任务运行器监听的外部事件 ID。")]
    [ParamDoc(Language.English, "payloadKey", "Payload Key", "Optional blackboard key receiving the event payload.")]
    [ParamDoc(Language.Chinese, "payloadKey", "载荷键", "可选：接收事件载荷的黑板键。")]
    public class TaskWaitEventNode : TaskNodeDefinition
    {
        public override TaskNodeKind Kind => TaskNodeKind.WaitEvent;

        protected override void Define()
        {
            Meta("Wait Event", NodeRole.Control);
            AddParam("eventId", TypeRef.String);
            AddParam("payloadKey", TypeRef.BBKey());
            AddIn("in", Arity.Many);
            AddOut("received", Arity.ExactlyOne);
        }
    }
}
