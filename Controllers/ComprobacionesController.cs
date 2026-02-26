using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SVV.Filters;
using SVV.Models;
using SVV.Services;
using SVV.ViewModels;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization; 
namespace SVV.Controllers
{
    [TypeFilter(typeof(CambioPassword))]
    [Authorize]
    public class ComprobacionesController : Controller
    {
        private readonly SvvContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ComprobacionesController> _logger;
        private readonly INotificationQueue _queue;

        // CONSTRUCTOR CON INYECCIÓN DE DEPENDENCIAS PARA CONTEXTO, ENTORNO WEB, LOGGING Y COLA DE NOTIFICACIONES
        public ComprobacionesController(
            SvvContext context,
            IWebHostEnvironment environment,
            ILogger<ComprobacionesController> logger,
            INotificationQueue queue)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
            _queue = queue;
        }

        // VISTA PRINCIPAL DE COMPROBACIONES CON GESTIÓN DE ESTADOS DIFERENCIADOS
        public async Task<IActionResult> Index()
        {
            var empleadoId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var hoy = DateOnly.FromDateTime(DateTime.Now);
            var hace5Dias = hoy.AddDays(-5);

            // PROCESAMIENTO AUTOMÁTICO DE COMPROBACIONES EXPIRADAS
            await ProcesarComprobacionesExpiradas(empleadoId);

            // SOLICITUDES APROBADAS SIN COMPROBACIÓN CREADA
            var solicitudesAprobadas = await _context.SolicitudesViajes
                .Include(s => s.Estado)
                .Include(s => s.Empleado)
                .Include(s => s.Anticipos)
                .Where(s => s.EmpleadoId == empleadoId
                         && s.Estado.Codigo == "APROBADA_DIRECCION"
                         && s.FechaRegreso <= hoy
                         && s.FechaRegreso >= hace5Dias
                         && !_context.ComprobacionesViaje.Any(c => c.SolicitudViajeId == s.Id))
                .ToListAsync();

            // COMPROBACIONES PENDIENTES DE ENVÍO (ESTADO 1)
            var comprobacionesPendientes = await _context.ComprobacionesViaje
                .Include(c => c.SolicitudViaje)
                .Include(c => c.EstadoComprobacion)
                .Where(c => c.SolicitudViaje.EmpleadoId == empleadoId &&
                           c.EstadoComprobacionId == 1)
                .ToListAsync();

            // COMPROBACIONES EN REVISIÓN POR FINANZAS (ESTADO 2)
            var comprobacionesEnProceso = await _context.ComprobacionesViaje
                .Include(c => c.SolicitudViaje)
                .Include(c => c.EstadoComprobacion)
                .Where(c => c.SolicitudViaje.EmpleadoId == empleadoId &&
                           c.EstadoComprobacionId == 2)
                .ToListAsync();

            // COMPROBACIONES CON GASTOS DEVUELTOS PARA CORRECCIÓN (ESTADOS 8, 9)
            var comprobacionesConCorrecciones = await _context.ComprobacionesViaje
                .Include(c => c.SolicitudViaje)
                    .ThenInclude(s => s.Empleado)
                .Include(c => c.EstadoComprobacion)
                .Include(c => c.SolicitudViaje)
                    .ThenInclude(s => s.GastosReales)
                        .ThenInclude(g => g.EstadoGasto)
                .Where(c => c.SolicitudViaje.EmpleadoId == empleadoId
                         && (c.EstadoComprobacionId == 8 || c.EstadoComprobacionId == 9)
                         && c.SolicitudViaje.GastosReales.Any(g =>
                            g.EstadoGasto.Codigo == "DEVUELTO_CORRECCION"))
                .ToListAsync();

            // COMPROBACIONES GENERADAS AUTOMÁTICAMENTE POR SISTEMA
            var comprobacionesAutomaticas = await _context.ComprobacionesViaje
                .Include(c => c.SolicitudViaje)
                .Include(c => c.EstadoComprobacion)
                .Where(c => c.SolicitudViaje.EmpleadoId == empleadoId
                         && c.DescripcionActividades.Contains("AUTOMÁTICA"))
                .ToListAsync();

            ViewBag.SolicitudesAprobadas = solicitudesAprobadas;
            ViewBag.ComprobacionesPendientes = comprobacionesPendientes;
            ViewBag.ComprobacionesEnProceso = comprobacionesEnProceso;
            ViewBag.ComprobacionesConCorrecciones = comprobacionesConCorrecciones;
            ViewBag.ComprobacionesAutomaticas = comprobacionesAutomaticas;

            var mostrarAlertaExpiracion = TempData["MostrarAlertaExpiracion"] as bool? ?? false;
            ViewBag.MostrarAlertaExpiracion = mostrarAlertaExpiracion;

            return View(solicitudesAprobadas);
        }

