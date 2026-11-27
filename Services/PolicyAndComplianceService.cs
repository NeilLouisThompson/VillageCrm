using Microsoft.Extensions.Options;
using VillageCrm.Data;
using VillageCrm.Models;

namespace VillageCrm.Services;

public class PolicyService(
    IRepository<Policy> policies,
    IRepository<ComplianceItem> compliance,
    IRepository<StaffMember> staff,
    IRepository<Resident> residents,
    TimeProvider time)
{
    private DateTime Now => time.GetLocalNow().DateTime;

    public async Task<List<Policy>> ListAsync(bool residentView)
    {
        var all = (await policies.ListAsync()).OrderBy(p => p.Title).ToList();
        return residentView
            ? all.Where(p => p.Audience != PolicyAudience.Staff && p.Current is not null).ToList()
            : all;
    }

    public async Task<Policy> CreateAsync(string title, string owner, PolicyAudience audience, string body)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Give the policy a title.");
        }
        return await policies.AddAsync(new Policy
        {
            Title = title.Trim(),
            Owner = owner.Trim(),
            Audience = audience,
            Versions = [new PolicyVersion { Number = 1, Status = PolicyStatus.Draft, Body = body }]
        });
    }

    public async Task NewDraftAsync(int policyId)
    {
        var policy = await RequireAsync(policyId);
        if (policy.Pending is not null)
        {
            throw new InvalidOperationException("There is already a draft or a version under review.");
        }
        policy.Versions.Add(new PolicyVersion
        {
            Number = policy.Versions.Max(v => v.Number) + 1,
            Status = PolicyStatus.Draft,
            Body = policy.Current?.Body ?? ""
        });
        await policies.UpdateAsync(policy);
    }

    public async Task EditDraftAsync(int policyId, string body)
    {
        var policy = await RequireAsync(policyId);
        var draft = policy.Versions.FirstOrDefault(v => v.Status == PolicyStatus.Draft)
            ?? throw new InvalidOperationException("Only drafts can be edited. Approved and old versions are read-only.");
        draft.Body = body;
        await policies.UpdateAsync(policy);
    }

    public async Task SubmitForReviewAsync(int policyId)
    {
        var policy = await RequireAsync(policyId);
        var draft = policy.Versions.FirstOrDefault(v => v.Status == PolicyStatus.Draft)
            ?? throw new InvalidOperationException("There is no draft to submit.");
        draft.Status = PolicyStatus.Review;
        await policies.UpdateAsync(policy);
    }

    /// <summary>Approves the version under review, supersedes the old one and books the review date in the compliance calendar.</summary>
    public async Task ApproveAsync(int policyId, DateTime effectiveDate, DateTime reviewDate, UserRole role, string actor)
    {
        if (!Access.Allows(role, Area.ManagePolicies))
        {
            throw new UnauthorizedAccessException($"Policies can be approved by: {Access.WhoMay(Area.ManagePolicies)}.");
        }
        if (reviewDate.Date <= effectiveDate.Date)
        {
            throw new ArgumentException("The review date must be after the effective date.");
        }

        var policy = await RequireAsync(policyId);
        var review = policy.Versions.FirstOrDefault(v => v.Status == PolicyStatus.Review)
            ?? throw new InvalidOperationException("There is no version under review.");

        foreach (var old in policy.Versions.Where(v => v.Status == PolicyStatus.Approved))
        {
            old.Status = PolicyStatus.Superseded;
        }
        review.Status = PolicyStatus.Approved;
        review.EffectiveDate = effectiveDate.Date;
        review.ReviewDate = reviewDate.Date;
        review.ApprovedBy = actor;
        await policies.UpdateAsync(policy);

        var title = $"Policy review: {policy.Title}";
        var existing = (await compliance.ListAsync(c => c.Title == title && c.ClosedAt == null)).FirstOrDefault();
        if (existing is null)
        {
            await compliance.AddAsync(new ComplianceItem
            {
                Title = title,
                Law = "Internal policy",
                Requirement = "Review and re-approve the policy",
                Frequency = Frequency.Once,
                DueDate = reviewDate.Date,
                Owner = policy.Owner,
                EvidenceRequired = "Approved new version"
            });
        }
        else
        {
            existing.DueDate = reviewDate.Date;
            await compliance.UpdateAsync(existing);
        }
    }

    public async Task AcknowledgeAsync(int policyId, string person)
    {
        var policy = await RequireAsync(policyId);
        var current = policy.Current ?? throw new InvalidOperationException("This policy has no approved version.");
        if (HasAcknowledged(policy, person))
        {
            return;
        }
        policy.Acknowledgements.Add(new PolicyAcknowledgement { VersionNumber = current.Number, Person = person, At = Now });
        await policies.UpdateAsync(policy);
    }

    public static bool HasAcknowledged(Policy policy, string person) =>
        policy.Current is { } current
        && policy.Acknowledgements.Any(a => a.VersionNumber == current.Number && a.Person == person);

    public async Task<int> TargetCountAsync(Policy policy)
    {
        var staffCount = (await staff.ListAsync()).Count;
        var residentCount = (await residents.ListAsync()).Count(r => r.IsActive);
        return policy.Audience switch
        {
            PolicyAudience.Staff => staffCount,
            PolicyAudience.Residents => residentCount,
            _ => staffCount + residentCount
        };
    }

    public static double AcknowledgementRate(Policy policy, int targetCount)
    {
        if (policy.Current is not { } current || targetCount == 0)
        {
            return 0;
        }
        var count = policy.Acknowledgements.Where(a => a.VersionNumber == current.Number).Select(a => a.Person).Distinct().Count();
        return Math.Min(1, (double)count / targetCount);
    }

    private async Task<Policy> RequireAsync(int id) =>
        await policies.GetAsync(id) ?? throw new ArgumentException("Unknown policy.");
}

