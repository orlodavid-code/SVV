// Si no existe BASE_URL global, lo definimos (por si acaso)
const BASE_URL = window.BASE_URL || (window.location.pathname.includes('/Viaticos') ? '/Viaticos' : '');

class SistemaReportes {
    constructor() {
        this.charts = {};
        this.isLoading = false;
        this.initialLoadDone = false;
        this.dataTableInstance = null;
        this.tablaDetalleGastos = null;

        // CONFIGURACIONES DE GRÁFICOS - TIPOS PERMITIDOS POR CADA CHART
        this.chartConfigs = {
            gastosDepartamento: { defaultType: 'bar', allowedTypes: ['bar', 'pie'] },
            anticiposMayores: { defaultType: 'bar', allowedTypes: ['bar'] },
            evolucionMensual: { defaultType: 'line', allowedTypes: ['line', 'bar'] },
            escenarios: { defaultType: 'doughnut', allowedTypes: ['doughnut', 'pie'] },
            estadosComprobacion: { defaultType: 'doughnut', allowedTypes: ['doughnut', 'pie'] }
        };

        console.log("SistemaReportes: Clase inicializada");
    }

    // INICIALIZACIÓN PRINCIPAL DEL SISTEMA DE REPORTES
    inicializar() {
        if (this.initialLoadDone) return;

        try {
            this.configurarFechasPorDefecto();
            this.configurarEventos();
            this.initialLoadDone = true;

            setTimeout(() => {
                this.cargarResumenGeneral();
                this.cargarTodosLosGraficos();
                this.inicializarTablaDetalle();
                console.log("SistemaReportes: Carga completa realizada");
            }, 300);

        } catch (error) {
            console.error("SistemaReportes: Error en inicialización", error);
        }
    }

    // CONFIGURACIÓN AUTOMÁTICA DE FECHAS (MES ACTUAL POR DEFECTO)
    configurarFechasPorDefecto() {
        const hoy = new Date();
        const añoActual = hoy.getFullYear();
        const mesActual = hoy.getMonth();

        const primerDiaMes = new Date(añoActual, mesActual, 1);
        const ultimoDiaMes = new Date(añoActual, mesActual + 1, 0);

        const fechaInicio = document.getElementById('fechaInicio');
        const fechaFin = document.getElementById('fechaFin');

        const formatDate = (date) => {
            if (!date || isNaN(date.getTime())) return '';
            return date.toISOString().split('T')[0];
        };

        if (fechaInicio && (!fechaInicio.value || fechaInicio.value === '')) {
            fechaInicio.value = formatDate(primerDiaMes);
        }

        if (fechaFin && (!fechaFin.value || fechaFin.value === '')) {
            fechaFin.value = formatDate(ultimoDiaMes);
        }
    }

    // CONFIGURACIÓN DE TODOS LOS EVENTOS DEL DOM
    configurarEventos() {
        const btnAplicar = document.getElementById('aplicarFiltros');
        if (btnAplicar) {
            btnAplicar.addEventListener('click', (e) => {
                e.preventDefault();
                this.aplicarFiltros();
            });
        }

        const btnExportar = document.getElementById('exportarExcel');
        if (btnExportar) {
            btnExportar.addEventListener('click', () => this.exportarExcel());
        }

        const btnActualizar = document.getElementById('actualizarDashboard');
        if (btnActualizar) {
            btnActualizar.addEventListener('click', () => this.actualizarDashboard());
        }

        // EVENTOS PARA FILTROS RÁPIDOS
        ['filtroUltimoMes', 'filtroEsteAnio', 'filtroRangoCompleto'].forEach(id => {
            const elemento = document.getElementById(id);
            if (elemento) {
                elemento.addEventListener('change', (e) => this.manejarFiltroRapido(e.target));
            }
        });

        // EVENTOS PARA SELECTS DE FILTRO
        ['departamento', 'escenario'].forEach(id => {
            const select = document.getElementById(id);
            if (select) {
                select.addEventListener('change', () => {
                    setTimeout(() => this.aplicarFiltros(), 300);
                });
            }
        });

        // EVENTOS PARA CAMBIOS EN FECHAS
        const fechaInicio = document.getElementById('fechaInicio');
        const fechaFin = document.getElementById('fechaFin');

        if (fechaInicio) {
            fechaInicio.addEventListener('change', () => {
                this.actualizarCheckboxes();
                setTimeout(() => this.aplicarFiltros(), 500);
            });
        }

        if (fechaFin) {
            fechaFin.addEventListener('change', () => {
                this.actualizarCheckboxes();
                setTimeout(() => this.aplicarFiltros(), 500);
            });
        }
    }

