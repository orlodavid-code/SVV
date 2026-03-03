using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SVV.DTOs.Cotizacion;
using SVV.Models;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using System.Net;

namespace SVV.Services
{
    // Interfaz que define los métodos del servicio de cotizaciones
    public interface ICotizacionService
    {
        ResultadoCotizacionDto Calcular(CalcularCotizacionDto dto);
        Task<ResultadoCotizacionDto> CalcularAsync(CalcularCotizacionDto dto);
        Task<Dictionary<string, object>> ObtenerConfiguracionEstadosAsync();
        Task<List<EstadoInfo>> ObtenerEstadosAsync();
        Task<List<string>> ObtenerMunicipiosPorEstadoAsync(string estado);
    }

    // Implementación del servicio de cotizaciones con lógica compleja de cálculo
    public class CotizacionService : ICotizacionService
    {
        private readonly SvvContext _context;
        private readonly ILogger<CotizacionService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly CotizacionConfig _cotizacionConfig;
        private readonly IWebHostEnvironment _env;
        private List<EstadoMexico> _estadosMexico = new List<EstadoMexico>();
        private List<MunicipioDetalle> _municipios = new List<MunicipioDetalle>();

        // Constructor con inyección de dependencias
        public CotizacionService(
            SvvContext context,
            ILogger<CotizacionService> logger,
            IHttpClientFactory httpClientFactory,
            IOptions<CotizacionConfig> cotizacionConfig,
            IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _cotizacionConfig = cotizacionConfig.Value;
            _env = env;
            CargarEstadosMexico();
            CargarMunicipios();
        }

        private void CargarEstadosMexico()
        {
            try
            {
                var path = Path.Combine(_env.WebRootPath, "data", "estados-mexico.json");
                _logger.LogInformation("Cargando estados desde: {Path}", path);
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<EstadosMexicoData>(json);
                    _estadosMexico = data?.Estados ?? new List<EstadoMexico>();
                    _logger.LogInformation("Cargados {Count} estados de México", _estadosMexico.Count);
                }
                else
                {
                    _logger.LogWarning("Archivo de estados-mexico.json no encontrado en {Path}", path);
                    _estadosMexico = CargarEstadosPorDefecto();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar estados de México");
                _estadosMexico = CargarEstadosPorDefecto();
            }
        }