        // GENERACIÓN AUTOMÁTICA DE COMPROBACIONES PARA PERÍODOS EXPIRADOS
        private async Task ProcesarComprobacionesExpiradas(int empleadoId)
        {
            var hoy = DateOnly.FromDateTime(DateTime.Now);

            var solicitudesExpiradas = await _context.SolicitudesViajes
                .Include(s => s.GastosReales)
                .Where(s => s.EmpleadoId == empleadoId
                         && s.Estado.Codigo == "APROBADA_DIRECCION"
                         && s.FechaRegreso.AddDays(5) < hoy
                         && !_context.ComprobacionesViaje.Any(c => c.SolicitudViajeId == s.Id))
                .ToListAsync();

            foreach (var solicitud in solicitudesExpiradas)
            {
                var comprobacionAutomatica = new ComprobacionesViaje
                {
                    SolicitudViajeId = solicitud.Id,
                    CodigoComprobacion = $"COMP-AUTO-{DateTime.Now:yyyyMMdd-HHmmss}",
                    DescripcionActividades = "COMPROBACIÓN CERRADA AUTOMÁTICAMENTE - Período de comprobación expirado",
                    ResultadosViaje = "El sistema cerró automáticamente esta comprobación al vencer el período de 5 días después del regreso",
                    EstadoComprobacionId = 2,
                    EscenarioLiquidacion = "AUTOMÁTICO",
                    FechaComprobacion = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                if (solicitud.GastosReales != null && solicitud.GastosReales.Any())
                {
                    comprobacionAutomatica.TotalGastosComprobados = solicitud.GastosReales.Sum(g => g.Monto);
                }

                _context.ComprobacionesViaje.Add(comprobacionAutomatica);
            }

            if (solicitudesExpiradas.Any())
            {
                await _context.SaveChangesAsync();
                TempData["MostrarAlertaExpiracion"] = true;
            }
        }

        // FORMULARIO DE REGISTRO DE GASTOS Y COMPROBACIÓN
        public async Task<IActionResult> Comprobar(int id)
        {
            try
            {
                var solicitud = await _context.SolicitudesViajes
                    .Include(s => s.Empleado)
                    .Include(s => s.Anticipos)
                    .Include(s => s.GastosReales)
                        .ThenInclude(g => g.CategoriaGasto)
                    .Include(s => s.GastosReales)
                        .ThenInclude(g => g.Factura)
                    .Include(s => s.GastosReales)
                        .ThenInclude(g => g.EstadoGasto)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (solicitud == null)
                {
                    TempData["Error"] = "Solicitud no encontrada";
                    return RedirectToAction("Index");
                }

                // VALIDACIÓN DE PERÍODO DE COMPROBACIÓN (5 DÍAS POSTERIORES AL REGRESO)
                var hoy = DateOnly.FromDateTime(DateTime.Now);
                var fechaLimite = solicitud.FechaRegreso.AddDays(5);

                var comprobacionAutomatica = await _context.ComprobacionesViaje
                    .FirstOrDefaultAsync(c => c.SolicitudViajeId == id &&
                        (c.EscenarioLiquidacion == "AUTOMÁTICO" || c.EscenarioLiquidacion == "AUTOMATICO"));

                if (comprobacionAutomatica != null)
                {
                    TempData["Error"] = "Esta comprobacion fue cerrada automaticamente por el sistema al vencer el periodo de 5 dias y ya fue enviada a Finanzas.";
                    return RedirectToAction("Index");
                }

                if (hoy > fechaLimite)
                {
                    TempData["Error"] = "El periodo para comprobar gastos ha expirado. El limite es 5 dias despues de tu regreso.";
                    return RedirectToAction("Index");
                }

                // CÁLCULO DEL MONTO TOTAL DE ANTICIPO
                decimal anticipoTotal = 0;
                if (solicitud.Anticipos != null && solicitud.Anticipos.Any())
                {
                    anticipoTotal = solicitud.Anticipos
                        .Where(a => a.MontoAutorizado.HasValue)
                        .Sum(a => a.MontoAutorizado.Value);
                }

                // CONVERSIÓN DE DATES PARA EL VIEWMODEL
                var fechaSalidaDateTime = new DateTime(solicitud.FechaSalida.Year, solicitud.FechaSalida.Month, solicitud.FechaSalida.Day);
                var fechaRegresoDateTime = new DateTime(solicitud.FechaRegreso.Year, solicitud.FechaRegreso.Month, solicitud.FechaRegreso.Day);
                var fechaLimiteDateTime = new DateTime(fechaLimite.Year, fechaLimite.Month, fechaLimite.Day);

                // CONSULTA DE COTIZACIÓN APROBADA ASOCIADA A LA SOLICITUD
                var cotizacion = await _context.CotizacionesFinanzas
                    .FirstOrDefaultAsync(c => c.SolicitudViajeId == id && c.Estado == "APROBADA");

                // CARGA DE DATOS DE COTIZACIÓN PARA COMPARACIÓN CON GASTOS REALES
                if (cotizacion != null)
                {
                    ViewBag.TieneCotizacion = true;
                    ViewBag.Cotizacion = cotizacion;

                    try
                    {
                        var transporteTemp = DeserializarJson<List<ConceptoItemJsonViewModel>>(cotizacion.TransportePreciosJson);
                        ViewBag.TransportePrecios = transporteTemp?.Select(x => new ConceptoItemViewModel
                        {
                            Precio = x.Precio ?? 0,
                            Descripcion = x.Descripcion
                        }).ToList() ?? new List<ConceptoItemViewModel>();

                        var gasolinaTemp = DeserializarJson<List<ConceptoItemJsonViewModel>>(cotizacion.GasolinaPreciosJson);
                        ViewBag.GasolinaPrecios = gasolinaTemp?.Select(x => new ConceptoItemViewModel
                        {
                            Precio = x.Precio ?? 0,
                            Descripcion = x.Descripcion
                        }).ToList() ?? new List<ConceptoItemViewModel>();

                        var uberTaxiTemp = DeserializarJson<List<ConceptoItemJsonViewModel>>(cotizacion.UberTaxiPreciosJson);
                        ViewBag.UberTaxiPrecios = uberTaxiTemp?.Select(x => new ConceptoItemViewModel
                        {
                            Precio = x.Precio ?? 0,
                            Descripcion = x.Descripcion
                        }).ToList() ?? new List<ConceptoItemViewModel>();

                        var casetasTemp = DeserializarJson<List<ConceptoItemJsonViewModel>>(cotizacion.CasetasPreciosJson);
                        ViewBag.CasetasPrecios = casetasTemp?.Select(x => new ConceptoItemViewModel
                        {
                            Precio = x.Precio ?? 0,
                            Descripcion = x.Descripcion
                        }).ToList() ?? new List<ConceptoItemViewModel>();

                        var hospedajeTemp = DeserializarJson<List<ConceptoItemJsonViewModel>>(cotizacion.HospedajePreciosJson);
                        ViewBag.HospedajePrecios = hospedajeTemp?.Select(x => new ConceptoItemViewModel
                        {
                            Precio = x.Precio ?? 0,
                            Descripcion = x.Descripcion
                        }).ToList() ?? new List<ConceptoItemViewModel>();

                        var alimentosTemp = DeserializarJson<List<ConceptoItemJsonViewModel>>(cotizacion.AlimentosPreciosJson);
                        ViewBag.AlimentosPrecios = alimentosTemp?.Select(x => new ConceptoItemViewModel
                        {
                            Precio = x.Precio ?? 0,
                            Descripcion = x.Descripcion
                        }).ToList() ?? new List<ConceptoItemViewModel>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al deserializar JSON de cotización");
                        ViewBag.TransportePrecios = new List<ConceptoItemViewModel>();
                        ViewBag.GasolinaPrecios = new List<ConceptoItemViewModel>();
                        ViewBag.UberTaxiPrecios = new List<ConceptoItemViewModel>();
                        ViewBag.CasetasPrecios = new List<ConceptoItemViewModel>();
                        ViewBag.HospedajePrecios = new List<ConceptoItemViewModel>();
                        ViewBag.AlimentosPrecios = new List<ConceptoItemViewModel>();
                    }
                }
                else
                {
                    ViewBag.TieneCotizacion = false;
                    ViewBag.TransportePrecios = new List<ConceptoItemViewModel>();
                    ViewBag.GasolinaPrecios = new List<ConceptoItemViewModel>();
                    ViewBag.UberTaxiPrecios = new List<ConceptoItemViewModel>();
                    ViewBag.CasetasPrecios = new List<ConceptoItemViewModel>();
                    ViewBag.HospedajePrecios = new List<ConceptoItemViewModel>();
                    ViewBag.AlimentosPrecios = new List<ConceptoItemViewModel>();
                }

                var viewModel = new ComprobacionViewModel
                {
                    SolicitudId = solicitud.Id,
                    CodigoSolicitud = solicitud.CodigoSolicitud,
                    EmpleadoNombre = $"{solicitud.Empleado.Nombre} {solicitud.Empleado.Apellidos}",
                    Destino = solicitud.Destino,
                    FechaSalida = fechaSalidaDateTime,
                    FechaRegreso = fechaRegresoDateTime,
                    AnticipoAutorizado = anticipoTotal,
                    FechaLimiteComprobacion = fechaLimiteDateTime
                };

                // CARGA DE COMPROBACIÓN EXISTENTE CON SUS GASTOS ASOCIADOS
                var comprobacionExistente = await _context.ComprobacionesViaje
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
                    .FirstOrDefaultAsync(c => c.SolicitudViajeId == id);

                if (comprobacionExistente != null)
                {
                    viewModel.ComprobacionId = comprobacionExistente.Id;
                    viewModel.DescripcionActividades = comprobacionExistente.DescripcionActividades;
                    viewModel.ResultadosViaje = comprobacionExistente.ResultadosViaje;
                    viewModel.EstatusComprobacion = comprobacionExistente.EstadoComprobacionId;
                    viewModel.EstadoComprobacionNombre = comprobacionExistente.EstadoComprobacion?.Codigo ?? "Pendiente";
                    viewModel.TotalComprobado = comprobacionExistente.TotalGastosComprobados ?? 0;

                    // CARGA DE GASTOS PREVIAMENTE REGISTRADOS
                    foreach (var gasto in comprobacionExistente.SolicitudViaje.GastosReales)
                    {
                        var fechaGastoDateTime = new DateTime(gasto.FechaGasto.Year, gasto.FechaGasto.Month, gasto.FechaGasto.Day);

                        viewModel.Gastos.Add(new GastoRealViewModel
                        {
                            Id = gasto.Id,
                            SolicitudId = gasto.SolicitudViajeId,
                            CategoriaGastoId = gasto.CategoriaGastoId,
                            Concepto = gasto.Concepto,
                            FechaGasto = fechaGastoDateTime,
                            Monto = gasto.Monto,
                            Proveedor = gasto.Proveedor,
                            Descripcion = gasto.Descripcion,
                            MedioPago = gasto.MedioPago,
                            LugarGasto = gasto.LugarGasto,
                            EstadoGasto = gasto.EstadoGasto?.Nombre ?? "Pendiente",
                            EstadoGastoCodigo = gasto.EstadoGasto?.Codigo ?? "PENDIENTE",
                            FacturaPDF = gasto.Factura?.ArchivoPdfUrl,
                            FacturaXML = gasto.Factura?.ArchivoXmlUrl
                        });
                    }
                }
                else
                {
                    // CARGA DE GASTOS DIRECTAMENTE DE LA SOLICITUD SI NO HAY COMPROBACIÓN
                    if (solicitud.GastosReales != null && solicitud.GastosReales.Any())
                    {
                        viewModel.TotalComprobado = solicitud.GastosReales.Sum(g => g.Monto);

                        foreach (var gasto in solicitud.GastosReales)
                        {
                            var fechaGastoDateTime = new DateTime(gasto.FechaGasto.Year, gasto.FechaGasto.Month, gasto.FechaGasto.Day);

                            viewModel.Gastos.Add(new GastoRealViewModel
                            {
                                Id = gasto.Id,
                                SolicitudId = gasto.SolicitudViajeId,
                                CategoriaGastoId = gasto.CategoriaGastoId,
                                Concepto = gasto.Concepto,
                                FechaGasto = fechaGastoDateTime,
                                Monto = gasto.Monto,
                                Proveedor = gasto.Proveedor,
                                Descripcion = gasto.Descripcion,
                                MedioPago = gasto.MedioPago,
                                LugarGasto = gasto.LugarGasto,
                                EstadoGasto = gasto.EstadoGasto?.Nombre ?? "Pendiente",
                                EstadoGastoCodigo = gasto.EstadoGasto?.Codigo ?? "PENDIENTE",
                                FacturaPDF = gasto.Factura?.ArchivoPdfUrl,
                                FacturaXML = gasto.Factura?.ArchivoXmlUrl
                            });
                        }
                    }
                }

                ViewBag.CategoriasGasto = await _context.CategoriasGasto.ToListAsync();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Comprobar");
                TempData["Error"] = "Error al cargar la información de comprobación";
                return RedirectToAction("Index");
            }
        }

        // PROCESAMIENTO DE NUEVOS GASTOS CON GESTIÓN DE TRANSACCIÓN Y ARCHIVOS
        [HttpPost]
        public async Task<IActionResult> GuardarGasto(GastoRealViewModel model)
        {
            if (ModelState.IsValid)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // VERIFICACIÓN O CREACIÓN DE COMPROBACIÓN PRINCIPAL
                    var comprobacion = await _context.ComprobacionesViaje
                        .FirstOrDefaultAsync(c => c.SolicitudViajeId == model.SolicitudId);

                    if (comprobacion == null)
                    {
                        comprobacion = new ComprobacionesViaje
                        {
                            SolicitudViajeId = model.SolicitudId,
                            CodigoComprobacion = $"COMP-{DateTime.Now:yyyyMMdd-HHmmss}",
                            DescripcionActividades = "Pendiente de completar",
                            ResultadosViaje = "Pendiente de completar",
                            EstadoComprobacionId = 1,
                            EscenarioLiquidacion = "PENDIENTE",
                            FechaComprobacion = DateTime.Now,
                            CreatedAt = DateTime.Now
                        };
                        _context.ComprobacionesViaje.Add(comprobacion);
                        await _context.SaveChangesAsync();
                    }

                    // CONVERSIÓN DE FECHAS PARA BASE DE DATOS
                    var fechaGasto = DateOnly.FromDateTime(model.FechaGasto);

                    // CREACIÓN DE REGISTRO DE GASTO REAL
                    var gastoReal = new GastosReales
                    {
                        SolicitudViajeId = model.SolicitudId,
                        CategoriaGastoId = model.CategoriaGastoId,
                        Concepto = model.Concepto,
                        FechaGasto = fechaGasto,
                        Monto = model.Monto,
                        Proveedor = model.Proveedor,
                        Descripcion = model.Descripcion,
                        MedioPago = model.MedioPago,
                        LugarGasto = model.LugarGasto,
                        EstadoGastoId = 1,
                        CreatedAt = DateTime.Now
                    };

                    _context.GastosReales.Add(gastoReal);
                    await _context.SaveChangesAsync();

                    // ALMACENAMIENTO DE FACTURAS PDF/XML EN SERVIDOR
                    if (model.ArchivoPDF != null || model.ArchivoXML != null)
                    {
                        var factura = new Facturas
                        {
                            GastoRealId = gastoReal.Id,
                            CreatedAt = DateTime.Now
                        };

                        if (model.ArchivoPDF != null)
                        {
                            factura.ArchivoPdfUrl = await GuardarArchivo(model.ArchivoPDF, "pdf", comprobacion.Id, gastoReal.Id);
                        }

                        if (model.ArchivoXML != null)
                        {
                            factura.ArchivoXmlUrl = await GuardarArchivo(model.ArchivoXML, "xml", comprobacion.Id, gastoReal.Id);
                        }

                        _context.Facturas.Add(factura);
                        await _context.SaveChangesAsync();
                    }

                    // ACTUALIZACIÓN AUTOMÁTICA DE TOTALES DE COMPROBACIÓN
                    await ActualizarTotalesComprobacion(comprobacion.Id);

                    await transaction.CommitAsync();

                    TempData["Success"] = "Gasto guardado exitosamente";
                    return RedirectToAction("Comprobar", new { id = model.SolicitudId });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error al guardar el gasto");
                    TempData["Error"] = "Error al guardar el gasto: " + ex.Message;
                }
            }

