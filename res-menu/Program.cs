using res_menu.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure culture for PLN currency
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "pl-PL" };
    options.SetDefaultCulture(supportedCultures[0])
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
});

// Configure Kestrel for production HTTPS
if (!builder.Environment.IsDevelopment())
{
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ListenAnyIP(80); // HTTP
        serverOptions.ListenAnyIP(443, listenOptions =>
        {
            listenOptions.UseHttps(httpsOptions =>
            {
                // In production, use Let's Encrypt certificate paths
                var certPath = "/etc/letsencrypt/live/res-menu.duckdns.org/fullchain.pem";
                var keyPath = "/etc/letsencrypt/live/res-menu.duckdns.org/privkey.pem";
                
                if (File.Exists(certPath) && File.Exists(keyPath))
                {
                    httpsOptions.ServerCertificate = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPemFile(certPath, keyPath);
                }
            });
        });
    });
}

// Get connection string with environment variable fallback
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? 
    Environment.GetEnvironmentVariable("DATABASE_URL") ?? 
    throw new InvalidOperationException("Connection string not found. Please set either DefaultConnection in configuration or DATABASE_URL environment variable."); 

// Log connection info (sanitized)
var sanitizedConnectionString = connectionString.Contains("@") 
    ? new Uri(connectionString).Host 
    : connectionString.Replace(";Password=", ";Password=***").Replace(";User Id=", ";User Id=***").Replace(";Username=", ";Username=***");
Console.WriteLine($"Using database connection: {sanitizedConnectionString}");

// Apply localhost override for development
var dockerEnvVar = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
if (builder.Environment.IsDevelopment() && 
    !string.Equals(dockerEnvVar, "true", StringComparison.OrdinalIgnoreCase) && 
    connectionString.Contains("Host=postgres"))
{
    connectionString = connectionString.Replace("Host=postgres", "Host=localhost");
}

// Add services using extension methods
builder.Services.AddDatabaseServices(connectionString);
builder.Services.AddIdentityServices();
builder.Services.AddApplicationServices();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    // Use our custom exception handling instead of default
    app.UseCustomExceptionHandling();
    app.UseHsts();
}
else
{
    // Use custom exception handling in development too for consistency
    app.UseCustomExceptionHandling();
}

// Add localization and custom middleware
app.UseRequestLocalization();
app.UseRequestLogging();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Add subdomain routing before authentication
app.UseSubdomainRouting();

app.UseAuthentication();
app.UseAuthorization();

// Configure endpoints
app.MapRazorPages();

// Initialize database
await app.InitializeDatabaseAsync();

app.Run();