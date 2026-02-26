using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SVV.DTOs.Cotizacion;
using SVV.Filters;
using SVV.Models;
using SVV.Services;
using SVV.ViewModels;
using System;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ServicesNotificationItem = SVV.Services.NotificationItem;

namespace SVV.Controllers
{
    // FILTRO QUE OBLIGA A CAMBIAR PASSWORD SI ES PRIMER INGRESO
    [TypeFilter(typeof(CambioPassword))]
    public class FinanzasController : Controller
    {
        private readonly SvvContext _context;
        private readonly IUserService _userService;
        private readonly ICotizacionService _cotizacionService;
        private readonly ILogger<FinanzasController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly INotificationQueue _queue;
        private readonly IConfiguration _configuration;

        // INYECCIÓN DE DEPENDENCIAS CON TODOS LOS SERVICIOS NECESARIOS
        public FinanzasController(
            SvvContext context,
            IWebHostEnvironment environment,
            INotificationQueue queue,
            IConfiguration configuration,
            ICotizacionService cotizacionService,
            IUserService userService,
            ILogger<FinanzasController> logger)
        {
            _context = context;
            _environment = environment;
            _queue = queue;
            _configuration = configuration;
            _cotizacionService = cotizacionService;
            _userService = userService;
            _logger = logger;
        }

        // ============================================
        // SECCIÓN COTIZACIONES - MÉTODOS GET
        // ============================================

        // LISTA TODAS LAS COTIZACIONES DEL SISTEMA
        public async Task<IActionResult> Cotizaciones()
        {
            try
            {
                var cotizaciones = await _context.CotizacionesFinanzas
                    .Include(c => c.SolicitudViaje)
                    .ThenInclude(s => s.Empleado)
                    .Include(c => c.SolicitudViaje)
                    .ThenInclude(s => s.Estado)
                    .Include(c => c.CreadoPor)
                    .Include(c => c.RevisadoPor)
                    .OrderByDescending(c => c.FechaCotizacion)
                    .ToListAsync();

                return View(cotizaciones);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar las cotizaciones: " + ex.Message;
                return View(Enumerable.Empty<CotizacionesFinanzas>());
            }
        }

        // FORMULARIO PARA CREAR NUEVA COTIZACIÓN
        [HttpGet]
        public async Task<IActionResult> CrearCotizacion(int solicitudId)
        {
            try
            {
                if (solicitudId <= 0)
                {
                    TempData["Error"] = "Solicitud inválida.";
                    return RedirectToAction(nameof(Cotizaciones));
                }

                var solicitud = await _context.SolicitudesViajes
                    .Include(s => s.Empleado)
                    .Include(s => s.Estado)
                    .FirstOrDefaultAsync(s => s.Id == solicitudId);

                if (solicitud == null)
                {
                    TempData["Error"] = "La solicitud no existe.";
                    return RedirectToAction(nameof(Cotizaciones));
                }

                var cotizacionExistente = await _context.CotizacionesFinanzas
                    .FirstOrDefaultAsync(c => c.SolicitudViajeId == solicitudId);

                if (cotizacionExistente != null)
                {
                    TempData["Error"] = "Esta solicitud ya cuenta con una cotización.";
                    return RedirectToAction(nameof(DetallesCotizacion), new { id = cotizacionExistente.Id });
                }

                var model = new CrearCotizacionViewModel
                {
                    SolicitudViajeId = solicitud.Id
                };

                await CargarDatosSolicitudEnModeloAsync(model);
                model.InicializarListas();

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al preparar la cotización: " + ex.Message;
                return RedirectToAction(nameof(Cotizaciones));
            }
        }

        // MUESTRA DETALLES DE UNA COTIZACIÓN ESPECÍFICA
        public async Task<IActionResult> DetallesCotizacion(int id)
        {
            try
            {
                var cotizacion = await _context.CotizacionesFinanzas
                    .Include(c => c.SolicitudViaje)
                    .ThenInclude(s => s.Empleado)
                    .Include(c => c.CreadoPor)
                    .Include(c => c.RevisadoPor)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (cotizacion == null)
                {
                    TempData["Error"] = "Cotización no encontrada";
                    return RedirectToAction(nameof(Cotizaciones));
                }

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                if (!string.IsNullOrEmpty(cotizacion.TransportePreciosJson))
                {
                    ViewBag.TransportePrecios = JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(cotizacion.TransportePreciosJson, jsonOptions);
                }
                if (!string.IsNullOrEmpty(cotizacion.GasolinaPreciosJson))
                {
                    ViewBag.GasolinaPrecios = JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(cotizacion.GasolinaPreciosJson, jsonOptions);
                }
                if (!string.IsNullOrEmpty(cotizacion.UberTaxiPreciosJson))
                {
                    ViewBag.UberTaxiPrecios = JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(cotizacion.UberTaxiPreciosJson, jsonOptions);
                }
                if (!string.IsNullOrEmpty(cotizacion.CasetasPreciosJson))
                {
                    ViewBag.CasetasPrecios = JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(cotizacion.CasetasPreciosJson, jsonOptions);
                }
                if (!string.IsNullOrEmpty(cotizacion.HospedajePreciosJson))
                {
                    ViewBag.HospedajePrecios = JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(cotizacion.HospedajePreciosJson, jsonOptions);
                }
                if (!string.IsNullOrEmpty(cotizacion.AlimentosPreciosJson))
                {
                    ViewBag.AlimentosPrecios = JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(cotizacion.AlimentosPreciosJson, jsonOptions);
                }

                return View(cotizacion);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar la cotización: " + ex.Message;
                return RedirectToAction(nameof(Cotizaciones));
            }
        }

        // EDITA UNA COTIZACIÓN EXISTENTE
        public async Task<IActionResult> EditarCotizacion(int? id)
        {
            if (id == null) return NotFound();

            var cotizacion = await _context.CotizacionesFinanzas
                .Include(c => c.SolicitudViaje)
                .ThenInclude(s => s.Empleado)
                .Include(c => c.SolicitudViaje)
                .ThenInclude(s => s.Estado)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cotizacion == null) return NotFound();

            if (cotizacion.SolicitudViaje.EstadoId == 9)
            {
                TempData["Error"] = "No se puede editar la cotización porque la solicitud ya fue aprobada por Dirección General.";
                return RedirectToAction(nameof(Cotizaciones));
            }

            var model = new CrearCotizacionViewModel();
            model.CargarDesdeCotizacion(cotizacion);

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            try
            {
                if (!string.IsNullOrEmpty(cotizacion.TransportePreciosJson))
                {
                    model.TransportePrecios = JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(cotizacion.TransportePreciosJson, jsonOptions) ?? new List<ConceptoItemViewModel>();
                }
                if (!string.IsNullOrEmpty(cotizacion.GasolinaPreciosJson))
                {
                    model.GasolinaPrecios = JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(cotizacion.GasolinaPreciosJson, jsonOptions) ?? new List<ConceptoItemViewModel>();
                }
                if (!string.IsNullOrEmpty(cotizacion.UberTaxiPreciosJson))
                {
                    model.UberTaxiPrecios = JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(cotizacion.UberTaxiPreciosJson, jsonOptions) ?? new List<ConceptoItemViewModel>();
                }
                if (!string.IsNullOrEmpty(cotizacion.CasetasPreciosJson))
                {
                    model.CasetasPrecios = JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(cotizacion.CasetasPreciosJson, jsonOptions) ?? new List<ConceptoItemViewModel>();
                }
                if (!string.IsNullOrEmpty(cotizacion.HospedajePreciosJson))
                {
                    model.HospedajePrecios = JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(cotizacion.HospedajePreciosJson, jsonOptions) ?? new List<ConceptoItemViewModel>();
                }
                if (!string.IsNullOrEmpty(cotizacion.AlimentosPreciosJson))
                {
                    model.AlimentosPrecios = JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(cotizacion.AlimentosPreciosJson, jsonOptions) ?? new List<ConceptoItemViewModel>();
                }
            }
            catch (JsonException)
            {
                model.InicializarListas();
            }

            model.CalcularTotalesDesdeListas();
            model.TransporteCantidad = model.TransportePrecios.Count;
            model.GasolinaCantidad = model.GasolinaPrecios.Count;
            model.UberTaxiCantidad = model.UberTaxiPrecios.Count;
            model.CasetasCantidad = model.CasetasPrecios.Count;
            model.HospedajeCantidad = model.HospedajePrecios.Count;
            model.AlimentosCantidad = model.AlimentosPrecios.Count;
            model.InicializarListas();

            return View(model);
        }

        // CONFIRMA ELIMINACIÓN DE COTIZACIÓN
        public async Task<IActionResult> EliminarCotizacion(int? id)
        {
            if (id == null) return NotFound();

            var cotizacion = await _context.CotizacionesFinanzas
                .Include(c => c.SolicitudViaje)
                .ThenInclude(s => s.Empleado)
                .Include(c => c.SolicitudViaje)
                .ThenInclude(s => s.Estado)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cotizacion == null) return NotFound();

            if (cotizacion.SolicitudViaje.EstadoId == 9)
            {
                TempData["Error"] = "No se puede eliminar la cotización porque la solicitud ya fue aprobada por Dirección General.";
                return RedirectToAction(nameof(Cotizaciones));
            }

