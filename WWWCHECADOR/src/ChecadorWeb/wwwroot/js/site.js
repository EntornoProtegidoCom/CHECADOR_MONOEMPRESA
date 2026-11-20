(function () {
    'use strict';

    // Confirmación de acciones con modal accesible: <form data-confirmar="Mensaje">.
    var modalEl = document.getElementById('modalConfirmacion');
    var modal = modalEl && window.bootstrap ? new bootstrap.Modal(modalEl) : null;
    var formularioPendiente = null;

    function formularioValido(form) {
        if (window.jQuery && jQuery(form).data('validator')) {
            return jQuery(form).valid();
        }
        return true;
    }

    function marcarCargando(form) {
        form.dataset.enviando = 'true';
        form.classList.add('cargando');
        form.setAttribute('aria-busy', 'true');
        form.querySelectorAll('button[type="submit"]').forEach(function (b) {
            b.setAttribute('aria-disabled', 'true');
            if (!b.dataset.textoOriginal) {
                b.dataset.textoOriginal = b.textContent;
            }
            b.textContent = b.dataset.textoCargando || 'Procesando…';
        });
    }

    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!(form instanceof HTMLFormElement)) { return; }
        if (form.dataset.enviando === 'true') { e.preventDefault(); return; }
        if (!formularioValido(form)) { return; }

        var mensaje = form.getAttribute('data-confirmar');
        if (mensaje && modal && form.dataset.confirmado !== 'true') {
            e.preventDefault();
            formularioPendiente = form;
            document.getElementById('modalConfirmacionTexto').textContent = mensaje;
            var aceptar = document.getElementById('modalConfirmacionAceptar');
            aceptar.textContent = form.getAttribute('data-confirmar-boton') || 'Confirmar';
            aceptar.className = 'btn ' + (form.getAttribute('data-confirmar-clase') || 'btn-primary');
            modal.show();
            return;
        }
        marcarCargando(form);
    });

    var aceptarBtn = document.getElementById('modalConfirmacionAceptar');
    if (aceptarBtn) {
        aceptarBtn.addEventListener('click', function () {
            if (!formularioPendiente) { return; }
            var form = formularioPendiente;
            formularioPendiente = null;
            form.dataset.confirmado = 'true';
            modal.hide();
            marcarCargando(form);
            form.submit();
        });
    }

    // Filtros que se aplican al cambiar un select.
    document.querySelectorAll('[data-autoenviar]').forEach(function (el) {
        el.addEventListener('change', function () {
            if (el.form) { el.form.requestSubmit ? el.form.requestSubmit() : el.form.submit(); }
        });
    });

    // Restaurar botones si el usuario vuelve con el botón "atrás" del navegador.
    window.addEventListener('pageshow', function (ev) {
        if (!ev.persisted) { return; }
        document.querySelectorAll('form.cargando').forEach(function (f) {
            f.classList.remove('cargando');
            delete f.dataset.enviando;
            f.removeAttribute('aria-busy');
            f.querySelectorAll('button[type="submit"]').forEach(function (b) {
                b.removeAttribute('aria-disabled');
                if (b.dataset.textoOriginal) { b.textContent = b.dataset.textoOriginal; }
            });
        });
    });
})();
