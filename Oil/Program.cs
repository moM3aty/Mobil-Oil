using Microsoft.EntityFrameworkCore;
using Oil.Data;
using Oil.Models;
using Oil.Services;
using BCryptAuth = BCrypt.Net.BCrypt; // Alias for hashing

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Configure EF Core with SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// Register FileService
builder.Services.AddScoped<IFileService, FileService>();

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// SEED DATA
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // Apply any pending migrations (recommended over EnsureCreated)
        context.Database.Migrate();

        Console.WriteLine("Seeding started...");
        SeedProductTypes(context);
        Console.WriteLine("Product types seeded.");
        SeedUsers(context);
        Console.WriteLine("Users seeded.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Seed methods
void SeedProductTypes(ApplicationDbContext context)
{
    if (!context.ProductTypes.Any())
    {
        context.ProductTypes.AddRange(new List<ProductType>
        {
            new() { ProductTypeAr = "زيوت السيارات", ProductTypeEn = "Car oils" },
            new() { ProductTypeAr = "زيوت الدراجات النارية", ProductTypeEn = "Motorcycle oils" },
            new() { ProductTypeAr = "زيوت الديزل", ProductTypeEn = "Diesel oils" },
            new() { ProductTypeAr = "زيوت ناقل الحركة", ProductTypeEn = "Transmission oils" },
            new() { ProductTypeAr = "اضافات الزيوت", ProductTypeEn = "Oil additives" },
            new() { ProductTypeAr = "اضافات الوقود", ProductTypeEn = "Fuel additives" },
            new() { ProductTypeAr = "مياة التبريد والاضافات", ProductTypeEn = "Cooling water and additives" },
            new() { ProductTypeAr = "العناية بالسيارة", ProductTypeEn = "Car care" },
            new() { ProductTypeAr = "العناية بالدرجات النارية", ProductTypeEn = "Motorcycle care" },
            new() { ProductTypeAr = "اكسسوارات", ProductTypeEn = "Accessories" },
            new() { ProductTypeAr = "صيانة وإصلاح", ProductTypeEn = "Maintenance and repair" },
            new() { ProductTypeAr = "سكوتر", ProductTypeEn = "Scooter" },
            new() { ProductTypeAr = "قطع غيار", ProductTypeEn = "Spare parts" }
        });
        context.SaveChanges();
    }
}

void SeedUsers(ApplicationDbContext context)
{
    if (!context.Users.Any(u => u.Username == "admin"))
    {
        context.Users.Add(new User
        {
            Username = "admin",
            PasswordHash = BCryptAuth.HashPassword("1i]AXkrz5")
        });
    }

    if (!context.Users.Any(u => u.Username == "testuser"))
    {
        context.Users.Add(new User
        {
            Username = "testuser",
            PasswordHash = BCryptAuth.HashPassword("TestUserPass123")
        });
    }

    context.SaveChanges();
}

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage(); // Shows detailed error info in development
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // Add before UseAuthorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
