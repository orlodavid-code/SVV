using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SVV.Filters;
using SVV.Generators;
using QuestPDF.Fluent;
using SVV.Models;
using SVV.Services;
using System.Security.Claims;

namespace SVV.Controllers
{
    [TypeFilter(typeof(CambioPassword))]
    [Authorize]
    public class AprobacionesController : Controller
    {
        private readonly SvvContext _context;
        private readonly ILogger<AprobacionesController> _logger;
        private readonly INotificationQueue _queue;
        private readonly IConfiguration _configuration;

        // CONSTRUCTOR CON INYECCIÓN DE DEPENDENCIAS PARA CONTEXTO, LOGGING, COLA DE NOTIFICACIONES Y CONFIGURACIÓN
        public AprobacionesController(
            SvvContext context,
            ILogger<AprobacionesController> logger,
            INotificationQueue queue,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _queue = queue;
            _configuration = configuration;
        }

        // VISTA PRINCIPAL DE SOLICITUDES PENDIENTES SEGÚN ROL DEL USUARIO
        public async Task<IActionResult> Pendientes()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var empleado = await _context.Empleados
                    .AsNoTracking()
                    .Include(e => e.Rol)
                    .FirstOrDefaultAsync(e => e.Id == userId);

                if (empleado == null)
                {
                    TempData["Error"] = "Usuario no encontrado.";
                    return RedirectToAction("Index", "Home");
                }

                ViewBag.RolId = empleado.RolId;
                ViewBag.RolNombre = empleado.Rol?.Nombre ?? "Usuario";

                var solicitudesIds = await GetSolicitudesIdsOptimized(empleado.RolId, userId);

                var solicitudes = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .Where(s => solicitudesIds.Contains(s.Id))
                    .Select(s => new SolicitudesViaje
                    {
                        Id = s.Id,
                        CodigoSolicitud = s.CodigoSolicitud,
                        Destino = s.Destino,
                        Motivo = s.Motivo,
                        FechaSalida = s.FechaSalida,
                        FechaRegreso = s.FechaRegreso,
                        EstadoId = s.EstadoId,
                        CreatedAt = s.CreatedAt,
                        RequiereAnticipo = s.RequiereAnticipo,
                        MontoAnticipo = s.MontoAnticipo,
                        NombreProyecto = s.NombreProyecto,
                        Empleado = new Empleados
                        {
                            Nombre = s.Empleado.Nombre,
                            Apellidos = s.Empleado.Apellidos,
                            JefeDirectoId = s.Empleado.JefeDirectoId
                        },
                        Estado = s.Estado,
                        TipoViatico = s.TipoViatico
                    })
                    .OrderByDescending(s => s.CreatedAt)
                    .ToListAsync();

                return View(solicitudes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar solicitudes pendientes");
                TempData["Error"] = "Error al cargar las solicitudes pendientes.";
                return RedirectToAction("Index", "Home");
            }
        }

