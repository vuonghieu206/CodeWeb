# Quản lý thư viện – Hướng dẫn chạy và tài liệu chức năng

## 1. Chạy project (làm đúng thứ tự)

Bản này thêm bảng mới (Danh mục, Đánh giá) và cột mới (liên kết tài khoản với độc giả / phiếu mượn), nên **phải tạo migration mới**:

1. Mở `QuanLyThuVien.slnx` bằng Visual Studio (.NET 10).
2. **Xóa các file/thư mục đã bị bỏ** nếu bạn copy đè vào project cũ (copy đè không tự xóa file cũ):
   - `Resources/` (cả thư mục, đã bỏ chức năng đổi ngôn ngữ)
   - `Views/Auth/ChangePassword.cshtml`
   - `Views/KhachHang/TraCuu.cshtml`
   - `Services/Localization/` (nếu có)
3. Mở **Tools → NuGet Package Manager → Package Manager Console**, chọn project `QuanLyThuVien`, chạy:
   ```
   Add-Migration AddCategoryReviewAccountLink
   Update-Database
   ```
   Nếu bỏ qua bước này app sẽ báo lỗi "pending model changes" khi khởi động.
4. Nhấn F5. Trang mở ra là **giao diện Khách**.

Tài khoản mẫu (tự tạo khi bảng tài khoản còn trống):

| Vai trò | Tên đăng nhập | Mật khẩu |
|---|---|---|
| Admin | `admin` | `Admin@123` |
| Độc giả | `docgia` | `Docgia123` |

Nếu database của bạn đã có tài khoản từ trước thì hai tài khoản mẫu không được tạo; hãy đăng ký độc giả trên website và dùng tài khoản Admin sẵn có.
Danh mục được tự tạo từ các thể loại sách đang có (nếu chưa có sách thì tạo 6 danh mục mẫu).

## 2. Chức năng theo vai trò

| | Khách chưa đăng nhập | Độc giả | Thủ thư | Admin |
|---|---|---|---|---|
| Xem sách, danh mục, chi tiết, đánh giá | ✔ | ✔ | ✔ | ✔ |
| Đăng ký tài khoản | ✔ | | | |
| Giỏ mượn, gửi yêu cầu mượn, lịch sử mượn, tài khoản, đánh giá sao + bình luận | ✘ (mở hộp thoại đăng nhập) | ✔ | ✘ | ✘ |
| Quản lý sách, danh mục, độc giả, mượn trả, phạt, đánh giá, báo cáo | ✘ | ✘ | ✔ | ✔ |
| Quản lý tài khoản | ✘ | ✘ | ✘ | ✔ |

### Giao diện Khách
- Banner có ảnh, danh mục thể loại bên trái (bấm để lọc ngay, không tải lại trang), tìm kiếm có gợi ý, sắp xếp, phân trang.
- Nút **Đăng nhập / Đăng ký** trên menu mở hộp thoại (AJAX). Đăng ký gồm họ tên, email, số điện thoại, địa chỉ, tên đăng nhập, mật khẩu.
- Bấm vào tên tài khoản: **Thông tin tài khoản** (sửa thông tin cá nhân, đổi mật khẩu), **Lịch sử mượn**, **Giỏ mượn**, **Đăng xuất**.
- Trang chi tiết sách có phần **đánh giá 5 sao + bình luận**: gửi xong hiện ngay ở đầu danh sách cùng số sao, bình luận và thời gian. Mỗi độc giả có một đánh giá cho mỗi sách (gửi lại sẽ cập nhật).
- **Lịch sử mượn** hiển thị đúng các phiếu của tài khoản đang đăng nhập (có phân trang, lọc trạng thái, hủy yêu cầu đang chờ duyệt).

### Giao diện quản lý
- **Danh mục** (menu mới): thêm/sửa/xóa thể loại. **Không xóa được danh mục đang có sách.** Đổi tên danh mục sẽ đổi theo các sách thuộc danh mục đó.
- **Sách**: ô thể loại chỉ chọn trong danh sách Danh mục (kiểm tra cả ở server và API).
- **Độc giả**: độc giả đăng ký trên website tự xuất hiện ở đây (có nhãn "Có tài khoản"); thủ thư vẫn thêm được độc giả tại quầy.
- **Đánh giá** (menu mới): xem, lọc, xóa đánh giá không phù hợp.
- **Tài khoản**: hiện cả tài khoản độc giả để khóa/xóa; Admin chỉ tạo tài khoản nhân viên.
- **Phân trang** ở dưới mọi danh sách: Sách, Danh mục, Độc giả, Mượn trả, Phạt, Đánh giá, Tài khoản, Thể loại, Trang chủ Khách, Lịch sử mượn, danh sách đánh giá. Thanh số trang luôn hiển thị khi có dữ liệu.

## 3. Đối chiếu yêu cầu đề bài

