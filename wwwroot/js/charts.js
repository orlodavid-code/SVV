// charts.js - Sistema de Gráficas Dinámicas para Sistema de Viáticos

// CLASE PRINCIPAL: DashboardCharts - Manejo centralizado de gráficos
class DashboardCharts {
    constructor() {
        this.charts = {}; // Almacenamiento de instancias de gráficos
        this.colors = this.getThemeColors(); // Configuración de colores del tema
        this.init(); // Inicialización automática al crear instancia
    }

    // INICIALIZACIÓN PRINCIPAL DEL MÓDULO. Configura tema, carga gráficos y vincula eventos
    init() {
        this.loadCharts();
        this.bindEvents();
        this.setupTheme();
    }

    /*CONFIGURACIÓN GLOBAL DE TEMA PARA CHART.JS
     * Aplica colores y estilos consistentes con el tema oscuro */
    setupTheme() {
        Chart.defaults.color = '#c8d6e5';
        Chart.defaults.borderColor = 'rgba(92, 200, 123, 0.2)';
        Chart.defaults.font.family = "'Segoe UI', Tahoma, Geneva, Verdana, sans-serif";
        Chart.defaults.font.size = 12;
    }

    /* DEFINICIÓN DE PALETA DE COLORES PARA TEMA OSCURO
     * Colores corporativos Viamtek adaptados para modo oscuro */
    getThemeColors() {
        return {
            primary: {
                background: 'rgba(92, 200, 123, 0.7)',
                border: 'rgb(92, 200, 123)',
                hover: 'rgba(92, 200, 123, 0.9)'
            },
            warning: {
                background: 'rgba(255, 193, 7, 0.7)',
                border: 'rgb(255, 193, 7)',
                hover: 'rgba(255, 193, 7, 0.9)'
            },
            danger: {
                background: 'rgba(220, 53, 69, 0.7)',
                border: 'rgb(220, 53, 69)',
                hover: 'rgba(220, 53, 69, 0.9)'
            },
            info: {
                background: 'rgba(23, 162, 184, 0.7)',
                border: 'rgb(23, 162, 184)',
                hover: 'rgba(23, 162, 184, 0.9)'
            },
            secondary: {
                background: 'rgba(108, 117, 125, 0.7)',
                border: 'rgb(108, 117, 125)',
                hover: 'rgba(108, 117, 125, 0.9)'
            },
            darkGreen: {
                background: 'rgba(48, 129, 132, 0.7)',
                border: 'rgb(48, 129, 132)',
                hover: 'rgba(48, 129, 132, 0.9)'
            }
        };
    }

    /*CARGA Y RENDERIZACIÓN DE GRÁFICOS
     * Obtiene datos reales y renderiza gráficos según rol */
    loadCharts() {
        try {
            const userRole = this.getCurrentUserRole();
            const chartData = this.getRealChartData(userRole);
            this.renderCharts(chartData);
            this.initializeCharts(chartData);
        } catch (error) {
            this.renderFallbackCharts();
        }
    }

    /*DETECCIÓN DE ROL DE USUARIO ACTUAL
     * Obtiene rol desde atributo data-role del contenedor */
    getCurrentUserRole() {
        try {
            const container = document.getElementById('chartsContainer');
            return container ? parseInt(container.dataset.role) : 1;
        } catch (error) {
            return 1; // Rol por defecto: Empleado
        }
    }

