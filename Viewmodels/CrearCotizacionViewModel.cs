using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using SVV.DTOs.Cotizacion;

namespace SVV.ViewModels
{
    public class ConceptoItemViewModel
    {
        [Range(0, 999999.99, ErrorMessage = "El precio debe ser mayor o igual a 0")]
        [Display(Name = "Precio")]
        public decimal Precio { get; set; }

        [Display(Name = "Descripción (opcional)")]
        public string? Descripcion { get; set; }
    }

    public class CrearCotizacionViewModel
    {
        public int Id { get; set; }
        public int SolicitudViajeId { get; set; }
        public string CodigoSolicitud { get; set; } = string.Empty;
        public string EmpleadoNombre { get; set; } = string.Empty;
        public string Destino { get; set; } = string.Empty;
        public string Proyecto { get; set; } = string.Empty;

        [Display(Name = "Observaciones")]
        public string? Observaciones { get; set; }

        [Display(Name = "Usar cálculo automático")]
        public bool UsarCalculoAutomatico { get; set; } = false;

        //  Datos para cálculo automático
        [Display(Name = "Ubicación Base")]
        public string UbicacionBase { get; set; } = "Puebla";

        [Display(Name = "Fecha de Salida")]
        [DataType(DataType.Date)]
        public DateOnly? FechaSalida { get; set; }

        [Display(Name = "Fecha de Regreso")]
        [DataType(DataType.Date)]
        public DateOnly? FechaRegreso { get; set; }

        [Display(Name = "Hora de Salida")]
        [DataType(DataType.Time)]
        public TimeSpan? HoraSalida { get; set; }

        [Display(Name = "Hora de Regreso")]
        [DataType(DataType.Time)]
        public TimeSpan? HoraRegreso { get; set; }

        [Display(Name = "Número de Personas")]
        [Range(1, 20, ErrorMessage = "Debe haber al menos 1 persona")]
        public int NumeroPersonas { get; set; } = 1;

        [Display(Name = "Requiere Hospedaje")]
        public bool RequiereHospedaje { get; set; } = false;

        [Display(Name = "Noches de Hospedaje")]
        [Range(0, 30, ErrorMessage = "Máximo 30 noches")]
        public int NochesHospedaje { get; set; } = 0;

        [Display(Name = "Medio de Traslado")]
        public string MedioTraslado { get; set; } = "Avión";

        [Display(Name = "Requiere Taxi a Domicilio")]
        public bool RequiereTaxiDomicilio { get; set; } = false;

        [Display(Name = "Dirección Origen Taxi")]
        public string? DireccionTaxiOrigen { get; set; }

        [Display(Name = "Dirección Destino Taxi")]
        public string? DireccionTaxiDestino { get; set; }

        // Resultados del cálculo automático
        public decimal? DistanciaCalculada { get; set; }
        public string Origen { get; set; } = string.Empty;
        public string? TiempoEstimado { get; set; }
        public List<string>? AlertasCalculo { get; set; }
        public List<string>? ErroresCalculo { get; set; }
        public string? MensajeCalculo { get; set; }
        public bool CalculoRealizado { get; set; } = false;

        // Desglose del cálculo automático
        public DesgloseCalculoViewModel? DesgloseCalculo { get; set; }

        // CANTIDADES
        [Display(Name = "Número de precios para Transporte")]
        [Range(1, 20, ErrorMessage = "La cantidad debe ser entre 1 y 20")]
        public int TransporteCantidad { get; set; } = 1;

        [Display(Name = "Número de precios para Gasolina")]
        [Range(1, 20, ErrorMessage = "La cantidad debe ser entre 1 y 20")]
        public int GasolinaCantidad { get; set; } = 1;

        [Display(Name = "Número de precios para UBER/TAXI")]
        [Range(1, 20, ErrorMessage = "La cantidad debe ser entre 1 y 20")]
        public int UberTaxiCantidad { get; set; } = 1;

