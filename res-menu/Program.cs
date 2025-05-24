using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using res_menu.Data;
using res_menu.Areas.Identity.Data;
using Microsoft.AspNetCore.Http.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? 
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>    {        options.SignIn.RequireConfirmedAccount = false;        options.SignIn.RequireConfirmedEmail = false;        options.Password.RequireDigit = true;        options.Password.RequireLowercase = true;        options.Password.RequireUppercase = true;        options.Password.RequireNonAlphanumeric = true;        options.Password.RequiredLength = 8;    })    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

// Add subdomain routing middleware
app.Use(async (context, next) =>
{
    var host = context.Request.Host.Host.ToLower();
    var isLocalhost = host == "localhost" || host.StartsWith("127.") || host.StartsWith("192.168.");
    
    if (!isLocalhost && host.Count(c => c == '.') > 1)
    {
        var subdomain = host.Split('.')[0];
        if (!string.IsNullOrEmpty(subdomain))
        {
            var originalPath = context.Request.Path;
            context.Request.Path = "/Menu";
            context.Request.QueryString = context.Request.QueryString.Add("subdomain", subdomain);
        }
    }
    
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

app.Run();