        private void CargarMunicipios()
        {
            try
            {
                var path = Path.Combine(_env.WebRootPath, "data", "municipios.json");
                _logger.LogInformation("Cargando municipios desde: {Path}", path);
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<RootMunicipios>(json);
                    _municipios = data?.Estados?.SelectMany(e => e.Municipios).ToList() ?? new List<MunicipioDetalle>();
                    _logger.LogInformation("Cargados {Count} municipios con coordenadas", _municipios.Count);
                }
                else
                {
                    _logger.LogWarning("Archivo de municipios.json no encontrado en {Path}", path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar municipios");
            }
        }

        // Lista de respaldo por si falla la carga del archivo
        private List<EstadoMexico> CargarEstadosPorDefecto()
        {
            return new List<EstadoMexico>
            {
                new EstadoMexico { Nombre = "Aguascalientes", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Baja California", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Baja California Sur", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Campeche", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Chiapas", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Chihuahua", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Ciudad de México", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Coahuila de Zaragoza", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Colima", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Durango", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Guanajuato", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Guerrero", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Hidalgo", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Jalisco", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "México", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Michoacán de Ocampo", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Morelos", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Nayarit", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Nuevo León", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Oaxaca", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Puebla", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Querétaro", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Quintana Roo", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "San Luis Potosí", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Sinaloa", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Sonora", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Tabasco", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Tamaulipas", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Tlaxcala", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Veracruz de Ignacio de la Llave", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Yucatán", Municipios = new List<string>() },
                new EstadoMexico { Nombre = "Zacatecas", Municipios = new List<string>() }
            };
        }

        // Método asíncrono para cálculo de cotización
        public async Task<ResultadoCotizacionDto> CalcularAsync(CalcularCotizacionDto dto)
        {
            _logger.LogInformation("Iniciando cálculo de cotización. Automático: {EsAutomatico}", dto.EsCalculoAutomatico);

            if (dto.EsCalculoAutomatico)
            {
                return await CalcularAutomaticoAsync(dto);
            }
            else
            {
                return CalcularManual(dto);
            }
        }

        // Método síncrono para cálculo de cotización
        public ResultadoCotizacionDto Calcular(CalcularCotizacionDto dto)
        {
            if (dto.EsCalculoAutomatico)
            {
                return CalcularAutomaticoAsync(dto).GetAwaiter().GetResult();
            }
            else
            {
                return CalcularManual(dto);
            }
        }

        // Cálculo manual sumando los valores proporcionados
        private ResultadoCotizacionDto CalcularManual(CalcularCotizacionDto dto)
        {
            var result = new ResultadoCotizacionDto
            {
                EsCalculoAutomatico = false
            };

            result.TotalTransporte = dto.Transporte.Sum(x => x.Precio);
            result.TotalGasolina = dto.Gasolina.Sum(x => x.Precio);
            result.TotalUberTaxi = dto.UberTaxi.Sum(x => x.Precio);
            result.TotalCasetas = dto.Casetas.Sum(x => x.Precio);
            result.TotalHospedaje = dto.Hospedaje.Sum(x => x.Precio);
            result.TotalAlimentos = dto.Alimentos.Sum(x => x.Precio);

            result.TotalGeneral =
                result.TotalTransporte +
                result.TotalGasolina +
                result.TotalUberTaxi +
                result.TotalCasetas +
                result.TotalHospedaje +
                result.TotalAlimentos;

            if (result.TotalGeneral <= 0)
                result.Errores.Add("La cotización debe tener al menos un monto mayor a $0.");

            var todosPrecios = dto.Transporte.Select(x => x.Precio)
                .Concat(dto.Gasolina.Select(x => x.Precio))
                .Concat(dto.UberTaxi.Select(x => x.Precio))
                .Concat(dto.Casetas.Select(x => x.Precio))
                .Concat(dto.Hospedaje.Select(x => x.Precio))
                .Concat(dto.Alimentos.Select(x => x.Precio));

            if (todosPrecios.Any(p => p < 0))
                result.Errores.Add("No se permiten precios negativos.");

            return result;
        }

        // Cálculo automático usando datos de la solicitud y APIs externas
        private async Task<ResultadoCotizacionDto> CalcularAutomaticoAsync(CalcularCotizacionDto dto)
        {
            var resultado = new ResultadoCotizacionDto
            {
                EsCalculoAutomatico = true,
                Mensaje = "Cotización calculada automáticamente basada en la solicitud"
            };

            try
            {
                // Obtener datos completos de la solicitud desde BD
                if (dto.SolicitudViajeId > 0)
                {
                    var solicitud = await _context.SolicitudesViajes
                        .Include(s => s.Empleado)
                        .FirstOrDefaultAsync(s => s.Id == dto.SolicitudViajeId);

                    if (solicitud != null)
                    {
                        dto.Origen = solicitud.Empleado?.UbicacionBase ?? "Puebla, México";
                        dto.Destino = solicitud.Destino;
                        dto.FechaSalida = solicitud.FechaSalida;
                        dto.FechaRegreso = solicitud.FechaRegreso;
                        dto.NumeroPersonas = solicitud.NumeroPersonas ?? 1;
                        dto.RequiereHospedaje = solicitud.RequiereHospedaje ?? false;
                        dto.NochesHospedaje = solicitud.NochesHospedaje ?? 0;

                        // Decodificar HTML entities en el medio de traslado
                        var medioTrasladoPrincipal = solicitud.MedioTrasladoPrincipal ?? "Avión";
                        dto.MedioTraslado = WebUtility.HtmlDecode(medioTrasladoPrincipal);

                        dto.RequiereTaxiDomicilio = solicitud.RequiereTaxiDomicilio ?? false;
                        dto.DireccionTaxiOrigen = solicitud.DireccionTaxiOrigen;
                        dto.DireccionTaxiDestino = solicitud.DireccionTaxiDestino;
                        dto.HoraSalida = solicitud.HoraSalida;
                        dto.HoraRegreso = solicitud.HoraRegreso;
                    }
                }

                if (string.IsNullOrEmpty(dto.Origen) || string.IsNullOrEmpty(dto.Destino))
                {
                    resultado.Errores.Add("Se requiere origen y destino para cálculo automático");
                    return resultado;
                }

                // Formatear direcciones para la API de Google Maps
                dto.Origen = FormatearDireccionParaGoogleMaps(dto.Origen, "Puebla");
                dto.Destino = FormatearDireccionParaGoogleMaps(dto.Destino, "México");

                _logger.LogInformation("Origen formateado para Google Maps: {Origen}", dto.Origen);
                _logger.LogInformation("Destino formateado para Google Maps: {Destino}", dto.Destino);

                resultado.DistanciaCalculada = await CalcularDistanciaEstimada(dto.Origen, dto.Destino);

                var (estadoDestino, zonaAlimentos, zonaHospedaje, esCapital) = ClasificarDestino(dto.Destino);
                resultado.EstadoDestino = estadoDestino;
                resultado.ZonaAlimentos = zonaAlimentos;
                resultado.ZonaHospedaje = zonaHospedaje;

                await CalcularTransporteAutomatico(dto, resultado, estadoDestino);
                await CalcularGasolinaAutomatico(dto, resultado);
                await CalcularUberTaxiAutomatico(dto, resultado);
                await CalcularCasetasAutomatico(dto, resultado);
                await CalcularHospedajeAutomatico(dto, resultado, zonaHospedaje);
                await CalcularAlimentosAutomatico(dto, resultado, zonaAlimentos);

                resultado.TotalGeneral =
                    resultado.TotalTransporte +
                    resultado.TotalGasolina +
                    resultado.TotalUberTaxi +
                    resultado.TotalCasetas +
                    resultado.TotalHospedaje +
                    resultado.TotalAlimentos;

                AplicarReglasNegocio(dto, resultado);

                _logger.LogInformation("Cálculo automático exitoso. Total: {Total:C}", resultado.TotalGeneral);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en cálculo automático");
                resultado.Errores.Add($"Error en cálculo automático: {ex.Message}");
            }

            return resultado;
        }

        // Clasifica el destino para determinar tarifas adecuadas
        private (string estado, string zonaAlimentos, string zonaHospedaje, bool esCapital) ClasificarDestino(string destino)
        {
            if (string.IsNullOrEmpty(destino))
                return ("Desconocido", "MEDIA", "MEDIA", false);

            var destinoUpper = destino.ToUpper();
            string estadoEncontrado = "Desconocido";
            bool esCapital = false;

            var capitales = new Dictionary<string, string>
            {
                {"AGUASCALIENTES", "Aguascalientes"},
                {"MEXICALI", "Baja California"},
                {"LA PAZ", "Baja California Sur"},
                {"CAMPECHE", "Campeche"},
                {"TUXTLA GUTIÉRREZ", "Chiapas"},
                {"CHIHUAHUA", "Chihuahua"},
                {"CIUDAD DE MÉXICO", "Ciudad de México"},
                {"SALTILLO", "Coahuila de Zaragoza"},
                {"COLIMA", "Colima"},
                {"DURANGO", "Durango"},
                {"GUANAJUATO", "Guanajuato"},
                {"CHILPANCINGO", "Guerrero"},
                {"PACHUCA", "Hidalgo"},
                {"GUADALAJARA", "Jalisco"},
                {"TOLUCA", "México"},
                {"MORELIA", "Michoacán de Ocampo"},
                {"CUERNAVACA", "Morelos"},
                {"TEPIC", "Nayarit"},
                {"MONTERREY", "Nuevo León"},
                {"OAXACA", "Oaxaca"},
                {"PUEBLA", "Puebla"},
                {"QUERÉTARO", "Querétaro"},
                {"CHETUMAL", "Quintana Roo"},
                {"SAN LUIS POTOSÍ", "San Luis Potosí"},
                {"CULIACÁN", "Sinaloa"},
                {"HERMOSILLO", "Sonora"},
                {"VILLAHERMOSA", "Tabasco"},
                {"CIUDAD VICTORIA", "Tamaulipas"},
                {"TLAXCALA", "Tlaxcala"},
                {"XALAPA", "Veracruz de Ignacio de la Llave"},
                {"MÉRIDA", "Yucatán"},
                {"ZACATECAS", "Zacatecas"}
            };

            foreach (var capital in capitales)
            {
                if (destinoUpper.Contains(capital.Key))
                {
                    estadoEncontrado = capital.Value;
                    esCapital = true;
                    break;
                }
            }

            if (estadoEncontrado == "Desconocido")
            {
                foreach (var estado in _estadosMexico)
                {
                    if (destinoUpper.Contains(estado.Nombre.ToUpper()))
                    {
                        estadoEncontrado = estado.Nombre;
                        break;
                    }

                    foreach (var municipio in estado.Municipios)
                    {
                        if (destinoUpper.Contains(municipio.ToUpper()))
                        {
                            estadoEncontrado = estado.Nombre;
                            break;
                        }
                    }
                    if (estadoEncontrado != "Desconocido") break;
                }
            }

            string zonaAlimentos = ClasificarZonaAlimentos(estadoEncontrado, destinoUpper);
            string zonaHospedaje = ClasificarZonaHospedaje(estadoEncontrado, destinoUpper, esCapital);

            _logger.LogInformation("Destino clasificado: {Estado}, Alimentos: {ZonaAli}, Hospedaje: {ZonaHosp}, Capital: {EsCapital}",
                estadoEncontrado, zonaAlimentos, zonaHospedaje, esCapital);

            return (estadoEncontrado, zonaAlimentos, zonaHospedaje, esCapital);
        }

        // Determina la zona de alimentos según estado y destino
        private string ClasificarZonaAlimentos(string estado, string destinoUpper)
        {
            var estadosEconomicos = new[] { "Tlaxcala", "Hidalgo", "Morelos", "Puebla", "Oaxaca", "Guerrero", "Chiapas" };
            if (estadosEconomicos.Contains(estado))
                return "ECONOMICA";

            if (destinoUpper.Contains("CANCUN") || destinoUpper.Contains("PLAYA") ||
                destinoUpper.Contains("LOS CABOS") || destinoUpper.Contains("TIJUANA") ||
                destinoUpper.Contains("PUERTO VALLARTA") || destinoUpper.Contains("ACAPULCO"))
                return "TURISTICA";

            var estadosAltos = new[] { "Ciudad de México", "Nuevo León", "Jalisco", "Quintana Roo", "Baja California Sur" };
            if (estadosAltos.Contains(estado))
                return "ALTA";

            return "MEDIA";
        }

        // Determina la zona de hospedaje según estado y tipo de destino
        private string ClasificarZonaHospedaje(string estado, string destinoUpper, bool esCapital)
        {
            if (destinoUpper.Contains("CANCUN") || destinoUpper.Contains("PLAYA") ||
                destinoUpper.Contains("LOS CABOS") || destinoUpper.Contains("TIJUANA") ||
                destinoUpper.Contains("PUERTO VALLARTA") || destinoUpper.Contains("ACAPULCO"))
                return "TURISTICA";

            var ciudadesGrandes = new[] { "Ciudad de México", "Monterrey", "Guadalajara", "Puebla" };
            if (ciudadesGrandes.Contains(estado) || (esCapital && estado != "Desconocido"))
                return "GRANDE";

            var ciudadesPequenas = new[] { "Tlaxcala", "Colima", "Campeche", "Aguascalientes" };
            if (ciudadesPequenas.Contains(estado))
                return "PEQUENA";

            return "MEDIA";
        }

        // Formatea direcciones para la API de Google Maps
        private string FormatearDireccionParaGoogleMaps(string direccion, string ubicacionPorDefecto)
        {
            if (string.IsNullOrEmpty(direccion))
                return $"{ubicacionPorDefecto}, México";

            if (direccion.Contains(","))
                return direccion;

            var direccionLower = direccion.ToLower();

            var mapeoEstados = new Dictionary<string, string>
            {
                {"baja california", "Tijuana, Baja California, México"},
                {"baja california sur", "La Paz, Baja California Sur, México"},
                {"campeche", "Campeche, Campeche, México"},
                {"chiapas", "Tuxtla Gutiérrez, Chiapas, México"},
                {"chihuahua", "Chihuahua, Chihuahua, México"},
                {"ciudad de méxico", "Ciudad de México, México"},
                {"coahuila", "Saltillo, Coahuila, México"},
                {"colima", "Colima, Colima, México"},
                {"durango", "Durango, Durango, México"},
                {"guanajuato", "Guanajuato, Guanajuato, México"},
                {"guerrero", "Chilpancingo, Guerrero, México"},
                {"hidalgo", "Pachuca, Hidalgo, México"},
                {"jalisco", "Guadalajara, Jalisco, México"},
                {"méxico", "Toluca, Estado de México, México"},
                {"michoacán", "Morelia, Michoacán, México"},
                {"morelos", "Cuernavaca, Morelos, México"},
                {"nayarit", "Tepic, Nayarit, México"},
                {"nuevo león", "Monterrey, Nuevo León, México"},
                {"oaxaca", "Oaxaca, Oaxaca, México"},
                {"puebla", "Puebla, Puebla, México"},
                {"querétaro", "Querétaro, Querétaro, México"},
                {"quintana roo", "Chetumal, Quintana Roo, México"},
                {"san luis potosí", "San Luis Potosí, San Luis Potosí, México"},
                {"sinaloa", "Culiacán, Sinaloa, México"},
                {"sonora", "Hermosillo, Sonora, México"},
                {"tabasco", "Villahermosa, Tabasco, México"},
                {"tamaulipas", "Ciudad Victoria, Tamaulipas, México"},
                {"tlaxcala", "Tlaxcala, Tlaxcala, México"},
                {"veracruz", "Xalapa, Veracruz, México"},
                {"yucatán", "Mérida, Yucatán, México"},
                {"zacatecas", "Zacatecas, Zacatecas, México"}
            };

            foreach (var mapeo in mapeoEstados)
            {
                if (direccionLower.Contains(mapeo.Key))
                    return mapeo.Value;
            }

            return $"{direccion}, México";
        }

        // Calcula días totales del viaje
        private int CalcularDiasViaje(DateOnly? fechaSalida, DateOnly? fechaRegreso)
        {
            if (!fechaSalida.HasValue || !fechaRegreso.HasValue)
                return 1;

            var dias = (fechaRegreso.Value.DayNumber - fechaSalida.Value.DayNumber) + 1;
            return Math.Max(1, dias);
        }

        // Calcula distancia usando Google Maps API o estimación local
        private async Task<decimal> CalcularDistanciaEstimada(string origen, string destino)
        {
            try
            {
                var apiKey = _cotizacionConfig.GoogleMapsApiKey;

                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("No hay API key de Google Maps configurada. Usando distancia estimada.");
                    return await Task.FromResult(CalcularDistanciaEstimadaLocal(origen, destino));
                }

                var httpClient = _httpClientFactory.CreateClient();

                var url = $"https://maps.googleapis.com/maps/api/distancematrix/json?" +
                          $"origins={Uri.EscapeDataString(origen)}&" +
                          $"destinations={Uri.EscapeDataString(destino)}&" +
                          $"key={apiKey}&mode=driving&units=metric&language=es&region=mx";

                _logger.LogInformation("Consultando Google Maps API...");

                var response = await httpClient.GetAsync(url);

                _logger.LogInformation("Google Maps Response Status: {StatusCode}", response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    var contentParaLog = content.Replace(apiKey, "API_KEY_OCULTA");
                    _logger.LogDebug("Respuesta de Google Maps: {Content}", contentParaLog);

                    var result = JsonSerializer.Deserialize<GoogleMapsDistanceResponse>(content);

                    if (result?.Rows?.FirstOrDefault()?.Elements?.FirstOrDefault()?.Status == "OK")
                    {
                        var metros = result.Rows[0].Elements[0].Distance.Value;
                        var distanciaKm = (decimal)(metros / 1000.0);
                        var distanciaTexto = result.Rows[0].Elements[0].Distance.Text;

                        _logger.LogInformation("Distancia calculada con Google Maps: {DistanciaTexto} ({DistanciaKm} km)",
                            distanciaTexto, distanciaKm);
                        return distanciaKm;
                    }
                    else
                    {
                        var status = result?.Rows?.FirstOrDefault()?.Elements?.FirstOrDefault()?.Status ?? "NO_RESPONSE";
                        _logger.LogWarning("Google Maps no devolvió distancia válida. Status: {Status}", status);

                        if (result != null)
                        {
                            _logger.LogWarning("Direcciones interpretadas por Google:");
                            if (result.OriginAddresses != null && result.OriginAddresses.Any())
                                _logger.LogWarning("Origen: {Origen}", string.Join(", ", result.OriginAddresses));
                            if (result.DestinationAddresses != null && result.DestinationAddresses.Any())
                                _logger.LogWarning("Destino: {Destino}", string.Join(", ", result.DestinationAddresses));
                        }
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Error HTTP de Google Maps: {StatusCode} - {Content}",
                        response.StatusCode, errorContent);
                }

                _logger.LogWarning("Google Maps falló. Usando distancia estimada local.");
                return await Task.FromResult(CalcularDistanciaEstimadaLocal(origen, destino));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calcular distancia con Google Maps");
                return await Task.FromResult(CalcularDistanciaEstimadaLocal(origen, destino));
            }
        }

