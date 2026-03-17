// admin.js 

document.addEventListener('DOMContentLoaded', function () {
    // DETECCIÓN DE PÁGINA ACTUAL PARA INICIALIZACIONES ESPECÍFICAS
    const isDashboardPage = document.querySelector('.dashboard-container');

    if (isDashboardPage) {
        setupDashboard();
    }

    // INICIALIZACIÓN DE COMPONENTES GLOBALES
    setupVisualEffects();
    setupFormBehavior();
    initializeTooltips();
});

// ============================================================
// SECCIÓN: CONFIGURACIÓN DEL DASHBOARD
function setupDashboard() {
    initializeDashboardData();
    setupDashboardCharts();
    setupDashboardEvents();
    setupDashboardNotifications();
}

/**
 * Inicializa los datos del dashboard desde elementos HTML
 * Parseo seguro de datos JSON y configuración de formateo de moneda
 */
function initializeDashboardData() {
    const dashboardDataElement = document.getElementById('dashboard-data');

    if (dashboardDataElement) {
        try {
            window.dashboardData = JSON.parse(dashboardDataElement.textContent);
        } catch (error) {
            console.error('Error al parsear datos del dashboard:', error);
            window.dashboardData = {};
        }
    } else {
        window.dashboardData = {};
    }

    // CONFIGURACIÓN DE FORMATO DE MONEDA PARA TODO EL DASHBOARD
    window.formatCurrency = function (value) {
        if (value === null || value === undefined) {
            return '$0.00';
        }

        return new Intl.NumberFormat('es-MX', {
            style: 'currency',
            currency: 'MXN',
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(value);
    };

    // APLICACIÓN AUTOMÁTICA DE FORMATO DE MONEDA A CAMPOS ESPECÍFICOS
    if (window.dashboardData) {
        const currencyFields = [
            'TotalAnticiposAutorizados',
            'AnticiposAutorizadosPorUsuario',
            'AnticiposAutorizados',
            'AnticiposTotalesAutorizados'
        ];

        currencyFields.forEach(field => {
            if (window.dashboardData[field] !== undefined) {
                window.dashboardData[field + 'Formatted'] = window.formatCurrency(window.dashboardData[field]);
            }
        });
    }
}

/**
 * Configuración de gráficos del dashboard usando Chart.js
 * Verifica disponibilidad de la librería antes de inicializar
 */
function setupDashboardCharts() {
    if (typeof Chart === 'undefined') {
        return;
    }

    const solicitudesChartCtx = document.getElementById('solicitudesChart');
    if (solicitudesChartCtx) {
        createSolicitudesChart(solicitudesChartCtx);
    }

    const anticiposChartCtx = document.getElementById('anticiposChart');
    if (anticiposChartCtx) {
        createAnticiposChart(anticiposChartCtx);
    }
}

/**
 * Creación de gráfico de dona para estado de solicitudes
 * @param {CanvasRenderingContext2D} ctx - Contexto del canvas
 */
function createSolicitudesChart(ctx) {
    const data = {
        labels: ['Borrador', 'Pendientes', 'Aprobadas', 'Rechazadas'],
        datasets: [{
            label: 'Solicitudes por Estado',
            data: [
                window.dashboardData.MisSolicitudesBorrador || 0,
                window.dashboardData.MisSolicitudesPendientes || 0,
                window.dashboardData.MisSolicitudesAprobadas || 0,
                window.dashboardData.MisSolicitudesRechazadas || 0
            ],
            backgroundColor: [
                'rgba(108, 117, 125, 0.8)',
                'rgba(255, 193, 7, 0.8)',
                'rgba(40, 167, 69, 0.8)',
                'rgba(220, 53, 69, 0.8)'
            ],
            borderColor: [
                '#6c757d',
                '#ffc107',
                '#28a745',
                '#dc3545'
            ],
            borderWidth: 1
        }]
    };

    new Chart(ctx, {
        type: 'doughnut',
        data: data,
        options: {
            responsive: true,
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: {
                        color: '#e2e8f0'
                    }
                }
            }
        }
    });
}

/**
 * Creación de gráfico de línea para anticipos autorizados
 * @param {CanvasRenderingContext2D} ctx - Contexto del canvas
 */