    /* GENERACIÓN DE CONFIGURACIÓN DE GRÁFICOS POR ROL
     * @param {number} role - ID del rol del usuario (1-6)
     * @returns {object} Configuración completa de gráficos para el rol
     */
    getRealChartData(role) {
        const colors = this.colors;

        // ROL 1: EMPLEADO - Gráficos personales de solicitudes y anticipos
        if (role === 1) {
            return {
                role: 'empleado',
                title: 'Mis Métricas',
                icon: 'fa-user-chart',
                charts: [
                    {
                        id: 'chartSolicitudesEmpleado',
                        title: 'Estado de Mis Solicitudes',
                        type: 'doughnut',
                        data: {
                            labels: ['Aprobadas', 'Pendientes', 'Rechazadas', 'Borrador'],
                            datasets: [{
                                data: [
                                    this.getRealValue('MisSolicitudesAprobadas'),
                                    this.getRealValue('MisSolicitudesPendientes'),
                                    this.getRealValue('MisSolicitudesRechazadas'),
                                    this.getRealValue('MisSolicitudesBorrador')
                                ],
                                backgroundColor: [
                                    colors.primary.background,
                                    colors.warning.background,
                                    colors.danger.background,
                                    colors.secondary.background
                                ],
                                borderColor: [
                                    colors.primary.border,
                                    colors.warning.border,
                                    colors.danger.border,
                                    colors.secondary.border
                                ],
                                borderWidth: 1,
                                hoverBackgroundColor: [
                                    colors.primary.hover,
                                    colors.warning.hover,
                                    colors.danger.hover,
                                    colors.secondary.hover
                                ]
                            }]
                        },
                        stats: [
                            { value: this.getRealValue('MisSolicitudesAprobadas'), label: 'Aprobadas', class: 'stat-value-primary' },
                            { value: this.getRealValue('MisSolicitudesPendientes'), label: 'Pendientes', class: 'stat-value-warning' },
                            { value: this.getRealValue('MisSolicitudesRechazadas'), label: 'Rechazadas', class: 'stat-value-danger' }
                        ]
                    },
                    {
                        id: 'chartAnticiposEmpleado',
                        title: 'Mis Anticipos',
                        type: 'bar',
                        data: {
                            labels: ['Solicitados', 'Autorizados'],
                            datasets: [{
                                label: 'Monto ($)',
                                data: [
                                    this.getRealValue('TotalAnticiposSolicitados'),
                                    this.getRealValue('TotalAnticiposAutorizados')
                                ],
                                backgroundColor: [
                                    colors.warning.background,
                                    colors.primary.background
                                ],
                                borderColor: [
                                    colors.warning.border,
                                    colors.primary.border
                                ],
                                borderWidth: 1,
                                borderRadius: 4,
                                hoverBackgroundColor: [
                                    colors.warning.hover,
                                    colors.primary.hover
                                ]
                            }]
                        },
                        stats: [
                            { value: this.formatCurrency(this.getRealValue('TotalAnticiposSolicitados')), label: 'Solicitados', class: 'stat-value-warning' },
                            { value: this.formatCurrency(this.getRealValue('TotalAnticiposAutorizados')), label: 'Autorizados', class: 'stat-value-primary' }
                        ]
                    }
                ]
            };
        }
        // ROL 2: AUTORIZADOR/JEFE DE PROYECTO - Métricas de aprobaciones
        else if (role === 2) {
            return {
                role: 'jefe_proyecto',
                title: 'Métricas de Proyectos',
                icon: 'fa-tasks',
                charts: [
                    {
                        id: 'chartSolicitudesJP',
                        title: 'Solicitudes por Estado',
                        type: 'pie',
                        data: {
                            labels: ['Pendientes JP', 'Aprobadas JP', 'Rechazadas'],
                            datasets: [{
                                data: [
                                    this.getRealValue('PendientesJP'),
                                    this.getRealValue('AprobadasJP'),
                                    this.getRealValue('RechazadasJP')
                                ],
                                backgroundColor: [
                                    colors.warning.background,
                                    colors.primary.background,
                                    colors.danger.background
                                ],
                                borderColor: [
                                    colors.warning.border,
                                    colors.primary.border,
                                    colors.danger.border
                                ],
                                borderWidth: 1,
                                hoverBackgroundColor: [
                                    colors.warning.hover,
                                    colors.primary.hover,
                                    colors.danger.hover
                                ]
                            }]
                        },
                        stats: [
                            { value: this.getRealValue('PendientesJP'), label: 'Pendientes', class: 'stat-value-warning' },
                            { value: this.getRealValue('AprobadasJP'), label: 'Aprobadas', class: 'stat-value-primary' },
                            { value: this.getRealValue('RechazadasJP'), label: 'Rechazadas', class: 'stat-value-danger' }
                        ]
                    }
                ]
            };
        }
        // ROL 3: RECURSOS HUMANOS - Métricas de empleados y solicitudes
        else if (role === 3) {
            return {
                role: 'recursos_humanos',
                title: 'Métricas de RH',
                icon: 'fa-users',
                charts: [
                    {
                        id: 'chartSolicitudesRH',
                        title: 'Solicitudes por Estado',
                        type: 'pie',
                        data: {
                            labels: ['Pendientes RH', 'Aprobadas RH', 'Rechazadas'],
                            datasets: [{
                                data: [
                                    this.getRealValue('PendientesRH'),
                                    this.getRealValue('AprobadasRH'),
                                    this.getRealValue('RechazadasRH')
                                ],
                                backgroundColor: [
                                    colors.warning.background,
                                    colors.primary.background,
                                    colors.danger.background
                                ],
                                borderColor: [
                                    colors.warning.border,
                                    colors.primary.border,
                                    colors.danger.border
                                ],
                                borderWidth: 1,
                                hoverBackgroundColor: [
                                    colors.warning.hover,
                                    colors.primary.hover,
                                    colors.danger.hover
                                ]
                            }]
                        },
                        stats: [
                            { value: this.getRealValue('PendientesRH'), label: 'Pendientes', class: 'stat-value-warning' },
                            { value: this.getRealValue('AprobadasRH'), label: 'Aprobadas', class: 'stat-value-primary' },
                            { value: this.getRealValue('RechazadasRH'), label: 'Rechazadas', class: 'stat-value-danger' }
                        ]
                    },
                    {
                        id: 'chartEmpleadosRH',
                        title: 'Empleados Activos',
                        type: 'doughnut',
                        data: {
                            labels: ['Empleados Activos'],
                            datasets: [{
                                data: [this.getRealValue('TotalEmpleados')],
                                backgroundColor: [colors.primary.background],
                                borderColor: [colors.primary.border],
                                borderWidth: 1,
                                hoverBackgroundColor: [colors.primary.hover]
                            }]
                        },
                        stats: [
                            { value: this.getRealValue('TotalEmpleados'), label: 'Activos', class: 'stat-value-primary' }
                        ]
                    }
                ]
            };
        }
        // ROLES 4,5,6: ADMIN, DIRECCIÓN, FINANZAS - Métricas del sistema
        else {
            return {
                role: 'admin_finanzas',
                title: 'Métricas del Sistema',
                icon: 'fa-chart-line',
                charts: [
                    {
                        id: 'chartResumenAdmin',
                        title: 'Resumen General',
                        type: 'doughnut',
                        data: {
                            labels: ['Solicitudes Activas', 'Pendientes Aprobación'],
                            datasets: [{
                                data: [
                                    this.getRealValue('SolicitudesActivas'),
                                    this.getRealValue('PendientesAprobacion')
                                ],
                                backgroundColor: [
                                    colors.primary.background,
                                    colors.warning.background
                                ],
                                borderColor: [
                                    colors.primary.border,
                                    colors.warning.border
                                ],
                                borderWidth: 1,
                                hoverBackgroundColor: [
                                    colors.primary.hover,
                                    colors.warning.hover
                                ]
                            }]
                        },
                        stats: [
                            { value: this.getRealValue('SolicitudesActivas'), label: 'Activas', class: 'stat-value-primary' },
                            { value: this.getRealValue('PendientesAprobacion'), label: 'Pendientes', class: 'stat-value-warning' },
                            { value: this.getRealValue('TotalUsuariosActivos'), label: 'Usuarios Activos', class: 'stat-value-info' }
                        ]
                    }
                ]
            };
        }
    }

