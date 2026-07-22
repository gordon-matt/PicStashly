using Microsoft.EntityFrameworkCore.Design;

namespace PicStashly.Data.Sql;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> tooling so migrations can be generated without
/// needing a running SQL Server instance or a fully wired-up host application.
/// </summary>
public class ApplicationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=picstashly_design;Trusted_Connection=True;TrustServerCertificate=True",
            sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name));
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}