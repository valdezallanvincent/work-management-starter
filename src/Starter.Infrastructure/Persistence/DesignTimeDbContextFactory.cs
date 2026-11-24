using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Starter.Infrastructure.Persistence;

/// <summary>
/// Used only by the dotnet-ef tool. Adding migrations does not open a connection, so a local
/// placeholder is enough; `database update` reads the real string from the --connection argument.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<StarterDbContext>
{
    public StarterDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<StarterDbContext>()
            .UseSqlServer("Server=localhost;Database=Starter;Integrated Security=false;TrustServerCertificate=True")
            .Options;

        return new StarterDbContext(options);
    }
}
