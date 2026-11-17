using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Models;

// ------------------------------------------------------------------------------------------
// Nexus Communication System - application start-up.
// 1. Register services (database, Identity logins, MVC).
// 2. Build the app and make sure the five roles exist.
// 3. Configure the HTTP pipeline (errors, HTTPS, static files, login, routing).
// ------------------------------------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args);

// Connection string 'DefaultConnection' comes from appsettings.json (SQL Server).
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Entity Framework Core database context (SQL Server).
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Shows helpful database error pages (for example pending migrations) while developing.
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ASP.NET Core Identity: users, roles and password rules.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        // Password policy: at least 6 characters and one digit (RegisterViewModel uses the same rule).
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Where users are sent when they are not logged in / not allowed to open a page.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// MVC controllers and Razor views.
builder.Services.AddControllersWithViews(options =>
{
    // Friendly messages when a drop-down is left empty or a number is typed wrongly.
    options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(_ => "This field is required.");
    options.ModelBindingMessageProvider.SetMissingBindRequiredValueAccessor(name => $"{name} is required.");
    options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((value, name) => $"'{value}' is not a valid value for {name}.");
    options.ModelBindingMessageProvider.SetUnknownValueIsInvalidAccessor(name => $"The value entered for {name} is not valid.");
    options.ModelBindingMessageProvider.SetValueIsInvalidAccessor(value => $"'{value}' is not valid.");
});

var app = builder.Build();

// Create the five application roles (Admin, RetailEmployee, Technical, Accounts, Customer) if they do not exist yet.
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (await db.Database.CanConnectAsync())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            string[] roles = { "Admin", "RetailEmployee", "Technical", "Accounts", "Customer" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

            // First start: create the default Admin so somebody can log in and create the other employees.
            // Change this password after the first login.
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if ((await userManager.GetUsersInRoleAsync("Admin")).Count == 0)
            {
                var admin = new ApplicationUser { UserName = "admin@nexus.com", Email = "admin@nexus.com", EmailConfirmed = true, FullName = "Administrator" };
                if ((await userManager.CreateAsync(admin, "Admin@123")).Succeeded)
                    await userManager.AddToRoleAsync(admin, "Admin");
            }

            // Existing logins without a name get the name of the matching employee / customer (same e-mail).
            foreach (var u in userManager.Users.Where(x => x.FullName == null).ToList())
            {
                var nm = db.Employees.Where(x => x.Email == u.Email).Select(x => x.Name).FirstOrDefault()
                         ?? db.Customers.Where(x => x.Email == u.Email).Select(x => x.Name).FirstOrDefault();
                if (nm != null) { u.FullName = nm; await userManager.UpdateAsync(u); }
            }

            // Safety: any customer still without a 10-digit code gets one (portable across PCs / DBs).
            try
            {
                var empty = await db.Customers
                    .Where(c => c.CustomerCode == null || c.CustomerCode == "")
                    .OrderBy(c => c.Id).ToListAsync();
                if (empty.Count > 0)
                {
                    long start = 0;
                    var maxCode = await db.Customers
                        .Where(c => c.CustomerCode != null && c.CustomerCode != "")
                        .Select(c => c.CustomerCode).ToListAsync();
                    foreach (var s in maxCode)
                        if (long.TryParse(s, out var n) && n > start) start = n;
                    foreach (var c in empty)
                    {
                        start++;
                        c.CustomerCode = start.ToString("D10");
                    }
                    var seq = await db.SequenceCounters.FirstOrDefaultAsync(s => s.Key == "Customer");
                    if (seq == null)
                        db.SequenceCounters.Add(new SequenceCounter { Key = "Customer", LastValue = start });
                    else if (start > seq.LastValue)
                        seq.LastValue = start;
                    await db.SaveChangesAsync();
                }
            }
            catch { /* column may not exist until migration runs */ }
    }
}
catch { /* DB not ready */ }   // The application still starts when the database has not been created yet.

// Detailed errors in development, friendly error page + HSTS in production.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();   // redirect http -> https
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();  // who is the user?  (must come before authorization)
app.UseAuthorization();   // is the user allowed to open this page?  ([Authorize] attributes)

// Default route: /Controller/Action/Id  (home page = Home/Index).
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
