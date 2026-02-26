using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SVV.Filters;
using SVV.Models;
using SVV.Services;
using SVV.ViewModels;
using System.Security.Claims;

namespace SVV.Controllers
{
    // FILTRO QUE OBLIGA CAMBIO DE CONTRASEÑA EN PRIMER INGRESO
    [TypeFilter(typeof(CambioPassword))]
    [Authorize]
    public class HomeController : Controller
    {
        private readonly SvvContext _context;
        private readonly ILogger<HomeController> _logger;

        // INYECCIÓN DE DEPENDENCIAS PARA CONTEXTO DE BD Y LOGGING
        public HomeController(SvvContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ACCIÓN PRINCIPAL: DASHBOARD PERSONALIZADO POR ROL
        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // VALIDACIÓN DE USUARIO AUTENTICADO
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int empleadoId))
                {
                    await HttpContext.SignOutAsync();
                    return RedirectToAction("Login", "Auth");
                }

                var empleado = await _context.Empleados
                    .AsNoTracking()
                    .Include(e => e.Rol)
                    .FirstOrDefaultAsync(e => e.Id == empleadoId);

                // VALIDACIÓN DE EMPLEADO EXISTENTE
                if (empleado == null)
                {
                    await HttpContext.SignOutAsync();
                    return RedirectToAction("Login", "Auth");
                }

                // DATOS BÁSICOS PARA LA VISTA
                ViewBag.NombreUsuario = $"{empleado.Nombre} {empleado.Apellidos}";
                ViewBag.RolNombre = empleado.Rol?.Nombre ?? "Usuario";
                ViewBag.RolId = empleado.RolId;

                // CARGA DE ESTADÍSTICAS SEGÚN ROL DEL USUARIO
                await CargarEstadisticasPorRol(empleadoId, empleado.RolId);

                // PREPARACIÓN DE DATOS PARA GRÁFICOS JAVASCRIPT
                var dashboardData = new
                {
                    // DATOS PARA EMPLEADO (ROL 1)
                    MisSolicitudesBorrador = ViewBag.MisSolicitudesBorrador ?? 0,
                    MisSolicitudesPendientes = ViewBag.MisSolicitudesPendientes ?? 0,
                    MisSolicitudesAprobadas = ViewBag.MisSolicitudesAprobadas ?? 0,
                    MisSolicitudesRechazadas = ViewBag.MisSolicitudesRechazadas ?? 0,
                    TotalAnticiposAutorizados = ViewBag.TotalAnticiposAutorizados ?? 0m,

                    // DATOS PARA JEFE DE PROYECTO (ROL 2)
                    PendientesJP = ViewBag.PendientesJP ?? 0,
                    AprobadasJP = ViewBag.AprobadasJP ?? 0,
                    RechazadasJP = ViewBag.RechazadasJP ?? 0,
                    ComprobacionesPendientesRevision = ViewBag.ComprobacionesPendientesRevision ?? 0,

                    // DATOS PARA RECURSOS HUMANOS (ROL 3)
                    TotalSolicitudesActivas = ViewBag.TotalSolicitudesActivas ?? 0,
                    SolicitudesAprobadasDireccion = ViewBag.SolicitudesAprobadasDireccion ?? 0,
                    SolicitudesBorradorOtros = ViewBag.SolicitudesBorradorOtros ?? 0,

                    // DATOS PARA FINANZAS (ROL 4)
                    SolicitudesActivas = ViewBag.SolicitudesActivas ?? 0,
                    PendientesFinanzas = ViewBag.PendientesFinanzas ?? 0,
                    PendientesPorSaldar = ViewBag.PendientesPorSaldar ?? 0,
                    AnticiposAutorizadosPorUsuario = ViewBag.AnticiposAutorizadosPorUsuario ?? 0m,

                    // DATOS PARA DIRECCIÓN (ROL 5)
                    PendientesDireccion = ViewBag.PendientesDireccion ?? 0,
                    AnticiposAutorizados = ViewBag.AnticiposAutorizados ?? 0m,

                    // DATOS PARA ADMINISTRADOR (ROL 6)
                    PendientesAprobacion = ViewBag.PendientesAprobacion ?? 0,
                    TotalUsuariosActivos = ViewBag.TotalUsuariosActivos ?? 0,
                    AnticiposTotalesAutorizados = ViewBag.AnticiposTotalesAutorizados ?? 0m
                };