        // Calcula distancia estimada localmente usando coordenadas (Haversine)
        private decimal CalcularDistanciaEstimadaLocal(string origen, string destino)
        {
            _logger.LogInformation("Calculando distancia local estimada: {Origen} -> {Destino}", origen, destino);

            var (origenLat, origenLng) = ObtenerCoordenadas(origen);
            var (destinoLat, destinoLng) = ObtenerCoordenadas(destino);

            if (!origenLat.HasValue || !origenLng.HasValue || !destinoLat.HasValue || !destinoLng.HasValue)
            {
                _logger.LogWarning("No se encontraron coordenadas para origen o destino. Usando valor por defecto 300 km.");
                return 300m;
            }

            var distanciaLineaRecta = CalcularDistanciaHaversine(
                origenLat.Value, origenLng.Value,
                destinoLat.Value, destinoLng.Value);

            // Factor de corrección por carretera (aprox 15% más)
            var distanciaCarretera = distanciaLineaRecta * 1.15;

            _logger.LogInformation("Distancia calculada: {Distancia:F0} km (línea recta: {Recta:F0} km)", distanciaCarretera, distanciaLineaRecta);
            return Math.Round((decimal)distanciaCarretera, 0);
        }

        // Obtiene coordenadas de un lugar (origen o destino) buscando en municipios.json
        private (double? lat, double? lng) ObtenerCoordenadas(string lugar)
        {
            if (string.IsNullOrEmpty(lugar))
                return (null, null);

            // Tomar solo la primera parte antes de la coma (si existe)
            var lugarLimpio = lugar.Split(',')[0].Trim();

            // Normalizar quitando acentos
            var lugarNormalizado = lugarLimpio.ToLower().Normalize(System.Text.NormalizationForm.FormD);
            lugarNormalizado = new string(lugarNormalizado
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray());

            // Buscar en municipios por nombre (coincidencia parcial)
            var municipio = _municipios.FirstOrDefault(m =>
                m.Nombre.ToLower().Normalize(System.Text.NormalizationForm.FormD)
                    .Contains(lugarNormalizado) ||
                lugarNormalizado.Contains(m.Nombre.ToLower().Normalize(System.Text.NormalizationForm.FormD)));

            if (municipio != null)
                return (municipio.Latitud, municipio.Longitud);

            // Si no se encuentra, intentar extraer el estado y buscar su capital
            foreach (var estado in _estadosMexico)
            {
                if (lugarNormalizado.Contains(estado.Nombre.ToLower()))
                {
                    var capital = ObtenerCapitalPorEstado(estado.Nombre);
                    if (!string.IsNullOrEmpty(capital))
                    {
                        var capitalMun = _municipios.FirstOrDefault(m =>
                            m.Nombre.Equals(capital, StringComparison.OrdinalIgnoreCase));
                        if (capitalMun != null)
                            return (capitalMun.Latitud, capitalMun.Longitud);
                    }
                    break;
                }
            }

            return (null, null);
        }