        [Display(Name = "Número de precios para Casetas")]
        [Range(1, 20, ErrorMessage = "La cantidad debe ser entre 1 y 20")]
        public int CasetasCantidad { get; set; } = 1;

        [Display(Name = "Número de precios para Hospedaje")]
        [Range(1, 20, ErrorMessage = "La cantidad debe ser entre 1 y 20")]
        public int HospedajeCantidad { get; set; } = 1;

        [Display(Name = "Número de precios para Alimentos")]
        [Range(1, 20, ErrorMessage = "La cantidad debe ser entre 1 y 20")]
        public int AlimentosCantidad { get; set; } = 1;

        // LISTAS DE PRECIOS INDIVIDUALES
        public List<ConceptoItemViewModel> TransportePrecios { get; set; } = new();
        public List<ConceptoItemViewModel> GasolinaPrecios { get; set; } = new();
        public List<ConceptoItemViewModel> UberTaxiPrecios { get; set; } = new();
        public List<ConceptoItemViewModel> CasetasPrecios { get; set; } = new();
        public List<ConceptoItemViewModel> HospedajePrecios { get; set; } = new();
        public List<ConceptoItemViewModel> AlimentosPrecios { get; set; } = new();

        // TOTALES CALCULADOS
        public decimal TotalTransporte { get; set; }
        public decimal TotalGasolina { get; set; }
        public decimal TotalUberTaxi { get; set; }
        public decimal TotalCasetas { get; set; }
        public decimal TotalHospedaje { get; set; }
        public decimal TotalAlimentos { get; set; }
        public decimal Total { get; set; }

        // Constructor
        public CrearCotizacionViewModel()
        {
            InicializarListas();
            DesgloseCalculo = new DesgloseCalculoViewModel();
            AlertasCalculo = new List<string>();
            ErroresCalculo = new List<string>();
        }

        // Método para inicializar las listas
        public void InicializarListas()
        {
            // Inicializar cada lista con una fila por defecto si está vacía
            if (!TransportePrecios.Any())
                InicializarLista(TransportePrecios, TransporteCantidad);

            if (!GasolinaPrecios.Any())
                InicializarLista(GasolinaPrecios, GasolinaCantidad);

            if (!UberTaxiPrecios.Any())
                InicializarLista(UberTaxiPrecios, UberTaxiCantidad);

            if (!CasetasPrecios.Any())
                InicializarLista(CasetasPrecios, CasetasCantidad);

            if (!HospedajePrecios.Any())
                InicializarLista(HospedajePrecios, HospedajeCantidad);

            if (!AlimentosPrecios.Any())
                InicializarLista(AlimentosPrecios, AlimentosCantidad);
        }

        private void InicializarLista(List<ConceptoItemViewModel> lista, int cantidad)
        {
            if (lista.Count < cantidad)
            {
                for (int i = lista.Count; i < cantidad; i++)
                {
                    lista.Add(new ConceptoItemViewModel());
                }
            }
            else if (lista.Count > cantidad)
            {
                lista.RemoveRange(cantidad, lista.Count - cantidad);
            }
        }

        // Método para calcular totales basado en las listas
        public void CalcularTotalesDesdeListas()
        {
            TotalTransporte = TransportePrecios?.Sum(p => p.Precio) ?? 0;
            TotalGasolina = GasolinaPrecios?.Sum(p => p.Precio) ?? 0;
            TotalUberTaxi = UberTaxiPrecios?.Sum(p => p.Precio) ?? 0;
            TotalCasetas = CasetasPrecios?.Sum(p => p.Precio) ?? 0;
            TotalHospedaje = HospedajePrecios?.Sum(p => p.Precio) ?? 0;
            TotalAlimentos = AlimentosPrecios?.Sum(p => p.Precio) ?? 0;

            Total = TotalTransporte + TotalGasolina + TotalUberTaxi +
                   TotalCasetas + TotalHospedaje + TotalAlimentos;
        }

