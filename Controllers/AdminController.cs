using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SVV.Models;
using SVV.ViewModels;
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using System.Text;
using SVV.Services;
using Microsoft.Extensions.Configuration;

namespace SVV.Controllers
{
    [Authorize(Policy = "AdminOnly")]
    public class AdminController : Controller
    {
        private readonly SvvContext _context;
        private readonly INotificationQueue _queue;
        private readonly IConfiguration _configuration;



        public AdminController(
            SvvContext context,
            INotificationQueue queue,
            IConfiguration configuration)
        {
            _context = context;
            _queue = queue;
            _configuration = configuration;
        }

        // ACCIÓN PRINCIPAL PARA MOSTRAR EL DASHBOARD ADMINISTRATIVO
        public async Task<IActionResult> Dashboard()
        {
            var stats = await GetDashboardStatsAsync();
            return View(stats);
        }

        // ENDPOINT API PARA OBTENER ESTADÍSTICAS ACTUALIZADAS EN TIEMPO REAL
        [HttpGet]
        public async Task<JsonResult> GetUpdatedStats()
        {
            try
            {
                var stats = await GetDashboardStatsAsync();

                var recentActivities = await _context.SolicitudesViajes
                  .Include(s => s.Empleado)
                    .Include(s => s.Estado)
                    .OrderByDescending(s => s.CreatedAt)
                    .Take(5)
                    .Select(s => new
                    {
                        descripcion = $"Solicitud de {s.Empleado.Nombre} {s.Empleado.Apellidos} - {s.Motivo}",
                        fecha = s.CreatedAt.HasValue ? s.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "Fecha no disponible",
                        estado = s.Estado.Descripcion
                    })
                    .ToListAsync();

                var result = new
                {
                    totalSolicitudes = stats.TotalSolicitudes,
                    solicitudesPendientes = stats.SolicitudesPendientes,
                    solicitudesAprobadas = stats.SolicitudesAprobadas,
                    solicitudesRechazadas = stats.SolicitudesRechazadas,
                    totalEmpleados = stats.TotalEmpleados,
                    viaticosActivos = stats.ViaticosActivos,
                    viaticosEsteMes = stats.ViaticosEsteMes,
                    montoTotalEsteMes = stats.MontoTotalEsteMes,
                    recentActivities = recentActivities,
                    lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetUpdatedStats: {ex.Message}");
                return Json(new { error = "Error al obtener las estadísticas actualizadas" });
            }
        }

        // MÉTODO PRIVADO PARA CALCULAR ESTADÍSTICAS DEL DASHBOARD
        private async Task<DashboardStats> GetDashboardStatsAsync()
        {
            try
            {
                var stats = new DashboardStats
                {
                    TotalEmpleados = await _context.Empleados
                        .Where(e => e.Activo == true)
                        .CountAsync(),

                    TotalSolicitudes = await _context.SolicitudesViajes
                        .CountAsync(),

                    SolicitudesPendientes = await _context.SolicitudesViajes
                        .Include(s => s.Estado)
                        .Where(s => s.Estado.Descripcion != null &&
                                    (s.Estado.Descripcion.ToLower().Contains("pendiente") ||
                                     s.Estado.Descripcion.ToLower().Contains("revisión")))
                        .CountAsync(),

                    SolicitudesAprobadas = await _context.SolicitudesViajes
                        .Include(s => s.Estado)
                        .Where(s => s.Estado.Descripcion != null &&
                                    s.Estado.Descripcion.ToLower().Contains("aprobado"))
                        .CountAsync(),

                    SolicitudesRechazadas = await _context.SolicitudesViajes
                        .Include(s => s.Estado)
                        .Where(s => s.Estado.Descripcion != null &&
                                    s.Estado.Descripcion.ToLower().Contains("rechazado"))
                        .CountAsync(),

                    ViaticosActivos = await _context.SolicitudesViajes
                        .Include(s => s.Estado)
                        .Where(s => s.Estado.Descripcion != null &&
                                    s.Estado.Descripcion.ToLower().Contains("aprobado") &&
                                    s.FechaRegreso >= DateOnly.FromDateTime(DateTime.Now))
                        .CountAsync(),

                    ViaticosEsteMes = await _context.SolicitudesViajes
                        .Include(s => s.Estado)
                        .Where(s => s.Estado.Descripcion != null &&
                                    s.Estado.Descripcion.ToLower().Contains("aprobado") &&
                                    s.CreatedAt.HasValue &&
                                    s.CreatedAt.Value.Month == DateTime.Now.Month &&
                                    s.CreatedAt.Value.Year == DateTime.Now.Year)
                        .CountAsync(),

                    MontoTotalEsteMes = 0
                };

                return stats;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetDashboardStatsAsync: {ex.Message}");
                return new DashboardStats
                {
                    TotalEmpleados = await _context.Empleados.CountAsync(e => e.Activo == true),
                    TotalSolicitudes = await _context.SolicitudesViajes.CountAsync()
                };
            }
        }

        // LISTADO DE EMPLEADOS ACTIVOS CON SUS RELACIONES
        public async Task<IActionResult> Empleados()
        {
            var empleados = await _context.Empleados
                .Include(e => e.Rol)
                .Include(e => e.JefeDirecto)
                .Where(e => e.Activo == true)
                .OrderBy(e => e.Nombre)
                .ToListAsync();

            Console.WriteLine($"Total de empleados activos: {empleados.Count}");
            return View(empleados);
        }

        // FORMULARIO DE CREACIÓN DE NUEVO EMPLEADO
        public async Task<IActionResult> CrearEmpleado()
        {
            try
            {
                ViewBag.Roles = await _context.RolesSistema.ToListAsync();
                ViewBag.Jefes = await _context.Empleados
                    .Where(e => e.Activo == true)
                    .Select(e => new {
                        Id = e.Id,
                        Nombre = $"{e.Nombre} {e.Apellidos}"
                    })
                    .ToListAsync();

                Console.WriteLine("Roles y jefes cargados para creación");
                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al cargar datos para creación: {ex.Message}");
                TempData["Error"] = "Error al cargar los datos del formulario";
                return RedirectToAction("Empleados");
            }
        }

        // PROCESAMIENTO DEL FORMULARIO DE CREACIÓN DE EMPLEADO
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearEmpleado(CrearEmpleadoViewModel viewModel)
        {
            // VALIDACIÓN PERSONALIZADA DE DOMINIO DE CORREO ELECTRÓNICO
            if (!string.IsNullOrEmpty(viewModel.Email))
            {
                var email = viewModel.Email.Trim().ToLower();
                if (!email.EndsWith("@viamtek.com") && !email.EndsWith("@qvitek.com"))
                {
                    ModelState.AddModelError("Email", "El email debe ser del dominio viamtek.com o qvitek.com");
                }
                else
                {
                    viewModel.Email = email;
                }
            }

            if (!ModelState.IsValid)
            {
                Console.WriteLine("ModelState no válido en creación");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"Error: {error.ErrorMessage}");
                }

                TempData["Error"] = "Hay errores de validación en el formulario. Por favor, revisa los campos.";
                await CargarViewBagsCreacion();
                return View(viewModel);
            }

