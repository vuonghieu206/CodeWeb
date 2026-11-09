/*
 * Danh sách động bằng AJAX (dùng chung cho trang Quản trị và trang Khách).
 * Cấu trúc trang:
 *   <form id="filter-form" method="get">...</form>
 *   <div id="list-container" data-url="/Sach">  (partial)  </div>
 * - Gõ từ khóa / đổi bộ lọc  -> tải lại phần danh sách, không reload trang
 * - Bấm số trang             -> <a class="page-link" data-page="N">
 * - Xóa bằng AJAX            -> <form class="js-delete-form" data-confirm="...">
 */
(function ($) {
    'use strict';

    var token = $('meta[name="csrf-token"]').attr('content');
    if (token) {
        // Mọi request AJAX đều gửi anti-forgery token trong header (chống CSRF)
        $.ajaxSetup({ headers: { 'RequestVerificationToken': token } });
    }

    $(function () {
        var $box = $('#list-container');
        var $form = $('#filter-form');
        if (!$box.length) return;

        var baseUrl = $box.data('url');
        var current = parseInt($box.data('page'), 10) || 1;
        var timer = null;

        function load(page) {
            current = page || 1;
            var params = $form.length ? $form.serializeArray() : [];
            params.push({ name: 'page', value: current });
            var qs = $.param(params);

            $box.css('opacity', 0.5);
            $.get(baseUrl, qs)
                .done(function (html) {
                    $box.html(html);
                    if (window.history && history.replaceState) {
                        history.replaceState(null, '', baseUrl + '?' + qs);
                    }
                })
                .fail(function () { alert('Không tải được dữ liệu, vui lòng thử lại.'); })
                .always(function () { $box.css('opacity', 1); });
        }

        $form.on('submit', function (e) { e.preventDefault(); load(1); });
        $form.on('input', 'input[type=text], input[type=search]', function () {
            clearTimeout(timer);
            timer = setTimeout(function () { load(1); }, 350);   // debounce
        });
        $form.on('change', 'select', function () { load(1); });

        $box.on('click', 'a.page-link[data-page]', function (e) {
            e.preventDefault();
            load(parseInt($(this).data('page'), 10));
            window.scrollTo({ top: 0, behavior: 'smooth' });
        });

        $box.on('submit', 'form.js-delete-form', function (e) {
            e.preventDefault();
            var f = this;
            if (!confirm($(f).data('confirm') || 'Bạn có chắc chắn muốn xóa?')) return;
            $.post(f.action, $(f).serialize())
                .done(function (r) {
                    if (r && r.success === false) alert(r.message || 'Không thể xóa.');
                    load(current);
                })
                .fail(function () { alert('Xóa thất bại.'); });
        });
    });
})(jQuery);
