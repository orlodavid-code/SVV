using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SVV.Models;
using SVV.ViewModels;
using BCrypt.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace SVV.Controllers
{
    // CONTROLADOR DE PERFIL DE USUARIO - ACCESO SOLO PARA USUARIOS AUTENTICADOS
    [Authorize]
    public class PerfilController : Controller
    {
        private readonly SvvContext _context;
        private const string CONTRASEÑA_TEMPORAL = "Temp123!";

        // INYECCIÓN DE DEPENDENCIAS PARA ACCESO A BASE DE DATOS
        public PerfilController(SvvContext context)
        {
            _context = context;
        }

        // MÉTODOS DE GESTIÓN DE PERFIL

        // MUESTRA INFORMACIÓN DEL PERFIL DEL USUARIO ACTUAL
        public async Task<IActionResult> MiPerfil()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var empleado = await _context.Empleados
                    .Include(e => e.Rol)
                    .Include(e => e.JefeDirecto)
                    .FirstOrDefaultAsync(e => e.Id == userId);

                if (empleado == null)
                {
                    TempData["Error"] = "Usuario no encontrado";
                    return RedirectToAction("Index", "Home");
                }

                return View(empleado);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en MiPerfil: {ex.Message}");
                TempData["Error"] = "Error al cargar el perfil";
                return RedirectToAction("Index", "Home");
            }
        }

        // FORMULARIO PARA CAMBIAR CONTRASEÑA - CON DETECCIÓN DE CONTRASEÑA TEMPORAL
        public async Task<IActionResult> CambiarPassword(string mensaje = null, bool forzar = false)
        {
            // MODE FORZADO PARA CUANDO SE REQUIERE CAMBIO OBLIGATORIO
            if (forzar)
            {
                ViewBag.Forzado = true;
            }

            // MENSAJE PARA SITUACIONES ESPECIALES
            if (!string.IsNullOrEmpty(mensaje))
            {
                ViewBag.Mensaje = mensaje;
            }

            // VERIFICACIÓN DE CONTRASEÑA TEMPORAL (SOLO SI NO ES FORZADO)
            if (!forzar)
            {
                var contraseñaTemporalClaim = User.FindFirst("ContraseñaTemporal");
                if (contraseñaTemporalClaim != null && contraseñaTemporalClaim.Value == "True")
                {
                    ViewBag.EsContraseñaTemporal = true;

                    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                    var empleado = await _context.Empleados.FindAsync(userId);

                    if (empleado != null)
                    {
                        try
                        {
                            bool esTempEnBD = BCrypt.Net.BCrypt.Verify(CONTRASEÑA_TEMPORAL, empleado.PasswordHash);
                            if (!esTempEnBD)
                            {
                                ViewBag.EsContraseñaTemporal = false;
                            }
                        }
                        catch
                        {
                            ViewBag.EsContraseñaTemporal = false;
                        }
                    }
                }
            }

            return View();
        }

        // ============================================
        // MÉTODOS DE CAMBIO DE CONTRASEÑA
        // ============================================

        // PROCESA EL CAMBIO DE CONTRASEÑA CON VALIDACIONES DE SEGURIDAD
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarPassword(CambiarMiPasswordViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var empleado = await _context.Empleados.FindAsync(userId);

                if (empleado == null)
                {
                    TempData["Error"] = "Usuario no encontrado";
                    return RedirectToAction("Index", "Home");
                }

                bool passwordActualCorrecta = false;
                bool esContraseñaTemporalActual = false;

                // VALIDACIÓN DE CONTRASEÑA ACTUAL CON MANEJO DE ERRORES DE HASH
                try
                {
                    passwordActualCorrecta = BCrypt.Net.BCrypt.Verify(viewModel.PasswordActual, empleado.PasswordHash);
                    esContraseñaTemporalActual = BCrypt.Net.BCrypt.Verify(CONTRASEÑA_TEMPORAL, empleado.PasswordHash);
                }
                catch (Exception ex) when (ex.Message.Contains("Invalid salt version"))
                {
                    ModelState.AddModelError("",
                        "Error técnico: Tu contraseña actual tiene un formato incompatible. " +
                        "Por favor, usa la contraseña temporal 'Temp123!' o contacta al administrador.");

                    ViewBag.HashCorrupto = true;
                    return View(viewModel);
                }

                // VALIDACIÓN DE CONTRASEÑA ACTUAL INCORRECTA
                if (!passwordActualCorrecta)
                {
                    ModelState.AddModelError("PasswordActual", "La contraseña actual es incorrecta");

                    var contraseñaTemporalClaim = User.FindFirst("ContraseñaTemporal");
                    if (contraseñaTemporalClaim != null && contraseñaTemporalClaim.Value == "True")
                    {
                        ViewBag.SugerenciaTemp = "Recuerda que tu contraseña temporal es: Temp123!";
                    }

                    return View(viewModel);
                }

                // VALIDACIÓN DE CONTRASEÑA NUEVA IGUAL A TEMPORAL
                if (viewModel.NuevaPassword == CONTRASEÑA_TEMPORAL)
                {
                    ModelState.AddModelError("NuevaPassword",
                        "La nueva contraseña no puede ser igual a la temporal. Por favor, elige una diferente.");
                    return View(viewModel);
                }

                // VALIDACIÓN DE FORTALEZA DE CONTRASEÑA NUEVA
                if (!EsContraseñaFuerte(viewModel.NuevaPassword))
                {
                    ModelState.AddModelError("NuevaPassword",
                        "La contraseña debe tener al menos 8 caracteres, incluyendo mayúsculas, minúsculas, números y caracteres especiales (@, #, $, %, etc.)");
                    return View(viewModel);
                }

                // VALIDACIÓN DE CONFIRMACIÓN DE CONTRASEÑA
                if (viewModel.NuevaPassword != viewModel.ConfirmarPassword)
                {
                    ModelState.AddModelError("ConfirmarPassword", "Las contraseñas no coinciden");
                    return View(viewModel);
                }

                // ACTUALIZACIÓN DE CONTRASEÑA EN BASE DE DATOS
                empleado.PasswordHash = BCrypt.Net.BCrypt.HashPassword(viewModel.NuevaPassword, BCrypt.Net.BCrypt.GenerateSalt(12));
                empleado.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // ACTUALIZACIÓN DE CLAIMS DE AUTENTICACIÓN
                var empleadoCompleto = await _context.Empleados
                    .Include(e => e.Rol)
                    .FirstOrDefaultAsync(e => e.Id == userId);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, empleadoCompleto.Id.ToString()),
                    new Claim(ClaimTypes.Name, $"{empleadoCompleto.Nombre} {empleadoCompleto.Apellidos}"),
                    new Claim(ClaimTypes.Email, empleadoCompleto.Email),
                    new Claim("EmpleadoId", empleadoCompleto.Id.ToString()),
                    new Claim("RolId", empleadoCompleto.RolId.ToString()),
                    new Claim(ClaimTypes.Role, empleadoCompleto.Rol?.Codigo ?? "EMPLEADO"),
                    new Claim("ContraseñaTemporal", "False")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                    });

                // LIMPIEZA DE TEMPDATA Y REDIRECCIÓN SEGÚN TIPO DE CAMBIO
                TempData.Remove("Alerta");
                TempData.Remove("Error");

                if (esContraseñaTemporalActual)
                {
                    TempData["CambioPasswordExitoso"] = "true";
                    return RedirectToAction("Index", "Home", new { area = "" });
                }
                else
                {
                    TempData["Success"] = "¡Contraseña cambiada exitosamente!";
                    return RedirectToAction("MiPerfil", "Perfil", new { area = "" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al cambiar contraseña: {ex.Message}");
                TempData["Error"] = $"Error al cambiar contraseña: {ex.Message}";
                return View(viewModel);
            }
        }

        // ============================================
        // MÉTODOS DE EMERGENCIA Y ADMINISTRACIÓN
        // ============================================

        // RESTABLECE CONTRASEÑA TEMPORAL PARA USUARIOS CON PROBLEMAS DE HASH
        [HttpPost("resetear-temporal")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetearContraseñaTemporal()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var empleado = await _context.Empleados.FindAsync(userId);

                if (empleado == null)
                {
                    TempData["Error"] = "Usuario no encontrado";
                    return RedirectToAction("MiPerfil");
                }

                // GENERACIÓN DE NUEVO HASH BCrypt VÁLIDO
                empleado.PasswordHash = BCrypt.Net.BCrypt.HashPassword(CONTRASEÑA_TEMPORAL, BCrypt.Net.BCrypt.GenerateSalt(12));
                empleado.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // ACTUALIZACIÓN DE CLAIMS CON CONTRASEÑA TEMPORAL
                var empleadoCompleto = await _context.Empleados
                    .Include(e => e.Rol)
                    .FirstOrDefaultAsync(e => e.Id == userId);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, empleadoCompleto.Id.ToString()),
                    new Claim(ClaimTypes.Name, $"{empleadoCompleto.Nombre} {empleadoCompleto.Apellidos}"),
                    new Claim(ClaimTypes.Email, empleadoCompleto.Email),
                    new Claim("EmpleadoId", empleadoCompleto.Id.ToString()),
                    new Claim("RolId", empleadoCompleto.RolId.ToString()),
                    new Claim(ClaimTypes.Role, empleadoCompleto.Rol?.Codigo ?? "EMPLEADO"),
                    new Claim("ContraseñaTemporal", "True")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                    });

                TempData["Success"] = "Contraseña temporal restablecida. Ahora puedes cambiarla.";
                return RedirectToAction("CambiarPassword");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("MiPerfil");
            }
        }

        // CORRIGE HASH DE CONTRASEÑA CORRUPTO - SOLO PARA ADMINISTRADORES
        [Authorize(Roles = "Admin")]
        [HttpPost("corregir-hash/{id}")]
        public async Task<IActionResult> CorregirHashUsuario(int id)
        {
            try
            {
                var empleado = await _context.Empleados.FindAsync(id);

                if (empleado == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }

                // VERIFICACIÓN DE VALIDEZ DEL HASH ACTUAL
                bool hashValido = true;
                try
                {
                    BCrypt.Net.BCrypt.Verify("Temp123!", empleado.PasswordHash);
                }
                catch (Exception ex) when (ex.Message.Contains("Invalid salt version"))
                {
                    hashValido = false;
                }

                // CORRECCIÓN DE HASH SI ES NECESARIO
                if (!hashValido)
                {
                    var hashAntiguo = empleado.PasswordHash;
                    empleado.PasswordHash = BCrypt.Net.BCrypt.HashPassword(CONTRASEÑA_TEMPORAL, BCrypt.Net.BCrypt.GenerateSalt(12));
                    empleado.UpdatedAt = DateTime.Now;

                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = true,
                        message = "Hash corregido exitosamente",
                        antiguo = hashAntiguo,
                        nuevo = empleado.PasswordHash
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = true,
                        message = "El hash ya es válido",
                        hash = empleado.PasswordHash
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================
        // MÉTODOS PRIVADOS DE VALIDACIÓN
        // ============================================

        // VALIDA FORTALEZA DE CONTRASEÑA CON EXPRESIONES REGULARES
        private bool EsContraseñaFuerte(string password)
        {
            // REQUISITOS: Mínimo 8 caracteres, 1 mayúscula, 1 minúscula, 1 número, 1 carácter especial
            var regex = new System.Text.RegularExpressions.Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$");
            return regex.IsMatch(password);
        }
    }
}