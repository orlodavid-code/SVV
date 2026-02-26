using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace SVV.Models;

public partial class SvvContext : DbContext
{
    public SvvContext()
    {
    }

    public SvvContext(DbContextOptions<SvvContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Anticipos> Anticipos { get; set; }

    public virtual DbSet<AuditoriaSistema> AuditoriaSistema { get; set; }

    public virtual DbSet<CategoriasGasto> CategoriasGasto { get; set; }

    public virtual DbSet<ComprobacionesViaje> ComprobacionesViaje { get; set; }

    public virtual DbSet<CotizacionesFinanzas> CotizacionesFinanzas { get; set; }

    public virtual DbSet<Empleados> Empleados { get; set; }

    public virtual DbSet<EstadosComprobacion> EstadosComprobacion { get; set; }

    public virtual DbSet<EstadosGasto> EstadosGastos { get; set; }

    public virtual DbSet<EstadosSolicitud> EstadosSolicitud { get; set; }

    public virtual DbSet<Facturas> Facturas { get; set; }

    public virtual DbSet<FlujoAprobaciones> FlujoAprobaciones { get; set; }

    public virtual DbSet<GastosReales> GastosReales { get; set; }

    public virtual DbSet<Notificaciones> Notificaciones { get; set; }

    public virtual DbSet<RolesSistema> RolesSistema { get; set; }

    public virtual DbSet<SolicitudesViaje> SolicitudesViajes { get; set; }

    public virtual DbSet<TiposViatico> TiposViatico { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=QVTKDEV0033\\SQLEXPRESS;Database=SVV;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Anticipos>(entity =>
        {
            entity.ToTable("anticipos");

            entity.HasIndex(e => e.CodigoAnticipo, "UQ_anticipos_codigo").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AutorizadoPorId).HasColumnName("autorizado_por_id");
            entity.Property(e => e.CodigoAnticipo)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("codigo_anticipo");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("estado");
            entity.Property(e => e.FechaAutorizacion).HasColumnName("fecha_autorizacion");
            entity.Property(e => e.FechaSolicitud)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("fecha_solicitud");
            entity.Property(e => e.MontoAutorizado)
                .HasColumnType("decimal(8, 2)")
                .HasColumnName("monto_autorizado");
            entity.Property(e => e.MontoSolicitado)
                .HasColumnType("decimal(8, 2)")
                .HasColumnName("monto_solicitado");
            entity.Property(e => e.SolicitudViajeId).HasColumnName("solicitud_viaje_id");

            entity.HasOne(d => d.AutorizadoPor).WithMany(p => p.Anticipos)
                .HasForeignKey(d => d.AutorizadoPorId)
                .HasConstraintName("FK_anticipos_autorizado_por");

            entity.HasOne(d => d.SolicitudViaje).WithMany(p => p.Anticipos)
                .HasForeignKey(d => d.SolicitudViajeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_anticipos_solicitud");
        });

        modelBuilder.Entity<AuditoriaSistema>(entity =>
        {
            entity.ToTable("auditoria_sistema");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Accion)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("accion");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.EmpleadoId).HasColumnName("empleado_id");
            entity.Property(e => e.Entidad)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("entidad");
            entity.Property(e => e.EntidadId).HasColumnName("entidad_id");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("ip_address");
            entity.Property(e => e.UserAgent)
                .HasColumnType("text")
                .HasColumnName("user_agent");
            entity.Property(e => e.ValoresAnteriores)
                .HasColumnType("text")
                .HasColumnName("valores_anteriores");
            entity.Property(e => e.ValoresNuevos)
                .HasColumnType("text")
                .HasColumnName("valores_nuevos");

            entity.HasOne(d => d.Empleado).WithMany(p => p.AuditoriaSistemas)
                .HasForeignKey(d => d.EmpleadoId)
                .HasConstraintName("FK_auditoria_sistema_empleado");
        });