        // Método auxiliar para obtener la capital de un estado
        private string ObtenerCapitalPorEstado(string estado)
        {
            return estado switch
            {
                "Aguascalientes" => "Aguascalientes",
                "Baja California" => "Mexicali",
                "Baja California Sur" => "La Paz",
                "Campeche" => "Campeche",
                "Chiapas" => "Tuxtla Gutiérrez",
                "Chihuahua" => "Chihuahua",
                "Ciudad de México" => "Ciudad de México",
                "Coahuila de Zaragoza" => "Saltillo",
                "Colima" => "Colima",
                "Durango" => "Durango",
                "Guanajuato" => "Guanajuato",
                "Guerrero" => "Chilpancingo de los Bravo",
                "Hidalgo" => "Pachuca de Soto",
                "Jalisco" => "Guadalajara",
                "México" => "Toluca",
                "Michoacán de Ocampo" => "Morelia",
                "Morelos" => "Cuernavaca",
                "Nayarit" => "Tepic",
                "Nuevo León" => "Monterrey",
                "Oaxaca" => "Oaxaca de Juárez",
                "Puebla" => "Puebla",
                "Querétaro" => "Querétaro",
                "Quintana Roo" => "Chetumal",
                "San Luis Potosí" => "San Luis Potosí",
                "Sinaloa" => "Culiacán",
                "Sonora" => "Hermosillo",
                "Tabasco" => "Villahermosa",
                "Tamaulipas" => "Ciudad Victoria",
                "Tlaxcala" => "Tlaxcala",
                "Veracruz de Ignacio de la Llave" => "Xalapa",
                "Yucatán" => "Mérida",
                "Zacatecas" => "Zacatecas",
                _ => null
            };
        }