    // CARGA DE TODOS LOS GRÁFICOS EN PARALELO
    async cargarTodosLosGraficos() {
        if (this.isLoading) return;
        this.isLoading = true;

        try {
            // DESTRUIR GRÁFICOS EXISTENTES ANTES DE CREAR NUEVOS
            Object.keys(this.charts).forEach(chartId => {
                if (this.charts[chartId] && typeof this.charts[chartId].destroy === 'function') {
                    try {
                        this.charts[chartId].destroy();
                    } catch (e) { }
                }
            });
            this.charts = {};

            // CARGA PARALELA DE GRÁFICOS
            await Promise.all([
                this.cargarGraficoGastosPorDepartamento(),
                this.cargarGraficoAnticiposMayores(),
                this.cargarGraficoEvolucionMensual(),
                this.cargarGraficoEscenarios(),
                this.cargarGraficoEstadosComprobacion()
            ]);

            this.inicializarBotonesGraficos();

        } catch (error) {
            console.error('SistemaReportes: Error cargando gráficos', error);
        } finally {
            this.isLoading = false;
        }
    }

    // GRÁFICO DE GASTOS POR DEPARTAMENTO (TOP 10)
    async cargarGraficoGastosPorDepartamento() {
        try {
            const canvas = document.getElementById('chartGastosDepartamento');
            if (!canvas) return;

            const params = this.construirParametros();
            const response = await fetch(`${BASE_URL}/Reportes/api/GetGastosPorDepartamento?${params}`);

            if (!response.ok) {
                this.mostrarMensajeEnGrafico(canvas.id, 'Sin datos');
                return;
            }

            const data = await response.json();

            if (!Array.isArray(data) || data.length === 0) {
                this.mostrarMensajeEnGrafico(canvas.id, 'Sin datos');
                return;
            }

            const existingChart = Chart.getChart(canvas);
            if (existingChart) existingChart.destroy();

            const topData = data.slice(0, 10);
            const labels = topData.map(item => {
                const depto = item.Departamento || 'Sin departamento';
                return depto.length > 20 ? depto.substring(0, 17) + '...' : depto;
            });
            const valores = topData.map(item => item.TotalGastado || 0);

            const ctx = canvas.getContext('2d');
            this.charts.gastosDepartamento = new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [{
                        label: 'Gastos ($)',
                        data: valores,
                        backgroundColor: 'rgba(92, 200, 123, 0.7)',
                        borderColor: 'rgba(92, 200, 123, 1)',
                        borderWidth: 1,
                        borderRadius: 4
                    }]
                },
                options: this.obtenerOpcionesGrafica('bar', true)
            });

        } catch (error) {
            console.error('SistemaReportes: Error en gráfico departamentos', error);
            this.mostrarMensajeEnGrafico('chartGastosDepartamento', 'Error');
        }
    }

    // GRÁFICO DE ANTICIPOS MAYORES (TOP 10 EMPLEADOS)
    async cargarGraficoAnticiposMayores() {
        try {
            const canvas = document.getElementById('chartAnticiposMayores');
            if (!canvas) return;

            const params = this.construirParametros();
            const response = await fetch(`${BASE_URL}/Reportes/api/GetAnticiposMayores?${params}&top=10`);

            if (!response.ok) {
                this.mostrarMensajeEnGrafico(canvas.id, 'Sin datos');
                return;
            }

            const data = await response.json();

            if (!Array.isArray(data) || data.length === 0) {
                this.mostrarMensajeEnGrafico(canvas.id, 'Sin datos');
                return;
            }

            const existingChart = Chart.getChart(canvas);
            if (existingChart) existingChart.destroy();

            const topData = data.slice(0, 10);
            const labels = topData.map(item => {
                const nombre = item.Empleado || 'Sin nombre';
                return nombre.length > 15 ? nombre.substring(0, 12) + '...' : nombre;
            });
            const valores = topData.map(item => item.MontoAnticipo || 0);

            const ctx = canvas.getContext('2d');
            this.charts.anticiposMayores = new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [{
                        label: 'Anticipo ($)',
                        data: valores,
                        backgroundColor: 'rgba(255, 193, 7, 0.7)',
                        borderColor: 'rgba(255, 193, 7, 1)',
                        borderWidth: 1,
                        borderRadius: 4
                    }]
                },
                options: {
                    ...this.obtenerOpcionesGrafica('bar', true),
                    indexAxis: 'x'
                }
            });

        } catch (error) {
            console.error('SistemaReportes: Error en gráfico anticipos', error);
            this.mostrarMensajeEnGrafico('chartAnticiposMayores', 'Error');
        }
    }

    // GRÁFICO DE DISTRIBUCIÓN POR ESCENARIO (PIE/DOUGHNUT)
    async cargarGraficoEscenarios() {
        try {
            const canvas = document.getElementById('chartEscenarios');
            if (!canvas) return;

            const params = this.construirParametros();
            const response = await fetch(`${BASE_URL}/Reportes/api/GetEstadisticasPorEscenario?${params}`);

            if (!response.ok) {
                this.mostrarMensajeEnGrafico(canvas.id, 'Sin datos');
                return;
            }

            const data = await response.json();

            if (!Array.isArray(data) || data.length === 0) {
                this.mostrarMensajeEnGrafico(canvas.id, 'Sin datos');
                return;
            }

            const existingChart = Chart.getChart(canvas);
            if (existingChart) existingChart.destroy();

            const labels = data.map(item => item.EscenarioTraducido || item.Escenario || 'Sin escenario');
            const valores = data.map(item => item.Cantidad || 0);
            const colores = [
                'rgba(92, 200, 123, 0.7)',
                'rgba(255, 193, 7, 0.7)',
                'rgba(0, 123, 255, 0.7)',
                'rgba(220, 53, 69, 0.7)',
                'rgba(23, 162, 184, 0.7)'
            ];

            const ctx = canvas.getContext('2d');
            this.charts.escenarios = new Chart(ctx, {
                type: 'pie',
                data: {
                    labels: labels,
                    datasets: [{
                        data: valores,
                        backgroundColor: colores.slice(0, valores.length),
                        borderColor: 'rgba(255, 255, 255, 0.2)',
                        borderWidth: 1
                    }]
                },
                options: this.obtenerOpcionesGrafica('pie')
            });

        } catch (error) {
            console.error('SistemaReportes: Error en gráfico escenarios', error);
            this.mostrarMensajeEnGrafico('chartEscenarios', 'Error');
        }
    }

    // GRÁFICO DE ESTADOS DE COMPROBACIÓN (DOUGHNUT)
    async cargarGraficoEstadosComprobacion() {
        try {
            const canvas = document.getElementById('chartEstadosComprobacion');
            if (!canvas) return;

            const params = this.construirParametros();
            const response = await fetch(`${BASE_URL}/Reportes/api/GetComprobacionesPorEstado?${params}`);

            if (!response.ok) {
                this.mostrarMensajeEnGrafico(canvas.id, 'Sin datos');
                return;
            }

            const data = await response.json();

            if (!Array.isArray(data) || data.length === 0) {
                this.mostrarMensajeEnGrafico(canvas.id, 'Sin datos');
                return;
            }

            const existingChart = Chart.getChart(canvas);
            if (existingChart) existingChart.destroy();

            const labels = data.map(item => item.EstadoDescripcion || item.Estado || 'Sin estado');
            const valores = data.map(item => item.Cantidad || 0);
            const colores = [
                'rgba(40, 167, 69, 0.7)',
                'rgba(255, 193, 7, 0.7)',
                'rgba(220, 53, 69, 0.7)',
                'rgba(23, 162, 184, 0.7)',
                'rgba(108, 117, 125, 0.7)'
            ];

            const ctx = canvas.getContext('2d');
            this.charts.estadosComprobacion = new Chart(ctx, {
                type: 'doughnut',
                data: {
                    labels: labels,
                    datasets: [{
                        data: valores,
                        backgroundColor: colores.slice(0, valores.length),
                        borderColor: 'rgba(255, 255, 255, 0.2)',
                        borderWidth: 1
                    }]
                },
                options: this.obtenerOpcionesGrafica('doughnut')
            });

        } catch (error) {
            console.error('SistemaReportes: Error en gráfico estados', error);
            this.mostrarMensajeEnGrafico('chartEstadosComprobacion', 'Error');
        }
    }

    // GRÁFICO DE EVOLUCIÓN MENSUAL DE GASTOS (LÍNEA)
    async cargarGraficoEvolucionMensual() {
        try {
            const canvas = document.getElementById('chartEvolucionMensual');
            if (!canvas) return;

            const params = this.construirParametros();
            const response = await fetch(`${BASE_URL}/Reportes/api/GetGastosMensuales?${params}`);

            if (!response.ok) {
                this.mostrarMensajeEnGrafico(canvas.id, 'Sin datos');
                return;
            }

            const data = await response.json();
            const gastosMensuales = data.GastosMensuales || [];

            if (!Array.isArray(gastosMensuales) || gastosMensuales.length === 0) {
                this.mostrarMensajeEnGrafico(canvas.id, 'Sin datos');
                return;
            }

            const existingChart = Chart.getChart(canvas);
            if (existingChart) existingChart.destroy();

            const labels = gastosMensuales.map(item => item.MesNombre || `Mes ${item.Mes}`);
            const valores = gastosMensuales.map(item => item.TotalGastado || 0);

            const ctx = canvas.getContext('2d');
            this.charts.evolucionMensual = new Chart(ctx, {
                type: 'line',
                data: {
                    labels: labels,
                    datasets: [{
                        label: 'Gastos ($)',
                        data: valores,
                        borderColor: 'rgba(0, 123, 255, 1)',
                        backgroundColor: 'rgba(0, 123, 255, 0.1)',
                        fill: true,
                        tension: 0.4,
                        borderWidth: 2
                    }]
                },
                options: this.obtenerOpcionesGrafica('line', true)
            });

        } catch (error) {
            console.error('SistemaReportes: Error en gráfico evolución', error);
            this.mostrarMensajeEnGrafico('chartEvolucionMensual', 'Error');
        }
    }

    // INICIALIZACIÓN DE BOTONES PARA CAMBIAR TIPO DE GRÁFICO
    inicializarBotonesGraficos() {
        const chartMapping = {
            'gastosDepartamento': 'gastosdepartamento',
            'anticiposMayores': 'anticiposmayores',
            'evolucionMensual': 'evolucionmensual',
            'escenarios': 'escenarios',
            'estadosComprobacion': 'estadoscomprobacion'
        };

        Object.keys(this.chartConfigs).forEach(chartId => {
            const config = this.chartConfigs[chartId];
            const dataChartValue = chartMapping[chartId] || chartId;
            const chartContainer = document.querySelector(`[data-chart="${dataChartValue}"]`);

            if (!chartContainer) return;

            const typeButtons = chartContainer.querySelectorAll('.chart-type-btn');
            typeButtons.forEach(btn => {
                const btnType = btn.getAttribute('data-type');
                const allowedTypes = config.allowedTypes;

                if (allowedTypes && !allowedTypes.includes(btnType)) {
                    btn.style.display = 'none';
                    return;
                }

                const newBtn = btn.cloneNode(true);
                btn.parentNode.replaceChild(newBtn, btn);

                newBtn.addEventListener('click', (e) => {
                    e.preventDefault();
                    const chartType = newBtn.getAttribute('data-type');
                    this.cambiarTipoGrafico(chartId, chartType);
                    this.actualizarBotonesActivos(dataChartValue, chartType);
                });

                if (btnType === config.defaultType) {
                    newBtn.classList.add('active');
                }
            });

            const exportButtons = chartContainer.querySelectorAll('.export-chart-btn');
            exportButtons.forEach(btn => {
                const newBtn = btn.cloneNode(true);
                btn.parentNode.replaceChild(newBtn, btn);

                newBtn.addEventListener('click', (e) => {
                    e.preventDefault();
                    this.exportarGrafico(chartId);
                });
            });
        });
    }

    // CAMBIO DINÁMICO DE TIPO DE GRÁFICO
    cambiarTipoGrafico(chartId, nuevoTipo) {
        const chart = this.charts[chartId];
        if (!chart) return;

        try {
            chart.config.type = nuevoTipo;
            chart.update();
        } catch (error) {
            console.error(`SistemaReportes: Error cambiando gráfico ${chartId}`, error);
        }
    }

    // ACTUALIZACIÓN DE ESTADO DE BOTONES DE TIPO
    actualizarBotonesActivos(dataChartValue, tipoActivo) {
        const contenedor = document.querySelector(`[data-chart="${dataChartValue}"]`);
        if (!contenedor) return;

        contenedor.querySelectorAll('.chart-type-btn').forEach(btn => {
            btn.classList.remove('active');
        });

        const botonActivo = contenedor.querySelector(`.chart-type-btn[data-type="${tipoActivo}"]`);
        if (botonActivo) {
            botonActivo.classList.add('active');
        }
    }

    // EXPORTACIÓN DE GRÁFICO A PNG
    exportarGrafico(chartId) {
        const chart = this.charts[chartId];
        if (!chart) return;

        try {
            const link = document.createElement('a');
            const fecha = new Date().toISOString().split('T')[0];
            link.download = `grafico_${chartId}_${fecha}.png`;
            link.href = chart.toBase64Image();
            link.click();
        } catch (error) {
           console.error('SistemaReportes: Error exportando gráfico ${chartId}', error);
        }
    }

    inicializarTablaDetalle() {
        console.log('SistemaReportes: Inicializando tabla detalle');

        if (this.tablaDetalleGastos && $.fn.DataTable.isDataTable('#tablaDetalleGastos')) {
            console.log('SistemaReportes: Recargando tabla existente');
            this.tablaDetalleGastos.ajax.reload(null, false);
            return;
        }

        this.tablaDetalleGastos = $('#tablaDetalleGastos').DataTable({
            processing: true,
            serverSide: true,
            searching: true,
            ordering: true,
            order: [[0, 'asc']],
            pageLength: 25,
            lengthMenu: [10, 25, 50, 100],
            language: { url: `${BASE_URL}/data/datatables/es-MX.json` }, // <-- Corregido con BASE_URL

            ajax: {
                url: `${BASE_URL}/Reportes/api/GetDetalleGastos`, // <-- Corregido con BASE_URL y barra inicial
                type: 'GET',
                data: d => {
                    const filtros = this.obtenerFiltrosActuales();
                    d.fechaInicio = filtros.fechaInicio || '';
                    d.fechaFin = filtros.fechaFin || '';
                    d.departamento = filtros.departamento || '';
                    d.escenario = filtros.escenario || '';
                },
                dataSrc: json => {
                    if (!json || json.error) {
                        console.error('SistemaReportes: Error desde API', json?.error);
                        return [];
                    }

                    if (json.totals) {
                        $('#totalAnticipoTabla').text(this.formatearMoneda(json.totals.totalAnticipo || 0));
                        $('#totalGastadoTabla').text(this.formatearMoneda(json.totals.totalGastado || 0));
                        $('#totalDiferenciaTabla').text(this.formatearMoneda(json.totals.totalDiferencia || 0));
                    }

                    return json.data || [];
                },
                error: (xhr, status, error) => {
                    console.error('SistemaReportes: Error AJAX tabla', {
                        status: xhr.status,
                        error: error
                    });
                }
            },

            columns: [
                { data: 'Empleado', className: 'text-start' },
                { data: 'Departamento', className: 'text-start' },
                { data: 'Proyecto', className: 'text-start' },
                { data: 'Anticipo', className: 'text-end', render: d => this.formatearMoneda(d) },
                { data: 'Gastado', className: 'text-end', render: d => this.formatearMoneda(d) },
                {
                    data: 'Diferencia',
                    className: 'text-end',
                    render: d => {
                        const cls = d >= 0 ? 'text-success' : 'text-danger';
                        return `<span class="${cls} fw-bold">${this.formatearMoneda(d)}</span>`;
                    }
                },
                { data: 'EscenarioTraducido', className: 'text-center', render: d => obtenerBadgeEscenario(d) },
                { data: 'EstadoTraducido', className: 'text-center', render: d => obtenerBadgeEstado(d) },
                {
                    data: 'ComprobacionId',
                    className: 'text-center',
                    orderable: false,
                    searchable: false,
                    render: id => `
                    <a href="${BASE_URL}/Reportes/DetallesCompletos/${id}"
                       class="btn btn-sm btn-primary"
                       data-bs-toggle="tooltip"
                       title="Ver expediente completo">
                        <i class="fas fa-eye"></i>
                    </a>`
                }
            ],

            initComplete: () => {
                console.log('SistemaReportes: DataTable inicializada');
                $('[data-bs-toggle="tooltip"]').tooltip();
            },

            drawCallback: () => {
                $('[data-bs-toggle="tooltip"]').tooltip();
            }
        });
    }

    // OBTENCIÓN DE FILTROS ACTUALES DEL FORMULARIO
    obtenerFiltrosActuales() {
        return {
            fechaInicio: document.getElementById('fechaInicio')?.value || '',
            fechaFin: document.getElementById('fechaFin')?.value || '',
            departamento: document.getElementById('departamento')?.value || '',
            escenario: document.getElementById('escenario')?.value || ''
        };
    }

    obtenerFiltros() {
        return this.obtenerFiltrosActuales();
    }

    // CONSTRUCCIÓN DE PARÁMETROS PARA LLAMADAS API
    construirParametros() {
        const params = new URLSearchParams();

        const fechaInicio = document.getElementById('fechaInicio')?.value || '';
        const fechaFin = document.getElementById('fechaFin')?.value || '';
        const departamento = document.getElementById('departamento')?.value || '';
        const escenario = document.getElementById('escenario')?.value || '';

        if (fechaInicio) params.append('fechaInicio', fechaInicio);
        if (fechaFin) params.append('fechaFin', fechaFin);
        if (departamento && departamento.trim() !== '') {
            params.append('departamento', departamento.trim());
        }
        if (escenario && escenario.trim() !== '') {
            params.append('escenario', escenario.trim());
        }

        return params.toString();
    }

    // APLICACIÓN DE FILTROS Y RECARGA DE DATOS
    aplicarFiltros() {
        if (this.isLoading) {
            console.log('SistemaReportes: Ya se está procesando, ignorando');
            return;
        }

        console.log('SistemaReportes: Aplicando filtros', this.obtenerFiltrosActuales());
        this.actualizarCheckboxes();

        this.cargarResumenGeneral();
        this.cargarTodosLosGraficos();

        if (this.tablaDetalleGastos) {
            console.log('SistemaReportes: Recargando DataTable');
            this.tablaDetalleGastos.ajax.reload(null, false);
        } else {
            console.log('SistemaReportes: Inicializando DataTable nueva');
            this.inicializarTablaDetalle();
        }
    }

    // MANEJO DE FILTROS RÁPIDOS (ÚLTIMO MES, ESTE AÑO, RANGO COMPLETO)
    manejarFiltroRapido(checkbox) {
        if (!checkbox.checked) return;

        const hoy = new Date();
        const fechaInicio = document.getElementById('fechaInicio');
        const fechaFin = document.getElementById('fechaFin');

        if (!fechaInicio || !fechaFin) return;

        ['filtroUltimoMes', 'filtroEsteAnio', 'filtroRangoCompleto'].forEach(id => {
            if (id !== checkbox.id) {
                const otherCheckbox = document.getElementById(id);
                if (otherCheckbox) otherCheckbox.checked = false;
            }
        });

        switch (checkbox.id) {
            case 'filtroUltimoMes':
                const mesPasado = new Date();
                mesPasado.setMonth(mesPasado.getMonth() - 1);
                fechaInicio.value = this.formatearFecha(mesPasado);
                fechaFin.value = this.formatearFecha(hoy);
                break;
            case 'filtroEsteAnio':
                const anio = hoy.getFullYear();
                fechaInicio.value = `${anio}-01-01`;
                fechaFin.value = `${anio}-12-31`;
                break;
            case 'filtroRangoCompleto':
                fechaInicio.value = '';
                fechaFin.value = '';
                break;
        }

        setTimeout(() => this.aplicarFiltros(), 300);
    }

    // FORMATO DE FECHA A YYYY-MM-DD
    formatearFecha(fecha) {
        const año = fecha.getFullYear();
        const mes = String(fecha.getMonth() + 1).padStart(2, '0');
        const dia = String(fecha.getDate()).padStart(2, '0');
        return `${año}-${mes}-${dia}`;
    }

    // ACTUALIZACIÓN DE CHECKBOXES SEGÚN FECHAS SELECCIONADAS
    actualizarCheckboxes() {
        const fechaInicio = document.getElementById('fechaInicio').value;
        const fechaFin = document.getElementById('fechaFin').value;

        if (!fechaInicio || !fechaFin) {
            document.getElementById('filtroRangoCompleto').checked = true;
            document.getElementById('filtroUltimoMes').checked = false;
            document.getElementById('filtroEsteAnio').checked = false;
            return;
        }

        const hoy = new Date();
        const haceUnMes = new Date();
        haceUnMes.setMonth(hoy.getMonth() - 1);

        if (fechaInicio === this.formatearFecha(haceUnMes) &&
            fechaFin === this.formatearFecha(hoy)) {
            document.getElementById('filtroUltimoMes').checked = true;
            document.getElementById('filtroEsteAnio').checked = false;
            document.getElementById('filtroRangoCompleto').checked = false;
        } else if (fechaInicio.includes(hoy.getFullYear().toString() + '-01-01') &&
            fechaFin.includes(hoy.getFullYear().toString() + '-12-31')) {
            document.getElementById('filtroUltimoMes').checked = false;
            document.getElementById('filtroEsteAnio').checked = true;
            document.getElementById('filtroRangoCompleto').checked = false;
        } else {
            document.getElementById('filtroUltimoMes').checked = false;
            document.getElementById('filtroEsteAnio').checked = false;
            document.getElementById('filtroRangoCompleto').checked = false;
        }
    }

    // CARGA DE RESUMEN GENERAL (KPIs)
    async cargarResumenGeneral() {
        try {
            const params = this.construirParametros();
            const response = await fetch(`${BASE_URL}/Reportes/api/GetResumenGeneral?${params}`);

            if (!response.ok) return;

            const data = await response.json();

            if (data.TotalGastado !== undefined) {
                this.actualizarKPI('totalGastado', data.TotalGastado, true);
            }
            if (data.TotalAnticipos !== undefined) {
                this.actualizarKPI('totalAnticipos', data.TotalAnticipos, true);
            }
            if (data.TotalSolicitudes !== undefined) {
                this.actualizarKPI('totalSolicitudes', data.TotalSolicitudes, false);
            }
            if (data.PromedioAnticipo !== undefined) {
                this.actualizarKPI('promedioAnticipo', data.PromedioAnticipo, true);
            }
            if (data.TotalComprobaciones !== undefined) {
                this.actualizarKPI('totalComprobaciones', data.TotalComprobaciones, false);
            }

        } catch (error) {
            console.error('SistemaReportes: Error cargando resumen', error);
        }
    }

    // ACTUALIZACIÓN DE ELEMENTOS KPI EN EL DOM
    actualizarKPI(elementId, valor, esMoneda = false) {
        const element = document.getElementById(elementId);
        if (element) {
            element.textContent = esMoneda ? this.formatearMoneda(valor) : this.formatearNumero(valor);
        }
    }

    // FORMATO DE MONEDA MEXICANA
    formatearMoneda(monto) {
        const val = Number(monto || 0);
        try {
            return new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' }).format(val);
        } catch (e) {
            return val.toFixed(2);
        }
    }

    // FORMATO DE NÚMERO SIN DECIMALES
    formatearNumero(valor) {
        const num = typeof valor === 'number' ? valor : parseFloat(valor || 0);
        if (isNaN(num)) return '0';

        return new Intl.NumberFormat('es-MX', {
            minimumFractionDigits: 0,
            maximumFractionDigits: 0
        }).format(num);
    }

    // MOSTRAR MENSAJE EN CANVAS CUANDO NO HAY DATOS
    mostrarMensajeEnGrafico(canvasId, mensaje) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        const existingChart = Chart.getChart(canvas);
        if (existingChart) {
            existingChart.destroy();
        }

        const ctx = canvas.getContext('2d');
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        ctx.fillStyle = '#6c757d';
        ctx.font = '14px Arial';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(mensaje, canvas.width / 2, canvas.height / 2);
    }

    // OBTENER OPCIONES DE CONFIGURACIÓN PARA CHARTS.JS
    obtenerOpcionesGrafica(tipo, conMoneda = false) {
        const opcionesBase = {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: 'bottom' } }
        };

        if (['bar', 'line'].includes(tipo) && conMoneda) {
            return {
                ...opcionesBase,
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: (value) => `$${this.formatearNumero(value)}`
                        }
                    }
                }
            };
        }

        return opcionesBase;
    }

    // EXPORTACIÓN A EXCEL VÍA API
    exportarExcel() {
        try {
            const params = this.construirParametros();
            const url = `${BASE_URL}/Reportes/api/ExportarExcel?${params}&tipoReporte=COMPLETO`;
            window.open(url, '_blank');
        } catch (error) {
            console.error('SistemaReportes: Error exportando Excel', error);
        }
    }

    // ACTUALIZACIÓN COMPLETA DEL DASHBOARD
    actualizarDashboard() {
        this.aplicarFiltros();
    }
}