                ViewBag.DashboardData = dashboardData;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar dashboard en Home/Index");
                TempData["Error"] = "Error al cargar el dashboard";
                return View();
            }
        }

        // MÉTODO PRINCIPAL PARA CARGAR ESTADÍSTICAS SEGÚN ROL
        private async Task CargarEstadisticasPorRol(int empleadoId, int rolId)
        {
            try
            {
                _logger.LogInformation($"Cargando estadísticas para rol {rolId}");

                // SELECCIÓN DE MÉTODO SEGÚN ROL
                switch (rolId)
                {
                    case 1: // EMPLEADO
                        await CargarEstadisticasEmpleado(empleadoId);
                        break;
                    case 2: // JEFE DE PROYECTO
                        await CargarEstadisticasJefeProyecto(empleadoId);
                        break;
                    case 3: // RECURSOS HUMANOS
                        await CargarEstadisticasRH(empleadoId);
                        break;
                    case 4: // FINANZAS
                        await CargarEstadisticasFinanzas(empleadoId);
                        break;
                    case 5: // DIRECCIÓN
                        await CargarEstadisticasDireccion();
                        break;
                    case 6: // ADMINISTRADOR
                        await CargarEstadisticasAdmin();
                        break;
                    default:
                        EstablecerValoresPorDefecto();
                        break;
                }

                _logger.LogInformation("Estadísticas cargadas exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CargarEstadisticasPorRol");
                EstablecerValoresPorDefecto();
            }
        }

        // ESTADÍSTICAS ESPECÍFICAS PARA ROL EMPLEADO
        private async Task CargarEstadisticasEmpleado(int empleadoId)
        {
            try
            {
                _logger.LogInformation($"Cargando estadísticas para empleado {empleadoId}");

                // CONTEO DE SOLICITUDES POR ESTADO
                ViewBag.MisSolicitudesBorrador = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .CountAsync(s => s.EmpleadoId == empleadoId && s.EstadoId == 1);

                ViewBag.MisSolicitudesPendientes = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .CountAsync(s => s.EmpleadoId == empleadoId &&
                                   s.EstadoId >= 2 && s.EstadoId <= 8 && s.EstadoId != 9);

                ViewBag.MisSolicitudesAprobadas = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .CountAsync(s => s.EmpleadoId == empleadoId && s.EstadoId == 9);

                ViewBag.MisSolicitudesRechazadas = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .CountAsync(s => s.EmpleadoId == empleadoId && s.EstadoId == 10);

                // TOTAL DE ANTICIPOS AUTORIZADOS PARA ESTE EMPLEADO
                ViewBag.TotalAnticiposAutorizados = await _context.Anticipos
                    .AsNoTracking()
                    .Include(a => a.SolicitudViaje)
                    .Where(a => a.SolicitudViaje.EmpleadoId == empleadoId &&
                               (a.Estado == "AUTORIZADO" || a.Estado == "LIQUIDADO"))
                    .SumAsync(a => (decimal?)a.MontoAutorizado) ?? 0m;

                // HISTÓRICO DE SOLICITUDES RECIENTES DEL EMPLEADO
                ViewBag.SolicitudesRecientes = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .Where(s => s.EmpleadoId == empleadoId)
                    .Include(s => s.Estado)
                    .Include(s => s.Anticipos)
                    .OrderByDescending(s => s.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                _logger.LogInformation($"Estadísticas de empleado cargadas");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CargarEstadisticasEmpleado");
                EstablecerValoresPorDefectoEmpleado();
            }
        }

        // ESTADÍSTICAS ESPECÍFICAS PARA ROL JEFE DE PROYECTO
        private async Task CargarEstadisticasJefeProyecto(int empleadoId)
        {
            try
            {
                _logger.LogInformation($"Cargando estadísticas para Jefe de Proyecto {empleadoId}");

                // OBTENER IDs DE SUBORDINADOS DIRECTOS
                var subordinadosIds = await _context.Empleados
                    .AsNoTracking()
                    .Where(e => e.JefeDirectoId == empleadoId && e.Activo == true)
                    .Select(e => e.Id)
                    .ToListAsync();

                // SOLICITUDES PENDIENTES DE APROBACIÓN POR JP
                ViewBag.PendientesJP = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .CountAsync(s => subordinadosIds.Contains(s.EmpleadoId) && s.EstadoId == 2);

                // SOLICITUDES APROBADAS POR JP
                ViewBag.AprobadasJP = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .CountAsync(s => subordinadosIds.Contains(s.EmpleadoId) && s.EstadoId == 3);

                // SOLICITUDES RECHAZADAS POR JP
                ViewBag.RechazadasJP = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .CountAsync(s => subordinadosIds.Contains(s.EmpleadoId) && s.EstadoId == 10);

                // COMPROBACIONES PENDIENTES DE REVISIÓN POR JP
                var estadoComprobacionJP = await _context.EstadosComprobacion
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ec => ec.Codigo == "EN_REVISION_JP");

                if (estadoComprobacionJP != null)
                {
                    var comprobacionesDeSubordinados = await _context.ComprobacionesViaje
                        .AsNoTracking()
                        .Include(c => c.SolicitudViaje)
                        .ThenInclude(s => s.Empleado)
                        .Where(c => c.EstadoComprobacionId == estadoComprobacionJP.Id &&
                                   subordinadosIds.Contains(c.SolicitudViaje.EmpleadoId))
                        .ToListAsync();

                    ViewBag.ComprobacionesPendientesRevision = comprobacionesDeSubordinados.Count;
                    ViewBag.ComprobacionesPendientesRevisionJP = comprobacionesDeSubordinados
                        .OrderByDescending(c => c.CreatedAt)
                        .Take(5)
                        .ToList();
                }
                else
                {
                    ViewBag.ComprobacionesPendientesRevision = 0;
                    ViewBag.ComprobacionesPendientesRevisionJP = new List<ComprobacionesViaje>();
                }

                // SOLICITUDES PENDIENTES PARA MOSTRAR EN TABLA
                ViewBag.SolicitudesPendientesJP = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .Include(s => s.Empleado)
                    .Include(s => s.Estado)
                    .Where(s => subordinadosIds.Contains(s.EmpleadoId) && s.EstadoId == 2)
                    .OrderByDescending(s => s.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                // CONVERSIÓN A VIEWMODEL PARA VISTA
                var solicitudesPendientesData = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .Include(s => s.Empleado)
                    .Include(s => s.Estado)
                    .Where(s => subordinadosIds.Contains(s.EmpleadoId) && s.EstadoId == 2)
                    .OrderByDescending(s => s.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                var solicitudesPendientesJP = solicitudesPendientesData.Select(s => new SolicitudDashboardViewModel
                {
                    IdSolicitud = s.Id,
                    NombreProyecto = s.NombreProyecto ?? "Sin proyecto",
                    Codigo = s.CodigoSolicitud ?? "N/A",
                    EmpleadoNombre = s.Empleado != null ? $"{s.Empleado.Nombre} {s.Empleado.Apellidos}" : "N/A",
                    MontoSolicitado = s.MontoAnticipo != null ? s.MontoAnticipo.Value.ToString("C") : "$0.00",
                    FechaCreacion = s.CreatedAt != null ? s.CreatedAt.Value.ToString("dd MMM yyyy") : "N/A",
                    EstadoNombre = s.Estado != null ? s.Estado.Codigo : "N/A",
                    EstadoCssClass = s.Estado != null ? ObtenerClaseCssEstado(s.Estado.Codigo) : "info",
                    Destino = s.Destino ?? "N/A"
                }).ToList();

                ViewBag.SolicitudesPendientes = solicitudesPendientesJP;

                _logger.LogInformation($"Estadísticas JP cargadas: {ViewBag.PendientesJP} pendientes, {ViewBag.ComprobacionesPendientesRevision} comprobaciones");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CargarEstadisticasJefeProyecto");
                EstablecerValoresPorDefectoJP();
            }
        }

        // ESTADÍSTICAS ESPECÍFICAS PARA ROL RECURSOS HUMANOS
        private async Task CargarEstadisticasRH(int empleadoId)
        {
            try
            {
                _logger.LogInformation("Cargando estadísticas para Recursos Humanos");

                // TOTAL DE SOLICITUDES ACTIVAS EN EL SISTEMA
                ViewBag.TotalSolicitudesActivas = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .CountAsync(s => s.EstadoId != 10 && s.EstadoId != 1);

                // SOLICITUDES APROBADAS POR DIRECCIÓN
                ViewBag.SolicitudesAprobadasDireccion = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .CountAsync(s => s.EstadoId == 9);

                // SOLICITUDES EN BORRADOR DE OTROS EMPLEADOS
                ViewBag.SolicitudesBorradorOtros = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .CountAsync(s => s.EstadoId == 1 && s.EmpleadoId != empleadoId);

                // SOLICITUDES ACTIVAS PARA MOSTRAR EN TABLA
                ViewBag.SolicitudesPendientesRH = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .Include(s => s.Empleado)
                    .Include(s => s.Estado)
                    .Where(s => s.EstadoId != 10 && s.EstadoId != 1)
                    .OrderByDescending(s => s.CreatedAt)
                    .Take(10)
                    .ToListAsync();

                _logger.LogInformation($"Estadísticas RH cargadas: {ViewBag.TotalSolicitudesActivas} activas, {ViewBag.SolicitudesAprobadasDireccion} aprobadas por dirección, {ViewBag.SolicitudesBorradorOtros} en borrador de otros");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CargarEstadisticasRH");
                EstablecerValoresPorDefectoRH();
            }
        }

        // ESTADÍSTICAS ESPECÍFICAS PARA ROL FINANZAS
        private async Task CargarEstadisticasFinanzas(int empleadoId)
        {
            try
            {
                _logger.LogInformation($"Cargando estadísticas para Finanzas {empleadoId}");

                // TOTAL DE SOLICITUDES ACTIVAS
                ViewBag.SolicitudesActivas = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .CountAsync(s => s.EstadoId != 10 && s.EstadoId != 1);

                // SOLICITUDES PENDIENTES PARA FINANZAS
                ViewBag.PendientesFinanzas = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .CountAsync(s => s.EstadoId == 6);

                // COMPROBACIONES PENDIENTES POR SALDAR
                ViewBag.PendientesPorSaldar = await _context.ComprobacionesViaje
                    .AsNoTracking()
                    .CountAsync(c => c.EstadoComprobacion.Codigo != "LIQUIDADA" &&
                                    c.EstadoComprobacion.Codigo != "RECHAZADA");

                // ANTICIPOS AUTORIZADOS POR ESTE USUARIO DE FINANZAS
                ViewBag.AnticiposAutorizadosPorUsuario = await _context.Anticipos
                    .AsNoTracking()
                    .Where(a => a.AutorizadoPorId == empleadoId &&
                               (a.Estado == "AUTORIZADO" || a.Estado == "LIQUIDADO"))
                    .SumAsync(a => (decimal?)a.MontoAutorizado) ?? 0m;

                // CONVERSIÓN A VIEWMODEL PARA VISTA
                var solicitudesPendientesData = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .Include(s => s.Empleado)
                    .Include(s => s.Estado)
                    .Where(s => s.EstadoId == 6)
                    .OrderByDescending(s => s.CreatedAt)
                    .Take(10)
                    .ToListAsync();

                var solicitudesPendientesFinanzasList = solicitudesPendientesData.Select(s => new SolicitudDashboardViewModel
                {
                    IdSolicitud = s.Id,
                    NombreProyecto = s.NombreProyecto ?? "Sin proyecto",
                    Codigo = s.CodigoSolicitud ?? "N/A",
                    EmpleadoNombre = s.Empleado != null ? $"{s.Empleado.Nombre} {s.Empleado.Apellidos}" : "N/A",
                    MontoSolicitado = s.MontoAnticipo != null ? s.MontoAnticipo.Value.ToString("C") : "$0.00",
                    FechaCreacion = s.CreatedAt != null ? s.CreatedAt.Value.ToString("dd MMM yyyy") : "N/A",
                    EstadoNombre = s.Estado != null ? s.Estado.Codigo : "N/A",
                    EstadoCssClass = s.Estado != null ? ObtenerClaseCssEstado(s.Estado.Codigo) : "info",
                    Destino = s.Destino ?? "N/A"
                }).ToList();

                ViewBag.SolicitudesPendientes = solicitudesPendientesFinanzasList;

                _logger.LogInformation($"Estadísticas Finanzas cargadas: {ViewBag.PendientesFinanzas} pendientes, {ViewBag.AnticiposAutorizadosPorUsuario} autorizados por mí, {ViewBag.PendientesPorSaldar} por saldar");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CargarEstadisticasFinanzas");
                EstablecerValoresPorDefectoFinanzas();
            }
        }

        // ESTADÍSTICAS ESPECÍFICAS PARA ROL DIRECCIÓN
        private async Task CargarEstadisticasDireccion()
        {
            try
            {
                _logger.LogInformation("Cargando estadísticas para Dirección");

                // TOTAL DE SOLICITUDES ACTIVAS
                ViewBag.SolicitudesActivas = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .CountAsync(s => s.EstadoId != 10 && s.EstadoId != 1);

                // SOLICITUDES PENDIENTES PARA DIRECCIÓN
                ViewBag.PendientesDireccion = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .CountAsync(s => s.EstadoId == 8);

                // TOTAL DE ANTICIPOS AUTORIZADOS EN EL SISTEMA
                ViewBag.AnticiposAutorizados = await _context.Anticipos
                    .AsNoTracking()
                    .Where(a => a.Estado == "AUTORIZADO" || a.Estado == "LIQUIDADO")
                    .SumAsync(a => (decimal?)a.MontoAutorizado) ?? 0m;

                // CONVERSIÓN A VIEWMODEL PARA VISTA
                var solicitudesPendientesData = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .Include(s => s.Empleado)
                    .Include(s => s.Estado)
                    .Where(s => s.EstadoId == 8)
                    .OrderByDescending(s => s.CreatedAt)
                    .Take(10)
                    .ToListAsync();

                var solicitudesPendientesDireccionList = solicitudesPendientesData.Select(s => new SolicitudDashboardViewModel
                {
                    IdSolicitud = s.Id,
                    NombreProyecto = s.NombreProyecto ?? "Sin proyecto",
                    Codigo = s.CodigoSolicitud ?? "N/A",
                    EmpleadoNombre = s.Empleado != null ? $"{s.Empleado.Nombre} {s.Empleado.Apellidos}" : "N/A",
                    MontoSolicitado = s.MontoAnticipo != null ? s.MontoAnticipo.Value.ToString("C") : "$0.00",
                    FechaCreacion = s.CreatedAt != null ? s.CreatedAt.Value.ToString("dd MMM yyyy") : "N/A",
                    EstadoNombre = s.Estado != null ? s.Estado.Codigo : "N/A",
                    EstadoCssClass = s.Estado != null ? ObtenerClaseCssEstado(s.Estado.Codigo) : "info",
                    Destino = s.Destino ?? "N/A"
                }).ToList();

                ViewBag.SolicitudesPendientes = solicitudesPendientesDireccionList;

                _logger.LogInformation($"Estadísticas Dirección cargadas: {ViewBag.PendientesDireccion} pendientes, {ViewBag.AnticiposAutorizados} autorizados totales");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CargarEstadisticasDireccion");
                EstablecerValoresPorDefectoDireccion();
            }
        }

        // ESTADÍSTICAS ESPECÍFICAS PARA ROL ADMINISTRADOR
        private async Task CargarEstadisticasAdmin()
        {
            try
            {
                _logger.LogInformation("Cargando estadísticas para Administrador");

                // TOTAL DE SOLICITUDES ACTIVAS
                ViewBag.SolicitudesActivas = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .CountAsync(s => s.EstadoId != 10 && s.EstadoId != 1);

                // SOLICITUDES PENDIENTES DE APROBACIÓN EN CUALQUIER ETAPA
                ViewBag.PendientesAprobacion = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .CountAsync(s => s.EstadoId == 2 || s.EstadoId == 4 || s.EstadoId == 6 || s.EstadoId == 8);

                // TOTAL DE USUARIOS ACTIVOS EN EL SISTEMA
                ViewBag.TotalUsuariosActivos = await _context.Empleados
                    .AsNoTracking()
                    .CountAsync(e => e.Activo == true);

                // TOTAL DE ANTICIPOS AUTORIZADOS EN EL SISTEMA
                ViewBag.AnticiposTotalesAutorizados = await _context.Anticipos
                    .AsNoTracking()
                    .Where(a => a.Estado == "AUTORIZADO" || a.Estado == "LIQUIDADO")
                    .SumAsync(a => (decimal?)a.MontoAutorizado) ?? 0m;

                // CONVERSIÓN A VIEWMODEL PARA VISTA
                var solicitudesPendientesData = await _context.SolicitudesViajes
                    .AsNoTracking()
                    .Include(s => s.Empleado)
                    .Include(s => s.Estado)
                    .Where(s => s.EstadoId == 2 || s.EstadoId == 4 || s.EstadoId == 6 || s.EstadoId == 8)
                    .OrderByDescending(s => s.CreatedAt)
                    .Take(10)
                    .ToListAsync();

                var solicitudesPendientesList = solicitudesPendientesData.Select(s => new SolicitudDashboardViewModel
                {
                    IdSolicitud = s.Id,
                    NombreProyecto = s.NombreProyecto ?? "Sin proyecto",
                    Codigo = s.CodigoSolicitud ?? "N/A",
                    EmpleadoNombre = s.Empleado != null ? $"{s.Empleado.Nombre} {s.Empleado.Apellidos}" : "N/A",
                    MontoSolicitado = s.MontoAnticipo != null ? s.MontoAnticipo.Value.ToString("C") : "$0.00",
                    FechaCreacion = s.CreatedAt != null ? s.CreatedAt.Value.ToString("dd MMM yyyy") : "N/A",
                    EstadoNombre = s.Estado != null ? s.Estado.Codigo : "N/A",
                    EstadoCssClass = s.Estado != null ? ObtenerClaseCssEstado(s.Estado.Codigo) : "info",
                    Destino = s.Destino ?? "N/A"
                }).ToList();

                ViewBag.SolicitudesPendientes = solicitudesPendientesList;

                _logger.LogInformation($"Estadísticas Admin cargadas: {ViewBag.SolicitudesActivas} activas, {ViewBag.PendientesAprobacion} pendientes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CargarEstadisticasAdmin");
                EstablecerValoresPorDefectoAdmin();
            }
        }

        // ASIGNACIÓN DE CLASES CSS SEGÚN ESTADO DE SOLICITUD
        private static string ObtenerClaseCssEstado(string estadoCodigo)
        {
            if (string.IsNullOrEmpty(estadoCodigo)) return "info";

            return estadoCodigo switch
            {
                "BORRADOR" => "secondary",
                "ENVIADA_JP" => "warning",
                "APROBADA_JP" => "success",
                "ENVIADA_RH" => "warning",
                "APROBADA_RH" => "success",
                "ENVIADA_FINANZAS" => "warning",
                "APROBADA_FINANZAS" => "success",
                "ENVIADA_DIRECCION" => "warning",
                "APROBADA_DIRECCION" => "success",
                "RECHAZADA" => "danger",
                _ => "info"
            };
        }

        // MÉTODOS PARA ESTABLECER VALORES POR DEFECTO EN CASO DE ERROR
        private void EstablecerValoresPorDefectoEmpleado()
        {
            ViewBag.MisSolicitudesBorrador = 0;
            ViewBag.MisSolicitudesPendientes = 0;
            ViewBag.MisSolicitudesAprobadas = 0;
            ViewBag.MisSolicitudesRechazadas = 0;
            ViewBag.TotalAnticiposAutorizados = 0m;
            ViewBag.SolicitudesRecientes = new List<SolicitudesViaje>();
        }

        private void EstablecerValoresPorDefectoJP()
        {
            ViewBag.PendientesJP = 0;
            ViewBag.AprobadasJP = 0;
            ViewBag.RechazadasJP = 0;
            ViewBag.ComprobacionesPendientesRevision = 0;
            ViewBag.SolicitudesPendientesJP = new List<SolicitudesViaje>();
            ViewBag.ComprobacionesPendientesRevisionJP = new List<ComprobacionesViaje>();
            ViewBag.SolicitudesPendientes = new List<SolicitudDashboardViewModel>();
        }

        private void EstablecerValoresPorDefectoRH()
        {
            ViewBag.TotalSolicitudesActivas = 0;
            ViewBag.SolicitudesAprobadasDireccion = 0;
            ViewBag.SolicitudesBorradorOtros = 0;
            ViewBag.SolicitudesPendientesRH = new List<SolicitudesViaje>();
        }

        private void EstablecerValoresPorDefectoFinanzas()
        {
            ViewBag.SolicitudesActivas = 0;
            ViewBag.PendientesFinanzas = 0;
            ViewBag.PendientesPorSaldar = 0;
            ViewBag.AnticiposAutorizadosPorUsuario = 0m;
            ViewBag.SolicitudesPendientes = new List<SolicitudDashboardViewModel>();
        }

        private void EstablecerValoresPorDefectoDireccion()
        {
            ViewBag.SolicitudesActivas = 0;
            ViewBag.PendientesDireccion = 0;
            ViewBag.AnticiposAutorizados = 0m;
            ViewBag.SolicitudesPendientes = new List<SolicitudDashboardViewModel>();
        }

        private void EstablecerValoresPorDefectoAdmin()
        {
            ViewBag.SolicitudesActivas = 0;
            ViewBag.PendientesAprobacion = 0;
            ViewBag.TotalUsuariosActivos = 0;
            ViewBag.AnticiposTotalesAutorizados = 0m;
            ViewBag.SolicitudesPendientes = new List<SolicitudDashboardViewModel>();
        }

        private void EstablecerValoresPorDefecto()
        {
            ViewBag.SolicitudesPendientes = new List<SolicitudDashboardViewModel>();
            ViewBag.SolicitudesPendientesJP = new List<SolicitudesViaje>();
            ViewBag.SolicitudesPendientesRH = new List<SolicitudesViaje>();
            ViewBag.ComprobacionesPendientesRevisionJP = new List<ComprobacionesViaje>();
            ViewBag.SolicitudesRecientes = new List<SolicitudesViaje>();
        }

        // VISTA DE POLÍTICA DE PRIVACIDAD
        public IActionResult Privacy()
        {
            return View();
        }

        // PANEL DE ADMINISTRACIÓN SOLO PARA ROL ADMIN
        [Authorize(Policy = "AdminOnly")]
        public IActionResult AdminPanel()
        {
            return View();
        }
    }
} 
