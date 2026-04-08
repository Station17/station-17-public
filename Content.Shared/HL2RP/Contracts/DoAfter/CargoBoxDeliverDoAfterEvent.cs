using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.HL2RP.Contracts.DoAfter;

[Serializable, NetSerializable]
public sealed partial class CargoBoxDeliverDoAfterEvent : SimpleDoAfterEvent
{
}