        //  NUEVO: Método para aplicar el cálculo automático desde ResultadoCotizacionDto
        public void AplicarResultadoCalculo(ResultadoCotizacionDto resultado)
        {
            if (resultado == null) return;

            CalculoRealizado = true;
            DistanciaCalculada = resultado.DistanciaCalculada;
            MensajeCalculo = resultado.Mensaje;
            AlertasCalculo = resultado.Alertas ?? new List<string>();
            ErroresCalculo = resultado.Errores ?? new List<string>();

            // Actualizar desglose
            DesgloseCalculo = new DesgloseCalculoViewModel
            {
                Transporte = resultado.TotalTransporte,
                Gasolina = resultado.TotalGasolina,
                UberTaxi = resultado.TotalUberTaxi,
                Casetas = resultado.TotalCasetas,
                Hospedaje = resultado.TotalHospedaje,
                Alimentos = resultado.TotalAlimentos
            };

            // Determinar si es vehículo propio
            var medioTrasladoLower = MedioTraslado?.ToLower() ?? "";
            var esVehiculoPropio = medioTrasladoLower.Contains("vehículo") ||
                                  medioTrasladoLower.Contains("vehiculo") ||
                                  medioTrasladoLower.Contains("auto") ||
                                  medioTrasladoLower.Contains("carro") ||
                                  medioTrasladoLower.Contains("personal") ||
                                  medioTrasladoLower.Contains("utilitario") ||
                                  medioTrasladoLower.Contains("particular");

            // Aplicar precios basados en los detalles
            if (resultado.DetalleTransporte.Any() || resultado.DetalleGasolina.Any() ||
                resultado.DetalleUberTaxi.Any() || resultado.DetalleCasetas.Any() ||
                resultado.DetalleHospedaje.Any() || resultado.DetalleAlimentos.Any())
            {
                // Usar detalles específicos del servicio
                AplicarPreciosDesdeDetalles(resultado, esVehiculoPropio);
            }
            else
            {
                // Usar totales generales
                AplicarPreciosDesdeTotales(resultado, esVehiculoPropio);
            }

            // Recalcular totales
            CalcularTotalesDesdeListas();
        }

        //  NUEVO: Aplicar precios desde detalles específicos
        private void AplicarPreciosDesdeDetalles(ResultadoCotizacionDto resultado, bool esVehiculoPropio)
        {
            // Limpiar listas existentes
            TransportePrecios.Clear();
            GasolinaPrecios.Clear();
            UberTaxiPrecios.Clear();
            CasetasPrecios.Clear();
            HospedajePrecios.Clear();
            AlimentosPrecios.Clear();

            // TRANSPORTE
            if (resultado.DetalleTransporte.Any())
            {
                TransporteCantidad = resultado.DetalleTransporte.Count;
                TransportePrecios = resultado.DetalleTransporte.Select(d => new ConceptoItemViewModel
                {
                    Precio = d.Precio,
                    Descripcion = d.Descripcion
                }).ToList();
            }

            // GASOLINA (solo para vehículos)
            if (esVehiculoPropio && resultado.DetalleGasolina.Any())
            {
                GasolinaCantidad = resultado.DetalleGasolina.Count;
                GasolinaPrecios = resultado.DetalleGasolina.Select(d => new ConceptoItemViewModel
                {
                    Precio = d.Precio,
                    Descripcion = d.Descripcion
                }).ToList();
            }

            // UBER/TAXI
            if (resultado.DetalleUberTaxi.Any())
            {
                UberTaxiCantidad = resultado.DetalleUberTaxi.Count;
                UberTaxiPrecios = resultado.DetalleUberTaxi.Select(d => new ConceptoItemViewModel
                {
                    Precio = d.Precio,
                    Descripcion = d.Descripcion
                }).ToList();
            }

            // CASETAS (solo para vehículos)
            if (esVehiculoPropio && resultado.DetalleCasetas.Any())
            {
                CasetasCantidad = resultado.DetalleCasetas.Count;
                CasetasPrecios = resultado.DetalleCasetas.Select(d => new ConceptoItemViewModel
                {
                    Precio = d.Precio,
                    Descripcion = d.Descripcion
                }).ToList();
            }

            // HOSPEDAJE
            if (resultado.DetalleHospedaje.Any())
            {
                HospedajeCantidad = resultado.DetalleHospedaje.Count;
                HospedajePrecios = resultado.DetalleHospedaje.Select(d => new ConceptoItemViewModel
                {
                    Precio = d.Precio,
                    Descripcion = d.Descripcion
                }).ToList();
            }

            // ALIMENTOS
            if (resultado.DetalleAlimentos.Any())
            {
                AlimentosCantidad = resultado.DetalleAlimentos.Count;
                AlimentosPrecios = resultado.DetalleAlimentos.Select(d => new ConceptoItemViewModel
                {
                    Precio = d.Precio,
                    Descripcion = d.Descripcion
                }).ToList();
            }
        }

