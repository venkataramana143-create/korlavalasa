using Korlavalasa.Data;
using Korlavalasa.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------------------
// Razor Pages
// ----------------------------------------
builder.Services.AddRazorPages();

// ----------------------------------------
// File Upload Limits
// ----------------------------------------
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

// ----------------------------------------
// DATABASE CONFIG
// ----------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<AppDbContext>(opts =>
        opts.UseSqlServer(connectionString));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(opts =>
        opts.UseNpgsql(connectionString));
}

// Required for PostgreSQL datetime compatibility
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
AppContext.SetSwitch("Npgsql.EnableDateTimeInfinityConversions", true);

// ----------------------------------------
// Identity Configuration
// ----------------------------------------
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

// ----------------------------------------
// Cookies
// ----------------------------------------
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.LoginPath = "/Admin/Login";
    options.AccessDeniedPath = "/Admin/AccessDenied";
    options.SlidingExpiration = true;
});

var app = builder.Build();

// ----------------------------------------
// Pipeline
// ----------------------------------------
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

// ----------------------------------------
// DATABASE INIT (Only Admin User)
// ----------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<AdminUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        Console.WriteLine("🚀 DB Initialization starting...");

        if (await context.Database.CanConnectAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        // Only ensure Admin user exists
        await EnsureAdminUserAlwaysExists(userManager, roleManager);

        Console.WriteLine("🎉 DB Initialization complete");
    }
    catch (Exception ex)
    {
        Console.WriteLine("🔥 DB Error: " + ex.Message);
    }
}

app.Run();

// ----------------------------------------
// KEEP ONLY THIS — Admin creation
// ----------------------------------------
async Task EnsureAdminUserAlwaysExists(
    UserManager<AdminUser> userManager,
    RoleManager<IdentityRole> roleManager)
{
    string adminEmail = "admin@korlavalasa.com";
    string adminUsername = "admin";

    // Create role if missing
    if (!await roleManager.RoleExistsAsync("Admin"))
        await roleManager.CreateAsync(new IdentityRole("Admin"));

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
            await userManager.AddToRoleAsync(admin, "Admin");
        else
            Console.WriteLine("❌ Error creating admin: " +
                string.Join(", ", result.Errors.Select(e => e.Description)));
    }
    else
    {
        Console.WriteLine("✔ Admin user already exists");
    }
}
