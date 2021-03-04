using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
        public ActionResult<WebhookResponse> PostWebhook(WebhookRequest req)
        {
            if (!LiverData.DetectLiver(req.Liver, out var liver))
                return BadRequest(new WebhookResponse(400, "The specified river cannot be found."));
            var dest = new WebhookDestination(req.Url);
            foreach(var content in req.Services)
            {
                if (!NotifyEvent.DetectType(liver, out var type, content.Service))
                    return BadRequest(new WebhookResponse(400, "This service is not supported/found."));
                dest.AddContent(type, content.Only, content.Content);
            }

            switch (req.ReqestType)
            {
                case "Add":
                    NotifyEvent.AddWebhookList(liver, dest);
                    break;
                case "Update":
                    NotifyEvent.UpdateWebhookList(liver, dest);
                    break;
                case "Remove":
                    NotifyEvent.RemoveWebhookList(liver, dest);
                    break;
                default:
                    return BadRequest(new WebhookResponse(400, "The specified operation cannot be ran."));
            }
            return new WebhookResponse(200, "OK.");
        }
    }
}