        // Fórmula Haversine para calcular distancia en km entre dos puntos geográficos
        private double CalcularDistanciaHaversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Radio de la Tierra en km
            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        // Calcula costo de transporte según medio seleccionado
        private async Task CalcularTransporteAutomatico(CalcularCotizacionDto dto, ResultadoCotizacionDto resultado, string estadoDestino)
        {
            decimal costo = 0;
            var detalles = new List<ConceptoItemDto>();

            // Determinar si es vehículo propio
            bool esVehiculoPropio = EsVehiculo(dto.MedioTraslado);
            string medioDecodificado = WebUtility.HtmlDecode(dto.MedioTraslado ?? "");
            string medioParaComparar = medioDecodificado.ToLower();

            _logger.LogInformation("DETECCIÓN - Medio: '{Medio}', Es vehículo: {EsVehiculo}",
                dto.MedioTraslado, esVehiculoPropio);

            if (esVehiculoPropio)
            {
                // No se agrega ningún detalle, total 0 y lista vacía
                costo = 0;
                detalles.Clear();
            }
            // Cálculo para avión
            else if (medioParaComparar.Contains("avión") || medioParaComparar.Contains("avion") || medioParaComparar.Contains("vuelo"))
            {
                var distancia = resultado.DistanciaCalculada ?? 0;

                if (distancia < 500)
                    costo = dto.NumeroPersonas * 1800;
                else if (distancia < 1500)
                    costo = dto.NumeroPersonas * 2800;
                else
                    costo = dto.NumeroPersonas * 4500;

                detalles.Add(new ConceptoItemDto
                {
                    Precio = costo,
                    Descripcion = $"Vuelo a {estadoDestino} ({dto.NumeroPersonas} personas)"
                });
            }
            // Cálculo para autobús
            else if (medioParaComparar.Contains("autobús") || medioParaComparar.Contains("autobus") ||
                     medioParaComparar.Contains("bus") || medioParaComparar.Contains("camión"))
            {
                var distancia = resultado.DistanciaCalculada ?? 0;
                decimal tarifaPorPersona;

                if (distancia < 100)
                    tarifaPorPersona = 150;
                else if (distancia < 200)
                    tarifaPorPersona = 250;
                else if (distancia < 400)
                    tarifaPorPersona = 400;
                else if (distancia < 600)
                    tarifaPorPersona = 600;
                else
                    tarifaPorPersona = 800;

                var precioPorPersona = tarifaPorPersona * 2;
                costo = precioPorPersona * dto.NumeroPersonas;

                if (dto.NumeroPersonas == 1)
                {
                    detalles.Add(new ConceptoItemDto
                    {
                        Precio = precioPorPersona,
                        Descripcion = $"Pasaje de autobús (ida y vuelta)"
                    });
                }
                else
                {
                    for (int i = 0; i < dto.NumeroPersonas; i++)
                    {
                        detalles.Add(new ConceptoItemDto
                        {
                            Precio = precioPorPersona,
                            Descripcion = $"Pasaje de autobús - Persona {i + 1}"
                        });
                    }
                }

                resultado.Alertas.Add($"Autobús calculado: ${tarifaPorPersona} por trayecto × 2 = ${precioPorPersona}/persona");
            }
            // Cálculo para taxi/uber local
            else if (medioParaComparar.Contains("taxi") || medioParaComparar.Contains("uber") ||
                     medioParaComparar.Contains("didi") || medioParaComparar.Contains("cabify"))
            {
                var diasViaje = CalcularDiasViaje(dto.FechaSalida, dto.FechaRegreso);
                var diasLaborables = Math.Max(1, diasViaje - (dto.RequiereHospedaje ? 0 : 1));
                var viajesPorDia = 2;
                var tarifaPorViaje = 70;
                var totalViajes = diasLaborables * viajesPorDia * dto.NumeroPersonas;
                costo = totalViajes * tarifaPorViaje;

                detalles.Add(new ConceptoItemDto
                {
                    Precio = costo,
                    Descripcion = $"{dto.MedioTraslado} ({diasLaborables} días, {totalViajes} viajes de ${tarifaPorViaje})"
                });
            }
            // Cálculo para transporte público
            else if (medioParaComparar.Contains("transporte público") || medioParaComparar.Contains("transporte") ||
                     medioParaComparar.Contains("público") || medioParaComparar.Contains("metro") ||
                     medioParaComparar.Contains("metrobus"))
            {
                var diasViaje = CalcularDiasViaje(dto.FechaSalida, dto.FechaRegreso);
                var viajesPorDia = 4;
                var costoPorViaje = 10;
                var totalViajes = diasViaje * viajesPorDia * dto.NumeroPersonas;
                costo = totalViajes * costoPorViaje;

                detalles.Add(new ConceptoItemDto
                {
                    Precio = costo,
                    Descripcion = $"{dto.MedioTraslado} ({diasViaje} días, {totalViajes} viajes de ${costoPorViaje})"
                });
            }
            // Cálculo por defecto
            else
            {
                var diasViaje = CalcularDiasViaje(dto.FechaSalida, dto.FechaRegreso);
                costo = dto.NumeroPersonas * 500 * diasViaje;

                detalles.Add(new ConceptoItemDto
                {
                    Precio = costo,
                    Descripcion = $"Transporte {dto.Origen}-{dto.Destino} ({dto.NumeroPersonas} personas, {diasViaje} días)"
                });

                resultado.Alertas.Add($"Medio de transporte '{dto.MedioTraslado}' no reconocido. Se aplicó cálculo genérico.");
            }

            resultado.TotalTransporte = costo;
            resultado.DetalleTransporte = detalles;
            _logger.LogInformation("Transporte calculado: ${Costo} para {Estado}", costo, estadoDestino);

            await Task.CompletedTask;
        }

        // Determina si el medio de traslado es vehículo propio
        private bool EsVehiculo(string medioTraslado)
        {
            if (string.IsNullOrWhiteSpace(medioTraslado))
                return false;

            try
            {
                string medioDecodificado = WebUtility.HtmlDecode(medioTraslado);

                string medioNormalizado = medioDecodificado
                    .ToLower()
                    .Replace("vehículo", "vehiculo")
                    .Replace("ú", "u").Replace("é", "e").Replace("í", "i")
                    .Replace("ó", "o").Replace("á", "a").Replace("ñ", "n")
                    .Replace("'", "").Replace("-", " ").Replace("  ", " ").Trim();

                string[] patronesVehiculo = {
                    "vehiculo utilitario",
                    "vehiculo personal",
                    "vehiculo",
                    "utilitario",
                    "automovil",
                    "vehiculo propio",
                    "vehiculo particular"
                };

                var palabras = medioNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var patron in patronesVehiculo)
                {
                    if (patron.Split(' ').Length == 1)
                    {
                        if (palabras.Contains(patron))
                        {
                            _logger.LogInformation("VEHÍCULO DETECTADO: '{Medio}' contiene palabra clave: '{Patron}'",
                                medioTraslado, patron);
                            return true;
                        }
                    }
                    else if (medioNormalizado.Contains(patron))
                    {
                        _logger.LogInformation("VEHÍCULO DETECTADO: '{Medio}' coincide con patrón: '{Patron}'",
                            medioTraslado, patron);
                        return true;
                    }
                }

                _logger.LogInformation("NO ES VEHÍCULO: '{Medio}' (normalizado: '{Normalizado}')",
                    medioTraslado, medioNormalizado);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al determinar si es vehículo: {Medio}", medioTraslado);
                return false;
            }
        }

