using Microsoft.EntityFrameworkCore;
using Oil.Data;
using Oil.Models;
using Oil.Services; // For IFileService
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

// Seed data after the app is built and services are available
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        // Ensure database is created.
        // context.Database.EnsureCreated(); // Or use migrations: context.Database.Migrate();

        SeedProductTypes(context);
        SeedUsers(context); // Call the new seeding method for users
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

void SeedProductTypes(ApplicationDbContext context)
{
    if (!context.ProductTypes.Any())
    {
        context.ProductTypes.AddRange(new List<ProductType>
        {
             new ProductType {ProductTypeAr = "زيوت السيارات", ProductTypeEn = "Car oils" },
             new ProductType {ProductTypeAr = "زيوت الدراجات النارية", ProductTypeEn = "Motorcycle oils" },
             new ProductType {ProductTypeAr = "زيوت الديزل", ProductTypeEn = "Diesel oils" },
             new ProductType {ProductTypeAr = "زيوت ناقل الحركة", ProductTypeEn = "Transmission oils" },
             new ProductType {ProductTypeAr = "اضافات الزيوت", ProductTypeEn = "Oil additives" },
             new ProductType {ProductTypeAr = "اضافات الوقود", ProductTypeEn = "Fuel additives" },
             new ProductType {ProductTypeAr = "مياة التبريد والاضافات", ProductTypeEn = "Cooling water and additives" },
             new ProductType {ProductTypeAr = "العناية بالسيارة", ProductTypeEn = "Car care" },
             new ProductType {ProductTypeAr = "العناية بالدرجات النارية", ProductTypeEn = "Motorcycle care" },
             new ProductType { ProductTypeAr = "اكسسوارات", ProductTypeEn = "Accessories" },
             new ProductType { ProductTypeAr = "صيانة وإصلاح", ProductTypeEn = "Maintenance and repair" },
             new ProductType { ProductTypeAr = "سكوتر", ProductTypeEn = "Scooter" },
             new ProductType { ProductTypeAr = "قطع غيار", ProductTypeEn = "Spare parts" }
        });
        context.SaveChanges();
    }
}

// New method to seed a test user
void SeedUsers(ApplicationDbContext context)
{
    if (!context.Users.Any(u => u.Username == "Info@mobiloil-eg.com")) // Check if admin user already exists
    {
        context.Users.Add(new User
        {
            Username = "admin",
           
            PasswordHash = BCryptAuth.HashPassword("1i]AXkrz5")
        });
        context.SaveChanges();
    }
    // You can add more test users here if needed
    if (!context.Users.Any(u => u.Username == "testuser"))
    {
        context.Users.Add(new User
        {
            Username = "testuser",
            PasswordHash = BCryptAuth.HashPassword("TestUserPass123")
        });
        context.SaveChanges();
    }
}


// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage(); // Useful for seeing detailed errors in development
}


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // Ensure UseSession is called before UseAuthorization and MapControllerRoute

app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();