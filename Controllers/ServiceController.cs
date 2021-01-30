using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VTuberNotifier.Watcher;

namespace VTuberNotifier.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceController : ControllerBase
    {
        [HttpPost]
        public IActionResult Start()
        {
            if (DataManager.Instance != null) return NoContent();
            DataManager.CreateInstance();
            WatcherTask.CreateInstance();
            return Ok();
        }
    }
}
