using Content.Shared.Access.Components;
using Content.Shared.HL2RP.CID.Components;
using System.Linq;

namespace Content.Server.HL2RP.Denunciations.Systems;

public sealed class DenunciationsSystem : EntitySystem
{
    private readonly List<DenunciationEntry> _entries = new();
    private int _nextId = 1;

    public event Action? ReportsChanged;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CIDCardComponent, EntityTerminatingEvent>(OnCidTerminating);
    }

    private void OnCidTerminating(Entity<CIDCardComponent> ent, ref EntityTerminatingEvent args)
    {
        var removed = _entries.RemoveAll(x =>
            x.TargetCard == ent.Owner ||
            x.ReporterCard == ent.Owner ||
            x.ResolverCard == ent.Owner) > 0;

        if (removed)
            ReportsChanged?.Invoke();
    }

    public IReadOnlyList<DenunciationEntry> GetEntriesSortedBySeverity()
    {
        _entries.Sort((a, b) =>
        {
            var severityOrder = b.Severity.CompareTo(a.Severity);
            return severityOrder != 0 ? severityOrder : a.Id.CompareTo(b.Id);
        });
        return _entries;
    }

    public DenunciationEntry? GetEntry(int id)
    {
        return _entries.FirstOrDefault(x => x.Id == id);
    }

    public bool Submit(EntityUid reporterCard, EntityUid targetCard, string reason, int severity)
    {
        if (!TryComp<CIDCardComponent>(reporterCard, out _))
            return false;
        if (!TryComp<CIDCardComponent>(targetCard, out _))
            return false;

        var trimmed = reason.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return false;

        if (severity is < 1 or > 5)
            return false;

        if (trimmed.Length > 512)
            trimmed = trimmed[..512];

        _entries.Add(new DenunciationEntry(
            _nextId++,
            reporterCard,
            targetCard,
            trimmed,
            severity));
        ReportsChanged?.Invoke();
        return true;
    }

    public bool TryTake(int id, EntityUid resolverCard)
    {
        if (!TryComp<CIDCardComponent>(resolverCard, out _))
            return false;

        var entry = GetEntry(id);
        if (entry == null || entry.ResolverCard != null)
            return false;

        entry.ResolverCard = resolverCard;
        ReportsChanged?.Invoke();
        return true;
    }

    public bool TryCancel(int id, EntityUid resolverCard)
    {
        var entry = GetEntry(id);
        if (entry == null || entry.ResolverCard != resolverCard)
            return false;

        entry.ResolverCard = null;
        ReportsChanged?.Invoke();
        return true;
    }

    public bool TryAccept(int id, EntityUid resolverCard)
    {
        var entry = GetEntry(id);
        if (entry == null || entry.ResolverCard != resolverCard)
            return false;

        ApplyReporterLpDelta(entry.ReporterCard, SeverityToLp(entry.Severity));
        _entries.Remove(entry);
        ReportsChanged?.Invoke();
        return true;
    }

    public bool TryReject(int id, EntityUid resolverCard)
    {
        var entry = GetEntry(id);
        if (entry == null || entry.ResolverCard != resolverCard)
            return false;

        ApplyReporterLpDelta(entry.ReporterCard, -SeverityToLp(entry.Severity));
        _entries.Remove(entry);
        ReportsChanged?.Invoke();
        return true;
    }

    private void ApplyReporterLpDelta(EntityUid reporterCard, int delta)
    {
        if (!TryComp<CIDCardComponent>(reporterCard, out var cid))
            return;

        cid.LPCount = Math.Clamp(cid.LPCount + delta, -9999, 9999);
        Dirty(reporterCard, cid);
    }

    private static int SeverityToLp(int severity)
    {
        return severity switch
        {
            1 => 5,
            2 => 8,
            3 => 10,
            4 => 15,
            _ => 20
        };
    }

    public sealed class DenunciationEntry
    {
        public int Id { get; }
        public EntityUid ReporterCard { get; }
        public EntityUid TargetCard { get; }
        public string Reason { get; }
        public int Severity { get; }
        public EntityUid? ResolverCard { get; set; }

        public DenunciationEntry(int id, EntityUid reporterCard, EntityUid targetCard, string reason, int severity)
        {
            Id = id;
            ReporterCard = reporterCard;
            TargetCard = targetCard;
            Reason = reason;
            Severity = severity;
        }
    }
}
