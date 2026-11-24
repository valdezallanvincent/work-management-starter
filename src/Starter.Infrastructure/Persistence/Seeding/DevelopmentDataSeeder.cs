using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Security;
using Starter.Domain.Projects;
using Starter.Domain.Users;
using Starter.Domain.WorkItems;

namespace Starter.Infrastructure.Persistence.Seeding;

/// <summary>
/// Creates a small fictional organisation so the UI has something to show. Runs only when the
/// database has no users, so it never overwrites data entered during development.
/// </summary>
internal sealed class DevelopmentDataSeeder(
    StarterDbContext db,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider,
    IOptions<SeedOptions> options,
    ILogger<DevelopmentDataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var password = options.Value.DefaultPassword;
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            logger.LogWarning("Seed:DefaultPassword is not set (minimum 12 characters); development data was not seeded");
            return;
        }

        var admin = CreateUser("admin@kestrel-logistics.test", "Morgan Admin", UserRole.Admin, password);
        var manager = CreateUser("dana.reyes@kestrel-logistics.test", "Dana Reyes", UserRole.Manager, password);
        var lead = CreateUser("samir.okafor@kestrel-logistics.test", "Samir Okafor", UserRole.Manager, password);
        var dev = CreateUser("lena.fischer@kestrel-logistics.test", "Lena Fischer", UserRole.Member, password);
        var analyst = CreateUser("tomas.ruiz@kestrel-logistics.test", "Tomas Ruiz", UserRole.Member, password);
        User[] users = [admin, manager, lead, dev, analyst];
        db.Users.AddRange(users);

        var warehouse = Project.Create(ProjectKey.Create("WMS"), "Warehouse scanner rollout", "Replace handheld scanners at the three regional depots and migrate pick lists.", manager.Id);
        var billing = Project.Create(ProjectKey.Create("BILL"), "Carrier invoice reconciliation", "Automate matching of carrier invoices against shipment records.", lead.Id);
        var portal = Project.Create(ProjectKey.Create("PORTAL"), "Customer tracking portal", "Self-service shipment tracking for business customers.", manager.Id);
        Project[] projects = [warehouse, billing, portal];
        db.Projects.AddRange(projects);

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var items = new List<WorkItem>
        {
            Item(warehouse, "Inventory current scanner models per depot", WorkItemPriority.Medium, manager, dev, WorkItemStatus.Completed, today.AddDays(-10)),
            Item(warehouse, "Pilot new scanners at Leeds depot", WorkItemPriority.High, manager, dev, WorkItemStatus.InProgress, today.AddDays(5)),
            Item(warehouse, "Update pick list export to new barcode format", WorkItemPriority.High, manager, analyst, WorkItemStatus.InReview, today.AddDays(2)),
            Item(warehouse, "Train shift leads on scanner pairing", WorkItemPriority.Low, manager, null, WorkItemStatus.Open, today.AddDays(21)),
            Item(billing, "Map carrier invoice CSV columns", WorkItemPriority.Medium, lead, analyst, WorkItemStatus.Completed, null),
            Item(billing, "Flag invoices with weight variance above 5%", WorkItemPriority.Critical, lead, analyst, WorkItemStatus.InProgress, today.AddDays(-1)),
            Item(billing, "Duplicate invoice detection", WorkItemPriority.High, lead, null, WorkItemStatus.Open, today.AddDays(14)),
            Item(billing, "Retire manual reconciliation spreadsheet", WorkItemPriority.Low, lead, null, WorkItemStatus.Cancelled, null),
            Item(portal, "Define tracking status vocabulary with support", WorkItemPriority.Medium, manager, dev, WorkItemStatus.Open, today.AddDays(-3)),
            Item(portal, "Rate limit public tracking lookups", WorkItemPriority.High, manager, null, WorkItemStatus.Open, null),
        };

        foreach (var item in items)
        {
            item.ClearDomainEvents();
        }

        db.WorkItems.AddRange(items);
        db.WorkItemComments.Add(WorkItemComment.Create(items[1], dev.Id, "Leeds has 14 units on site. Two need firmware updates before pairing."));
        db.WorkItemComments.Add(WorkItemComment.Create(items[1], manager.Id, "Thanks. Please log the firmware versions so we can plan the other depots."));
        db.WorkItemComments.Add(WorkItemComment.Create(items[5], analyst.Id, "Variance threshold agreed with finance on Monday's call."));

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {UserCount} users, {ProjectCount} projects and {WorkItemCount} work items", users.Length, projects.Length, items.Count);

        WorkItem Item(Project project, string title, WorkItemPriority priority, User reporter, User? assignee, WorkItemStatus target, DateOnly? due)
        {
            var item = WorkItem.Create(project, title, null, priority, reporter.Id, due);
            if (assignee is not null)
            {
                item.AssignTo(assignee.Id, reporter.Id, now);
            }

            // Walk the real workflow so seeded data always satisfies the transition rules.
            WorkItemStatus[] path = target switch
            {
                WorkItemStatus.InProgress => [WorkItemStatus.InProgress],
                WorkItemStatus.InReview => [WorkItemStatus.InProgress, WorkItemStatus.InReview],
                WorkItemStatus.Completed => [WorkItemStatus.InProgress, WorkItemStatus.InReview, WorkItemStatus.Completed],
                WorkItemStatus.Cancelled => [WorkItemStatus.Cancelled],
                _ => [],
            };

            foreach (var status in path)
            {
                item.ChangeStatus(status, now);
            }

            return item;
        }
    }

    private User CreateUser(string email, string name, UserRole role, string password)
    {
        var user = User.Create(email, name, role, "pending");
        user.ChangePasswordHash(passwordHasher.Hash(user, password));
        return user;
    }
}
