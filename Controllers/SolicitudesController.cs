using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SVV.Models;
using SVV.ViewModels;
using System.Security.Claims;
using SVV.Services;
using Microsoft.Extensions.Configuration;
using ServicesNotificationItem = SVV.Services.NotificationItem;
using SVV.Filters;

namespace SVV.Controllers
{
    [TypeFilter(typeof(CambioPassword))]
    public class SolicitudesController : Controller
    {
        private readonly SvvContext _context;
        private readonly INotificationQueue _queue;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SolicitudesController> _logger;

        public SolicitudesController(
            SvvContext context,
            INotificationQueue queue,
            IConfiguration configuration,
            ILogger<SolicitudesController> logger)
        {
            _context = context;
            _queue = queue;
            _configuration = configuration;
            _logger = logger;
        }

        // ==================== ACCIONES PRINCIPALES ====================

        public async Task<IActionResult> Index()
        {
            try
            {
                var empleadoId = ObtenerEmpleadoId();
                var solicitudes = await _context.SolicitudesViajes
                    .Where(s => s.EmpleadoId == empleadoId)
                    .OrderByDescending(s => s.CreatedAt)
                    .Include(s => s.TipoViatico)
                    .Include(s => s.Estado)
                    .Select(s => new SolicitudesViaje
                    {
                        Id = s.Id,
                        CodigoSolicitud = s.CodigoSolicitud,
                        Destino = s.Destino,
                        FechaSalida = s.FechaSalida,
                        FechaRegreso = s.FechaRegreso,
                        CreatedAt = s.CreatedAt,
                        UpdatedAt = s.UpdatedAt,
                        TipoViatico = s.TipoViatico,
                        Estado = s.Estado,
                        NombreProyecto = s.NombreProyecto,
                        RequiereAnticipo = s.RequiereAnticipo,
                        MontoAnticipo = s.MontoAnticipo
                    })
                    .AsNoTracking()
                    .ToListAsync();
                return View(solicitudes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar solicitudes");
                TempData["Error"] = "Error al cargar las solicitudes.";
                return View(new List<SolicitudesViaje>());
            }
        }

        public async Task<IActionResult> Detalles(int? id)
        {
            if (id == null) return NotFound();
            var solicitud = await _context.SolicitudesViajes
                .Include(s => s.TipoViatico)
                .Include(s => s.Estado)
                .Include(s => s.Anticipos)
                .Include(s => s.Empleado)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (solicitud == null) return NotFound();
            if (!PuedeVerSolicitud(solicitud.EmpleadoId)) return Forbid();
            return View(solicitud);
        }

        public IActionResult CrearSolicitud()
        {
            try
            {
                var viewModel = new CrearSolicitudViewModel
                {
                    TiposViatico = ObtenerTiposViatico(),
                    FechaSalida = DateTime.Today.AddDays(3),
                    FechaRegreso = DateTime.Today.AddDays(4),
                    NumeroPersonas = 1
                };
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Crear GET");
                var viewModel = new CrearSolicitudViewModel
                {
                    TiposViatico = new List<TiposViatico>(),
                    FechaSalida = DateTime.Today.AddDays(3),
                    FechaRegreso = DateTime.Today.AddDays(4),
                    NumeroPersonas = 1
                };
                return View(viewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearSolicitud(CrearSolicitudViewModel viewModel, string action)
        {
            if (!viewModel.CumplePlazoMinimo())
                ModelState.AddModelError("FechaSalida", "La solicitud debe hacerse con al menos 3 días hábiles de anticipación");

            if (!ModelState.IsValid)
            {
                viewModel.TiposViatico = ObtenerTiposViatico();
                return View(viewModel);
            }

            if (viewModel.FechaRegreso < viewModel.FechaSalida)
            {
                ModelState.AddModelError("FechaRegreso", "La fecha de regreso debe ser posterior a la fecha de salida");
                viewModel.TiposViatico = ObtenerTiposViatico();
                return View(viewModel);
            }

            try
            {
                var empleadoId = ObtenerEmpleadoId();
                var usuarioActual = await _context.Empleados
                    .Include(e => e.JefeDirecto)
                    .Include(e => e.Rol)
                    .FirstOrDefaultAsync(e => e.Id == empleadoId);

                if (usuarioActual == null)
                {
                    TempData["Error"] = "Usuario no encontrado.";
                    viewModel.TiposViatico = ObtenerTiposViatico();
                    return View(viewModel);
                }

                var codigoSolicitud = GenerarCodigoSolicitud();
                string colaboradores = null;
                if (viewModel.NombresPersonas != null && viewModel.NombresPersonas.Any(n => !string.IsNullOrWhiteSpace(n)))
                {
                    var nombresValidos = viewModel.NombresPersonas
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n.Trim());
                    colaboradores = string.Join(", ", nombresValidos);
                }

                var solicitud = new SolicitudesViaje
                {
                    EmpleadoId = empleadoId,
                    Destino = viewModel.Destino,
                    DireccionEmpresa = viewModel.DireccionEmpresa,
                    Motivo = viewModel.Motivo,
                    FechaSalida = DateOnly.FromDateTime(viewModel.FechaSalida),
                    FechaRegreso = DateOnly.FromDateTime(viewModel.FechaRegreso),
                    TipoViaticoId = viewModel.TipoViaticoId,
                    MedioTrasladoPrincipal = viewModel.MedioTrasladoPrincipal,
                    RequiereTaxiDomicilio = viewModel.RequiereTaxiDomicilio,
                    DireccionTaxiOrigen = viewModel.DireccionTaxiOrigen,
                    DireccionTaxiDestino = viewModel.DireccionTaxiDestino,
                    RequiereHospedaje = viewModel.RequiereHospedaje,
                    NochesHospedaje = viewModel.NochesHospedaje,
                    EmpresaVisitada = viewModel.EmpresaVisitada,
                    LugarComisionDetallado = viewModel.LugarComisionDetallado,
                    HoraSalida = viewModel.HoraSalida.HasValue ? TimeOnly.FromTimeSpan(viewModel.HoraSalida.Value) : null,
                    HoraRegreso = viewModel.HoraRegreso.HasValue ? TimeOnly.FromTimeSpan(viewModel.HoraRegreso.Value) : null,
                    NumeroPersonas = viewModel.NumeroPersonas,
                    EstadoId = 1,
                    CodigoSolicitud = codigoSolicitud,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    ValidacionPlazos = viewModel.CumplePlazoMinimo(),
                    CumplePlazoMinimo = viewModel.CumplePlazoMinimo(),
                    Colaboradores = colaboradores,
                    RequiereAnticipo = viewModel.requiere_anticipo,
                    NombreProyecto = viewModel.NombreProyecto
                };

                _context.SolicitudesViajes.Add(solicitud);
                await _context.SaveChangesAsync();

                if (action == "enviar")
                {
                    await ProcesarEnvioInmediato(solicitud, usuarioActual);
                    TempData["Success"] = "Solicitud enviada a aprobación exitosamente.";
                    _logger.LogInformation("Solicitud {Codigo} enviada a aprobación por empleado {EmpleadoId} con estado {EstadoId}",
                        solicitud.CodigoSolicitud, usuarioActual.Id, solicitud.EstadoId);
                }
                else
                {
                    await EnviarNotificacionBorrador(solicitud, usuarioActual);
                    TempData["Success"] = $"Solicitud {codigoSolicitud} guardada como BORRADOR.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear la solicitud");
                TempData["Error"] = "Error al crear la solicitud: " + ex.Message;
                viewModel.TiposViatico = ObtenerTiposViatico();
                return View(viewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarAprobacion(int id)
        {
            try
            {
                var solicitud = await _context.SolicitudesViajes
                    .Include(s => s.Empleado)
                    .ThenInclude(e => e.JefeDirecto)
                    .Include(s => s.Estado)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (solicitud == null)
                {
                    TempData["Error"] = "Solicitud no encontrada.";
                    return RedirectToAction(nameof(Index));
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                if (solicitud.EmpleadoId != userId)
                {
                    TempData["Error"] = "No tienes permisos para enviar esta solicitud.";
                    return RedirectToAction(nameof(Index));
                }

                if (solicitud.EstadoId != 1)
                {
                    TempData["Error"] = "Solo las solicitudes en borrador pueden ser enviadas a aprobación.";
                    return RedirectToAction(nameof(Index));
                }

                // Capturar URL base antes del Task.Run
                var baseUrl = $"{Request.Scheme}://{Request.Host}";

                if (solicitud.Empleado.JefeDirectoId == null || solicitud.Empleado.JefeDirecto == null)
                {
                    solicitud.EstadoId = 6;
                    solicitud.UpdatedAt = DateTime.Now;

                    var flujoDirectoFinanzas = new FlujoAprobaciones
                    {
                        SolicitudViajeId = solicitud.Id,
                        Etapa = "FINANZAS",
                        EmpleadoAprobadorId = userId,
                        EstadoAprobacion = "Pendiente",
                        Comentarios = $"Solicitud enviada directamente a Finanzas porque el empleado {solicitud.Empleado.Nombre} {solicitud.Empleado.Apellidos} no tiene jefe directo asignado.",
                        OrdenEtapa = 2,
                        FechaAprobacion = DateTime.Now,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    _context.FlujoAprobaciones.Add(flujoDirectoFinanzas);
                }
                else
                {
                    solicitud.EstadoId = 2;
                    solicitud.UpdatedAt = DateTime.Now;

                    var flujoJP = new FlujoAprobaciones
                    {
                        SolicitudViajeId = solicitud.Id,
                        Etapa = "JEFE_INMEDIATO",
                        EmpleadoAprobadorId = solicitud.Empleado.JefeDirectoId.Value,
                        EstadoAprobacion = "Pendiente",
                        Comentarios = "Solicitud enviada al jefe directo para aprobación.",
                        OrdenEtapa = 2,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    _context.FlujoAprobaciones.Add(flujoJP);
                }

                await _context.SaveChangesAsync();

                // Pasar baseUrl a la tarea en segundo plano
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(500);
                        await EnviarCorreoSinBloquear(solicitud, baseUrl, esParaFinanzas: solicitud.EstadoId == 6);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error en envío de correo en segundo plano para solicitud {Id}", solicitud.Id);
                    }
                });

                TempData["Success"] = solicitud.EstadoId == 6
                    ? "Solicitud enviada directamente a Finanzas (no tienes jefe directo asignado)."
                    : "Solicitud enviada a aprobación exitosamente. Ahora está en revisión por el Autorizador/Jefe Directo.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar solicitud a aprobación");
                TempData["Error"] = "Error al enviar la solicitud a aprobación.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Editar(int? id)
        {
            if (id == null) return NotFound();
            var solicitud = await _context.SolicitudesViajes
                .Include(s => s.TipoViatico)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (solicitud == null) return NotFound();
            var empleadoId = ObtenerEmpleadoId();
            if (solicitud.EmpleadoId != empleadoId || solicitud.EstadoId != 1)
            {
                TempData["Error"] = "Solo puedes editar solicitudes en estado BORRADOR";
                return RedirectToAction("Index");
            }

            var viewModel = new CrearSolicitudViewModel
            {
                Id = solicitud.Id,
                Destino = solicitud.Destino,
                DireccionEmpresa = solicitud.DireccionEmpresa,
                Motivo = solicitud.Motivo,
                FechaSalida = solicitud.FechaSalida.ToDateTime(TimeOnly.MinValue),
                FechaRegreso = solicitud.FechaRegreso.ToDateTime(TimeOnly.MinValue),
                TipoViaticoId = solicitud.TipoViaticoId,
                MedioTrasladoPrincipal = solicitud.MedioTrasladoPrincipal,
                RequiereTaxiDomicilio = solicitud.RequiereTaxiDomicilio ?? false,
                DireccionTaxiOrigen = solicitud.DireccionTaxiOrigen,
                DireccionTaxiDestino = solicitud.DireccionTaxiDestino,
                RequiereHospedaje = solicitud.RequiereHospedaje ?? false,
                NochesHospedaje = solicitud.NochesHospedaje,
                EmpresaVisitada = solicitud.EmpresaVisitada,
                LugarComisionDetallado = solicitud.LugarComisionDetallado,
                HoraSalida = solicitud.HoraSalida?.ToTimeSpan(),
                HoraRegreso = solicitud.HoraRegreso?.ToTimeSpan(),
                NumeroPersonas = solicitud.NumeroPersonas.GetValueOrDefault(1),
                requiere_anticipo = solicitud.RequiereAnticipo,
                NombreProyecto = solicitud.NombreProyecto,
                TiposViatico = ObtenerTiposViatico()
            };

            if (!string.IsNullOrEmpty(solicitud.Colaboradores))
                viewModel.NombresPersonas = solicitud.Colaboradores.Split(',').Select(n => n.Trim()).ToList();
            else
                viewModel.NombresPersonas = new List<string>();

            return View("EditarSolicitud", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, CrearSolicitudViewModel viewModel)
        {
            if (id != viewModel.Id) return NotFound();
            if (!ModelState.IsValid)
            {
                viewModel.TiposViatico = ObtenerTiposViatico();
                return View("EditarSolicitud", viewModel);
            }
            if (viewModel.FechaRegreso < viewModel.FechaSalida)
            {
                ModelState.AddModelError("FechaRegreso", "La fecha de regreso debe ser posterior a la fecha de salida");
                viewModel.TiposViatico = ObtenerTiposViatico();
                return View("EditarSolicitud", viewModel);
            }

            try
            {
                var solicitudExistente = await _context.SolicitudesViajes
                    .FirstOrDefaultAsync(s => s.Id == id);
                if (solicitudExistente == null) return NotFound();

                var empleadoId = ObtenerEmpleadoId();
                if (solicitudExistente.EmpleadoId != empleadoId || solicitudExistente.EstadoId != 1)
                {
                    TempData["Error"] = "Solo puedes editar solicitudes en estado BORRADOR";
                    return RedirectToAction("Index");
                }

                string colaboradores = null;
                if (viewModel.NombresPersonas != null && viewModel.NombresPersonas.Any(n => !string.IsNullOrWhiteSpace(n)))
                {
                    var nombresValidos = viewModel.NombresPersonas
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n.Trim());
                    colaboradores = string.Join(", ", nombresValidos);
                }

                solicitudExistente.Destino = viewModel.Destino;
                solicitudExistente.DireccionEmpresa = viewModel.DireccionEmpresa;
                solicitudExistente.Motivo = viewModel.Motivo;
                solicitudExistente.FechaSalida = DateOnly.FromDateTime(viewModel.FechaSalida);
                solicitudExistente.FechaRegreso = DateOnly.FromDateTime(viewModel.FechaRegreso);
                solicitudExistente.TipoViaticoId = viewModel.TipoViaticoId;
                solicitudExistente.MedioTrasladoPrincipal = viewModel.MedioTrasladoPrincipal;
                solicitudExistente.RequiereTaxiDomicilio = viewModel.RequiereTaxiDomicilio;
                solicitudExistente.DireccionTaxiOrigen = viewModel.DireccionTaxiOrigen;
                solicitudExistente.DireccionTaxiDestino = viewModel.DireccionTaxiDestino;
                solicitudExistente.RequiereHospedaje = viewModel.RequiereHospedaje;
                solicitudExistente.NochesHospedaje = viewModel.NochesHospedaje;
                solicitudExistente.EmpresaVisitada = viewModel.EmpresaVisitada;
                solicitudExistente.LugarComisionDetallado = viewModel.LugarComisionDetallado;
                solicitudExistente.HoraSalida = viewModel.HoraSalida.HasValue ? TimeOnly.FromTimeSpan(viewModel.HoraSalida.Value) : null;
                solicitudExistente.HoraRegreso = viewModel.HoraRegreso.HasValue ? TimeOnly.FromTimeSpan(viewModel.HoraRegreso.Value) : null;
                solicitudExistente.NumeroPersonas = viewModel.NumeroPersonas;
                solicitudExistente.UpdatedAt = DateTime.Now;
                solicitudExistente.ValidacionPlazos = true;
                solicitudExistente.CumplePlazoMinimo = true;
                solicitudExistente.Colaboradores = colaboradores;
                solicitudExistente.RequiereAnticipo = viewModel.requiere_anticipo;
                solicitudExistente.NombreProyecto = viewModel.NombreProyecto;

                _context.Entry(solicitudExistente).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Solicitud {solicitudExistente.CodigoSolicitud} actualizada exitosamente.";
                return RedirectToAction("Index");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Error de concurrencia al actualizar la solicitud {Id}", id);
                if (!SolicitudExists(id)) return NotFound();
                TempData["Error"] = "Error: La solicitud fue modificada por otro usuario. Por favor, recarga la página e intenta nuevamente.";
                viewModel.TiposViatico = ObtenerTiposViatico();
                return View("EditarSolicitud", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar la solicitud {Id}", id);
                TempData["Error"] = "Error al actualizar la solicitud: " + ex.Message;
                viewModel.TiposViatico = ObtenerTiposViatico();
                return View("EditarSolicitud", viewModel);
            }
        }

        public async Task<IActionResult> Eliminar(int? id)
        {
            if (id == null) return NotFound();
            var solicitud = await _context.SolicitudesViajes
                .Include(s => s.TipoViatico)
                .Include(s => s.Estado)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (solicitud == null) return NotFound();
            var empleadoId = ObtenerEmpleadoId();
            if (solicitud.EmpleadoId != empleadoId || solicitud.EstadoId != 1)
            {
                TempData["Error"] = "Solo puedes eliminar solicitudes en estado BORRADOR";
                return RedirectToAction("Index");
            }
            return View(solicitud);
        }

        [HttpPost, ActionName("Eliminar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarConfirmado(int id)
        {
            try
            {
                var solicitud = await _context.SolicitudesViajes.FindAsync(id);
                if (solicitud == null) return NotFound();
                var empleadoId = ObtenerEmpleadoId();
                if (solicitud.EmpleadoId != empleadoId || solicitud.EstadoId != 1)
                {
                    TempData["Error"] = "Solo puedes eliminar solicitudes en estado BORRADOR";
                    return RedirectToAction("Index");
                }
                var codigoSolicitud = solicitud.CodigoSolicitud;
                _context.SolicitudesViajes.Remove(solicitud);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Solicitud {codigoSolicitud} eliminada exitosamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar la solicitud {Id}", id);
                TempData["Error"] = "Error al eliminar la solicitud: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ==================== MÉTODOS AUXILIARES ====================
        private async Task EnviarCorreoSinBloquear(SolicitudesViaje solicitud, string baseUrl, bool esParaFinanzas = false)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var optionsBuilder = new DbContextOptionsBuilder<SvvContext>();
            optionsBuilder.UseSqlServer(connectionString);

            using (var newContext = new SvvContext(optionsBuilder.Options))
            {
                try
                {
                    var solicitudActualizada = await newContext.SolicitudesViajes
                        .Include(s => s.Empleado)
                        .ThenInclude(e => e.JefeDirecto)
                        .FirstOrDefaultAsync(s => s.Id == solicitud.Id);

                    if (solicitudActualizada == null)
                    {
                        _logger.LogWarning("No se encontró la solicitud {SolicitudId} en el contexto nuevo", solicitud.Id);
                        return;
                    }

                    var empleadoSolicitante = solicitudActualizada.Empleado;
                    var urlSolic = $"{baseUrl}/Aprobaciones/Detalles/{solicitud.Id}";

                    if (esParaFinanzas)
                    {
                        // Caso: sin jefe directo → enviar a todos los de FINANZAS
                        var emailsFinanzas = await ObtenerEmailsPorRolConContexto(newContext, "FINANZAS");
                        if (!emailsFinanzas.Any())
                        {
                            _logger.LogWarning("No se encontraron emails para Finanzas para la solicitud {SolicitudId}", solicitud.Id);
                            return;
                        }

                        foreach (var email in emailsFinanzas)
                        {
                            _queue.Enqueue(new ServicesNotificationItem
                            {
                                ToEmail = email,
                                Subject = $"Solicitud de Viáticos (Sin Jefe) - {solicitud.CodigoSolicitud}",
                                TemplateName = "/Views/Emails/SolicitudCreada.cshtml",
                                Model = new
                                {
                                    Solicitud = solicitud,
                                    Url = urlSolic,
                                    EsBorrador = false,
                                    EsEnvioAprobacion = true,
                                    EsParaRH = false,
                                    EsParaJP = false,
                                    EsParaFinanzas = true,
                                    EsSinJefe = true,
                                    EsNotificacionMultiple = true,
                                    EmpleadoSolicitante = ObtenerNombreCompletoEmpleado(empleadoSolicitante),
                                    JefeDestinatario = "",
                                    Mensaje = "Se ha enviado una solicitud de viáticos que requiere su aprobación (el empleado no tiene jefe directo asignado)."
                                }
                            });
                        }
                        _logger.LogInformation("Notificación enviada a Finanzas para solicitud sin jefe: {Codigo}", solicitud.CodigoSolicitud);
                    }
                    else
                    {
                        // Caso normal: hay jefe directo
                        if (empleadoSolicitante?.JefeDirecto == null || string.IsNullOrEmpty(empleadoSolicitante.JefeDirecto.Email))
                        {
                            _logger.LogError("Error de lógica: se esperaba jefe directo pero no se encontró para solicitud {Id}", solicitud.Id);
                            return;
                        }

                        var jefeDirecto = empleadoSolicitante.JefeDirecto;
                        _queue.Enqueue(new ServicesNotificationItem
                        {
                            ToEmail = jefeDirecto.Email,
                            Subject = $"Solicitud de Viáticos para aprobación - {solicitud.CodigoSolicitud}",
                            TemplateName = "/Views/Emails/SolicitudCreada.cshtml",
                            Model = new
                            {
                                Solicitud = solicitud,
                                Url = urlSolic,
                                EsBorrador = false,
                                EsEnvioAprobacion = true,
                                EsParaRH = false,
                                EsParaJP = true,
                                EsParaFinanzas = false,
                                EsSinJefe = false,
                                EsNotificacionMultiple = false,
                                EmpleadoSolicitante = ObtenerNombreCompletoEmpleado(empleadoSolicitante),
                                JefeDestinatario = ObtenerNombreCompletoEmpleado(jefeDirecto),
                                Mensaje = "Se ha enviado una solicitud de viáticos que requiere su aprobación."
                            }
                        });
                        _logger.LogInformation("Correo encolado para JP {JefeEmail}", jefeDirecto.Email);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al preparar correo para solicitud {SolicitudId}", solicitud.Id);
                }
            }
        }
        private async Task<List<string>> ObtenerEmailsPorRolConContexto(SvvContext context, string rolCodigo)
        {
            var emails = new List<string>();
            try
            {
                var empleados = await context.Empleados
                    .Include(e => e.Rol)
                    .Where(e => e.Activo == true && !string.IsNullOrEmpty(e.Email) && e.Rol.Codigo == rolCodigo)
                    .ToListAsync();
                emails = empleados.Select(e => e.Email).Distinct().ToList();

                if (!emails.Any())
                {
                    _logger.LogWarning("No se encontraron empleados activos con rol {RolCodigo}", rolCodigo);
                    // Opcional: puedes agregar un fallback a configuración, pero se recomienda tener los datos en BD
                }
                _logger.LogInformation("Encontrados {Count} emails para rol {RolCodigo}", emails.Count, rolCodigo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener emails para rol {RolCodigo}", rolCodigo);
            }
            return emails;
        }

        private async Task ProcesarEnvioInmediato(SolicitudesViaje solicitud, Empleados usuarioActual)
        {
            try
            {
                await EnviarNotificacionRH(solicitud, usuarioActual, "ENVIADA");

                if (usuarioActual.JefeDirectoId != null && usuarioActual.JefeDirecto != null)
                {
                    solicitud.EstadoId = 2;
                    solicitud.UpdatedAt = DateTime.Now;

                    var flujoJP = new FlujoAprobaciones
                    {
                        SolicitudViajeId = solicitud.Id,
                        Etapa = "JEFE_INMEDIATO",
                        EmpleadoAprobadorId = usuarioActual.JefeDirectoId.Value,
                        EstadoAprobacion = "Pendiente",
                        Comentarios = "Solicitud enviada al jefe directo para aprobación.",
                        OrdenEtapa = 2,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    _context.FlujoAprobaciones.Add(flujoJP);
                }
                else
                {
                    solicitud.EstadoId = 6;
                    solicitud.UpdatedAt = DateTime.Now;

                    var flujoDirectoFinanzas = new FlujoAprobaciones
                    {
                        SolicitudViajeId = solicitud.Id,
                        Etapa = "FINANZAS",
                        EmpleadoAprobadorId = usuarioActual.Id,
                        EstadoAprobacion = "Pendiente",
                        Comentarios = $"Solicitud enviada directamente a Finanzas porque el empleado {usuarioActual.Nombre} {usuarioActual.Apellidos} no tiene jefe directo asignado.",
                        OrdenEtapa = 2,
                        FechaAprobacion = DateTime.Now,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    _context.FlujoAprobaciones.Add(flujoDirectoFinanzas);
                }

                await _context.SaveChangesAsync();

                if (usuarioActual.JefeDirectoId != null && usuarioActual.JefeDirecto != null)
                    await EnviarNotificacionJefe(solicitud, usuarioActual.JefeDirecto);
                else
                    await EnviarNotificacionFinanzas(solicitud, usuarioActual);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar envío inmediato para solicitud {Id}", solicitud.Id);
                throw;
            }
        }

        private async Task EnviarNotificacionBorrador(SolicitudesViaje solicitud, Empleados empleadoSolicitante)
        {
            try
            {
                await EnviarNotificacionRH(solicitud, empleadoSolicitante, "BORRADOR");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de borrador para solicitud {Id}", solicitud.Id);
            }
        }

        private async Task EnviarNotificacionJefe(SolicitudesViaje solicitud, Empleados jefe)
        {
            try
            {
                if (jefe == null || string.IsNullOrEmpty(jefe.Email))
                {
                    _logger.LogWarning("No se puede notificar al jefe, email no encontrado para solicitud {Id}", solicitud.Id);
                    return;
                }

                var urlSolic = $"{Request.Scheme}://{Request.Host}/Aprobaciones/Detalles/{solicitud.Id}";
                string subject = $"Solicitud de Viáticos para aprobación - {solicitud.CodigoSolicitud}";

                var empleadoSolicitante = await _context.Empleados.FindAsync(solicitud.EmpleadoId);

                _queue.Enqueue(new ServicesNotificationItem
                {
                    ToEmail = jefe.Email,
                    Subject = subject,
                    TemplateName = "/Views/Emails/SolicitudCreada.cshtml",
                    Model = new
                    {
                        Solicitud = solicitud,
                        Url = urlSolic,
                        EsBorrador = false,
                        EsEnvioAprobacion = true,
                        EsParaRH = false,
                        EsParaJP = true,
                        EsParaFinanzas = false,
                        EsSinJefe = false,
                        EsNotificacionMultiple = false,
                        EmpleadoSolicitante = empleadoSolicitante != null ? ObtenerNombreCompletoEmpleado(empleadoSolicitante) : "N/A",
                        JefeDestinatario = ObtenerNombreCompletoEmpleado(jefe),
                        Mensaje = "Se ha enviado una solicitud de viáticos que requiere su aprobación como jefe directo."
                    }
                });

                _logger.LogInformation("Notificación enviada al Jefe Directo {Email} para solicitud {Codigo}",
                    jefe.Email, solicitud.CodigoSolicitud);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación al jefe para solicitud {Id}", solicitud.Id);
            }
        }

        private async Task EnviarNotificacionFinanzas(SolicitudesViaje solicitud, Empleados empleadoSolicitante)
        {
            try
            {
                var emailsFinanzas = await _context.Empleados
                    .Include(e => e.Rol)
                    .Where(e => e.Activo == true && !string.IsNullOrEmpty(e.Email) && e.Rol.Codigo == "FINANZAS")
                    .Select(e => e.Email)
                    .Distinct()
                    .ToListAsync();

                if (!emailsFinanzas.Any())
                {
                    _logger.LogWarning("No se encontraron emails para Finanzas");
                    return;
                }

                var urlSolic = $"{Request.Scheme}://{Request.Host}/Aprobaciones/Detalles/{solicitud.Id}";
                string subject = $"Solicitud de Viáticos (Sin Jefe) - {solicitud.CodigoSolicitud}";

                foreach (var email in emailsFinanzas)
                {
                    _queue.Enqueue(new ServicesNotificationItem
                    {
                        ToEmail = email,
                        Subject = subject,
                        TemplateName = "/Views/Emails/SolicitudCreada.cshtml",
                        Model = new
                        {
                            Solicitud = solicitud,
                            Url = urlSolic,
                            EsBorrador = false,
                            EsEnvioAprobacion = true,
                            EsParaRH = false,
                            EsParaJP = false,
                            EsParaFinanzas = true,
                            EsSinJefe = true,
                            EsNotificacionMultiple = true,
                            EmpleadoSolicitante = ObtenerNombreCompletoEmpleado(empleadoSolicitante),
                            JefeDestinatario = "",
                            Mensaje = "Se ha enviado una solicitud de viáticos que requiere su aprobación (el empleado no tiene jefe directo asignado)."
                        }
                    });
                }

                _logger.LogInformation("Notificación enviada a Finanzas ({Count} emails) para solicitud {Codigo}",
                    emailsFinanzas.Count, solicitud.CodigoSolicitud);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación a Finanzas para solicitud {Id}", solicitud.Id);
            }
        }
        private async Task EnviarNotificacionRH(SolicitudesViaje solicitud, Empleados empleadoSolicitante, string tipoNotificacion)
        {
            try
            {
                var emailsRH = await _context.Empleados
                    .Include(e => e.Rol)
                    .Where(e => e.Activo == true && !string.IsNullOrEmpty(e.Email) && e.Rol.Nombre == "RH")
                    .Select(e => e.Email)
                    .Distinct()
                    .ToListAsync();

                if (!emailsRH.Any())
                {
                    _logger.LogWarning("No se encontraron emails para Recursos Humanos");
                    return;
                }

                var urlSolic = $"{Request.Scheme}://{Request.Host}/Solicitudes/Detalles/{solicitud.Id}";
                string subject = tipoNotificacion == "BORRADOR"
                    ? $"Nueva Solicitud de Viáticos (Borrador) - {solicitud.CodigoSolicitud}"
                    : $"Nueva Solicitud de Viáticos - {solicitud.CodigoSolicitud}";

                foreach (var emailRH in emailsRH)
                {
                    _queue.Enqueue(new ServicesNotificationItem
                    {
                        ToEmail = emailRH,
                        Subject = subject,
                        TemplateName = "/Views/Emails/SolicitudCreada.cshtml",
                        Model = new
                        {
                            Solicitud = solicitud,
                            Url = urlSolic,
                            EsBorrador = (tipoNotificacion == "BORRADOR"),
                            EsEnvioAprobacion = (tipoNotificacion != "BORRADOR"),
                            EsParaRH = true,
                            EsParaJP = false,
                            EsParaFinanzas = false,
                            EsSinJefe = false,
                            EsNotificacionMultiple = false,
                            EmpleadoSolicitante = ObtenerNombreCompletoEmpleado(empleadoSolicitante),
                            JefeDestinatario = "",
                            Mensaje = tipoNotificacion == "BORRADOR"
                                ? "Se ha creado un nuevo borrador de solicitud de viáticos para su conocimiento."
                                : "Se ha creado una nueva solicitud de viáticos para su conocimiento."
                        }
                    });
                }

                _logger.LogInformation("Notificación {Tipo} enviada a RH ({Count} emails) para solicitud {Codigo}",
                    tipoNotificacion, emailsRH.Count, solicitud.CodigoSolicitud);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación a RH para solicitud {Id}", solicitud.Id);
            }
        }

        private string ObtenerNombreCompletoEmpleado(Empleados empleado) =>
            empleado == null ? "N/A" : $"{empleado.Nombre} {empleado.Apellidos}".Trim();

        private int ObtenerEmpleadoId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return 1;
            var empleado = _context.Empleados.FirstOrDefault(e => e.Id == int.Parse(userId));
            return empleado?.Id ?? 1;
        }

        private string GenerarCodigoSolicitud()
        {
            var fecha = DateTime.Now.ToString("yyyyMMdd");
            var solicitudesDelDia = _context.SolicitudesViajes.Where(s => s.CreatedAt.Value.Date == DateTime.Today).ToList();
            var maxNumero = 0;
            foreach (var solicitud in solicitudesDelDia)
            {
                if (solicitud.CodigoSolicitud.StartsWith($"SOL-{fecha}-"))
                {
                    var numeroStr = solicitud.CodigoSolicitud.Replace($"SOL-{fecha}-", "");
                    if (int.TryParse(numeroStr, out int numero) && numero > maxNumero)
                        maxNumero = numero;
                }
            }
            return $"SOL-{fecha}-{(maxNumero + 1):D3}";
        }

        private bool SolicitudExists(int id) => _context.SolicitudesViajes.Any(e => e.Id == id);

        private bool PuedeVerSolicitud(int solicitudEmpleadoId)
        {
            var empleadoId = ObtenerEmpleadoId();
            if (solicitudEmpleadoId == empleadoId) return true;
            var rolUsuario = User.FindFirstValue(ClaimTypes.Role);
            var rolesAutorizados = new[] { "FINANZAS", "JP", "RH", "DIRECCION", "ADMIN" };
            return !string.IsNullOrEmpty(rolUsuario) && rolesAutorizados.Contains(rolUsuario);
        }

        private List<TiposViatico> ObtenerTiposViatico()
        {
            try { return _context.Set<TiposViatico>().ToList(); }
            catch (Exception ex) { _logger.LogError(ex, "Error obteniendo tipos de viático"); return new List<TiposViatico>(); }
        }
    }
}