        // Calcula número de vehículos necesarios según capacidad
        private int CalcularVehiculosNecesarios(int numeroPersonas)
        {
            const int capacidadPorVehiculo = 5;
            if (numeroPersonas <= 0) return 1;
            return (int)Math.Ceiling((double)numeroPersonas / capacidadPorVehiculo);
        }

        // Calcula costo de gasolina para vehículo propio
        private async Task CalcularGasolinaAutomatico(CalcularCotizacionDto dto, ResultadoCotizacionDto resultado)
        {
            bool esVehiculoPropio = EsVehiculo(dto.MedioTraslado);

            _logger.LogInformation("CALCULAR GASOLINA - Medio: '{Medio}', Es vehículo: {EsVehiculo}",
                dto.MedioTraslado, esVehiculoPropio);

            if (!esVehiculoPropio)
            {
                resultado.TotalGasolina = 0;
                resultado.DetalleGasolina.Clear();
                _logger.LogInformation("No es vehículo, gasolina = 0");
                await Task.CompletedTask;
                return;
            }

            var distancia = resultado.DistanciaCalculada ?? 0;
            var costoPorKm = 1.92m;
            var distanciaTotal = distancia * 2;
            var vehiculosNecesarios = CalcularVehiculosNecesarios(dto.NumeroPersonas);
            var costoPorVehiculo = distanciaTotal * costoPorKm;
            var costo = costoPorVehiculo * vehiculosNecesarios;

            resultado.TotalGasolina = costo;
            resultado.DetalleGasolina.Clear();

            if (vehiculosNecesarios == 1)
            {
                resultado.DetalleGasolina.Add(new ConceptoItemDto
                {
                    Precio = costoPorVehiculo,
                    Descripcion = $"Gasolina ({distanciaTotal:F0}km ida y vuelta a ${costoPorKm}/km)"
                });
            }
            else
            {
                resultado.DetalleGasolina.Add(new ConceptoItemDto
                {
                    Precio = costoPorVehiculo,
                    Descripcion = $"Gasolina por vehículo ({distanciaTotal:F0}km)"
                });

                resultado.DetalleGasolina.Add(new ConceptoItemDto
                {
                    Precio = costoPorVehiculo * (vehiculosNecesarios - 1),
                    Descripcion = $"Gasolina para {vehiculosNecesarios - 1} vehículos adicionales"
                });
            }

            _logger.LogInformation("Gasolina calculada: ${Costo} para {Vehiculos} vehículos", costo, vehiculosNecesarios);
            await Task.CompletedTask;
        }

        // Calcula costo de Uber/Taxi
        private async Task CalcularUberTaxiAutomatico(CalcularCotizacionDto dto, ResultadoCotizacionDto resultado)
        {
            // Si no requiere taxi a domicilio, no calculamos nada
            if (!dto.RequiereTaxiDomicilio)
            {
                resultado.TotalUberTaxi = 0;
                resultado.DetalleUberTaxi.Clear();
                await Task.CompletedTask;
                return;
            }

            var costo = 0m;
            var detalles = new List<ConceptoItemDto>();

            // Taxi a domicilio (origen-destino específico)
            var costoTaxi = 200 * dto.NumeroPersonas;
            costo += costoTaxi;
            detalles.Add(new ConceptoItemDto
            {
                Precio = costoTaxi,
                Descripcion = $"Taxi a domicilio ({dto.NumeroPersonas} personas)"
            });

            resultado.TotalUberTaxi = costo;
            resultado.DetalleUberTaxi = detalles;

            await Task.CompletedTask;
        }

        // Calcula costo de casetas para vehículo propio
        private async Task CalcularCasetasAutomatico(CalcularCotizacionDto dto, ResultadoCotizacionDto resultado)
        {
            bool esVehiculoPropio = EsVehiculo(dto.MedioTraslado);

            _logger.LogInformation("CALCULAR CASETAS - Medio: '{Medio}', Es vehículo: {EsVehiculo}",
                dto.MedioTraslado, esVehiculoPropio);

            if (!esVehiculoPropio)
            {
                resultado.TotalCasetas = 0;
                resultado.DetalleCasetas.Clear();
                _logger.LogInformation("No es vehículo, casetas = 0");
                await Task.CompletedTask;
                return;
            }

            var distancia = resultado.DistanciaCalculada ?? 0;
            var numeroCasetas = Math.Max(1, (int)(distancia / 50));
            var costoPorCaseta = 80m;
            var costoPorVehiculo = numeroCasetas * costoPorCaseta * 2;
            var vehiculosNecesarios = CalcularVehiculosNecesarios(dto.NumeroPersonas);
            var costo = costoPorVehiculo * vehiculosNecesarios;

            resultado.TotalCasetas = costo;
            resultado.DetalleCasetas.Clear();

            resultado.DetalleCasetas.Add(new ConceptoItemDto
            {
                Precio = costoPorVehiculo,
                Descripcion = $"Casetas ({numeroCasetas} casetas de ${costoPorCaseta} c/u, ida y vuelta)"
            });

            if (vehiculosNecesarios > 1)
            {
                resultado.DetalleCasetas.Add(new ConceptoItemDto
                {
                    Precio = costoPorVehiculo * (vehiculosNecesarios - 1),
                    Descripcion = $"Casetas para {vehiculosNecesarios - 1} vehículos adicionales"
                });
            }

            _logger.LogInformation("Casetas calculadas: ${Costo} para {Vehiculos} vehículos", costo, vehiculosNecesarios);
            await Task.CompletedTask;
        }

        // Calcula costo de hospedaje
        private async Task CalcularHospedajeAutomatico(CalcularCotizacionDto dto, ResultadoCotizacionDto resultado, string zonaHospedaje)
        {
            if (!dto.RequiereHospedaje || dto.NochesHospedaje <= 0)
            {
                resultado.TotalHospedaje = 0;
                resultado.DetalleHospedaje.Clear();
                await Task.CompletedTask;
                return;
            }

            decimal tarifaPorNoche = zonaHospedaje switch
            {
                "ECONOMICA" => 600,
                "MEDIA" => 800,
                "GRANDE" => 1000,
                "TURISTICA" => 1200,
                "PEQUENA" => 500,
                _ => 700
            };

            var personasPorHabitacion = 2;
            var habitacionesNecesarias = (int)Math.Ceiling((double)dto.NumeroPersonas / personasPorHabitacion);
            var costo = habitacionesNecesarias * dto.NochesHospedaje * tarifaPorNoche;

            resultado.TotalHospedaje = costo;
            resultado.DetalleHospedaje.Clear();

            resultado.DetalleHospedaje.Add(new ConceptoItemDto
            {
                Precio = tarifaPorNoche * dto.NochesHospedaje,
                Descripcion = $"Hospedaje ({dto.NochesHospedaje} noches × ${tarifaPorNoche}/noche)"
            });

            if (habitacionesNecesarias > 1)
            {
                resultado.DetalleHospedaje.Add(new ConceptoItemDto
                {
                    Precio = tarifaPorNoche * dto.NochesHospedaje * (habitacionesNecesarias - 1),
                    Descripcion = $"Hospedaje para {habitacionesNecesarias - 1} habitaciones adicionales"
                });
            }

            await Task.CompletedTask;
        }