function createAnticiposChart(ctx) {
    const data = {
        labels: ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun'],
        datasets: [{
            label: 'Anticipos Autorizados ($)',
            data: [12000, 19000, 15000, 25000, 22000, 30000],
            backgroundColor: 'rgba(0, 169, 107, 0.2)',
            borderColor: 'rgba(0, 169, 107, 1)',
            borderWidth: 2,
            tension: 0.4,
            fill: true
        }]
    };

    new Chart(ctx, {
        type: 'line',
        data: data,
        options: {
            responsive: true,
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        callback: function (value) {
                            return '$' + value.toLocaleString();
                        },
                        color: '#e2e8f0'
                    },
                    grid: {
                        color: 'rgba(255, 255, 255, 0.1)'
                    }
                },
                x: {
                    ticks: {
                        color: '#e2e8f0'
                    },
                    grid: {
                        color: 'rgba(255, 255, 255, 0.1)'
                    }
                }
            },
            plugins: {
                legend: {
                    labels: {
                        color: '#e2e8f0'
                    }
                }
            }
        }
    });
}

/**
 * Configuración de eventos interactivos del dashboard
 * Manejo de clics en botones y tarjetas estadísticas
 */
function setupDashboardEvents() {
    const botonesProximamente = [
        'btnSubirInformes', 'btnVerDetalle'
    ];

    botonesProximamente.forEach(id => {
        const btn = document.getElementById(id);
        if (btn) {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                showSimpleNotification('Próximamente', 'Esta funcionalidad estará disponible en futuras actualizaciones', 'info');
            });
        }
    });

    const statsCards = document.querySelectorAll('.stats-card');
    statsCards.forEach(card => {
        card.addEventListener('click', function () {
            const title = this.querySelector('p')?.textContent || 'Estadística';
            const value = this.querySelector('h3')?.textContent || '0';
            showSimpleNotification(title, `Valor actual: ${value}`, 'info');
        });
    });
}

// SECCIÓN: NOTIFICACIONES DEL DASHBOARD
function setupDashboardNotifications() {
    // Reservado para futuras implementaciones de notificaciones push
}

/**
 * Muestra notificaciones usando SweetAlert2
 * @param {string} title - Título de la notificación
 * @param {string} message - Mensaje de la notificación
 * @param {string} type - Tipo de notificación (error, warning, success, info)
 */
function showSimpleNotification(title, message, type = 'info') {
    if (typeof Swal === 'undefined') {
        return;
    }

    const config = {
        error: { icon: 'error', timer: 4000, showConfirmButton: true },
        warning: { icon: 'warning', timer: 3000, showConfirmButton: false },
        success: { icon: 'success', timer: 3000, showConfirmButton: false },
        info: { icon: 'info', timer: 3000, showConfirmButton: false }
    }[type] || { icon: 'info', timer: 3000, showConfirmButton: false };

    const alertTitle = {
        'success': 'Éxito',
        'error': 'Error',
        'warning': 'Advertencia',
        'info': 'Información'
    }[type] || 'Notificación';

    Swal.fire({
        icon: config.icon,
        title: alertTitle,
        text: message,
        timer: config.timer,
        timerProgressBar: true,
        showConfirmButton: config.showConfirmButton,
        confirmButtonText: 'OK',
        allowOutsideClick: true,
        allowEscapeKey: true
    });
}

/**
 * Notificación específica para errores de formulario
 * @param {string} message - Mensaje de error
 * @param {string} type - Tipo de notificación
 */
function showFormNotification(message, type = 'error') {
    showSimpleNotification(type === 'error' ? 'Error' : 'Advertencia', message, type);
}
function setupVisualEffects() {
    setupCardHoverEffects();
    setupEntryAnimations();
    setupButtonRipples();
    setupTableRowHover();
}

/**
 * Efectos hover para tarjetas con elevación y sombra
 */
function setupCardHoverEffects() {
    const adminCards = document.querySelectorAll('.admin-card, .solicitud-card, .info-card, .stats-card');

    adminCards.forEach(card => {
        card.addEventListener('mouseenter', () => {
            card.style.transition = 'all 0.3s ease-out';
            card.style.boxShadow = '0 12px 35px rgba(0, 169, 107, 0.15)';
            card.style.transform = 'translateY(-5px)';
        });

        card.addEventListener('mouseleave', () => {
            card.style.boxShadow = '0 8px 25px rgba(0, 0, 0, 0.08)';
            card.style.transform = 'translateY(0)';
        });
    });
}

