using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PicStashly.Data.Npgsql;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the PostgreSQL <see cref="ApplicationDbContext"/>, an <see cref="IDbContextFactory"/>
        /// and the abstract <see cref="ApplicationDbContextBase"/>. Falls back to EF Core's in-memory
        /// provider when no connection string is configured.
        /// </summary>
        public IServiceCollection AddPicStashlyNpgsql(IConfiguration configuration)
        {
            string? connectionString = configuration.GetConnectionString("DefaultConnection");

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    options.UseInMemoryDatabase("PicStashlyDb");
                }
                else
                {
                    options.UseNpgsql(connectionString, npgsql =>
                        npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name));
                }
            });

            services.AddScoped<ApplicationDbContextBase>(sp => sp.GetRequiredService<ApplicationDbContext>());
            services.AddSingleton<IDbContextFactory, ApplicationDbContextFactory>();

            return services;
        }
    }
}