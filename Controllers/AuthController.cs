using BCrypt.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SVV.Models;
using System.Security.Claims;

namespace SVV.Controllers
{
    public class AuthController : Controller
    {
        private readonly SvvContext _context;
        private readonly ILogger<AuthController> _logger;

        // CONSTRUCTOR CON INYECCIÓN DE DEPENDENCIAS PARA CONTEXTO DE BD Y LOGGING
        public AuthController(SvvContext context, ILogger<AuthController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // VISTA DE LOGIN CON HEADERS ANTI-CACHÉ PARA SEGURIDAD
        [HttpGet]
        public IActionResult Login()
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.ClearMessages = true;
            return View();
        }

        // PROCESAMIENTO DE AUTENTICACIÓN DE USUARIO
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.ErrorMessage = "Email y contraseña son requeridos";
                return View();
            }

            var empleado = await _context.Empleados
                .Include(e => e.Rol)
                .FirstOrDefaultAsync(e => e.Email == email && e.Activo == true);

            if (empleado == null)
            {
                ViewBag.ErrorMessage = "Credenciales incorrectas";
                return View();
            }

            try
            {
                // VERIFICACIÓN DE CONTRASEÑA CON BCRYPT
                bool passwordCorrecta = BCrypt.Net.BCrypt.Verify(password, empleado.PasswordHash);

                if (!passwordCorrecta)
                {
                    ViewBag.ErrorMessage = "Credenciales incorrectas";
                    return View();
                }

                // DETECCIÓN DE CONTRASEÑA TEMPORAL PARA FORZAR CAMBIO
                bool esContraseñaTemporal = false;
                if (BCrypt.Net.BCrypt.Verify("Temp123!", empleado.PasswordHash))
                {
                    esContraseñaTemporal = true;
                }

                // CREACIÓN DE CLAIMS DE IDENTIDAD PARA EL USUARIO
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, empleado.Id.ToString()),
                    new Claim(ClaimTypes.Name, $"{empleado.Nombre} {empleado.Apellidos}"),
                    new Claim(ClaimTypes.Email, empleado.Email),
                    new Claim("EmpleadoId", empleado.Id.ToString()),
                    new Claim("RolId", empleado.RolId.ToString()),
                    new Claim(ClaimTypes.Role, empleado.Rol?.Codigo ?? "EMPLEADO"),
                    new Claim("LastLogin", DateTime.UtcNow.Ticks.ToString()),
                    new Claim("ContraseñaTemporal", esContraseñaTemporal ? "True" : "False")   
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                    AllowRefresh = true
                };

                // LIMPIEZA DE SESIÓN PREVIA Y CREACIÓN DE NUEVA SESIÓN
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                // REDIRECCIÓN SEGÚN ESTADO DE CONTRASEÑA
                if (esContraseñaTemporal)
                {
                    TempData["PasswordTemporalAlert"] = "Debes cambiar tu contraseña temporal antes de continuar";
                    return RedirectToAction("CambiarPassword", "Perfil", new { forzar = true });
                }

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en autenticación para email: {Email}", email);
                ViewBag.ErrorMessage = "Error en la autenticación";
                return View();
            }
        }

        // MÉTODO LOGOUT CON LIMPIEZA COMPLETA DE SESIÓN Y COOKIES
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try
            {
                _logger.LogInformation("Iniciando logout para usuario: {User}", User?.Identity?.Name ?? "Anónimo");

                HttpContext.Session.Clear();

                foreach (var cookie in Request.Cookies.Keys)
                {
                    Response.Cookies.Delete(cookie, new CookieOptions
                    {
                        Path = "/",
                        Secure = !HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment(),
                        SameSite = SameSiteMode.Lax
                    });
                }

                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                TempData.Clear();

                _logger.LogInformation("Logout completado exitosamente");
                return RedirectToAction("Login", "Auth");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el logout");
                return RedirectToAction("Login", "Auth");
            }
        }

        // ENDPOINT GET PARA LOGOUT (COMPATIBILIDAD CON ENLACES DIRECTOS)
        [HttpGet("logout")]
        public async Task<IActionResult> LogoutGet()
        {
            try
            {
                _logger.LogInformation("Logout GET para usuario: {User}", User?.Identity?.Name ?? "Anónimo");

                HttpContext.Session.Clear();

                foreach (var cookie in Request.Cookies.Keys)
                {
                    Response.Cookies.Delete(cookie, new CookieOptions
                    {
                        Path = "/",
                        Secure = !HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment(),
                        SameSite = SameSiteMode.Lax
                    });
                }

                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                TempData.Clear();

                return RedirectToAction("Login", "Auth");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el logout GET");
                return RedirectToAction("Login", "Auth");
            }
        }

        // VISTA DE ACCESO DENEGADO PARA USUARIOS SIN PERMISOS
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}