function setupEntryAnimations() {
    const animateOnScroll = function () {
        const elements = document.querySelectorAll('.admin-card, .admin-header, .dashboard-header, .section-title');
        elements.forEach(element => {
            const elementPosition = element.getBoundingClientRect().top;
            const screenPosition = window.innerHeight / 1.2;

            if (elementPosition < screenPosition) {
                element.style.opacity = '1';
                element.style.transform = 'translateY(0)';
            }
        });
    };

    const animatedElements = document.querySelectorAll('.admin-card, .admin-header, .dashboard-header, .section-title');
    animatedElements.forEach(el => {
        el.style.opacity = '0';
        el.style.transform = 'translateY(20px)';
        el.style.transition = 'opacity 0.6s ease, transform 0.6s ease';
    });

    window.addEventListener('load', animateOnScroll);
    window.addEventListener('scroll', animateOnScroll);
}

/**
 * Efectos ripple en botones para retroalimentación visual
 * Crea ondas concéntricas al hacer clic
 */
function setupButtonRipples() {
    const buttons = document.querySelectorAll('.btn:not([type="submit"]), .quick-action-btn');

    buttons.forEach(button => {
        if (button.dataset.rippleAttached) return;
        button.dataset.rippleAttached = '1';

        button.addEventListener('click', function (e) {
            const ripple = document.createElement('span');
            ripple.className = 'btn-ripple';
            const rect = this.getBoundingClientRect();
            const size = Math.max(rect.width, rect.height);
            const x = e.clientX - rect.left - size / 2;
            const y = e.clientY - rect.top - size / 2;

            ripple.style.width = ripple.style.height = size + 'px';
            ripple.style.left = x + 'px';
            ripple.style.top = y + 'px';
            this.appendChild(ripple);

            setTimeout(() => {
                if (ripple.parentNode) {
                    ripple.remove();
                }
            }, 600);
        });
    });

    // INYECCIÓN DINÁMICA DE ESTILOS PARA EFECTO RIPPLE
    if (!document.getElementById('admin-ripple-styles')) {
        const rippleStyles = `
            .btn-ripple {
                position: absolute;
                border-radius: 50%;
                background-color: rgba(255, 255, 255, 0.7);
                transform: scale(0);
                animation: ripple-animation 0.6s linear;
                pointer-events: none;
            }
            
            @keyframes ripple-animation {
                to {
                    transform: scale(4);
                    opacity: 0;
                }
            }
            
            .btn, .quick-action-btn {
                position: relative;
                overflow: hidden;
            }
        `;

        const styleSheet = document.createElement('style');
        styleSheet.id = 'admin-ripple-styles';
        styleSheet.textContent = rippleStyles;
        document.head.appendChild(styleSheet);
    }
}

/**
 * Efecto hover para filas de tabla con cambio de fondo
 */
function setupTableRowHover() {
    const tableRows = document.querySelectorAll('.table-row-custom, tbody tr');

    tableRows.forEach(row => {
        row.addEventListener('mouseenter', function () {
            this.style.backgroundColor = 'rgba(0, 169, 107, 0.1)';
            this.style.transition = 'background-color 0.3s ease';
        });

        row.addEventListener('mouseleave', function () {
            this.style.backgroundColor = '';
        });
    });
}

// SECCIÓN: COMPORTAMIENTO DE FORMULARIOS
function setupFormBehavior() {
    setupEditFormBehavior();
    setupCreateFormBehavior();
    setupGenericFormBehavior();
}

/**
 * Configuración específica para formularios de edición
 * Maneja validación, estados de botón y prevención de envíos múltiples
 */
function setupEditFormBehavior() {
    const editForm = document.getElementById('editForm');
    if (!editForm) return;

    const submitBtn = document.getElementById('submitBtn');
    if (!submitBtn) return;

    let isSubmitting = false;

    editForm.addEventListener('submit', function (e) {
        if (isSubmitting) {
            e.preventDefault();
            return;
        }

        if (!editForm.checkValidity()) {
            e.preventDefault();
            e.stopPropagation();
            editForm.classList.add('was-validated');
            return;
        }

        isSubmitting = true;
        const originalHTML = submitBtn.innerHTML;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i> Actualizando...';
        submitBtn.disabled = true;

        // TIMEOUT DE SEGURIDAD PARA RESTAURAR EL BOTÓN
        setTimeout(() => {
            if (isSubmitting) {
                isSubmitting = false;
                submitBtn.disabled = false;
                submitBtn.innerHTML = originalHTML;
                showFormNotification('El envío está tomando más tiempo de lo esperado. Por favor intenta nuevamente.', 'warning');
            }
        }, 10000);
    });

    setupRealTimeValidation(editForm);
}