// ==================== INICIALIZACIÓN GLOBAL ====================

let primeraCargaReportes = true;

function inicializarReportes() {
    if (typeof Chart === 'undefined' || typeof $ === 'undefined') {
        setTimeout(inicializarReportes, 100);
        return;
    }

    window.sistemaReportes = new SistemaReportes();

    setTimeout(() => {
        if (window.sistemaReportes && typeof window.sistemaReportes.inicializar === 'function') {
            window.sistemaReportes.inicializar();
        }
    }, 200);
}

function detectarNavegacionReportes() {
    const esPaginaReportes = window.location.pathname.includes('/Reportes') ||
        document.querySelector('.reportes-page') !== null;

    if (esPaginaReportes && primeraCargaReportes) {
        primeraCargaReportes = false;

        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', inicializarReportes);
        } else {
            setTimeout(inicializarReportes, 300);
        }
    }
}

// INICIALIZACIÓN CUANDO EL DOM ESTÁ LISTO
document.addEventListener('DOMContentLoaded', function () {
    detectarNavegacionReportes();
});

// OBSERVER PARA CAMBIOS DINÁMICOS EN EL DOM (SPA)
const observer = new MutationObserver(function (mutations) {
    mutations.forEach(function (mutation) {
        if (mutation.addedNodes.length) {
            detectarNavegacionReportes();
        }
    });
});