            ViewBag.CategoriasGasto = await _context.CategoriasGasto.ToListAsync();
            return View("Comprobar", new ComprobacionViewModel { SolicitudId = model.SolicitudId });
        }

        // GUARDADO DE INFORME DESCRIPTIVO DEL VIAJE MEDIANTE AJAX
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarInforme([FromBody] GuardarInformeViewModel model)
        {
            try
            {
                _logger.LogInformation("Iniciando guardado de informe para solicitud: {SolicitudId}", model.SolicitudId);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors);
                    _logger.LogError("Errores de validación: {Errores}", string.Join(", ", errors.Select(e => e.ErrorMessage)));
                    return Json(new
                    {
                        success = false,
                        message = "Error de validación: " + string.Join(", ", errors.Select(e => e.ErrorMessage))
                    });
                }

                // BÚSQUEDA O CREACIÓN DE COMPROBACIÓN ASOCIADA
                var comprobacion = await _context.ComprobacionesViaje
                    .FirstOrDefaultAsync(c => c.SolicitudViajeId == model.SolicitudId);

                if (comprobacion == null)
                {
                    _logger.LogInformation("Creando nueva comprobación para solicitud: {SolicitudId}", model.SolicitudId);

                    comprobacion = new ComprobacionesViaje
                    {
                        SolicitudViajeId = model.SolicitudId,
                        CodigoComprobacion = $"COMP-{DateTime.Now:yyyyMMdd-HHmmss}",
                        DescripcionActividades = model.DescripcionActividades,
                        ResultadosViaje = model.ResultadosViaje,
                        EstadoComprobacionId = 1,
                        EscenarioLiquidacion = "PENDIENTE",
                        FechaComprobacion = DateTime.Now,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    _context.ComprobacionesViaje.Add(comprobacion);
                }
                else
                {
                    _logger.LogInformation("Actualizando comprobación existente: ID {ComprobacionId}", comprobacion.Id);

                    comprobacion.DescripcionActividades = model.DescripcionActividades;
                    comprobacion.ResultadosViaje = model.ResultadosViaje;
                    comprobacion.UpdatedAt = DateTime.Now;

                    if (comprobacion.EstadoComprobacionId == 0)
                    {
                        comprobacion.EstadoComprobacionId = 1;
                    }

                    _context.ComprobacionesViaje.Update(comprobacion);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Informe guardado exitosamente para comprobación: {ComprobacionId}", comprobacion.Id);

                return Json(new
                {
                    success = true,
                    message = "Informe guardado correctamente",
                    comprobacionId = comprobacion.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar informe");
                return Json(new
                {
                    success = false,
                    message = $"Error al guardar el informe: {ex.Message}"
                });
            }
        }

        // ENVÍO DE COMPROBACIÓN COMPLETA A FINANZAS PARA REVISIÓN
        [HttpPost]
        public async Task<IActionResult> EnviarAFianzas(int solicitudId)
        {
            try
            {
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                    .FirstOrDefaultAsync(c => c.SolicitudViajeId == solicitudId);

                if (comprobacion != null)
                {
                    // VALIDACIÓN DE MÍNIMO UN GASTO REGISTRADO
                    var tieneGastos = await _context.GastosReales
                        .AnyAsync(g => g.SolicitudViajeId == solicitudId);

                    if (!tieneGastos)
                    {
                        TempData["Error"] = "Debe registrar al menos un gasto antes de enviar a Finanzas";
                        return RedirectToAction("Comprobar", new { id = solicitudId });
                    }

                    // VALIDACIÓN DE COMPLETITUD DEL INFORME DESCRIPTIVO
                    if (string.IsNullOrEmpty(comprobacion.DescripcionActividades) ||
                        string.IsNullOrEmpty(comprobacion.ResultadosViaje))
                    {
                        TempData["Error"] = "Debe completar el informe del viaje antes de enviar a Finanzas";
                        return RedirectToAction("Comprobar", new { id = solicitudId });
                    }

                    // CAMBIO DE ESTADO A EN PROCESO (FINANZAS)
                    comprobacion.EstadoComprobacionId = 2;
                    comprobacion.UpdatedAt = DateTime.Now;

                    await _context.SaveChangesAsync();

                    // NOTIFICACIÓN AUTOMÁTICA A DEPARTAMENTO DE FINANZAS
                    await NotificarFinanzasComprobacionEnviada(comprobacion);

                    TempData["Success"] = "Comprobación enviada a Finanzas exitosamente. Estará en revisión.";
                    return RedirectToAction("Index");
                }

                TempData["Error"] = "No se encontró la comprobación";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar la comprobación a Finanzas");
                TempData["Error"] = "Error al enviar la comprobación: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ELIMINACIÓN SEGURA DE GASTOS CON REMOCIÓN DE ARCHIVOS FÍSICOS
        [HttpPost]
        public async Task<IActionResult> EliminarGasto(int id)
        {
            try
            {
                var gasto = await _context.GastosReales
                    .Include(g => g.Factura)
                    .FirstOrDefaultAsync(g => g.Id == id);

                if (gasto != null)
                {
                    // ELIMINACIÓN FÍSICA DE ARCHIVOS PDF/XML DEL SERVIDOR
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

                    _context.GastosReales.Remove(gasto);
                    await _context.SaveChangesAsync();

                    // RE-CÁLCULO DE TOTALES DE COMPROBACIÓN
                    var comprobacion = await _context.ComprobacionesViaje
                        .FirstOrDefaultAsync(c => c.SolicitudViajeId == gasto.SolicitudViajeId);

                    if (comprobacion != null)
                    {
                        await ActualizarTotalesComprobacion(comprobacion.Id);
                    }

                    return Json(new { success = true, message = "Gasto eliminado exitosamente" });
                }

                return Json(new { success = false, message = "Gasto no encontrado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar el gasto");
                return Json(new { success = false, message = "Error al eliminar el gasto: " + ex.Message });
            }
        }

        // INTERFAZ DE CORRECCIÓN PARA GASTOS DEVUELTOS POR FINANZAS
        public async Task<IActionResult> CorregirGastos(int id)
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
                    .Include(c => c.EstadoComprobacion)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (comprobacion == null)
                {
                    TempData["Error"] = "Comprobación no encontrada";
                    return RedirectToAction(nameof(Index));
                }

                // VALIDACIÓN DE PROPIEDAD DE LA COMPROBACIÓN
                var empleadoId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                if (comprobacion.SolicitudViaje.EmpleadoId != empleadoId)
                {
                    TempData["Error"] = "No tienes permiso para corregir esta comprobación";
                    return RedirectToAction(nameof(Index));
                }

                // FILTRADO DE GASTOS EN ESTADO "DEVUELTO_CORRECCION"
                var gastosDevueltos = comprobacion.SolicitudViaje.GastosReales
                    .Where(g => g.EstadoGasto?.Codigo == "DEVUELTO_CORRECCION")
                    .ToList();

                if (!gastosDevueltos.Any())
                {
                    TempData["Info"] = "No hay gastos pendientes de corrección";
                    return RedirectToAction(nameof(Index));
                }

                var viewModel = new ComprobacionCorreccionViewModel
                {
                    ComprobacionId = comprobacion.Id,
                    CodigoComprobacion = comprobacion.CodigoComprobacion ?? "",
                    ComentariosFinanzas = comprobacion.ComentariosFinanzas ?? "",
                    EmpleadoNombre = $"{comprobacion.SolicitudViaje.Empleado.Nombre} {comprobacion.SolicitudViaje.Empleado.Apellidos}",
                    EmpleadoEmail = comprobacion.SolicitudViaje.Empleado.Email ?? "",
                    TotalComprobacion = comprobacion.TotalGastosComprobados ?? 0,
                    TotalAnticipo = comprobacion.TotalAnticipo ?? 0,
                    Diferencia = comprobacion.Diferencia ?? 0,
                    EstadoActual = comprobacion.EstadoComprobacion?.Codigo ?? "DEVUELTO_CORRECCION",
                    EstadoComprobacionId = comprobacion.EstadoComprobacionId,
                    ComentariosDevolucion = "Finanzas devolvió este gasto para corrección. Por favor, sube los archivos correctos.",
                    EsVistaEmpleado = true,
                    EsVistaFinanzas = false,
                    GastosDevueltos = gastosDevueltos.Select(g => new GastoCorreccionViewModel
                    {
                        GastoId = g.Id,
                        Concepto = g.Concepto ?? "",
                        Monto = g.Monto,
                        Categoria = g.CategoriaGasto?.Nombre ?? "N/A",
                        FechaGasto = g.FechaGasto.ToDateTime(TimeOnly.MinValue),
                        Proveedor = g.Proveedor ?? "",
                        ComentarioFinanzas = comprobacion.ComentariosFinanzas ?? "Documentación insuficiente o incorrecta",
                        FacturaPDFActual = g.Factura?.ArchivoPdfUrl ?? "",
                        FacturaXMLActual = g.Factura?.ArchivoXmlUrl ?? "",
                        EstadoGasto = g.EstadoGasto?.Nombre ?? "Devuelto para corrección",
                        EstadoGastoCodigo = g.EstadoGasto?.Codigo ?? "DEVUELTO_CORRECCION",
                        EstadoActual = g.EstadoGasto?.Codigo ?? "DEVUELTO_CORRECCION",
                        FacturaPDFCorregido = "",
                        FacturaXMLCorregido = "",
                        ErroresValidacionXml = "",
                        TieneCorrecciones = false,
                        XmlEsValido = false
                    }).ToList()
                };

                ModelState.Clear();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CorregirGastos (GET)");
                TempData["Error"] = $"Error al cargar la vista de corrección: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // PROCESAMIENTO DE CORRECCIONES DE GASTOS CON ACTUALIZACIÓN DE ESTADOS
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CorregirGastos(ComprobacionCorreccionViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    _logger.LogError("Modelo inválido. Errores: {Errores}", string.Join(", ", errors));

                    return Json(new
                    {
                        success = false,
                        message = "Datos del formulario inválidos",
                        errors = errors
                    });
                }

                _logger.LogInformation("Iniciando corrección de gastos para comprobación: {ComprobacionId}", model.ComprobacionId);

                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.Factura)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.EstadoGasto)
                    .FirstOrDefaultAsync(c => c.Id == model.ComprobacionId);

                if (comprobacion == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Comprobación no encontrada"
                    });
                }

                bool archivosGuardados = false;
                int gastosCorregidos = 0;
                int totalGastosDevueltos = model.GastosDevueltos.Count;

                // CONSULTA DEL ESTADO "APROBADO" PARA ACTUALIZACIÓN
                var estadoGastoAprobado = await _context.EstadosGastos
                    .FirstOrDefaultAsync(e => e.Id == 2);

                if (estadoGastoAprobado == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "No se encontró el estado APROBADO en el sistema"
                    });
                }

                var gastosCorregidosIds = new List<int>();

                // PROCESAMIENTO INDIVIDUAL DE CADA GASTO DEVUELTO
                foreach (var gastoViewModel in model.GastosDevueltos)
                {
                    var gasto = comprobacion.SolicitudViaje.GastosReales
                        .FirstOrDefault(g => g.Id == gastoViewModel.GastoId);

                    if (gasto == null)
                    {
                        _logger.LogWarning("Gasto {GastoId} no encontrado", gastoViewModel.GastoId);
                        continue;
                    }

                    // VALIDACIÓN DE ESTADO ACTUAL DEL GASTO
                    if (gasto.EstadoGastoId != 4)
                    {
                        _logger.LogWarning("Gasto {GastoId} no está en estado DEVUELTO_CORRECCION", gasto.Id);
                        continue;
                    }

                    if (gasto.Factura == null)
                    {
                        gasto.Factura = new Facturas
                        {
                            GastoRealId = gasto.Id,
                            CreatedAt = DateTime.Now
                        };
                    }

                    bool gastoCorregido = false;

                    // PROCESAMIENTO DE ARCHIVO PDF CORREGIDO
                    if (gastoViewModel.ArchivoPDF != null && gastoViewModel.ArchivoPDF.Length > 0)
                    {
                        try
                        {
                            var pdfUrl = await GuardarArchivoCorregido(
                                gastoViewModel.ArchivoPDF, "pdf", comprobacion.Id, gasto.Id);

                            gasto.Factura.ArchivoPdfUrl = pdfUrl;

                            _logger.LogInformation("PDF corregido guardado para gasto {GastoId}", gasto.Id);
                            archivosGuardados = true;
                            gastoCorregido = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error al guardar PDF para gasto {GastoId}", gasto.Id);
                        }
                    }

                    // PROCESAMIENTO DE ARCHIVO XML CORREGIDO
                    if (gastoViewModel.ArchivoXML != null && gastoViewModel.ArchivoXML.Length > 0)
                    {
                        try
                        {
                            var xmlUrl = await GuardarArchivoCorregido(
                                gastoViewModel.ArchivoXML, "xml", comprobacion.Id, gasto.Id);

                            gasto.Factura.ArchivoXmlUrl = xmlUrl;

                            _logger.LogInformation("XML corregido guardado para gasto {GastoId}", gasto.Id);
                            archivosGuardados = true;
                            gastoCorregido = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error al guardar XML para gasto {GastoId}", gasto.Id);
                        }
                    }

                    // ACTUALIZACIÓN DE ESTADO A APROBADO SI SE SUBIERON ARCHIVOS
                    if (gastoCorregido)
                    {
                        gasto.EstadoGastoId = estadoGastoAprobado.Id;

                        if (string.IsNullOrEmpty(gasto.Descripcion))
                        {
                            gasto.Descripcion = $"Corregido el {DateTime.Now:dd/MM/yyyy HH:mm}";
                        }
                        else
                        {
                            gasto.Descripcion += $"\n[Corregido el {DateTime.Now:dd/MM/yyyy HH:mm}]";
                        }

                        gastosCorregidosIds.Add(gasto.Id);
                        gastosCorregidos++;
                        _logger.LogInformation("Gasto {GastoId} marcado como APROBADO (corregido)", gasto.Id);
                    }
                }

                // ACTUALIZACIÓN GLOBAL DEL ESTADO DE LA COMPROBACIÓN
                if (gastosCorregidos > 0)
                {
                    comprobacion.EstadoComprobacionId = 9;

                    var fechaCorreccion = DateTime.Now;
                    comprobacion.ComentariosFinanzas = (comprobacion.ComentariosFinanzas ?? "") +
                        $"\n\n--- CORRECCIÓN REALIZADA POR EMPLEADO ({fechaCorreccion:dd/MM/yyyy HH:mm}) ---\n" +
                        $"El empleado corrigió {gastosCorregidos} de {totalGastosDevueltos} gastos devueltos.\n" +
                        $"Gastos corregidos (IDs): {string.Join(", ", gastosCorregidosIds)}\n" +
                        $"Archivos corregidos subidos. Pendiente de revisión por Finanzas.";

                    comprobacion.UpdatedAt = fechaCorreccion;

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Correcciones guardadas exitosamente. {GastosCorregidos} gastos corregidos.", gastosCorregidos);

                    // NOTIFICACIÓN A FINANZAS SOBRE CORRECCIONES COMPLETADAS
                    await NotificarFinanzasCorreccionCompletada(comprobacion, gastosCorregidos);

                    return Json(new
                    {
                        success = true,
                        message = $"Correcciones enviadas exitosamente. {gastosCorregidos} de {totalGastosDevueltos} gastos corregidos. Los gastos han sido enviados para revisión por Finanzas.",
                        comprobacionId = comprobacion.Id,
                        estadoId = 9,
                        gastosCorregidos = gastosCorregidosIds,
                        redirectUrl = Url.Action("Index", "Comprobaciones")
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = "No se subieron archivos nuevos. Por favor, adjunta los archivos corregidos (PDF y/o XML) antes de enviar."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CorregirGastos");
                return Json(new
                {
                    success = false,
                    message = $"Error interno del servidor: {ex.Message}"
                });
            }
        }

        // ACTUALIZACIÓN AUTOMÁTICA DE TOTALES Y ESCENARIO DE LIQUIDACIÓN
        private async Task ActualizarTotalesComprobacion(int comprobacionId)
        {
            var comprobacion = await _context.ComprobacionesViaje
                .Include(c => c.SolicitudViaje)
                .FirstOrDefaultAsync(c => c.Id == comprobacionId);

            if (comprobacion != null)
            {
                // CÁLCULO DE TOTAL DE GASTOS COMPROBADOS
                comprobacion.TotalGastosComprobados = await _context.GastosReales
                    .Where(g => g.SolicitudViajeId == comprobacion.SolicitudViajeId)
                    .SumAsync(g => g.Monto);

                // CÁLCULO DE TOTAL DE ANTICIPO AUTORIZADO
                comprobacion.TotalAnticipo = await _context.Anticipos
                    .Where(a => a.SolicitudViajeId == comprobacion.SolicitudViajeId)
                    .SumAsync(a => a.MontoAutorizado ?? 0);

                // CÁLCULO DE DIFERENCIA ENTRE GASTOS Y ANTICIPO
                comprobacion.Diferencia = (comprobacion.TotalGastosComprobados ?? 0) - (comprobacion.TotalAnticipo ?? 0);

                // DETERMINACIÓN AUTOMÁTICA DEL ESCENARIO DE LIQUIDACIÓN
                comprobacion.EscenarioLiquidacion = comprobacion.Diferencia >= 0 ?
                    "REPOSICION_EMPRESA" : "REPOSICION_COLABORADOR";

                await _context.SaveChangesAsync();
            }
        }

        // NOTIFICACIÓN A FINANZAS SOBRE CORRECCIONES COMPLETADAS
        private async Task NotificarFinanzasCorreccionCompletada(ComprobacionesViaje comprobacion, int gastosCorregidos)
        {
            try
            {
                var finanzasUsers = await _context.Empleados
                    .Include(e => e.Rol)
                    .Where(e => (e.Rol.Nombre.Contains("Finanzas") || e.Rol.Nombre.Contains("FINANZAS")) &&
                               e.Activo == true)
                    .ToListAsync();

                foreach (var finanzas in finanzasUsers)
                {
                    await CrearNotificacion(
                        finanzas.Id,
                        $"Corrección Completada - {comprobacion.CodigoComprobacion}",
                        $"El empleado ha completado las correcciones de {gastosCorregidos} gastos.\n" +
                        $"Comprobación: {comprobacion.CodigoComprobacion}\n" +
                        $"Estado: CON_CORRECCIONES_PENDIENTES\n" +
                        $"Haga clic en 'Revisar Corrección' para verificar los archivos corregidos.",
                        "CORRECCION_COMPLETADA",
                        comprobacion.Id
                    );
                }

                _logger.LogInformation("Notificaciones enviadas a {Cantidad} usuarios de Finanzas", finanzasUsers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al notificar a Finanzas");
            }
        }

        // CREACIÓN DE NOTIFICACIONES EN SISTEMA CON PRIORIDAD DINÁMICA
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
                _logger.LogError(ex, "Error creando notificación para empleado {EmpleadoId}", empleadoId);
            }
        }

        // DETERMINACIÓN DE PRIORIDAD SEGÚN TIPO DE NOTIFICACIÓN
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

        // NOTIFICACIÓN A FINANZAS SOBRE NUEVA COMPROBACIÓN ENVIADA
        private async Task NotificarFinanzasComprobacionEnviada(ComprobacionesViaje comprobacion)
        {
            try
            {
                var finanzasUsers = await _context.Empleados
                    .Include(e => e.Rol)
                    .Where(e => (e.Rol.Nombre.Contains("Finanzas") || e.Rol.Nombre.Contains("FINANZAS")) &&
                               e.Activo == true)
                    .ToListAsync();

                // ESTADÍSTICAS DE GASTOS PARA NOTIFICACIÓN
                var gastos = await _context.GastosReales
                    .Include(g => g.Factura)
                    .Where(g => g.SolicitudViajeId == comprobacion.SolicitudViajeId)
                    .ToListAsync();

                var gastosConDocumentos = gastos.Count(g =>
                    !string.IsNullOrEmpty(g.Factura?.ArchivoXmlUrl) &&
                    !string.IsNullOrEmpty(g.Factura?.ArchivoPdfUrl));
                var totalGastos = gastos.Count;
                var montoTotal = gastos.Sum(g => g.Monto);

                var empleado = await _context.Empleados
                    .FirstOrDefaultAsync(e => e.Id == comprobacion.SolicitudViaje.EmpleadoId);

                // ENVÍO DE NOTIFICACIONES Y CORREOS A CADA USUARIO DE FINANZAS
                foreach (var finanzas in finanzasUsers)
                {
                    await CrearNotificacion(
                        finanzas.Id,
                        $"Nueva Comprobación Enviada - {comprobacion.CodigoComprobacion}",
                        $"El empleado {empleado?.Nombre} {empleado?.Apellidos} ha enviado una comprobación para revisión.\n" +
                        $"Comprobación: {comprobacion.CodigoComprobacion}\n" +
                        $"Monto Total: ${montoTotal:N2}\n" +
                        $"Gastos: {gastosConDocumentos}/{totalGastos} completos\n" +
                        $"Haga clic para revisar.",
                        "COMPROBACION_ENVIADA",
                        comprobacion.Id
                    );

                    await EnviarCorreoFinanzasComprobacionEnviada(finanzas, comprobacion,
                        empleado, gastosConDocumentos, totalGastos, montoTotal);
                }

                _logger.LogInformation("Notificaciones enviadas a {Cantidad} usuarios de Finanzas", finanzasUsers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al notificar a Finanzas para comprobación {CodigoComprobacion}",
                    comprobacion.CodigoComprobacion);
            }
        }

        // ENVÍO DE CORREO ELECTRÓNICO A FINANZAS SOBRE NUEVA COMPROBACIÓN
        private async Task EnviarCorreoFinanzasComprobacionEnviada(
            Empleados usuarioFinanzas,
            ComprobacionesViaje comprobacion,
            Empleados empleado,
            int gastosConDocumentos,
            int totalGastos,
            decimal montoTotal)
        {
            try
            {
                _queue.Enqueue(new Services.NotificationItem
                {
                    ToEmail = usuarioFinanzas.Email,
                    Subject = $"Nueva Comprobación Enviada - {comprobacion.CodigoComprobacion}",
                    TemplateName = "/Views/Emails/ComprobacionEnviadaFinanzas.cshtml",
                    Model = new
                    {
                        NombreFinanzas = $"{usuarioFinanzas.Nombre} {usuarioFinanzas.Apellidos}",
                        NombreEmpleado = $"{empleado?.Nombre} {empleado?.Apellidos}",
                        CodigoComprobacion = comprobacion.CodigoComprobacion,
                        FechaComprobacion = comprobacion.FechaComprobacion,
                        FechaEnvio = DateTime.Now,
                        MontoTotal = montoTotal,
                        MontoTotalFormateado = montoTotal.ToString("C", new System.Globalization.CultureInfo("es-MX")),
                        GastosCompletos = gastosConDocumentos,
                        TotalGastos = totalGastos,
                        PorcentajeCompletos = totalGastos > 0 ? ((double)gastosConDocumentos / totalGastos * 100) : 0,
                        EnlaceRevisar = Url.Action("DetallesFactura", "Finanzas", new { id = comprobacion.Id },
                            protocol: HttpContext.Request.Scheme),
                        EnlaceDashboard = Url.Action("Facturas", "Finanzas", null, protocol: HttpContext.Request.Scheme)
                    }
                });

                _logger.LogInformation("Correo encolado para {Email} para comprobación {CodigoComprobacion}",
                    usuarioFinanzas.Email, comprobacion.CodigoComprobacion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al encolar correo a {Email}", usuarioFinanzas.Email);
            }
        }

        // ENVÍO DE CORREO DE CORRECCIÓN APROBADA AL EMPLEADO
        private async Task EnviarCorreoCorreccionAprobada(ComprobacionesViaje comprobacion, string comentarios)
        {
            try
            {
                var empleado = comprobacion.SolicitudViaje.Empleado;
                if (empleado == null || string.IsNullOrEmpty(empleado.Email)) return;

                _queue.Enqueue(new Services.NotificationItem
                {
                    ToEmail = empleado.Email,
                    Subject = $"Corrección Aprobada - {comprobacion.CodigoComprobacion}",
                    TemplateName = "/Views/Emails/CorreccionAprobada.cshtml",
                    Model = new
                    {
                        EmpleadoNombre = $"{empleado.Nombre} {empleado.Apellidos}",
                        CodigoComprobacion = comprobacion.CodigoComprobacion,
                        Comentarios = comentarios,
                        FechaAprobacion = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                        Url = $"{Request.Scheme}://{Request.Host}/Comprobaciones/Index"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo de corrección aprobada");
            }
        }

        // ALMACENAMIENTO SEGURO DE ARCHIVOS EN ESTRUCTURA DE DIRECTORIOS JERÁRQUICA
        private async Task<string> GuardarArchivo(IFormFile archivo, string tipo, int comprobacionId, int gastoId)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "facturas", comprobacionId.ToString(), gastoId.ToString(), tipo);

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(archivo.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await archivo.CopyToAsync(stream);
            }

            return Path.Combine("facturas", comprobacionId.ToString(), gastoId.ToString(), tipo, fileName).Replace("\\", "/");
        }

        // ALMACENAMIENTO DE ARCHIVOS CORREGIDOS CON TIMESTAMP PARA CONTROL DE VERSIONES
        private async Task<string> GuardarArchivoCorregido(IFormFile archivo, string tipo, int comprobacionId, int gastoId)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "facturas", comprobacionId.ToString(), gastoId.ToString(), tipo, "corregido");

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var fileName = $"corregido_{timestamp}_{Path.GetFileName(archivo.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await archivo.CopyToAsync(stream);
            }

            return Path.Combine("facturas", comprobacionId.ToString(), gastoId.ToString(), tipo, "corregido", fileName).Replace("\\", "/");
        }

        // DESERIALIZACIÓN SEGURA DE JSON CON MANEJO DE EXCEPCIONES
        private T DeserializarJson<T>(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString))
                return default(T);

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };

                return JsonSerializer.Deserialize<T>(jsonString, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al deserializar JSON");
                return default(T);
            }
        }
    }
}