using Discord;
using Microsoft.AspNetCore.Mvc;
using System.Net.NetworkInformation;
using VTuberNotifier.Liver;
using VTuberNotifier.Notification;

namespace VTuberNotifier.Controllers
{
    [Route("api/webhook")]
    [ApiController]
    public class WebhookController : ControllerBase
    {
        [HttpPost]
        public ActionResult<ApiResponse> AddWebhook(WebhookRequest req)
        {
            var res = GetRequestData(req, out var liver, out var dest);
            if (res.IsSuccess)
            {
                using var ping = new Ping();
                try
                {
                    var url = req.Url.Replace("http://", "").Replace("https://", "");
                    var reply = ping.Send(url);
                    if (reply.Status == IPStatus.Success)
                    {
                        EventNotifier.Instance.AddWebhookList(liver, dest);
                        LocalConsole.Log("WebhookCtl", new LogMessage(LogSeverity.Info, "Add", "Add webhook."));
                        return res;
                    }
                }
                catch (PingException) { }
                Response.StatusCode = 400;
                res = new(400, "Unable to connect to the specified URL.");
            }
            LocalConsole.Log("WebhookCtl", new LogMessage(LogSeverity.Warning, "Add", "Failed to add webhook."));
            Response.StatusCode = res.Code;
            return res;
        }

        [HttpPut]
        public ActionResult<ApiResponse> UpdateWebhook(WebhookRequest req)
        {
            var res = GetRequestData(req, out var liver, out var dest);
            if (res.IsSuccess) 
            {
                EventNotifier.Instance.UpdateWebhookList(liver, dest);
                LocalConsole.Log("WebhookCtl", new LogMessage(LogSeverity.Info, "Update", "Update webhook."));
            }
            else LocalConsole.Log("WebhookCtl", new LogMessage(LogSeverity.Warning, "Update", "Failed to update webhook."));
            Response.StatusCode = res.Code;
            return res;
        }

        [HttpDelete]
        public ActionResult<ApiResponse> RemoveWebhook(WebhookRequest req)
        {
            var res = GetRequestData(req, out var liver, out var dest);
            if (res.IsSuccess)
            {
                EventNotifier.Instance.RemoveWebhookList(liver, dest);
                LocalConsole.Log("WebhookCtl", new LogMessage(LogSeverity.Info, "Remove", "Remove webhook."));
            }
            else LocalConsole.Log("WebhookCtl", new LogMessage(LogSeverity.Warning, "Remove", "Failed to remove webhook."));
            Response.StatusCode = res.Code;
            return res;
        }

        [NonAction]
        private static ApiResponse GetRequestData(WebhookRequest req, out LiverDetail liver, out WebhookDestination dest)
        {
            dest = new WebhookDestination(req.Url);
            if (!LiverData.DetectLiver(req.Liver, out liver)) return new(404, "The specified river cannot be found.");
            foreach (var content in req.Services)
            {
                if (!EventNotifier.Instance.DetectType(liver, out var type, content.Service)) return new(404, "This service is not supported/found.");
                dest.AddContent(type, content.Only, content.Content);
            }
            return new ApiResponse(200, "OK.");
        }
    }
}