        //  NUEVO: Aplicar precios desde totales generales
        private void AplicarPreciosDesdeTotales(ResultadoCotizacionDto resultado, bool esVehiculoPropio)
        {
            // Limpiar listas existentes
            TransportePrecios.Clear();
            GasolinaPrecios.Clear();
            UberTaxiPrecios.Clear();
            CasetasPrecios.Clear();
            HospedajePrecios.Clear();
            AlimentosPrecios.Clear();

            // TRANSPORTE
            if (resultado.TotalTransporte > 0)
            {
                if (esVehiculoPropio)
                {
                    // VEHÍCULO: Un solo precio
                    TransporteCantidad = 1;
                    TransportePrecios.Add(new ConceptoItemViewModel
                    {
                        Precio = resultado.TotalTransporte,
                        Descripcion = $"Transporte en {MedioTraslado}"
                    });
                }
                else
                {
                    // NO vehículo: Precio por persona
                    TransporteCantidad = NumeroPersonas;
                    for (int i = 0; i < NumeroPersonas; i++)
                    {
                        TransportePrecios.Add(new ConceptoItemViewModel
                        {
                            Precio = resultado.TotalTransporte / NumeroPersonas,
                            Descripcion = $"Transporte persona {i + 1} - {MedioTraslado}"
                        });
                    }
                }
            }

            // GASOLINA (solo para vehículos)
            if (esVehiculoPropio && resultado.TotalGasolina > 0)
            {
                GasolinaCantidad = 1;
                GasolinaPrecios.Add(new ConceptoItemViewModel
                {
                    Precio = resultado.TotalGasolina,
                    Descripcion = "Gasolina para el viaje"
                });
            }

            // UBER/TAXI (siempre por persona)
            if (resultado.TotalUberTaxi > 0)
            {
                UberTaxiCantidad = NumeroPersonas;
                for (int i = 0; i < NumeroPersonas; i++)
                {
                    UberTaxiPrecios.Add(new ConceptoItemViewModel
                    {
                        Precio = resultado.TotalUberTaxi / NumeroPersonas,
                        Descripcion = $"UBER/TAXI persona {i + 1}"
                    });
                }
            }

            // CASETAS (solo para vehículos)
            if (esVehiculoPropio && resultado.TotalCasetas > 0)
            {
                CasetasCantidad = 1;
                CasetasPrecios.Add(new ConceptoItemViewModel
                {
                    Precio = resultado.TotalCasetas,
                    Descripcion = "Casetas de carretera"
                });
            }

            // HOSPEDAJE (siempre por persona)
            if (resultado.TotalHospedaje > 0)
            {
                HospedajeCantidad = NumeroPersonas;
                for (int i = 0; i < NumeroPersonas; i++)
                {
                    HospedajePrecios.Add(new ConceptoItemViewModel
                    {
                        Precio = resultado.TotalHospedaje / NumeroPersonas,
                        Descripcion = $"Hospedaje persona {i + 1} - {NochesHospedaje} noches"
                    });
                }
            }

            // ALIMENTOS (siempre por persona)
            if (resultado.TotalAlimentos > 0)
            {
                AlimentosCantidad = NumeroPersonas;
                for (int i = 0; i < NumeroPersonas; i++)
                {
                    AlimentosPrecios.Add(new ConceptoItemViewModel
                    {
                        Precio = resultado.TotalAlimentos / NumeroPersonas,
                        Descripcion = $"Alimentos persona {i + 1}"
                    });
                }
            }
        }

