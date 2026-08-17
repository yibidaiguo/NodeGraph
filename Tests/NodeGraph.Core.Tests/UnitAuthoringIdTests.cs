// UnitAuthoringIdTests.cs —— Unit 创作类型 id 的纯 C# 契约门禁。
// id 必须独立于 CLR 类型名保持稳定；同时守住 Unit 四角色基类的运行时红线。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NodeEditor;
using Xunit;

namespace NodeGraph.Core.Tests
{
    public class UnitAuthoringIdTests
    {
        static readonly IReadOnlyDictionary<Type, string> ExpectedIds =
            new Dictionary<Type, string>
            {
                [typeof(ConstProvider)] = "core.const",
                [typeof(BlackboardProvider)] = "core.blackboard-read",
                [typeof(ArithmeticProvider)] = "core.arithmetic",
                [typeof(CompareCondition)] = "core.compare",
                [typeof(BlackboardCompareCondition)] = "core.blackboard-compare",
                [typeof(AlwaysCondition)] = "core.always",
                [typeof(NotCondition)] = "core.not",
                [typeof(AndCondition)] = "core.and",
                [typeof(OrCondition)] = "core.or",
                [typeof(SetVariableAction)] = "core.set-variable",
                [typeof(SetVariableLiteralAction)] = "core.set-variable-literal",
                [typeof(SequenceAction)] = "core.sequence-action",
                [typeof(ConditionalAction)] = "core.conditional-action",
                [typeof(ConditionControl)] = "core.condition-control",
                [typeof(SelectorControl)] = "core.selector",
                [typeof(SequenceControl)] = "core.sequence-control",
                [typeof(ParallelControl)] = "core.parallel",
                [typeof(InverterControl)] = "core.inverter",
            };

        [Fact]
        public void FrameworkConcreteUnitsHaveStableUniqueAuthoringIds()
        {
            var concreteUnits = typeof(Unit).Assembly.GetTypes()
                .Where(type => typeof(Unit).IsAssignableFrom(type) && !type.IsAbstract)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(
                ExpectedIds.Keys.OrderBy(type => type.FullName, StringComparer.Ordinal),
                concreteUnits);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var unitType in concreteUnits)
            {
                var attribute = unitType.GetCustomAttribute<UnitAuthoringIdAttribute>(inherit: false);
                Assert.NotNull(attribute);
                Assert.False(string.IsNullOrWhiteSpace(attribute.Id));
                Assert.Equal(ExpectedIds[unitType], attribute.Id);
                Assert.True(ids.Add(attribute.Id), $"Unit 创作类型 id 重复：{attribute.Id}");
            }
        }

        [Fact]
        public void UnitRoleBasesKeepSerializationAndRuntimeSeams()
        {
            var unitRole = typeof(Unit).GetProperty(nameof(Unit.Role));
            Assert.NotNull(unitRole);
            Assert.True(unitRole.GetMethod.IsAbstract);
            Assert.True(HasSerializableAttribute(typeof(Unit)));

            foreach (var contract in new (Type RoleBase, Unit Sample, NodeRole ExpectedRole)[]
                     {
                         (typeof(ActionUnit), new SetVariableAction(), NodeRole.Action),
                         (typeof(ConditionUnit), new AlwaysCondition(), NodeRole.Condition),
                         (typeof(ProviderUnit), new ConstProvider(), NodeRole.Provider),
                         (typeof(ControlUnit), new SelectorControl(), NodeRole.Control),
                     })
            {
                var roleBase = contract.RoleBase;
                Assert.True(roleBase.IsAbstract);
                Assert.Equal(typeof(Unit), roleBase.BaseType);
                Assert.True(HasSerializableAttribute(roleBase));

                var role = roleBase.GetProperty(nameof(Unit.Role));
                Assert.NotNull(role);
                Assert.Equal(roleBase, role.DeclaringType);
                Assert.True(role.GetMethod.IsVirtual);
                Assert.True(role.GetMethod.IsFinal);
                Assert.Equal(contract.ExpectedRole, contract.Sample.Role);

                Assert.False(typeof(ITickNode).IsAssignableFrom(roleBase));
                Assert.False(typeof(IDataflowNode).IsAssignableFrom(roleBase));
                Assert.False(typeof(IControlFlowNode).IsAssignableFrom(roleBase));
            }
        }

        static bool HasSerializableAttribute(Type type) =>
            type.IsDefined(typeof(SerializableAttribute), inherit: false);
    }
}
