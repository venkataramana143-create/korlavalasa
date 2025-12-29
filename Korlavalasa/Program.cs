using CloudinaryDotNet;
using Korlavalasa.Data;
using Korlavalasa.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// Razor Pages
// =====================================================
builder.Services.AddRazorPages();

// =====================================================
// File Upload Limits (50 MB)
// =====================================================
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

// =====================================================
// Cloudinary (FROM ENV VARIABLES)
// =====================================================
builder.Services.AddSingleton(sp =>
{
    var cloudinarySection = builder.Configuration.GetSection("Cloudinary");

    return new Cloudinary(new Account(
        cloudinarySection["CloudName"],
        cloudinarySection["ApiKey"],
        cloudinarySection["ApiSecret"]
    ));
});


// =====================================================
// DATABASE (POSTGRESQL - RENDER)
// =====================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new Exception("❌ Database connection string not found");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));


// =====================================================
// Identity Configuration
// =====================================================
builder.Services.AddIdentity<AdminUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 4;
    options.Password.RequiredUniqueChars = 1;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// =====================================================
// Cookie Settings
// =====================================================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.LoginPath = "/Admin/Login";
    options.AccessDeniedPath = "/Admin/AccessDenied";
    options.SlidingExpiration = true;
});

var app = builder.Build();

// =====================================================
// HTTP PIPELINE
// =====================================================
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

// =====================================================
// DATABASE INIT + ADMIN SEED
// =====================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<AdminUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        Console.WriteLine("🚀 Database initialization starting...");

        await context.Database.MigrateAsync();
        await EnsureAdminUserAlwaysExists(userManager, roleManager);

        Console.WriteLine("🎉 Database initialization complete");
    }
    catch (Exception ex)
    {
        Console.WriteLine("🔥 Database error: " + ex.Message);
    }
}

app.Run();

// =====================================================
// ADMIN SEED METHOD
// =====================================================
async Task EnsureAdminUserAlwaysExists(
    UserManager<AdminUser> userManager,
    RoleManager<IdentityRole> roleManager)
{
    string adminUsername = "kvuser";
    string adminEmail = "kvuser@korlavalasa.com";
    string adminPassword = "kvalasa@123";

    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
        Console.WriteLine("✔ Admin role created");
    }

    var admin = await userManager.FindByNameAsync(adminUsername);
    if (admin == null)
    {
        admin = new AdminUser
        {
            UserName = adminUsername,
            Email = adminEmail,
            FullName = "Korlavalasa Administrator",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
            Console.WriteLine("✔ Admin user created");
        }
        else
        {
            Console.WriteLine("❌ Admin creation failed: " +
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
