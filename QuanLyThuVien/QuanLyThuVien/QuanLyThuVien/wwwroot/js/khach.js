/* Giao diện Khách: thêm vào giỏ mượn (AJAX) + gợi ý tìm kiếm qua REST API /api/books */
(function ($) {
    'use strict';

    function toast(message, ok) {
        var $t = $('<div class="alert shadow-sm mb-2"></div>')
            .addClass(ok ? 'alert-success' : 'alert-warning')
            .text(message);               // .text() -> tự escape, không XSS
        $('#toast-box').append($t);
        setTimeout(function () { $t.fadeOut(300, function () { $t.remove(); }); }, 2500);
    }

    // Thêm vào giỏ (Session phía server)
    $(document).on('click', '.js-add-cart', function () {
        var $btn = $(this);
        $btn.prop('disabled', true);
        $.post('/KhachHang/AddToCart', { id: $btn.data('id') })
            .done(function (r) {
                if (typeof r.count !== 'undefined') $('#cart-count').text(r.count);
                toast(r.message, r.success);
            })
            .fail(function () { toast('Có lỗi xảy ra, vui lòng thử lại.', false); })
            .always(function () { $btn.prop('disabled', false); });
    });

    // Gợi ý tên sách khi gõ (gọi RESTful API công khai)
    var timer = null;
    $(document).on('input', '#search-input', function () {
        var v = $.trim($(this).val());
        clearTimeout(timer);
        if (v.length < 2) { $('#suggest').empty(); return; }
        timer = setTimeout(function () {
            $.getJSON('/api/books', { q: v, pageSize: 6 }).done(function (res) {
                var $dl = $('#suggest').empty();
                $.each(res.items, function (_, b) { $('<option>').attr('value', b.title).appendTo($dl); });
            });
        }, 250);
    });
})(jQuery);
