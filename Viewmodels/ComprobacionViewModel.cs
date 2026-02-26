// ViewModels/ComprobacionViewModel.cs
public class ComprobacionViewModel
{
    public int? ComprobacionId { get; set; }
    public int SolicitudId { get; set; }
    public string CodigoSolicitud { get; set; }
    public string EmpleadoNombre { get; set; }
    public string Destino { get; set; }
    public DateTime FechaSalida { get; set; }
    public DateTime FechaRegreso { get; set; }
    public decimal AnticipoAutorizado { get; set; }
    public decimal TotalComprobado { get; set; }
    public DateTime FechaLimiteComprobacion { get; set; }

    // Campos del informe
    public string DescripcionActividades { get; set; }
    public string ResultadosViaje { get; set; }

    // Estado
    public int EstatusComprobacion { get; set; }
    public string EstadoComprobacionNombre { get; set; } = "Pendiente";

    // Lista de gastos
    public List<GastoRealViewModel> Gastos { get; set; } = new List<GastoRealViewModel>();

    // Propiedades calculadas
    public string DisplayAnticipo => AnticipoAutorizado.ToString("C");
    public string DisplayTotalComprobado => TotalComprobado.ToString("C");
    public string DisplayFechaSalida => FechaSalida.ToString("dd/MM/yyyy");
    public string DisplayFechaRegreso => FechaRegreso.ToString("dd/MM/yyyy");
    public string DisplayFechaLimite => FechaLimiteComprobacion.ToString("dd/MM/yyyy");
}
public class GastoRealViewModel
{
    public int? Id { get; set; }
    public int SolicitudId { get; set; }
    public int CategoriaGastoId { get; set; }
    public string Concepto { get; set; }
    public DateTime FechaGasto { get; set; } // Cambiado a DateTime para compatibilidad
    public decimal Monto { get; set; }
    public string? Proveedor { get; set; }
    public string? Descripcion { get; set; }
    public string? MedioPago { get; set; }
    public string? LugarGasto { get; set; }

    // NUEVA PROPIEDAD: Estado del gasto
    public string EstadoGasto { get; set; } = "Pendiente";
    public string EstadoGastoCodigo { get; set; } = "PENDIENTE";

    // Archivos
    public IFormFile? ArchivoPDF { get; set; }
    public IFormFile? ArchivoXML { get; set; }

    // Para mostrar archivos existentes
    public string? FacturaPDF { get; set; }
    public string? FacturaXML { get; set; }

    // Propiedades calculadas (opcional, para facilitar uso en vistas)
    public string DisplayMonto => Monto.ToString("C");
    public string DisplayFecha => FechaGasto.ToString("dd/MM/yyyy");

    public string EstadoGastoBadgeClass => EstadoGastoCodigo switch
    {
        "APROBADO" => "bg-success",
        "RECHAZADO" => "bg-danger",
        "DEVUELTO_CORRECCION" => "bg-warning",
        _ => "bg-secondary"
    };
}

// Esta clase es temporal para manejar los JSON con Precio null
public class ConceptoItemJsonViewModel
{
    public decimal? Precio { get; set; }
    public string Descripcion { get; set; }
}