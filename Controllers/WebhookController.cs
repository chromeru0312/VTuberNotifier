using Discord;
using Microsoft.AspNetCore.Mvc;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using VTuberNotifier.Liver;
using VTuberNotifier.Notification;

namespace VTuberNotifier.Controllers
{
    [Route("api/webhook")]
    [ApiController]
    public class WebhookController : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<WebhookResponse>> AddWebhook(WebhookRequest req)
        {
            var res = GetRequestData(req, out var liver, out var dest);
            Response.StatusCode = res.Code;
            if (res.IsSuccess)
            {
                using var ping = new Ping();
                try
                {
                    var url = req.Url.Replace("http://", "").Replace("https://", "");
                    var reply = ping.Send(url);
                    if (reply.Status == IPStatus.Success)
                    {
                        NotifyEvent.AddWebhookList(liver, dest);
                        await LocalConsole.Log("WebhookCtl", new LogMessage(LogSeverity.Info, "Add", "Add webhook."));
                        return res;
                    }
                }
                catch (PingException) { }
                Response.StatusCode = 400;
                res = new(400, "Unable to connect to the specified URL.");
            }
            await LocalConsole.Log("WebhookCtl", new LogMessage(LogSeverity.Warning, "Add", "Failed to add webhook."));
            return res;
        }

        [HttpPut]
        public async Task<ActionResult<WebhookResponse>> UpdateWebhook(WebhookRequest req)
        {
            var res = GetRequestData(req, out var liver, out var dest);
            if (res.IsSuccess) 
            {
                NotifyEvent.UpdateWebhookList(liver, dest);
                await LocalConsole.Log("WebhookCtl", new LogMessage(LogSeverity.Info, "Update", "Update webhook."));
            }
            else await LocalConsole.Log("WebhookCtl", new LogMessage(LogSeverity.Warning, "Update", "Failed to update webhook."));
            Response.StatusCode = res.Code;
            return res;
        }

        [HttpDelete]
        public async Task<ActionResult<WebhookResponse>> RemoveWebhook(WebhookRequest req)
        {
            var res = GetRequestData(req, out var liver, out var dest);
            if (res.IsSuccess)
            {
                NotifyEvent.RemoveWebhookList(liver, dest);
                await LocalConsole.Log("WebhookCtl", new LogMessage(LogSeverity.Info, "Remove", "Remove webhook."));
            }
            else await LocalConsole.Log("WebhookCtl", new LogMessage(LogSeverity.Warning, "Remove", "Failed to remove webhook."));
            Response.StatusCode = res.Code;
            return res;
        }

        private static WebhookResponse GetRequestData(WebhookRequest req, out LiverDetail liver, out WebhookDestination dest)
        {
            dest = new WebhookDestination(req.Url);
            if (!LiverData.DetectLiver(req.Liver, out liver)) return new(404, "The specified river cannot be found.");
            foreach (var content in req.Services)
            {
                if (!NotifyEvent.DetectType(liver, out var type, content.Service)) return new(404, "This service is not supported/found.");
                dest.AddContent(type, content.Only, content.Content);
            }
            return new WebhookResponse(200, "OK.");
        }
    }
}
