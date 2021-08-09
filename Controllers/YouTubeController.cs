using Discord;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using VTuberNotifier.Notification;
using VTuberNotifier.Watcher;
using VTuberNotifier.Watcher.Event;

namespace VTuberNotifier.Controllers
{
    [Route("api/youtube")]
    [ApiController]
    public class YouTubeController : ControllerBase
    {
        [HttpGet]
        public IActionResult RegistNotification()
        {
            var b = Request.Query.TryGetValue("hub.challenge", out var value);
            if (b)
            {
                var s = value.ToString();
                LocalConsole.Log("YouTubeCtl", new(LogSeverity.Debug, "GET", $"Accept registration. [{s}]"));
                return Ok(s);
            }
            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> NotificationCallback()
        {
            if (TimerManager.Instance?.TimerCount > 0)
            {
                using var sr = new StreamReader(Request.Body);
                var xml = await sr.ReadToEndAsync();
                LocalConsole.Log("YouTubeCtl", new(LogSeverity.Info, "POST", "Recieved xml."));
                if (!string.IsNullOrEmpty(xml)) await ReadFeed(xml, DateTime.Now);
            }
            return Ok();
        }

        [NonAction]
        public async Task ReadFeed(string xml, DateTime dt)
        {
            var doc = XDocument.Parse(xml);
            XNamespace xmlns = doc.Root.Attribute("xmlns").Value;
            var at = doc.Root.Attribute(XNamespace.Xmlns + "yt");
            if (at == null) return;
            XNamespace ns = at.Value;
            var entry = doc.Root.Element(xmlns + "entry");
            var id = entry.Element(ns + "videoId").Value.Trim();

            var file = $"xml/{dt:yyyyMMdd/HHmmss}_{id}";
            if (YouTubeWatcher.Instance.CheckNewLive(id, out var item))
            {
                file += "(new)";
                await EventNotifier.Instance.Notify(YouTubeNewEvent.CreateEvent(item));
            }
            await DataManager.Instance.StringSaveAsync(file, ".xml", xml);
        }
    }
}