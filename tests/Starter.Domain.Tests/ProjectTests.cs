using Starter.Domain.Common;
using Starter.Domain.Projects;
using Starter.Domain.WorkItems;

namespace Starter.Domain.Tests;

public sealed class ProjectTests
{
    [Fact]
    public void Archived_projects_cannot_accept_new_work_items()
    {
        var project = TestData.ActiveProject();
        project.Archive(TestData.Now);

        var exception = Assert.Throws<DomainException>(() =>
            WorkItem.Create(project, "Late request", null, WorkItemPriority.Low, TestData.OwnerId, null));

        Assert.Equal("project.archived", exception.Code);
    }

    [Fact]
    public void Archived_projects_are_read_only_until_restored()
    {
        var project = TestData.ActiveProject();
        project.Archive(TestData.Now);

        Assert.Equal("project.archived", Assert.Throws<DomainException>(() => project.UpdateDetails("Renamed", null)).Code);
        Assert.Equal("project.already_archived", Assert.Throws<DomainException>(() => project.Archive(TestData.Now)).Code);

        project.Restore();
        project.UpdateDetails("Renamed", null);

        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Null(project.ArchivedAtUtc);
        Assert.Equal("Renamed", project.Name);
    }

    [Fact]
    public void Restoring_an_active_project_is_rejected() =>
        Assert.Equal("project.not_archived", Assert.Throws<DomainException>(() => TestData.ActiveProject().Restore()).Code);

    [Theory]
    [InlineData("ops", "OPS")]
    [InlineData(" Fin2 ", "FIN2")]
    [InlineData("AB", "AB")]
    public void Project_keys_are_normalized(string input, string expected) =>
        Assert.Equal(expected, ProjectKey.Create(input).Value);

    [Theory]
    [InlineData(null)]
    [InlineData("A")]
    [InlineData("2FA")]
    [InlineData("OPS-1")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("ÄBC")]
    public void Invalid_project_keys_are_rejected(string? input) =>
        Assert.Equal("project.key_invalid", Assert.Throws<DomainException>(() => ProjectKey.Create(input)).Code);
}
