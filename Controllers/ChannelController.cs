using Discord;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Controllers
{
    [Route("api/channel")]
    [ApiController]
    public class ChannelController : ControllerBase
    {
        [HttpGet]
        public ActionResult<LiveChannelDetail> GetChannel(AddressRequest req)
        {
            if (req.Id == null)
            {
                Response.StatusCode = 400;
                return null;
            }

            var id = (int)req.Id;
            var ch = LiveChannel.GetLiveChannelList().FirstOrDefault(c => c.Id == id);
            Response.StatusCode = ch == null ? 404 : 200;
            return ch;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse>> AddChannel(AddressRequest req)
        {
            var res = new ApiResponse(400, "Unknown error has occured.");
            if (string.IsNullOrEmpty(req.Name.Trim()) || string.IsNullOrEmpty(req.YouTubeId.Trim()))
            {
                res = new(400, "\"Name\" or \"YouTubeId\" is not set.");
                return res;
            }

            var code = await LiveChannel.AddLiveChannel(req.Name.Trim(), req.YouTubeId.Trim(), req.TwitterId.Trim());
            if (code == 201)
            {
                res = new(code, "Success to create and add data of the liver.");
                await LocalConsole.Log("ChannelCtl",
                    new LogMessage(LogSeverity.Info, "Add", $"Add data of the liver : {req.Name}."));
            }
            else if (code == 400)
            {
                res = new(code, "Failed to create and add data of the liver.");
                await LocalConsole.Log("ChannelCtl",
                    new LogMessage(LogSeverity.Warning, "Add", $"Failed to add data of the liver : {req.Name}."));
            }
            Response.StatusCode = res.Code;
            return res;
        }

        [HttpPut]
        public async Task<ActionResult<ApiResponse>> UpdateChannel(AddressRequest req)
        {
            var res = new ApiResponse(400, "Unknown error has occured.");
            if (req.Id == null)
            {
                res = new(400, "\"Id\" is not set.");
                return res;
            }

            var id = (int)req.Id;
            var code = await LiveChannel.UpdateLiveChannel(id, req.Name?.Trim(), req.YouTubeId?.Trim(), req.TwitterId?.Trim());
            if (code == 200)
            {
                res = new(code, "Success to update data of the liver.");
                await LocalConsole.Log("ChannelCtl",
                    new LogMessage(LogSeverity.Info, "Update", $"Update data of the liver : {req.Id}."));
            }
            else if (code == 400)
            {
                res = new(code, "Failed to update data of the liver.");
                await LocalConsole.Log("ChannelCtl",
                    new LogMessage(LogSeverity.Warning, "Update", $"Failed to update data of the liver : {req.Id}."));
            }
            else if (code == 404)
            {
                res = new(code, "The specified ID cannot be found.");
                await LocalConsole.Log("ChannelCtl",
                    new LogMessage(LogSeverity.Warning, "Update", $"The specified ID cannot be found : {req.Id}."));
            }
            Response.StatusCode = res.Code;
            return res;
        }

        [HttpDelete]
        public async Task<ActionResult<ApiResponse>> RemoveChannel(AddressRequest req)
        {
            var res = new ApiResponse(400, "Unknown error has occured.");
            if (req.Id == null)
            {
                res = new(400, "\"Id\" is not set.");
                return res;
            }

            var id = (int)req.Id;
            var code = await LiveChannel.DeleteLiveChannel(id);
            if (code == 200)
            {
                res = new(code, "Success to delete data of the liver.");
                await LocalConsole.Log("ChannelCtl",
                    new LogMessage(LogSeverity.Info, "Delete", $"Delete data of the liver : {req.Id}."));
            }
            else if (code == 404)
            {
                res = new(code, "The specified ID cannot be found.");
                await LocalConsole.Log("ChannelCtl",
                    new LogMessage(LogSeverity.Warning, "Delete", $"The specified ID cannot be found : {req.Id}."));
            }
            Response.StatusCode = res.Code;
            return res;
        }
    }
}