observer.observe(document.body, { childList: true, subtree: true });

// ==================== FUNCIONES GLOBALES AUXILIARES ====================

function formatoMoneda(valor) {
    const num = typeof valor === 'number' ? valor : parseFloat(valor || 0);
    if (isNaN(num)) return '$0.00';

    return new Intl.NumberFormat('es-MX', {
        style: 'currency',
        currency: 'MXN',
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    }).format(num);
}

function normalizar(valor) {
    return (valor || '')
        .toString()
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .replace(/\s+/g, '_')
        .toUpperCase()
        .trim();
}

function obtenerBadgeEscenario(escenario) {
    const key = normalizar(escenario);

    const map = {
        REPOSICION_EMPRESA: 'badge-reposicion-empresa',
        REPOSICION_COLABORADOR: 'badge-reposicion-colaborador',
        SALDADA: 'badge-saldada',
        PAGO_AUTORIZADO: 'badge-pago-autorizado',
        CON_CORRECCIONES_PENDIENTES: 'badge-correcciones',
        PARCIALMENTE_APROBADA: 'badge-parcialmente',
        EN_REVISION_JP: 'badge-revision-jp'
    };

    const badgeClass = map[key] || 'badge-sin-escenario';
    const texto = escenario || 'Sin escenario';

    return `<span class="badge ${badgeClass}">${texto}</span>`;
}

