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
    public class WebhookResponse
    {
        public int Code { get; }
        public string Message { get; }
        [JsonIgnore]
        public bool IsSuccess { get { return Code < 400; } }

        public WebhookResponse(HttpStatusCode code, string msg) : this((int)code, msg) { }
        public WebhookResponse(int code, string msg)
        {
            Code = code;
            Message = msg;
        }
    }
}
