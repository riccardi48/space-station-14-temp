using Robust.Shared.Serialization;

namespace Content.Shared.Cards
{
    [DataDefinition, NetSerializable, Serializable]
    public partial struct CardData
    {
        [DataField]
        public int CardId { get; private set; }
    }
}