public enum ComplianceState { OnTrack, DueSoon, Acknowledged, Overdue, Closed }

public class ComplianceService(IRepository<ComplianceItem> items, IOptions<VillageOptions> options, TimeProvider time)
{
    private DateTime Now => time.GetLocalNow().DateTime;

    public async Task<List<ComplianceItem>> ListAsync(bool includeClosed) =>
        (await items.ListAsync(c => includeClosed || c.ClosedAt == null)).OrderBy(c => c.DueDate).ToList();

    public ComplianceState StateOf(ComplianceItem item)
    {
        if (item.ClosedAt is not null) return ComplianceState.Closed;
        if (item.DueDate.Date < Now.Date) return ComplianceState.Overdue;
        if (item.AcknowledgedBy is not null) return ComplianceState.Acknowledged;
        if ((item.DueDate.Date - Now.Date).TotalDays <= options.Value.DueSoonDays) return ComplianceState.DueSoon;
        return ComplianceState.OnTrack;
    }

    /// <summary>Overdue items escalate to the village manager.</summary>
    public bool IsEscalated(ComplianceItem item) => StateOf(item) == ComplianceState.Overdue;

    /// <summary>The reminder that applies today (e.g. 7 for "7 days to go"), or null.</summary>
    public int? ReminderDays(ComplianceItem item)
    {
        if (item.ClosedAt is not null) return null;
        var daysLeft = (int)(item.DueDate.Date - Now.Date).TotalDays;
        if (daysLeft < 0) return null;
        var matches = options.Value.ComplianceReminderDays.Where(d => daysLeft <= d).ToList();
        return matches.Count == 0 ? null : matches.Min();
    }

    public async Task AddAsync(ComplianceItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Owner))
        {
            throw new ArgumentException("Give a title and a responsible person.");
        }
        await items.AddAsync(item);
    }

    public async Task ChangeDueDateAsync(int id, DateTime dueDate)
    {
        var item = await RequireAsync(id);
        item.DueDate = dueDate.Date;
        await items.UpdateAsync(item);
    }

    public async Task AcknowledgeAsync(int id, string actor)
    {
        var item = await RequireAsync(id);
        item.AcknowledgedBy = actor;
        item.AcknowledgedAt = Now;
        await items.UpdateAsync(item);
    }

    /// <summary>Closes the item with evidence. Repeating items get their next occurrence created.</summary>
    public async Task CloseAsync(int id, string evidence, string actor)
    {
        if (string.IsNullOrWhiteSpace(evidence))
        {
            throw new ArgumentException("Evidence is needed to close this item.");
        }
        var item = await RequireAsync(id);
        if (item.ClosedAt is not null)
        {
            throw new InvalidOperationException("This item is already closed.");
        }

        item.Evidence = evidence.Trim();
        item.ClosedBy = actor;
        item.ClosedAt = Now;
        await items.UpdateAsync(item);

        if (item.Frequency.Next(item.DueDate) is { } next)
        {
            await items.AddAsync(new ComplianceItem
            {
                Title = item.Title,
                Law = item.Law,
                Requirement = item.Requirement,
                Frequency = item.Frequency,
                DueDate = next,
                Owner = item.Owner,
                EvidenceRequired = item.EvidenceRequired,
                VerifyWithLegalAdvisor = item.VerifyWithLegalAdvisor
            });
        }
    }

    private async Task<ComplianceItem> RequireAsync(int id) =>
        await items.GetAsync(id) ?? throw new ArgumentException("Unknown compliance item.");
}
