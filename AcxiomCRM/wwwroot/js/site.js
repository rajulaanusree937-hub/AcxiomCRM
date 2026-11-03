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
