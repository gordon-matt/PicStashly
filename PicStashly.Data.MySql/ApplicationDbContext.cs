namespace PicStashly.Data.MySql;

/// <summary>
/// MySQL/MariaDB-flavoured <see cref="ApplicationDbContextBase"/>. Migrations for MySQL live in
/// this assembly so that <c>dotnet ef</c> only ever sees one provider per project.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : ApplicationDbContextBase(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // MySQL has no concept of a schema that is distinct from a database. The entity maps place
        // tables in an "app" schema, which MySQL would interpret as a separate database literally
        // named "app". Flatten every table back into the connection's own database.
        builder.Model.SetDefaultSchema(null);
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            entityType.SetSchema(null);
        }
    }
}