        //  ACTUALIZADO: Cargar desde entidad CotizacionesFinanzas
        public void CargarDesdeCotizacion(Models.CotizacionesFinanzas cotizacion)
        {
            if (cotizacion == null) return;

            Id = cotizacion.Id;
            SolicitudViajeId = cotizacion.SolicitudViajeId;
            Observaciones = cotizacion.Observaciones;

            // Cargar totales
            TotalTransporte = cotizacion.TransporteTotal;
            TotalGasolina = cotizacion.GasolinaTotal;
            TotalUberTaxi = cotizacion.UberTaxiTotal;
            TotalCasetas = cotizacion.CasetasTotal;
            TotalHospedaje = cotizacion.HospedajeTotal;
            TotalAlimentos = cotizacion.AlimentosTotal;
            Total = cotizacion.TotalAutorizado;

            // Cargar cantidades
            TransporteCantidad = (int)(cotizacion.TransporteCantidad ?? 1);
            GasolinaCantidad = (int)(cotizacion.GasolinaCantidad ?? 1);
            UberTaxiCantidad = (int)(cotizacion.UberTaxiCantidad ?? 1);
            CasetasCantidad = (int)(cotizacion.CasetasCantidad ?? 1);
            HospedajeCantidad = (int)(cotizacion.HospedajeCantidad ?? 1);
            AlimentosCantidad = (int)(cotizacion.AlimentosCantidad ?? 1);

            // Cargar datos desde solicitud
            if (cotizacion.SolicitudViaje != null)
            {
                var solicitud = cotizacion.SolicitudViaje;
                CargarDatosSolicitud(solicitud);
            }

            // Deserializar JSON de precios
            DeserializarPreciosDesdeJson(cotizacion);
        }

        //  NUEVO: Cargar datos de la solicitud
        private void CargarDatosSolicitud(Models.SolicitudesViaje solicitud)
        {
            CodigoSolicitud = solicitud.CodigoSolicitud;
            EmpleadoNombre = $"{solicitud.Empleado?.Nombre} {solicitud.Empleado?.Apellidos}";
            Destino = solicitud.Destino;
            Proyecto = solicitud.NombreProyecto;
            UbicacionBase = solicitud.Empleado?.UbicacionBase ?? "Puebla";
            FechaSalida = solicitud.FechaSalida;
            FechaRegreso = solicitud.FechaRegreso;
            HoraSalida = solicitud.HoraSalida.HasValue ?
                (TimeSpan?)solicitud.HoraSalida.Value.ToTimeSpan() : null;
            HoraRegreso = solicitud.HoraRegreso.HasValue ?
                (TimeSpan?)solicitud.HoraRegreso.Value.ToTimeSpan() : null;
            NumeroPersonas = solicitud.NumeroPersonas ?? 1;
            RequiereHospedaje = solicitud.RequiereHospedaje ?? false;
            NochesHospedaje = solicitud.NochesHospedaje ?? 0;
            MedioTraslado = solicitud.MedioTrasladoPrincipal ?? "Avión";
            RequiereTaxiDomicilio = solicitud.RequiereTaxiDomicilio ?? false;
            DireccionTaxiOrigen = solicitud.DireccionTaxiOrigen;
            DireccionTaxiDestino = solicitud.DireccionTaxiDestino;
        }

        //  NUEVO: Deserializar precios desde JSON
        private void DeserializarPreciosDesdeJson(Models.CotizacionesFinanzas cotizacion)
        {
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };

