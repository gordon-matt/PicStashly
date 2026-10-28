using Heroicsoft.NetForge.Drawing;
using Microsoft.Extensions.DependencyInjection;

namespace PicStashly.Services;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPicStashlyServices(IConfiguration configuration)
        {
            services.AddNetForgeDrawing();

            services.Configure<ImageProcessingOptions>(configuration.GetSection("ImageProcessing"));
            services.Configure<UploadOptions>(configuration.GetSection("Upload"));
            services.Configure<OcrOptions>(configuration.GetSection("Ocr"));

            services.AddHttpClient("capture", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            });

            services.AddHttpContextAccessor();

            services.AddSingleton<IFileStorageService, FileStorageService>();
            services.AddSingleton<IPerceptualHashService, PerceptualHashService>();
            services.AddSingleton<IOcrService, OcrService>();

            services.AddScoped<IUserContextService, UserContextService>();
            services.AddScoped<IImageService, ImageService>();
            services.AddScoped<ITagService, TagService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IAdminService, AdminService>();

            return services;
        }
    }
}