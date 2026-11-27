using Microsoft.Extensions.Options;
using VillageCrm.Data;
using VillageCrm.Models;

namespace VillageCrm.Services;

public class ProjectService(
    IRepository<Project> projects,
    IRepository<Contractor> contractors,
    OutboxService outbox,
    IOptions<VillageOptions> options,
    TimeProvider time)
{
    private VillageOptions Options => options.Value;
    private DateTime Now => time.GetLocalNow().DateTime;

    public async Task<List<Project>> ListAsync() => (await projects.ListAsync()).OrderBy(p => p.Status).ThenBy(p => p.Name).ToList();

    public Task<Project?> GetAsync(int id) => projects.GetAsync(id);

    public async Task<Project> CreateAsync(string name, string scope, string costCentre, decimal budget)
    {
        if (string.IsNullOrWhiteSpace(name) || budget <= 0)
        {
            throw new ArgumentException("Give a name and a budget above zero.");
        }
        return await projects.AddAsync(new Project { Name = name.Trim(), Scope = scope.Trim(), CostCentre = costCentre.Trim(), Budget = budget });
    }

    public async Task AddQuoteAsync(int projectId, int contractorId, decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("The quote must be more than zero.");
        }
        var project = await RequireOpenAsync(projectId);
        var contractor = await contractors.GetAsync(contractorId) ?? throw new ArgumentException("Unknown contractor.");
        project.Quotes.Add(new Quote
        {
            Number = project.Quotes.Count + 1,
            ContractorId = contractor.Id,
            ContractorName = contractor.Company,
            Amount = amount,
            ReceivedOn = Now.Date
        });
        await projects.UpdateAsync(project);
    }

    /// <summary>Returns the reasons appointment is blocked; empty means the contractor was appointed.</summary>
    public async Task<List<string>> AppointAsync(int projectId, int quoteNumber)
    {
        var project = await RequireOpenAsync(projectId);
        var problems = new List<string>();
        var distinctQuotes = DistinctQuoteCount(project);
        if (distinctQuotes < Options.MinimumQuotes)
        {
            problems.Add($"At least {Options.MinimumQuotes} quotes from different contractors are needed (there are {distinctQuotes})");
        }

        var quote = project.Quotes.FirstOrDefault(q => q.Number == quoteNumber) ?? throw new ArgumentException("Unknown quote.");
        var contractor = await contractors.GetAsync(quote.ContractorId) ?? throw new ArgumentException("Unknown contractor.");
        problems.AddRange(ContractorService.Problems(contractor, Now));
        if (problems.Count > 0)
        {
            return problems;
        }

        project.AppointedContractorId = contractor.Id;
        project.AppointedContractor = contractor.Company;
        project.ContractAmount = quote.Amount;
        project.Status = ProjectStatus.Appointed;
        await projects.UpdateAsync(project);
        return problems;
    }

    public static int DistinctQuoteCount(Project project) => project.Quotes.Select(q => q.ContractorId).Distinct().Count();

    public async Task AddMilestoneAsync(int projectId, string name, DateTime dueDate)
    {
        var project = await RequireOpenAsync(projectId);
        project.Milestones.Add(new Milestone { Name = name.Trim(), DueDate = dueDate.Date });
        await projects.UpdateAsync(project);
    }

    public async Task CompleteMilestoneAsync(int projectId, int index)
    {
        var project = await RequireOpenAsync(projectId);
        project.Milestones[index].Done = true;
        await projects.UpdateAsync(project);
    }

    public async Task AddVariationAsync(int projectId, string description, decimal amount)
    {
        var project = await RequireOpenAsync(projectId);
        project.Variations.Add(new Variation { Description = description.Trim(), Amount = amount });
        await projects.UpdateAsync(project);
    }

    public async Task ApproveVariationAsync(int projectId, int index, UserRole role)
    {
        if (role != UserRole.Manager)
        {
            throw new UnauthorizedAccessException("Only the village manager can approve variations.");
        }
        var project = await RequireOpenAsync(projectId);
        project.Variations[index].Approved = true;
        await projects.UpdateAsync(project);
    }

    /// <summary>Records a payment certificate. Retention is held back from the amount payable.</summary>
    public async Task<decimal> AddPaymentCertificateAsync(int projectId, decimal grossAmount, string description)
    {
        var project = await RequireOpenAsync(projectId);
        if (project.AppointedContractorId is null)
        {
            throw new InvalidOperationException("Appoint a contractor first.");
        }
        if (grossAmount <= 0)
        {
            throw new ArgumentException("The amount must be more than zero.");
        }

        var retention = Math.Round(grossAmount * Options.RetentionPercent / 100m, 2);
        project.RetentionHeld += retention;
        project.Status = ProjectStatus.InProgress;
        await AddCostAndPostAsync(project, "Payment certificate", description, grossAmount, retention);
        return retention;
    }

    public async Task AddCostAsync(int projectId, string kind, string description, decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("The amount must be more than zero.");
        }
        var project = await RequireOpenAsync(projectId);
        await AddCostAndPostAsync(project, kind, description, amount, 0);
    }

    public async Task CloseAsync(int projectId, string notes)
    {
        var project = await RequireOpenAsync(projectId);
        project.Status = ProjectStatus.Closed;
        project.CloseOutNotes = notes.Trim();
        await projects.UpdateAsync(project);
    }

    private async Task AddCostAndPostAsync(Project project, string kind, string description, decimal amount, decimal retention)
    {
        project.Costs.Add(new ProjectCost { Date = Now, Kind = kind, Description = description.Trim(), Amount = amount });
        await projects.UpdateAsync(project);
        await outbox.EnqueueAsync(OutboxTarget.Pastel, "ProjectCostJournal", $"PRJ-{project.Id}-COST-{project.Costs.Count}", new
        {
            project.CostCentre,
            Project = project.Name,
            Kind = kind,
            Amount = amount,
            Retention = retention,
            NetPayable = amount - retention
        });
    }

    private async Task<Project> RequireOpenAsync(int id)
    {
        var project = await projects.GetAsync(id) ?? throw new ArgumentException("Unknown project.");
        if (project.Status == ProjectStatus.Closed)
        {
            throw new InvalidOperationException("This project is closed.");
        }
        return project;
    }
}
