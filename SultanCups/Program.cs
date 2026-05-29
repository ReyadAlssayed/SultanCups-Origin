using Microsoft.EntityFrameworkCore;
using SultanCups.Components;
using SultanCups.Data;
using SultanCups.Services;
using System;
using System.IO;


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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    var currentGuid =
        DeviceHelper.GetMachineGuid();

    var license = db.system_license
        .FirstOrDefault();

    if (license == null ||
    license.device_guid != currentGuid)
    {
        app.MapGet("/", async context =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";

            await context.Response.WriteAsync("""
        <h2 style="
            text-align:center;
            margin-top:100px;
            font-family:Tahoma;
            color:red;">
            هذا الجهاز غير مصرح له بتشغيل النظام
        </h2>
    """);
        });

        app.Run();

        return;


    }

    // ===== ضع الكود هنا =====

    var today = DateOnly.FromDateTime(DateTime.Today);

    if (license.last_telegram_backup_date != today)
    {
        var adminService =
            scope.ServiceProvider.GetRequiredService<AdminService>();

        var backupFile =
            $@"C:\SultanBackups\backup_{DateTime.Today:yyyy-MM-dd}.backup";

        if (File.Exists(backupFile))
        {
            try
            {
                await adminService.SendBackupToTelegram(backupFile);

                license.last_telegram_backup_date = today;

                await db.SaveChangesAsync();
            }
            catch
            {
            }
        }
    }
}


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