        modelBuilder.Entity<CategoriasGasto>(entity =>
        {
            entity.ToTable("categorias_gasto");

            entity.HasIndex(e => e.Codigo, "UQ_categorias_gasto_codigo").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Activo)
                .HasDefaultValue(true)
                .HasColumnName("activo");
            entity.Property(e => e.AplicaLimiteDiario)
                .HasDefaultValue(false)
                .HasColumnName("aplica_limite_diario");
            entity.Property(e => e.Codigo)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("codigo");
            entity.Property(e => e.Nombre)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("nombre");
            entity.Property(e => e.RequiereFactura)
                .HasDefaultValue(true)
                .HasColumnName("requiere_factura");
        });

        modelBuilder.Entity<ComprobacionesViaje>(entity =>
        {
            entity.ToTable("comprobaciones_viaje");

            entity.HasIndex(e => e.CodigoComprobacion, "UQ_comprobaciones_viaje_codigo").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AprobacionJefeId).HasColumnName("aprobacion_jefe_id");
            entity.Property(e => e.CodigoComprobacion)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("codigo_comprobacion");
            entity.Property(e => e.ComentariosFinanzas)
                .HasColumnType("text")
                .HasColumnName("comentarios_finanzas");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.DescripcionActividades)
                .HasColumnType("text")
                .HasColumnName("descripcion_actividades");
            entity.Property(e => e.Diferencia)
                .HasColumnType("decimal(8, 2)")
                .HasColumnName("diferencia");
            entity.Property(e => e.EscenarioLiquidacion)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("escenario_liquidacion");
            entity.Property(e => e.EstadoComprobacionId).HasColumnName("estado_comprobacion_id");
            entity.Property(e => e.FechaCierre).HasColumnName("fecha_cierre");
            entity.Property(e => e.FechaComprobacion)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("fecha_comprobacion");
            entity.Property(e => e.ReabiertoPorId).HasColumnName("reabierto_por_id");
            entity.Property(e => e.RequiereAprobacionJefe)
                .HasDefaultValue(false)
                .HasColumnName("requiere_aprobacion_jefe");
            entity.Property(e => e.ResultadosViaje)
                .HasColumnType("text")
                .HasColumnName("resultados_viaje");
            entity.Property(e => e.SolicitudViajeId).HasColumnName("solicitud_viaje_id");
            entity.Property(e => e.TotalAnticipo)
                .HasColumnType("decimal(8, 2)")
                .HasColumnName("total_anticipo");
            entity.Property(e => e.TotalGastosComprobados)
                .HasColumnType("decimal(8, 2)")
                .HasColumnName("total_gastos_comprobados");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.AprobacionJefe).WithMany(p => p.ComprobacionesViajeAprobacionJeves)
                .HasForeignKey(d => d.AprobacionJefeId)
                .HasConstraintName("FK_comprobaciones_viaje_aprobacion_jefe");

            entity.HasOne(d => d.EstadoComprobacion).WithMany(p => p.ComprobacionesViajes)
                .HasForeignKey(d => d.EstadoComprobacionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_comprobaciones_viaje_estado");

            entity.HasOne(d => d.ReabiertoPor).WithMany(p => p.ComprobacionesViajeReabiertoPors)
                .HasForeignKey(d => d.ReabiertoPorId)
                .HasConstraintName("FK_comprobaciones_viaje_reabierto_por");

            entity.HasOne(d => d.SolicitudViaje).WithMany(p => p.ComprobacionesViajes)
                .HasForeignKey(d => d.SolicitudViajeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_comprobaciones_viaje_solicitud");
        });

        modelBuilder.Entity<CotizacionesFinanzas>(entity =>
        {
            entity.ToTable("cotizaciones_finanzas");

            entity.HasIndex(e => e.CodigoCotizacion, "UQ_cotizaciones_finanzas_codigo").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AlimentosCantidad)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("alimentos_cantidad");
            entity.Property(e => e.AlimentosPreciosJson).HasColumnName("alimentos_precios_json");
            entity.Property(e => e.AlimentosTotal)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("alimentos_total");
            entity.Property(e => e.CasetasCantidad)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("casetas_cantidad");
            entity.Property(e => e.CasetasPreciosJson).HasColumnName("casetas_precios_json");
            entity.Property(e => e.CasetasTotal)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("casetas_total");
            entity.Property(e => e.CodigoCotizacion)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("codigo_cotizacion");
            entity.Property(e => e.CreadoPorId).HasColumnName("creado_por_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.Estado)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("estado");
            entity.Property(e => e.FechaAprobacion).HasColumnName("fecha_aprobacion");
            entity.Property(e => e.FechaCotizacion)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("fecha_cotizacion");
            entity.Property(e => e.GasolinaCantidad)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("gasolina_cantidad");
            entity.Property(e => e.GasolinaPreciosJson).HasColumnName("gasolina_precios_json");
            entity.Property(e => e.GasolinaTotal)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("gasolina_total");
            entity.Property(e => e.HospedajeCantidad)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("hospedaje_cantidad");
            entity.Property(e => e.HospedajePreciosJson).HasColumnName("hospedaje_precios_json");
            entity.Property(e => e.HospedajeTotal)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("hospedaje_total");
            entity.Property(e => e.Observaciones)
                .HasColumnType("text")
                .HasColumnName("observaciones");
            entity.Property(e => e.RevisadoPorId).HasColumnName("revisado_por_id");
            entity.Property(e => e.SolicitudViajeId).HasColumnName("solicitud_viaje_id");
            entity.Property(e => e.TotalAutorizado)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("total_autorizado");
            entity.Property(e => e.TransporteCantidad)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("transporte_cantidad");
            entity.Property(e => e.TransportePreciosJson).HasColumnName("transporte_precios_json");
            entity.Property(e => e.TransporteTotal)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("transporte_total");
            entity.Property(e => e.UberTaxiCantidad)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("uber_taxi_cantidad");
            entity.Property(e => e.UberTaxiPreciosJson).HasColumnName("uber_taxi_precios_json");
            entity.Property(e => e.UberTaxiTotal)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("uber_taxi_total");

            entity.HasOne(d => d.CreadoPor).WithMany(p => p.CotizacionesFinanzaCreadoPors)
                .HasForeignKey(d => d.CreadoPorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_cotizaciones_finanzas_creado_por");

            entity.HasOne(d => d.RevisadoPor).WithMany(p => p.CotizacionesFinanzaRevisadoPors)
                .HasForeignKey(d => d.RevisadoPorId)
                .HasConstraintName("FK_cotizaciones_finanzas_revisado_por");

            entity.HasOne(d => d.SolicitudViaje).WithMany(p => p.CotizacionesFinanzas)
                .HasForeignKey(d => d.SolicitudViajeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_cotizaciones_finanzas_solicitud");
        });

        modelBuilder.Entity<Empleados>(entity =>
        {
            entity.ToTable("empleados");

            entity.HasIndex(e => e.JefeDirectoId, "IX_empleados_jefe_directo_id");

            entity.HasIndex(e => e.RolId, "IX_empleados_rol_id");

            entity.HasIndex(e => e.Email, "UQ_empleados_email").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Activo)
                .HasDefaultValue(true)
                .HasColumnName("activo");
            entity.Property(e => e.Apellidos)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("apellidos");
            entity.Property(e => e.AreaAdscripcion)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("area_adscripcion");
            entity.Property(e => e.ColaboradorRemoto)
                .HasDefaultValue(false)
                .HasColumnName("colaborador_remoto");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.Departamento)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("departamento");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.FechaIngreso).HasColumnName("fecha_ingreso");
            entity.Property(e => e.JefeDirectoId).HasColumnName("jefe_directo_id");
            entity.Property(e => e.NivelPuesto)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("nivel_puesto");
            entity.Property(e => e.Nombre)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("nombre");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(256)
                .IsUnicode(false)
                .HasColumnName("password_hash");
            entity.Property(e => e.Puesto)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("puesto");
            entity.Property(e => e.RolId).HasColumnName("rol_id");
            entity.Property(e => e.Telefono)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("telefono");
            entity.Property(e => e.UbicacionBase)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("ubicacion_base");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.JefeDirecto).WithMany(p => p.InverseJefeDirecto)
                .HasForeignKey(d => d.JefeDirectoId)
                .HasConstraintName("FK_empleados_jefe");

            entity.HasOne(d => d.Rol).WithMany(p => p.Empleados)
                .HasForeignKey(d => d.RolId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_empleados_rol");
        });

        modelBuilder.Entity<EstadosComprobacion>(entity =>
        {
            entity.ToTable("estados_comprobacion");

            entity.HasIndex(e => e.Codigo, "UQ_estados_comprobacion_codigo").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Codigo)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("codigo");
            entity.Property(e => e.Descripcion)
                .HasColumnType("text")
                .HasColumnName("descripcion");
        });

        modelBuilder.Entity<EstadosGasto>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__estados___3214EC075FDF725F");

            entity.ToTable("estados_gasto");

            entity.Property(e => e.Codigo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Descripcion)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<EstadosSolicitud>(entity =>
        {
            entity.ToTable("estados_solicitud");

            entity.HasIndex(e => e.Codigo, "UQ_estados_solicitud_codigo").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Codigo)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("codigo");
            entity.Property(e => e.Descripcion)
                .HasColumnType("text")
                .HasColumnName("descripcion");
            entity.Property(e => e.EsEstadoFinal)
                .HasDefaultValue(false)
                .HasColumnName("es_estado_final");
            entity.Property(e => e.Orden).HasColumnName("orden");
        });

        modelBuilder.Entity<Facturas>(entity =>
        {
            entity.ToTable("facturas");

            entity.HasIndex(e => e.GastoRealId, "UQ_facturas_gasto_real").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ArchivoPdfUrl)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("archivo_pdf_url");
            entity.Property(e => e.ArchivoXmlUrl)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("archivo_xml_url");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.GastoRealId).HasColumnName("gasto_real_id");
            entity.Property(e => e.Moneda)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("MXN")
                .HasColumnName("moneda");

            entity.HasOne(d => d.GastoReal).WithOne(p => p.Factura)
                .HasForeignKey<Facturas>(d => d.GastoRealId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_facturas_gasto_real");
        });

        modelBuilder.Entity<FlujoAprobaciones>(entity =>
        {
            entity.ToTable("flujo_aprobaciones");

            entity.HasIndex(e => e.SolicitudViajeId, "IX_flujo_aprobaciones_solicitud_id");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Comentarios)
                .HasColumnType("text")
                .HasColumnName("comentarios");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.EmpleadoAprobadorId).HasColumnName("empleado_aprobador_id");
            entity.Property(e => e.EstadoAprobacion)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("estado_aprobacion");
            entity.Property(e => e.Etapa)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("etapa");
            entity.Property(e => e.FechaAprobacion).HasColumnName("fecha_aprobacion");
            entity.Property(e => e.FirmaElectronicaUrl)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("firma_electronica_url");
            entity.Property(e => e.Notificado)
                .HasDefaultValue(false)
                .HasColumnName("notificado");
            entity.Property(e => e.OrdenEtapa).HasColumnName("orden_etapa");
            entity.Property(e => e.SolicitudViajeId).HasColumnName("solicitud_viaje_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.EmpleadoAprobador).WithMany(p => p.FlujoAprobaciones)
                .HasForeignKey(d => d.EmpleadoAprobadorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_flujo_aprobaciones_aprobador");

            entity.HasOne(d => d.SolicitudViaje).WithMany(p => p.FlujoAprobaciones)
                .HasForeignKey(d => d.SolicitudViajeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_flujo_aprobaciones_solicitud");
        });

        modelBuilder.Entity<GastosReales>(entity =>
        {
            entity.ToTable("gastos_reales");

            entity.HasIndex(e => e.FechaGasto, "IX_gastos_reales_fecha_gasto");

            entity.HasIndex(e => e.SolicitudViajeId, "IX_gastos_reales_solicitud_id");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AplicaLimiteHorario)
                .HasDefaultValue(false)
                .HasColumnName("aplica_limite_horario");
            entity.Property(e => e.CategoriaGastoId).HasColumnName("categoria_gasto_id");
            entity.Property(e => e.Concepto)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("concepto");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.Descripcion)
                .HasColumnType("text")
                .HasColumnName("descripcion");
            entity.Property(e => e.EstadoGastoId).HasColumnName("estado_gasto_id");
            entity.Property(e => e.FechaGasto).HasColumnName("fecha_gasto");
            entity.Property(e => e.FueraHorarioLaboral)
                .HasDefaultValue(false)
                .HasColumnName("fuera_horario_laboral");
            entity.Property(e => e.LugarGasto)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("lugar_gasto");
            entity.Property(e => e.MedioPago)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("medio_pago");
            entity.Property(e => e.Monto)
                .HasColumnType("decimal(8, 2)")
                .HasColumnName("monto");
            entity.Property(e => e.PagoConTarjeta)
                .HasDefaultValue(false)
                .HasColumnName("pago_con_tarjeta");
            entity.Property(e => e.Proveedor)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("proveedor");
            entity.Property(e => e.SolicitudViajeId).HasColumnName("solicitud_viaje_id");

            entity.HasOne(d => d.CategoriaGasto).WithMany(p => p.GastosReales)
                .HasForeignKey(d => d.CategoriaGastoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_gastos_reales_categoria");

            entity.HasOne(d => d.EstadoGasto).WithMany(p => p.GastosReales)
                .HasForeignKey(d => d.EstadoGastoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GastosReales_EstadosGasto");

            entity.HasOne(d => d.SolicitudViaje).WithMany(p => p.GastosReales)
                .HasForeignKey(d => d.SolicitudViajeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_gastos_reales_solicitud");
        });

        modelBuilder.Entity<Notificaciones>(entity =>
        {
            entity.ToTable("notificaciones");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EmpleadoId).HasColumnName("empleado_id");
            entity.Property(e => e.EntidadId).HasColumnName("entidad_id");
            entity.Property(e => e.EntidadRelacionada)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("entidad_relacionada");
            entity.Property(e => e.FechaEnvio)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("fecha_envio");
            entity.Property(e => e.Leida)
                .HasDefaultValue(false)
                .HasColumnName("leida");
            entity.Property(e => e.Mensaje)
                .HasColumnType("text")
                .HasColumnName("mensaje");
            entity.Property(e => e.Prioridad)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("prioridad");
            entity.Property(e => e.Tipo)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("tipo");
            entity.Property(e => e.Titulo)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("titulo");

            entity.HasOne(d => d.Empleado).WithMany(p => p.Notificaciones)
                .HasForeignKey(d => d.EmpleadoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_notificaciones_empleado");
        });

        modelBuilder.Entity<RolesSistema>(entity =>
        {
            entity.ToTable("Roles_Sistema");

            entity.HasIndex(e => e.Codigo, "UQ_Roles_Sistema_codigo").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Codigo)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("codigo");
            entity.Property(e => e.Descripcion)
                .HasColumnType("text")
                .HasColumnName("descripcion");
            entity.Property(e => e.NivelAprobacion).HasColumnName("nivel_aprobacion");
            entity.Property(e => e.Nombre)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("nombre");
        });

        modelBuilder.Entity<SolicitudesViaje>(entity =>
        {
            entity.ToTable("solicitudes_viaje");

            entity.HasIndex(e => e.EmpleadoId, "IX_solicitudes_viaje_empleado_id");

            entity.HasIndex(e => e.EstadoId, "IX_solicitudes_viaje_estado_id");

            entity.HasIndex(e => e.FechaSalida, "IX_solicitudes_viaje_fecha_salida");

            entity.HasIndex(e => e.CodigoSolicitud, "UQ_solicitudes_viaje_codigo").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClasificacionDistancia)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("clasificacion_distancia");
            entity.Property(e => e.CodigoSolicitud)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("codigo_solicitud");
            entity.Property(e => e.Colaboradores)
                .HasColumnType("text")
                .HasColumnName("colaboradores");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.CumplePlazoMinimo)
                .HasDefaultValue(false)
                .HasColumnName("cumple_plazo_minimo");
            entity.Property(e => e.Destino)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("destino");
            entity.Property(e => e.DireccionEmpresa)
                .HasMaxLength(170)
                .IsUnicode(false)
                .HasColumnName("direccion_empresa");
            entity.Property(e => e.DireccionTaxiDestino)
                .HasMaxLength(170)
                .IsUnicode(false)
                .HasColumnName("direccion_taxi_destino");
            entity.Property(e => e.DireccionTaxiOrigen)
                .HasMaxLength(250)
                .IsUnicode(false)
                .HasColumnName("direccion_taxi_origen");
            entity.Property(e => e.EmpleadoId).HasColumnName("empleado_id");
            entity.Property(e => e.EmpresaVisitada)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("empresa_visitada");
            entity.Property(e => e.EstadoId).HasColumnName("estado_id");
            entity.Property(e => e.FechaRegreso).HasColumnName("fecha_regreso");
            entity.Property(e => e.FechaSalida).HasColumnName("fecha_salida");
            entity.Property(e => e.HoraRegreso).HasColumnName("hora_regreso");
            entity.Property(e => e.HoraSalida).HasColumnName("hora_salida");
            entity.Property(e => e.LugarComisionDetallado)
                .HasColumnType("text")
                .HasColumnName("lugar_comision_detallado");
            entity.Property(e => e.MedioTrasladoPrincipal)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("medio_traslado_principal");
            entity.Property(e => e.MontoAnticipo)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("monto_anticipo");
            entity.Property(e => e.Motivo)
                .HasColumnType("text")
                .HasColumnName("motivo");
            entity.Property(e => e.NochesHospedaje)
                .HasDefaultValue(0)
                .HasColumnName("noches_hospedaje");
            entity.Property(e => e.NombreProyecto)
                .HasMaxLength(400)
                .HasDefaultValue("Proyecto General")
                .HasColumnName("nombre_proyecto");
            entity.Property(e => e.NumeroPersonas)
                .HasDefaultValue(1)
                .HasColumnName("numero_personas");
            entity.Property(e => e.RequiereAnticipo)
                .HasDefaultValue(false)
                .HasColumnName("requiere_anticipo");
            entity.Property(e => e.RequiereHospedaje)
                .HasDefaultValue(false)
                .HasColumnName("requiere_hospedaje");
            entity.Property(e => e.RequiereTaxiDomicilio)
                .HasDefaultValue(false)
                .HasColumnName("requiere_taxi_domicilio");
            entity.Property(e => e.TipoViaticoId).HasColumnName("tipo_viatico_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("updated_at");
            entity.Property(e => e.ValidacionPlazos)
                .HasDefaultValue(false)
                .HasColumnName("validacion_plazos");

            entity.HasOne(d => d.Empleado).WithMany(p => p.SolicitudesViajes)
                .HasForeignKey(d => d.EmpleadoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_solicitudes_viaje_empleado");

            entity.HasOne(d => d.Estado).WithMany(p => p.SolicitudesViajes)
                .HasForeignKey(d => d.EstadoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_solicitudes_viaje_estado");

            entity.HasOne(d => d.TipoViatico).WithMany(p => p.SolicitudesViajes)
                .HasForeignKey(d => d.TipoViaticoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_solicitudes_viaje_tipo_viatico");
        });

        modelBuilder.Entity<TiposViatico>(entity =>
        {
            entity.ToTable("tipos_viatico");

            entity.HasIndex(e => e.Codigo, "UQ_tipos_viatico_codigo").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AplicaLimitesLegales)
                .HasDefaultValue(true)
                .HasColumnName("aplica_limites_legales");
            entity.Property(e => e.Codigo)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("codigo");
            entity.Property(e => e.Descripcion)
                .HasColumnType("text")
                .HasColumnName("descripcion");
            entity.Property(e => e.Nombre)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("nombre");
            entity.Property(e => e.RequiereAprobacionDireccion)
                .HasDefaultValue(false)
                .HasColumnName("requiere_aprobacion_direccion");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
