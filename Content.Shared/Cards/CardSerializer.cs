using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared.Cards;

[TypeSerializer]
public sealed class CardDataSerializer : ITypeSerializer<CardData, ValueDataNode>
{
    public CardData Read(
        ISerializationManager mgr,
        ValueDataNode node,
        IDependencyCollection deps,
        SerializationHookContext ctx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<CardData>? instanceProvider = null
    )
    {
        var protoMan = deps.Resolve<IPrototypeManager>();
        if (!protoMan.TryIndex<CardPrototype>(node.Value, out var proto))
            throw new InvalidMappingException($"Unknown card prototype: {node.Value} at {node.Start}");
        return new CardData(proto);
    }

    public ValidationNode Validate(
        ISerializationManager mgr,
        ValueDataNode node,
        IDependencyCollection deps,
        ISerializationContext? context = null
    )
    {
        var protoMan = deps.Resolve<IPrototypeManager>();
        return protoMan.HasIndex<CardPrototype>(node.Value)
            ? new ValidatedValueNode(node)
            : new ErrorNode(node, $"Unknown card prototype: {node.Value}");
    }

    public DataNode Write(
        ISerializationManager mgr,
        CardData value,
        IDependencyCollection deps,
        bool alwaysWrite = false,
        ISerializationContext? context = null
    )
    {
        return new ValueDataNode(value.CardId.ToString());
    }

    public CardData Copy(
        ISerializationManager mgr,
        CardData source,
        CardData target,
        IDependencyCollection deps,
        SerializationHookContext ctx,
        ISerializationContext? context = null
    )
    {
        return source;
    }
}
