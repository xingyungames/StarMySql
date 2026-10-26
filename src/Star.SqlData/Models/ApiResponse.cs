using Newtonsoft.Json;

namespace Star.SqlData.Models
{
    /// <summary>
    /// Unified API response model.
    /// </summary>
    public class ApiResponse
    {
        [JsonProperty("code")]
        public string Code { get; set; } = "200";

        [JsonProperty("msg")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("data")]
        public object? Data { get; set; }

        public ApiResponse() { }

        public ApiResponse(string code, string message, object? data = null)
        {
            Code = code;
            Message = message;
            Data = data;
        }

        public static ApiResponse Success(object? data = null, string message = "Operation succeeded.")
            => new("200", message, data);

        public static ApiResponse Error(string code, string message, object? data = null)
            => new(code, message, data);

        public static ApiResponse NotFound(string message = "Resource not found.")
            => new("404", message);

        public static ApiResponse BadRequest(string message = "Invalid request parameters.")
            => new("400", message);

        public static ApiResponse Unauthorized(string message = "Authentication failed.")
            => new("401", message);

        public static ApiResponse Forbidden(string message = "Insufficient permissions.")
            => new("403", message);

        public static ApiResponse ServerError(string message = "Internal server error.")
            => new("500", message);

        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}