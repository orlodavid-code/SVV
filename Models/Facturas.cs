using System;
using System.Collections.Generic;

namespace SVV.Models;

public partial class Facturas
{
    public int Id { get; set; }

    public int GastoRealId { get; set; }

    public string? ArchivoPdfUrl { get; set; }

    public string? ArchivoXmlUrl { get; set; }

    public string? Moneda { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual GastosReales GastoReal { get; set; } = null!;
}
