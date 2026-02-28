using Microsoft.AspNetCore.Mvc;

namespace User.API.Controllers;
[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{

    [HttpGet(Name = "login")]
    public int Get()
    {
        return -1;
    }
}
