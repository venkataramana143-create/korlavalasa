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


// Database Initialization and Seeding - WITH DETAILED ERROR HANDLING
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<AdminUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        Console.WriteLine("🚀 STARTING DATABASE INITIALIZATION...");

        // Test database connection first
        Console.WriteLine("🔧 Testing database connection...");
        var canConnect = await context.Database.CanConnectAsync();
        Console.WriteLine($"✅ Database connection: {canConnect}");

        if (canConnect)
        {
            // Use EnsureCreated instead of Migrate for PostgreSQL
            Console.WriteLine("🔧 Creating database tables...");
            var created = await context.Database.EnsureCreatedAsync();
            Console.WriteLine($"✅ Database tables created: {created}");

            if (created)
            {
                Console.WriteLine("🔧 Seeding initial data...");
                await SeedInitialDataWithRetry(context, userManager, roleManager);
                Console.WriteLine("✅ Data seeded successfully");
            }
        }

        Console.WriteLine("🎉 DATABASE INITIALIZATION COMPLETED SUCCESSFULLY");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"💥 CRITICAL DATABASE ERROR: {ex.Message}");
        Console.WriteLine($"💥 ERROR TYPE: {ex.GetType().FullName}");

        if (ex.InnerException != null)
        {
            Console.WriteLine($"💥 INNER EXCEPTION: {ex.InnerException.Message}");
            Console.WriteLine($"💥 INNER EXCEPTION TYPE: {ex.InnerException.GetType().FullName}");
        }

        Console.WriteLine($"💥 STACK TRACE: {ex.StackTrace}");

        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "A critical error occurred while initializing the database.");
    }
}

app.Run();

// Seed Initial Data Method with Individual Entity Handling
async Task SeedInitialDataWithRetry(AppDbContext context, UserManager<AdminUser> userManager, RoleManager<IdentityRole> roleManager)
{
    Console.WriteLine("🔧 Starting data seeding with individual entity handling...");

    // 1. Seed Admin Role
    await SeedAdminRole(roleManager);

    // 2. Seed Admin User
    await SeedAdminUser(userManager);

    // 3. Seed VillageInfo
    await SeedVillageInfo(context);

    // 4. Seed News
    await SeedNews(context);

    // 5. Seed Events
    await SeedEvents(context);

    Console.WriteLine("🎉 All data seeded successfully");
}

async Task SeedAdminRole(RoleManager<IdentityRole> roleManager)
{
    try
    {
        Console.WriteLine("🔧 Seeding admin role...");
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            var result = await roleManager.CreateAsync(new IdentityRole("Admin"));
            if (result.Succeeded)
                Console.WriteLine("✅ Admin role created");
            else
                Console.WriteLine($"❌ Failed to create admin role: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
        else
        {
            Console.WriteLine("✅ Admin role already exists");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ERROR seeding admin role: {ex.Message}");
    }
}

async Task SeedAdminUser(UserManager<AdminUser> userManager)
{
    try
    {
        Console.WriteLine("🔧 Seeding admin user...");
        string adminEmail = "admin@korlavalasa.com";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new AdminUser
            {
                UserName = "admin",
                Email = adminEmail,
                FullName = "Korlavalasa Administrator",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                Console.WriteLine("✅ Admin user created successfully");
            }
            else
            {
                Console.WriteLine($"❌ Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            Console.WriteLine("✅ Admin user already exists");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ERROR seeding admin user: {ex.Message}");
    }
}

async Task SeedVillageInfo(AppDbContext context)
{
    try
    {
        Console.WriteLine("🔧 Seeding village info...");
        if (!context.VillageInfo.Any())
        {
            var villageInfo = new VillageInfo
            {
                AboutText = "Welcome to Korlavalasa village official website.",
                History = "Korlavalasa has a rich history and cultural heritage.",
                Population = 2500,
                Area = 15.75m,
                MainCrops = "Rice, Vegetables, Fruits",
                ContactEmail = "info@korlavalasa.com",
                ContactNumber = "+91XXXXXXXXXX",
                Address = "Korlavalasa Village",
                SarpanchName = "Sarpanch Name",
                SecretaryName = "Secretary Name"
            };

            context.VillageInfo.Add(villageInfo);
            await context.SaveChangesAsync();
            Console.WriteLine("✅ Village information seeded successfully");
        }
        else
        {
            Console.WriteLine("✅ Village information already exists");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ERROR seeding village info: {ex.Message}");
        Console.WriteLine($"❌ VILLAGE INFO ERROR DETAILS: {ex}");
    }
}

async Task SeedNews(AppDbContext context)
{
    try
    {
        Console.WriteLine("🔧 Seeding news...");
        if (!context.News.Any())
        {
            var news = new News
            {
                Title = "Welcome to Korlavalasa",
                Content = "Welcome to our village website!",
                PublishedDate = DateTime.Now,
                IsActive = true
            };

            context.News.Add(news);
            await context.SaveChangesAsync();
            Console.WriteLine("✅ News seeded successfully");
        }
        else
        {
            Console.WriteLine("✅ News already exists");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ERROR seeding news: {ex.Message}");
    }
}

async Task SeedEvents(AppDbContext context)
{
    try
    {
        Console.WriteLine("🔧 Seeding events...");
        if (!context.Events.Any())
        {
            var eventItem = new Event
            {
                Title = "Village Meeting",
                Description = "Monthly village meeting",
                EventDate = DateTime.Now.AddDays(7),
                Location = "Village Hall"
            };

            context.Events.Add(eventItem);
            await context.SaveChangesAsync();
            Console.WriteLine("✅ Events seeded successfully");
        }
        else
        {
            Console.WriteLine("✅ Events already exist");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ERROR seeding events: {ex.Message}");
    }
}