            return View(cotizacion);
        }

        // SECCIÓN COTIZACIONES - MÉTODOS POST

        // CREA UNA NUEVA COTIZACIÓN EN LA BASE DE DATOS
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearCotizacion(CrearCotizacionViewModel model)
        {
            try
            {
                ModelState.Remove(nameof(CrearCotizacionViewModel.CodigoSolicitud));
                ModelState.Remove(nameof(CrearCotizacionViewModel.EmpleadoNombre));
                ModelState.Remove(nameof(CrearCotizacionViewModel.Destino));
                ModelState.Remove(nameof(CrearCotizacionViewModel.Proyecto));
                ModelState.Remove(nameof(CrearCotizacionViewModel.UbicacionBase));
                ModelState.Remove(nameof(CrearCotizacionViewModel.FechaSalida));
                ModelState.Remove(nameof(CrearCotizacionViewModel.FechaRegreso));
                ModelState.Remove(nameof(CrearCotizacionViewModel.HoraSalida));
                ModelState.Remove(nameof(CrearCotizacionViewModel.HoraRegreso));
                ModelState.Remove(nameof(CrearCotizacionViewModel.NumeroPersonas));
                ModelState.Remove(nameof(CrearCotizacionViewModel.RequiereHospedaje));
                ModelState.Remove(nameof(CrearCotizacionViewModel.NochesHospedaje));
                ModelState.Remove(nameof(CrearCotizacionViewModel.MedioTraslado));
                ModelState.Remove(nameof(CrearCotizacionViewModel.RequiereTaxiDomicilio));
                ModelState.Remove(nameof(CrearCotizacionViewModel.DistanciaCalculada));
                ModelState.Remove(nameof(CrearCotizacionViewModel.TiempoEstimado));
                ModelState.Remove(nameof(CrearCotizacionViewModel.AlertasCalculo));
                ModelState.Remove(nameof(CrearCotizacionViewModel.ErroresCalculo));
                ModelState.Remove(nameof(CrearCotizacionViewModel.MensajeCalculo));
                ModelState.Remove(nameof(CrearCotizacionViewModel.CalculoRealizado));
                ModelState.Remove(nameof(CrearCotizacionViewModel.DesgloseCalculo));

                if (!ModelState.IsValid)
                {
                    model.CalcularTotalesDesdeListas();
                    await CargarDatosSolicitudEnModeloAsync(model);
                    return View(model);
                }

                if (!model.ValidarCantidades())
                {
                    var errores = model.GetErroresValidacion();
                    foreach (var error in errores)
                    {
                        ModelState.AddModelError(string.Empty, error);
                    }
                    model.CalcularTotalesDesdeListas();
                    await CargarDatosSolicitudEnModeloAsync(model);
                    return View(model);
                }

                model.CalcularTotalesDesdeListas();

                if (model.Total <= 0)
                {
                    ModelState.AddModelError(string.Empty, "Al menos un concepto debe tener precios mayores a $0.00");
                    await CargarDatosSolicitudEnModeloAsync(model);
                    return View(model);
                }

                var cotizacionExistente = await _context.CotizacionesFinanzas
                    .FirstOrDefaultAsync(c => c.SolicitudViajeId == model.SolicitudViajeId);

                if (cotizacionExistente != null)
                {
                    ModelState.AddModelError(string.Empty, "Ya existe una cotización para esta solicitud.");
                    model.CalcularTotalesDesdeListas();
                    await CargarDatosSolicitudEnModeloAsync(model);
                    return View(model);
                }

                var solicitud = await _context.SolicitudesViajes
                    .Include(s => s.Empleado)
                    .FirstOrDefaultAsync(s => s.Id == model.SolicitudViajeId);

                if (solicitud == null)
                {
                    TempData["Error"] = "Solicitud no encontrada";
                    return RedirectToAction(nameof(Cotizaciones));
                }

                var usuarioActualId = await GetCurrentUserIdAsync();

                var cotizacion = new CotizacionesFinanzas
                {
                    SolicitudViajeId = model.SolicitudViajeId,
                    CodigoCotizacion = GenerarCodigoCotizacion(),
                    TransporteCantidad = (decimal?)model.TransporteCantidad,
                    GasolinaCantidad = (decimal?)model.GasolinaCantidad,
                    UberTaxiCantidad = (decimal?)model.UberTaxiCantidad,
                    CasetasCantidad = (decimal?)model.CasetasCantidad,
                    HospedajeCantidad = (decimal?)model.HospedajeCantidad,
                    AlimentosCantidad = (decimal?)model.AlimentosCantidad,
                    TransportePreciosJson = JsonSerializer.Serialize(model.TransportePrecios),
                    GasolinaPreciosJson = JsonSerializer.Serialize(model.GasolinaPrecios),
                    UberTaxiPreciosJson = JsonSerializer.Serialize(model.UberTaxiPrecios),
                    CasetasPreciosJson = JsonSerializer.Serialize(model.CasetasPrecios),
                    HospedajePreciosJson = JsonSerializer.Serialize(model.HospedajePrecios),
                    AlimentosPreciosJson = JsonSerializer.Serialize(model.AlimentosPrecios),
                    TransporteTotal = model.TotalTransporte,
                    GasolinaTotal = model.TotalGasolina,
                    UberTaxiTotal = model.TotalUberTaxi,
                    CasetasTotal = model.TotalCasetas,
                    HospedajeTotal = model.TotalHospedaje,
                    AlimentosTotal = model.TotalAlimentos,
                    Observaciones = model.Observaciones,
                    CreadoPorId = usuarioActualId,
                    Estado = "APROBADA",
                    FechaCotizacion = DateTime.Now,
                    FechaAprobacion = DateTime.Now,
                    RevisadoPorId = usuarioActualId,
                    CreatedAt = DateTime.Now,
                    TotalAutorizado = model.Total
                };

                solicitud.MontoAnticipo = cotizacion.TotalAutorizado;
                solicitud.RequiereAnticipo = true;

                var estadoEnviadaFinanzas = await _context.EstadosSolicitud
                    .FirstOrDefaultAsync(e => e.Codigo == "ENVIADA_FINANZAS");

                if (estadoEnviadaFinanzas != null && solicitud.EstadoId != estadoEnviadaFinanzas.Id)
                {
                    solicitud.EstadoId = estadoEnviadaFinanzas.Id;
                }

                _context.CotizacionesFinanzas.Add(cotizacion);

                var anticipo = new Anticipos
                {
                    SolicitudViajeId = solicitud.Id,
                    CodigoAnticipo = GenerarCodigoAnticipo(),
                    MontoSolicitado = solicitud.MontoAnticipo ?? 0,
                    MontoAutorizado = cotizacion.TotalAutorizado,
                    Estado = "AUTORIZADO",
                    FechaSolicitud = solicitud.CreatedAt,
                    FechaAutorizacion = DateTime.Now,
                    AutorizadoPorId = usuarioActualId,
                    CreatedAt = DateTime.Now
                };
                _context.Anticipos.Add(anticipo);

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Cotización {cotizacion.CodigoCotizacion} creada y aprobada exitosamente. " +
                                    $"Anticipo autorizado: {cotizacion.TotalAutorizado.ToString("C")} " +
                                    $"La solicitud sigue en revisión de Finanzas.";

                return RedirectToAction("Pendientes", "Aprobaciones");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al crear la cotización: {ex.Message}";
                model.CalcularTotalesDesdeListas();
                await CargarDatosSolicitudEnModeloAsync(model);
                return View(model);
            }
        }

        // ACTUALIZA UNA COTIZACIÓN EXISTENTE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarCotizacion(int id, CrearCotizacionViewModel model)
        {
            if (id != model.Id) return NotFound();

            ModelState.Remove(nameof(CrearCotizacionViewModel.CodigoSolicitud));
            ModelState.Remove(nameof(CrearCotizacionViewModel.EmpleadoNombre));
            ModelState.Remove(nameof(CrearCotizacionViewModel.Destino));
            ModelState.Remove(nameof(CrearCotizacionViewModel.Proyecto));
            ModelState.Remove(nameof(CrearCotizacionViewModel.UbicacionBase));
            ModelState.Remove(nameof(CrearCotizacionViewModel.FechaSalida));
            ModelState.Remove(nameof(CrearCotizacionViewModel.FechaRegreso));
            ModelState.Remove(nameof(CrearCotizacionViewModel.NumeroPersonas));
            ModelState.Remove(nameof(CrearCotizacionViewModel.RequiereHospedaje));
            ModelState.Remove(nameof(CrearCotizacionViewModel.NochesHospedaje));
            ModelState.Remove(nameof(CrearCotizacionViewModel.MedioTraslado));
            ModelState.Remove(nameof(CrearCotizacionViewModel.RequiereTaxiDomicilio));
            ModelState.Remove(nameof(CrearCotizacionViewModel.UsarCalculoAutomatico));
            ModelState.Remove(nameof(CrearCotizacionViewModel.DistanciaCalculada));
            ModelState.Remove(nameof(CrearCotizacionViewModel.TiempoEstimado));
            ModelState.Remove(nameof(CrearCotizacionViewModel.AlertasCalculo));
            ModelState.Remove(nameof(CrearCotizacionViewModel.ErroresCalculo));
            ModelState.Remove(nameof(CrearCotizacionViewModel.MensajeCalculo));
            ModelState.Remove(nameof(CrearCotizacionViewModel.CalculoRealizado));
            ModelState.Remove(nameof(CrearCotizacionViewModel.DesgloseCalculo));

            var cotizacionExistente = await _context.CotizacionesFinanzas
                .Include(c => c.SolicitudViaje)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cotizacionExistente == null) return NotFound();

            if (cotizacionExistente.SolicitudViaje.EstadoId == 9)
            {
                TempData["Error"] = "No se puede editar la cotización porque la solicitud ya fue aprobada por Dirección General.";
                return RedirectToAction(nameof(Cotizaciones));
            }

            if (!model.ValidarCantidades())
            {
                var errores = model.GetErroresValidacion();
                foreach (var error in errores)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
                model.CalcularTotalesDesdeListas();
                return View(model);
            }

            model.CalcularTotalesDesdeListas();

            if (model.Total <= 0)
            {
                ModelState.AddModelError(string.Empty, "Al menos un concepto debe tener precios mayores a $0.00");
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var cotizacion = await _context.CotizacionesFinanzas
                    .Include(c => c.SolicitudViaje)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (cotizacion == null) return NotFound();

                cotizacion.TransportePreciosJson = JsonSerializer.Serialize(model.TransportePrecios);
                cotizacion.GasolinaPreciosJson = JsonSerializer.Serialize(model.GasolinaPrecios);
                cotizacion.UberTaxiPreciosJson = JsonSerializer.Serialize(model.UberTaxiPrecios);
                cotizacion.CasetasPreciosJson = JsonSerializer.Serialize(model.CasetasPrecios);
                cotizacion.HospedajePreciosJson = JsonSerializer.Serialize(model.HospedajePrecios);
                cotizacion.AlimentosPreciosJson = JsonSerializer.Serialize(model.AlimentosPrecios);
                cotizacion.TransporteCantidad = (decimal?)model.TransporteCantidad;
                cotizacion.GasolinaCantidad = (decimal?)model.GasolinaCantidad;
                cotizacion.UberTaxiCantidad = (decimal?)model.UberTaxiCantidad;
                cotizacion.CasetasCantidad = (decimal?)model.CasetasCantidad;
                cotizacion.HospedajeCantidad = (decimal?)model.HospedajeCantidad;
                cotizacion.AlimentosCantidad = (decimal?)model.AlimentosCantidad;
                cotizacion.TransporteTotal = model.TotalTransporte;
                cotizacion.GasolinaTotal = model.TotalGasolina;
                cotizacion.UberTaxiTotal = model.TotalUberTaxi;
                cotizacion.CasetasTotal = model.TotalCasetas;
                cotizacion.HospedajeTotal = model.TotalHospedaje;
                cotizacion.AlimentosTotal = model.TotalAlimentos;
                cotizacion.TotalAutorizado = model.Total;
                cotizacion.Observaciones = model.Observaciones;

                var anticipo = await _context.Anticipos
                    .FirstOrDefaultAsync(a => a.SolicitudViajeId == cotizacion.SolicitudViajeId);

                if (anticipo != null)
                {
                    anticipo.MontoAutorizado = model.Total;
                    _context.Anticipos.Update(anticipo);
                }

                if (cotizacion.SolicitudViaje != null)
                {
                    cotizacion.SolicitudViaje.MontoAnticipo = model.Total;
                    cotizacion.SolicitudViaje.RequiereAnticipo = true;
                }

                _context.Update(cotizacion);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Cotización {cotizacion.CodigoCotizacion} actualizada exitosamente. " +
                                    $"Nuevo total: {model.Total.ToString("C")}";

                return RedirectToAction(nameof(Cotizaciones));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!CotizacionExists(id))
                {
                    return NotFound();
                }
                else
                {
                    TempData["Error"] = "Error de concurrencia al actualizar: " + ex.Message;
                    model.CalcularTotalesDesdeListas();
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar la cotización: " + ex.Message;
                model.CalcularTotalesDesdeListas();
                return View(model);
            }
        }

        // ELIMINA DEFINITIVAMENTE UNA COTIZACIÓN
        [HttpPost, ActionName("EliminarCotizacion")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarCotizacionConfirmada(int id)
        {
            var cotizacion = await _context.CotizacionesFinanzas
                .Include(c => c.SolicitudViaje)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cotizacion == null) return NotFound();

            if (cotizacion.SolicitudViaje.EstadoId == 9)
            {
                TempData["Error"] = "No se puede eliminar la cotización porque la solicitud ya fue aprobada por Dirección General.";
                return RedirectToAction(nameof(Cotizaciones));
            }

            try
            {
                var anticipo = await _context.Anticipos
                    .FirstOrDefaultAsync(a => a.SolicitudViajeId == cotizacion.SolicitudViajeId);

                if (anticipo != null)
                {
                    _context.Anticipos.Remove(anticipo);
                }

                var solicitud = cotizacion.SolicitudViaje;
                solicitud.MontoAnticipo = null;
                solicitud.RequiereAnticipo = false;

                var estadoEnviadaFinanzas = await _context.EstadosSolicitud
                    .FirstOrDefaultAsync(e => e.Codigo == "ENVIADA_FINANZAS");

                if (estadoEnviadaFinanzas != null)
                {
                    solicitud.EstadoId = estadoEnviadaFinanzas.Id;
                }

                _context.CotizacionesFinanzas.Remove(cotizacion);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Cotización {cotizacion.CodigoCotizacion} eliminada exitosamente. " +
                                    "El anticipo asociado también ha sido eliminado y la solicitud ha regresado a Finanzas.";
            }
            catch (DbUpdateException ex)
            {
                TempData["Error"] = "No se puede eliminar la cotización porque tiene registros relacionados. " + ex.Message;
                return RedirectToAction(nameof(EliminarCotizacion), new { id = id });
            }

            return RedirectToAction(nameof(Cotizaciones));
        }

        // ============================================
        // MÉTODOS PARA CÁLCULO AUTOMÁTICO DE COTIZACIONES
        // ============================================

        // CALCULA AUTOMÁTICAMENTE UNA COTIZACIÓN BASADA EN DESTINO Y PARÁMETROS
        [HttpPost]
        public async Task<IActionResult> CalcularAutomatico([FromBody] CalculoAutomaticoDto dto)
        {
            try
            {
                _logger.LogInformation("Iniciando cálculo automático para solicitud: {SolicitudId}", dto.SolicitudViajeId);

                var resultado = await _cotizacionService.CalcularAsync(new CalcularCotizacionDto
                {
                    SolicitudViajeId = dto.SolicitudViajeId,
                    EsCalculoAutomatico = true,
                    Origen = dto.UbicacionBase,
                    Destino = dto.Destino,
                    FechaSalida = dto.FechaSalida,
                    FechaRegreso = dto.FechaRegreso,
                    NumeroPersonas = dto.NumeroPersonas,
                    RequiereHospedaje = dto.RequiereHospedaje,
                    NochesHospedaje = dto.NochesHospedaje,
                    MedioTraslado = dto.MedioTraslado,
                    RequiereTaxiDomicilio = dto.RequiereTaxiDomicilio,
                    DireccionTaxiOrigen = dto.DireccionTaxiOrigen,
                    DireccionTaxiDestino = dto.DireccionTaxiDestino
                });

                return Ok(new ResultadoCalculoAutomaticoDto
                {
                    Success = resultado.Success,
                    Message = resultado.Mensaje,
                    DistanciaCalculada = resultado.DistanciaCalculada,
                    Desglose = new DesgloseCalculoDto
                    {
                        Transporte = resultado.TotalTransporte,
                        Gasolina = resultado.TotalGasolina,
                        UberTaxi = resultado.TotalUberTaxi,
                        Casetas = resultado.TotalCasetas,
                        Hospedaje = resultado.TotalHospedaje,
                        Alimentos = resultado.TotalAlimentos
                    },
                    Alertas = resultado.Alertas,
                    Errores = resultado.Errores,
                    DetallesTransporte = resultado.DetalleTransporte,
                    DetallesGasolina = resultado.DetalleGasolina,
                    DetallesUberTaxi = resultado.DetalleUberTaxi,
                    DetallesCasetas = resultado.DetalleCasetas,
                    DetallesHospedaje = resultado.DetalleHospedaje,
                    DetallesAlimentos = resultado.DetalleAlimentos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en cálculo automático");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor",
                    detail = ex.Message
                });
            }
        }

        // APLICA RESULTADOS DEL CÁLCULO AUTOMÁTICO AL VIEWMODEL
        [HttpPost]
        public async Task<IActionResult> AplicarCalculoAutomatico(int solicitudId)
        {
            try
            {
                var solicitud = await _context.SolicitudesViajes
                    .Include(s => s.Empleado)
                    .FirstOrDefaultAsync(s => s.Id == solicitudId);

                if (solicitud == null)
                {
                    return NotFound(new { success = false, message = "Solicitud no encontrada" });
                }

                var resultado = await _cotizacionService.CalcularAsync(new CalcularCotizacionDto
                {
                    SolicitudViajeId = solicitudId,
                    EsCalculoAutomatico = true,
                    Origen = solicitud.Empleado?.UbicacionBase ?? "Puebla",
                    Destino = solicitud.Destino,
                    FechaSalida = solicitud.FechaSalida,
                    FechaRegreso = solicitud.FechaRegreso,
                    NumeroPersonas = solicitud.NumeroPersonas ?? 1,
                    RequiereHospedaje = solicitud.RequiereHospedaje ?? false,
                    NochesHospedaje = solicitud.NochesHospedaje ?? 0,
                    MedioTraslado = solicitud.MedioTrasladoPrincipal ?? "Avión",
                    RequiereTaxiDomicilio = solicitud.RequiereTaxiDomicilio ?? false,
                    DireccionTaxiOrigen = solicitud.DireccionTaxiOrigen,
                    DireccionTaxiDestino = solicitud.DireccionTaxiDestino
                });

                if (!resultado.Success)
                {
                    return BadRequest(new { success = false, errors = resultado.Errores });
                }

                var viewModel = new CrearCotizacionViewModel
                {
                    SolicitudViajeId = solicitud.Id,
                    CodigoSolicitud = solicitud.CodigoSolicitud,
                    EmpleadoNombre = $"{solicitud.Empleado?.Nombre} {solicitud.Empleado?.Apellidos}",
                    Destino = solicitud.Destino,
                    Proyecto = solicitud.NombreProyecto,
                    UbicacionBase = solicitud.Empleado?.UbicacionBase ?? "Puebla",
                    FechaSalida = solicitud.FechaSalida,
                    FechaRegreso = solicitud.FechaRegreso,
                    HoraSalida = solicitud.HoraSalida.HasValue ?
                                (TimeSpan?)solicitud.HoraSalida.Value.ToTimeSpan() : null,
                    HoraRegreso = solicitud.HoraRegreso.HasValue ?
                                 (TimeSpan?)solicitud.HoraRegreso.Value.ToTimeSpan() : null,
                    NumeroPersonas = solicitud.NumeroPersonas ?? 1,
                    RequiereHospedaje = solicitud.RequiereHospedaje ?? false,
                    NochesHospedaje = solicitud.NochesHospedaje ?? 0,
                    MedioTraslado = solicitud.MedioTrasladoPrincipal ?? "Avión",
                    RequiereTaxiDomicilio = solicitud.RequiereTaxiDomicilio ?? false,
                    DireccionTaxiOrigen = solicitud.DireccionTaxiOrigen,
                    DireccionTaxiDestino = solicitud.DireccionTaxiDestino,
                    DistanciaCalculada = resultado.DistanciaCalculada,
                    CalculoRealizado = true,
                    MensajeCalculo = resultado.Mensaje,
                    AlertasCalculo = resultado.Alertas,
                    ErroresCalculo = resultado.Errores,
                    DesgloseCalculo = new DesgloseCalculoViewModel
                    {
                        Transporte = resultado.TotalTransporte,
                        Gasolina = resultado.TotalGasolina,
                        UberTaxi = resultado.TotalUberTaxi,
                        Casetas = resultado.TotalCasetas,
                        Hospedaje = resultado.TotalHospedaje,
                        Alimentos = resultado.TotalAlimentos
                    }
                };

                viewModel.AplicarResultadoCalculo(resultado);
                viewModel.CalcularTotalesDesdeListas();

                return Ok(new
                {
                    success = true,
                    data = viewModel,
                    message = "Cálculo automático aplicado correctamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al aplicar cálculo automático");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor"
                });
            }
        }

        // ============================================
        // EXPORTACIÓN DE COTIZACIONES A EXCEL

        // GENERA REPORTE EN EXCEL CON TODAS LAS COTIZACIONES
        [HttpGet]
        public async Task<IActionResult> ExportarCotizacionesExcel()
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            try
            {
                var cotizaciones = await _context.CotizacionesFinanzas
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                            .ThenInclude(e => e.JefeDirecto)
                    .Include(c => c.CreadoPor)
                    .Include(c => c.RevisadoPor)
                    .OrderByDescending(c => c.FechaCotizacion)
                    .ToListAsync();

                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Cotizaciones");

                var headerStyle = worksheet.Cells[1, 1, 1, 12].Style;
                headerStyle.Fill.PatternType = ExcelFillStyle.Solid;
                headerStyle.Fill.BackgroundColor.SetColor(Color.FromArgb(0, 102, 51));
                headerStyle.Font.Color.SetColor(Color.White);
                headerStyle.Font.Bold = true;
                headerStyle.Font.Size = 11;
                headerStyle.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                headerStyle.VerticalAlignment = ExcelVerticalAlignment.Center;
                headerStyle.Border.Top.Style = ExcelBorderStyle.Thin;
                headerStyle.Border.Bottom.Style = ExcelBorderStyle.Thin;
                headerStyle.Border.Left.Style = ExcelBorderStyle.Thin;
                headerStyle.Border.Right.Style = ExcelBorderStyle.Thin;

                worksheet.Cells[1, 1].Value = "CÓDIGO COTIZACIÓN";
                worksheet.Cells[1, 2].Value = "CÓDIGO SOLICITUD";
                worksheet.Cells[1, 3].Value = "EMPLEADO";
                worksheet.Cells[1, 4].Value = "EMAIL";
                worksheet.Cells[1, 5].Value = "PUESTO";
                worksheet.Cells[1, 6].Value = "JEFE DIRECTO";
                worksheet.Cells[1, 7].Value = "TOTAL AUTORIZADO";
                worksheet.Cells[1, 8].Value = "ESTADO";
                worksheet.Cells[1, 9].Value = "FECHA COTIZACIÓN";
                worksheet.Cells[1, 10].Value = "FECHA CREACIÓN";
                worksheet.Cells[1, 11].Value = "CREADO POR";
                worksheet.Cells[1, 12].Value = "DETALLES DE CONCEPTOS";

                worksheet.Column(1).Width = 20;
                worksheet.Column(2).Width = 20;
                worksheet.Column(3).Width = 25;
                worksheet.Column(4).Width = 25;
                worksheet.Column(5).Width = 20;
                worksheet.Column(6).Width = 25;
                worksheet.Column(7).Width = 18;
                worksheet.Column(8).Width = 15;
                worksheet.Column(9).Width = 15;
                worksheet.Column(10).Width = 15;
                worksheet.Column(11).Width = 20;
                worksheet.Column(12).Width = 40;

                int row = 2;
                foreach (var cotizacion in cotizaciones)
                {
                    worksheet.Cells[row, 1].Value = cotizacion.CodigoCotizacion;
                    worksheet.Cells[row, 2].Value = cotizacion.SolicitudViaje?.CodigoSolicitud ?? "N/A";
                    worksheet.Cells[row, 3].Value = $"{cotizacion.SolicitudViaje?.Empleado?.Nombre} {cotizacion.SolicitudViaje?.Empleado?.Apellidos}";
                    worksheet.Cells[row, 4].Value = cotizacion.SolicitudViaje?.Empleado?.Email ?? "N/A";
                    worksheet.Cells[row, 5].Value = cotizacion.SolicitudViaje?.Empleado?.Puesto ?? "N/A";
                    worksheet.Cells[row, 6].Value = $"{cotizacion.SolicitudViaje?.Empleado?.JefeDirecto?.Nombre} {cotizacion.SolicitudViaje?.Empleado?.JefeDirecto?.Apellidos}" ?? "N/A";
                    worksheet.Cells[row, 7].Value = cotizacion.TotalAutorizado;
                    worksheet.Cells[row, 7].Style.Numberformat.Format = "$#,##0.00";

                    var estadoCell = worksheet.Cells[row, 8];
                    estadoCell.Value = cotizacion.Estado;

                    if (cotizacion.Estado == "APROBADA")
                    {
                        estadoCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        estadoCell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(220, 237, 220));
                        estadoCell.Style.Font.Color.SetColor(Color.FromArgb(0, 102, 51));
                    }
                    else if (cotizacion.Estado == "PENDIENTE")
                    {
                        estadoCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        estadoCell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 205));
                        estadoCell.Style.Font.Color.SetColor(Color.FromArgb(133, 100, 4));
                    }
                    else if (cotizacion.Estado == "RECHAZADA")
                    {
                        estadoCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        estadoCell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(248, 215, 218));
                        estadoCell.Style.Font.Color.SetColor(Color.FromArgb(114, 28, 36));
                    }

                    worksheet.Cells[row, 9].Value = cotizacion.FechaCotizacion?.ToString("dd/MM/yyyy");
                    worksheet.Cells[row, 10].Value = cotizacion.CreatedAt?.ToString("dd/MM/yyyy HH:mm");
                    worksheet.Cells[row, 11].Value = $"{cotizacion.CreadoPor?.Nombre} {cotizacion.CreadoPor?.Apellidos}" ?? "N/A";

                    var detalles = new List<string>();

                    if (cotizacion.TransporteTotal > 0)
                        detalles.Add($"Transporte: {cotizacion.TransporteCantidad} x ${cotizacion.TransporteTotal:C2}");

                    if (cotizacion.GasolinaTotal > 0)
                        detalles.Add($"Gasolina: {cotizacion.GasolinaCantidad} x ${cotizacion.GasolinaTotal:C2}");

                    if (cotizacion.UberTaxiTotal > 0)
                        detalles.Add($"Uber/Taxi: {cotizacion.UberTaxiCantidad} x ${cotizacion.UberTaxiTotal:C2}");

                    if (cotizacion.CasetasTotal > 0)
                        detalles.Add($"Casetas: {cotizacion.CasetasCantidad} x ${cotizacion.CasetasTotal:C2}");

                    if (cotizacion.HospedajeTotal > 0)
                        detalles.Add($"Hospedaje: {cotizacion.HospedajeCantidad} x ${cotizacion.HospedajeTotal:C2}");

                    if (cotizacion.AlimentosTotal > 0)
                        detalles.Add($"Alimentos: {cotizacion.AlimentosCantidad} x ${cotizacion.AlimentosTotal:C2}");

                    worksheet.Cells[row, 12].Value = string.Join("; ", detalles);

                    for (int col = 1; col <= 12; col++)
                    {
                        worksheet.Cells[row, col].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells[row, col].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells[row, col].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells[row, col].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                        worksheet.Cells[row, col].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }

                    row++;
                }

                int totalRow = row + 1;
                worksheet.Cells[totalRow, 6].Value = "TOTAL GENERAL:";
                worksheet.Cells[totalRow, 6].Style.Font.Bold = true;
                worksheet.Cells[totalRow, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                worksheet.Cells[totalRow, 7].Value = cotizaciones.Sum(c => c.TotalAutorizado);
                worksheet.Cells[totalRow, 7].Style.Numberformat.Format = "$#,##0.00";
                worksheet.Cells[totalRow, 7].Style.Font.Bold = true;
                worksheet.Cells[totalRow, 7].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[totalRow, 7].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(220, 237, 220));
                worksheet.Cells[totalRow, 7].Style.Font.Color.SetColor(Color.FromArgb(0, 102, 51));

                int statsRow = totalRow + 2;
                worksheet.Cells[statsRow, 1].Value = "ESTADÍSTICAS DE COTIZACIONES";
                worksheet.Cells[statsRow, 1].Style.Font.Bold = true;
                worksheet.Cells[statsRow, 1].Style.Font.Size = 12;
                worksheet.Cells[statsRow, 1].Style.Font.Color.SetColor(Color.FromArgb(0, 102, 51));

                statsRow++;
                worksheet.Cells[statsRow, 1].Value = "Total Cotizaciones:";
                worksheet.Cells[statsRow, 2].Value = cotizaciones.Count;

                statsRow++;
                worksheet.Cells[statsRow, 1].Value = "Aprobadas:";
                worksheet.Cells[statsRow, 2].Value = cotizaciones.Count(c => c.Estado == "APROBADA");

                statsRow++;
                worksheet.Cells[statsRow, 1].Value = "Pendientes:";
                worksheet.Cells[statsRow, 2].Value = cotizaciones.Count(c => c.Estado == "PENDIENTE");

                statsRow++;
                worksheet.Cells[statsRow, 1].Value = "Rechazadas:";
                worksheet.Cells[statsRow, 2].Value = cotizaciones.Count(c => c.Estado == "RECHAZADA");

                statsRow++;
                worksheet.Cells[statsRow, 1].Value = "Total Monto Autorizado:";
                worksheet.Cells[statsRow, 2].Value = cotizaciones.Where(c => c.Estado == "APROBADA").Sum(c => c.TotalAutorizado);
                worksheet.Cells[statsRow, 2].Style.Numberformat.Format = "$#,##0.00";

                worksheet.InsertRow(1, 3);

                worksheet.Cells[1, 1].Value = "REPORTE DE COTIZACIONES - VIAMTEK";
                worksheet.Cells[1, 1, 1, 12].Merge = true;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.Font.Size = 16;
                worksheet.Cells[1, 1].Style.Font.Color.SetColor(Color.FromArgb(0, 102, 51));
                worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[1, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

                worksheet.Cells[2, 1].Value = $"Fecha de generación: {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}";
                worksheet.Cells[2, 1, 2, 12].Merge = true;
                worksheet.Cells[2, 1].Style.Font.Italic = true;
                worksheet.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[2, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

                worksheet.Cells[3, 1, 3, 12].Merge = true;
                worksheet.Cells[3, 1].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
                worksheet.Cells[3, 1].Style.Border.Bottom.Color.SetColor(Color.FromArgb(0, 102, 51));

                worksheet.Cells[4, 1, worksheet.Dimension.End.Row, worksheet.Dimension.End.Column]
                    .Copy(worksheet.Cells[7, 1]);

                worksheet.DeleteRow(4, 3);

                int firmaRow = worksheet.Dimension.End.Row + 3;
                worksheet.Cells[firmaRow, 1].Value = "__________________________________";
                worksheet.Cells[firmaRow, 1, firmaRow, 4].Merge = true;
                worksheet.Cells[firmaRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                firmaRow++;
                worksheet.Cells[firmaRow, 1].Value = "Finanzas Viamtek";
                worksheet.Cells[firmaRow, 1, firmaRow, 4].Merge = true;
                worksheet.Cells[firmaRow, 1].Style.Font.Bold = true;
                worksheet.Cells[firmaRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                var fileName = $"Cotizaciones_Viamtek_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var fileBytes = package.GetAsByteArray();

                return File(fileBytes,
                           "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                           fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar cotizaciones a Excel");
                return Json(new { success = false, message = "Error al generar el archivo Excel" });
            }
        }

        // ============================================
        // SECCIÓN FACTURAS Y VALIDACIONES

        // MUESTRA LISTADO DE FACTURAS PENDIENTES DE VALIDACIÓN
        public async Task<IActionResult> Facturas()
        {
            try
            {
                var comprobacionesIds = await _context.ComprobacionesViaje
                    .Where(c => c.EstadoComprobacionId == 1 || c.EstadoComprobacionId == 2 || c.EstadoComprobacionId == 8 || c.EstadoComprobacionId == 9)
                    .Select(c => new { c.Id, c.CodigoComprobacion, c.EstadoComprobacionId, c.SolicitudViajeId })
                    .ToListAsync();

                var solicitudIds = comprobacionesIds.Select(c => c.SolicitudViajeId).Distinct().ToList();

                var solicitudes = await _context.SolicitudesViajes
                    .Include(s => s.Empleado)
                    .Include(s => s.GastosReales)
                        .ThenInclude(g => g.CategoriaGasto)
                    .Include(s => s.GastosReales)
                        .ThenInclude(g => g.Factura)
                    .Include(s => s.GastosReales)
                        .ThenInclude(g => g.EstadoGasto)
                    .Where(s => solicitudIds.Contains(s.Id))
                    .ToListAsync();

                var estadoIds = comprobacionesIds.Select(c => c.EstadoComprobacionId).Distinct().ToList();
                var estados = await _context.EstadosComprobacion
                    .Where(e => estadoIds.Contains(e.Id))
                    .ToListAsync();

                var comprobaciones = await _context.ComprobacionesViaje
                    .Where(c => c.EstadoComprobacionId == 1 || c.EstadoComprobacionId == 2 || c.EstadoComprobacionId == 9)
                    .ToListAsync();

                var viewModel = new List<FacturasViewModel>();

                foreach (var comp in comprobaciones)
                {
                    var solicitud = solicitudes.FirstOrDefault(s => s.Id == comp.SolicitudViajeId);
                    if (solicitud == null) continue;

                    var estado = estados.FirstOrDefault(e => e.Id == comp.EstadoComprobacionId);

                    var totalGastos = comp.TotalGastosComprobados ?? 0;
                    var totalAnticipo = comp.TotalAnticipo ?? 0;
                    var diferencia = (comp.TotalAnticipo ?? 0) - (comp.TotalGastosComprobados ?? 0);

                    var gastosVM = new List<GastoFacturaViewModel>();
                    if (solicitud.GastosReales != null && solicitud.GastosReales.Any())
                    {
                        foreach (var gasto in solicitud.GastosReales)
                        {
                            gastosVM.Add(new GastoFacturaViewModel
                            {
                                GastoId = gasto.Id,
                                Categoria = gasto.CategoriaGasto?.Nombre ?? "N/A",
                                Concepto = gasto.Concepto,
                                FechaGasto = gasto.FechaGasto.ToDateTime(TimeOnly.MinValue),
                                Monto = gasto.Monto,
                                Proveedor = gasto.Proveedor ?? "N/A",
                                FacturaPDF = gasto.Factura?.ArchivoPdfUrl,
                                FacturaXML = gasto.Factura?.ArchivoXmlUrl,
                                EstadoValidacion = "NO_VALIDADO",  
                                ErroresValidacion = null,
                                TieneXML = !string.IsNullOrEmpty(gasto.Factura?.ArchivoXmlUrl),
                                TienePDF = !string.IsNullOrEmpty(gasto.Factura?.ArchivoPdfUrl),
                                EstadoGasto = gasto.EstadoGasto?.Nombre ?? "Pendiente",
                                EstadoGastoId = gasto.EstadoGastoId,
                                EstadoGastoCodigo = gasto.EstadoGasto?.Codigo ?? "PENDIENTE"
                            });
                        }
                    }

                    var facturaVM = new FacturasViewModel
                    {
                        ComprobacionId = comp.Id,
                        CodigoComprobacion = comp.CodigoComprobacion,
                        CodigoSolicitud = solicitud.CodigoSolicitud,
                        EmpleadoNombre = $"{solicitud.Empleado?.Nombre} {solicitud.Empleado?.Apellidos}",
                        Destino = solicitud.Destino,
                        FechaComprobacion = comp.FechaComprobacion ?? DateTime.MinValue,
                        TotalGastosComprobados = totalGastos,
                        TotalAnticipo = totalAnticipo,
                        Diferencia = diferencia,
                        EstadoComprobacion = estado?.Codigo ?? "Desconocido",
                        EstadoComprobacionId = comp.EstadoComprobacionId,
                        ComentariosFinanzas = comp.ComentariosFinanzas,
                        Gastos = gastosVM,
                        TieneFacturasPendientes = solicitud.GastosReales?.Any(g =>
                            string.IsNullOrEmpty(g.Factura?.ArchivoXmlUrl)) ?? false
                    };

                    viewModel.Add(facturaVM);
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al cargar las facturas: {ex.Message}";
                return View(new List<FacturasViewModel>());
            }
        }

        // MUESTRA DETALLE COMPLETO DE UNA FACTURA
        public async Task<IActionResult> DetallesFactura(int id)
        {
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.EstadoComprobacion)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.CategoriaGasto)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.Factura)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.EstadoGasto)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (comprobacion == null)
                {
                    TempData["Error"] = "Comprobación no encontrada";
                    return RedirectToAction(nameof(Facturas));
                }

                var viewModel = new FacturasViewModel
                {
                    ComprobacionId = comprobacion.Id,
                    CodigoComprobacion = comprobacion.CodigoComprobacion,
                    CodigoSolicitud = comprobacion.SolicitudViaje.CodigoSolicitud,
                    EmpleadoNombre = $"{comprobacion.SolicitudViaje.Empleado.Nombre} {comprobacion.SolicitudViaje.Empleado.Apellidos}",
                    Destino = comprobacion.SolicitudViaje.Destino,
                    FechaComprobacion = comprobacion.FechaComprobacion ?? DateTime.MinValue,
                    TotalGastosComprobados = comprobacion.TotalGastosComprobados,
                    TotalAnticipo = comprobacion.TotalAnticipo,
                    Diferencia = (comprobacion.TotalAnticipo ?? 0) - (comprobacion.TotalGastosComprobados ?? 0),
                    EstadoComprobacion = comprobacion.EstadoComprobacion.Codigo ?? "Desconocido",
                    EstadoComprobacionId = comprobacion.EstadoComprobacionId,
                    ComentariosFinanzas = comprobacion.ComentariosFinanzas,
                    DescripcionActividades = comprobacion.DescripcionActividades,
                    ResultadosViaje = comprobacion.ResultadosViaje,

                    Gastos = comprobacion.SolicitudViaje.GastosReales.Select(g => new GastoFacturaViewModel
                    {
                        GastoId = g.Id,
                        Categoria = g.CategoriaGasto?.Nombre ?? "N/A",
                        Concepto = g.Concepto,
                        FechaGasto = g.FechaGasto.ToDateTime(TimeOnly.MinValue),
                        Monto = g.Monto,
                        Proveedor = g.Proveedor ?? "N/A",
                        FacturaPDF = g.Factura?.ArchivoPdfUrl,
                        FacturaXML = g.Factura?.ArchivoXmlUrl,
                        EstadoValidacion = "NO_VALIDADO",   // <-- CORREGIDO
                        ErroresValidacion = null,
                        TieneXML = !string.IsNullOrEmpty(g.Factura?.ArchivoXmlUrl),
                        TienePDF = !string.IsNullOrEmpty(g.Factura?.ArchivoPdfUrl),
                        EstadoGasto = g.EstadoGasto?.Nombre ?? "Pendiente"
                    }).ToList(),
                    TieneFacturasPendientes = comprobacion.SolicitudViaje.GastosReales.Any(g =>
                        string.IsNullOrEmpty(g.Factura?.ArchivoXmlUrl))
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar los detalles: " + ex.Message;
                return RedirectToAction(nameof(Facturas));
            }
        }

        // VALIDA UNA COMPROBACIÓN Y DETERMINA EL ESCENARIO DE LIQUIDACIÓN
        [HttpPost]
        public async Task<IActionResult> ValidarComprobacion(int id, string comentarios)
        {
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                            .ThenInclude(e => e.JefeDirecto)
                    .Include(c => c.EstadoComprobacion)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (comprobacion == null)
                {
                    return Json(new { success = false, message = "Comprobación no encontrada" });
                }

                decimal totalGastos = comprobacion.TotalGastosComprobados ?? 0;
                decimal totalAnticipo = comprobacion.TotalAnticipo ?? 0;
                decimal diferencia = totalAnticipo - totalGastos;

                int nuevoEstadoId;
                string escenario;
                string mensajeEstado;
                bool requiereJP = false;

                if (Math.Abs(diferencia) < 0.01m)
                {
                    escenario = "SALDADA";
                    nuevoEstadoId = 4;
                    mensajeEstado = "Validación completada. La comprobación está saldada.";
                }
                else if (diferencia > 0)
                {
                    escenario = "REPOSICION_EMPRESA";

                    if (comprobacion.SolicitudViaje.Empleado?.JefeDirecto != null)
                    {
                        requiereJP = true;
                        nuevoEstadoId = 10;
                        mensajeEstado = $"Validación completada. Enviada al Jefe de Proceso para revisión. Diferencia: {diferencia:C}";
                    }
                    else
                    {
                        nuevoEstadoId = 5;
                        mensajeEstado = $"Validación completada. Listo para procesar pago de {diferencia:C}";
                    }
                }
                else
                {
                    escenario = "REPOSICION_COLABORADOR";
                    diferencia = Math.Abs(diferencia);

                    if (comprobacion.SolicitudViaje.Empleado?.JefeDirecto != null)
                    {
                        requiereJP = true;
                        nuevoEstadoId = 10;
                        mensajeEstado = $"Validación completada. Enviada al Jefe de Proceso para revisión. Diferencia: {diferencia:C}";
                    }
                    else
                    {
                        nuevoEstadoId = 6;
                        mensajeEstado = $"Validación completada. Listo para procesar reintegro de {diferencia:C}";
                    }
                }

                var estadoNuevo = await _context.EstadosComprobacion
                    .FirstOrDefaultAsync(e => e.Id == nuevoEstadoId);

                if (estadoNuevo == null)
                {
                    return Json(new { success = false, message = "Estado no encontrado" });
                }

                comprobacion.EstadoComprobacionId = nuevoEstadoId;
                comprobacion.EstadoComprobacion = estadoNuevo;
                comprobacion.EscenarioLiquidacion = escenario;
                comprobacion.Diferencia = diferencia;
                comprobacion.RequiereAprobacionJefe = requiereJP;
                comprobacion.ComentariosFinanzas = (comprobacion.ComentariosFinanzas ?? "") +
                                                  $"\n--- VALIDADA POR FINANZAS ({DateTime.Now:dd/MM/yyyy HH:mm}) ---\n" +
                                                  $"Comentarios: {comentarios}\n" +
                                                  $"Requiere JP: {(requiereJP ? "SÍ" : "NO")}";
                comprobacion.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                await EnviarCorreoValidacionEmpleado(comprobacion, comentarios, diferencia, escenario, requiereJP);

                if (requiereJP)
                {
                    await EnviarCorreoRevisionJP(comprobacion, comentarios, diferencia, escenario);

                    var jefe = comprobacion.SolicitudViaje.Empleado.JefeDirecto;
                    await CrearNotificacion(
                        jefe.Id,
                        $"Revisión Pendiente - {comprobacion.CodigoComprobacion}",
                        $"Comprobación de {comprobacion.SolicitudViaje.Empleado.Nombre} requiere revisión.\n" +
                        $"Escenario: {escenario}\nDiferencia: {diferencia:C}",
                        "REVISION_JP_PENDIENTE",
                        comprobacion.Id
                    );
                }
                else
                {
                    await EnviarCorreoProcesarPago(comprobacion, comentarios, diferencia, escenario);

                    var finanzasUsers = await _context.Empleados
                        .Include(e => e.Rol)
                        .Where(e => e.Rol.Codigo == "FINANZAS" && e.Activo == true)
                        .ToListAsync();

                    foreach (var finanzas in finanzasUsers)
                    {
                        await CrearNotificacion(
                            finanzas.Id,
                            $"Pago/Reintegro Listo - {comprobacion.CodigoComprobacion}",
                            $"Comprobación lista para procesar {escenario}. Diferencia: {diferencia:C}",
                            "PAGO_LISTO_PROCESAR",
                            comprobacion.Id
                        );
                    }
                }

                return Json(new
                {
                    success = true,
                    message = mensajeEstado,
                    estado = nuevoEstadoId,
                    escenario = escenario,
                    diferencia = diferencia.ToString("C"),
                    requiereRevisionJP = requiereJP
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // ============================================
        // SECCIÓN REVISIÓN DE CORRECCIONES

        // REVISA CORRECCIONES REALIZADAS POR EMPLEADOS
        [Authorize(Roles = "FINANZAS,ADMIN")]
        public async Task<IActionResult> RevisarCorreccion(int id)
        {
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.EstadoComprobacion)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.CategoriaGasto)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.Factura)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.EstadoGasto)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (comprobacion == null)
                {
                    TempData["Error"] = "Comprobación no encontrada";
                    return RedirectToAction(nameof(Facturas));
                }

                var gastosDevueltos = comprobacion.SolicitudViaje.GastosReales
                    .Where(g => g.EstadoGasto?.Codigo == "DEVUELTO_CORRECCION")
                    .ToList();

                var gastosCorregidos = comprobacion.SolicitudViaje.GastosReales
                    .Where(g => g.EstadoGasto?.Codigo == "APROBADO")
                    .ToList();

                var todosGastos = gastosDevueltos.Concat(gastosCorregidos).ToList();

                int totalArchivosEsperados = todosGastos.Count * 2;
                int archivosCorregidosCount = 0;

                foreach (var gasto in todosGastos)
                {
                    var pdfCorregidoPath = Path.Combine(_environment.WebRootPath,
                        "facturas", comprobacion.Id.ToString(), gasto.Id.ToString(), "pdf", "corregido");

                    var xmlCorregidoPath = Path.Combine(_environment.WebRootPath,
                        "facturas", comprobacion.Id.ToString(), gasto.Id.ToString(), "xml", "corregido");

                    bool tienePdfCorregido = Directory.Exists(pdfCorregidoPath) &&
                        Directory.GetFiles(pdfCorregidoPath).Any();
                    bool tieneXmlCorregido = Directory.Exists(xmlCorregidoPath) &&
                        Directory.GetFiles(xmlCorregidoPath).Any();

                    if (tienePdfCorregido) archivosCorregidosCount++;
                    if (tieneXmlCorregido) archivosCorregidosCount++;

                    if (gasto.EstadoGasto?.Codigo == "APROBADO")
                    {
                        if (!tienePdfCorregido) archivosCorregidosCount++;
                        if (!tieneXmlCorregido) archivosCorregidosCount++;
                    }
                }

                decimal porcentajeCompletitud = 0;
                if (totalArchivosEsperados > 0)
                {
                    porcentajeCompletitud = (decimal)archivosCorregidosCount / totalArchivosEsperados * 100;
                }

                string accionRecomendada = "OBSERVAR_MENORES";

                if (porcentajeCompletitud >= 100)
                {
                    accionRecomendada = "APROBAR_CORRECCION";
                }
                else if (porcentajeCompletitud >= 70)
                {
                    accionRecomendada = "OBSERVAR_MENORES";
                }
                else if (porcentajeCompletitud > 0)
                {
                    accionRecomendada = "DEVOLVER_CORRECCION";
                }
                else
                {
                    accionRecomendada = "DEVOLVER_CORRECCION";
                }

                var viewModel = new RevisionCorreccionViewModel
                {
                    ComprobacionId = comprobacion.Id,
                    CodigoComprobacion = comprobacion.CodigoComprobacion,
                    ComentariosFinanzas = comprobacion.ComentariosFinanzas ?? "",
                    NombreEmpleado = $"{comprobacion.SolicitudViaje.Empleado.Nombre} {comprobacion.SolicitudViaje.Empleado.Apellidos}",
                    EmpleadoNombre = $"{comprobacion.SolicitudViaje.Empleado.Nombre} {comprobacion.SolicitudViaje.Empleado.Apellidos}",
                    EmpleadoEmail = comprobacion.SolicitudViaje.Empleado.Email,
                    TotalComprobacion = comprobacion.TotalGastosComprobados ?? 0,
                    TotalAnticipo = comprobacion.TotalAnticipo ?? 0,
                    Diferencia = comprobacion.Diferencia ?? 0,
                    EstadoActual = comprobacion.EstadoComprobacion?.Codigo ?? "Pendiente",
                    EstadoComprobacionId = comprobacion.EstadoComprobacionId,
                    FechaCorreccion = comprobacion.UpdatedAt ?? comprobacion.CreatedAt ?? DateTime.Now,
                    TotalGastos = todosGastos.Count,
                    GastosConXmlValido = todosGastos.Count(g =>
                          !string.IsNullOrEmpty(g.Factura?.ArchivoXmlUrl) && g.EstadoGasto?.Codigo == "APROBADO"),
                    GastosConPdfValido = todosGastos.Count(g =>
                        !string.IsNullOrEmpty(g.Factura?.ArchivoPdfUrl)),
                    AccionRecomendada = accionRecomendada,
                    PorcentajeCompletitud = porcentajeCompletitud,
                    GastosDevueltos = todosGastos.Select(g => new GastoCorreccionViewModel
                    {
                        GastoId = g.Id,
                        Concepto = g.Concepto,
                        Monto = g.Monto,
                        Categoria = g.CategoriaGasto?.Nombre ?? "N/A",
                        FechaGasto = g.FechaGasto.ToDateTime(TimeOnly.MinValue),
                        Proveedor = g.Proveedor,
                        ComentarioFinanzas = g.EstadoGasto?.Codigo == "DEVUELTO_CORRECCION"
                            ? "Pendiente de corrección"
                            : "Corregido por empleado",
                        FacturaPDFActual = g.Factura?.ArchivoPdfUrl,
                        FacturaXMLActual = g.Factura?.ArchivoXmlUrl,
                        EstadoGasto = g.EstadoGasto?.Nombre ?? "Pendiente",
                        EstadoGastoCodigo = g.EstadoGasto?.Codigo ?? "PENDIENTE",
                        TieneCorrecciones = !string.IsNullOrEmpty(GetArchivoCorregido(comprobacion.Id, g.Id, "pdf")) ||
                                           !string.IsNullOrEmpty(GetArchivoCorregido(comprobacion.Id, g.Id, "xml")) ||
                                           g.EstadoGasto?.Codigo == "APROBADO",
                        FacturaPDFCorregido = GetArchivoCorregido(comprobacion.Id, g.Id, "pdf"),
                        FacturaXMLCorregido = GetArchivoCorregido(comprobacion.Id, g.Id, "xml")
                    }).ToList()
                };

                return View("RevisarCorreccion", viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar la revisión: " + ex.Message;
                return RedirectToAction(nameof(Facturas));
            }
        }

        // APROBA CORRECCIONES REALIZADAS POR EL EMPLEADO
        [HttpPost]
        [Authorize(Roles = "FINANZAS,ADMIN")]
        public async Task<IActionResult> AprobarCorreccion(int id, string comentarios)
        {
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.EstadoGasto)
                    .Include(c => c.EstadoComprobacion)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (comprobacion == null)
                {
                    return Json(new ProcesarCorreccionResponse
                    {
                        Success = false,
                        Message = "Comprobación no encontrada"
                    });
                }

                var estadoEnProceso = await _context.EstadosComprobacion
                    .FirstOrDefaultAsync(e => e.Codigo == "EN_PROCESO");

                if (estadoEnProceso == null)
                {
                    estadoEnProceso = new EstadosComprobacion
                    {
                        Descripcion = "Comprobacion en proceso",
                        Codigo = "EN_PROCESO"
                    };
                    _context.EstadosComprobacion.Add(estadoEnProceso);
                    await _context.SaveChangesAsync();
                }

                if (comprobacion.EstadoComprobacion?.Codigo == "CON_CORRECCIONES_PENDIENTES")
                {
                    comprobacion.EstadoComprobacionId = estadoEnProceso.Id;
                }

                comprobacion.ComentariosFinanzas = comentarios;
                comprobacion.UpdatedAt = DateTime.Now;

                var gastosParaAprobar = comprobacion.SolicitudViaje.GastosReales
                    .Where(g => g.EstadoGasto?.Codigo == "DEVUELTO_CORRECCION")
                    .ToList();

                if (gastosParaAprobar.Any())
                {
                    var estadoGastoAprobado = await _context.EstadosGastos
                        .FirstOrDefaultAsync(e => e.Codigo == "APROBADO");

                    if (estadoGastoAprobado == null)
                    {
                        return Json(new ProcesarCorreccionResponse
                        {
                            Success = false,
                            Message = "Estado de gasto 'APROBADO' no encontrado en el sistema"
                        });
                    }

                    foreach (var gasto in gastosParaAprobar)
                    {
                        gasto.EstadoGastoId = estadoGastoAprobado.Id;
                        gasto.CreatedAt = DateTime.Now;
                        MoverArchivosCorregidosAPrincipal(comprobacion.Id, gasto.Id);
                    }
                }

                await _context.SaveChangesAsync();
                await EnviarCorreoCorreccionAprobada(comprobacion, comentarios, gastosParaAprobar.Count);

                return Json(new ProcesarCorreccionResponse
                {
                    Success = true,
                    Message = $"Correcciones aprobadas exitosamente. {gastosParaAprobar.Count} gastos actualizados.",
                    ComprobacionId = comprobacion.Id,
                    CodigoComprobacion = comprobacion.CodigoComprobacion,
                    AccionRealizada = "APROBAR_CORRECCION",
                    GastosAfectados = gastosParaAprobar.Select(g => g.Id).ToList(),
                    RedirectUrl = Url.Action("Facturas", "Finanzas")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al aprobar correcciones");
                return Json(new ProcesarCorreccionResponse
                {
                    Success = false,
                    Message = "Error interno al aprobar correcciones: " + ex.Message
                });
            }
        }

        // DEVUELVE CORRECCIONES AL EMPLEADO PARA NUEVA REVISIÓN
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "FINANZAS,ADMIN")]
        public async Task<IActionResult> DevolverCorreccion([FromBody] DevolucionGastosViewModel model)
        {
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.EstadoGasto)
                    .FirstOrDefaultAsync(c => c.Id == model.ComprobacionId);

                if (comprobacion == null)
                {
                    return Json(new { success = false, message = "Comprobación no encontrada" });
                }

                var estadoComprobacionPendiente = await _context.EstadosComprobacion
                    .FirstOrDefaultAsync(e => e.Id == 1);

                var estadoGastoDevuelto = await _context.EstadosGastos
                    .FirstOrDefaultAsync(e => e.Codigo == "DEVUELTO_CORRECCION");

                if (estadoComprobacionPendiente == null || estadoGastoDevuelto == null)
                {
                    return Json(new { success = false, message = "Estados no configurados en el sistema" });
                }

                var gastosAfectados = new List<int>();
                foreach (var gasto in comprobacion.SolicitudViaje.GastosReales)
                {
                    if (model.GastosSeleccionados.Contains(gasto.Id))
                    {
                        gasto.EstadoGastoId = estadoGastoDevuelto.Id;
                        gastosAfectados.Add(gasto.Id);
                    }
                }

                if (!gastosAfectados.Any())
                {
                    return Json(new { success = false, message = "No se seleccionaron gastos para devolver" });
                }

                comprobacion.EstadoComprobacionId = estadoComprobacionPendiente.Id;
                comprobacion.ComentariosFinanzas = (comprobacion.ComentariosFinanzas ?? "") +
                    $"\n--- CORRECCIÓN DEVUELTA POR FINANZAS ({DateTime.Now:dd/MM/yyyy HH:mm}) ---\n" +
                    $"Devuelta por: {User.Identity.Name}\n" +
                    $"Tipo de corrección: {model.TipoCorreccion}\n" +
                    $"Comentarios: {model.Comentarios}\n" +
                    $"Gastos devueltos: {gastosAfectados.Count}\n" +
                    $"Fecha límite para corrección: {model.FechaLimite:dd/MM/yyyy}";
                comprobacion.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                await EnviarCorreoCorreccionDevuelta(comprobacion, model);

                return Json(new ProcesarCorreccionResponse
                {
                    Success = true,
                    Message = $"Corrección devuelta al empleado. {gastosAfectados.Count} gastos requieren corrección.",
                    ComprobacionId = comprobacion.Id,
                    CodigoComprobacion = comprobacion.CodigoComprobacion,
                    NuevoEstado = comprobacion.EstadoComprobacionId,
                    AccionRealizada = "DEVOLVER_CORRECCION",
                    GastosAfectados = gastosAfectados,
                    RedirectUrl = Url.Action("Facturas", "Finanzas")
                });
            }
            catch (Exception ex)
            {
                return Json(new ProcesarCorreccionResponse
                {
                    Success = false,
                    Message = "Error al devolver la corrección: " + ex.Message
                });
            }
        }

        // ============================================
        // SECCIÓN REVISIÓN JP (JEFE DE PROCESO)

        // MUESTRA COMPROBACIONES PENDIENTES DE REVISIÓN POR JP
        [Authorize(Roles = "JP,ADMIN")]
        public async Task<IActionResult> RevisionActividadesJP()
        {
            try
            {
                var comprobaciones = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.EstadoComprobacion)
                    .Where(c => c.EstadoComprobacionId == 10)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                return View(comprobaciones);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar comprobaciones: " + ex.Message;
                return View(new List<ComprobacionesViaje>());
            }
        }

        // CARGA DETALLES DE UNA COMPROBACIÓN PARA REVISIÓN JP
        [Authorize(Roles = "JP,ADMIN")]
        public async Task<IActionResult> CargarRevisionJP(int id)
        {
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.EstadoComprobacion)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.CategoriaGasto)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.Factura)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (comprobacion == null)
                {
                    return Content("<div class='alert alert-danger'>Comprobación no encontrada</div>");
                }

                return PartialView("~/Views/Finanzas/Shared/_RevisionJPDetalle.cshtml", comprobacion);
            }
            catch (Exception ex)
            {
                return Content($"<div class='alert alert-danger'>Error al cargar: {ex.Message}</div>");
            }
        }

        // APROBA UNA COMPROBACIÓN REVISADA POR JP
        [HttpPost]
        [Authorize(Roles = "JP,ADMIN")]
        public async Task<IActionResult> AprobarRevisionJP(int id, string comentarios = "")
        {
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.EstadoComprobacion)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (comprobacion == null || comprobacion.EstadoComprobacionId != 10)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Comprobación no encontrada o no está en estado de revisión por JP"
                    });
                }

                int nuevoEstadoId;
                if (comprobacion.EscenarioLiquidacion == "REPOSICION_EMPRESA")
                {
                    nuevoEstadoId = 5;
                }
                else if (comprobacion.EscenarioLiquidacion == "REPOSICION_COLABORADOR")
                {
                    nuevoEstadoId = 6;
                }
                else
                {
                    nuevoEstadoId = 5;
                }

                var estadoReposicion = await _context.EstadosComprobacion
                    .FirstOrDefaultAsync(e => e.Id == nuevoEstadoId);

                if (estadoReposicion == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Estado de reposición no encontrado"
                    });
                }

                var emailUsuario = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
                var jefe = await _context.Empleados
                    .FirstOrDefaultAsync(e => e.Email == emailUsuario);

                if (jefe == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "No se encontró información del Jefe de Proceso."
                    });
                }

                comprobacion.EstadoComprobacionId = nuevoEstadoId;
                comprobacion.EstadoComprobacion = estadoReposicion;
                comprobacion.AprobacionJefeId = jefe.Id;
                comprobacion.RequiereAprobacionJefe = false;
                comprobacion.ComentariosFinanzas = (comprobacion.ComentariosFinanzas ?? "") +
                                                  $"\n--- APROBADO POR JP ({DateTime.Now:dd/MM/yyyy HH:mm}) ---\n" +
                                                  $"Aprobado por: {jefe.Nombre} {jefe.Apellidos}\n" +
                                                  $"Comentarios: {comentarios}\n" +
                                                  $"Nuevo estado: {estadoReposicion.Codigo}";
                comprobacion.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                await EnviarCorreoAprobacionFinanzas(comprobacion, jefe, comentarios);

                var finanzasUsers = await _context.Empleados
                    .Include(e => e.Rol)
                    .Where(e => e.Rol.Codigo == "FINANZAS" && e.Activo == true)
                    .ToListAsync();

                foreach (var finanzas in finanzasUsers)
                {
                    await CrearNotificacion(
                        finanzas.Id,
                        $"Comprobación Aprobada por JP - {comprobacion.CodigoComprobacion}",
                        $"El JP {jefe.Nombre} ha aprobado las actividades. La comprobación está lista para procesar el " +
                        $"{comprobacion.EscenarioLiquidacion} de {comprobacion.Diferencia:C}.\n" +
                        $"Estado actual: {estadoReposicion.Codigo}\n" +
                        $"Acción requerida: Procesar desde 'Autorizaciones de Pago'.",
                        "PAGO_LISTO_PROCESAR",
                        comprobacion.Id
                    );
                }

                return Json(new
                {
                    success = true,
                    message = $"Actividades aprobadas por JP. La comprobación está ahora en estado {estadoReposicion.Codigo} y lista para que Finanzas procese el pago/reintegro.",
                    nuevoEstado = nuevoEstadoId,
                    estadoNombre = estadoReposicion.Codigo,
                    diferencia = comprobacion.Diferencia?.ToString("C") ?? "$0.00",
                    escenario = comprobacion.EscenarioLiquidacion
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error al aprobar la comprobación: " + ex.Message
                });
            }
        }

        // RECHAZA UNA COMPROBACIÓN REVISADA POR JP
        [HttpPost]
        [Authorize(Roles = "JP,ADMIN")]
        public async Task<IActionResult> RechazarRevisionJP(int id, string comentarios, string tipoCorreccion)
        {
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.EstadoComprobacion)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (comprobacion == null || comprobacion.EstadoComprobacionId != 10)
                {
                    return Json(new { success = false, message = "Comprobación no encontrada o no está en revisión JP." });
                }

                var emailUsuario = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
                if (string.IsNullOrEmpty(emailUsuario))
                {
                    return Json(new { success = false, message = "No se pudo obtener el email del usuario actual." });
                }

                var jefe = await _context.Empleados
                    .FirstOrDefaultAsync(e => e.Email == emailUsuario);

                if (jefe == null)
                {
                    var userName = User.Identity?.Name;
                    jefe = await _context.Empleados
                        .FirstOrDefaultAsync(e => e.Email == userName);
                }

                if (jefe == null)
                {
                    return Json(new { success = false, message = "No se encontró información del Jefe de Proceso." });
                }

                var estadoPendiente = await _context.EstadosComprobacion
                    .FirstOrDefaultAsync(e => e.Id == 1);

                if (estadoPendiente == null)
                {
                    return Json(new { success = false, message = "Estado PENDIENTE no encontrado." });
                }

                comprobacion.EstadoComprobacionId = estadoPendiente.Id;
                comprobacion.EstadoComprobacion = estadoPendiente;
                comprobacion.AprobacionJefeId = jefe.Id;
                comprobacion.RequiereAprobacionJefe = false;
                comprobacion.ComentariosFinanzas = (comprobacion.ComentariosFinanzas ?? "") +
                                                  $"\n--- RECHAZADA POR JP ({DateTime.Now:dd/MM/yyyy HH:mm}) ---\n" +
                                                  $"Rechazada por: {jefe.Nombre} {jefe.Apellidos}\n" +
                                                  $"Tipo de corrección: {tipoCorreccion}\n" +
                                                  $"Comentarios: {comentarios}";
                comprobacion.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                await EnviarCorreoRechazoEmpleado(comprobacion, jefe, comentarios, tipoCorreccion);

                await CrearNotificacion(
                    comprobacion.SolicitudViaje.EmpleadoId,
                    $"Correcciones Requeridas - {comprobacion.CodigoComprobacion}",
                    $"El Jefe de Proceso ha solicitado correcciones en tu comprobación:\n\n" +
                    $"Tipo: {tipoCorreccion}\n" +
                    $"Comentarios: {comentarios}\n\n" +
                    $"Por favor, corrige y reenvía la comprobación.",
                    "CORRECCIONES_REQUERIDAS",
                    comprobacion.Id
                );

                return Json(new { success = true, message = "Correcciones enviadas al empleado." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // ============================================
        // SECCIÓN AUTORIZACIONES DE PAGO
        // ============================================

        // MUESTRA AUTORIZACIONES DE PAGO PENDIENTES
        [Authorize(Roles = "FINANZAS,ADMIN")]
        public async Task<IActionResult> AutorizacionesPago()
        {
            try
            {
                var comprobaciones = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                            .ThenInclude(e => e.JefeDirecto)
                    .Include(c => c.EstadoComprobacion)
                    .Include(c => c.AprobacionJefe)
                    .Where(c => c.EstadoComprobacionId == 5 ||
                               c.EstadoComprobacionId == 6 ||
                               c.EstadoComprobacionId == 11)
                    .OrderByDescending(c => c.UpdatedAt)
                    .ToListAsync();

                ViewBag.TotalPagar = comprobaciones
                    .Where(c => c.EstadoComprobacionId == 5 || c.EstadoComprobacionId == 11)
                    .Sum(c => c.Diferencia ?? 0);

                ViewBag.TotalReintegrar = comprobaciones
                    .Where(c => c.EstadoComprobacionId == 6)
                    .Sum(c => c.Diferencia ?? 0);

                ViewBag.TotalComprobaciones = comprobaciones.Count;

                return View(comprobaciones);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar las autorizaciones de pago.";
                return View(new List<ComprobacionesViaje>());
            }
        }

        // CARGA DETALLE DE UNA AUTORIZACIÓN DE PAGO
        [Authorize(Roles = "FINANZAS,ADMIN")]
        public async Task<IActionResult> CargarDetalleAutorizacion(int id)
        {
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.EstadoComprobacion)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.CategoriaGasto)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.Factura)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (comprobacion == null)
                {
                    return Content("<div class='alert alert-danger'>Comprobación no encontrada</div>");
                }

                return PartialView("~/Views/Finanzas/Shared/_DetalleAutorizacion.cshtml", comprobacion);
            }
            catch (Exception ex)
            {
                return Content($"<div class='alert alert-danger'>Error al cargar: {ex.Message}</div>");
            }
        }

        // AUTORIZA PAGO INDIVIDUAL
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "FINANZAS,ADMIN")]
        public async Task<IActionResult> AutorizarPagoIndividual(int id, string tipoAccion)
        {
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.EstadoComprobacion)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (comprobacion == null)
                {
                    return Json(new { success = false, message = "Comprobación no encontrada." });
                }

                if (comprobacion.EstadoComprobacionId != 5 &&
                    comprobacion.EstadoComprobacionId != 6 &&
                    comprobacion.EstadoComprobacionId != 11)
                {
                    return Json(new { success = false, message = "La comprobación no está en un estado válido para autorizar pago." });
                }

                var estadoSaldada = await _context.EstadosComprobacion
                    .FirstOrDefaultAsync(e => e.Id == 4);

                if (estadoSaldada == null)
                {
                    return Json(new { success = false, message = "Estado 'SALDADA' no configurado." });
                }

                var auditoria = new AuditoriaSistema
                {
                    EmpleadoId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value != null
                        ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value)
                        : 1,
                    Entidad = "ComprobacionesViaje",
                    EntidadId = comprobacion.Id,
                    Accion = tipoAccion,
                    ValoresAnteriores = JsonSerializer.Serialize(new
                    {
                        EstadoAnterior = comprobacion.EstadoComprobacion?.Codigo,
                        Escenario = comprobacion.EscenarioLiquidacion,
                        Diferencia = comprobacion.Diferencia
                    }),
                    ValoresNuevos = JsonSerializer.Serialize(new
                    {
                        EstadoNuevo = "SALDADA",
                        FechaCierre = DateTime.Now
                    }),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers["User-Agent"].ToString()
                };

                comprobacion.EstadoComprobacionId = estadoSaldada.Id;
                comprobacion.FechaCierre = DateTime.Now;
                comprobacion.UpdatedAt = DateTime.Now;

                _context.AuditoriaSistema.Add(auditoria);
                await _context.SaveChangesAsync();

                await EnviarCorreoComprobacionSaldada(comprobacion, $"Pago autorizado. Acción: {tipoAccion}");

                return Json(new
                {
                    success = true,
                    message = $"Comprobación {tipoAccion.ToLower()} autorizada exitosamente.",
                    tipoAccion = tipoAccion,
                    monto = comprobacion.Diferencia,
                    nuevoEstado = "SALDADA"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // AUTORIZA MÚLTIPLES PAGOS MASIVAMENTE
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "FINANZAS,ADMIN")]
        public async Task<IActionResult> AutorizarPagosMasivo([FromBody] List<int> ids)
        {
            try
            {
                if (ids == null || ids.Count == 0)
                {
                    return Json(new { success = false, message = "No hay comprobaciones seleccionadas." });
                }

                var comprobaciones = await _context.ComprobacionesViaje
                    .Include(c => c.EstadoComprobacion)
                    .Where(c => ids.Contains(c.Id) && c.EstadoComprobacion.Codigo == "APROBADA_FINANZAS")
                    .ToListAsync();

                if (!comprobaciones.Any())
                {
                    return Json(new { success = false, message = "No hay comprobaciones válidas para autorizar." });
                }

                var estadoSaldada = await _context.EstadosComprobacion
                    .FirstOrDefaultAsync(e => e.Codigo == "SALDADA");

                if (estadoSaldada == null)
                {
                    return Json(new { success = false, message = "Estado 'SALDADA' no configurado." });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var empleadoId = userId != null ? int.Parse(userId) : 1;

                int autorizadas = 0;
                foreach (var comprobacion in comprobaciones)
                {
                    var auditoria = new AuditoriaSistema
                    {
                        EmpleadoId = empleadoId,
                        Entidad = "ComprobacionesViaje",
                        EntidadId = comprobacion.Id,
                        Accion = "AUTORIZACION_MASIVA",
                        ValoresAnteriores = JsonSerializer.Serialize(new
                        {
                            EstadoAnterior = comprobacion.EstadoComprobacion?.Codigo,
                            Escenario = comprobacion.EscenarioLiquidacion
                        }),
                        ValoresNuevos = JsonSerializer.Serialize(new
                        {
                            EstadoNuevo = "SALDADA",
                            FechaCierre = DateTime.Now
                        }),
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        UserAgent = Request.Headers["User-Agent"].ToString()
                    };

                    comprobacion.EstadoComprobacionId = estadoSaldada.Id;
                    comprobacion.FechaCierre = DateTime.Now;
                    comprobacion.UpdatedAt = DateTime.Now;

                    _context.AuditoriaSistema.Add(auditoria);
                    autorizadas++;
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Se autorizaron {autorizadas} comprobaciones exitosamente.",
                    cantidadAutorizada = autorizadas
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // OBTIENE HISTORIAL DE REVISIONES POR JP
        [Authorize(Roles = "JP,ADMIN")]
        public async Task<IActionResult> ObtenerHistorialRevisiones()
        {
            try
            {
                var emailUsuario = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;

                if (string.IsNullOrEmpty(emailUsuario))
                {
                    return Content("<div class='alert alert-warning'>No se pudo identificar al usuario.</div>");
                }

                var jefe = await _context.Empleados
                    .FirstOrDefaultAsync(e => e.Email == emailUsuario);

                if (jefe == null)
                {
                    var userName = User.Identity?.Name;
                    jefe = await _context.Empleados
                        .FirstOrDefaultAsync(e => e.Email == userName);
                }

                if (jefe == null)
                {
                    return Content("<div class='alert alert-warning'>No se encontró información del Jefe de Proceso.</div>");
                }

                var comprobaciones = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.EstadoComprobacion)
                    .Where(c => c.AprobacionJefeId == jefe.Id)
                    .OrderByDescending(c => c.UpdatedAt)
                    .Take(10)
                    .ToListAsync();

                if (!comprobaciones.Any())
                {
                    return Content("<div class='text-center py-3'>No hay revisiones previas.</div>");
                }

                var historialHtml = "";
                foreach (var comp in comprobaciones)
                {
                    var fecha = comp.UpdatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "Fecha no disponible";
                    historialHtml += $@"
                    <div class='timeline-item'>
                        <div class='timeline-date'>{fecha}</div>
                        <div class='timeline-content'>
                            <strong>{comp.SolicitudViaje.Empleado.Nombre} {comp.SolicitudViaje.Empleado.Apellidos}</strong><br>
                            <small>Comprobación: {comp.CodigoComprobacion}</small><br>
                            <span class='badge bg-info'>{comp.EstadoComprobacion.Codigo}</span>
                        </div>
                    </div>";
                }

                return Content(historialHtml);
            }
            catch (Exception ex)
            {
                return Content($"<div class='alert alert-danger'>Error: {ex.Message}</div>");
            }
        }

        // ============================================
        // MÉTODOS DE GESTIÓN DE COMPROBACIONES
        // ============================================

        // DEVUELVE GASTOS ESPECÍFICOS PARA CORRECCIÓN
        public async Task<IActionResult> DevolverGastos(int id)
        {
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.CategoriaGasto)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.Factura)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.EstadoGasto)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (comprobacion == null)
                {
                    return Json(new { success = false, message = "Comprobación no encontrada" });
                }

                var gastos = comprobacion.SolicitudViaje.GastosReales.Select(g => new
                {
                    GastoId = g.Id,
                    Concepto = g.Concepto,
                    Monto = g.Monto,
                    Categoria = g.CategoriaGasto?.Nombre ?? "N/A",
                    FechaGasto = g.FechaGasto.ToDateTime(TimeOnly.MinValue).ToString("dd/MM/yyyy"),
                    Proveedor = g.Proveedor ?? "N/A",
                    TieneXML = !string.IsNullOrEmpty(g.Factura?.ArchivoXmlUrl),
                    TienePDF = !string.IsNullOrEmpty(g.Factura?.ArchivoPdfUrl),
                    EstadoActual = g.EstadoGasto?.Nombre ?? "Pendiente",
                    EstadoGastoCodigo = g.EstadoGasto?.Codigo ?? "PENDIENTE"
                }).ToList();

                return Json(new
                {
                    success = true,
                    comprobacionId = comprobacion.Id,
                    codigoComprobacion = comprobacion.CodigoComprobacion,
                    empleadoNombre = $"{comprobacion.SolicitudViaje.Empleado.Nombre} {comprobacion.SolicitudViaje.Empleado.Apellidos}",
                    empleadoEmail = comprobacion.SolicitudViaje.Empleado.Email,
                    totalComprobacion = comprobacion.TotalGastosComprobados ?? 0,
                    totalAnticipo = comprobacion.TotalAnticipo ?? 0,
                    diferencia = comprobacion.Diferencia ?? 0,
                    gastos = gastos
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al cargar la comprobación: " + ex.Message });
            }
        }

        // PROCESA DEVOLUCIÓN DE GASTOS
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcesarDevolucionGastos([FromBody] ProcesarDevolucionRequest model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                    .FirstOrDefaultAsync(c => c.Id == model.ComprobacionId);

                if (comprobacion == null)
                {
                    return Json(new { success = false, message = "Comprobación no encontrada" });
                }

                var estadoGastoAprobado = await _context.EstadosGastos.FirstAsync(e => e.Codigo == "APROBADO");
                var estadoGastoDevuelto = await _context.EstadosGastos.FirstAsync(e => e.Codigo == "DEVUELTO_CORRECCION");

                var gastosSeleccionados = model.GastosSeleccionados ?? new List<int>();

                foreach (var gasto in comprobacion.SolicitudViaje.GastosReales)
                {
                    if (gastosSeleccionados.Contains(gasto.Id))
                    {
                        gasto.EstadoGastoId = estadoGastoDevuelto.Id;
                    }
                    else
                    {
                        gasto.EstadoGastoId = estadoGastoAprobado.Id;
                    }
                }

                if (gastosSeleccionados.Any())
                {
                    var estadoComprobacionCorrecciones = await _context.EstadosComprobacion
                        .FirstAsync(e => e.Codigo == "CON_CORRECCIONES_PENDIENTES");
                    comprobacion.EstadoComprobacionId = estadoComprobacionCorrecciones.Id;
                }
                else
                {
                    var estadoComprobacionParcial = await _context.EstadosComprobacion
                        .FirstAsync(e => e.Codigo == "PARCIALMENTE_APROBADA");
                    comprobacion.EstadoComprobacionId = estadoComprobacionParcial.Id;
                }

                comprobacion.ComentariosFinanzas = model.Comentarios;
                comprobacion.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await EnviarCorreoGastosDevueltos(comprobacion.Id, model.Comentarios, gastosSeleccionados.Count);

                return Json(new
                {
                    success = true,
                    message = gastosSeleccionados.Any()
                        ? $"Comprobación devuelta para corrección. {gastosSeleccionados.Count} gastos requieren corrección."
                        : "Comprobación aprobada parcialmente. Todos los gastos fueron aprobados.",
                    comprobacionId = comprobacion.Id,
                    nuevoEstado = comprobacion.EstadoComprobacionId
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Error al procesar la devolución: " + ex.Message });
            }
        }

        // REABRE UNA COMPROBACIÓN PARA NUEVA CORRECCIÓN
        [HttpPost]
        public async Task<IActionResult> ReabrirComprobacion(int id, string comentarios)
        {
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (comprobacion == null)
                {
                    return Json(new { success = false, message = "Comprobación no encontrada" });
                }

                comprobacion.EstadoComprobacionId = 1;

                var estadoGastoDevuelto = await _context.EstadosGastos
                    .FirstOrDefaultAsync(e => e.Codigo == "DEVUELTO_CORRECCION");

                if (estadoGastoDevuelto != null)
                {
                    foreach (var gasto in comprobacion.SolicitudViaje.GastosReales)
                    {
                        gasto.EstadoGastoId = estadoGastoDevuelto.Id;
                    }
                }

                comprobacion.ComentariosFinanzas = (comprobacion.ComentariosFinanzas ?? "") +
                                                  $"\n--- REABIERTA POR FINANZAS ({DateTime.Now:dd/MM/yyyy HH:mm}) ---\n{comentarios}";
                comprobacion.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                await EnviarCorreoComprobacionReabierta(comprobacion.Id, comentarios);

                return Json(new
                {
                    success = true,
                    message = "Comprobación reabierta para corrección del empleado",
                    nuevoEstado = comprobacion.EstadoComprobacionId
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error al reabrir la comprobación: " + ex.Message
                });
            }
        }

        // RECHAZA COMPLETAMENTE UNA COMPROBACIÓN
        [HttpPost]
        public async Task<IActionResult> RechazarComprobacion(int id, string comentarios)
        {
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.Factura)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (comprobacion == null)
                {
                    return Json(new { success = false, message = "Comprobación no encontrada" });
                }

                var empleadoEmail = comprobacion.SolicitudViaje?.Empleado?.Email;
                var empleadoNombre = $"{comprobacion.SolicitudViaje?.Empleado?.Nombre} {comprobacion.SolicitudViaje?.Empleado?.Apellidos}";
                var codigoComprobacion = comprobacion.CodigoComprobacion;

                foreach (var gasto in comprobacion.SolicitudViaje.GastosReales)
                {
                    if (gasto.Factura != null)
                    {
                        if (!string.IsNullOrEmpty(gasto.Factura.ArchivoPdfUrl))
                        {
                            var pdfPath = Path.Combine(_environment.WebRootPath, gasto.Factura.ArchivoPdfUrl);
                            if (System.IO.File.Exists(pdfPath))
                                System.IO.File.Delete(pdfPath);
                        }
                        if (!string.IsNullOrEmpty(gasto.Factura.ArchivoXmlUrl))
                        {
                            var xmlPath = Path.Combine(_environment.WebRootPath, gasto.Factura.ArchivoXmlUrl);
                            if (System.IO.File.Exists(xmlPath))
                                System.IO.File.Delete(xmlPath);
                        }
                        _context.Facturas.Remove(gasto.Factura);
                    }
                }

                _context.ComprobacionesViaje.Remove(comprobacion);
                await _context.SaveChangesAsync();

                if (!string.IsNullOrEmpty(empleadoEmail))
                {
                    _queue.Enqueue(new ServicesNotificationItem
                    {
                        ToEmail = empleadoEmail,
                        Subject = $"Comprobación Rechazada - {codigoComprobacion}",
                        TemplateName = "/Views/Emails/ComprobacionRechazada.cshtml",
                        Model = new
                        {
                            EmpleadoNombre = empleadoNombre,
                            CodigoComprobacion = codigoComprobacion,
                            Comentarios = comentarios
                        }
                    });
                }

                return Json(new
                {
                    success = true,
                    message = "Comprobación rechazada y eliminada exitosamente"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error al rechazar la comprobación: " + ex.Message
                });
            }
        }

        // ============================================
        // MÉTODOS AUXILIARES PRIVADOS
        // ============================================

        // CARGA DATOS DE SOLICITUD EN VIEWMODEL
        private async Task CargarDatosSolicitudEnModeloAsync(CrearCotizacionViewModel model)
        {
            var solicitud = await _context.SolicitudesViajes
                .Include(s => s.Empleado)
                .FirstOrDefaultAsync(s => s.Id == model.SolicitudViajeId);

            if (solicitud != null)
            {
                model.CodigoSolicitud = solicitud.CodigoSolicitud;
                model.EmpleadoNombre = $"{solicitud.Empleado?.Nombre} {solicitud.Empleado?.Apellidos}";
                model.Destino = solicitud.Destino;
                model.Proyecto = solicitud.NombreProyecto;
                model.UbicacionBase = solicitud.Empleado?.UbicacionBase ?? "Puebla";
                model.FechaSalida = solicitud.FechaSalida;
                model.FechaRegreso = solicitud.FechaRegreso;
                model.HoraSalida = solicitud.HoraSalida.HasValue ?
                                  (TimeSpan?)solicitud.HoraSalida.Value.ToTimeSpan() : null;
                model.HoraRegreso = solicitud.HoraRegreso.HasValue ?
                                   (TimeSpan?)solicitud.HoraRegreso.Value.ToTimeSpan() : null;
                model.NumeroPersonas = solicitud.NumeroPersonas ?? 1;
                model.RequiereHospedaje = solicitud.RequiereHospedaje ?? false;
                model.NochesHospedaje = solicitud.NochesHospedaje ?? 0;
                model.MedioTraslado = solicitud.MedioTrasladoPrincipal ?? "Avión";
                model.RequiereTaxiDomicilio = solicitud.RequiereTaxiDomicilio ?? false;
                model.DireccionTaxiOrigen = solicitud.DireccionTaxiOrigen;
                model.DireccionTaxiDestino = solicitud.DireccionTaxiDestino;
            }
        }

        // OBTIENE ARCHIVO CORREGIDO DE CARPETA ESPECÍFICA
        private string GetArchivoCorregido(int comprobacionId, int gastoId, string tipo)
        {
            var basePath = Path.Combine(_environment.WebRootPath,
                "facturas", comprobacionId.ToString(), gastoId.ToString(), tipo, "corregido");

            if (Directory.Exists(basePath))
            {
                var files = Directory.GetFiles(basePath);
                if (files.Any())
                {
                    var latestFile = files.OrderByDescending(f => System.IO.File.GetCreationTime(f)).First();
                    var relativePath = latestFile.Replace(_environment.WebRootPath, "").Replace("\\", "/").TrimStart('/');
                    return relativePath;
                }
            }

            return null;
        }

        // MUEVE ARCHIVOS CORREGIDOS A CARPETA PRINCIPAL
        private void MoverArchivosCorregidosAPrincipal(int comprobacionId, int gastoId)
        {
            try
            {
                var basePath = Path.Combine(_environment.WebRootPath,
                    "facturas", comprobacionId.ToString(), gastoId.ToString());

                var pdfCorregidoPath = Path.Combine(basePath, "pdf", "corregido");
                var pdfPrincipalPath = Path.Combine(basePath, "pdf");

                if (Directory.Exists(pdfCorregidoPath) && Directory.Exists(pdfPrincipalPath))
                {
                    var archivosPdf = Directory.GetFiles(pdfCorregidoPath);
                    foreach (var archivo in archivosPdf)
                    {
                        var nombreArchivo = Path.GetFileName(archivo);
                        var destino = Path.Combine(pdfPrincipalPath, nombreArchivo);

                        if (System.IO.File.Exists(destino))
                        {
                            System.IO.File.Delete(destino);
                        }

                        System.IO.File.Move(archivo, destino);
                    }

                    if (!Directory.GetFiles(pdfCorregidoPath).Any())
                    {
                        Directory.Delete(pdfCorregidoPath);
                    }
                }

                var xmlCorregidoPath = Path.Combine(basePath, "xml", "corregido");
                var xmlPrincipalPath = Path.Combine(basePath, "xml");

                if (Directory.Exists(xmlCorregidoPath) && Directory.Exists(xmlPrincipalPath))
                {
                    var archivosXml = Directory.GetFiles(xmlCorregidoPath);
                    foreach (var archivo in archivosXml)
                    {
                        var nombreArchivo = Path.GetFileName(archivo);
                        var destino = Path.Combine(xmlPrincipalPath, nombreArchivo);

                        if (System.IO.File.Exists(destino))
                        {
                            System.IO.File.Delete(destino);
                        }

                        System.IO.File.Move(archivo, destino);
                    }

                    if (!Directory.GetFiles(xmlCorregidoPath).Any())
                    {
                        Directory.Delete(xmlCorregidoPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al mover archivos corregidos para comprobación {ComprobacionId}, gasto {GastoId}",
                    comprobacionId, gastoId);
            }
        }

        // ============================================
        // MÉTODOS DE ENVÍO DE CORREOS ELECTRÓNICOS
        // ============================================

        // ENVÍA CORREO DE VALIDACIÓN AL EMPLEADO
        private async Task EnviarCorreoValidacionEmpleado(ComprobacionesViaje comprobacion, string comentarios,
                                                  decimal diferencia, string escenario, bool requiereJP)
        {
            try
            {
                var empleado = comprobacion.SolicitudViaje.Empleado;

                if (empleado == null || string.IsNullOrEmpty(empleado.Email))
                {
                    Console.WriteLine("No se pudo enviar correo: empleado o email no encontrado");
                    return;
                }

                var url = $"{Request.Scheme}://{Request.Host}/Comprobaciones/Index";

                string mensajeProceso = "";
                if (requiereJP)
                {
                    var jefeNombre = comprobacion.SolicitudViaje.Empleado.JefeDirecto != null
                        ? $"{comprobacion.SolicitudViaje.Empleado.JefeDirecto.Nombre} {comprobacion.SolicitudViaje.Empleado.JefeDirecto.Apellidos}"
                        : "Jefe de Proceso";

                    mensajeProceso = $"Tu comprobación ha sido enviada a <strong>{jefeNombre}</strong> para revisión de actividades. " +
                                    $"Una vez que sea aprobada, Finanzas procederá con el proceso de pago/reintegro.";
                }
                else
                {
                    if (escenario == "REPOSICION_EMPRESA")
                    {
                        mensajeProceso = $"Finanzas procederá con el pago de <strong>{diferencia:C}</strong> a tu favor en los próximos días.";
                    }
                    else if (escenario == "REPOSICION_COLABORADOR")
                    {
                        mensajeProceso = $"Debes reintegrar a la empresa el monto de <strong>{diferencia:C}</strong>. " +
                                        $"Finanzas se pondrá en contacto contigo para coordinar el proceso.";
                    }
                    else
                    {
                        mensajeProceso = "Tu comprobación ha sido validada y está lista para el proceso de liquidación.";
                    }
                }

                _queue.Enqueue(new ServicesNotificationItem
                {
                    ToEmail = empleado.Email,
                    Subject = $"Comprobación Validada - {comprobacion.CodigoComprobacion}",
                    TemplateName = "/Views/Emails/ComprobacionValidada.cshtml",
                    Model = new
                    {
                        EmpleadoNombre = $"{empleado.Nombre} {empleado.Apellidos}",
                        CodigoComprobacion = comprobacion.CodigoComprobacion,
                        FechaProcesamiento = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                        TotalGastos = comprobacion.TotalGastosComprobados?.ToString("C") ?? "$0.00",
                        TotalAnticipo = comprobacion.TotalAnticipo?.ToString("C") ?? "$0.00",
                        Diferencia = (escenario == "REPOSICION_COLABORADOR" ? "-" : "") + diferencia.ToString("C"),
                        Comentarios = comentarios,
                        Url = url,
                        RequiereJP = requiereJP,
                        MensajeProceso = mensajeProceso,
                        Escenario = escenario
                    }
                });

                Console.WriteLine($"Correo enviado al empleado: {empleado.Email}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar correo al empleado: {ex.Message}");
            }
        }

        // ENVÍA CORREO DE REVISIÓN AL JEFE DE PROCESO
        private async Task EnviarCorreoRevisionJP(ComprobacionesViaje comprobacion, string comentarios, decimal diferencia, string escenario)
        {
            try
            {
                var empleado = comprobacion.SolicitudViaje.Empleado;
                var jefe = empleado.JefeDirecto;

                if (jefe == null || string.IsNullOrEmpty(jefe.Email))
                {
                    jefe = await _context.Empleados
                        .Include(e => e.Rol)
                        .Where(e => e.Rol.Codigo == "JP" && e.Activo == true)
                        .FirstOrDefaultAsync();
                }

                if (jefe != null && !string.IsNullOrEmpty(jefe.Email))
                {
                    var url = $"{Request.Scheme}://{Request.Host}/Finanzas/RevisionActividadesJP";

                    _queue.Enqueue(new ServicesNotificationItem
                    {
                        ToEmail = jefe.Email,
                        Subject = $"Revisión Pendiente - {comprobacion.CodigoComprobacion}",
                        TemplateName = "/Views/Emails/ComprobacionPendienteJP.cshtml",
                        Model = new
                        {
                            JefeNombre = $"{jefe.Nombre} {jefe.Apellidos}",
                            EmpleadoNombre = $"{empleado.Nombre} {empleado.Apellidos}",
                            CodigoComprobacion = comprobacion.CodigoComprobacion,
                            Escenario = escenario,
                            Diferencia = diferencia.ToString("C"),
                            Comentarios = comentarios,
                            Url = url,
                            Fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar correo al JP: {ex.Message}");
            }
        }

        // ENVÍA CORREO DE PROCESAMIENTO DE PAGO A FINANZAS
        private async Task EnviarCorreoProcesarPago(ComprobacionesViaje comprobacion, string comentarios, decimal diferencia, string escenario)
        {
            try
            {
                var finanzasUsers = await _context.Empleados
                        .Include(e => e.Rol)
                        .Where(e => e.Rol.Codigo == "FINANZAS" && e.Activo == true)
                        .ToListAsync();

                var url = $"{Request.Scheme}://{Request.Host}/Finanzas/AutorizacionesPago";

                foreach (var finanzas in finanzasUsers)
                {
                    _queue.Enqueue(new ServicesNotificationItem
                    {
                        ToEmail = finanzas.Email,
                        Subject = $"Pago para Procesar - {comprobacion.CodigoComprobacion}",
                        TemplateName = "/Views/Emails/PagoListoProcesar.cshtml",
                        Model = new
                        {
                            FinanzasNombre = $"{finanzas.Nombre} {finanzas.Apellidos}",
                            EmpleadoNombre = $"{comprobacion.SolicitudViaje.Empleado.Nombre} {comprobacion.SolicitudViaje.Empleado.Apellidos}",
                            CodigoComprobacion = comprobacion.CodigoComprobacion,
                            Diferencia = diferencia.ToString("C"),
                            Escenario = escenario,
                            Comentarios = comentarios,
                            Url = url,
                            Fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                            Nota = "NO REQUIERE APROBACIÓN DEL Jefe Directo",
                            AccionRequerida= "Procesar pago desde el módulo de Autorizaciones"
                        }
                    });
                }

                var empleado = comprobacion.SolicitudViaje.Empleado;
                _queue.Enqueue(new ServicesNotificationItem
                {
                    ToEmail = empleado.Email,
                    Subject = $"Comprobación Validada - {comprobacion.CodigoComprobacion}",
                    TemplateName = "/Views/Emails/ComprobacionValidadaSinJP.cshtml",
                    Model = new
                    {
                        EmpleadoNombre = $"{empleado.Nombre} {empleado.Apellidos}",
                        CodigoComprobación = comprobacion.CodigoComprobacion,
                        Diferencia = diferencia.ToString("C"),
                        Escenario = escenario,
                        Comentarios = comentarios,
                        ProximoPaso = "El área de Finanzas procesará el pago/reintegro en los próximos días."
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar correo: {ex.Message}");
            }
        }

        // ENVÍA CORREO DE APROBACIÓN A FINANZAS
        private async Task EnviarCorreoAprobacionFinanzas(ComprobacionesViaje comprobacion, Empleados jefe, string comentarios)
        {
            try
            {
                var finanzasUsers = await _context.Empleados
                 .Include(e => e.Rol)
                 .Where(e => e.Activo == true && e.Rol.Codigo == "FINANZAS")
                 .ToListAsync();

                var urlFinanzas = $"{Request.Scheme}://{Request.Host}/Finanzas/AutorizacionesPago";

                foreach (var finanzas in finanzasUsers)
                {
                    if (string.IsNullOrEmpty(finanzas.Email))
                        continue;

                    _queue.Enqueue(new ServicesNotificationItem
                    {
                        ToEmail = finanzas.Email,
                        Subject = $"Comprobación Aprobada por JP - {comprobacion.CodigoComprobacion}",
                        TemplateName = "/Views/Emails/ComprobacionAprobadaJP.cshtml",
                        Model = new
                        {
                            FinanzasNombre = $"{finanzas.Nombre} {finanzas.Apellidos}",
                            JefeNombre = $"{jefe.Nombre} {jefe.Apellidos}",
                            EmpleadoNombre = $"{comprobacion.SolicitudViaje.Empleado.Nombre} {comprobacion.SolicitudViaje.Empleado.Apellidos}",
                            CodigoComprobacion = comprobacion.CodigoComprobacion,
                            Diferencia = comprobacion.Diferencia?.ToString("C") ?? "$0.00",
                            Escenario = comprobacion.EscenarioLiquidacion,
                            EstadoActual = comprobacion.EstadoComprobacion?.Codigo ?? "PENDIENTE",
                            Comentarios = comentarios,
                            Url = urlFinanzas,
                            AccionRequerida = "Procesar pago o reintegro desde Autorizaciones de Pago"
                        }
                    });
                }

                var empleado = comprobacion.SolicitudViaje?.Empleado;
                if (empleado != null && !string.IsNullOrEmpty(empleado.Email))
                {
                    var urlEmpleado = $"{Request.Scheme}://{Request.Host}/Comprobaciones/Detalle/{comprobacion.Id}";

                    _queue.Enqueue(new ServicesNotificationItem
                    {
                        ToEmail = empleado.Email,
                        Subject = $"Comprobación Aprobada por tu Jefe - {comprobacion.CodigoComprobacion}",
                        TemplateName = "/Views/Emails/ComprobacionAprobadaPorJefe.cshtml",
                        Model = new
                        {
                            EmpleadoNombre = $"{empleado.Nombre} {empleado.Apellidos}",
                            JefeNombre = $"{jefe.Nombre} {jefe.Apellidos}",
                            CodigoComprobacion = comprobacion.CodigoComprobacion,
                            Diferencia = comprobacion.Diferencia?.ToString("C") ?? "$0.00",
                            EstadoActual = comprobacion.EstadoComprobacion?.Codigo ?? "APROBADA_JP",
                            Comentarios = comentarios,
                            Url = urlEmpleado
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar correos de aprobación JP: {ex.Message}");
            }
        }

        // ENVÍA CORREO DE RECHAZO AL EMPLEADO
        private async Task EnviarCorreoRechazoEmpleado(ComprobacionesViaje comprobacion, Empleados jefe, string comentarios, string tipoCorreccion)
        {
            try
            {
                var empleado = comprobacion.SolicitudViaje.Empleado;

                if (empleado != null && !string.IsNullOrEmpty(empleado.Email))
                {
                    var url = $"{Request.Scheme}://{Request.Host}/Comprobaciones/CorregirGastos/{comprobacion.Id}";

                    _queue.Enqueue(new ServicesNotificationItem
                    {
                        ToEmail = empleado.Email,
                        Subject = $"Correcciones Requeridas - {comprobacion.CodigoComprobacion}",
                        TemplateName = "/Views/Emails/ComprobacionRechazadaJP.cshtml",
                        Model = new
                        {
                            EmpleadoNombre = $"{empleado.Nombre} {empleado.Apellidos}",
                            JefeNombre = $"{jefe.Nombre} {jefe.Apellidos}",
                            CodigoComprobacion = comprobacion.CodigoComprobacion,
                            TipoCorreccion = tipoCorreccion,
                            Comentarios = comentarios,
                            Url = url,
                            FechaLimite = DateTime.Now.AddDays(3).ToString("dd/MM/yyyy")
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar correo de rechazo al empleado: {ex.Message}");
            }
        }

        // ENVÍA CORREO DE COMPROBACIÓN SALDADA
        private async Task EnviarCorreoComprobacionSaldada(ComprobacionesViaje comprobacion, string comentarios)
        {
            try
            {
                var empleado = comprobacion.SolicitudViaje.Empleado;

                if (empleado != null && !string.IsNullOrEmpty(empleado.Email))
                {
                    _queue.Enqueue(new ServicesNotificationItem
                    {
                        ToEmail = empleado.Email,
                        Subject = $"Comprobación Saldada - {comprobacion.CodigoComprobacion}",
                        TemplateName = "/Views/Emails/ComprobacionSaldada.cshtml",
                        Model = new
                        {
                            EmpleadoNombre = $"{empleado.Nombre} {empleado.Apellidos}",
                            CodigoComprobacion = comprobacion.CodigoComprobacion,
                            TotalGastos = comprobacion.TotalGastosComprobados?.ToString("C") ?? "$0.00",
                            TotalAnticipo = comprobacion.TotalAnticipo?.ToString("C") ?? "$0.00",
                            Diferencia = comprobacion.Diferencia?.ToString("C") ?? "$0.00",
                            Comentarios = comentarios,
                            FechaProcesamiento = DateTime.Now.ToString("dd/MM/yyyy")
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar correo de comprobación saldada: {ex.Message}");
            }
        }

        // ENVÍA CORREO DE GASTOS DEVUELTOS
        private async Task EnviarCorreoGastosDevueltos(int comprobacionId, string comentarios, int cantidadGastos)
        {
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.CategoriaGasto)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.EstadoGasto)
                    .FirstOrDefaultAsync(c => c.Id == comprobacionId);

                if (comprobacion?.SolicitudViaje?.Empleado?.Email == null)
                {
                    return;
                }

                var gastosDevueltos = comprobacion.SolicitudViaje.GastosReales
                    .Where(g => g.EstadoGasto?.Codigo == "DEVUELTO_CORRECCION")
                    .ToList();

                if (!gastosDevueltos.Any())
                {
                    return;
                }

                var empleado = comprobacion.SolicitudViaje.Empleado;
                var url = $"{Request.Scheme}://{Request.Host}/Comprobaciones/CorregirGastos/{comprobacion.Id}";

                _queue.Enqueue(new ServicesNotificationItem
                {
                    ToEmail = empleado.Email,
                    Subject = $"Gastos Devueltos para Corrección - {comprobacion.CodigoComprobacion}",
                    TemplateName = "/Views/Emails/GastosDevueltos.cshtml",
                    Model = new
                    {
                        EmpleadoNombre = $"{empleado.Nombre} {empleado.Apellidos}",
                        CodigoComprobacion = comprobacion.CodigoComprobacion,
                        ComentariosFinanzas = comentarios,
                        CantidadGastos = cantidadGastos,
                        GastosDevueltos = gastosDevueltos.Select(g => new
                        {
                            Concepto = g.Concepto,
                            Monto = g.Monto,
                            Fecha = g.FechaGasto.ToString("dd/MM/yyyy"),
                            Proveedor = g.Proveedor,
                            Categoria = g.CategoriaGasto?.Nombre ?? "N/A"
                        }).ToList(),
                        Url = url,
                        FechaLimite = DateTime.Now.AddDays(3).ToString("dd/MM/yyyy")
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al encolar correo: {ex.Message}");
            }
        }

        // ENVÍA CORREO DE COMPROBACIÓN REABIERTA
        private async Task EnviarCorreoComprobacionReabierta(int comprobacionId, string comentarios)
        {
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .FirstOrDefaultAsync(c => c.Id == comprobacionId);

                if (comprobacion?.SolicitudViaje?.Empleado?.Email == null)
                {
                    return;
                }

                var empleado = comprobacion.SolicitudViaje.Empleado;
                var url = $"{Request.Scheme}://{Request.Host}/Comprobaciones/CorregirGastos/{comprobacion.Id}";

                _queue.Enqueue(new ServicesNotificationItem
                {
                    ToEmail = empleado.Email,
                    Subject = $"Comprobación Reabierta - {comprobacion.CodigoComprobacion}",
                    TemplateName = "/Views/Emails/ComprobacionReabierta.cshtml",
                    Model = new
                    {
                        EmpleadoNombre = $"{empleado.Nombre} {empleado.Apellidos}",
                        CodigoComprobacion = comprobacion.CodigoComprobacion,
                        Comentarios = comentarios,
                        Url = url,
                        FechaReapertura = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                        Instrucciones = "Por favor, corrige todos los gastos devueltos y reenvía la comprobación a Finanzas."
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al encolar correo de reapertura: {ex.Message}");
            }
        }

        // ENVÍA CORREO DE CORRECCIÓN APROBADA
        private async Task EnviarCorreoCorreccionAprobada(ComprobacionesViaje comprobacion, string comentarios, int cantidadGastosAprobados)
        {
            try
            {
                var empleado = comprobacion.SolicitudViaje.Empleado;
                if (empleado == null || string.IsNullOrEmpty(empleado.Email)) return;

                _queue.Enqueue(new ServicesNotificationItem
                {
                    ToEmail = empleado.Email,
                    Subject = $"Correcciones Aprobadas - {comprobacion.CodigoComprobacion}",
                    TemplateName = "/Views/Emails/CorreccionAprobada.cshtml",
                    Model = new
                    {
                        EmpleadoNombre = $"{empleado.Nombre} {empleado.Apellidos}",
                        CodigoComprobacion = comprobacion.CodigoComprobacion,
                        FechaAprobacion = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                        Comentarios = comentarios,
                        CantidadGastosAprobados = cantidadGastosAprobados,
                        Url = $"{Request.Scheme}://{Request.Host}/Comprobaciones/Detalles/{comprobacion.Id}"
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al encolar correo de corrección aprobada: {ex.Message}");
            }
        }

        // ENVÍA CORREO DE CORRECCIÓN DEVUELTA
        private async Task EnviarCorreoCorreccionDevuelta(ComprobacionesViaje comprobacion, DevolucionGastosViewModel model)
        {
            try
            {
                var empleado = comprobacion.SolicitudViaje.Empleado;
                if (empleado == null || string.IsNullOrEmpty(empleado.Email)) return;

                var gastosDevueltos = comprobacion.SolicitudViaje.GastosReales
                    .Where(g => model.GastosSeleccionados.Contains(g.Id))
                    .ToList();

                _queue.Enqueue(new ServicesNotificationItem
                {
                    ToEmail = empleado.Email,
                    Subject = $"Correcciones Requeridas - {comprobacion.CodigoComprobacion}",
                    TemplateName = "/Views/Emails/CorreccionDevuelta.cshtml",
                    Model = new
                    {
                        EmpleadoNombre = $"{empleado.Nombre} {empleado.Apellidos}",
                        CodigoComprobacion = comprobacion.CodigoComprobacion,
                        Comentarios = model.Comentarios,
                        TipoCorreccion = model.TipoCorreccion,
                        FechaLimite = model.FechaLimite.ToString("dd/MM/yyyy"),
                        CantidadGastos = model.GastosSeleccionados.Count,
                        GastosDevueltos = gastosDevueltos.Select(g => new
                        {
                            Concepto = g.Concepto,
                            Monto = g.Monto.ToString("C"),
                            Fecha = g.FechaGasto.ToString("dd/MM/yyyy")
                        }).ToList(),
                        Url = $"{Request.Scheme}://{Request.Host}/Comprobaciones/CorregirGastos/{comprobacion.Id}"
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar correo de corrección devuelta: {ex.Message}");
            }
        }

        // ============================================
        // MÉTODOS DE NOTIFICACIONES EN SISTEMA
        // ============================================

        // CREA NOTIFICACIÓN EN BASE DE DATOS
        private async Task CrearNotificacion(int empleadoId, string titulo, string mensaje, string tipo, int? referenciaId)
        {
            try
            {
                var prioridad = DeterminarPrioridad(tipo);

                var notificacion = new Notificaciones
                {
                    EmpleadoId = empleadoId,
                    Tipo = tipo,
                    Titulo = titulo,
                    Mensaje = mensaje,
                    EntidadRelacionada = "ComprobacionViaje",
                    EntidadId = referenciaId,
                    Leida = false,
                    FechaEnvio = DateTime.Now,
                    Prioridad = prioridad
                };

                _context.Notificaciones.Add(notificacion);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creando notificación: {ex.Message}");
            }
        }

        // DETERMINA PRIORIDAD DE NOTIFICACIÓN
        private string DeterminarPrioridad(string tipoNotificacion)
        {
            var mapeoPrioridades = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "PAGO_PENDIENTE", "ALTA" },
                { "PAGO_AUTORIZADO", "ALTA" },
                { "PAGO_RECHAZADO", "ALTA" },
                { "URGENTE", "ALTA" },
                { "ERROR_CRITICO", "ALTA" },
                { "REVISION_JP_PENDIENTE", "MEDIA" },
                { "PAGO_LISTO_PROCESAR", "MEDIA" },
                { "LIQUIDACION_COMPLETA", "MEDIA" },
                { "SOLICITUD_CREADA", "MEDIA" },
                { "APROBACION_JP", "MEDIA" },
                { "CORRECCIONES_REQUERIDAS", "MEDIA" },
                { "COMENTARIO", "MEDIA" },
                { "INFORMATIVO", "BAJA" },
                { "RECORDATORIO", "BAJA" },
                { "ACTUALIZACION", "BAJA" }
            };

            return mapeoPrioridades.TryGetValue(tipoNotificacion, out string prioridad)
                ? prioridad
                : "MEDIA";
        }

        // ============================================
        // MÉTODOS AUXILIARES GENERALES

        // VERIFICA EXISTENCIA DE COTIZACIÓN
        private bool CotizacionExists(int id)
        {
            return _context.CotizacionesFinanzas.Any(e => e.Id == id);
        }

        // OBTIENE ID DEL USUARIO ACTUAL
        private async Task<int> GetCurrentUserIdAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                var defaultAdmin = await _context.Empleados
                    .FirstOrDefaultAsync(e => e.Email == "admin@viamtek.com");

                return defaultAdmin?.Id ?? 1;
            }
            return userId;
        }

        // GENERA CÓDIGO ÚNICO PARA COTIZACIÓN
        private string GenerarCodigoCotizacion()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var random = new Random().Next(1000, 9999);
            return $"COT-{timestamp}-{random}";
        }

        // GENERA CÓDIGO ÚNICO PARA ANTICIPO
        private string GenerarCodigoAnticipo()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var random = new Random().Next(1000, 9999);
            return $"ANT-{timestamp}-{random}";
        }

        // ============================================
        // MÉTODOS DE REDIRECCIÓN
        // ============================================

        // REDIRIGE A REPORTES
        public IActionResult Reportes()
        {
            return RedirectToAction("Dashboard", "Reportes");
        }

        // MUESTRA DOCUMENTOS
        public IActionResult Documentos()
        {
            return View();
        }

        // CLASE PARA SOLICITUD DE PROCESAMIENTO DE DEVOLUCIÓN
        public class ProcesarDevolucionRequest
        {
            public int ComprobacionId { get; set; }
            public string CodigoComprobacion { get; set; }
            public string Comentarios { get; set; }
            public List<int> GastosSeleccionados { get; set; }
        }
    }
}