| Yêu cầu | Đã làm ở đâu |
|---|---|
| Hai loại người dùng, quyền khác nhau | Cookie Authentication + vai trò (`Program.cs`, `AuthController`, `StaffController`) |
| Template + framework front-end | AdminLTE (quản trị), Bootstrap 5 + jQuery (Khách) |
| Paging, Filtering, AJAX | `PaginatedList<T>`, `_Pager`, `list-ajax.js`, `khach.js` |
| Thêm/sửa/xóa | Mọi controller; xóa bằng POST + anti-forgery |
| Session, cookies | Session: giỏ mượn. Cookie: đăng nhập, "sách xem gần đây" |
| Validation (Annotation) | Thuộc tính trong `Models/` và các ViewModel + jQuery validation |
| Web API RESTful | `/api/books`, `/api/readers`, `/api/borrows` |
| Upload file | Ảnh bìa sách (`ImageUploadService`) |
| Editor cho textarea | CKEditor 5 ở ô Mô tả sách |
| Responsive | Bootstrap grid, menu thu gọn, bảng cuộn ngang |
| Bảo mật | Mục 4 |

API:
```
GET    /api/books?q=&category=&page=&pageSize=   (công khai)     GET /api/books/{id} | /api/books/categories
POST/PUT/DELETE /api/books                       (Admin/Thủ thư; danh mục phải có sẵn)
GET/POST/PUT/DELETE /api/readers                 (Admin/Thủ thư)
GET /api/borrows, GET /api/borrows/{id}, POST /api/borrows/{id}/approve|reject|return   (Admin/Thủ thư)
```

## 4. Bảo mật
- Mật khẩu băm PBKDF2; chính sách ≥ 8 ký tự có chữ và số (đăng ký, đổi mật khẩu, tạo tài khoản nhân viên).
- Vai trò của tài khoản đăng ký **luôn do server gán là "Độc giả"**, không nhận từ form → không ai tự đăng ký thành Admin.
- Phân quyền: trang quản trị chỉ Admin/Thủ thư (độc giả bị 403); trang độc giả chỉ vai trò Độc giả; đánh giá và lịch sử luôn lọc theo tài khoản đang đăng nhập (không xem được dữ liệu người khác).
- Chống CSRF (anti-forgery token, gửi qua header với AJAX), chống XSS (Razor encode, bình luận hiển thị dạng văn bản, HTML từ editor được sanitize), upload ảnh kiểm tra nội dung file, giới hạn tần suất đăng nhập/đăng ký/gửi yêu cầu mượn, cookie `HttpOnly` + `SameSite`, header bảo mật, chống Open Redirect.
- Không xóa được sách/độc giả/danh mục đang được sử dụng.

## 5. Kịch bản kiểm thử gợi ý

| # | Việc cần thử | Kết quả mong đợi |
|---|---|---|
| 1 | Mở app (F5) | Hiện giao diện Khách có banner, danh mục thể loại |
| 2 | Bấm "Đăng ký", nhập thiếu/sai dữ liệu | Báo lỗi dưới từng ô; đúng thì tự đăng nhập |
| 3 | Đăng nhập admin → mục Độc giả | Thấy độc giả vừa đăng ký, nhãn "Có tài khoản" |
| 4 | Admin → Danh mục → xóa danh mục đang có sách | Bị chặn, báo số sách đang thuộc danh mục |
| 5 | Admin → thêm sách | Ô thể loại chỉ chọn từ danh sách Danh mục |
| 6 | Khách bấm "Thêm vào giỏ mượn" khi chưa đăng nhập | Mở hộp thoại đăng nhập |
| 7 | Độc giả thêm giỏ → Gửi yêu cầu mượn | Chuyển sang Lịch sử mượn, trạng thái "Chờ duyệt" |
| 8 | Admin duyệt phiếu; độc giả mở Lịch sử mượn | Trạng thái đổi "Đang mượn"; chỉ thấy phiếu của mình |
| 9 | Độc giả đánh giá 4 sao + bình luận ở trang chi tiết | Hiện ngay đầu danh sách, có số sao + bình luận + giờ |
| 10 | Đánh giá lại cùng cuốn | Cập nhật đánh giá cũ, không tạo bản sao |
| 11 | Tên tài khoản → Thông tin tài khoản → sửa và Lưu; Đổi mật khẩu | Lưu được; đổi mật khẩu đúng/sai báo rõ |
| 12 | Độc giả vào `/Sach` hoặc `/TaiKhoan` | 403 Access Denied |
| 13 | Thêm > 8 sách | Có thanh số trang dưới cùng ở trang Khách và quản lý |
| 14 | Admin → Đánh giá → xóa 1 đánh giá | Biến mất ở trang chi tiết sách |
| 15 | Phiếu Đang mượn có hạn trả đã qua | Mở trang Tiền phạt thấy khoản phạt tự xuất hiện (2.000đ/ngày) |

## 6. Lưu ý
- Giao diện hoàn toàn tiếng Việt (đã bỏ chức năng đổi ngôn ngữ). Nếu báo cáo có mục "quốc tế hóa" thì cần bỏ hoặc trình bày là hướng phát triển.
- Mỗi người dùng database riêng trên máy mình (LocalDB), dữ liệu không tự chia sẻ giữa các máy.
- CKEditor, Chart.js và Bootstrap Icons tải từ CDN nên cần Internet khi chạy.
- Phiếu mượn cũ không có `DueDate` sẽ không bị tính phạt tự động.
