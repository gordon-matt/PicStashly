using Microsoft.EntityFrameworkCore.Design;

namespace PicStashly.Data.Sqlite;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> tooling so migrations can be generated without
/// needing a fully wired-up host application.
/// </summary>
public class ApplicationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlite(
            "Data Source=picstashly_design.db",
            sqlite => sqlite.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name));
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}