using Discord;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using VTuberNotifier.Notification;
using VTuberNotifier.Watcher.Event;
using VTuberNotifier.Watcher.Feed;

namespace VTuberNotifier.Controllers
{
    [Route("api/youtube")]
    [ApiController]
    public class YouTubeController : ControllerBase
    {
        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> NotificationCallback()
        {
            var b = Request.Query.TryGetValue("hub.challenge", out var value);
            if (b)
            {
                var s = value.ToString();
                await LocalConsole.Log(this, new(LogSeverity.Info, "GET", $"Accept registration. [{s}]"));
                return Ok(s);
            }
            else
            {
                var sr = new StreamReader(Request.Body);
                var xml = await sr.ReadToEndAsync();
                await LocalConsole.Log(this, new(LogSeverity.Info, "POST", "Recieved xml."));
                await DataManager.Instance.StringSaveAsync($"xml/{DateTime.Now:yyyyMMddHHmmssff}", ".xml", xml);
                if (!string.IsNullOrEmpty(xml)) await ReadFeed(xml);
                return Ok();
            }
        }

        public async Task ReadFeed(string xml)
        {
            var doc = XDocument.Parse(xml);
            XNamespace xmlns = doc.Root.Attribute("xmlns").Value;
            var at = doc.Root.Attribute(XNamespace.Xmlns + "yt");
            if (at == null) return;
            XNamespace ns = at.Value;
            var entry = doc.Root.Element(xmlns + "entry");
            var id = entry.Element(ns + "videoId").Value.Trim();
            if(!YouTubeFeed.Instance.CheckNewLive(id, out var video)) return;

            YouTubeEvent evt;
            if (video.Mode == YouTubeItem.YouTubeMode.Live) evt = new YouTubeNewLiveEvent(video);
            else if (video.Mode == YouTubeItem.YouTubeMode.Premire) evt = new YouTubeNewPremireEvent(video);
            else evt = new YouTubeNewVideoEvent(video);
            await NotifyEvent.Notify(evt);
        }
    }
}
