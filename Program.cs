using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SVV.Models;
using SVV.Services;

var builder = WebApplication.CreateBuilder(args);

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
//PRUEBA

// CONFIGURACIÓN DE AUTENTICACIÓN 
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Nombre personalizado para la cookie
        options.Cookie.Name = "SVV.Auth";

        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";

        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        // configuración de seguridad de cookies
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? Microsoft.AspNetCore.Http.CookieSecurePolicy.None
            : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;

        options.ReturnUrlParameter = "returnUrl";

        // Evento para limpiar completamente al cerrar sesión
        options.Events.OnSigningOut = async context =>
        {
            // Limpiar la sesión
            context.HttpContext.Session.Clear();

            // Eliminar cookies específicas
            context.HttpContext.Response.Cookies.Delete("SVV.Auth");
            context.HttpContext.Response.Cookies.Delete(".AspNetCore.Session");

            await Task.CompletedTask;
        };
    });

// Configurar políticas de autorización
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("RolId", "4", "5", "6"));
    options.AddPolicy("JefeProyecto", policy =>
        policy.RequireClaim("RolId", "2", "5"));
    options.AddPolicy("Gerente", policy =>
        policy.RequireClaim("RolId", "1", "2", "5", "6"));
    options.AddPolicy("Empleado", policy =>
        policy.RequireClaim("RolId", "1", "2", "3", "4", "5", "6"));
});

//  CONFIGURACIÓN DE SESIÓN MEJORADA (CAMBIOS AQUÍ)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.Cookie.Name = ".AspNetCore.Session"; // Nombre explícito

    // Configurar seguridad según ambiente
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? Microsoft.AspNetCore.Http.CookieSecurePolicy.None
        : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
});

var app = builder.Build();

// Middleware simple de logging
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Request: {Method} {Path}", context.Request.Method, context.Request.Path);
    await next();
    logger.LogInformation("Response: {StatusCode}", context.Response.StatusCode);
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();

// Middleware para prevenir caché en páginas autenticadas
app.Use(async (context, next) =>
{
    // Si el usuario está autenticado, aplicar headers anti-caché
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Expires"] = "0";
    }

    await next();
});

app.UseAuthorization();

// RUTAS DE API
app.MapControllerRoute(
    name: "api",
    pattern: "api/{controller}/{action}/{id?}");

// Ruta por defecto
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();