            try
            {
                if (!string.IsNullOrEmpty(cotizacion.TransportePreciosJson))
                {
                    var precios = System.Text.Json.JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(
                        cotizacion.TransportePreciosJson, jsonOptions);
                    if (precios != null) TransportePrecios = precios;
                }

                if (!string.IsNullOrEmpty(cotizacion.GasolinaPreciosJson))
                {
                    var precios = System.Text.Json.JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(
                        cotizacion.GasolinaPreciosJson, jsonOptions);
                    if (precios != null) GasolinaPrecios = precios;
                }

                if (!string.IsNullOrEmpty(cotizacion.UberTaxiPreciosJson))
                {
                    var precios = System.Text.Json.JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(
                        cotizacion.UberTaxiPreciosJson, jsonOptions);
                    if (precios != null) UberTaxiPrecios = precios;
                }

                if (!string.IsNullOrEmpty(cotizacion.CasetasPreciosJson))
                {
                    var precios = System.Text.Json.JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(
                        cotizacion.CasetasPreciosJson, jsonOptions);
                    if (precios != null) CasetasPrecios = precios;
                }

                if (!string.IsNullOrEmpty(cotizacion.HospedajePreciosJson))
                {
                    var precios = System.Text.Json.JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(
                        cotizacion.HospedajePreciosJson, jsonOptions);
                    if (precios != null) HospedajePrecios = precios;
                }

                if (!string.IsNullOrEmpty(cotizacion.AlimentosPreciosJson))
                {
                    var precios = System.Text.Json.JsonSerializer.Deserialize<List<ConceptoItemViewModel>>(
                        cotizacion.AlimentosPreciosJson, jsonOptions);
                    if (precios != null) AlimentosPrecios = precios;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Si hay error en JSON, inicializar listas vacías
                InicializarListas();
            }
        }

        // Validación
        public bool ValidarCantidades()
        {
            return TransporteCantidad == TransportePrecios.Count &&
                   GasolinaCantidad == GasolinaPrecios.Count &&
                   UberTaxiCantidad == UberTaxiPrecios.Count &&
                   CasetasCantidad == CasetasPrecios.Count &&
                   HospedajeCantidad == HospedajePrecios.Count &&
                   AlimentosCantidad == AlimentosPrecios.Count;
        }

        public List<string> GetErroresValidacion()
        {
            var errores = new List<string>();

            if (TransporteCantidad != TransportePrecios.Count)
                errores.Add("La cantidad de Transporte no coincide con el número de precios");

            if (GasolinaCantidad != GasolinaPrecios.Count)
                errores.Add("La cantidad de Gasolina no coincide con el número de precios");

            if (UberTaxiCantidad != UberTaxiPrecios.Count)
                errores.Add("La cantidad de UBER/TAXI no coincide con el número de precios");

            if (CasetasCantidad != CasetasPrecios.Count)
                errores.Add("La cantidad de Casetas no coincide con el número de precios");

            if (HospedajeCantidad != HospedajePrecios.Count)
                errores.Add("La cantidad de Hospedaje no coincide con el número de precios");

            if (AlimentosCantidad != AlimentosPrecios.Count)
                errores.Add("La cantidad de Alimentos no coincide con el número de precios");

            if (Total <= 0)
                errores.Add("Al menos un concepto debe tener precios mayores a $0.00");

            return errores;
        }
    }

    //  CLASE VIEWMODEL PARA DESGLOSE (para la vista)
    public class DesgloseCalculoViewModel
    {
        public decimal Transporte { get; set; }
        public decimal Gasolina { get; set; }
        public decimal UberTaxi { get; set; }
        public decimal Casetas { get; set; }
        public decimal Hospedaje { get; set; }
        public decimal Alimentos { get; set; }
        public decimal Total => Transporte + Gasolina + UberTaxi + Casetas + Hospedaje + Alimentos;
    }
}