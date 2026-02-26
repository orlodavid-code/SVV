// dashboard.js - VERSIÓN SIMPLIFICADA Y FUNCIONAL

const dashboard = {
    // Función para mostrar la notificación de éxito
    showSuccessAlert: function (message) {
        if (typeof Swal !== 'undefined') {
            Swal.fire({
                toast: true,
                position: 'top-end',
                icon: 'success',
                title: message,
                showConfirmButton: false,
                timer: 3000,
                timerProgressBar: true,
                customClass: {
                    popup: 'swal2-dark-popup'
                }
            });
        } else {
            // Fallback simple si SweetAlert no está disponible
            alert(message);
        }
    },

    // Función para mostrar errores
    showErrorAlert: function (message) {
        if (typeof Swal !== 'undefined') {
            Swal.fire({
                toast: true,
                position: 'top-end',
                icon: 'error',
                title: message,
                showConfirmButton: false,
                timer: 4000,
                timerProgressBar: true,
                customClass: {
                    popup: 'swal2-dark-popup'
                }
            });
        } else {
            alert('Error: ' + message);
        }
    },

    // Función principal para refrescar las estadísticas
    refreshStats: async function () {
        const refreshBtn = document.getElementById('refresh-stats-btn');
        if (!refreshBtn) return;

        // Guardar HTML original
        const originalHtml = refreshBtn.innerHTML;
        const originalText = refreshBtn.textContent.trim();

        // Deshabilitar y mostrar spinner
        refreshBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status"></span> Actualizando...';
        refreshBtn.disabled = true;

        try {
            await new Promise(resolve => setTimeout(resolve, 1500));

            // Mostrar notificación de éxito
            this.showSuccessAlert('Datos actualizados correctamente');

            console.log("Estadísticas actualizadas - Aquí puedes procesar los datos reales");

        } catch (error) {
            console.error("Error al actualizar:", error);
            this.showErrorAlert('Error al actualizar los datos');
        } finally {
            // Restaurar botón siempre
            refreshBtn.innerHTML = '<i class="fas fa-sync-alt me-2"></i> Actualizar';
            refreshBtn.disabled = false;
        }
    },

    // Inicializar
    init: function () {
        const refreshBtn = document.getElementById('refresh-stats-btn');
        if (refreshBtn) {
            refreshBtn.addEventListener('click', (e) => {
                e.preventDefault();
                this.refreshStats();
            });
            console.log('Dashboard inicializado correctamente');
        } else {
            console.warn('Botón de actualizar no encontrado');
        }
    }
};

// Inicializar cuando el DOM esté listo
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => dashboard.init());
} else {
    dashboard.init();
}

// Hacer disponible globalmente
window.dashboard = dashboard;