using Korlavalasa.Data;
using Korlavalasa.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Configure file upload limits
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50MB
    options.ValueLengthLimit = 10 * 1024 * 1024; // 10MB per file
    options.MultipartBoundaryLengthLimit = int.MaxValue;
    options.MemoryBufferThreshold = int.MaxValue;
});

// DATABASE CONFIGURATION - UPDATED FOR PRODUCTION
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (builder.Environment.IsDevelopment())
{
    // Local Development - SQL Server
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString));
}
else
{
    // Production - PostgreSQL (Render)
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Identity Configuration
builder.Services.AddIdentity<AdminUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 4;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Configure Application Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.LoginPath = "/Admin/Login";
    options.AccessDeniedPath = "/Admin/AccessDenied";
    options.SlidingExpiration = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline
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


// Database Initialization and Seeding - NUCLEAR OPTION
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<AdminUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        Console.WriteLine("🚀 STARTING DATABASE INITIALIZATION...");

        // Nuclear option: Delete and recreate database
        Console.WriteLine("🔧 Dropping existing database...");
        await context.Database.EnsureDeletedAsync();
        Console.WriteLine("✅ Database dropped");

        Console.WriteLine("🔧 Creating new database tables...");
        var created = await context.Database.EnsureCreatedAsync();
        Console.WriteLine($"✅ Database tables created: {created}");

        if (created)
        {
            Console.WriteLine("🔧 Seeding initial data...");
            await SeedInitialData(context, userManager, roleManager);
            Console.WriteLine("✅ Data seeded successfully");
        }
        else
        {
            Console.WriteLine("❌ Database tables were not created");
        }

        Console.WriteLine("🎉 DATABASE INITIALIZATION COMPLETED SUCCESSFULLY");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"💥 CRITICAL DATABASE ERROR: {ex.Message}");
        Console.WriteLine($"💥 STACK TRACE: {ex.StackTrace}");

        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "A critical error occurred while initializing the database.");

        // Don't crash the app, but log everything
        if (ex.InnerException != null)
        {
            Console.WriteLine($"💥 INNER EXCEPTION: {ex.InnerException.Message}");
        }
    }
}

app.Run();

// Seed Initial Data Method
async Task SeedInitialData(AppDbContext context, UserManager<AdminUser> userManager, RoleManager<IdentityRole> roleManager)
{
    // Create Admin Role if it doesn't exist
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
        Console.WriteLine("✅ Admin role created");
    }

    // Create Admin User if it doesn't exist
    string adminEmail = "admin@korlavalasa.com";
    string adminUsername = "admin";
    string adminPassword = "Admin@123";
    string adminFullName = "Korlavalasa Administrator";

    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new AdminUser
        {
            UserName = adminUsername,
            Email = adminEmail,
            FullName = adminFullName,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            Console.WriteLine("✅ Admin user created successfully");
        }
        else
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            Console.WriteLine($"❌ Failed to create admin user: {errors}");
        }
    }

   
}