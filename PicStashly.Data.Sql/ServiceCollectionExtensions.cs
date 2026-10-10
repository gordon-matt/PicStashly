using LogVue.Data.Sql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PicStashly.Data.Sql;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the SQL Server <see cref="ApplicationDbContext"/>, an <see cref="IDbContextFactory"/>
        /// and the abstract <see cref="ApplicationDbContextBase"/>. Falls back to EF Core's in-memory
        /// provider when no connection string is configured.
        /// </summary>
        public IServiceCollection AddPicStashlySqlServer(IConfiguration configuration)
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
                    options.UseSqlServer(connectionString, sql =>
                        sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name));
                }
            });

            services.AddScoped<ApplicationDbContextBase>(sp => sp.GetRequiredService<ApplicationDbContext>());
            services.AddSingleton<IDbContextFactory, ApplicationDbContextFactory>();

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                services.AddLogVueSqlServer(connectionString);
            }

            return services;
        }
    }
}