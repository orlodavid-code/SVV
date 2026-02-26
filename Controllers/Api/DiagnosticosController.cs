using Microsoft.AspNetCore.Mvc;
using SVV.Services;
using SVV.DTOs.Cotizacion;

namespace SVV.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiagnosticosController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ICotizacionService _cotizacionService;
        private readonly ILogger<DiagnosticosController> _logger;

        // CONSTRUCTOR CON INYECCIÓN DE DEPENDENCIAS PARA CONFIGURACIÓN Y SERVICIOS
        public DiagnosticosController(
            IConfiguration configuration,
            ICotizacionService cotizacionService,
            ILogger<DiagnosticosController> logger)
        {
            _configuration = configuration;
            _cotizacionService = cotizacionService;
            _logger = logger;
        }

        // ENDPOINT DE VERIFICACIÓN DE SALUD GENERAL DEL SISTEMA
        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Service = "Sistema Viáticos Viamtek",
                Version = "2.0.0"
            });
        }

        // ENDPOINT PARA VALIDAR CONFIGURACIONES CRÍTICAS DE LA APLICACIÓN
        [HttpGet("config-check")]
        public IActionResult ConfigCheck()
        {
            // VERIFICACIÓN DE LLAVES DE API Y CADENAS DE CONEXIÓN
            var googleApiKey = _configuration["CotizacionConfig:GoogleMapsApiKey"];
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            return Ok(new
            {
                GoogleMapsApiKeyConfigured = !string.IsNullOrEmpty(googleApiKey),
                DatabaseConfigured = !string.IsNullOrEmpty(connectionString),
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
            });
        }

        // ENDPOINT DE PRUEBA PARA VALIDAR FUNCIONAMIENTO DEL SERVICIO DE COTIZACIONES
        [HttpGet("test-cotizacion")]
        public async Task<IActionResult> TestCotizacion()
        {
            try
            {
                // DATOS DE PRUEBA ESTÁTICOS PARA VALIDAR CÁLCULOS
                var testDto = new CalcularCotizacionDto
                {
                    EsCalculoAutomatico = true,
                    Origen = "PUEBLA",
                    Destino = "CDMX",
                    FechaSalida = new DateOnly(2024, 1, 15),
                    FechaRegreso = new DateOnly(2024, 1, 18),
                    NumeroPersonas = 2,
                    RequiereHospedaje = true,
                    NochesHospedaje = 3,
                    MedioTraslado = "Vehículo Utilitario",
                    RequiereTaxiDomicilio = true,

                    // LISTAS VACÍAS QUE EL SERVICIO COMPLETARÁ CON CÁLCULOS
                    Transporte = new List<ConceptoItemDto>(),
                    Gasolina = new List<ConceptoItemDto>(),
                    UberTaxi = new List<ConceptoItemDto>(),
                    Casetas = new List<ConceptoItemDto>(),
                    Hospedaje = new List<ConceptoItemDto>(),
                    Alimentos = new List<ConceptoItemDto>()
                };

                // EJECUCIÓN DE PRUEBA CON EL SERVICIO DE COTIZACIONES
                var resultado = await _cotizacionService.CalcularAsync(testDto);

                return Ok(new
                {
                    Success = true,
                    Test = "Cálculo automático de cotización",
                    Resultado = new
                    {
                        resultado.TotalTransporte,
                        resultado.TotalGasolina,
                        resultado.TotalUberTaxi,
                        resultado.TotalCasetas,
                        resultado.TotalHospedaje,
                        resultado.TotalAlimentos,
                        resultado.TotalGeneral,
                        resultado.DistanciaCalculada,
                        Alertas = resultado.Alertas,
                        Errores = resultado.Errores
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en test de cotización");
                return StatusCode(500, new
                {
                    Success = false,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }

        // ENDPOINT DE PING PARA VERIFICAR DISPONIBILIDAD BÁSICA
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new
            {
                Message = "API de diagnóstico funcionando",
                Timestamp = DateTime.UtcNow,
                Server = Environment.MachineName
            });
        }
    }
}