            try
            {
                // VERIFICACIÓN DE UNICIDAD DE CORREO ELECTRÓNICO
                var emailExiste = await _context.Empleados
                    .AnyAsync(e => e.Email == viewModel.Email);

                if (emailExiste)
                {
                    Console.WriteLine($"Email ya existe: {viewModel.Email}");
                    ModelState.AddModelError("Email", "Este email ya está registrado en el sistema");
                    TempData["Error"] = "El email ingresado ya está registrado";
                    await CargarViewBagsCreacion();
                    return View(viewModel);
                }

                // CREACIÓN DEL NUEVO EMPLEADO EN BASE DE DATOS
                var nuevoEmpleado = new Empleados
                {
                    Nombre = viewModel.Nombre?.Trim(),
                    Apellidos = viewModel.Apellidos?.Trim(),
                    Email = viewModel.Email?.Trim().ToLower(),
                    RolId = viewModel.RolId,
                    JefeDirectoId = viewModel.JefeDirectoId,
                    AreaAdscripcion = viewModel.AreaAdscripcion?.Trim(),
                    NivelPuesto = viewModel.NivelPuesto?.Trim(),
                    FechaIngreso = viewModel.FechaIngreso,
                    Departamento = viewModel.Departamento?.Trim(),
                    Puesto = viewModel.Puesto?.Trim(),
                    Telefono = viewModel.Telefono?.Trim(),
                    ColaboradorRemoto = viewModel.ColaboradorRemoto,
                    UbicacionBase = viewModel.UbicacionBase?.Trim(),
                    Activo = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Temp123!")
                };

                Console.WriteLine($"ColaboradorRemoto a guardar en BD: {nuevoEmpleado.ColaboradorRemoto}");

                _context.Empleados.Add(nuevoEmpleado);
                await _context.SaveChangesAsync();

                Console.WriteLine($"Empleado creado exitosamente - ID: {nuevoEmpleado.Id}");

                // ENVÍO ASINCRÓNICO DE CORREO DE BIENVENIDA
                await EnviarCorreoBienvenida(nuevoEmpleado);

                TempData["Success"] = $"Empleado {nuevoEmpleado.Nombre} {nuevoEmpleado.Apellidos} creado exitosamente. Contraseña temporal: Temp123! Se ha enviado un correo con las instrucciones.";

                return RedirectToAction(nameof(Empleados));
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"Error de BD al crear empleado: {dbEx.Message}");
                Console.WriteLine($"Inner Exception: {dbEx.InnerException?.Message}");
                TempData["Error"] = "Error de base de datos al crear el empleado. Verifica los datos e intenta nuevamente.";
                await CargarViewBagsCreacion();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inesperado al crear empleado: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                TempData["Error"] = $"Ocurrió un error inesperado: {ex.Message}";
                await CargarViewBagsCreacion();
                return View(viewModel);
            }
        }

        // ENVÍO DE CORREO DE BIENVENIDA MEDIANTE COLA DE NOTIFICACIONES
        private async Task EnviarCorreoBienvenida(Empleados empleado)
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";

                var subject = "Bienvenido al Sistema de Viáticos de Viamtek";

                _queue.Enqueue(new Services.NotificationItem
                {
                    ToEmail = empleado.Email,
                    Subject = subject,
                    TemplateName = "/Views/Emails/BienvenidaEmpleado.cshtml",
                    Model = new
                    {
                        Empleado = empleado,
                        UrlLogin = $"{baseUrl}/Auth/Login",
                        ContrasenaTemporal = "Temp123!",
                        BaseUrl = baseUrl
                    }
                });

                Console.WriteLine($"Correo de bienvenida encolado para {empleado.Email}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al encolar correo de bienvenida: {ex.Message}");
            }
        }

        // CARGA DE DATOS PARA DROPDOWNS EN CREACIÓN DE EMPLEADOS
        private async Task CargarViewBagsCreacion()
        {
            ViewBag.Roles = await _context.RolesSistema.ToListAsync();
            ViewBag.Jefes = await _context.Empleados
                .Where(e => e.Activo == true)
                .Select(e => new {
                    Id = e.Id,
                    Nombre = $"{e.Nombre} {e.Apellidos}"
                })
                .ToListAsync();
        }

        // FORMULARIO DE EDICIÓN DE EMPLEADO EXISTENTE
        [HttpGet]
        public async Task<IActionResult> EditarEmpleado(int id)
        {
            var empleado = await _context.Empleados.FindAsync(id);

            if (empleado == null)
            {
                TempData["Error"] = "El empleado solicitado no fue encontrado.";
                return RedirectToAction("Empleados");
            }

            var viewModel = new EditarEmpleadoViewModel
            {
                Id = empleado.Id,
                Nombre = empleado.Nombre,
                Apellidos = empleado.Apellidos,
                Email = empleado.Email,
                Telefono = empleado.Telefono,
                RolId = empleado.RolId,
                JefeDirectoId = empleado.JefeDirectoId,
                AreaAdscripcion = empleado.AreaAdscripcion,
                Departamento = empleado.Departamento,
                Puesto = empleado.Puesto,
                NivelPuesto = empleado.NivelPuesto,
                UbicacionBase = empleado.UbicacionBase,
                FechaIngreso = empleado.FechaIngreso,
                ColaboradorRemoto = empleado.ColaboradorRemoto ?? false
            };

            Console.WriteLine($"En GET - ColaboradorRemoto: {viewModel.ColaboradorRemoto}");

            await LoadEditViewBags(id);
            return View(viewModel);
        }

        // EXPORTACIÓN DE LISTA DE EMPLEADOS A ARCHIVO EXCEL
        public async Task<IActionResult> ExportarEmpleadosExcel()
        {
            try
            {
                var empleados = await _context.Empleados
                    .Include(e => e.Rol)
                    .Include(e => e.JefeDirecto)
                    .Where(e => e.Activo == true)
                    .OrderBy(e => e.Nombre)
                    .ThenBy(e => e.Apellidos)
                    .ToListAsync();

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Empleados");
                    var currentRow = 1;

                    worksheet.Cell(currentRow, 1).Value = "ID";
                    worksheet.Cell(currentRow, 2).Value = "Nombre Completo";
                    worksheet.Cell(currentRow, 3).Value = "Email";
                    worksheet.Cell(currentRow, 4).Value = "Rol";
                    worksheet.Cell(currentRow, 5).Value = "Puesto";
                    worksheet.Cell(currentRow, 6).Value = "Departamento";
                    worksheet.Cell(currentRow, 7).Value = "Área de Adscripción";
                    worksheet.Cell(currentRow, 8).Value = "Teléfono";
                    worksheet.Cell(currentRow, 9).Value = "Fecha de Ingreso";
                    worksheet.Cell(currentRow, 10).Value = "Jefe Directo";
                    worksheet.Cell(currentRow, 11).Value = "Colaborador Remoto";
                    worksheet.Cell(currentRow, 12).Value = "Ubicación Base";
                    worksheet.Cell(currentRow, 13).Value = "Nivel de Puesto";
                    worksheet.Cell(currentRow, 14).Value = "Estado";

                    var headerRange = worksheet.Range(1, 1, 1, 14);
                    headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
                    headerRange.Style.Font.FontColor = XLColor.White;
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    foreach (var empleado in empleados)
                    {
                        currentRow++;

                        worksheet.Cell(currentRow, 1).Value = empleado.Id;
                        worksheet.Cell(currentRow, 2).Value = $"{empleado.Nombre} {empleado.Apellidos}";
                        worksheet.Cell(currentRow, 3).Value = empleado.Email;
                        worksheet.Cell(currentRow, 4).Value = empleado.Rol?.Nombre ?? "N/A";
                        worksheet.Cell(currentRow, 5).Value = empleado.Puesto ?? "N/A";
                        worksheet.Cell(currentRow, 6).Value = empleado.Departamento ?? "N/A";
                        worksheet.Cell(currentRow, 7).Value = empleado.AreaAdscripcion ?? "N/A";
                        worksheet.Cell(currentRow, 8).Value = empleado.Telefono ?? "N/A";
                        worksheet.Cell(currentRow, 9).Value = empleado.FechaIngreso?.ToString("dd/MM/yyyy") ?? "N/A";
                        worksheet.Cell(currentRow, 10).Value = empleado.JefeDirecto != null ?
                            $"{empleado.JefeDirecto.Nombre} {empleado.JefeDirecto.Apellidos}" : "N/A";

                        worksheet.Cell(currentRow, 11).Value = (empleado.ColaboradorRemoto ?? false) ? "Sí" : "No";
                        worksheet.Cell(currentRow, 12).Value = empleado.UbicacionBase ?? "N/A";
                        worksheet.Cell(currentRow, 13).Value = empleado.NivelPuesto ?? "N/A";
                        worksheet.Cell(currentRow, 14).Value = (empleado.Activo ?? false) ? "Activo" : "Inactivo";
                    }

                    worksheet.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();

                        return File(
                            content,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"Empleados_Activos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al exportar empleados a Excel: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                TempData["Error"] = "Error al generar el archivo Excel: " + ex.Message;
                return RedirectToAction("Empleados");
            }
        }

        // PROCESAMIENTO DEL FORMULARIO DE EDICIÓN DE EMPLEADO
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarEmpleado(EditarEmpleadoViewModel viewModel)
        {
            foreach (var key in Request.Form.Keys)
            {
                Console.WriteLine($"   {key}: {Request.Form[key]}");
            }
            Console.WriteLine($"ColaboradorRemoto recibido: {viewModel.ColaboradorRemoto}");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("ModelState no válido");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"Error: {error.ErrorMessage}");
                }

                TempData["Error"] = "Hay errores de validación en el formulario. Por favor, revisa los campos.";
                await LoadEditViewBags(viewModel.Id);
                return View(viewModel);
            }

            try
            {
                Console.WriteLine("Buscando empleado existente...");

                var empleadoExistente = await _context.Empleados
                    .FirstOrDefaultAsync(e => e.Id == viewModel.Id);

                if (empleadoExistente == null)
                {
                    Console.WriteLine("Empleado no encontrado");
                    TempData["Error"] = "Error: El empleado a actualizar no existe.";
                    return RedirectToAction("Empleados");
                }

                Console.WriteLine($"Empleado encontrado - ColaboradorRemoto actual: {empleadoExistente.ColaboradorRemoto}");

                empleadoExistente.Nombre = viewModel.Nombre?.Trim();
                empleadoExistente.Apellidos = viewModel.Apellidos?.Trim();
                empleadoExistente.Email = viewModel.Email?.Trim();
                empleadoExistente.Telefono = viewModel.Telefono?.Trim();
                empleadoExistente.RolId = viewModel.RolId;
                empleadoExistente.JefeDirectoId = viewModel.JefeDirectoId;
                empleadoExistente.AreaAdscripcion = viewModel.AreaAdscripcion?.Trim();
                empleadoExistente.Departamento = viewModel.Departamento?.Trim();
                empleadoExistente.Puesto = viewModel.Puesto?.Trim();
                empleadoExistente.NivelPuesto = viewModel.NivelPuesto?.Trim();
                empleadoExistente.UbicacionBase = viewModel.UbicacionBase?.Trim();
                empleadoExistente.FechaIngreso = viewModel.FechaIngreso;
                empleadoExistente.ColaboradorRemoto = viewModel.ColaboradorRemoto;
                empleadoExistente.UpdatedAt = DateTime.Now;

                Console.WriteLine($"ColaboradorRemoto después de asignar: {empleadoExistente.ColaboradorRemoto}");

                _context.Entry(empleadoExistente).Property(e => e.ColaboradorRemoto).IsModified = true;
                _context.Entry(empleadoExistente).Property(e => e.UpdatedAt).IsModified = true;

                var entry = _context.Entry(empleadoExistente);
                Console.WriteLine($"Estado de la entidad: {entry.State}");
                Console.WriteLine($"ColaboradorRemoto IsModified: {entry.Property(e => e.ColaboradorRemoto).IsModified}");

                await _context.SaveChangesAsync();

                Console.WriteLine("Empleado actualizado exitosamente");
                TempData["Success"] = $"El empleado {viewModel.Nombre} {viewModel.Apellidos} ha sido actualizado correctamente.";

                return RedirectToAction("Empleados");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                Console.WriteLine($"Error de concurrencia: {ex.Message}");
                if (!await _context.Empleados.AnyAsync(e => e.Id == viewModel.Id))
                {
                    TempData["Error"] = "El empleado ya no existe.";
                    return RedirectToAction("Empleados");
                }
                else
                {
                    TempData["Error"] = "No se pudo guardar los cambios. Por favor, intenta nuevamente.";
                    await LoadEditViewBags(viewModel.Id);
                    return View(viewModel);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error al actualizar: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                TempData["Error"] = $"Ocurrió un error al actualizar el empleado: {ex.Message}";
                await LoadEditViewBags(viewModel.Id);
                return View(viewModel);
            }
        }

        // CARGA DE DATOS PARA DROPDOWNS EN EDICIÓN DE EMPLEADOS
        private async Task LoadEditViewBags(int currentEmployeeId)
        {
            ViewBag.Roles = await _context.RolesSistema
                .Select(r => new { r.Id, r.Nombre })
                .ToListAsync();

            ViewBag.Jefes = await _context.Empleados
                .Where(e => e.Id != currentEmployeeId && e.Activo == true)
                .Select(e => new {
                    Id = e.Id,
                    Nombre = e.Nombre + " " + e.Apellidos
                })
                .OrderBy(e => e.Nombre)
                .ToListAsync();
        }

        // DESACTIVACIÓN LÓGICA DE EMPLEADO (BORRADO SUAVE)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesactivarEmpleado(int id)
        {
            var empleado = await _context.Empleados.FindAsync(id);
            if (empleado != null)
            {
                empleado.Activo = false;
                empleado.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                TempData["Success"] = "Empleado desactivado exitosamente";
            }

            return RedirectToAction(nameof(Empleados));
        }

        private bool EmpleadoExists(int id)
        {
            return _context.Empleados.Any(e => e.Id == id);
        }

        // CLASE INTERNA PARA REPRESENTAR ESTADÍSTICAS DEL DASHBOARD
        public class DashboardStats
        {
            public int TotalEmpleados { get; set; }
            public int TotalSolicitudes { get; set; }
            public int SolicitudesPendientes { get; set; }
            public int SolicitudesAprobadas { get; set; }
            public int SolicitudesRechazadas { get; set; }
            public int ViaticosActivos { get; set; }
            public int ViaticosEsteMes { get; set; }
            public decimal MontoTotalEsteMes { get; set; }
        }
    }
}