// SITE.JS - VERSIÓN DEFINITIVA Y ROBUSTA

console.log("site.js cargado correctamente");

$(function () {
    console.log("Inicializando sistema...");
    mostrarSweetAlerts();
    configurarLogout();      
    configurarPrevencionDobleSubmit();
});

// 1. MOSTRAR NOTIFICACIONES PENDIENTES
function mostrarSweetAlerts() {
    if (typeof Swal === "undefined") {
        console.warn(" SweetAlert2 no está disponible");
        return;
    }

    if (window.sweetSuccess) Swal.fire({ icon: "success", title: "Éxito", text: window.sweetSuccess, timer: 3000 });
    if (window.sweetError) Swal.fire({ icon: "error", title: "Error", text: window.sweetError });
    if (window.sweetWarning) Swal.fire({ icon: "warning", title: "Aviso", text: window.sweetWarning, timer: 4000 });
}

// 2. LOGOUT CON CONFIRMACIÓN Y LIMPIEZA COMPLETA (VERSIÓN CORREGIDA)
function configurarLogout() {
    // Eliminar manejadores anteriores para evitar duplicados
    $(document).off('submit', 'form[action*="Logout"]');

    $(document).on('submit', 'form[action*="Logout"]', function (e) {
        e.preventDefault();
        const form = this;
        const $form = $(form);
        const $button = $form.find('button[type="submit"]');
        const originalText = $button.html();

        $button.prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i> Procesando...');

        Swal.fire({
            title: '¿Cerrar sesión?',
            text: '¿Estás seguro de que quieres salir del sistema?',
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'Sí, salir',
            cancelButtonText: 'Cancelar',
            confirmButtonColor: '#28a745',
            cancelButtonColor: '#dc3545',
            reverseButtons: true
        }).then((result) => {
            // Restaurar botón siempre
            $button.prop('disabled', false).html(originalText);

            if (result.isConfirmed) {
                console.log("Cerrando sesión...");
                try { localStorage.clear(); sessionStorage.clear(); } catch (e) { }
                form.submit(); // Esto recarga la página, fin de la ejecución
            } else {
                console.log(" Logout cancelado");

                // --- CERRAR EL DROPDOWN CORRECTAMENTE CON API DE BOOTSTRAP ---
                const $dropdown = $form.closest('.dropdown');
                if ($dropdown.length) {
                    const $toggle = $dropdown.find('.dropdown-toggle');

                    // 1. Usar API de Bootstrap si está disponible
                    if (typeof bootstrap !== 'undefined' && bootstrap.Dropdown) {
                        try {
                            const dropdown = bootstrap.Dropdown.getInstance($toggle[0]);
                            if (dropdown) {
                                dropdown.hide();
                            } else {
                                new bootstrap.Dropdown($toggle[0]).hide();
                            }
                        } catch (err) {
                            console.warn("Error usando Bootstrap API, usando fallback", err);
                            $dropdown.removeClass('show');
                            $dropdown.find('.dropdown-menu').removeClass('show').css('display', '');
                            $toggle.removeClass('show').attr('aria-expanded', 'false');
                        }
                    } else {
                        // Fallback manual
                        $dropdown.removeClass('show');
                        $dropdown.find('.dropdown-menu').removeClass('show').css('display', '');
                        $toggle.removeClass('show').attr('aria-expanded', 'false');
                    }

                    // 2. Forzar reinicio del atributo data-bs-toggle para que el botón pueda volver a abrir
                    const toggleAttr = $toggle.attr('data-bs-toggle');
                    $toggle.removeAttr('data-bs-toggle');
                    setTimeout(() => {
                        $toggle.attr('data-bs-toggle', toggleAttr);
                    }, 50);
                }

                // --- LIMPIEZA PROFUNDA DE RESIDUOS DE SWEETALERT ---
                $('.swal2-container').remove();                     // Eliminar contenedor
                $('body').removeClass(                               // Quitar clases añadidas
                    'swal2-shown swal2-height-auto swal2-backdrop-show'
                );
                $('[aria-hidden="true"]').removeAttr('aria-hidden'); // Eliminar aria-hidden
                $('header').removeAttr('aria-hidden');               // Específicamente del header

                // Restaurar estilos del body a valores por defecto
                $('body').css({
                    'overflow': '',
                    'pointer-events': ''
                });

                // Quitar foco de cualquier elemento
                if (document.activeElement) {
                    document.activeElement.blur();
                }
                document.body.focus();

                console.log("Limpieza completada. El menú debería funcionar de nuevo.");
            }
        }).catch((error) => {
            console.error("Error en SweetAlert:", error);
            $button.prop('disabled', false).html(originalText);
            // Misma limpieza por seguridad
            $('.swal2-container').remove();
            $('body').removeClass('swal2-shown swal2-height-auto swal2-backdrop-show');
            $('[aria-hidden="true"]').removeAttr('aria-hidden');
            $('header').removeAttr('aria-hidden');
            $('body').css({ 'overflow': '', 'pointer-events': '' });
        });

        return false; // Doble seguridad
    });
}

// 3. PREVENIR DOBLE ENVÍO EN FORMULARIOS (EXCLUYENDO LOGOUT)
function configurarPrevencionDobleSubmit() {
    setTimeout(function () {
        $('form')
            .not('form[action*="Logout"], .dataTables_wrapper form, form[data-no-prevent]')
            .each(function () {
                const $form = $(this);
                $form.on('submit', function () {
                    if ($form.data('submitted')) {
                        console.log(" Doble submit bloqueado");
                        return false;
                    }
                    $form.data('submitted', true);
                    $form.find('button[type="submit"]').prop('disabled', true);
                    setTimeout(() => {
                        $form.data('submitted', false);
                        $form.find('button[type="submit"]').prop('disabled', false);
                    }, 4000);
                });
            });
        console.log("🛡️ Protección doble submit activa");
    }, 400);
}