using System.Text.Json;

namespace QuanLyThuVien.Helpers
{
    public static class RequestExtensions
    {
        // Request do jQuery/fetch gửi kèm header này -> trả partial thay vì cả trang
        public static bool IsAjax(this HttpRequest request) =>
            request.Headers["X-Requested-With"] == "XMLHttpRequest";

        public static void SetObject<T>(this ISession session, string key, T value) =>
            session.SetString(key, JsonSerializer.Serialize(value));

        public static T? GetObject<T>(this ISession session, string key)
        {
            var json = session.GetString(key);
            return json == null ? default : JsonSerializer.Deserialize<T>(json);
        }
    }
}
