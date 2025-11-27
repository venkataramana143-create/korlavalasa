using Korlavalasa.Data;
using Korlavalasa.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();

// File upload limits
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 50 * 1024 * 1024;
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50 * 1024 * 1024;
    options.ValueLengthLimit = 10 * 1024 * 1024;
    options.MultipartBoundaryLengthLimit = int.MaxValue;
    options.MemoryBufferThreshold = int.MaxValue;
});

// DATABASE CONFIG
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (builder.Environment.IsDevelopment())
{
    // SQL Server - Development
    builder.Services.AddDbContext<AppDbContext>(opts =>
        opts.UseSqlServer(connectionString));
}
else
{
    // PostgreSQL - Production Render
    builder.Services.AddDbContext<AppDbContext>(opts =>
        opts.UseNpgsql(connectionString));
}

// Identity
builder.Services.AddIdentity<AdminUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 4;
    options.Password.RequiredUniqueChars = 1;

    options.User.RequireUniqueEmail = true;

})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Cookies
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.LoginPath = "/Admin/Login";
    options.AccessDeniedPath = "/Admin/AccessDenied";
    options.SlidingExpiration = true;
});

var app = builder.Build();

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// ----------------------
// DATABASE INIT + ADMIN FIX
// ----------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<AdminUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        Console.WriteLine("🚀 Starting DB Initialization...");

        bool canConnect = await context.Database.CanConnectAsync();
        Console.WriteLine($"Database Connect: {canConnect}");

        if (canConnect)
        {
            // Ensure database exists
            await context.Database.EnsureCreatedAsync();
        }

        // ⭐ ALWAYS ensure Admin user + role exist
        await EnsureAdminUserAlwaysExists(userManager, roleManager);

        // Village, News, Events seeding (safe)
        await SeedVillageInfo(context);
        await SeedNews(context);
        await SeedEvents(context);

        Console.WriteLine("🎉 DB Initialization Complete!");
    }
    catch (Exception ex)
    {
        Console.WriteLine("🔥 DB Error: " + ex.Message);
    }
}

app.Run();

// ------------------------------------------------------------
// REQUIRED METHODS BELOW
// ------------------------------------------------------------

// 1. ALWAYS ensure Admin exists
async Task EnsureAdminUserAlwaysExists(
    UserManager<AdminUser> userManager,
    RoleManager<IdentityRole> roleManager)
{
    string adminEmail = "admin@korlavalasa.com";
    string adminUsername = "admin";

    // Create role if missing
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
        Console.WriteLine("✔ Admin role created");
    }

    // Create admin user if missing
    var admin = await userManager.FindByEmailAsync(adminEmail);

    if (admin == null)
    {
        admin = new AdminUser
        {
            UserName = adminUsername,
            Email = adminEmail,
            FullName = "Korlavalasa Administrator",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, "Admin@123");

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
            Console.WriteLine("✔ Admin user created");
        }
        else
        {
            Console.WriteLine("❌ Error creating admin: " +
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
    else
    {
        Console.WriteLine("✔ Admin user already exists");
    }
}

// 2. Village Info Seed
async Task SeedVillageInfo(AppDbContext context)
{
    if (!context.VillageInfo.Any())
    {
        context.VillageInfo.Add(new VillageInfo
        {
            AboutText = "Welcome to Korlavalasa village official website.",
            History = "Korlavalasa has a rich history.",
            Population = 2500,
            Area = 15.75m,
            MainCrops = "Rice, Vegetables, Fruits",
            ContactEmail = "info@korlavalasa.com",
            ContactNumber = "+91XXXXXXXXXX",
            Address = "Korlavalasa Village",
            SarpanchName = "Sarpanch Name",
            SecretaryName = "Secretary Name"
        });

        await context.SaveChangesAsync();
        Console.WriteLine("✔ VillageInfo seeded");
    }
}

// 3. News Seed
async Task SeedNews(AppDbContext context)
{
    if (!context.News.Any())
    {
        context.News.Add(new News
        {
            Title = "Welcome to Korlavalasa",
            Content = "Welcome to our village!",
            PublishedDate = DateTimeOffset.UtcNow,
            IsActive = true
        });

        await context.SaveChangesAsync();
        Console.WriteLine("✔ News seeded");
    }
}

// 4. Events Seed
async Task SeedEvents(AppDbContext context)
{
    if (!context.Events.Any())
    {
        context.Events.Add(new Event
        {
            Title = "Village Meeting",
            Description = "Monthly meeting",
            EventDate = DateTimeOffset.UtcNow.AddDays(7),
            Location = "Village Hall"
        });

        await context.SaveChangesAsync();
        Console.WriteLine("✔ Events seeded");
    }
}
