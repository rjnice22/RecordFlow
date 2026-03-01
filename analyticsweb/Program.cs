using analyticsweb.Data;

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;

    //These are the Password complexity rules
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;

    // This makes a minimum unique character
    options.Password.RequiredUniqueChars = 1;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddRazorPages();

builder.Services.AddRateLimiter(options =>
{
    // Return 429 automatically
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // A simple per-IP limiter: allow 10 requests per minute
    options.AddPolicy("auth", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});




var app = builder.Build();





// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    // Basic hardening headers
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    // CSP: start strict-ish but not insane.
    // If you use CDNs (Bootstrap, etc.), we’ll adjust.
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "img-src 'self' data:; " +
        "style-src 'self' 'unsafe-inline'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "frame-ancestors 'none';";

    await next();
});
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter(); // Apply rate limiting globally (you can also apply it to specific endpoints if


app.UseAuthentication();   
app.UseAuthorization();

app.MapRazorPages().RequireRateLimiting("auth");

// Seed an admin user at startup (create if missing; reset password if it already exists)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

    string adminEmail = "admin@analyticsweb.com";
    string adminPassword = "Admin!234";

    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        adminUser = new IdentityUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(adminUser, adminPassword);

        // Optional: write errors to console if something goes wrong
        if (!createResult.Succeeded)
        {
            Console.WriteLine("Admin create failed: " +
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }
    }
    else
    {
        // Force-reset password to the known value
        var token = await userManager.GeneratePasswordResetTokenAsync(adminUser);
        var resetResult = await userManager.ResetPasswordAsync(adminUser, token, adminPassword);

        if (!resetResult.Succeeded)
        {
            Console.WriteLine("Admin password reset failed: " +
                string.Join(", ", resetResult.Errors.Select(e => e.Description)));
        }
    }
}

app.Run();