function setupGenericFormBehavior() {
    const forms = document.querySelectorAll('form:not(#editForm, #createForm)');

    forms.forEach(form => {
        if (form.dataset.handlerAttached) return;
        form.dataset.handlerAttached = 'true';

        form.addEventListener('submit', function () {
            const submitBtn = this.querySelector('button[type="submit"], input[type="submit"]');
            if (submitBtn) {
                const originalHTML = submitBtn.innerHTML;
                submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i> Procesando...';
                submitBtn.disabled = true;

                setTimeout(() => {
                    if (submitBtn.disabled) {
                        submitBtn.disabled = false;
                        submitBtn.innerHTML = originalHTML;
                    }
                }, 10000);
            }
        });
    });
}

/**
 * Validación en tiempo real para campos de formulario
 * @param {HTMLFormElement} form - Formulario a validar
 */
function setupRealTimeValidation(form) {
    const inputs = form.querySelectorAll('input, select, textarea');

    inputs.forEach(input => {
        input.addEventListener('blur', function () {
            if (!this.checkValidity()) {
                this.classList.add('is-invalid');
                this.classList.remove('is-valid');
            } else {
                this.classList.remove('is-invalid');
                this.classList.add('is-valid');
            }
        });

        if (input.checkValidity() && input.value.trim() !== '') {
            input.classList.add('is-valid');
        }
    });
}

function setupCreateFormBehavior() {
    const createForm = document.getElementById('createForm');
    if (!createForm) return;

    const submitBtn = document.getElementById('submitBtn');
    if (!submitBtn) return;

    let isSubmitting = false;

    createForm.addEventListener('submit', function (e) {
        if (isSubmitting) {
            e.preventDefault();
            return;
        }

        // VALIDACIÓN ESPECÍFICA DE DOMINIO DE EMAIL
        const emailInput = document.querySelector('input[name="Email"]');
        if (emailInput && !isValidEmailDomain(emailInput.value)) {
            e.preventDefault();
            showFormNotification('El email debe ser del dominio viamtek.com o qvitek.com', 'error');
            emailInput.focus();
            return;
        }

        if (!createForm.checkValidity()) {
            e.preventDefault();
            e.stopPropagation();
            createForm.classList.add('was-validated');
            return;
        }

        isSubmitting = true;
        const originalHTML = submitBtn.innerHTML;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i> Creando...';
        submitBtn.disabled = true;

        setTimeout(() => {
            if (isSubmitting) {
                isSubmitting = false;
                submitBtn.disabled = false;
                submitBtn.innerHTML = originalHTML;
                showFormNotification('El envío está tomando más tiempo de lo esperado. Por favor intenta nuevamente.', 'warning');
            }
        }, 10000);
    });

    const emailInput = document.querySelector('input[name="Email"]');
    if (emailInput) {
        emailInput.addEventListener('blur', function () {
            if (this.value && !isValidEmailDomain(this.value)) {
                this.classList.add('is-invalid');
                this.classList.remove('is-valid');
            } else if (this.value) {
                this.classList.remove('is-invalid');
                this.classList.add('is-valid');
            }
        });
    }

    setupRealTimeValidation(createForm);
}

// SECCIÓN: FUNCIONES UTILITARIAS
/**
 * Valida que el dominio del email sea permitido
 * @param {string} email - Email a validar
 * @returns {boolean} - True si el dominio es válido
 */
function isValidEmailDomain(email) {
    const domainRegex = /@(viamtek\.com|qvitek\.com)$/i;
    return domainRegex.test(email);
}

/**
 * Inicializa tooltips de Bootstrap en elementos con data-bs-toggle="tooltip"
 */
function initializeTooltips() {
    if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }
}

// EXPOSICIÓN DE FUNCIONES AL ÁMBITO GLOBAL

window.adminUtils = {
    formatCurrency: window.formatCurrency || function (value) {
        return new Intl.NumberFormat('es-MX', {
            style: 'currency',
            currency: 'MXN'
        }).format(value || 0);
    },
    showSimpleNotification: showSimpleNotification,
    isValidEmailDomain: isValidEmailDomain
};