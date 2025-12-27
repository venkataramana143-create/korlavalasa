using CloudinaryDotNet;
using Korlavalasa.Data;
using Korlavalasa.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------
// Razor Pages
// --------------------------------------------------
builder.Services.AddRazorPages();

// --------------------------------------------------
// File Upload Limits (50 MB)
// --------------------------------------------------
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

// --------------------------------------------------
// Cloudinary (Image Storage)
// --------------------------------------------------
builder.Services.AddSingleton(new Cloudinary(new Account(
    builder.Configuration["Cloudinary:CloudName"],
    builder.Configuration["Cloudinary:ApiKey"],
    builder.Configuration["Cloudinary:ApiSecret"]
)));

// --------------------------------------------------
// DATABASE CONFIG (SUPABASE POSTGRESQL)
// --------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// --------------------------------------------------
// Identity Configuration
// --------------------------------------------------
builder.Services.AddIdentity<AdminUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// --------------------------------------------------
// Cookie Settings
// --------------------------------------------------
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.LoginPath = "/Admin/Login";
    options.AccessDeniedPath = "/Admin/AccessDenied";
    options.SlidingExpiration = true;
});

var app = builder.Build();

// --------------------------------------------------
// HTTP PIPELINE
// --------------------------------------------------
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

// --------------------------------------------------
// DATABASE INIT + ADMIN USER
// --------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<AdminUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        Console.WriteLine("🚀 Database initialization starting...");

        await context.Database.EnsureCreatedAsync();
        await EnsureAdminUserAlwaysExists(userManager, roleManager);

        Console.WriteLine("🎉 Database initialization complete");
    }
    catch (Exception ex)
    {
        Console.WriteLine("🔥 Database error: " + ex.Message);
    }
}

app.Run();

// --------------------------------------------------
// ADMIN CREATION (ONLY THIS SEED)
// --------------------------------------------------
async Task EnsureAdminUserAlwaysExists(
    UserManager<AdminUser> userManager,
    RoleManager<IdentityRole> roleManager)
{
    string adminUsername = "kvuser";
    string adminEmail = "kvuser@korlavalasa.com";
    string adminPassword = "kvalasa@123";

    // Ensure role exists
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
        Console.WriteLine("✔ Admin role created");
    }

    // Remove old admin (if exists)
    var oldAdmin = await userManager.FindByNameAsync("admin");
    if (oldAdmin != null)
    {
        await userManager.DeleteAsync(oldAdmin);
        Console.WriteLine("✔ Old admin removed");
    }

    // Create new admin
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
            Console.WriteLine("✔ New Admin created: kvuser");
        }
        else
        {
            Console.WriteLine("❌ Admin creation failed: " +
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
    else
    {
        Console.WriteLine("✔ Admin user already exists");
    }
}
