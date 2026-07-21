using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using PicStashly;
using PicStashly.Infrastructure;
using PicStashly.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddPicStashlyDatabase(builder.Configuration);
builder.Services.AddEntityFrameworkRepository();
builder.Services.AddPicStashlyServices(builder.Configuration);

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ── Authentication provider selection ────────────────────────────────────────
// Set "Authentication:Provider" to "Keycloak" to use Keycloak instead of the
// built-in ASP.NET Core Identity system. See appsettings.json for configuration
// keys and docs/KEYCLOAK_SETUP.md for Keycloak-side setup instructions.
string authProviderName = builder.Configuration.GetValue<string>("Authentication:Provider") ?? "Identity";
bool useKeycloak = authProviderName.Equals("Keycloak", StringComparison.OrdinalIgnoreCase);

builder.Services.AddPicStashlyAuthentication(builder.Configuration, useKeycloak);

builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson();

builder.Services.AddRazorPages();

// Allow large multi-file upload batches (per-file size is validated in the ingest pipeline).
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000; // 500 MB
});

var app = builder.Build();

// When using Keycloak behind a reverse proxy (HTTPS → app), process forwarded headers first
// so the app sees the original scheme (https) and host. Must run before other middleware.
if (useKeycloak)
{
    app.UseForwardedHeaders();
}

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// When using Keycloak behind a reverse proxy, the app typically listens only on HTTP;
// the proxy terminates HTTPS. UseHttpsRedirection would try to redirect but cannot
// determine an HTTPS port, causing "Failed to determine the https port for redirect."
// Rely on the proxy and forwarded headers instead.
if (!useKeycloak)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

// Serve stored images and thumbnails from the storage folder under /media (images only).
{
    string storagePath = app.Configuration.GetValue<string>("FileStorage:BasePath")
        ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");

    Directory.CreateDirectory(storagePath);

    var imageContentTypeProvider = new FileExtensionContentTypeProvider(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
            [".gif"] = "image/gif",
            [".bmp"] = "image/bmp",
        });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(storagePath),
        RequestPath = Constants.MediaRequestPath,
        ContentTypeProvider = imageContentTypeProvider,
        ServeUnknownFileTypes = false
    });
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Use fingerprinted static assets only in Development (avoids 500s in Docker when manifest paths differ)
if (app.Environment.IsDevelopment())
{
    app.MapStaticAssets();
}

var defaultRoute = app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
if (app.Environment.IsDevelopment())
{
    defaultRoute.WithStaticAssets();
}

var razorPages = app.MapRazorPages();
if (app.Environment.IsDevelopment())
{
    razorPages.WithStaticAssets();
}

await using (var scope = app.Services.CreateAsyncScope())
{
    try
    {
        await DbInitializer.InitializeAsync(scope.ServiceProvider, app.Configuration, useKeycloak);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        if (logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(ex, "An error occurred while initializing the database.");
        }
    }
}

app.Run();
