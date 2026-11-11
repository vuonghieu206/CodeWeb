/*
 * Giao diện Khách (jQuery + Bootstrap 5):
 *  - Đăng nhập / Đăng ký bằng hộp thoại (AJAX)
 *  - Thêm vào giỏ mượn (AJAX, chưa đăng nhập thì mở hộp thoại đăng nhập)
 *  - Lọc theo danh mục thể loại, gợi ý tìm kiếm (REST API /api/books)
 *  - Đánh giá 5 sao + bình luận (AJAX, hiện ngay sau khi gửi), phân trang đánh giá
 *  - Đổi mật khẩu trong trang Tài khoản
 * Token chống CSRF được list-ajax.js gắn sẵn vào mọi request AJAX.
 */
(function ($) {
    'use strict';

    function toast(message, ok) {
        var $t = $('<div class="alert shadow-sm mb-2"></div>')
            .addClass(ok ? 'alert-success' : 'alert-warning')
            .text(message);                       // .text() tự escape -> không bị XSS
        $('#toast-box').append($t);
        setTimeout(function () { $t.fadeOut(300, function () { $t.remove(); }); }, 2600);
    }

    function modal(id) {
        var el = document.getElementById(id);
        return el ? bootstrap.Modal.getOrCreateInstance(el) : null;
    }

    function openLogin() { var m = modal('loginModal'); if (m) m.show(); }

    function showAlert($box, text) { $box.text(text).removeClass('d-none'); }

    function showFieldErrors($form, errors) {
        $form.find('[data-err]').text('');
        var globalMsgs = [];
        $.each(errors || {}, function (key, msgs) {
            var $e = $form.find('[data-err="' + key + '"]');
            if ($e.length) $e.text(msgs.join(' ')); else globalMsgs = globalMsgs.concat(msgs);
        });
        return globalMsgs;
    }

    $(function () {

        // ---------- Mở hộp thoại đăng nhập / chuyển qua lại giữa đăng nhập và đăng ký ----------
        $(document).on('click', '.js-require-login, .js-open-login', function (e) {
            e.preventDefault();
            openLogin();
        });
        $(document).on('click', '.js-switch-register', function (e) {
            e.preventDefault(); var l = modal('loginModal'), r = modal('registerModal');
            if (l) l.hide(); if (r) r.show();
        });
        $(document).on('click', '.js-switch-login', function (e) {
            e.preventDefault(); var l = modal('loginModal'), r = modal('registerModal');
            if (r) r.hide(); if (l) l.show();
        });

        // ---------- Đăng nhập ----------
        $('#loginForm').on('submit', function (e) {
            e.preventDefault();
            var $f = $(this), $err = $('#loginError').addClass('d-none');
            var data = $f.serializeArray();
            data.push({ name: 'returnUrl', value: $('meta[name="return-url"]').attr('content') || '/' });
            $f.find('button[type=submit]').prop('disabled', true);
            $.post('/Auth/Login', $.param(data))
                .done(function (r) {
                    if (r.success) { window.location.href = r.redirect || '/'; }
                    else { showAlert($err, r.message || 'Đăng nhập không thành công'); }
                })
                .fail(function (xhr) {
                    showAlert($err, xhr.status === 429 ? 'Bạn thử quá nhiều lần, vui lòng đợi một lát.' : 'Có lỗi xảy ra, vui lòng thử lại.');
                })
                .always(function () { $f.find('button[type=submit]').prop('disabled', false); });
        });

        // ---------- Đăng ký ----------
        $('#registerForm').on('submit', function (e) {
            e.preventDefault();
            var $f = $(this), $err = $('#registerError').addClass('d-none');
            $f.find('[data-err]').text('');
            $f.find('button[type=submit]').prop('disabled', true);
            $.post('/Auth/Register', $f.serialize())
                .done(function (r) {
                    if (r.success) { window.location.href = r.redirect || '/'; return; }
                    var rest = showFieldErrors($f, r.errors);
                    if (rest.length) showAlert($err, rest.join(' '));
                })
                .fail(function (xhr) {
                    showAlert($err, xhr.status === 429 ? 'Bạn thử quá nhiều lần, vui lòng đợi một lát.' : 'Có lỗi xảy ra, vui lòng thử lại.');
                })
                .always(function () { $f.find('button[type=submit]').prop('disabled', false); });
        });

        // ---------- Đổi mật khẩu (trang Tài khoản) ----------
        $('#pwForm').on('submit', function (e) {
            e.preventDefault();
            var $f = $(this), $err = $('#pwError').addClass('d-none');
            $f.find('[data-err]').text('');
            $.post('/KhachHang/DoiMatKhau', $f.serialize())
                .done(function (r) {
                    if (r.success) {
                        var m = modal('pwModal'); if (m) m.hide();
                        $f[0].reset();
                        toast(r.message, true);
                    } else {
                        var rest = showFieldErrors($f, r.errors);
                        if (rest.length) showAlert($err, rest.join(' '));
                    }
                })
                .fail(function () { showAlert($err, 'Có lỗi xảy ra, vui lòng thử lại.'); });
        });

        // ---------- Thêm vào giỏ mượn ----------
        $(document).on('click', '.js-add-cart', function () {
            var $btn = $(this);
            $btn.prop('disabled', true);
            $.post('/KhachHang/AddToCart', { id: $btn.data('id') })
                .done(function (r) {
                    if (r.requireLogin) { toast(r.message, false); openLogin(); return; }
                    if (typeof r.count !== 'undefined') $('#cart-count').text(r.count);
                    toast(r.message, r.success);
                    if (r.success && $btn.closest('#list-container').length === 0) {
                        $btn.html('<i class="bi bi-basket"></i> Sách đã có trong giỏ');   // trang chi tiết: giữ nút bị khóa
                        return;
                    }
                })
                .fail(function () { toast('Có lỗi xảy ra, vui lòng thử lại.', false); })
                .always(function () {
                    // ở trang chi tiết, nếu đã thêm thành công thì không mở khóa lại
                    if ($btn.text().indexOf('đã có trong giỏ') === -1) $btn.prop('disabled', false);
                });
        });

        // ---------- Danh mục thể loại (lọc bằng AJAX, dùng list-ajax.js) ----------
        $('#category-list').on('click', 'a[data-category]', function (e) {
            e.preventDefault();
            var $a = $(this);
            $('#category-list a').removeClass('active');
            $a.addClass('active');
            $('#category-input').val($a.data('category'));
            $('#filter-form').trigger('submit');
        });

        // ---------- Gợi ý tìm kiếm (REST API công khai) ----------
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

        // ---------- Đánh giá sách ----------
        var $reviews = $('#reviews-box');
        if ($reviews.length) {
            var bookId = $reviews.data('book');

            // chọn sao (có hiệu ứng rê chuột)
            $reviews.on('mouseenter', '.star-btn', function () {
                var v = $(this).data('value');
                $(this).parent().children().each(function () { $(this).toggleClass('hover', $(this).data('value') <= v); });
            });
            $reviews.on('mouseleave', '.star-picker', function () { $(this).children().removeClass('hover'); });
            $reviews.on('click', '.star-btn', function () {
                var v = $(this).data('value'), $p = $(this).parent();
                $p.children().each(function () { $(this).toggleClass('on', $(this).data('value') <= v); });
                $('#ratingInput').val(v);
            });

            // gửi đánh giá -> danh sách đánh giá hiện ngay (có thời gian, số sao, bình luận)
            $reviews.on('submit', '#reviewForm', function (e) {
                e.preventDefault();
                var rating = parseInt($('#ratingInput').val(), 10) || 0;
                if (rating < 1) { toast('Vui lòng chọn số sao trước khi gửi.', false); return; }
                var $f = $(this);
                $f.find('button[type=submit]').prop('disabled', true);
                $.post('/KhachHang/AddReview', { bookId: bookId, rating: rating, comment: $f.find('[name=comment]').val() })
                    .done(function (html) { $reviews.html(html); toast('Cảm ơn bạn đã đánh giá!', true); })
                    .fail(function (xhr) {
                        toast(xhr.responseText || 'Không gửi được đánh giá, vui lòng thử lại.', false);
                        $f.find('button[type=submit]').prop('disabled', false);
                    });
            });

            // phân trang đánh giá
            $reviews.on('click', 'a.page-link[data-page]', function (e) {
                e.preventDefault();
                $.get('/KhachHang/Reviews', { bookId: bookId, page: $(this).data('page') })
                    .done(function (html) { $reviews.html(html); });
            });
        }
    });
})(jQuery);
