using System.Collections.Generic;
using System.Net;
using System.Text.Json.Serialization;

namespace VTuberNotifier.Controllers
{
    public class WebhookRequest
    {
        public string Url { get; set; }
        public string Liver { get; set; }
        public List<ServiceContent> Services { get; set; }

        public struct ServiceContent
        {
            public string Service { get; set; }
            public bool Only { get; set; }
            public string Content { get; set; }
        }
    }

    public class AddressRequest
    {
        public int? Id { get; set; }
        public string Name { get; set; }
        public string GroupId { get; set; }
        public string YouTubeId { get; set; }
        public string TwitterId { get; set; }
    }

    public class ApiResponse
    {
        public int Code { get; }
        public string Message { get; }
        [JsonIgnore]
        public bool IsSuccess { get { return Code < 400; } }

        public ApiResponse(HttpStatusCode code, string msg) : this((int)code, msg) { }
        public ApiResponse(int code, string msg)
        {
            Code = code;
            Message = msg;
        }
    }
}