        // Calcula costo de alimentos con lógica de tiempos
        private async Task CalcularAlimentosAutomatico(CalcularCotizacionDto dto, ResultadoCotizacionDto resultado, string zonaAlimentos)
        {
            var tarifasPorZona = new Dictionary<string, (decimal desayuno, decimal comida, decimal cena)>
            {
                { "ECONOMICA", (80, 120, 100) },
                { "MEDIA", (100, 150, 120) },
                { "ALTA", (150, 220, 180) },
                { "TURISTICA", (200, 300, 250) }
            };

            if (!tarifasPorZona.TryGetValue(zonaAlimentos, out var tarifas))
            {
                tarifas = (100, 150, 120);
            }

            var diasViaje = CalcularDiasViaje(dto.FechaSalida, dto.FechaRegreso);

            _logger.LogInformation("Cálculo de alimentos: Días de viaje = {DiasViaje}", diasViaje);

            int numeroDesayunos = 0;
            int numeroComidas = 0;
            int numeroCenas = 0;

            if (!dto.HoraSalida.HasValue || !dto.HoraRegreso.HasValue)
            {
                _logger.LogInformation("Horas no especificadas, usando cálculo por defecto");
                resultado.Alertas.Add("Horas no especificadas. Cálculo de alimentos basado en días completos.");

                numeroDesayunos = diasViaje;
                numeroComidas = diasViaje;
                numeroCenas = Math.Max(0, diasViaje - 1);
            }
            else
            {
                var horaSalida = dto.HoraSalida.Value;
                var horaRegreso = dto.HoraRegreso.Value;

                _logger.LogInformation("Horas especificadas: Salida={HoraSalida}, Regreso={HoraRegreso}",
                    horaSalida.ToString(@"hh\:mm"), horaRegreso.ToString(@"hh\:mm"));

                if (horaSalida.Hour < 8)
                {
                    numeroDesayunos++;
                }

                if (horaSalida.Hour < 15)
                {
                    numeroComidas++;
                }

                numeroCenas++;

                if (diasViaje > 2)
                {
                    for (int i = 1; i < diasViaje - 1; i++)
                    {
                        numeroDesayunos++;
                        numeroComidas++;
                        numeroCenas++;
                    }
                }

                numeroDesayunos++;

                if (horaRegreso.Hour > 15)
                {
                    numeroComidas++;
                }

                if (horaRegreso.Hour >= 20)
                {
                    numeroCenas++;
                }

                _logger.LogInformation("Cálculo basado en horas: Desayunos={Desayunos}, Comidas={Comidas}, Cenas={Cenas}",
                    numeroDesayunos, numeroComidas, numeroCenas);
            }

            var costoTotalPorPersona =
                (numeroDesayunos * tarifas.desayuno) +
                (numeroComidas * tarifas.comida) +
                (numeroCenas * tarifas.cena);

            var costoTotal = costoTotalPorPersona * dto.NumeroPersonas;

            resultado.DetalleAlimentos.Clear();

            for (int i = 0; i < dto.NumeroPersonas; i++)
            {
                resultado.DetalleAlimentos.Add(new ConceptoItemDto
                {
                    Precio = costoTotalPorPersona,
                    Descripcion = $"Alimentos Persona {i + 1} ({numeroDesayunos}Desayunos/{numeroComidas}Comidas/{numeroCenas}Cenas)"
                });
            }

            resultado.TotalAlimentos = costoTotal;

            resultado.Alertas.Add($"Alimentos calculados: {numeroDesayunos} desayunos, {numeroComidas} comidas, {numeroCenas} cenas por persona");

            _logger.LogInformation("Costo alimentos por persona: ${CostoPersona}", costoTotalPorPersona);
            _logger.LogInformation("Número de personas: {Personas}", dto.NumeroPersonas);
            _logger.LogInformation("Total alimentos: ${Total}", costoTotal);

            await Task.CompletedTask;
        }

        // Aplica reglas de negocio como descuentos y validaciones
        private void AplicarReglasNegocio(CalcularCotizacionDto dto, ResultadoCotizacionDto resultado)
        {
            var medioTraslado = WebUtility.HtmlDecode(dto.MedioTraslado ?? "").ToLower();
            var esVehiculoPropio = EsVehiculo(medioTraslado);

           
            // Advertencias sobre porcentajes
            if (resultado.TotalGeneral > 0)
            {
                var porcentajeHospedaje = resultado.TotalHospedaje / resultado.TotalGeneral * 100;
                if (porcentajeHospedaje > 50)
                {
                    resultado.Alertas.Add($"El hospedaje representa el {porcentajeHospedaje:F1}% del total. Considerar alternativas más económicas.");
                }

                var porcentajeAlimentos = resultado.TotalAlimentos / resultado.TotalGeneral * 100;
                if (porcentajeAlimentos > 40)
                {
                    resultado.Alertas.Add($"Los alimentos representan el {porcentajeAlimentos:F1}% del total. Revisar tarifas.");
                }
            }

            if (esVehiculoPropio && dto.NumeroPersonas > 5)
            {
                resultado.Alertas.Add("Grupo grande para un vehículo (máx 5 personas). Considerar vehículos adicionales.");
            }

            if (dto.Origen?.ToUpper().Contains("PUEBLA") == true && (resultado.DistanciaCalculada ?? 0) < 200)
            {
                resultado.Alertas.Add("Viaje corto desde Puebla: Considerar transporte terrestre como opción más económica.");
            }
        }

        // Obtiene configuración de estados para la UI
        public async Task<Dictionary<string, object>> ObtenerConfiguracionEstadosAsync()
        {
            var config = new Dictionary<string, object>
            {
                ["estados"] = _estadosMexico.Select(e => new { e.Nombre, Municipios = e.Municipios.Take(50).ToList() }).ToList(),
                ["totalEstados"] = _estadosMexico.Count,
                ["zonasAlimentos"] = _cotizacionConfig.ZonasAlimentos,
                ["zonasHospedaje"] = _cotizacionConfig.ZonasHospedaje,
                ["tarifasTransporte"] = new
                {
                    AUTOBUS = _cotizacionConfig.TarifasTransporte?.AUTOBUS?.Take(10).ToDictionary(k => k.Key, v => v.Value) ?? new Dictionary<string, decimal>(),
                    AUTO_PROPIO = _cotizacionConfig.TarifasTransporte?.AUTO_PROPIO?.Take(10).ToDictionary(k => k.Key, v => v.Value) ?? new Dictionary<string, decimal>(),
                    AVION = _cotizacionConfig.TarifasTransporte?.AVION ?? new Dictionary<string, decimal>()
                }
            };

            return await Task.FromResult(config);
        }

