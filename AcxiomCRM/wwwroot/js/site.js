// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
// ---------- Confirmation dialog for destructive actions (4.1, 12.0) ----------
// Usage in any view:
//   <form asp-action="Delete" method="post" data-confirm="Delete this customer?"> ... </form>
document.addEventListener('submit', function (e) {
    const message = e.target.getAttribute('data-confirm');
    if (message && !window.confirm(message)) {
        e.preventDefault();   // user clicked Cancel -> nothing is sent to the server
    }
});

// ---------- Auto-hide success messages after 5 seconds ----------
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.alert-success').forEach(function (el) {
        setTimeout(function () {
            if (window.bootstrap) {
                bootstrap.Alert.getOrCreateInstance(el).close();
            }
        }, 5000);
    });
});
// ---------- Client-side rule for [NotInPast] (5.1 Date validation) ----------
$(function () {
    if (!window.jQuery || !$.validator || !$.validator.unobtrusive) return;

    $.validator.addMethod('notinpast', function (value, element, params) {
        if (!value) return true;   // [Required] handles empty

        // Conditional: skip when the dependent field has an "inactive" value
        if (params.dependent) {
            const form = $(element).closest('form');
            let dep = form.find('[name="' + params.dependent + '"]').filter(':checkbox, select, input:not([type=hidden])').first();
            if (!dep.length) dep = form.find('[name="' + params.dependent + '"]').first();
            const depVal = (dep.is(':checkbox') ? String(dep.is(':checked')) : String(dep.val() || '')).toLowerCase();
            const inactive = (params.inactive || '').split(',').map(s => s.trim().toLowerCase()).filter(Boolean);
            if (inactive.includes(depVal)) return true;
        }

        const d = new Date(value);
        if (isNaN(d)) return true;
        d.setHours(0, 0, 0, 0);
        const today = new Date(); today.setHours(0, 0, 0, 0);
        return d >= today;
    });

    $.validator.unobtrusive.adapters.add('notinpast', ['dependent', 'inactive'], function (options) {
        options.rules['notinpast'] = options.params;
        options.messages['notinpast'] = options.message;
    });
});


// ---------- Client-side rule for [NotInPast] (5.1 Date validation) ----------
$(function () {
    if (!window.jQuery || !$.validator || !$.validator.unobtrusive) return;

    $.validator.addMethod('notinpast', function (value, element, params) {
        if (!value) return true;   // [Required] handles empty

        // Conditional: skip when the dependent field has an "inactive" value
        if (params.dependent) {
            const form = $(element).closest('form');
            let dep = form.find('[name="' + params.dependent + '"]').filter(':checkbox, select, input:not([type=hidden])').first();
            if (!dep.length) dep = form.find('[name="' + params.dependent + '"]').first();
            const depVal = (dep.is(':checkbox') ? String(dep.is(':checked')) : String(dep.val() || '')).toLowerCase();
            const inactive = (params.inactive || '').split(',').map(s => s.trim().toLowerCase()).filter(Boolean);
            if (inactive.includes(depVal)) return true;
        }

        const d = new Date(value);
        if (isNaN(d)) return true;
        d.setHours(0, 0, 0, 0);
        const today = new Date(); today.setHours(0, 0, 0, 0);
        return d >= today;
    });

    $.validator.unobtrusive.adapters.add('notinpast', ['dependent', 'inactive'], function (options) {
        options.rules['notinpast'] = options.params;
        options.messages['notinpast'] = options.message;
    });
});