namespace PicStashly.Data.Sql;

/// <summary>
/// SQL Server-flavoured <see cref="ApplicationDbContextBase"/>. Migrations for SQL Server live in
/// this assembly so that <c>dotnet ef</c> only ever sees one provider per project.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : ApplicationDbContextBase(options)
{
}
