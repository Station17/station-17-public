using Content.Shared.HL2RP.CID.Components;
using Robust.Shared.Random;

namespace Content.Server.HL2RP.CID.Services;

public sealed class CIDNumberGenerator
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public void Initialize()
    {
        IoCManager.InjectDependencies(this);
    }

    public string GenerateUniqueNumber()
    {
        for (var i = 0; i < 200; i++)
        {
            var number = _random.Next(0, 1_000_000).ToString("D6");
            if (!IsNumberTaken(number))
                return number;
        }

        return _random.Next(0, 1_000_000).ToString("D6");
    }

    public bool IsNumberTaken(string number)
    {
        var query = IoCManager.Resolve<IEntityManager>().EntityQueryEnumerator<CIDCardComponent>();
        while (query.MoveNext(out _, out var cid))
        {
            if (cid.CNumber == number)
                return true;
        }

        return false;
    }
}
