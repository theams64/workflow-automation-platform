using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Controllers
{

    [ApiController]
    [Route("test")]
    public sealed class TestController : ControllerBase
    {
        [HttpGet("protected")]
        public IActionResult ProtectedEndpoint()
            => Ok(new { message = "You are authenticated." });
    }
}