    /*OBTENCIÓN SEGURA DE VALORES DE DATOS
     * @param {string} key - Clave del dato en window.dashboardData
     * @returns {number} Valor numérico o 0 por defecto
     */
    getRealValue(key) {
        if (window.dashboardData && window.dashboardData[key] !== undefined) {
            return window.dashboardData[key];
        }
        return 0;
    }

    /* FORMATEO DE MONEDA EN PESOS MEXICANOS
     * @param {number} amount - Cantidad a formatear
     * @returns {string} Cadena formateada con símbolo de peso
     */
    formatCurrency(amount) {
        if (typeof amount === 'string' && amount.includes('$')) {
            return amount;
        }
        return new Intl.NumberFormat('es-MX', {
            style: 'currency',
            currency: 'MXN'
        }).format(amount);
    }

    //RENDERIZADO DINÁMICO DE HTML DE GRÁFICOS
    renderCharts(chartData) {
        const container = document.getElementById('chartsContainer');
        if (!container) return;

        let html = `
            <div class="charts-header">
                <div class="charts-title">
                    <div class="charts-icon">
                        <i class="fas ${chartData.icon}"></i>
                    </div>
                    ${chartData.title}
                </div>
                <button class="chart-refresh-btn" id="refreshCharts">
                    <i class="fas fa-sync-alt"></i> Actualizar
                </button>
            </div>
            <div class="charts-grid">
        `;

        chartData.charts.forEach((chart, index) => {
            html += `
                <div class="chart-wrapper" style="animation-delay: ${index * 0.1}s;">
                    <div class="chart-title">${chart.title}</div>
                    <div class="chart-container">
                        <canvas id="${chart.id}" class="chart-glow"></canvas>
                    </div>
                    <div class="chart-stats">
            `;

            chart.stats.forEach(stat => {
                html += `
                    <div class="chart-stat">
                        <div class="stat-value ${stat.class}">${stat.value}</div>
                        <div class="stat-label">${stat.label}</div>
                    </div>
                `;
            });

            html += `
                    </div>
                </div>
            `;
        });

        html += `</div>`;
        container.innerHTML = html;
    }

