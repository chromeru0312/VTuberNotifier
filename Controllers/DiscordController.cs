using Discord;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using VTuberNotifier.Notification.Discord;

namespace VTuberNotifier.Controllers
{
    [Route("api/discord")]
    [ApiController]
    public class DiscordController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> NotifyAllGuild(SendingContent content)
        {
            const string Hash = "c2dfc22c421080a8e93f8295d24acbcd7242801b436e3d14ed380cded5f5fb79d82e3d7cd4d3a92dd559663d307b934379a6dd9797ca01dfd9fb5b1269ae0c23";
            byte[] data = Encoding.UTF8.GetBytes(content.Key);
            var sha = new SHA512CryptoServiceProvider();
            byte[] bs = sha.ComputeHash(data);
            sha.Clear();
            var res = new StringBuilder();
            foreach (byte b in bs) res.Append(b.ToString("X2"));
            if (res.ToString() != Hash) return Unauthorized();

            var client = SettingData.DiscordClient;
            foreach (var (guild, channel) in DiscordBot.Instance.AllChannels)
            {
                var g = client.GetGuild(guild);
                var ch = g.GetTextChannel(channel);
                await ch.SendMessageAsync(content.Message);
            }
            await LocalConsole.Log("DiscordCtl", new LogMessage(LogSeverity.Info, "Notification", $"Success to notify content to all channels."));
            return Ok();
        }
    }

    public class SendingContent
    {
        public string Key { get; set; }
        public string Message { get; set; }
    }
}
