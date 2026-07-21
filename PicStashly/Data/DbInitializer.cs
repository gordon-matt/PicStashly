using PicStashly.Data;

namespace PicStashly;

/// <summary>Applies pending EF Core migrations on startup (or EnsureCreated for the in-memory fallback).</summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContextBase>();

        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }
    }
}