    //INICIALIZACIÓN DE INSTANCIAS DE CHART.JS

    initializeCharts(chartData) {
        chartData.charts.forEach(chartConfig => {
            try {
                const canvas = document.getElementById(chartConfig.id);
                if (!canvas) return;

                const ctx = canvas.getContext('2d');
                const options = this.getChartOptions(chartConfig.type);

                this.charts[chartConfig.id] = new Chart(ctx, {
                    type: chartConfig.type,
                    data: chartConfig.data,
                    options: options
                });
            } catch (error) {
                // Error silencioso para gráficos individuales
            }
        });
    }

    /**
     * CONFIGURACIÓN DE OPCIONES POR TIPO DE GRÁFICO
     * @param {string} type - Tipo de gráfico (bar, doughnut, pie)
     * @returns {object} Opciones de configuración específicas
     */
    getChartOptions(type) {
        const baseOptions = {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: {
                        color: '#c8d6e5',
                        font: {
                            size: 11,
                            family: "'Segoe UI', Tahoma, Geneva, Verdana, sans-serif"
                        },
                        padding: 15,
                        usePointStyle: true,
                        pointStyle: 'circle'
                    }
                },
                tooltip: {
                    backgroundColor: 'rgba(30, 40, 50, 0.95)',
                    titleColor: '#5cc87b',
                    bodyColor: '#c8d6e5',
                    borderColor: 'rgba(92, 200, 123, 0.3)',
                    borderWidth: 1,
                    cornerRadius: 6,
                    padding: 12,
                    displayColors: true,
                    callbacks: {
                        label: function (context) {
                            let label = context.dataset.label || '';
                            if (label) {
                                label += ': ';
                            }
                            if (context.parsed.y !== undefined) {
                                label += new Intl.NumberFormat('es-MX', {
                                    style: 'currency',
                                    currency: 'MXN'
                                }).format(context.parsed.y);
                            } else if (context.parsed !== undefined) {
                                label += context.parsed;
                            }
                            return label;
                        }
                    }
                }
            }
        };

        // CONFIGURACIÓN ESPECÍFICA PARA GRÁFICOS DE BARRAS
        if (type === 'bar') {
            return {
                ...baseOptions,
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            color: '#a0aec0',
                            font: { size: 10 },
                            callback: function (value) {
                                return new Intl.NumberFormat('es-MX', {
                                    style: 'currency',
                                    currency: 'MXN',
                                    notation: 'compact'
                                }).format(value);
                            }
                        },
                        grid: {
                            color: 'rgba(255, 255, 255, 0.08)',
                            drawBorder: false
                        }
                    },
                    x: {
                        ticks: {
                            color: '#a0aec0',
                            font: { size: 10 }
                        },
                        grid: {
                            color: 'rgba(255, 255, 255, 0.08)',
                            drawBorder: false
                        }
                    }
                }
            };
        }

        // CONFIGURACIÓN ESPECÍFICA PARA GRÁFICOS CIRCULARES
        if (type === 'doughnut' || type === 'pie') {
            const circularOptions = {
                ...baseOptions,
                plugins: {
                    ...baseOptions.plugins,
                    tooltip: {
                        ...baseOptions.plugins.tooltip,
                        callbacks: {
                            label: function (context) {
                                const label = context.label || '';
                                const value = context.raw || 0;
                                const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                const percentage = Math.round((value / total) * 100);
                                return `${label}: ${value} (${percentage}%)`;
                            }
                        }
                    }
                },
                elements: {
                    arc: {
                        borderWidth: 2,
                        borderColor: 'rgba(30, 40, 50, 0.8)'
                    }
                }
            };

            if (type === 'doughnut') {
                circularOptions.cutout = '65%';
            }

            return circularOptions;
        }

        return baseOptions;
    }

    //Configura listeners para refresh, resize y temas
    bindEvents() {
        document.addEventListener('click', (e) => {
            if (e.target.id === 'refreshCharts' || e.target.closest('#refreshCharts')) {
                this.refreshCharts();
            }
        });

        window.addEventListener('resize', () => {
            this.resizeCharts();
        });

        window.addEventListener('load', () => {
            setTimeout(() => this.resizeCharts(), 100);
        });
    }

    //Destruye y recrea todos los gráficos con datos actualizados

    refreshCharts() {
        const btn = document.getElementById('refreshCharts');
        if (btn) {
            const originalHTML = btn.innerHTML;
            btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Actualizando...';
            btn.disabled = true;

            const chartWrappers = document.querySelectorAll('.chart-wrapper');
            chartWrappers.forEach(wrapper => {
                wrapper.style.opacity = '0.5';
                wrapper.style.transform = 'scale(0.98)';
            });

            Object.values(this.charts).forEach(chart => {
                if (chart && typeof chart.destroy === 'function') {
                    chart.destroy();
                }
            });

            this.charts = {};

            setTimeout(() => {
                this.loadCharts();
                btn.innerHTML = originalHTML;
                btn.disabled = false;

                chartWrappers.forEach(wrapper => {
                    wrapper.style.opacity = '1';
                    wrapper.style.transform = 'scale(1)';
                });
            }, 1500);
        }
    }

    // Llama al método resize de cada gráfico al cambiar ventana
    resizeCharts() {
        Object.values(this.charts).forEach(chart => {
            if (chart && typeof chart.resize === 'function') {
                chart.resize();
            }
        });
    }

    //Muestra interfaz de error cuando no se pueden cargar gráficos
    renderFallbackCharts() {
        const container = document.getElementById('chartsContainer');
        if (!container) return;

        container.innerHTML = `
            <div class="chart-error">
                <i class="fas fa-exclamation-triangle fa-2x text-warning mb-3"></i>
                <h5 class="text-warning mb-2">Sistema de gráficas no disponible</h5>
                <p class="text-muted mb-3">No se pudieron cargar los datos de las gráficas.</p>
                <button class="chart-refresh-btn" onclick="window.dashboardCharts?.refreshCharts()">
                    <i class="fas fa-redo"></i> Reintentar
                </button>
            </div>
        `;
    }

    /*ACTUALIZACIÓN DINÁMICA DE DATOS DE UN GRÁFICO*/
    updateChartData(chartId, newData) {
        if (this.charts[chartId]) {
            this.charts[chartId].data = newData;
            this.charts[chartId].update('active');
        }
    }

    //CAMBIO DINÁMICO DE TIPO DE GRÁFICO

    changeChartType(chartId, newType) {
        if (this.charts[chartId]) {
            const chart = this.charts[chartId];
            chart.config.type = newType;
            chart.options = this.getChartOptions(newType);
            chart.update();
        }
    }
}

