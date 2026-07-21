namespace PicStashly.Data;

public abstract class ApplicationDbContextBase : DbContext
{
    protected ApplicationDbContextBase(DbContextOptions options) : base(options)
    {
    }

    public DbSet<Image> Images { get; set; }

    public DbSet<Tag> Tags { get; set; }

    public DbSet<ImageTag> ImageTags { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContextBase).Assembly);
    }
}
