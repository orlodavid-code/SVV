using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SVV.DTOs.Cotizacion;
using SVV.Models;
using SVV.Services;

namespace SVV.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class CotizacionesApiController : ControllerBase
    {
        private readonly ICotizacionService _cotizacionService;
        private readonly SvvContext _context;
        private readonly ILogger<CotizacionesApiController> _logger;

        // CONSTRUCTOR CON INYECCIÓN DE DEPENDENCIAS
        public CotizacionesApiController(
            ICotizacionService cotizacionService,
            SvvContext context,
            ILogger<CotizacionesApiController> logger)
        {
            _cotizacionService = cotizacionService;
            _context = context;
            _logger = logger;
        }

        // ENDPOINT PARA CÁLCULO COMPLETO DE COTIZACIÓN
        [HttpPost("calcular")]
        public async Task<IActionResult> CalcularCotizacion([FromBody] CalcularCotizacionDto dto)
        {
            try
            {
                _logger.LogInformation("Calculando cotización. Modo: {Modo}",
                    dto.EsCalculoAutomatico ? "Automático" : "Manual");

                var resultado = await _cotizacionService.CalcularAsync(dto);

                // VALIDACIÓN DE ERRORES EN EL CÁLCULO
                if (resultado.Errores.Any())
                {
                    _logger.LogWarning("Cotización inválida: {Errores}", string.Join(", ", resultado.Errores));
                    return BadRequest(new
                    {
                        success = false,
                        errors = resultado.Errores,
                        message = "Errores en la validación de la cotización"
                    });
                }

                // RESPUESTA EXITOSA CON DATOS CALCULADOS
                return Ok(new
                {
                    success = true,
                    data = resultado,
                    message = resultado.EsCalculoAutomatico
                        ? "Cotización calculada automáticamente"
                        : "Cotización calculada exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calcular cotización");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor",
                    detail = ex.Message
                });
            }
        }

        // ENDPOINT PARA CÁLCULO AUTOMÁTICO BASADO EN SOLICITUD EXISTENTE
        [HttpGet("calcular-auto/{solicitudId}")]
        public async Task<IActionResult> CalcularAutomatico(int solicitudId)
        {
            try
            {
                _logger.LogInformation("Iniciando cálculo automático para solicitud {SolicitudId}", solicitudId);

                // OBTENER DATOS DE SOLICITUD DESDE BASE DE DATOS
                var solicitud = await _context.SolicitudesViajes
                    .Include(s => s.Empleado)
                    .FirstOrDefaultAsync(s => s.Id == solicitudId);

                if (solicitud == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Solicitud no encontrada"
                    });
                }

                // CONSTRUCCIÓN DE DTO PARA CÁLCULO AUTOMÁTICO
                var dto = new CalcularCotizacionDto
                {
                    SolicitudViajeId = solicitudId,
                    EsCalculoAutomatico = true,
                    // EL ORIGEN SE DETERMINA EN EL SERVICIO SEGÚN REGLAS DE NEGOCIO
                    Origen = null,
                    Destino = solicitud.Destino,
                    FechaSalida = solicitud.FechaSalida,
                    FechaRegreso = solicitud.FechaRegreso,
                    NumeroPersonas = solicitud.NumeroPersonas ?? 1,
                    RequiereHospedaje = solicitud.RequiereHospedaje ?? false,
                    NochesHospedaje = solicitud.NochesHospedaje ?? 0,
                    MedioTraslado = solicitud.MedioTrasladoPrincipal ?? "Avión",
                    RequiereTaxiDomicilio = solicitud.RequiereTaxiDomicilio ?? false,
                    Transporte = new List<ConceptoItemDto>(),
                    Gasolina = new List<ConceptoItemDto>(),
                    UberTaxi = new List<ConceptoItemDto>(),
                    Casetas = new List<ConceptoItemDto>(),
                    Hospedaje = new List<ConceptoItemDto>(),
                    Alimentos = new List<ConceptoItemDto>()
                };

                // EJECUCIÓN DEL CÁLCULO MEDIANTE EL SERVICIO
                var resultado = await _cotizacionService.CalcularAsync(dto);

                if (resultado.Errores.Any())
                {
                    return BadRequest(new
                    {
                        success = false,
                        errors = resultado.Errores,
                        message = "Errores en el cálculo automático"
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = resultado,
                    message = "Cotización calculada automáticamente"
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

        // ENDPOINT PARA CÁLCULO SIMPLIFICADO (USADO EN FORMULARIOS RÁPIDOS)
        [HttpPost("calcular-simple")]
        public async Task<IActionResult> CalcularSimple([FromBody] CalcularSimpleDto dto)
        {
            try
            {
                _logger.LogInformation("Calculando cotización simple: {Origen} -> {Destino}", dto.Origen, dto.Destino);

                // CONVERSIÓN DE DTO SIMPLE A DTO COMPLETO
                var cotizacionDto = new CalcularCotizacionDto
                {
                    EsCalculoAutomatico = true,
                    Origen = dto.Origen,
                    Destino = dto.Destino,
                    FechaSalida = dto.FechaSalida,
                    FechaRegreso = dto.FechaRegreso,
                    NumeroPersonas = dto.NumeroPersonas,
                    RequiereHospedaje = dto.RequiereHospedaje,
                    NochesHospedaje = dto.NochesHospedaje,
                    MedioTraslado = dto.MedioTraslado,
                    RequiereTaxiDomicilio = dto.RequiereTaxiDomicilio,

                    // INICIALIZACIÓN DE LISTAS VACÍAS
                    Transporte = new List<ConceptoItemDto>(),
                    Gasolina = new List<ConceptoItemDto>(),
                    UberTaxi = new List<ConceptoItemDto>(),
                    Casetas = new List<ConceptoItemDto>(),
                    Hospedaje = new List<ConceptoItemDto>(),
                    Alimentos = new List<ConceptoItemDto>()
                };

                var resultado = await _cotizacionService.CalcularAsync(cotizacionDto);

                if (resultado.Errores.Any())
                {
                    return BadRequest(new
                    {
                        success = false,
                        errors = resultado.Errores,
                        message = "Errores en el cálculo"
                    });
                }

                // RESPUESTA CON ESTRUCTURA SIMPLIFICADA PARA FRONTEND
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        total = resultado.TotalGeneral,
                        distancia = resultado.DistanciaCalculada,
                        desglose = new
                        {
                            transporte = resultado.TotalTransporte,
                            gasolina = resultado.TotalGasolina,
                            uberTaxi = resultado.TotalUberTaxi,
                            casetas = resultado.TotalCasetas,
                            hospedaje = resultado.TotalHospedaje,
                            alimentos = resultado.TotalAlimentos
                        },
                        alertas = resultado.Alertas
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en cálculo simple");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor"
                });
            }
        }

        // ENDPOINT DE PRUEBA PARA VALIDAR FUNCIONAMIENTO DEL SERVICIO
        [HttpGet("test")]
        public IActionResult Test()
        {
            var testDto = new CalcularCotizacionDto
            {
                EsCalculoAutomatico = true,
                Origen = "CDMX",
                Destino = "Guadalajara",
                FechaSalida = new DateOnly(2024, 1, 15),
                FechaRegreso = new DateOnly(2024, 1, 18),
                NumeroPersonas = 2,
                RequiereHospedaje = true,
                NochesHospedaje = 3,
                MedioTraslado = "Avión",
                RequiereTaxiDomicilio = true,

                // LISTAS VACÍAS PARA PRUEBA
                Transporte = new List<ConceptoItemDto>(),
                Gasolina = new List<ConceptoItemDto>(),
                UberTaxi = new List<ConceptoItemDto>(),
                Casetas = new List<ConceptoItemDto>(),
                Hospedaje = new List<ConceptoItemDto>(),
                Alimentos = new List<ConceptoItemDto>()
            };

            try
            {
                var resultado = _cotizacionService.Calcular(testDto);
                return Ok(new
                {
                    success = true,
                    data = resultado,
                    message = "Test ejecutado correctamente"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error en test",
                    error = ex.Message
                });
            }
        }

        // ENDPOINT DE SALUD PARA MONITOREO DE LA API
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new
            {
                message = "API de cotizaciones funcionando",
                timestamp = DateTime.UtcNow,
                status = "OK"
            });
        }
    }

    // DTO PARA CÁLCULOS SIMPLIFICADOS DESDE FRONTEND
    public class CalcularSimpleDto
    {
        public string Origen { get; set; } = string.Empty;
        public string Destino { get; set; } = string.Empty;
        public DateOnly? FechaSalida { get; set; }
        public DateOnly? FechaRegreso { get; set; }
        public int NumeroPersonas { get; set; } = 1;
        public bool RequiereHospedaje { get; set; } = false;
        public int NochesHospedaje { get; set; } = 0;
        public string MedioTraslado { get; set; } = "Avión";
        public bool RequiereTaxiDomicilio { get; set; } = false;
    }
}