// ============================================================
// FUNCIONES GLOBALES Y UTILIDADES
// CARGA ASÍNCRONA DE DATOS DEL DASHBOARD
// Realiza petición fetch para obtener datos actualizados del servidor

window.loadDashboardData = async function () {
    try {
        const response = await fetch('/api/Reportes/GetDashboardData');
        if (response.ok) {
            const data = await response.json();
            window.dashboardData = data;

            if (window.dashboardCharts) {
                const userRole = window.dashboardCharts.getCurrentUserRole();
                const chartData = window.dashboardCharts.getRealChartData(userRole);

                chartData.charts.forEach(chartConfig => {
                    if (window.dashboardCharts.charts[chartConfig.id]) {
                        window.dashboardCharts.updateChartData(
                            chartConfig.id,
                            chartConfig.data
                        );
                    }
                });
            }

            return data;
        }
        return null;
    } catch (error) {
        return null;
    }
};

// INICIALIZACIÓN GLOBAL DEL SISTEMA DE GRÁFICAS

document.addEventListener('DOMContentLoaded', function () {
    if (document.getElementById('chartsContainer')) {
        // VERIFICACIÓN DE DEPENDENCIA CHART.JS
        if (typeof Chart === 'undefined') {
            document.getElementById('chartsContainer').innerHTML = `
                <div class="alert alert-danger">
                    <i class="fas fa-exclamation-triangle"></i>
                    Error: Chart.js no se cargó correctamente. Verifica que el script esté incluido.
                </div>
            `;
            return;
        }

        // DETECCIÓN AUTOMÁTICA DE TEMA DEL SISTEMA
        const prefersDarkMode = window.matchMedia('(prefers-color-scheme: dark)').matches;

        // CREACIÓN DE INSTANCIA PRINCIPAL
        window.dashboardCharts = new DashboardCharts();

        // CARGA INICIAL DE DATOS SI NO EXISTEN
        if (!window.dashboardData) {
            window.loadDashboardData().then(data => {
                if (data) {
                    const userRole = window.dashboardCharts.getCurrentUserRole();
                    const chartData = window.dashboardCharts.getRealChartData(userRole);
                    window.dashboardCharts.initializeCharts(chartData);
                }
            });
        }

        // DETECCIÓN DE CAMBIOS DE TEMA EN TIEMPO REAL
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', e => {
            window.dashboardCharts.refreshCharts();
        });
    }
});

// MÉTODOS DE DEPURACIÓN Y DIAGNÓSTICO

window.debugCharts = function () {
    return {
        instance: window.dashboardCharts,
        activeCharts: Object.keys(window.dashboardCharts?.charts || {}),
        dashboardData: window.dashboardData
    };
};

window.exportChartsData = function () {
    return {
        timestamp: new Date().toISOString(),
        dashboardData: window.dashboardData,
        activeCharts: Object.keys(window.dashboardCharts?.charts || {})
    };
};