        // Obtiene lista de estados con información básica
        public async Task<List<EstadoInfo>> ObtenerEstadosAsync()
        {
            return await Task.FromResult(_estadosMexico.Select(e => new EstadoInfo
            {
                Nombre = e.Nombre,
                TotalMunicipios = e.Municipios.Count
            }).ToList());
        }

        // Obtiene municipios por estado específico
        public async Task<List<string>> ObtenerMunicipiosPorEstadoAsync(string estado)
        {
            var estadoEncontrado = _estadosMexico.FirstOrDefault(e =>
                e.Nombre.Equals(estado, StringComparison.OrdinalIgnoreCase));

            return await Task.FromResult(estadoEncontrado?.Municipios ?? new List<string>());
        }

        // Clases internas para deserialización de respuesta de Google Maps
        public class GoogleMapsDistanceResponse
        {
            [JsonPropertyName("destination_addresses")]
            public List<string> DestinationAddresses { get; set; } = new List<string>();

            [JsonPropertyName("origin_addresses")]
            public List<string> OriginAddresses { get; set; } = new List<string>();

            [JsonPropertyName("rows")]
            public List<GoogleMapsRow> Rows { get; set; } = new List<GoogleMapsRow>();

            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;
        }

        public class GoogleMapsRow
        {
            [JsonPropertyName("elements")]
            public List<GoogleMapsElement> Elements { get; set; } = new List<GoogleMapsElement>();
        }

        public class GoogleMapsElement
        {
            [JsonPropertyName("distance")]
            public GoogleMapsDistance Distance { get; set; } = new GoogleMapsDistance();

            [JsonPropertyName("duration")]
            public GoogleMapsDuration Duration { get; set; } = new GoogleMapsDuration();

            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;
        }

        public class GoogleMapsDistance
        {
            [JsonPropertyName("text")]
            public string Text { get; set; } = string.Empty;

            [JsonPropertyName("value")]
            public int Value { get; set; }
        }

        public class GoogleMapsDuration
        {
            [JsonPropertyName("text")]
            public string Text { get; set; } = string.Empty;

            [JsonPropertyName("value")]
            public int Value { get; set; }
        }
    }

    // Clases para representar datos de estados de México
    public class EstadosMexicoData
    {
        [JsonPropertyName("estados")]
        public List<EstadoMexico> Estados { get; set; } = new List<EstadoMexico>();
    }

    public class EstadoMexico
    {
        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("municipios")]
        public List<string> Municipios { get; set; } = new List<string>();
    }
    

    public class EstadoInfo
    {
        public string Nombre { get; set; } = string.Empty;
        public int TotalMunicipios { get; set; }
    }

    // Clases para municipios con coordenadas
    public class RootMunicipios
    {
        [JsonPropertyName("estados")]
        public List<EstadoMunicipios> Estados { get; set; } = new List<EstadoMunicipios>();
    }

    public class EstadoMunicipios
    {
        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("municipios")]
        public List<MunicipioDetalle> Municipios { get; set; } = new List<MunicipioDetalle>();
    }

    public class MunicipioDetalle
    {
        [JsonPropertyName("clave_entidad")]
        public string ClaveEntidad { get; set; } = string.Empty;

        [JsonPropertyName("clave_municipio")]
        public string ClaveMunicipio { get; set; } = string.Empty;

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("latitud")]
        public double Latitud { get; set; }

        [JsonPropertyName("longitud")]
        public double Longitud { get; set; }

        [JsonPropertyName("poblacion")]
        public int Poblacion { get; set; }
    }
}

// Clases de configuración para tarifas y parámetros del sistema
public class CotizacionConfig
{
    public string GoogleMapsApiKey { get; set; } = string.Empty;
    public TarifasConfig Tarifas { get; set; } = new TarifasConfig();
    public Dictionary<string, ZonaAlimentosConfig> ZonasAlimentos { get; set; } = new Dictionary<string, ZonaAlimentosConfig>();
    public Dictionary<string, ZonaHospedajeConfig> ZonasHospedaje { get; set; } = new Dictionary<string, ZonaHospedajeConfig>();
    public TarifasTransporteConfig TarifasTransporte { get; set; } = new TarifasTransporteConfig();
    public CostosVueloConfig CostosVuelo { get; set; } = new CostosVueloConfig();
    public Dictionary<string, decimal> DistanciasEstimadas { get; set; } = new Dictionary<string, decimal>();
    public Dictionary<string, decimal> CasetasPorRuta { get; set; } = new Dictionary<string, decimal>();
}

public class TarifasConfig
{
    public decimal TransportePorKm { get; set; }
    public decimal GasolinaPorLitro { get; set; }
    public decimal RendimientoAutoKmPorLitro { get; set; }
    public decimal CostoCaseta { get; set; }
    public decimal CasetasPor100Km { get; set; }
    public decimal TaxiDomicilio { get; set; }
    public decimal TrasladoLocal { get; set; }
    public DescuentoGrupoGrandeConfig DescuentoGrupoGrande { get; set; } = new DescuentoGrupoGrandeConfig();
    public decimal RentaAutoPorDia { get; set; }
    public decimal CostoVueloCorto { get; set; }
    public decimal CostoVueloMediano { get; set; }
    public decimal CostoVueloLargo { get; set; }
    public decimal CostoPorKmGasolina { get; set; }
}

public class DescuentoGrupoGrandeConfig
{
    public int MinPersonas { get; set; }
    public decimal Porcentaje { get; set; }
}

public class ZonaAlimentosConfig
{
    public decimal Desayuno { get; set; }
    public decimal Comida { get; set; }
    public decimal Cena { get; set; }
    public decimal TotalDiario { get; set; }
}

public class ZonaHospedajeConfig
{
    public decimal Min { get; set; }
    public decimal Promedio { get; set; }
    public decimal Max { get; set; }
}

public class TarifasTransporteConfig
{
    public Dictionary<string, decimal> AUTOBUS { get; set; } = new Dictionary<string, decimal>();
    public Dictionary<string, decimal> AUTO_PROPIO { get; set; } = new Dictionary<string, decimal>();
    public Dictionary<string, decimal> AVION { get; set; } = new Dictionary<string, decimal>();
}

public class CostosVueloConfig
{
    public decimal CortoAlcance { get; set; }
    public decimal MedianoAlcance { get; set; }
    public decimal LargoAlcance { get; set; }
    public decimal Internacional { get; set; }
}