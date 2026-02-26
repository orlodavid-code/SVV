using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using SVV.Models;
using SVV.ViewModels;
using System.Drawing;
using System.Globalization;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SVV.Controllers
{
    // Controlador para reportes y dashboards financieros
    [Authorize(Roles = "FINANZAS,ADMIN,DIRECCION,RH")]
    [Route("[controller]")]
    [ApiController]
    public class ReportesController : Controller
    {
        private readonly SvvContext _context;
        private readonly ILogger<ReportesController> _logger;

        // Constructor con inyección de dependencias
        public ReportesController(SvvContext context, ILogger<ReportesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Dashboard principal con múltiples rutas de acceso
        [Authorize(Roles = "FINANZAS,ADMIN,DIRECCION,RH")]
        [HttpGet("Finanzas/Reportes")]
        [HttpGet("api/Finanzas/Reportes")]
        [HttpGet("Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                // Obtener información del usuario autenticado
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = userIdString != null ? int.Parse(userIdString) : 0;
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "EMPLEADO";

                var fechaActual = DateTime.Now;

                // Configurar fechas por defecto (mes actual)
                var fechaInicio = new DateTime(fechaActual.Year, fechaActual.Month, 1);
                var fechaFin = new DateTime(fechaActual.Year, fechaActual.Month,
                                           DateTime.DaysInMonth(fechaActual.Year, fechaActual.Month));

                var resumenData = await ObtenerResumenReal(fechaInicio, fechaFin, null, null);

                // Obtener lista de departamentos activos
                var departamentos = await _context.Empleados
                    .Where(e => e.Activo == true && !string.IsNullOrEmpty(e.Departamento))
                    .Select(e => e.Departamento!)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToListAsync();

                // Construir ViewModel para la vista
                var viewModel = new DashboardReportesViewModel
                {
                    Filtros = new FiltrosReportesViewModel
                    {
                        FechaInicio = fechaInicio,
                        FechaFin = fechaFin,
                        Departamento = "",
                        Escenario = ""
                    },
                    Resumen = resumenData != null ? new ResumenGeneralViewModel
                    {
                        TotalGastado = resumenData.TotalGastado,
                        TotalAnticipos = resumenData.TotalAnticipos,
                        TotalSolicitudes = resumenData.TotalSolicitudes,
                        TotalComprobaciones = resumenData.TotalComprobaciones,
                        PromedioAnticipo = resumenData.PromedioAnticipo,
                        SolicitudesAprobadas = resumenData.SolicitudesAprobadas,
                        SolicitudesPendientes = resumenData.SolicitudesPendientes
                    } : new ResumenGeneralViewModel(),
                    Departamentos = departamentos
                };

                ViewBag.Role = userRole;
                ViewBag.UserId = userId;

                return View("~/Views/Finanzas/Reportes.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar dashboard de reportes");
                return View("~/Views/Finanzas/Reportes.cshtml", new DashboardReportesViewModel
                {
                    Filtros = new FiltrosReportesViewModel
                    {
                        FechaInicio = DateTime.Now.AddMonths(-1),
                        FechaFin = DateTime.Now
                    },
                    Resumen = new ResumenGeneralViewModel(),
                    Departamentos = new List<string>()
                });
            }
        }

        // Método privado para obtener resumen de datos con filtros aplicados
        private async Task<dynamic> ObtenerResumenReal(
            DateTime? fechaInicio,
            DateTime? fechaFin,
            string? departamento,
            string? escenario)
        {
            // Consultar comprobaciones con filtros aplicados
            var comprobaciones = await _context.ComprobacionesViaje
                .Include(c => c.SolicitudViaje)
                .ThenInclude(s => s.Empleado)
                .Where(c => c.FechaComprobacion.HasValue)
                .ApplyFechasFilter(fechaInicio, fechaFin, c => c.FechaComprobacion)
                .ApplyDepartamentoFilter(departamento, c => c.SolicitudViaje.Empleado.Departamento)
                .ApplyEscenarioFilter(escenario, c => c.EscenarioLiquidacion)
                .ToListAsync();

            // Consultar anticipos con filtros aplicados
            var anticipos = await _context.Anticipos
                .Include(a => a.SolicitudViaje)
                .ThenInclude(s => s.Empleado)
                .Where(a => a.FechaAutorizacion.HasValue)
                .ApplyFechasFilter(fechaInicio, fechaFin, a => a.FechaAutorizacion)
                .ApplyDepartamentoFilter(departamento, a => a.SolicitudViaje.Empleado.Departamento)
                .ToListAsync();

            // Consultar solicitudes con filtros aplicados
            var solicitudes = await _context.SolicitudesViajes
                .Include(s => s.Empleado)
                .Where(s => s.CreatedAt.HasValue)
                .ApplyFechasFilter(fechaInicio, fechaFin, s => s.CreatedAt)
                .ApplyDepartamentoFilter(departamento, s => s.Empleado.Departamento)
                .ToListAsync();

            // Calcular KPIs del sistema
            var totalGastado = comprobaciones.Sum(c => c.TotalGastosComprobados ?? 0);
            var totalAnticipos = anticipos.Sum(a => a.MontoAutorizado);
            var totalSolicitudes = solicitudes.Count;
            var totalComprobaciones = comprobaciones.Count;
            var promedioAnticipo = anticipos.Any() ? anticipos.Average(a => a.MontoAutorizado) : 0;
            var solicitudesAprobadas = solicitudes.Count(s => s.EstadoId == 9);
            var solicitudesPendientes = solicitudes.Count(s => s.EstadoId == 1 || s.EstadoId == 10);

            return new
            {
                TotalGastado = Math.Round(totalGastado, 2),
                TotalAnticipos = Math.Round(totalAnticipos ?? 0, 2),
                TotalSolicitudes = totalSolicitudes,
                TotalComprobaciones = totalComprobaciones,
                PromedioAnticipo = Math.Round(promedioAnticipo ?? 0, 2),
                SolicitudesAprobadas = solicitudesAprobadas,
                SolicitudesPendientes = solicitudesPendientes
            };
        }

        // API para obtener resumen general con filtros
        [HttpGet("api/GetResumenGeneral")]
        public async Task<IActionResult> GetResumenGeneral(
            [FromQuery] string? fechaInicio = null,
            [FromQuery] string? fechaFin = null,
            [FromQuery] string? departamento = null,
            [FromQuery] string? escenario = null)
        {
            try
            {
                // Parsear fechas de los parámetros
                DateTime? inicio = null;
                DateTime? fin = null;

                if (!string.IsNullOrEmpty(fechaInicio))
                    inicio = DateTime.Parse(fechaInicio);

                if (!string.IsNullOrEmpty(fechaFin))
                    fin = DateTime.Parse(fechaFin).AddDays(1).AddTicks(-1);

                // Validar que fecha inicio no sea mayor que fecha fin
                if (inicio.HasValue && fin.HasValue && inicio > fin)
                {
                    return BadRequest(new { error = "La fecha de inicio no puede ser mayor a la fecha fin" });
                }

                // Obtener datos con filtros aplicados
                var comprobaciones = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                    .ThenInclude(s => s.Empleado)
                    .Where(c => c.FechaComprobacion.HasValue)
                    .ApplyFechasFilter(inicio, fin, c => c.FechaComprobacion)
                    .ApplyDepartamentoFilter(departamento, c => c.SolicitudViaje.Empleado.Departamento)
                    .ApplyEscenarioFilter(escenario, c => c.EscenarioLiquidacion)
                    .ToListAsync();

                var anticipos = await _context.Anticipos
                    .Include(a => a.SolicitudViaje)
                    .ThenInclude(s => s.Empleado)
                    .Where(a => a.FechaAutorizacion.HasValue)
                    .ApplyFechasFilter(inicio, fin, a => a.FechaAutorizacion)
                    .ApplyDepartamentoFilter(departamento, a => a.SolicitudViaje.Empleado.Departamento)
                    .ToListAsync();

                var solicitudes = await _context.SolicitudesViajes
                    .Include(s => s.Empleado)
                    .Where(s => s.CreatedAt.HasValue)
                    .ApplyFechasFilter(inicio, fin, s => s.CreatedAt)
                    .ApplyDepartamentoFilter(departamento, s => s.Empleado.Departamento)
                    .ToListAsync();

                // Calcular métricas financieras
                var totalGastado = comprobaciones.Sum(c => c.TotalGastosComprobados ?? 0);
                var totalAnticipos = anticipos.Sum(a => a.MontoAutorizado);
                var totalSolicitudes = solicitudes.Count;
                var totalComprobaciones = comprobaciones.Count;
                var promedioAnticipo = anticipos.Any() ? anticipos.Average(a => a.MontoAutorizado) : 0;
                var promedioGasto = comprobaciones.Any() ? comprobaciones.Average(c => c.TotalGastosComprobados ?? 0) : 0;
                var eficiencia = totalAnticipos > 0 ? (totalGastado / totalAnticipos * 100) : 0;

                return Ok(new
                {
                    TotalGastado = Math.Round(totalGastado, 2),
                    TotalAnticipos = Math.Round(totalAnticipos ?? 0, 2),
                    TotalSolicitudes = totalSolicitudes,
                    TotalComprobaciones = totalComprobaciones,
                    PromedioAnticipo = Math.Round(promedioAnticipo ?? 0, 2),
                    PromedioGasto = Math.Round(promedioGasto, 2),
                    Eficiencia = Math.Round(eficiencia ?? 0, 1),
                    TotalEmpleados = await _context.Empleados.CountAsync(e => e.Activo == true),
                    TotalProyectos = solicitudes.Select(s => s.NombreProyecto).Distinct().Count(),
                    TasaAprobacion = totalSolicitudes > 0 ? Math.Round((decimal)solicitudes.Count(s => s.EstadoId == 9) / totalSolicitudes * 100, 1) : 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener resumen general");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        // API para obtener gastos agrupados por departamento
        [HttpGet("api/GetGastosPorDepartamento")]
        public async Task<IActionResult> GetGastosPorDepartamento(
            [FromQuery] string? fechaInicio = null,
            [FromQuery] string? fechaFin = null,
            [FromQuery] string? departamento = null,
            [FromQuery] string? escenario = null)
        {
            try
            {
                DateTime? inicio = null;
                DateTime? fin = null;

                if (!string.IsNullOrEmpty(fechaInicio))
                    inicio = DateTime.Parse(fechaInicio);

                if (!string.IsNullOrEmpty(fechaFin))
                    fin = DateTime.Parse(fechaFin).AddDays(1).AddTicks(-1);

                // Construir consulta con joins
                var query = from comp in _context.ComprobacionesViaje
                            join sol in _context.SolicitudesViajes on comp.SolicitudViajeId equals sol.Id
                            join emp in _context.Empleados on sol.EmpleadoId equals emp.Id
                            where emp.Activo == true
                            select new { comp, emp };

                // Aplicar filtros manualmente
                if (inicio.HasValue)
                    query = query.Where(x => x.comp.FechaComprobacion >= inicio.Value);

                if (fin.HasValue)
                    query = query.Where(x => x.comp.FechaComprobacion <= fin.Value);

                if (!string.IsNullOrEmpty(departamento) && departamento != "TODOS")
                    query = query.Where(x => x.emp.Departamento == departamento);

                // Manejo de filtro de escenario con normalización
                if (!string.IsNullOrEmpty(escenario) && escenario != "TODOS")
                {
                    var escenarioNormalizado = escenario.Trim().ToUpper();
                    query = query.Where(x =>
                        (x.comp.EscenarioLiquidacion != null ?
                         x.comp.EscenarioLiquidacion.Trim().ToUpper() : "") == escenarioNormalizado);
                }

                // Agrupar resultados por departamento
                var resultados = await query
                    .GroupBy(x => x.emp.Departamento ?? "Sin Departamento")
                    .Select(g => new
                    {
                        Departamento = g.Key,
                        TotalGastado = g.Sum(x => x.comp.TotalGastosComprobados ?? 0),
                        CantidadComprobaciones = g.Count(),
                        PromedioGasto = g.Average(x => x.comp.TotalGastosComprobados ?? 0),
                        AnticipoTotal = g.Sum(x => x.comp.TotalAnticipo ?? 0),
                        DiferenciaTotal = g.Sum(x => x.comp.Diferencia ?? 0),
                        Escenario = g.FirstOrDefault().comp.EscenarioLiquidacion
                    })
                    .OrderByDescending(x => x.TotalGastado)
                    .Take(15)
                    .ToListAsync();

                // Calcular porcentajes de participación
                var totalGeneral = resultados.Sum(x => x.TotalGastado);
                var resultadosConPorcentaje = resultados.Select(r => new
                {
                    r.Departamento,
                    r.TotalGastado,
                    r.CantidadComprobaciones,
                    r.PromedioGasto,
                    r.AnticipoTotal,
                    r.DiferenciaTotal,
                    PorcentajeDelTotal = totalGeneral > 0 ? Math.Round((r.TotalGastado / totalGeneral) * 100, 2) : 0,
                    Color = "#5cc87b"
                }).ToList();

                return Ok(resultadosConPorcentaje);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener gastos por departamento");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        // API para obtener los anticipos más altos
        [HttpGet("api/GetAnticiposMayores")]
        public async Task<IActionResult> GetAnticiposMayores(
            [FromQuery] string? fechaInicio = null,
            [FromQuery] string? fechaFin = null,
            [FromQuery] string? departamento = null,
            [FromQuery] int top = 10)
        {
            try
            {
                DateTime? inicio = null;
                DateTime? fin = null;

                if (!string.IsNullOrEmpty(fechaInicio))
                    inicio = DateTime.Parse(fechaInicio);

                if (!string.IsNullOrEmpty(fechaFin))
                    fin = DateTime.Parse(fechaFin).AddDays(1).AddTicks(-1);

                // Consulta para obtener anticipos con información de empleado
                var query = from anticipo in _context.Anticipos
                            join solicitud in _context.SolicitudesViajes on anticipo.SolicitudViajeId equals solicitud.Id
                            join empleado in _context.Empleados on solicitud.EmpleadoId equals empleado.Id
                            where anticipo.FechaAutorizacion.HasValue && empleado.Activo == true
                            select new { anticipo, solicitud, empleado };

                query = query
                    .ApplyFechasFilter(inicio, fin, x => x.anticipo.FechaAutorizacion)
                    .ApplyDepartamentoFilter(departamento, x => x.empleado.Departamento);

                // Obtener top de anticipos por monto
                var anticipos = await query
                    .OrderByDescending(x => x.anticipo.MontoAutorizado)
                    .Take(top)
                    .Select(x => new
                    {
                        Id = x.anticipo.Id,
                        CodigoAnticipo = x.anticipo.CodigoAnticipo,
                        Empleado = $"{x.empleado.Nombre} {x.empleado.Apellidos}",
                        Departamento = x.empleado.Departamento,
                        MontoAnticipo = x.anticipo.MontoAutorizado ?? 0,
                        MontoSolicitado = x.anticipo.MontoSolicitado,
                        Diferencia = (x.anticipo.MontoAutorizado ?? 0) - x.anticipo.MontoSolicitado,
                        FechaAutorizacion = x.anticipo.FechaAutorizacion,
                        Proyecto = x.solicitud.NombreProyecto,
                        Destino = x.solicitud.Destino
                    })
                    .ToListAsync();

                return Ok(anticipos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener anticipos mayores");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        // API para obtener estadísticas por escenario de liquidación
        [HttpGet("api/GetEstadisticasPorEscenario")]
        public async Task<IActionResult> GetEstadisticasPorEscenario(
            [FromQuery] string? fechaInicio = null,
            [FromQuery] string? fechaFin = null,
            [FromQuery] string? departamento = null,
            [FromQuery] string? escenario = null)
        {
            try
            {
                DateTime? inicio = null;
                DateTime? fin = null;

                if (!string.IsNullOrEmpty(fechaInicio))
                    inicio = DateTime.Parse(fechaInicio);

                if (!string.IsNullOrEmpty(fechaFin))
                    fin = DateTime.Parse(fechaFin).AddDays(1).AddTicks(-1);

                // Consulta base para comprobaciones
                var query = _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .AsQueryable();

                // Aplicar filtros manualmente sin usar extension methods
                if (inicio.HasValue)
                    query = query.Where(c => c.FechaComprobacion >= inicio.Value);

                if (fin.HasValue)
                    query = query.Where(c => c.FechaComprobacion <= fin.Value);

                if (!string.IsNullOrEmpty(departamento) && departamento != "TODOS")
                    query = query.Where(c => c.SolicitudViaje.Empleado.Departamento == departamento);

                if (!string.IsNullOrEmpty(escenario) && escenario != "TODOS")
                    query = query.Where(c => c.EscenarioLiquidacion == escenario);

                // Agrupar por escenario y contar ocurrencias
                var resultados = await query
                    .Where(c => c.EscenarioLiquidacion != null)
                    .GroupBy(c => c.EscenarioLiquidacion)
                    .Select(g => new
                    {
                        Escenario = g.Key,
                        Cantidad = g.Count()
                    })
                    .ToListAsync();

                // Aplicar traducciones en memoria para mejor rendimiento
                var resultadosTraducidos = resultados.Select(r => new
                {
                    r.Escenario,
                    EscenarioTraducido = TraducirEscenario(r.Escenario),
                    r.Cantidad,
                    Color = ObtenerColorPorEscenario(r.Escenario)
                }).ToList();

                return Ok(resultadosTraducidos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetEstadisticasPorEscenario");
                return StatusCode(500, new { error = "Error interno del servidor: " + ex.Message });
            }
        }

        // API para obtener distribución de comprobaciones por estado
        [HttpGet("api/GetComprobacionesPorEstado")]
        public async Task<IActionResult> GetComprobacionesPorEstado(
            [FromQuery] string? fechaInicio = null,
            [FromQuery] string? fechaFin = null,
            [FromQuery] string? departamento = null,
            [FromQuery] string? escenario = null)
        {
            try
            {
                DateTime? inicio = null;
                DateTime? fin = null;

                if (!string.IsNullOrEmpty(fechaInicio))
                    inicio = DateTime.Parse(fechaInicio);

                if (!string.IsNullOrEmpty(fechaFin))
                    fin = DateTime.Parse(fechaFin).AddDays(1).AddTicks(-1);

                // Consulta incluyendo estados de comprobación
                var query = _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.EstadoComprobacion)
                    .AsQueryable();

                // Aplicar filtros
                if (inicio.HasValue)
                    query = query.Where(c => c.FechaComprobacion >= inicio.Value);

                if (fin.HasValue)
                    query = query.Where(c => c.FechaComprobacion <= fin.Value);

                if (!string.IsNullOrEmpty(departamento) && departamento != "TODOS")
                    query = query.Where(c => c.SolicitudViaje.Empleado.Departamento == departamento);

                if (!string.IsNullOrEmpty(escenario) && escenario != "TODOS")
                    query = query.Where(c => c.EscenarioLiquidacion == escenario);

                // Obtener datos sin agrupar primero
                var datos = await query
                    .Where(c => c.EstadoComprobacion != null && c.EstadoComprobacion.Codigo != null)
                    .Select(c => new
                    {
                        EstadoCodigo = c.EstadoComprobacion.Codigo,
                        EstadoDescripcion = c.EstadoComprobacion.Descripcion
                    })
                    .ToListAsync();

                // Agrupar en memoria para mejor control
                var resultados = datos
                    .GroupBy(c => c.EstadoCodigo)
                    .Select(g => new
                    {
                        Estado = g.Key,
                        EstadoDescripcion = g.First().EstadoDescripcion ?? TraducirEstado(g.Key),
                        Cantidad = g.Count(),
                        Color = ObtenerColorPorEstado(g.Key)
                    })
                    .ToList();

                return Ok(resultados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetComprobacionesPorEstado");
                return StatusCode(500, new { error = "Error interno del servidor: " + ex.Message });
            }
        }

        // API para obtener gastos mensuales con análisis de tendencias
        [HttpGet("api/GetGastosMensuales")]
        public async Task<IActionResult> GetGastosMensuales(
            [FromQuery] string? fechaInicio = null,
            [FromQuery] string? fechaFin = null,
            [FromQuery] string? departamento = null)
        {
            try
            {
                DateTime? inicio = null;
                DateTime? fin = null;

                if (!string.IsNullOrEmpty(fechaInicio))
                    inicio = DateTime.Parse(fechaInicio);

                if (!string.IsNullOrEmpty(fechaFin))
                    fin = DateTime.Parse(fechaFin).AddDays(1).AddTicks(-1);

                // Establecer período por defecto (últimos 12 meses)
                if (!inicio.HasValue || !fin.HasValue)
                {
                    fin = DateTime.Now;
                    inicio = fin.Value.AddMonths(-12);
                }

                if (inicio.HasValue && fin.HasValue && inicio > fin)
                {
                    return BadRequest(new { error = "La fecha de inicio no puede ser mayor a la fecha fin" });
                }

                var query = from comp in _context.ComprobacionesViaje
                            join sol in _context.SolicitudesViajes on comp.SolicitudViajeId equals sol.Id
                            join emp in _context.Empleados on sol.EmpleadoId equals emp.Id
                            where emp.Activo == true && comp.FechaComprobacion.HasValue
                            select new { comp, emp };

                query = query
                    .ApplyFechasFilter(inicio, fin, x => x.comp.FechaComprobacion)
                    .ApplyDepartamentoFilter(departamento, x => x.emp.Departamento);

                // Agrupar por año y mes
                var gastosMensuales = await query
                    .GroupBy(x => new
                    {
                        x.comp.FechaComprobacion.Value.Year,
                        x.comp.FechaComprobacion.Value.Month
                    })
                    .Select(g => new
                    {
                        Año = g.Key.Year,
                        Mes = g.Key.Month,
                        MesNombre = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                        TotalGastado = g.Sum(x => x.comp.TotalGastosComprobados ?? 0),
                        CantidadComprobaciones = g.Count(),
                        PromedioMensual = g.Average(x => x.comp.TotalGastosComprobados ?? 0),
                        AnticipoTotal = g.Sum(x => x.comp.TotalAnticipo ?? 0),
                        DiferenciaTotal = g.Sum(x => x.comp.Diferencia ?? 0)
                    })
                    .OrderBy(x => x.Año)
                    .ThenBy(x => x.Mes)
                    .ToListAsync();

                // Generar todos los meses en el rango (incluso meses sin datos)
                var fechaInicioDate = inicio.Value;
                var fechaFinDate = fin.Value;
                var todosLosMeses = new List<object>();

                for (var fecha = new DateTime(fechaInicioDate.Year, fechaInicioDate.Month, 1);
                     fecha <= fechaFinDate;
                     fecha = fecha.AddMonths(1))
                {
                    var datosMes = gastosMensuales
                        .FirstOrDefault(g => g.Año == fecha.Year && g.Mes == fecha.Month);

                    todosLosMeses.Add(new
                    {
                        Año = fecha.Year,
                        Mes = fecha.Month,
                        MesNombre = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(fecha.Month),
                        TotalGastado = datosMes?.TotalGastado ?? 0,
                        CantidadComprobaciones = datosMes?.CantidadComprobaciones ?? 0,
                        PromedioMensual = datosMes?.PromedioMensual ?? 0,
                        AnticipoTotal = datosMes?.AnticipoTotal ?? 0,
                        DiferenciaTotal = datosMes?.DiferenciaTotal ?? 0
                    });
                }

                // Calcular estadísticas anuales
                var totalAnual = todosLosMeses.Sum(m => (decimal)((dynamic)m).TotalGastado);
                var promedioAnual = todosLosMeses.Average(m => (decimal)((dynamic)m).TotalGastado);
                var mesMaximo = todosLosMeses
                    .OrderByDescending(m => (decimal)((dynamic)m).TotalGastado)
                    .FirstOrDefault();
                var mesMinimo = todosLosMeses
                    .Where(m => (decimal)((dynamic)m).TotalGastado > 0)
                    .OrderBy(m => (decimal)((dynamic)m).TotalGastado)
                    .FirstOrDefault();

                return Ok(new
                {
                    GastosMensuales = todosLosMeses,
                    Estadisticas = new
                    {
                        TotalAnual = Math.Round(totalAnual, 2),
                        PromedioAnual = Math.Round(promedioAnual, 2),
                        MesMaximo = mesMaximo,
                        MesMinimo = mesMinimo
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener gastos mensuales");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        // API para obtener lista de departamentos con métricas
        [HttpGet("api/GetDepartamentos")]
        public async Task<IActionResult> GetDepartamentos()
        {
            try
            {
                // Obtener departamentos únicos de empleados activos
                var departamentos = await _context.Empleados
                    .Where(e => e.Activo == true && !string.IsNullOrEmpty(e.Departamento))
                    .Select(e => e.Departamento)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToListAsync();

                var resultado = departamentos.Select(d => new
                {
                    Id = d,
                    Nombre = d,
                    CantidadEmpleados = _context.Empleados.Count(e => e.Departamento == d && e.Activo == true),
                    Color = "#5cc87b"
                }).ToList();

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener departamentos");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        // API para obtener ranking de empleados con mayores gastos
        [HttpGet("api/GetTopEmpleados")]
        public async Task<IActionResult> GetTopEmpleados(
            [FromQuery] string? fechaInicio = null,
            [FromQuery] string? fechaFin = null,
            [FromQuery] string? departamento = null,
            [FromQuery] int top = 10)
        {
            try
            {
                DateTime? inicio = null;
                DateTime? fin = null;

                if (!string.IsNullOrEmpty(fechaInicio))
                    inicio = DateTime.Parse(fechaInicio);

                if (!string.IsNullOrEmpty(fechaFin))
                    fin = DateTime.Parse(fechaFin).AddDays(1).AddTicks(-1);

                var query = from comp in _context.ComprobacionesViaje
                            join sol in _context.SolicitudesViajes on comp.SolicitudViajeId equals sol.Id
                            join emp in _context.Empleados on sol.EmpleadoId equals emp.Id
                            where emp.Activo == true
                            select new { comp, emp };

                query = query
                    .ApplyFechasFilter(inicio, fin, x => x.comp.FechaComprobacion)
                    .ApplyDepartamentoFilter(departamento, x => x.emp.Departamento);

                // Obtener ranking de empleados por gastos totales
                var topEmpleados = await query
                    .GroupBy(x => new { x.emp.Id, x.emp.Nombre, x.emp.Apellidos, x.emp.Departamento })
                    .Select(g => new
                    {
                        Empleado = $"{g.Key.Nombre} {g.Key.Apellidos}",
                        Departamento = g.Key.Departamento,
                        TotalGastado = g.Sum(x => x.comp.TotalGastosComprobados ?? 0),
                        CantidadViajes = g.Select(x => x.comp.SolicitudViajeId).Distinct().Count(),
                        PromedioGasto = g.Average(x => x.comp.TotalGastosComprobados ?? 0),
                        AnticipoTotal = g.Sum(x => x.comp.TotalAnticipo ?? 0),
                        DiferenciaTotal = g.Sum(x => x.comp.Diferencia ?? 0),
                        Eficiencia = g.Sum(x => x.comp.TotalAnticipo ?? 0) > 0 ?
                            (g.Sum(x => x.comp.TotalGastosComprobados ?? 0) / g.Sum(x => x.comp.TotalAnticipo ?? 0) * 100) : 0
                    })
                    .OrderByDescending(x => x.TotalGastado)
                    .Take(top)
                    .ToListAsync();

                return Ok(topEmpleados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener top empleados");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        // API para obtener distribución de gastos por categoría
        [HttpGet("api/GetGastosPorCategoria")]
        public async Task<IActionResult> GetGastosPorCategoria(
            [FromQuery] string? fechaInicio = null,
            [FromQuery] string? fechaFin = null,
            [FromQuery] string? departamento = null)
        {
            try
            {
                DateTime? inicio = null;
                DateTime? fin = null;

                if (!string.IsNullOrEmpty(fechaInicio))
                    inicio = DateTime.Parse(fechaInicio);

                if (!string.IsNullOrEmpty(fechaFin))
                    fin = DateTime.Parse(fechaFin).AddDays(1).AddTicks(-1);

                // Consulta con joins para categorías de gasto
                var query = from gasto in _context.GastosReales
                            join cat in _context.CategoriasGasto on gasto.CategoriaGastoId equals cat.Id
                            join sol in _context.SolicitudesViajes on gasto.SolicitudViajeId equals sol.Id
                            join emp in _context.Empleados on sol.EmpleadoId equals emp.Id
                            where emp.Activo == true
                            select new { gasto, cat, emp };

                query = query
                    .ApplyFechasFilter(inicio, fin, x => x.gasto.FechaGasto)
                    .ApplyDepartamentoFilter(departamento, x => x.emp.Departamento);

                // Agrupar por categoría de gasto
                var gastosPorCategoria = await query
                    .GroupBy(x => new { x.cat.Id, x.cat.Nombre, x.cat.Codigo })
                    .Select(g => new
                    {
                        CategoriaId = g.Key.Id,
                        Categoria = g.Key.Nombre ?? "Sin Categoría",
                        Codigo = g.Key.Codigo,
                        TotalGastado = g.Sum(x => x.gasto.Monto),
                        CantidadGastos = g.Count(),
                        PromedioGasto = g.Average(x => x.gasto.Monto),
                        MaximoGasto = g.Max(x => x.gasto.Monto),
                        MinimoGasto = g.Min(x => x.gasto.Monto),
                        RequiereFactura = g.First().cat.RequiereFactura,
                        AplicaLimiteDiario = g.First().cat.AplicaLimiteDiario
                    })
                    .OrderByDescending(x => x.TotalGastado)
                    .Take(20)
                    .ToListAsync();

                // Calcular porcentajes de participación por categoría
                var totalGeneral = gastosPorCategoria.Sum(x => x.TotalGastado);
                var categoriasConPorcentaje = gastosPorCategoria.Select(c => new
                {
                    c.CategoriaId,
                    c.Categoria,
                    c.Codigo,
                    c.TotalGastado,
                    c.CantidadGastos,
                    c.PromedioGasto,
                    c.MaximoGasto,
                    c.MinimoGasto,
                    c.RequiereFactura,
                    c.AplicaLimiteDiario,
                    PorcentajeDelTotal = totalGeneral > 0 ? Math.Round((c.TotalGastado / totalGeneral) * 100, 2) : 0,
                    Color = GenerarColorCategoria(c.CategoriaId),
                    Icono = ObtenerIconoCategoria(c.Codigo)
                }).ToList();

                return Ok(categoriasConPorcentaje);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener gastos por categoría");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        // API para obtener gastos agrupados por proyecto
        [HttpGet("api/GetGastosPorProyecto")]
        public async Task<IActionResult> GetGastosPorProyecto(
            [FromQuery] string? fechaInicio = null,
            [FromQuery] string? fechaFin = null,
            [FromQuery] string? departamento = null)
        {
            try
            {
                DateTime? inicio = null;
                DateTime? fin = null;

                if (!string.IsNullOrEmpty(fechaInicio))
                    inicio = DateTime.Parse(fechaInicio);

                if (!string.IsNullOrEmpty(fechaFin))
                    fin = DateTime.Parse(fechaFin).AddDays(1).AddTicks(-1);

                // Consulta con left join para incluir proyectos sin comprobaciones
                var query = from sol in _context.SolicitudesViajes
                            join comp in _context.ComprobacionesViaje on sol.Id equals comp.SolicitudViajeId into compGroup
                            from comp in compGroup.DefaultIfEmpty()
                            join emp in _context.Empleados on sol.EmpleadoId equals emp.Id
                            where !string.IsNullOrEmpty(sol.NombreProyecto) && emp.Activo == true
                            select new { sol, comp, emp };

                query = query
                    .ApplyFechasFilter(inicio, fin, x => x.sol.CreatedAt)
                    .ApplyDepartamentoFilter(departamento, x => x.emp.Departamento);

                // Agrupar por proyecto
                var gastosPorProyecto = await query
                    .GroupBy(x => x.sol.NombreProyecto)
                    .Select(g => new
                    {
                        Proyecto = g.Key,
                        TotalGastado = g.Sum(x => x.comp != null ? x.comp.TotalGastosComprobados ?? 0 : 0),
                        CantidadViajes = g.Select(x => x.sol.Id).Distinct().Count(),
                        CantidadEmpleados = g.Select(x => x.sol.EmpleadoId).Distinct().Count(),
                        PresupuestoPromedio = g.Average(x => x.sol.MontoAnticipo ?? 0),
                        AnticipoTotal = g.Sum(x => x.sol.MontoAnticipo ?? 0),
                        DiferenciaTotal = g.Sum(x => x.comp != null ? x.comp.Diferencia ?? 0 : 0),
                        Eficiencia = g.Sum(x => x.sol.MontoAnticipo ?? 0) > 0 ?
                            (g.Sum(x => x.comp != null ? x.comp.TotalGastosComprobados ?? 0 : 0) / g.Sum(x => x.sol.MontoAnticipo ?? 0) * 100) : 0,
                        Destinos = g.Select(x => x.sol.Destino).Distinct().Take(5).ToList()
                    })
                    .OrderByDescending(x => x.TotalGastado)
                    .Take(15)
                    .ToListAsync();

                // Calcular porcentajes de participación por proyecto
                var totalGeneral = gastosPorProyecto.Sum(x => x.TotalGastado);
                var proyectosConPorcentaje = gastosPorProyecto.Select(p => new
                {
                    p.Proyecto,
                    p.TotalGastado,
                    p.CantidadViajes,
                    p.CantidadEmpleados,
                    p.PresupuestoPromedio,
                    p.AnticipoTotal,
                    p.DiferenciaTotal,
                    p.Eficiencia,
                    p.Destinos,
                    PorcentajeDelTotal = totalGeneral > 0 ? Math.Round((p.TotalGastado / totalGeneral) * 100, 2) : 0,
                    Color = GenerarColorProyecto(p.Proyecto),
                    Icono = "fas fa-project-diagram"
                }).ToList();

                return Ok(proyectosConPorcentaje);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener gastos por proyecto");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        // API para comparativa anual entre dos años
        [HttpGet("api/GetComparativaAnual")]
        public async Task<IActionResult> GetComparativaAnual(
            [FromQuery] int? año1 = null,
            [FromQuery] int? año2 = null,
            [FromQuery] string? departamento = null)
        {
            try
            {
                var añoActual = DateTime.Now.Year;
                var añoComparacion1 = año1 ?? añoActual - 1;
                var añoComparacion2 = año2 ?? añoActual;

                // Obtener datos para año 1
                var comprobacionesAño1 = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Where(c => c.FechaComprobacion.HasValue && c.FechaComprobacion.Value.Year == añoComparacion1)
                    .ApplyDepartamentoFilter(departamento, c => c.SolicitudViaje.Empleado.Departamento)
                    .ToListAsync();

                var solicitudesAño1 = await _context.SolicitudesViajes
                    .Include(s => s.Empleado)
                    .Where(s => s.CreatedAt.HasValue && s.CreatedAt.Value.Year == añoComparacion1)
                    .ApplyDepartamentoFilter(departamento, s => s.Empleado.Departamento)
                    .ToListAsync();

                var anticiposAño1 = await _context.Anticipos
                    .Include(a => a.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Where(a => a.FechaAutorizacion.HasValue && a.FechaAutorizacion.Value.Year == añoComparacion1)
                    .ApplyDepartamentoFilter(departamento, a => a.SolicitudViaje.Empleado.Departamento)
                    .ToListAsync();

                // Obtener datos para año 2
                var comprobacionesAño2 = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Where(c => c.FechaComprobacion.HasValue && c.FechaComprobacion.Value.Year == añoComparacion2)
                    .ApplyDepartamentoFilter(departamento, c => c.SolicitudViaje.Empleado.Departamento)
                    .ToListAsync();

                var solicitudesAño2 = await _context.SolicitudesViajes
                    .Include(s => s.Empleado)
                    .Where(s => s.CreatedAt.HasValue && s.CreatedAt.Value.Year == añoComparacion2)
                    .ApplyDepartamentoFilter(departamento, s => s.Empleado.Departamento)
                    .ToListAsync();

                var anticiposAño2 = await _context.Anticipos
                    .Include(a => a.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Where(a => a.FechaAutorizacion.HasValue && a.FechaAutorizacion.Value.Year == añoComparacion2)
                    .ApplyDepartamentoFilter(departamento, a => a.SolicitudViaje.Empleado.Departamento)
                    .ToListAsync();

                // Calcular métricas de comparación
                var comparativa = new
                {
                    Año1 = añoComparacion1,
                    Año2 = añoComparacion2,

                    // Comparativa de gastos
                    GastosAño1 = comprobacionesAño1.Sum(c => c.TotalGastosComprobados ?? 0),
                    GastosAño2 = comprobacionesAño2.Sum(c => c.TotalGastosComprobados ?? 0),
                    CrecimientoGastos = comprobacionesAño1.Sum(c => c.TotalGastosComprobados ?? 0) > 0 ?
                        ((comprobacionesAño2.Sum(c => c.TotalGastosComprobados ?? 0) - comprobacionesAño1.Sum(c => c.TotalGastosComprobados ?? 0)) /
                         comprobacionesAño1.Sum(c => c.TotalGastosComprobados ?? 0) * 100) : 0,

                    // Comparativa de solicitudes
                    SolicitudesAño1 = solicitudesAño1.Count,
                    SolicitudesAño2 = solicitudesAño2.Count,
                    CrecimientoSolicitudes = solicitudesAño1.Count > 0 ?
                        ((solicitudesAño2.Count - solicitudesAño1.Count) / (double)solicitudesAño1.Count * 100) : 0,

                    // Comparativa de anticipos
                    AnticiposAño1 = anticiposAño1.Sum(a => a.MontoAutorizado),
                    AnticiposAño2 = anticiposAño2.Sum(a => a.MontoAutorizado),
                    CrecimientoAnticipos = anticiposAño1.Sum(a => a.MontoAutorizado) > 0 ?
                        ((anticiposAño2.Sum(a => a.MontoAutorizado) - anticiposAño1.Sum(a => a.MontoAutorizado)) /
                         anticiposAño1.Sum(a => a.MontoAutorizado) * 100) : 0,

                    // Comparativa de comprobaciones
                    ComprobacionesAño1 = comprobacionesAño1.Count,
                    ComprobacionesAño2 = comprobacionesAño2.Count,
                    CrecimientoComprobaciones = comprobacionesAño1.Count > 0 ?
                        ((comprobacionesAño2.Count - comprobacionesAño1.Count) / (double)comprobacionesAño1.Count * 100) : 0,

                    // Comparativa de eficiencia
                    EficienciaAño1 = anticiposAño1.Sum(a => a.MontoAutorizado) > 0 ?
                        (comprobacionesAño1.Sum(c => c.TotalGastosComprobados ?? 0) / anticiposAño1.Sum(a => a.MontoAutorizado) * 100) : 0,
                    EficienciaAño2 = anticiposAño2.Sum(a => a.MontoAutorizado) > 0 ?
                        (comprobacionesAño2.Sum(c => c.TotalGastosComprobados ?? 0) / anticiposAño2.Sum(a => a.MontoAutorizado) * 100) : 0,

                    // Comparativa de promedios
                    PromedioGastoAño1 = comprobacionesAño1.Any() ? comprobacionesAño1.Average(c => c.TotalGastosComprobados ?? 0) : 0,
                    PromedioGastoAño2 = comprobacionesAño2.Any() ? comprobacionesAño2.Average(c => c.TotalGastosComprobados ?? 0) : 0,
                    PromedioAnticipoAño1 = anticiposAño1.Any() ? anticiposAño1.Average(a => a.MontoAutorizado) : 0,
                    PromedioAnticipoAño2 = anticiposAño2.Any() ? anticiposAño2.Average(a => a.MontoAutorizado) : 0
                };

                // Preparar datos mensuales para gráfico comparativo
                var datosMensuales = new List<object>();
                for (int mes = 1; mes <= 12; mes++)
                {
                    var gastosAño1 = comprobacionesAño1
                        .Where(c => c.FechaComprobacion.Value.Month == mes)
                        .Sum(c => c.TotalGastosComprobados ?? 0);

                    var gastosAño2 = comprobacionesAño2
                        .Where(c => c.FechaComprobacion.Value.Month == mes)
                        .Sum(c => c.TotalGastosComprobados ?? 0);

                    datosMensuales.Add(new
                    {
                        Mes = mes,
                        MesNombre = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(mes),
                        GastosAño1 = gastosAño1,
                        GastosAño2 = gastosAño2
                    });
                }

                return Ok(new
                {
                    Comparativa = comparativa,
                    DatosMensuales = datosMensuales
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener comparativa anual");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        // API para obtener destinos más frecuentes
        [HttpGet("api/GetTopDestinos")]
        public async Task<IActionResult> GetTopDestinos(
            [FromQuery] string? fechaInicio = null,
            [FromQuery] string? fechaFin = null,
            [FromQuery] string? departamento = null,
            [FromQuery] int limite = 10)
        {
            try
            {
                DateTime? inicio = null;
                DateTime? fin = null;

                if (!string.IsNullOrEmpty(fechaInicio))
                    inicio = DateTime.Parse(fechaInicio);

                if (!string.IsNullOrEmpty(fechaFin))
                    fin = DateTime.Parse(fechaFin).AddDays(1).AddTicks(-1);

                var query = from sol in _context.SolicitudesViajes
                            join emp in _context.Empleados on sol.EmpleadoId equals emp.Id
                            join comp in _context.ComprobacionesViaje on sol.Id equals comp.SolicitudViajeId into compGroup
                            from comp in compGroup.DefaultIfEmpty()
                            where !string.IsNullOrEmpty(sol.Destino) && emp.Activo == true
                            select new { sol, emp, comp };

                query = query
                    .ApplyFechasFilter(inicio, fin, x => x.sol.CreatedAt)
                    .ApplyDepartamentoFilter(departamento, x => x.emp.Departamento);

                // Agrupar por destino y calcular métricas
                var destinos = await query
                    .GroupBy(x => x.sol.Destino)
                    .Select(g => new
                    {
                        Destino = g.Key,
                        CantidadViajes = g.Count(),
                        TotalPersonas = g.Sum(s => s.sol.NumeroPersonas ?? 0),
                        RequiereHospedaje = g.Count(s => s.sol.RequiereHospedaje == true),
                        PromedioDuracion = g.Average(s => (s.sol.FechaRegreso.ToDateTime(TimeOnly.MinValue) - s.sol.FechaSalida.ToDateTime(TimeOnly.MinValue)).TotalDays),
                        TotalGastado = g.Sum(s => s.comp != null ? s.comp.TotalGastosComprobados ?? 0 : 0),
                        Departamentos = g.Select(s => s.emp.Departamento).Distinct().ToList(),
                        Proyectos = g.Select(s => s.sol.NombreProyecto).Distinct().Take(5).ToList()
                    })
                    .OrderByDescending(x => x.CantidadViajes)
                    .Take(limite)
                    .ToListAsync();

                return Ok(destinos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener top destinos");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        // API para obtener detalle completo de comprobaciones
        [HttpGet("api/GetDetalleComprobaciones")]
        public async Task<IActionResult> GetDetalleComprobaciones(
            [FromQuery] string? fechaInicio = null,
            [FromQuery] string? fechaFin = null,
            [FromQuery] string? departamento = null,
            [FromQuery] string? escenario = null)
        {
            try
            {
                DateTime? inicio = null;
                DateTime? fin = null;

                if (!string.IsNullOrEmpty(fechaInicio))
                    inicio = DateTime.Parse(fechaInicio);

                if (!string.IsNullOrEmpty(fechaFin))
                    fin = DateTime.Parse(fechaFin).AddDays(1).AddTicks(-1);

                // Consulta base con todos los joins necesarios
                var query = from comp in _context.ComprobacionesViaje
                            join sol in _context.SolicitudesViajes on comp.SolicitudViajeId equals sol.Id
                            join emp in _context.Empleados on sol.EmpleadoId equals emp.Id
                            join estado in _context.EstadosComprobacion on comp.EstadoComprobacionId equals estado.Id into estadoGroup
                            from estado in estadoGroup.DefaultIfEmpty()
                            where emp.Activo == true && comp.FechaComprobacion.HasValue
                            select new
                            {
                                comp,
                                sol,
                                emp,
                                estado
                            };

                // Aplicar filtros manualmente
                if (inicio.HasValue)
                    query = query.Where(x => x.comp.FechaComprobacion >= inicio.Value);

                if (fin.HasValue)
                    query = query.Where(x => x.comp.FechaComprobacion <= fin.Value);

                if (!string.IsNullOrEmpty(departamento) && departamento != "TODOS")
                    query = query.Where(x => x.emp.Departamento == departamento);

                if (!string.IsNullOrEmpty(escenario) && escenario != "TODOS")
                    query = query.Where(x => x.comp.EscenarioLiquidacion == escenario);

                // Proyectar datos con traducciones
                var resultados = await query
                    .Select(x => new
                    {
                        ComprobacionId = x.comp.Id,
                        SolicitudId = x.sol.Id,
                        Empleado = $"{x.emp.Nombre} {x.emp.Apellidos}",
                        Departamento = x.emp.Departamento ?? "Sin departamento",
                        Proyecto = x.sol.NombreProyecto ?? "Sin proyecto",
                        Anticipo = x.comp.TotalAnticipo ?? 0,
                        Gastado = x.comp.TotalGastosComprobados ?? 0,
                        Diferencia = x.comp.Diferencia ?? 0,
                        Escenario = x.comp.EscenarioLiquidacion ?? "SIN_ESCENARIO",
                        EscenarioTraducido = x.comp.EscenarioLiquidacion == "REPOSICION_EMPRESA" ? "Reposición Empresa" :
                                           x.comp.EscenarioLiquidacion == "REPOSICION_COLABORADOR" ? "Reposición Colaborador" :
                                           x.comp.EscenarioLiquidacion == "SALDADA" ? "Saldada" :
                                           x.comp.EscenarioLiquidacion == "PAGO_AUTORIZADO" ? "Pago Autorizado" :
                                           x.comp.EscenarioLiquidacion == "CON_CORRECCIONES_PENDIENTES" ? "Con Correcciones" :
                                           x.comp.EscenarioLiquidacion == "PARCIALMENTE_APROBADA" ? "Parcialmente Aprobada" :
                                           x.comp.EscenarioLiquidacion == "EN_REVISION_JP" ? "En Revisión JP" :
                                           x.comp.EscenarioLiquidacion ?? "Sin escenario",
                        Estado = x.estado != null ? x.estado.Descripcion : "Sin estado",
                        CodigoEstado = x.estado != null ? x.estado.Codigo : "",
                        FechaComprobacion = x.comp.FechaComprobacion,
                        CodigoComprobacion = x.comp.CodigoComprobacion,
                        CodigoSolicitud = x.sol.CodigoSolicitud
                    })
                    .OrderByDescending(x => x.FechaComprobacion)
                    .ToListAsync();

                // Calcular totales agregados
                var totalAnticipo = resultados.Sum(x => x.Anticipo);
                var totalGastado = resultados.Sum(x => x.Gastado);
                var totalDiferencia = resultados.Sum(x => x.Diferencia);

                return Ok(new
                {
                    Datos = resultados,
                    Totales = new
                    {
                        TotalAnticipo = Math.Round(totalAnticipo, 2),
                        TotalGastado = Math.Round(totalGastado, 2),
                        TotalDiferencia = Math.Round(totalDiferencia, 2)
                    },
                    TotalRegistros = resultados.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle de comprobaciones");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        // API para obtener datos del dashboard según rol de usuario
        [HttpGet("api/GetDashboardData")]
        public async Task<IActionResult> GetDashboardData()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "EMPLEADO";

                // Seleccionar método según rol del usuario
                object dashboardData = userRole.ToUpper() switch
                {
                    "EMPLEADO" => await GetDashboardDataEmpleado(userId),
                    "JP" => await GetDashboardDataJefeProyecto(userId),
                    "RH" => await GetDashboardDataRH(),
                    "FINANZAS" => await GetDashboardDataFinanzas(),
                    "DIRECCION" => await GetDashboardDataDireccion(),
                    "ADMIN" => await GetDashboardDataAdmin(),
                    _ => await GetDashboardDataEmpleado(userId)
                };

                return Ok(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos del dashboard");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        // API para exportar reportes a Excel
        [HttpGet("api/ExportarExcel")]
        public async Task<IActionResult> ExportarExcel(
            [FromQuery] string? fechaInicio = null,
            [FromQuery] string? fechaFin = null,
            [FromQuery] string? departamento = null,
            [FromQuery] string? escenario = null,
            [FromQuery] string tipoReporte = "RESUMEN")
        {
            try
            {
                DateTime? inicio = null;
                DateTime? fin = null;

                if (!string.IsNullOrEmpty(fechaInicio))
                    inicio = DateTime.Parse(fechaInicio);

                if (!string.IsNullOrEmpty(fechaFin))
                    fin = DateTime.Parse(fechaFin);

                using var package = new ExcelPackage();

                // Generar diferentes tipos de reportes según parámetro
                switch (tipoReporte.ToUpper())
                {
                    case "RESUMEN":
                        await GenerarReporteResumen(package, inicio, fin, departamento, escenario);
                        break;
                    case "DETALLADO":
                        await GenerarReporteDetallado(package, inicio, fin, departamento, escenario);
                        break;
                    case "COMPLETO":
                    default:
                        await GenerarReporteCompleto(package, inicio, fin, departamento, escenario);
                        break;
                }

                var fileName = $"Reporte_Viaticos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var fileBytes = package.GetAsByteArray();

                return File(fileBytes,
                           "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                           fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ExportarExcel");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Métodos privados para obtener datos de dashboard por rol

        private async Task<object> GetDashboardDataEmpleado(int empleadoId)
        {
            var misSolicitudes = await _context.SolicitudesViajes
                .Where(s => s.EmpleadoId == empleadoId)
                .ToListAsync();

            var misComprobaciones = await _context.ComprobacionesViaje
                .Include(c => c.SolicitudViaje)
                .Where(c => c.SolicitudViaje.EmpleadoId == empleadoId)
                .ToListAsync();

            return new
            {
                MisSolicitudesAprobadas = misSolicitudes.Count(s => s.EstadoId == 9),
                MisSolicitudesPendientes = misSolicitudes.Count(s => s.EstadoId == 1 || s.EstadoId == 10),
                MisSolicitudesRechazadas = misSolicitudes.Count(s => s.EstadoId == 10),
                MisSolicitudesBorrador = misSolicitudes.Count(s => s.EstadoId == 1),
                TotalAnticiposSolicitados = misComprobaciones.Sum(c => c.TotalAnticipo ?? 0),
                TotalAnticiposAutorizados = misComprobaciones.Where(c => c.EstadoComprobacionId == 4).Sum(c => c.TotalAnticipo ?? 0),
                MisViajesRealizados = misComprobaciones.Count(c => c.EstadoComprobacionId == 4),
                MisViajesPendientes = misComprobaciones.Count(c => c.EstadoComprobacionId == 1),
                MisGastosTotales = misComprobaciones.Sum(c => c.TotalGastosComprobados ?? 0)
            };
        }

        private async Task<object> GetDashboardDataJefeProyecto(int jefeId)
        {
            var empleados = await _context.Empleados
                .Where(e => e.JefeDirectoId == jefeId && e.Activo == true)
                .Select(e => e.Id)
                .ToListAsync();

            var solicitudes = await _context.SolicitudesViajes
                .Where(s => empleados.Contains(s.EmpleadoId))
                .ToListAsync();

            return new
            {
                PendientesJP = solicitudes.Count(s => s.EstadoId == 10),
                AprobadasJP = solicitudes.Count(s => s.EstadoId == 9),
                RechazadasJP = solicitudes.Count(s => s.EstadoId == 11),
                TotalSolicitudesPendientes = solicitudes.Count(s => s.EstadoId == 1 || s.EstadoId == 10),
                SolicitudesEsteMes = solicitudes.Count(s => s.CreatedAt.Value.Month == DateTime.Now.Month),
                GestionesPendientes = solicitudes.Count(s => s.EstadoId == 10)
            };
        }

        private async Task<object> GetDashboardDataRH()
        {
            var solicitudes = await _context.SolicitudesViajes
                .Where(s => s.EstadoId == 5 || s.EstadoId == 6)
                .ToListAsync();

            var empleados = await _context.Empleados
                .Where(e => e.Activo == true)
                .ToListAsync();

            return new
            {
                PendientesRH = solicitudes.Count(s => s.EstadoId == 5),
                AprobadasRH = solicitudes.Count(s => s.EstadoId == 7),
                RechazadasRH = solicitudes.Count(s => s.EstadoId == 8),
                TotalEmpleados = empleados.Count,
                EmpleadosActivos = empleados.Count(e => e.Activo == true),
                NuevosEmpleadosMes = empleados.Count(e => e.CreatedAt.Value.Month == DateTime.Now.Month)
            };
        }

        private async Task<object> GetDashboardDataFinanzas()
        {
            var comprobaciones = await _context.ComprobacionesViaje
                .Where(c => c.EstadoComprobacionId == 1 || c.EstadoComprobacionId == 2)
                .ToListAsync();

            var anticipos = await _context.Anticipos
                .Where(a => a.FechaAutorizacion.Value.Month == DateTime.Now.Month)
                .ToListAsync();

            return new
            {
                SolicitudesActivas = await _context.SolicitudesViajes.CountAsync(s => s.EstadoId == 9),
                PendientesAprobacion = comprobaciones.Count,
                ComprobacionesPendientes = comprobaciones.Count(c => c.EstadoComprobacionId == 1),
                ComprobacionesEnRevision = comprobaciones.Count(c => c.EstadoComprobacionId == 2),
                AnticiposEsteMes = anticipos.Sum(a => a.MontoAutorizado),
                GastosEsteMes = await _context.ComprobacionesViaje
                    .Where(c => c.FechaComprobacion.Value.Month == DateTime.Now.Month && c.EstadoComprobacionId == 4)
                    .SumAsync(c => c.TotalGastosComprobados ?? 0)
            };
        }

        private async Task<object> GetDashboardDataDireccion()
        {
            var solicitudes = await _context.SolicitudesViajes
                .Where(s => s.EstadoId == 3 || s.EstadoId == 4)
                .ToListAsync();

            var comprobaciones = await _context.ComprobacionesViaje
                .Where(c => c.EstadoComprobacionId == 3)
                .ToListAsync();

            return new
            {
                PendientesDireccion = solicitudes.Count(s => s.EstadoId == 3),
                AprobadasDireccion = solicitudes.Count(s => s.EstadoId == 9),
                ComprobacionesPendientesDir = comprobaciones.Count,
                SolicitudesEsteMes = solicitudes.Count(s => s.CreatedAt.Value.Month == DateTime.Now.Month),
                MontoPendienteAprobacion = solicitudes.Sum(s => s.MontoAnticipo ?? 0)
            };
        }

        private async Task<object> GetDashboardDataAdmin()
        {
            var totalEmpleados = await _context.Empleados.CountAsync();
            var EmpleadosActivos = await _context.Empleados.CountAsync(u => u.Activo == true);
            var totalSolicitudes = await _context.SolicitudesViajes.CountAsync();
            var solicitudesActivas = await _context.SolicitudesViajes.CountAsync(s => s.EstadoId == 9);

            var totalGastos = await _context.ComprobacionesViaje
                .Where(c => c.EstadoComprobacionId == 4)
                .SumAsync(c => c.TotalGastosComprobados ?? 0);

            return new
            {
                TotalEmpleados = totalEmpleados,
                TotalEmpleadosActivos = EmpleadosActivos,
                TotalSolicitudes = totalSolicitudes,
                SolicitudesActivas = solicitudesActivas,
                PendientesAprobacion = await _context.SolicitudesViajes.CountAsync(s => s.EstadoId == 1),
                TotalGastos = totalGastos,
                SistemaActivo = true,
                UltimaActualizacion = DateTime.Now
            };
        }

        // API para DataTables con paginación y filtros
        [HttpGet("api/GetDetalleGastos")]
        public async Task<IActionResult> GetDetalleGastos(
            [FromQuery] string? fechaInicio = null,
            [FromQuery] string? fechaFin = null,
            [FromQuery] string? departamento = null,
            [FromQuery] string? escenario = null,
            [FromQuery] int start = 0,
            [FromQuery] int length = 10,
            [FromQuery] int draw = 1
        )
        {
            try
            {
                var hoy = DateTime.Now;
                DateTime inicio = !string.IsNullOrEmpty(fechaInicio)
                    ? DateTime.Parse(fechaInicio)
                    : new DateTime(hoy.Year, hoy.Month, 1);

                DateTime fin = !string.IsNullOrEmpty(fechaFin)
                    ? DateTime.Parse(fechaFin).AddDays(1).AddTicks(-1)
                    : new DateTime(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month));

                var search = Request.Query["search[value]"].ToString();

                // Consulta base con joins
                var query = _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.EstadoComprobacion)
                    .Where(c => c.FechaComprobacion >= inicio && c.FechaComprobacion <= fin);

                if (!string.IsNullOrEmpty(departamento) && departamento != "TODOS")
                    query = query.Where(c => c.SolicitudViaje.Empleado.Departamento == departamento);

                if (!string.IsNullOrEmpty(escenario) && escenario != "TODOS")
                    query = query.Where(c => c.EscenarioLiquidacion == escenario);

                // Aplicar filtro de búsqueda global
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(c =>
                        c.SolicitudViaje.Empleado.Nombre.Contains(search) ||
                        c.SolicitudViaje.Empleado.Apellidos.Contains(search) ||
                        c.CodigoComprobacion.Contains(search) ||
                        c.SolicitudViaje.CodigoSolicitud.Contains(search)
                    );
                }

                var recordsTotal = await query.CountAsync();

                // Aplicar paginación
                var datos = await query
                    .OrderByDescending(c => c.FechaComprobacion)
                    .Skip(start)
                    .Take(length)
                    .Select(c => new
                    {
                        ComprobacionId = c.Id,
                        SolicitudId = c.SolicitudViaje.Id,
                        Empleado = c.SolicitudViaje.Empleado.Nombre + " " + c.SolicitudViaje.Empleado.Apellidos,
                        Departamento = c.SolicitudViaje.Empleado.Departamento ?? "",
                        Proyecto = c.SolicitudViaje.NombreProyecto ?? "",
                        Anticipo = c.TotalAnticipo ?? 0,
                        Gastado = c.TotalGastosComprobados ?? 0,
                        Diferencia = c.Diferencia ?? 0,
                        Escenario = c.EscenarioLiquidacion ?? "",
                        Estado = c.EstadoComprobacion.Codigo
                    })
                    .ToListAsync();

                // Aplicar traducciones en memoria
                var resultado = datos.Select(c => new
                {
                    c.ComprobacionId,
                    c.SolicitudId,
                    c.Empleado,
                    c.Departamento,
                    c.Proyecto,
                    c.Anticipo,
                    c.Gastado,
                    c.Diferencia,
                    EscenarioTraducido = c.Escenario switch
                    {
                        "REPOSICION_EMPRESA" => "Reposición Empresa",
                        "REPOSICION_COLABORADOR" => "Reposición Colaborador",
                        "SALDADA" => "Saldada",
                        "PAGO_AUTORIZADO" => "Pago Autorizado",
                        _ => c.Escenario
                    },
                    EstadoTraducido = c.Estado switch
                    {
                        "PENDIENTE" => "Pendiente",
                        "APROBADA" => "Aprobada",
                        "RECHAZADA" => "Rechazada",
                        _ => c.Estado
                    }
                });

                return Json(new
                {
                    draw,
                    recordsTotal,
                    recordsFiltered = recordsTotal,
                    data = resultado
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error GetDetalleGastos");
                return Json(new
                {
                    draw,
                    recordsTotal = 0,
                    recordsFiltered = 0,
                    data = new List<object>(),
                    error = ex.Message
                });
            }
        }

        // Método para ver detalles completos de una comprobación específica
        [HttpGet("DetallesCompletos/{id}")]
        public async Task<IActionResult> DetallesCompletos(int id)
        {
            try
            {
                // Cargar comprobación con todas las relaciones necesarias
                var comprobacion = await _context.ComprobacionesViaje
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Estado)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.TipoViatico)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Anticipos)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.GastosReales)
                            .ThenInclude(g => g.CategoriaGasto)
                    .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.CotizacionesFinanzas)
                    .Include(c => c.EstadoComprobacion)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (comprobacion == null || comprobacion.SolicitudViaje == null)
                {
                    TempData["Error"] = "No se encontró la comprobación o la solicitud asociada";
                    return RedirectToAction("Dashboard");
                }

                var cotizacion = comprobacion.SolicitudViaje.CotizacionesFinanzas?.FirstOrDefault();

                // Construir ViewModel con toda la información
                var viewModel = new DetallesCompletosViewModel
                {
                    // Información de comprobación
                    ComprobacionId = comprobacion.Id,
                    CodigoComprobacion = comprobacion.CodigoComprobacion ?? "N/A",
                    FechaComprobacion = comprobacion.FechaComprobacion ?? DateTime.MinValue,
                    EstadoComprobacion = comprobacion.EstadoComprobacion?.Codigo ?? "SIN_ESTADO",
                    TotalAnticipo = comprobacion.TotalAnticipo ?? 0,
                    TotalGastosComprobados = comprobacion.TotalGastosComprobados ?? 0,
                    Diferencia = comprobacion.Diferencia ?? 0,
                    EscenarioLiquidacion = comprobacion.EscenarioLiquidacion,
                    DescripcionActividades = comprobacion.DescripcionActividades,
                    ResultadosViaje = comprobacion.ResultadosViaje,

                    // Información de solicitud
                    SolicitudId = comprobacion.SolicitudViaje.Id,
                    CodigoSolicitud = comprobacion.SolicitudViaje.CodigoSolicitud ?? "N/A",
                    EmpleadoNombre = $"{comprobacion.SolicitudViaje.Empleado?.Nombre ?? ""} {comprobacion.SolicitudViaje.Empleado?.Apellidos ?? ""}".Trim(),
                    Departamento = comprobacion.SolicitudViaje.Empleado?.Departamento ?? "Sin departamento",
                    Proyecto = comprobacion.SolicitudViaje.NombreProyecto ?? "Sin proyecto",
                    Destino = comprobacion.SolicitudViaje.Destino ?? "Sin destino",
                    FechaSalida = comprobacion.SolicitudViaje.FechaSalida,
                    FechaRegreso = comprobacion.SolicitudViaje.FechaRegreso,
                    Motivo = comprobacion.SolicitudViaje.Motivo,
                    EstadoSolicitud = comprobacion.SolicitudViaje.Estado?.Codigo ?? "SIN_ESTADO",
                    TipoViatico = comprobacion.SolicitudViaje.TipoViatico?.Nombre ?? "Sin tipo",

                    // Información de cotización
                    CotizacionId = cotizacion?.Id,
                    CodigoCotizacion = cotizacion?.CodigoCotizacion,
                    TotalAutorizadoCotizacion = cotizacion?.TotalAutorizado ?? 0,
                    EstadoCotizacion = cotizacion?.Estado,

                    // Lista de anticipos
                    Anticipos = comprobacion.SolicitudViaje.Anticipos
                        .Where(a => a != null)
                        .Select(a => new AnticipoViewModel
                        {
                            Id = a.Id,
                            CodigoAnticipo = a.CodigoAnticipo ?? "SIN_CODIGO",
                            MontoSolicitado = a.MontoSolicitado,
                            MontoAutorizado = a.MontoAutorizado ?? 0,
                            Estado = a.Estado ?? "SIN_ESTADO",
                            FechaSolicitud = a.FechaSolicitud ?? DateTime.MinValue
                        }).ToList(),

                    // Lista de gastos reales
                    Gastos = comprobacion.SolicitudViaje.GastosReales
                        .Where(g => g != null)
                        .Select(g => new GastoViewModel
                        {
                            Id = g.Id,
                            Concepto = g.Concepto ?? "Sin concepto",
                            Categoria = g.CategoriaGasto?.Nombre ?? "Sin categoría",
                            Monto = g.Monto,
                            FechaGasto = g.FechaGasto,
                            Proveedor = g.Proveedor ?? "Sin proveedor"
                        }).ToList()
                };

                return View("~/Views/Finanzas/DetallesCompletos.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener DetallesCompletos para ID: {Id}", id);
                TempData["Error"] = $"Error al cargar los detalles: {ex.Message}";
                return RedirectToAction("Dashboard");
            }
        }

        // Métodos auxiliares para traducción y configuración visual

        private string TraducirEscenario(string escenario)
        {
            if (string.IsNullOrEmpty(escenario)) return "Sin Escenario";

            return escenario.ToUpper() switch
            {
                "REPOSICION_EMPRESA" => "Reposición Empresa",
                "REPOSICION_COLABORADOR" => "Reposición Colaborador",
                "SALDADA" => "Saldada",
                "PAGO_AUTORIZADO" => "Pago Autorizado",
                "CON_CORRECCIONES_PENDIENTES" => "Con Correcciones",
                "PARCIALMENTE_APROBADA" => "Parcialmente Aprobada",
                "EN_REVISION_JP" => "En Revisión JP",
                "SIN_ESCENARIO" => "Sin Escenario",
                _ => escenario
            };
        }

        private string ObtenerColorPorEscenario(string escenario)
        {
            return escenario switch
            {
                "REPOSICION_EMPRESA" => "rgba(255, 193, 7, 0.7)",
                "REPOSICION_COLABORADOR" => "rgba(23, 162, 184, 0.7)",
                "SALDADA" => "rgba(40, 167, 69, 0.7)",
                "PAGO_AUTORIZADO" => "rgba(0, 123, 255, 0.7)",
                "CON_CORRECCIONES_PENDIENTES" => "rgba(23, 162, 184, 0.7)",
                "PARCIALMENTE_APROBADA" => "rgba(0, 123, 255, 0.7)",
                "EN_REVISION_JP" => "rgba(255, 193, 7, 0.7)",
                _ => "rgba(108, 117, 125, 0.7)"
            };
        }

        private static string ObtenerIconoEscenario(string escenario)
        {
            return escenario switch
            {
                "REPOSICION_EMPRESA" => "fas fa-building",
                "REPOSICION_COLABORADOR" => "fas fa-user",
                "SALDADA" => "fas fa-check-circle",
                "PAGO_AUTORIZADO" => "fas fa-money-check-alt",
                "CON_CORRECCIONES_PENDIENTES" => "fas fa-exclamation-triangle",
                "PARCIALMENTE_APROBADA" => "fas fa-check-double",
                "EN_REVISION_JP" => "fas fa-user-tie",
                "SIN_ESCENARIO" => "fas fa-question-circle",
                _ => "fas fa-chart-pie"
            };
        }

        private string TraducirEstado(string estado)
        {
            if (string.IsNullOrEmpty(estado)) return "Sin Estado";

            return estado.ToUpper() switch
            {
                "PENDIENTE" => "Pendiente",
                "APROBADA" => "Aprobada",
                "EN_REVISION" => "En Revisión",
                "RECHAZADA" => "Rechazada",
                "CON_CORRECCIONES" => "Con Correcciones",
                "SALDADA" => "Saldada",
                "PARCIALMENTE_APROBADA" => "Parcialmente Aprobada",
                "EN_REVISION_JP" => "En Revisión JP",
                _ => estado
            };
        }

        private string ObtenerColorPorEstado(string estado)
        {
            return estado switch
            {
                "PENDIENTE" => "rgba(108, 117, 125, 0.7)",
                "APROBADA" => "rgba(40, 167, 69, 0.7)",
                "EN_REVISION" => "rgba(255, 193, 7, 0.7)",
                "RECHAZADA" => "rgba(220, 53, 69, 0.7)",
                "CON_CORRECCIONES" => "rgba(23, 162, 184, 0.7)",
                "SALDADA" => "rgba(40, 167, 69, 0.7)",
                "PARCIALMENTE_APROBADA" => "rgba(0, 123, 255, 0.7)",
                "EN_REVISION_JP" => "rgba(23, 162, 184, 0.7)",
                _ => "rgba(108, 117, 125, 0.7)"
            };
        }

        private static string ObtenerIconoEstado(string estadoCodigo)
        {
            return estadoCodigo?.ToUpper() switch
            {
                "PENDIENTE" => "fas fa-clock",
                "APROBADA" => "fas fa-check-circle",
                "EN_REVISION" => "fas fa-search",
                "RECHAZADA" => "fas fa-times-circle",
                "CON_CORRECCIONES" => "fas fa-edit",
                "SALDADA" => "fas fa-file-invoice-dollar",
                "PARCIALMENTE_APROBADA" => "fas fa-check-double",
                "EN_REVISION_JP" => "fas fa-user-tie",
                _ => "fas fa-file-alt"
            };
        }

        private static string ObtenerIconoCategoria(string codigoCategoria)
        {
            return codigoCategoria?.ToUpper() switch
            {
                "TRANSPORTE" => "fas fa-bus",
                "HOSPEDAJE" => "fas fa-hotel",
                "ALIMENTOS" => "fas fa-utensils",
                "GASOLINA" => "fas fa-gas-pump",
                "CASETAS" => "fas fa-road",
                "UBER_TAXI" => "fas fa-taxi",
                "MATERIALES" => "fas fa-box",
                "SERVICIOS" => "fas fa-concierge-bell",
                _ => "fas fa-receipt"
            };
        }

        private string GenerarColorDepartamento(string departamento)
        {
            var hash = departamento.GetHashCode();
            var colors = new[]
            {
                "#5cc87b", "#308184", "#2c5282", "#6b46c1", "#d53f8c",
                "#dd6b20", "#38a169", "#319795", "#3182ce", "#805ad5",
                "#e53e3e", "#d69e2e", "#48bb78", "#38b2ac", "#4299e1",
                "#667eea", "#ed64a6", "#f56565", "#ed8936", "#ecc94b"
            };
            return colors[Math.Abs(hash) % colors.Length];
        }

        private string GenerarColorCategoria(int categoriaId)
        {
            var colors = new[]
            {
                "#5cc87b", "#308184", "#2c5282", "#6b46c1", "#d53f8c",
                "#dd6b20", "#38a169", "#319795", "#3182ce", "#805ad5",
                "#e53e3e", "#d69e2e", "#48bb78", "#38b2ac", "#4299e1"
            };
            return colors[categoriaId % colors.Length];
        }

        private string GenerarColorProyecto(string proyecto)
        {
            var hash = proyecto.GetHashCode();
            var colors = new[]
            {
                "#5cc87b", "#308184", "#2c5282", "#6b46c1", "#d53f8c",
                "#dd6b20", "#38a169", "#319795", "#3182ce", "#805ad5"
            };
            return colors[Math.Abs(hash) % colors.Length];
        }

        private string ObtenerClaseEscenario(string escenario)
        {
            return escenario switch
            {
                "REPOSICION_EMPRESA" => "bg-warning text-dark",
                "REPOSICION_COLABORADOR" => "bg-info",
                "SALDADA" => "bg-success",
                "PAGO_AUTORIZADO" => "bg-primary",
                _ => "bg-secondary"
            };
        }

        private string ObtenerClaseEstado(string estado)
        {
            return estado?.ToUpper() switch
            {
                "PENDIENTE" => "bg-secondary",
                "APROBADA" => "bg-success",
                "EN_REVISION" => "bg-warning",
                "RECHAZADA" => "bg-danger",
                "CON_CORRECCIONES" => "bg-info",
                "SALDADA" => "bg-success",
                "PARCIALMENTE_APROBADA" => "bg-primary",
                "EN_REVISION_JP" => "bg-info",
                _ => "bg-dark"
            };
        }

        // Métodos para generación de reportes Excel

        private async Task GenerarReporteCompleto(ExcelPackage package,
            DateTime? fechaInicio, DateTime? fechaFin, string? departamento, string? escenario)
        {
            var wsResumen = package.Workbook.Worksheets.Add("Resumen");
            await GenerarHojaResumen(wsResumen, fechaInicio, fechaFin, departamento, escenario);

            var wsDepartamentos = package.Workbook.Worksheets.Add("Gastos por Departamento");
            await GenerarHojaDepartamentos(wsDepartamentos, fechaInicio, fechaFin, departamento, escenario);

            var wsAnticipos = package.Workbook.Worksheets.Add("Anticipos Mayores");
            await GenerarHojaAnticipos(wsAnticipos, fechaInicio, fechaFin, departamento);

            var wsDetalle = package.Workbook.Worksheets.Add("Detalle de Gastos");
            await GenerarHojaDetalle(wsDetalle, fechaInicio, fechaFin, departamento, escenario);
        }

        private async Task GenerarReporteResumen(ExcelPackage package,
            DateTime? fechaInicio, DateTime? fechaFin, string? departamento, string? escenario)
        {
            var wsResumen = package.Workbook.Worksheets.Add("Resumen");
            await GenerarHojaResumen(wsResumen, fechaInicio, fechaFin, departamento, escenario);
        }

        private async Task GenerarReporteDetallado(ExcelPackage package,
            DateTime? fechaInicio, DateTime? fechaFin, string? departamento, string? escenario)
        {
            var wsDetalle = package.Workbook.Worksheets.Add("Detalle de Gastos");
            await GenerarHojaDetalle(wsDetalle, fechaInicio, fechaFin, departamento, escenario);
        }

        private async Task GenerarHojaResumen(ExcelWorksheet worksheet,
            DateTime? fechaInicio, DateTime? fechaFin, string? departamento, string? escenario)
        {
            try
            {
                var response = await GetResumenGeneral(
                    fechaInicio?.ToString("yyyy-MM-dd"),
                    fechaFin?.ToString("yyyy-MM-dd"),
                    departamento,
                    escenario);

                if (response is OkObjectResult okResult && okResult.Value != null)
                {
                    var resumen = okResult.Value;
                    var resumenType = resumen.GetType();

                    // Configurar cabecera del reporte
                    worksheet.Cells[1, 1].Value = "REPORTE DE VIÁTICOS - RESUMEN EJECUTIVO";
                    worksheet.Cells[1, 1, 1, 6].Merge = true;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.Font.Size = 16;
                    worksheet.Cells[1, 1].Style.Font.Color.SetColor(Color.FromArgb(0, 102, 51));

                    // Información del período
                    worksheet.Cells[2, 1].Value = $"Período: {(fechaInicio?.ToString("dd/MM/yyyy") ?? "Desde inicio")} - {(fechaFin?.ToString("dd/MM/yyyy") ?? "Hasta fin")}";
                    worksheet.Cells[2, 1, 2, 6].Merge = true;

                    if (!string.IsNullOrEmpty(departamento) && departamento != "TODOS")
                    {
                        worksheet.Cells[3, 1].Value = $"Departamento: {departamento}";
                        worksheet.Cells[3, 1, 3, 6].Merge = true;
                    }

                    worksheet.Cells[4, 1].Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
                    worksheet.Cells[4, 1, 4, 6].Merge = true;

                    // Extraer KPIs usando reflexión
                    var totalGastado = Convert.ToDecimal(resumenType.GetProperty("TotalGastado")?.GetValue(resumen) ?? 0);
                    var totalAnticipos = Convert.ToDecimal(resumenType.GetProperty("TotalAnticipos")?.GetValue(resumen) ?? 0);
                    var totalSolicitudes = Convert.ToInt32(resumenType.GetProperty("TotalSolicitudes")?.GetValue(resumen) ?? 0);
                    var totalComprobaciones = Convert.ToInt32(resumenType.GetProperty("TotalComprobaciones")?.GetValue(resumen) ?? 0);
                    var promedioAnticipo = Convert.ToDecimal(resumenType.GetProperty("PromedioAnticipo")?.GetValue(resumen) ?? 0);
                    var eficiencia = Convert.ToDecimal(resumenType.GetProperty("Eficiencia")?.GetValue(resumen) ?? 0);

                    int row = 6;
                    var kpis = new[]
                    {
                        new { Titulo = "TOTAL GASTADO", Valor = (object)totalGastado, Formato = "$#,##0.00" },
                        new { Titulo = "TOTAL ANTICIPOS", Valor = (object)totalAnticipos, Formato = "$#,##0.00" },
                        new { Titulo = "SOLICITUDES", Valor = (object)totalSolicitudes, Formato = "#,##0" },
                        new { Titulo = "COMPROBACIONES", Valor = (object)totalComprobaciones, Formato = "#,##0" },
                        new { Titulo = "PROMEDIO ANTICIPO", Valor = (object)promedioAnticipo, Formato = "$#,##0.00" },
                        new { Titulo = "EFICIENCIA", Valor = (object)eficiencia, Formato = "0.00%" }
                    };

                    // Organizar KPIs en grid 3x2
                    for (int i = 0; i < kpis.Length; i++)
                    {
                        var col = (i % 3) * 2 + 1;
                        var kpiRow = row + (i / 3) * 3;

                        worksheet.Cells[kpiRow, col].Value = kpis[i].Titulo;
                        worksheet.Cells[kpiRow, col].Style.Font.Bold = true;
                        worksheet.Cells[kpiRow, col, kpiRow, col + 1].Merge = true;

                        worksheet.Cells[kpiRow + 1, col].Value = kpis[i].Valor;
                        worksheet.Cells[kpiRow + 1, col].Style.Numberformat.Format = kpis[i].Formato;
                        worksheet.Cells[kpiRow + 1, col].Style.Font.Bold = true;
                        worksheet.Cells[kpiRow + 1, col, kpiRow + 1, col + 1].Merge = true;
                    }

                    // Ajustar anchos de columna
                    worksheet.Column(1).Width = 20;
                    worksheet.Column(2).Width = 5;
                    worksheet.Column(3).Width = 20;
                    worksheet.Column(4).Width = 5;
                    worksheet.Column(5).Width = 20;
                    worksheet.Column(6).Width = 5;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar hoja de resumen");
                worksheet.Cells[1, 1].Value = "Error al generar reporte";
            }
        }

        private async Task GenerarHojaDepartamentos(ExcelWorksheet worksheet,
            DateTime? fechaInicio, DateTime? fechaFin, string? departamento, string? escenario)
        {
            try
            {
                var response = await GetGastosPorDepartamento(
                    fechaInicio?.ToString("yyyy-MM-dd"),
                    fechaFin?.ToString("yyyy-MM-dd"),
                    departamento,
                    escenario);

                if (response is OkObjectResult okResult && okResult.Value is System.Collections.IEnumerable departamentos)
                {
                    var departamentosList = new List<dynamic>();
                    foreach (var item in departamentos)
                    {
                        departamentosList.Add(item);
                    }

                    worksheet.Cells[1, 1].Value = "GASTOS POR DEPARTAMENTO";
                    worksheet.Cells[1, 1, 1, 7].Merge = true;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.Font.Size = 14;

                    // Definir encabezados
                    string[] headers = { "Departamento", "Total Gastado", "% del Total", "Cantidad", "Promedio", "Anticipo", "Diferencia" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cells[3, i + 1].Value = headers[i];
                        worksheet.Cells[3, i + 1].Style.Font.Bold = true;
                    }

                    // Llenar datos
                    int row = 4;
                    foreach (var depto in departamentosList)
                    {
                        var deptoType = depto.GetType();
                        worksheet.Cells[row, 1].Value = deptoType.GetProperty("Departamento")?.GetValue(depto)?.ToString() ?? "";
                        worksheet.Cells[row, 2].Value = Convert.ToDecimal(deptoType.GetProperty("TotalGastado")?.GetValue(depto) ?? 0);
                        worksheet.Cells[row, 2].Style.Numberformat.Format = "$#,##0.00";
                        worksheet.Cells[row, 3].Value = Convert.ToDecimal(deptoType.GetProperty("PorcentajeDelTotal")?.GetValue(depto) ?? 0) / 100;
                        worksheet.Cells[row, 3].Style.Numberformat.Format = "0.00%";
                        worksheet.Cells[row, 4].Value = Convert.ToInt32(deptoType.GetProperty("CantidadComprobaciones")?.GetValue(depto) ?? 0);
                        worksheet.Cells[row, 5].Value = Convert.ToDecimal(deptoType.GetProperty("PromedioGasto")?.GetValue(depto) ?? 0);
                        worksheet.Cells[row, 5].Style.Numberformat.Format = "$#,##0.00";
                        worksheet.Cells[row, 6].Value = Convert.ToDecimal(deptoType.GetProperty("AnticipoTotal")?.GetValue(depto) ?? 0);
                        worksheet.Cells[row, 6].Style.Numberformat.Format = "$#,##0.00";
                        worksheet.Cells[row, 7].Value = Convert.ToDecimal(deptoType.GetProperty("DiferenciaTotal")?.GetValue(depto) ?? 0);
                        worksheet.Cells[row, 7].Style.Numberformat.Format = "$#,##0.00";
                        row++;
                    }

                    // Agregar fila de totales
                    worksheet.Cells[row, 1].Value = "TOTAL:";
                    worksheet.Cells[row, 1].Style.Font.Bold = true;
                    worksheet.Cells[row, 2].Formula = $"SUM(B4:B{row - 1})";
                    worksheet.Cells[row, 2].Style.Numberformat.Format = "$#,##0.00";
                    worksheet.Cells[row, 2].Style.Font.Bold = true;
                    worksheet.Cells[row, 3].Value = 1;
                    worksheet.Cells[row, 3].Style.Numberformat.Format = "0.00%";
                    worksheet.Cells[row, 4].Formula = $"SUM(D4:D{row - 1})";
                    worksheet.Cells[row, 4].Style.Font.Bold = true;

                    // Autoajustar columnas
                    for (int i = 1; i <= headers.Length; i++)
                    {
                        worksheet.Column(i).AutoFit();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar hoja de departamentos");
                worksheet.Cells[1, 1].Value = "Error al generar reporte de departamentos";
            }
        }

        private async Task GenerarHojaAnticipos(ExcelWorksheet worksheet,
            DateTime? fechaInicio, DateTime? fechaFin, string? departamento)
        {
            try
            {
                var response = await GetAnticiposMayores(
                    fechaInicio?.ToString("yyyy-MM-dd"),
                    fechaFin?.ToString("yyyy-MM-dd"),
                    departamento,
                    20);

                if (response is OkObjectResult okResult && okResult.Value is System.Collections.IEnumerable anticipos)
                {
                    var anticiposList = new List<dynamic>();
                    foreach (var item in anticipos)
                    {
                        anticiposList.Add(item);
                    }

                    worksheet.Cells[1, 1].Value = "TOP 20 ANTICIPOS MAYORES";
                    worksheet.Cells[1, 1, 1, 8].Merge = true;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.Font.Size = 14;

                    string[] headers = { "#", "Empleado", "Departamento", "Monto", "Solicitado", "Diferencia", "Fecha", "Proyecto" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cells[3, i + 1].Value = headers[i];
                        worksheet.Cells[3, i + 1].Style.Font.Bold = true;
                    }

                    int row = 4;
                    int contador = 1;
                    foreach (var ant in anticiposList)
                    {
                        var antType = ant.GetType();
                        worksheet.Cells[row, 1].Value = contador++;
                        worksheet.Cells[row, 2].Value = antType.GetProperty("Empleado")?.GetValue(ant)?.ToString() ?? "";
                        worksheet.Cells[row, 3].Value = antType.GetProperty("Departamento")?.GetValue(ant)?.ToString() ?? "";
                        worksheet.Cells[row, 4].Value = Convert.ToDecimal(antType.GetProperty("MontoAnticipo")?.GetValue(ant) ?? 0);
                        worksheet.Cells[row, 4].Style.Numberformat.Format = "$#,##0.00";
                        worksheet.Cells[row, 5].Value = Convert.ToDecimal(antType.GetProperty("MontoSolicitado")?.GetValue(ant) ?? 0);
                        worksheet.Cells[row, 5].Style.Numberformat.Format = "$#,##0.00";
                        worksheet.Cells[row, 6].Value = Convert.ToDecimal(antType.GetProperty("Diferencia")?.GetValue(ant) ?? 0);
                        worksheet.Cells[row, 6].Style.Numberformat.Format = "$#,##0.00";
                        var fecha = antType.GetProperty("FechaAutorizacion")?.GetValue(ant) as DateTime?;
                        worksheet.Cells[row, 7].Value = fecha?.ToString("dd/MM/yyyy");
                        worksheet.Cells[row, 8].Value = antType.GetProperty("Proyecto")?.GetValue(ant)?.ToString() ?? "";
                        row++;
                    }

                    // Totales
                    worksheet.Cells[row, 3].Value = "TOTAL:";
                    worksheet.Cells[row, 3].Style.Font.Bold = true;
                    worksheet.Cells[row, 4].Formula = $"SUM(D4:D{row - 1})";
                    worksheet.Cells[row, 4].Style.Numberformat.Format = "$#,##0.00";
                    worksheet.Cells[row, 4].Style.Font.Bold = true;
                    worksheet.Cells[row, 5].Formula = $"SUM(E4:E{row - 1})";
                    worksheet.Cells[row, 5].Style.Numberformat.Format = "$#,##0.00";
                    worksheet.Cells[row, 5].Style.Font.Bold = true;

                    for (int i = 1; i <= headers.Length; i++)
                    {
                        worksheet.Column(i).AutoFit();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar hoja de anticipos");
                worksheet.Cells[1, 1].Value = "Error al generar reporte de anticipos";
            }
        }

        private async Task GenerarHojaDetalle(ExcelWorksheet worksheet,
            DateTime? fechaInicio, DateTime? fechaFin, string? departamento, string? escenario)
        {
            try
            {
                var response = await GetDetalleGastos(
                    fechaInicio?.ToString("yyyy-MM-dd"),
                    fechaFin?.ToString("yyyy-MM-dd"),
                    departamento,
                    escenario
                );

                if (response is OkObjectResult okResult && okResult.Value != null)
                {
                    var detalleType = okResult.Value.GetType();
                    var datosProperty = detalleType.GetProperty("Datos");
                    var totalesProperty = detalleType.GetProperty("Totales");

                    if (datosProperty?.GetValue(okResult.Value) is System.Collections.IEnumerable datosEnumerable)
                    {
                        var datosList = new List<dynamic>();
                        foreach (var item in datosEnumerable)
                        {
                            datosList.Add(item);
                        }

                        worksheet.Cells[1, 1].Value = "DETALLE DE GASTOS";
                        worksheet.Cells[1, 1, 1, 10].Merge = true;
                        worksheet.Cells[1, 1].Style.Font.Bold = true;
                        worksheet.Cells[1, 1].Style.Font.Size = 14;

                        string[] headers = { "Empleado", "Departamento", "Proyecto", "Anticipo", "Gastado", "Diferencia", "% Dif", "Escenario", "Estado", "Fecha" };
                        for (int i = 0; i < headers.Length; i++)
                        {
                            worksheet.Cells[3, i + 1].Value = headers[i];
                            worksheet.Cells[3, i + 1].Style.Font.Bold = true;
                        }

                        int row = 4;
                        foreach (var item in datosList)
                        {
                            var itemType = item.GetType();
                            worksheet.Cells[row, 1].Value = itemType.GetProperty("Empleado")?.GetValue(item)?.ToString() ?? "";
                            worksheet.Cells[row, 2].Value = itemType.GetProperty("Departamento")?.GetValue(item)?.ToString() ?? "";
                            worksheet.Cells[row, 3].Value = itemType.GetProperty("Proyecto")?.GetValue(item)?.ToString() ?? "";
                            worksheet.Cells[row, 4].Value = Convert.ToDecimal(itemType.GetProperty("Anticipo")?.GetValue(item) ?? 0);
                            worksheet.Cells[row, 4].Style.Numberformat.Format = "$#,##0.00";
                            worksheet.Cells[row, 5].Value = Convert.ToDecimal(itemType.GetProperty("Gastado")?.GetValue(item) ?? 0);
                            worksheet.Cells[row, 5].Style.Numberformat.Format = "$#,##0.00";
                            worksheet.Cells[row, 6].Value = Convert.ToDecimal(itemType.GetProperty("Diferencia")?.GetValue(item) ?? 0);
                            worksheet.Cells[row, 6].Style.Numberformat.Format = "$#,##0.00";
                            worksheet.Cells[row, 7].Value = Convert.ToDecimal(itemType.GetProperty("PorcentajeDiferencia")?.GetValue(item) ?? 0) / 100;
                            worksheet.Cells[row, 7].Style.Numberformat.Format = "0.00%";
                            worksheet.Cells[row, 8].Value = itemType.GetProperty("EscenarioTraducido")?.GetValue(item)?.ToString() ?? "";
                            worksheet.Cells[row, 9].Value = itemType.GetProperty("EstadoDescripcion")?.GetValue(item)?.ToString() ?? "";
                            var fecha = itemType.GetProperty("FechaComprobacion")?.GetValue(item) as DateTime?;
                            worksheet.Cells[row, 10].Value = fecha?.ToString("dd/MM/yyyy");
                            row++;
                        }

                        if (totalesProperty?.GetValue(okResult.Value) is object totales)
                        {
                            var totalesType = totales.GetType();
                            worksheet.Cells[row, 3].Value = "TOTALES:";
                            worksheet.Cells[row, 3].Style.Font.Bold = true;
                            worksheet.Cells[row, 4].Value = Convert.ToDecimal(totalesType.GetProperty("TotalAnticipo")?.GetValue(totales) ?? 0);
                            worksheet.Cells[row, 4].Style.Numberformat.Format = "$#,##0.00";
                            worksheet.Cells[row, 4].Style.Font.Bold = true;
                            worksheet.Cells[row, 5].Value = Convert.ToDecimal(totalesType.GetProperty("TotalGastado")?.GetValue(totales) ?? 0);
                            worksheet.Cells[row, 5].Style.Numberformat.Format = "$#,##0.00";
                            worksheet.Cells[row, 5].Style.Font.Bold = true;
                            worksheet.Cells[row, 6].Value = Convert.ToDecimal(totalesType.GetProperty("TotalDiferencia")?.GetValue(totales) ?? 0);
                            worksheet.Cells[row, 6].Style.Numberformat.Format = "$#,##0.00";
                            worksheet.Cells[row, 6].Style.Font.Bold = true;
                            worksheet.Cells[row, 7].Value = Convert.ToDecimal(totalesType.GetProperty("Eficiencia")?.GetValue(totales) ?? 0) / 100;
                            worksheet.Cells[row, 7].Style.Numberformat.Format = "0.00%";
                            worksheet.Cells[row, 7].Style.Font.Bold = true;
                        }

                        for (int i = 1; i <= headers.Length; i++)
                        {
                            worksheet.Column(i).AutoFit();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar hoja de detalle");
                worksheet.Cells[1, 1].Value = "Error al generar reporte detallado";
            }
        }
    }

    // Clase de extensiones para métodos de filtrado de consultas
    public static class QueryExtensions
    {
        // Método de extensión para filtrar por fechas en propiedades DateTime?
        public static IQueryable<T> ApplyFechasFilter<T>(this IQueryable<T> query,
            DateTime? fechaInicio, DateTime? fin, Expression<Func<T, DateTime?>> fechaSelector)
        {
            if (fechaInicio.HasValue)
            {
                var param = fechaSelector.Parameters[0];
                var body = Expression.GreaterThanOrEqual(
                    fechaSelector.Body,
                    Expression.Constant(fechaInicio.Value.Date, typeof(DateTime?)));
                var lambda = Expression.Lambda<Func<T, bool>>(body, param);
                query = query.Where(lambda);
            }

            if (fin.HasValue)
            {
                var param = fechaSelector.Parameters[0];
                var body = Expression.LessThanOrEqual(
                    fechaSelector.Body,
                    Expression.Constant(fin.Value.Date.AddDays(1).AddSeconds(-1), typeof(DateTime?)));
                var lambda = Expression.Lambda<Func<T, bool>>(body, param);
                query = query.Where(lambda);
            }

            return query;
        }

        // Método de extensión para filtrar por fechas en propiedades DateOnly?
        public static IQueryable<T> ApplyFechasFilter<T>(this IQueryable<T> query,
            DateTime? fechaInicio, DateTime? fin, Expression<Func<T, DateOnly?>> fechaSelector)
        {
            if (fechaInicio.HasValue)
            {
                var fechaInicioDateOnly = DateOnly.FromDateTime(fechaInicio.Value);
                var param = fechaSelector.Parameters[0];
                var body = Expression.GreaterThanOrEqual(
                    fechaSelector.Body,
                    Expression.Constant(fechaInicioDateOnly, typeof(DateOnly?)));
                var lambda = Expression.Lambda<Func<T, bool>>(body, param);
                query = query.Where(lambda);
            }

            if (fin.HasValue)
            {
                var finDateOnly = DateOnly.FromDateTime(fin.Value);
                var param = fechaSelector.Parameters[0];
                var body = Expression.LessThanOrEqual(
                    fechaSelector.Body,
                    Expression.Constant(finDateOnly, typeof(DateOnly?)));
                var lambda = Expression.Lambda<Func<T, bool>>(body, param);
                query = query.Where(lambda);
            }

            return query;
        }

        // Método de extensión para filtrar por departamento
        public static IQueryable<T> ApplyDepartamentoFilter<T>(this IQueryable<T> query,
            string? departamento, Expression<Func<T, string?>> departamentoSelector)
        {
            if (!string.IsNullOrEmpty(departamento) && departamento != "TODOS")
            {
                var param = departamentoSelector.Parameters[0];
                var body = Expression.Equal(
                    departamentoSelector.Body,
                    Expression.Constant(departamento, typeof(string)));
                var lambda = Expression.Lambda<Func<T, bool>>(body, param);
                query = query.Where(lambda);
            }

            return query;
        }

        // Método de extensión para filtrar por escenario
        public static IQueryable<T> ApplyEscenarioFilter<T>(this IQueryable<T> query,
            string? escenario, Expression<Func<T, string?>> escenarioSelector)
        {
            if (!string.IsNullOrEmpty(escenario) && escenario != "TODOS")
            {
                var param = escenarioSelector.Parameters[0];
                var body = Expression.Equal(
                    escenarioSelector.Body,
                    Expression.Constant(escenario, typeof(string)));
                var lambda = Expression.Lambda<Func<T, bool>>(body, param);
                query = query.Where(lambda);
            }

            return query;
        }

        // Método de extensión para filtrar por proyecto
        public static IQueryable<T> ApplyProyectoFilter<T>(this IQueryable<T> query,
            string? proyecto, Expression<Func<T, string?>> proyectoSelector)
        {
            if (!string.IsNullOrEmpty(proyecto) && proyecto != "TODOS")
            {
                var param = proyectoSelector.Parameters[0];
                var body = Expression.Equal(
                    proyectoSelector.Body,
                    Expression.Constant(proyecto, typeof(string)));
                var lambda = Expression.Lambda<Func<T, bool>>(body, param);
                query = query.Where(lambda);
            }

            return query;
        }
    }
}