function obtenerBadgeEstado(estado) {
    const key = normalizar(estado);

    const map = {
        APROBADA: 'badge-aprobada',
        APROBADO: 'badge-aprobada',
        PENDIENTE: 'badge-pendiente',
        RECHAZADA: 'badge-rechazada',
        RECHAZADO: 'badge-rechazada',
        EN_REVISION: 'badge-en-revision',
        LIQUIDADA: 'badge-liquidada',
        COMPLETADA: 'badge-completada',
        PROCESANDO: 'badge-procesando'
    };

    const badgeClass = map[key] || 'badge-sin-estado';
    const texto = estado || 'Sin estado';

    return `<span class="badge ${badgeClass}">${texto}</span>`;
}

function mostrarError(mensaje) {
    console.error('SistemaReportes: Error', mensaje);
    if (typeof mostrarNotificacion === 'function') {
        mostrarNotificacion('Error', mensaje, 'danger');
    } else {
        const alertDiv = document.createElement('div');
        alertDiv.className = 'alert alert-danger alert-dismissible fade show';
        alertDiv.innerHTML = `
            <strong>Error:</strong> ${mensaje}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;
        document.querySelector('.reportes-page').prepend(alertDiv);
    }
}

// EXPOSICIÓN DE FUNCIONES GLOBALES
window.SistemaReportes = SistemaReportes;
window.formatoMoneda = formatoMoneda;
window.obtenerBadgeEscenario = obtenerBadgeEscenario;
window.obtenerBadgeEstado = obtenerBadgeEstado;