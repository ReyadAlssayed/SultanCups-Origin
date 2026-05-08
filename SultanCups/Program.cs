using Microsoft.EntityFrameworkCore;
using SultanCups.Components;
using SultanCups.Data;
using SultanCups.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// الأفضل Pool بدل AddDbContext العادي
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<SalesService>();
builder.Services.AddTransient<InventoryService>();
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<HrService>();
builder.Services.AddScoped<SultanCups.Services.ToastService>();
builder.Services.AddScoped<UserSession>();
builder.Services.AddScoped<FinanceService2>();
builder.Services.AddScoped<StatsAndArchiveService>();

var app = builder.Build();

// ✅ Warm-up مبكر لـ EF Core وفتح أول اتصال بقاعدة البيانات
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "EF Core warm-up failed during startup.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();