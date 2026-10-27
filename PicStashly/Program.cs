using Heroicsoft.NetForge.Abstractions.Navigation;
using Heroicsoft.NetForge.Components.Bootstrap;
using LogVue;
using Microsoft.AspNetCore.Http.Features;
using PicStashly;
using PicStashly.Components;
using PicStashly.Infrastructure;
using PicStashly.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogVue(options =>
    builder.Configuration.GetSection(LogVueOptions.SectionName).Bind(options));

builder.Services.AddPicStashlyDatabase(builder.Configuration);

builder.Services.AddSerilog((services, configuration) => configuration
    .MinimumLevel.Information()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.LogVue(services));

builder.Services.AddEntityFrameworkRepository();
builder.Services.AddPicStashlyServices(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddNetForgeBootstrapComponents(ui =>
{
    ui.ThemeStorageKey = PicStashly.Constants.ClientThemeLocalStorageKey;
    // theme-init.js uses darkly when nothing is stored. PicStashly.Constants.BootswatchThemes.Default
    // is sketchy, which is the list's documented name, not the theme the app actually opens on.
    ui.DefaultTheme = "darkly";
});
builder.Services.Configure<ForgeShellOptions>(shell =>
{
    shell.Brand = "PicStashly";
    shell.BrandHref = "/";
    shell.LogoUrl = "img/Logo.png";
    shell.LogoDarkUrl = "img/Logo-Light.png";
    shell.NavigationPlacement = ForgeNavigationPlacement.Topbar;
    shell.Version = typeof(Program).Assembly.GetName().Version?.ToString(3);
    shell.Sections =
    [
        new ForgeNavSection
        {
            Items =
            [
                new ForgeNavItem { Title = "Library", Href = "/", Icon = "grid-3x3-gap", MatchAll = true },
                new ForgeNavItem { Title = "Upload", Href = "/upload", Icon = "cloud-arrow-up" },
                new ForgeNavItem { Title = "Tags", Href = "/tags", Icon = "tags" },
                new ForgeNavItem { Title = "Categories", Href = "/categories", Icon = "folder2-open" },
            ],
        },
        new ForgeNavSection
        {
            Roles = PicStashly.Constants.Roles.Administrator,
            Items =
            [
                new ForgeNavItem { Title = "Admin", Href = "/admin", Icon = "gear" },
                new ForgeNavItem { Title = "Logs", Href = "/logvue", Icon = "journal-text", External = true },
            ],
        },
    ];
});

// Set "Authentication:Provider" to "Keycloak" to use Keycloak instead of the built-in ASP.NET Core
// Identity system. See appsettings.json for the configuration keys.
string authProviderName = builder.Configuration.GetValue<string>("Authentication:Provider") ?? "Identity";
bool useKeycloak = authProviderName.Equals("Keycloak", StringComparison.OrdinalIgnoreCase);

builder.Services.AddPicStashlyAuthentication(builder.Configuration, useKeycloak);

builder.Services.AddControllers();
builder.Services.AddRazorPages();

// Allow large multi-file upload batches (per-file size is validated in the ingest pipeline).
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 524_288_000);

var app = builder.Build();

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
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

if (!useKeycloak)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

// Stored images are served under /media by MediaController, which checks per-user ownership.
{
    string storagePath = app.Configuration.GetValue<string>("FileStorage:BasePath")
        ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");

    Directory.CreateDirectory(storagePath);
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .WithStaticAssets();

app.MapControllers();

app.MapRazorPages().WithStaticAssets();

// PWA share target. The browser posts multipart form field "images" and cannot send an
// antiforgery token, so this endpoint opts out.
app.MapPost("/upload/share", async (HttpRequest request, IImageService imageService) =>
{
    if (!request.HasFormContentType)
    {
        return Results.Redirect("/upload");
    }

    var form = await request.ReadFormAsync();
    var files = form.Files.Where(file => file.Name == "images").ToList();
    if (files.Count == 0)
    {
        return Results.Redirect("/upload");
    }

    int? lastImageId = null;
    foreach (var file in files)
    {
        await using var stream = file.OpenReadStream();
        var result = await imageService.IngestAsync(stream, file.FileName, sourceUrl: null, tagNames: null, categoryId: null, force: false);
        if (!result.IsSuccess)
        {
            continue;
        }

        lastImageId = result.Value.IsDuplicate
            ? result.Value.Duplicates.FirstOrDefault()?.Id
            : result.Value.Image?.Id;
    }

    return lastImageId is int id ? Results.Redirect($"/image/{id}") : Results.Redirect("/");
}).RequireAuthorization().DisableAntiforgery();

app.MapLogVue().RequireAuthorization(new AuthorizeAttribute
{
    Roles = PicStashly.Constants.Roles.Administrator
});

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