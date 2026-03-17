using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using SVV.Models;
using SVV.Services;
using QuestPDF.Infrastructure;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);
// === FORZAR CULTURA A MÉXICO ===  // 👈 Inserta esto AQUÍ
var defaultCulture = new CultureInfo("es-MX");
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

ExcelPackage.License.SetNonCommercialOrganization("SVV");

// ========== CONFIGURACIÓN DE QUESTPDF ==========
QuestPDF.Settings.License = LicenseType.Community;

// Agregar controladores y vistas (MVC)
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Configurar Entity Framework Core
builder.Services.AddDbContext<SvvContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient("GoogleMaps", client =>
{
    client.BaseAddress = new Uri("https://maps.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Servicios personalizados
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICotizacionService, CotizacionService>();
builder.Services.AddHttpContextAccessor();

// Configurar SMTP
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<CotizacionConfig>(builder.Configuration.GetSection("CotizacionConfig"));

// Registrar renderizador de vistas Razor
builder.Services.AddScoped<IRazorViewToStringRenderer, RazorViewToStringRenderer>();

// Registrar servicio de envío de correos
builder.Services.AddScoped<EmailSender, MailKitEmailSender>();

// Registrar servicios de notificación
builder.Services.AddSingleton<INotificationQueue, InMemoryNotificationQueue>();
builder.Services.AddHostedService<NotificationWorker>();

// CONFIGURACIÓN DE AUTENTICACIÓN 
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "SVV.Auth";
        // 👇 IMPORTANTE: Rutas de login/logout con el prefijo de la aplicación
        options.LoginPath = "/Viaticos/Auth/Login";
        options.LogoutPath = "/Viaticos/Auth/Logout";
        options.AccessDeniedPath = "/Viaticos/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        // 👇 Path de la cookie para que solo se envíe en /Viaticos
        options.Cookie.Path = "/Viaticos";
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
        options.ReturnUrlParameter = "returnUrl";
        options.Events.OnSigningOut = async context =>
        {
            context.HttpContext.Session.Clear();
            context.HttpContext.Response.Cookies.Delete("SVV.Auth");
            context.HttpContext.Response.Cookies.Delete(".AspNetCore.Session");
            await Task.CompletedTask;
        };
    });

// Políticas de autorización
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim("RolId", "4", "5", "6"));
    options.AddPolicy("JefeProyecto", policy => policy.RequireClaim("RolId", "2", "5"));
    options.AddPolicy("Gerente", policy => policy.RequireClaim("RolId", "1", "2", "5", "6"));
    options.AddPolicy("Empleado", policy => policy.RequireClaim("RolId", "1", "2", "3", "4", "5", "6"));
});

// CONFIGURACIÓN DE SESIÓN
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.Cookie.Name = ".AspNetCore.Session";
    // 👇 Path de la cookie de sesión
    options.Cookie.Path = "/Viaticos";
    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
});

var app = builder.Build();

// 👇 1. Primero: definir el path base (indica que la app está en /Viaticos)
app.UsePathBase("/Viaticos");

// Logging solo en desarrollo
if (app.Environment.IsProduction())
{
    app.Use(async (context, next) =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Request: {Method} {Path}", context.Request.Method, context.Request.Path);
        await next();
        logger.LogInformation("Response: {StatusCode}", context.Response.StatusCode);
    });
}

// 2. Middleware de manejo de errores (después de UsePathBase)
if (!app.Environment.IsProduction())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// 3. Archivos estáticos
app.UseStaticFiles();

// 4. Enrutamiento
app.UseRouting();

// 5. Sesión (después de UseRouting)
app.UseSession();

// 6. Autenticación
app.UseAuthentication();

// 7. Middleware anti-caché (opcional, puede ir antes o después de Authorization)
app.Use(async (context, next) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Expires"] = "0";
    }
    await next();
});

// 8. Autorización
app.UseAuthorization();

// 9. Mapeo de rutas
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");
// Rutas
app.MapControllerRoute(
    name: "api",
    pattern: "api/{controller}/{action}/{id?}");

app.Run();