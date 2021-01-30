using Discord.Commands;
using System;
using System.Threading.Tasks;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher;
using VTuberNotifier.Watcher.Feed;
using VTuberNotifier.Watcher.Store;

namespace VTuberNotifier.Discord
{
    public class CmdService : CmdBase
    {
        [Command("vinfo add")]
        public async Task Service(string liver, string services)
        {
            var search = liver.Split('=');
            LiverDetail detail;
            if (search.Length > 2) for (int i = 2; i < search.Length; i++) search[1] += '=' + search[i];

            if (search.Length == 1) detail = LiverData.GetLiverFromNameMatch(search[0]);
            else if (search[0] == "name") detail = LiverData.GetLiverFromNameMatch(search[1]);
            else if (search[0] == "youtube") detail = LiverData.GetLiverFromYouTubeId(search[1]);
            else if (search[0] == "twitter") detail = LiverData.GetLiverFromTwitterId(search[1]);
            else
            {
                await SendError(this, 3, "The specified river cannot be found.");
                return;
            }

            if (detail == null)
            {
                await SendError(this, 3, "The specified river cannot be found.");
                return;
            }
            var sers = services.Split(',');
            var ch = new DiscordChannel(Context.Guild.Id, Context.Channel.Id);
            foreach (var s in sers)
            {
                Type type;
                if (s == "youtube") type = typeof(YouTubeItem);
                else if (s == "twitter") type = typeof(Tweet);
                else if (s == "booth") type = typeof(BoothProduct);
                else if (s == "store")
                {
                    if(detail.Group == LiverGroup.Nijiasnji) type = typeof(NijisanjiProduct);
                    else
                    {
                        await SendError(this, 4, "Store of this liver's group is not supported.");
                        return;
                    }
                }
                else
                {
                    await SendError(this, 4, "This service is not supported.");
                    return;
                }
                ch.SetContent(type, null);
            }
            DiscordNotify.AddNotifyList(detail, ch);
            await ReplyAsync("Success.");
        }
    }
}