        // OBTIENE LISTA DE IDs DE SOLICITUDES SEGÚN ROL Y PERMISOS
        private async Task<List<int>> GetSolicitudesIdsOptimized(int rolId, int userId)
        {
            var query = _context.SolicitudesViajes
                .AsNoTracking()
                .Where(s => s.EstadoId != 1 && s.EstadoId != 10);

            switch (rolId)
            {
                case 2: // Jefe directo - solo solicitudes de sus subordinados
                    query = query.Where(s => s.Empleado.JefeDirectoId == userId && s.EstadoId == 2);
                    break;
                case 3: // RH - puede ver todas las solicitudes en flujo (solo lectura)
                    query = query.Where(s => s.EstadoId != 1 && s.EstadoId != 10);
                    break;
                case 4: // Finanzas - solicitudes en estado 6 o 7
                    query = query.Where(s => s.EstadoId == 6 || s.EstadoId == 7);
                    break;
                case 5: // Dirección - solicitudes en estado 8
                    query = query.Where(s => s.EstadoId == 8);
                    break;
                case 6: // Admin - puede ver todo y también actúa como jefe directo
                    var comoJefe = await _context.SolicitudesViajes
                        .AsNoTracking()
                        .Where(s => s.Empleado.JefeDirectoId == userId && s.EstadoId == 2)
                        .Select(s => s.Id)
                        .ToListAsync();

                    var todas = await query
                        .Select(s => s.Id)
                        .ToListAsync();

                    return comoJefe.Concat(todas).Distinct().ToList();
                default:
                    // Para cualquier rol, verificar si es jefe directo
                    return await query
                        .Where(s => s.Empleado.JefeDirectoId == userId && s.EstadoId == 2)
                        .Select(s => s.Id)
                        .ToListAsync();
            }

            return await query
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.Id)
                .ToListAsync();
        }

        // VISTA DETALLADA DE UNA SOLICITUD CON VALIDACIÓN DE PERMISOS
        public async Task<IActionResult> Detalles(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var solicitud = await _context.SolicitudesViajes
                    .Include(s => s.Empleado)
                    .ThenInclude(e => e.JefeDirecto)
                    .Include(s => s.Estado)
                    .Include(s => s.Anticipos)
                    .Include(s => s.TipoViatico)
                    .Include(s => s.FlujoAprobaciones)
                    .ThenInclude(f => f.EmpleadoAprobador)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (solicitud == null)
                {
                    TempData["Error"] = "Solicitud no encontrada.";
                    return RedirectToAction(nameof(Pendientes));
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var empleado = await _context.Empleados
                    .Include(e => e.Rol)
                    .FirstOrDefaultAsync(e => e.Id == userId);

                if (empleado == null)
                {
                    TempData["Error"] = "Usuario no encontrado.";
                    return RedirectToAction(nameof(Pendientes));
                }

                ViewBag.EsRH = (empleado.RolId == 3);
                ViewBag.TiposViatico = await _context.TiposViatico.ToListAsync();
                if (empleado.RolId == 3) // RH solo lectura
                {
                    if (solicitud.EstadoId == 1 || solicitud.EstadoId == 10)
                    {
                        TempData["Error"] = "No tienes permisos para ver esta solicitud.";
                        return RedirectToAction(nameof(Pendientes));
                    }
                    ViewBag.EsRH = true;
                }
                else if (!await PuedeAprobarAsync(empleado, solicitud))
                {
                    TempData["Error"] = "No tienes permisos para ver esta solicitud.";
                    return RedirectToAction(nameof(Pendientes));
                }

                ViewBag.RolId = empleado.RolId;
                ViewBag.PuedeAprobar = await PuedeAprobarAsync(empleado, solicitud);

                return View(solicitud);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar detalles de solicitud");
                TempData["Error"] = "Error al cargar los detalles de la solicitud.";
                return RedirectToAction(nameof(Pendientes));
            }
        }

        // ENDPOINT PARA APROBAR SOLICITUD
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aprobar(int id)
        {
            return await ProcesarAprobacion(id, true);
        }

        // ENDPOINT PARA RECHAZAR SOLICITUD
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rechazar(int id)
        {
            return await ProcesarAprobacion(id, false);
        }

        // ACTUALIZACIÓN DE TIPO DE VIÁTICO POR PARTE DE FINANZAS
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarTipoViatico(int id, int? NuevoTipoViaticoId)
        {
            try
            {
                _logger.LogInformation("ACTUALIZAR TIPO VIÁTICO - Solicitud ID: {SolicitudId}, Nuevo Tipo ID: {NuevoTipoId}",
                    id, NuevoTipoViaticoId);

                var solicitud = await _context.SolicitudesViajes
                    .Include(s => s.TipoViatico)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (solicitud == null)
                {
                    TempData["Error"] = "Solicitud no encontrada";
                    return RedirectToAction(nameof(Pendientes));
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var empleado = await _context.Empleados
                    .Include(e => e.Rol)
                    .FirstOrDefaultAsync(e => e.Id == userId);

                if (empleado == null || empleado.RolId != 4)
                {
                    TempData["Error"] = "No tienes permisos para realizar esta acción.";
                    return RedirectToAction(nameof(Pendientes));
                }

                string tipoAnteriorNombre = solicitud.TipoViatico?.Nombre ?? "N/A";
                string tipoNuevoNombre = tipoAnteriorNombre;
                bool tipoCambiado = false;

                // VALIDACIÓN Y ACTUALIZACIÓN DEL TIPO DE VIÁTICO EN BASE DE DATOS
                if (NuevoTipoViaticoId.HasValue && NuevoTipoViaticoId.Value > 0)
                {
                    var nuevoTipo = await _context.TiposViatico
                        .FirstOrDefaultAsync(t => t.Id == NuevoTipoViaticoId.Value);

                    if (nuevoTipo != null && nuevoTipo.Id != solicitud.TipoViaticoId)
                    {
                        solicitud.TipoViaticoId = nuevoTipo.Id;
                        tipoNuevoNombre = nuevoTipo.Nombre;
                        tipoCambiado = true;

                        var flujoCambioTipo = new FlujoAprobaciones
                        {
                            SolicitudViajeId = solicitud.Id,
                            Etapa = "FINANZAS",
                            EmpleadoAprobadorId = userId,
                            EstadoAprobacion = "APROBADO",
                            Comentarios = $"Tipo de viático cambiado de '{tipoAnteriorNombre}' a '{tipoNuevoNombre}' por Finanzas.",
                            OrdenEtapa = 4,
                            FechaAprobacion = DateTime.Now,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };

                        _context.FlujoAprobaciones.Add(flujoCambioTipo);
                        solicitud.UpdatedAt = DateTime.Now;
                        await _context.SaveChangesAsync();

                        await _context.Entry(solicitud)
                            .Reference(s => s.TipoViatico)
                            .LoadAsync();

                        TempData["Success"] = $"Tipo de viático actualizado: '{tipoAnteriorNombre}' → '{tipoNuevoNombre}'";
                    }
                    else if (nuevoTipo == null)
                    {
                        TempData["Error"] = "El tipo de viático seleccionado no existe.";
                    }
                    else
                    {
                        TempData["Info"] = "El tipo de viático seleccionado es el mismo que el actual.";
                    }
                }
                else
                {
                    TempData["Warning"] = "No se seleccionó un nuevo tipo de viático.";
                }

                return RedirectToAction(nameof(Detalles), new { id });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Error de BD al actualizar tipo de viático para solicitud {SolicitudId}", id);

                // MANEJO ESPECÍFICO DE ERROR DE RESTRICCIÓN CHECK EN BASE DE DATOS
                if (dbEx.InnerException != null && dbEx.InnerException is SqlException sqlEx && sqlEx.Number == 547)
                {
                    TempData["Error"] = "Error de validación en la base de datos. El valor para 'estado_aprobacion' no es válido. " +
                                       "Contacte al administrador si necesita agregar nuevos estados.";
                }
                else
                {
                    TempData["Error"] = $"Error al actualizar el tipo de viático: {dbEx.InnerException?.Message ?? dbEx.Message}";
                }

                return RedirectToAction(nameof(Detalles), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar tipo de viático para solicitud {SolicitudId}", id);
                TempData["Error"] = $"Error al actualizar el tipo de viático: {ex.Message}";
                return RedirectToAction(nameof(Detalles), new { id });
            }
        }

        // APROBACIÓN POR FINANZAS CON OPCIONAL CAMBIO DE TIPO DE VIÁTICO
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AprobarConTipoViatico(int id)
        {
            try
            {
                _logger.LogInformation("APROBAR Y ENVIAR A DIRECCIÓN - Solicitud ID: {SolicitudId}", id);

                var solicitud = await _context.SolicitudesViajes
                    .Include(s => s.Estado)
                    .Include(s => s.TipoViatico)
                    .Include(s => s.Empleado)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (solicitud == null)
                {
                    TempData["Error"] = "Solicitud no encontrada";
                    return RedirectToAction(nameof(Pendientes));
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var empleado = await _context.Empleados
                    .Include(e => e.Rol)
                    .FirstOrDefaultAsync(e => e.Id == userId);

                if (empleado == null)
                {
                    TempData["Error"] = "Usuario no encontrado.";
                    return RedirectToAction(nameof(Pendientes));
                }

                // VERIFICACIÓN DE PERMISOS EXCLUSIVOS PARA FINANZAS
                if (empleado.RolId != 4 || (solicitud.EstadoId != 6 && solicitud.EstadoId != 7))
                {
                    TempData["Error"] = "No tienes permisos para realizar esta acción.";
                    return RedirectToAction(nameof(Pendientes));
                }

                // BÚSQUEDA DEL ÚLTIMO CAMBIO DE TIPO PARA INCLUIR EN COMENTARIOS
                var ultimoCambioTipo = await _context.FlujoAprobaciones
                    .Where(f => f.SolicitudViajeId == id &&
                               f.EstadoAprobacion == "Modificado" &&
                               f.Comentarios.Contains("Tipo de viático cambiado"))
                    .OrderByDescending(f => f.CreatedAt)
                    .FirstOrDefaultAsync();

                string comentariosAprobacion = "Solicitud aprobada por Finanzas y enviada a Dirección.";

                if (ultimoCambioTipo != null)
                {
                    comentariosAprobacion = ultimoCambioTipo.Comentarios + " " + comentariosAprobacion;

                    var tipoAnterior = ultimoCambioTipo.Comentarios
                        .Split("de '")[1].Split("' a")[0];
                    var tipoNuevo = ultimoCambioTipo.Comentarios
                        .Split("a '")[1].Split("'")[0];

                    TempData["Success"] = $"Solicitud aprobada. Tipo de viático actualizado: '{tipoAnterior}' → '{tipoNuevo}'";
                }
                else
                {
                    TempData["Success"] = "Solicitud aprobada exitosamente (tipo de viático sin cambios).";
                }

                // ACTUALIZACIÓN DEL ESTADO A ENVIADO A DIRECCIÓN
                solicitud.EstadoId = 8;
                solicitud.UpdatedAt = DateTime.Now;

                var flujoAprobacion = new FlujoAprobaciones
                {
                    SolicitudViajeId = solicitud.Id,
                    Etapa = "FINANZAS",
                    EmpleadoAprobadorId = userId,
                    EstadoAprobacion = "Aprobado",
                    Comentarios = comentariosAprobacion,
                    OrdenEtapa = 4,
                    FechaAprobacion = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.FlujoAprobaciones.Add(flujoAprobacion);
                await _context.SaveChangesAsync();

                // NOTIFICACIÓN A DIRECCIÓN PARA CONTINUAR EL FLUJO
                await EnviarNotificacionSiguienteEtapa(solicitud, "DIRECCION");

                return RedirectToAction(nameof(Pendientes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AprobarConTipoViatico para solicitud {SolicitudId}", id);
                TempData["Error"] = $"Error al aprobar la solicitud: {ex.Message}";
                return RedirectToAction(nameof(Detalles), new { id });
            }
        }

        // MÉTODO PRIVADO PRINCIPAL PARA PROCESAR APROBACIONES Y RECHAZOS
        private async Task<IActionResult> ProcesarAprobacion(int solicitudId, bool aprobar)
        {
            try
            {
                var solicitud = await _context.SolicitudesViajes
                    .Include(s => s.Empleado)
                    .ThenInclude(e => e.JefeDirecto)
                    .Include(s => s.Estado)
                    .Include(s => s.TipoViatico)
                    .FirstOrDefaultAsync(s => s.Id == solicitudId);

                if (solicitud == null)
                {
                    TempData["Error"] = "Solicitud no encontrada.";
                    return RedirectToAction(nameof(Pendientes));
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var empleado = await _context.Empleados
                    .Include(e => e.Rol)
                    .FirstOrDefaultAsync(e => e.Id == userId);

                if (empleado == null)
                {
                    TempData["Error"] = "Usuario no encontrado.";
                    return RedirectToAction(nameof(Pendientes));
                }

                // VALIDACIÓN ESPECIAL PARA RH (SOLO VISUALIZACIÓN)
                if (empleado.RolId == 3)
                {
                    TempData["Error"] = "Recursos Humanos solo puede visualizar las solicitudes, no tiene permisos para aprobar o rechazar.";
                    return RedirectToAction(nameof(Pendientes));
                }

                // VERIFICACIÓN DE PERMISOS DE APROBACIÓN SEGÚN ROL
                if (!await PuedeAprobarAsync(empleado, solicitud))
                {
                    TempData["Error"] = "No tienes permisos para aprobar esta solicitud.";
                    return RedirectToAction(nameof(Pendientes));
                }

                string comentariosEspecificos = aprobar ? "Aprobado" : "Rechazado";

                if (!aprobar)
                {
                    // LÓGICA DE RECHAZO COMPLETO
                    solicitud.EstadoId = 10;
                    TempData["Success"] = "Solicitud rechazada exitosamente.";
                    await EnviarNotificacionRechazo(solicitud, empleado);
                }
                else
                {
                    // LÓGICA DE APROBACIÓN SEGÚN ROL DEL USUARIO
                    switch (empleado.RolId)
                    {
                        case 2: // Jefe Directo
                        case 6 when solicitud.Empleado.JefeDirectoId == userId: // Admin como jefe directo
                            if (solicitud.Empleado.JefeDirectoId != userId)
                            {
                                TempData["Error"] = "Solo puedes aprobar solicitudes de tus subordinados directos.";
                                return RedirectToAction(nameof(Pendientes));
                            }
                            solicitud.EstadoId = 6; // ENVIADA_FINANZAS
                            TempData["Success"] = "Solicitud aprobada y enviada a Finanzas.";
                            await EnviarNotificacionSiguienteEtapa(solicitud, "FINANZAS");
                            break;

                        case 4: // Finanzas - redirige a método específico
                            TempData["Info"] = "Para aprobar solicitudes desde Finanzas, utiliza el botón 'Aprobar Solicitud' en la sección de cambio de tipo de viático.";
                            return RedirectToAction(nameof(Detalles), new { id = solicitudId });

                        case 5: // Dirección
                        case 6 when solicitud.EstadoId == 8: // Admin como dirección
                            if (solicitud.EstadoId == 8)
                            {
                                solicitud.EstadoId = 9; // APROBADA_DIRECCION
                                TempData["Success"] = "Solicitud aprobada exitosamente.";

                                // CREACIÓN DE ANTICIPO SI NO EXISTE
                                var anticipoExistente = await _context.Anticipos
                                    .FirstOrDefaultAsync(a => a.SolicitudViajeId == solicitud.Id);

                                if (anticipoExistente == null && solicitud.MontoAnticipo.HasValue)
                                {
                                    var anticipo = new Anticipos
                                    {
                                        SolicitudViajeId = solicitud.Id,
                                        CodigoAnticipo = GenerarCodigoAnticipo(),
                                        MontoSolicitado = solicitud.MontoAnticipo.Value,
                                        MontoAutorizado = solicitud.MontoAnticipo.Value,
                                        Estado = "Aprobado",
                                        FechaSolicitud = solicitud.CreatedAt ?? DateTime.Now,
                                        FechaAutorizacion = DateTime.Now,
                                        AutorizadoPorId = userId,
                                        CreatedAt = DateTime.Now
                                    };
                                    _context.Anticipos.Add(anticipo);
                                }

                                // NOTIFICACIONES A EMPLEADO Y FINANZAS
                                await EnviarNotificacionAprobacionFinal(solicitud, empleado);
                                await EnviarNotificacionAprobacionFinanzas(solicitud, empleado);
                            }
                            else
                            {
                                TempData["Error"] = "No puedes aprobar esta solicitud en el estado actual.";
                                return RedirectToAction(nameof(Pendientes));
                            }
                            break;

                        default:
                            TempData["Error"] = "No tienes permisos para aprobar solicitudes.";
                            return RedirectToAction(nameof(Pendientes));
                    }
                }

                // REGISTRO EN FLUJO DE APROBACIONES (EXCEPTO FINANZAS)
                if (empleado.RolId != 4 || !aprobar)
                {
                    solicitud.UpdatedAt = DateTime.Now;

                    string etapaValida = DetermineEtapaAprobacion(empleado, solicitud, userId);

                    var flujoAprobacion = new FlujoAprobaciones
                    {
                        SolicitudViajeId = solicitud.Id,
                        Etapa = etapaValida,
                        EmpleadoAprobadorId = userId,
                        EstadoAprobacion = aprobar ? "Aprobado" : "Rechazado",
                        Comentarios = comentariosEspecificos,
                        OrdenEtapa = empleado.RolId,
                        FechaAprobacion = DateTime.Now,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    _context.FlujoAprobaciones.Add(flujoAprobacion);
                    _context.SolicitudesViajes.Update(solicitud);
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Pendientes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar aprobación");
                TempData["Error"] = "Error al procesar la solicitud: " + ex.Message;
                return RedirectToAction(nameof(Pendientes));
            }
        }

        // DETERMINA LA ETAPA DE APROBACIÓN SEGÚN ROL Y CONTEXTO
        private string DetermineEtapaAprobacion(Empleados empleado, SolicitudesViaje solicitud, int userId)
        {
            if (empleado.RolId == 6 && solicitud.Empleado.JefeDirectoId == userId)
                return "JEFE_INMEDIATO";
            else if (empleado.RolId == 6 && solicitud.EstadoId == 8)
                return "DIRECCION";
            else
                return empleado.Rol.Codigo switch
                {
                    "JP" => "JEFE_INMEDIATO",
                    "FINANZAS" => "FINANZAS",
                    "DIRECCION" => "DIRECCION",
                    "ADMIN" => "DIRECCION",
                    _ => "JEFE_INMEDIATO"
                };
        }
        // VALIDACIÓN DE PERMISOS DE APROBACIÓN SEGÚN ROL Y ESTADO
        private async Task<bool> PuedeAprobarAsync(Empleados empleado, SolicitudesViaje solicitud)
        {
            // VERIFICACIÓN DE JEFE DIRECTO (INDEPENDIENTEMENTE DEL ROL)
            if (solicitud.Empleado.JefeDirectoId == empleado.Id && solicitud.EstadoId == 2)
            {
                return true;
            }

            switch (empleado.RolId)
            {
                case 2: // Jefe Directo
                    return solicitud.EstadoId == 2 && solicitud.Empleado.JefeDirectoId == empleado.Id;

                case 3: // RH - solo visualización
                    return false;

                case 4: // Finanzas
                    return solicitud.EstadoId == 6 || solicitud.EstadoId == 7;

                case 5: // Dirección
                    return solicitud.EstadoId == 8;

                case 6: // Admin (puede actuar como jefe directo o dirección)
                    if (solicitud.Empleado.JefeDirectoId == empleado.Id && solicitud.EstadoId == 2)
                        return true;
                    else if (solicitud.EstadoId == 8)
                        return true;
                    else
                        return false;

                default:
                    return false;
            }
        }

        // NOTIFICACIÓN DE RECHAZO AL EMPLEADO SOLICITANTE
        private async Task EnviarNotificacionRechazo(SolicitudesViaje solicitud, Empleados empleadoAprobador)
        {
            try
            {
                var empleadoSolicitante = await _context.Empleados.FindAsync(solicitud.EmpleadoId);
                if (empleadoSolicitante == null || string.IsNullOrEmpty(empleadoSolicitante.Email))
                {
                    _logger.LogWarning("No se pudo notificar al empleado solicitante, email no encontrado.");
                    return;
                }

                var urlSolic = $"{Request.Scheme}://{Request.Host}/Solicitudes/Detalles/{solicitud.Id}";
                string subject = $"Solicitud de Viáticos RECHAZADA - {solicitud.CodigoSolicitud}";

                _queue.Enqueue(new Services.NotificationItem
                {
                    ToEmail = empleadoSolicitante.Email,
                    Subject = subject,
                    TemplateName = "/Views/Emails/SolicitudAprobadaRechazada.cshtml",
                    Model = new
                    {
                        Solicitud = solicitud,
                        Url = urlSolic,
                        Aprobada = false,
                        Aprobador = ObtenerNombreCompletoEmpleado(empleadoAprobador),
                        Comentarios = "La solicitud ha sido rechazada."
                    }
                });

                _logger.LogInformation("Notificación de rechazo enviada a {Email}", empleadoSolicitante.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de rechazo");
            }
        }

        // NOTIFICACIÓN A LA SIGUIENTE ETAPA DEL FLUJO DE APROBACIÓN
        private async Task EnviarNotificacionSiguienteEtapa(SolicitudesViaje solicitud, string siguienteEtapa)
        {
            try
            {
                var emails = await ObtenerEmailsPorRol(siguienteEtapa);
                if (!emails.Any())
                {
                    _logger.LogWarning("No se encontraron emails para la etapa: {Etapa}", siguienteEtapa);
                    return;
                }

                var urlSolic = $"{Request.Scheme}://{Request.Host}/Aprobaciones/Detalles/{solicitud.Id}";
                string subject = $"Solicitud de Viáticos para aprobación - {solicitud.CodigoSolicitud}";

                foreach (var email in emails)
                {
                    _queue.Enqueue(new Services.NotificationItem
                    {
                        ToEmail = email,
                        Subject = subject,
                        TemplateName = "/Views/Emails/SolicitudCreada.cshtml",
                        Model = new
                        {
                            Solicitud = solicitud,
                            Url = urlSolic,
                            EsEnvioAprobacion = true,
                            EtapaDestino = siguienteEtapa,
                            EmpleadoSolicitante = $"{solicitud.Empleado?.Nombre} {solicitud.Empleado?.Apellidos}",
                            TipoViatico = solicitud.TipoViatico?.Nombre ?? "N/A",
                            MontoAnticipo = solicitud.MontoAnticipo?.ToString("C") ?? "N/A",
                            Destino = solicitud.Destino,
                            EsNotificacionMultiple = true,
                            EsBorrador = false
                        }
                    });
                }

                _logger.LogInformation("Notificación enviada a {Etapa}: {Emails}", siguienteEtapa, string.Join(", ", emails));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación a siguiente etapa");
            }
        }

        // NOTIFICACIÓN DE APROBACIÓN FINAL AL EMPLEADO SOLICITANTE
        private async Task EnviarNotificacionAprobacionFinal(SolicitudesViaje solicitud, Empleados empleadoAprobador)
        {
            try
            {
                var empleadoSolicitante = await _context.Empleados.FindAsync(solicitud.EmpleadoId);
                if (empleadoSolicitante == null || string.IsNullOrEmpty(empleadoSolicitante.Email))
                {
                    _logger.LogWarning("No se pudo notificar al empleado solicitante, email no encontrado.");
                    return;
                }

                var urlSolic = $"{Request.Scheme}://{Request.Host}/Solicitudes/Detalles/{solicitud.Id}";
                string subject = $"Solicitud de Viáticos APROBADA - {solicitud.CodigoSolicitud}";

                _queue.Enqueue(new Services.NotificationItem
                {
                    ToEmail = empleadoSolicitante.Email,
                    Subject = subject,
                    TemplateName = "/Views/Emails/SolicitudAprobadaRechazada.cshtml",
                    Model = new
                    {
                        Solicitud = solicitud,
                        Url = urlSolic,
                        Aprobada = true,
                        Aprobador = ObtenerNombreCompletoEmpleado(empleadoAprobador),
                        Comentarios = "¡Felicidades! Tu solicitud ha sido aprobada completamente."
                    }
                });

                _logger.LogInformation("Notificación de aprobación final enviada a {Email}", empleadoSolicitante.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de aprobación final");
            }
        }

        // NOTIFICACIÓN A FINANZAS CUANDO DIRECCIÓN HA APROBADO
        private async Task EnviarNotificacionAprobacionFinanzas(SolicitudesViaje solicitud, Empleados empleadoAprobador)
        {
            try
            {
                var emailsFinanzas = await ObtenerEmailsPorRol("FINANZAS");
                if (!emailsFinanzas.Any())
                {
                    _logger.LogWarning("No se encontraron emails para Finanzas al notificar aprobación de Dirección.");
                    return;
                }

                var urlSolic = $"{Request.Scheme}://{Request.Host}/Solicitudes/Detalles/{solicitud.Id}";
                string subject = $"Solicitud APROBADA por Dirección - {solicitud.CodigoSolicitud}";

                foreach (var email in emailsFinanzas)
                {
                    _queue.Enqueue(new Services.NotificationItem
                    {
                        ToEmail = email,
                        Subject = subject,
                        TemplateName = "/Views/Emails/SolicitudAprobadaRechazada.cshtml",
                        Model = new
                        {
                            Solicitud = solicitud,
                            Url = urlSolic,
                            Aprobada = true,
                            Aprobador = ObtenerNombreCompletoEmpleado(empleadoAprobador),
                            Comentarios = "La solicitud ha sido aprobada por Dirección General y está lista para procesar el anticipo.",
                            EsParaFinanzas = true
                        }
                    });
                }

                _logger.LogInformation("Notificación de aprobación enviada a Finanzas para la solicitud {CodigoSolicitud}", solicitud.CodigoSolicitud);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación a Finanzas sobre aprobación de Dirección");
            }
        }

        // OBTIENE LISTA DE EMAILS POR ROL SEGÚN CONFIGURACIÓN
        private async Task<List<string>> ObtenerEmailsPorRol(string rolCodigo)
        {
            var emails = new List<string>();

            try
            {
                var empleados = await _context.Empleados
           .Include(e => e.Rol)
           .Where(e => e.Activo == true &&
                      !string.IsNullOrEmpty(e.Email) &&
                      e.Rol.Codigo == rolCodigo)
           .ToListAsync();

                emails = empleados.Select(e => e.Email).Distinct().ToList();


                _logger.LogInformation("Encontrados {Count} emails para rol {Rol}", emails.Count, rolCodigo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener emails para rol {Rol}", rolCodigo);
            }

            return emails;
        }

        // FORMATO DE NOMBRE COMPLETO DEL EMPLEADO
        private string ObtenerNombreCompletoEmpleado(Empleados empleado)
        {
            if (empleado == null) return "N/A";
            return $"{empleado.Nombre} {empleado.Apellidos}".Trim();
        }

        // GENERACIÓN DE CÓDIGO DE ANTICIPO SECUENCIAL
        private string GenerarCodigoAnticipo()
        {
            var ultimoAnticipo = _context.Anticipos
                .OrderByDescending(a => a.Id)
                .FirstOrDefault();

            var numero = 1;
            if (ultimoAnticipo != null && !string.IsNullOrEmpty(ultimoAnticipo.CodigoAnticipo))
            {
                var partes = ultimoAnticipo.CodigoAnticipo.Split('-');
                if (partes.Length > 1 && int.TryParse(partes[1], out int ultimoNumero))
                {
                    numero = ultimoNumero + 1;
                }
            }

            return $"ANT-{numero:D4}-{DateTime.Now:yyyy}";
        }

        // EXPORTACIÓN A EXCEL DE SOLICITUDES PENDIENTES DEL USUARIO ACTUAL
        public async Task<IActionResult> ExportarSolicitudesPendientesExcel()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var empleado = await _context.Empleados
                    .AsNoTracking()
                    .Include(e => e.Rol)
                    .FirstOrDefaultAsync(e => e.Id == userId);

                if (empleado == null)
                {
                    TempData["Error"] = "Usuario no encontrado.";
                    return RedirectToAction("Pendientes");
                }

                var solicitudesIds = await GetSolicitudesIdsOptimized(empleado.RolId, userId);

                var solicitudes = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .Where(s => solicitudesIds.Contains(s.Id))
                    .Include(s => s.Empleado)
                    .Include(s => s.Estado)
                    .Include(s => s.TipoViatico)
                    .Include(s => s.Empleado.JefeDirecto)
                    .Select(s => new
                    {
                        s.Id,
                        s.CodigoSolicitud,
                        Empleado = $"{s.Empleado.Nombre} {s.Empleado.Apellidos}",
                        s.NombreProyecto,
                        s.Destino,
                        FechaSalida = s.FechaSalida,
                        FechaRegreso = s.FechaRegreso,
                        TipoViatico = s.TipoViatico != null ? s.TipoViatico.Nombre : "N/A",
                        Estado = s.Estado != null ? s.Estado.Codigo : "N/A",
                        RequiereAnticipo = s.RequiereAnticipo,
                        MontoAnticipo = s.MontoAnticipo ?? 0,
                        s.Motivo,
                        JefeDirecto = s.Empleado.JefeDirecto != null ?
                            $"{s.Empleado.JefeDirecto.Nombre} {s.Empleado.JefeDirecto.Apellidos}" : "N/A",
                        FechaCreacion = s.CreatedAt
                    })
                    .OrderByDescending(s => s.FechaCreacion)
                    .ToListAsync();

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Solicitudes Pendientes");
                    var currentRow = 1;

                    // ENCABEZADOS DEL REPORTE
                    worksheet.Cell(currentRow, 1).Value = "Código";
                    worksheet.Cell(currentRow, 2).Value = "Empleado";
                    worksheet.Cell(currentRow, 3).Value = "Proyecto";
                    worksheet.Cell(currentRow, 4).Value = "Destino";
                    worksheet.Cell(currentRow, 5).Value = "Fecha Salida";
                    worksheet.Cell(currentRow, 6).Value = "Fecha Regreso";
                    worksheet.Cell(currentRow, 7).Value = "Duración (días)";
                    worksheet.Cell(currentRow, 8).Value = "Tipo Viático";
                    worksheet.Cell(currentRow, 9).Value = "Estado";
                    worksheet.Cell(currentRow, 10).Value = "Requiere Anticipo";
                    worksheet.Cell(currentRow, 11).Value = "Monto Anticipo";
                    worksheet.Cell(currentRow, 12).Value = "Motivo";
                    worksheet.Cell(currentRow, 13).Value = "Jefe Directo";
                    worksheet.Cell(currentRow, 14).Value = "Fecha de Creación";

                    var headerRange = worksheet.Range(1, 1, 1, 14);
                    headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
                    headerRange.Style.Font.FontColor = XLColor.White;
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // LLENADO DE DATOS CON FORMATO CONDICIONAL
                    foreach (var solicitud in solicitudes)
                    {
                        currentRow++;

                        var duracion = 0;
                        if (solicitud.FechaSalida != DateOnly.MinValue && solicitud.FechaRegreso != DateOnly.MinValue)
                        {
                            var salida = solicitud.FechaSalida.ToDateTime(TimeOnly.MinValue);
                            var regreso = solicitud.FechaRegreso.ToDateTime(TimeOnly.MinValue);
                            duracion = (regreso - salida).Days + 1;
                        }

                        worksheet.Cell(currentRow, 1).Value = solicitud.CodigoSolicitud;
                        worksheet.Cell(currentRow, 2).Value = solicitud.Empleado;
                        worksheet.Cell(currentRow, 3).Value = solicitud.NombreProyecto;
                        worksheet.Cell(currentRow, 4).Value = solicitud.Destino;
                        worksheet.Cell(currentRow, 5).Value = solicitud.FechaSalida.ToString();
                        worksheet.Cell(currentRow, 6).Value = solicitud.FechaRegreso.ToString();
                        worksheet.Cell(currentRow, 7).Value = duracion;
                        worksheet.Cell(currentRow, 8).Value = solicitud.TipoViatico;
                        worksheet.Cell(currentRow, 9).Value = solicitud.Estado;
                        worksheet.Cell(currentRow, 10).Value = solicitud.RequiereAnticipo.ToString();
                        worksheet.Cell(currentRow, 11).Value = solicitud.MontoAnticipo;
                        worksheet.Cell(currentRow, 12).Value = solicitud.Motivo;
                        worksheet.Cell(currentRow, 13).Value = solicitud.JefeDirecto;

                        if (solicitud.FechaCreacion != null)
                        {
                            var fechaFormateada = string.Format("{0:dd/MM/yyyy HH:mm}", solicitud.FechaCreacion);
                            worksheet.Cell(currentRow, 14).Value = fechaFormateada;
                        }
                        else
                        {
                            worksheet.Cell(currentRow, 14).Value = "N/A";
                        }

                        if (solicitud.MontoAnticipo > 0)
                        {
                            worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "$#,##0.00";
                        }
                    }

                    worksheet.Columns().AdjustToContents();

                    // TOTALES Y RESUMEN AL FINAL DEL REPORTE
                    currentRow++;
                    var totalAnticipos = solicitudes.Sum(s => s.MontoAnticipo);
                    worksheet.Cell(currentRow, 10).Value = "TOTAL ANTICIPOS:";
                    worksheet.Cell(currentRow, 11).Value = totalAnticipos;
                    worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "$#,##0.00";
                    worksheet.Cell(currentRow, 10).Style.Font.Bold = true;
                    worksheet.Cell(currentRow, 11).Style.Font.Bold = true;
                    worksheet.Cell(currentRow, 10).Style.Fill.BackgroundColor = XLColor.LightGray;
                    worksheet.Cell(currentRow, 11).Style.Fill.BackgroundColor = XLColor.LightGray;

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();

                        return File(
                            content,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"Solicitudes_Pendientes_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar solicitudes pendientes a Excel");
                TempData["Error"] = "Error al generar el archivo Excel: " + ex.Message;
                return RedirectToAction("Pendientes");
            }
        }

        // EXPORTACIÓN COMPLETA DE TODAS LAS SOLICITUDES DEL SISTEMA
        public async Task<IActionResult> ExportarTodasSolicitudesExcel()
        {
            try
            {
                var solicitudes = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .Include(s => s.Empleado)
                    .Include(s => s.Estado)
                    .Include(s => s.TipoViatico)
                    .Include(s => s.Empleado.JefeDirecto)
                    .Select(s => new
                    {
                        s.Id,
                        s.CodigoSolicitud,
                        Empleado = $"{s.Empleado.Nombre} {s.Empleado.Apellidos}",
                        s.NombreProyecto,
                        s.Destino,
                        FechaSalida = s.FechaSalida,
                        FechaRegreso = s.FechaRegreso,
                        TipoViatico = s.TipoViatico != null ? s.TipoViatico.Nombre : "N/A",
                        Estado = s.Estado != null ? s.Estado.Codigo : "N/A",
                        RequiereAnticipo = s.RequiereAnticipo ,
                        MontoAnticipo = s.MontoAnticipo ?? 0,
                        s.Motivo,
                        JefeDirecto = s.Empleado.JefeDirecto != null ?
                            $"{s.Empleado.JefeDirecto.Nombre} {s.Empleado.JefeDirecto.Apellidos}" : "N/A",
                        FechaCreacion = s.CreatedAt,
                        FechaActualizacion = s.UpdatedAt
                    })
                    .OrderByDescending(s => s.FechaCreacion)
                    .ToListAsync();

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Todas las Solicitudes");
                    var currentRow = 1;

                    string[] headers = {
                        "Código", "Empleado", "Proyecto", "Destino", "Fecha Salida", "Fecha Regreso",
                        "Duración (días)", "Tipo Viático", "Estado", "Requiere Anticipo", "Monto Anticipo",
                        "Motivo", "Jefe Directo", "Fecha de Creación", "Última Actualización"
                    };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cell(currentRow, i + 1).Value = headers[i];
                    }

                    var headerRange = worksheet.Range(1, 1, 1, headers.Length);
                    headerRange.Style.Fill.BackgroundColor = XLColor.DarkGreen;
                    headerRange.Style.Font.FontColor = XLColor.White;
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    foreach (var solicitud in solicitudes)
                    {
                        currentRow++;

                        var duracion = 0;
                        if (solicitud.FechaSalida != DateOnly.MinValue && solicitud.FechaRegreso != DateOnly.MinValue)
                        {
                            var salida = solicitud.FechaSalida.ToDateTime(TimeOnly.MinValue);
                            var regreso = solicitud.FechaRegreso.ToDateTime(TimeOnly.MinValue);
                            duracion = (regreso - salida).Days + 1;
                        }

                        worksheet.Cell(currentRow, 1).Value = solicitud.CodigoSolicitud;
                        worksheet.Cell(currentRow, 2).Value = solicitud.Empleado;
                        worksheet.Cell(currentRow, 3).Value = solicitud.NombreProyecto;
                        worksheet.Cell(currentRow, 4).Value = solicitud.Destino;
                        worksheet.Cell(currentRow, 5).Value = solicitud.FechaSalida.ToString();
                        worksheet.Cell(currentRow, 6).Value = solicitud.FechaRegreso.ToString();
                        worksheet.Cell(currentRow, 7).Value = duracion;
                        worksheet.Cell(currentRow, 8).Value = solicitud.TipoViatico;
                        worksheet.Cell(currentRow, 9).Value = solicitud.Estado;
                        worksheet.Cell(currentRow, 10).Value = solicitud.RequiereAnticipo.ToString();
                        worksheet.Cell(currentRow, 11).Value = solicitud.MontoAnticipo;
                        worksheet.Cell(currentRow, 12).Value = solicitud.Motivo;
                        worksheet.Cell(currentRow, 13).Value = solicitud.JefeDirecto;

                        if (solicitud.FechaCreacion != null)
                        {
                            var fechaCreacionFormateada = string.Format("{0:dd/MM/yyyy HH:mm}", solicitud.FechaCreacion);
                            worksheet.Cell(currentRow, 14).Value = fechaCreacionFormateada;
                        }
                        else
                        {
                            worksheet.Cell(currentRow, 14).Value = "N/A";
                        }

                        if (solicitud.FechaActualizacion.HasValue && solicitud.FechaActualizacion.Value != DateTime.MinValue)
                        {
                            var fechaActualizacionFormateada = string.Format("{0:dd/MM/yyyy HH:mm}", solicitud.FechaActualizacion.Value);
                            worksheet.Cell(currentRow, 15).Value = fechaActualizacionFormateada;
                        }
                        else
                        {
                            worksheet.Cell(currentRow, 15).Value = "N/A";
                        }

                        if (solicitud.MontoAnticipo > 0)
                        {
                            worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "$#,##0.00";
                        }

                        // COLORES CONDICIONALES POR ESTADO DE SOLICITUD
                        var rowRange = worksheet.Range(currentRow, 1, currentRow, headers.Length);
                        switch (solicitud.Estado)
                        {
                            case "RECHAZADA":
                                rowRange.Style.Fill.BackgroundColor = XLColor.Coral;
                                break;
                            case "APROBADA_DIRECCION":
                                rowRange.Style.Fill.BackgroundColor = XLColor.LightGreen;
                                break;
                            case "BORRADOR":
                                rowRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                                break;
                            case "ENVIADA_JP":
                            case "ENVIADA_RH":
                            case "ENVIADA_FINANZAS":
                            case "ENVIADA_DIRECCION":
                                rowRange.Style.Fill.BackgroundColor = XLColor.LightYellow;
                                break;
                        }
                    }

                    worksheet.Columns().AdjustToContents();

                    // ESTADÍSTICAS RESUMEN AL FINAL DEL REPORTE
                    currentRow++;
                    var totalAnticipos = solicitudes.Sum(s => s.MontoAnticipo);
                    var totalSolicitudes = solicitudes.Count;
                    var solicitudesAprobadas = solicitudes.Count(s => s.Estado.Contains("APROBADA"));
                    var solicitudesRechazadas = solicitudes.Count(s => s.Estado == "RECHAZADA");
                    var solicitudesPendientes = totalSolicitudes - solicitudesAprobadas - solicitudesRechazadas;

                    worksheet.Cell(currentRow, 8).Value = "ESTADÍSTICAS:";
                    worksheet.Cell(currentRow, 9).Value = $"Total: {totalSolicitudes}";
                    worksheet.Cell(currentRow, 10).Value = $"Aprobadas: {solicitudesAprobadas}";
                    worksheet.Cell(currentRow, 11).Value = $"Rechazadas: {solicitudesRechazadas}";
                    worksheet.Cell(currentRow, 12).Value = $"Pendientes: {solicitudesPendientes}";
                    worksheet.Cell(currentRow, 13).Value = "TOTAL ANTICIPOS:";
                    worksheet.Cell(currentRow, 14).Value = totalAnticipos;
                    worksheet.Cell(currentRow, 14).Style.NumberFormat.Format = "$#,##0.00";

                    var statsRange = worksheet.Range(currentRow, 8, currentRow, 14);
                    statsRange.Style.Font.Bold = true;
                    statsRange.Style.Fill.BackgroundColor = XLColor.LightBlue;

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();

                        return File(
                            content,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"Todas_Solicitudes_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar todas las solicitudes a Excel");
                TempData["Error"] = "Error al generar el archivo Excel: " + ex.Message;
                return RedirectToAction("Pendientes");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DescargarPdf(int id)
        {
            try
            {
                var solicitud = await _context.SolicitudesViajes
                    .Include(s => s.Empleado)
                    .Include(s => s.Estado)
                    .Include(s => s.TipoViatico)
                    .Include(s => s.Anticipos)
                    .Include(s => s.FlujoAprobaciones)
                        .ThenInclude(f => f.EmpleadoAprobador)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (solicitud == null)
                {
                    TempData["Error"] = "Solicitud no encontrada.";
                    return RedirectToAction(nameof(Pendientes));
                }

                var duracion = 0;
                if (solicitud.FechaSalida != DateOnly.MinValue && solicitud.FechaRegreso != DateOnly.MinValue)
                {
                    var salida = solicitud.FechaSalida.ToDateTime(TimeOnly.MinValue);
                    var regreso = solicitud.FechaRegreso.ToDateTime(TimeOnly.MinValue);
                    duracion = (regreso - salida).Days + 1;
                }

                // Ruta del logo (ajusta según tu estructura)
                string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logoviamtek.jpeg");

                // Opcional: si no existe, usar un logo por defecto
                if (!System.IO.File.Exists(logoPath))
                {
                    _logger.LogWarning("Logo no encontrado en {LogoPath}. Usando fallback.", logoPath);
                    logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "LOGOHORIZONTAL.png");
                }

                var tiposViatico = await _context.TiposViatico.ToListAsync();

                var pdfGenerator = new PdfSolicitudes(solicitud, duracion, logoPath, tiposViatico);
                var pdfBytes = pdfGenerator.GeneratePdf();

                Response.Headers.Add("Content-Disposition", $"inline; filename=Solicitud_{solicitud.CodigoSolicitud}.pdf");
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar PDF para solicitud {Id}", id);
                TempData["Error"] = "Error al generar el PDF. Intente más tarde.";
                return RedirectToAction(nameof(Detalles), new { id });
            }
        }
    }
}