using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using src.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;

namespace src.Controllers;

[ApiController]
[Route("user")]
public class UserController : ControllerBase
{
    private readonly ILogger<UserController> _logger;
    private readonly IConfiguration _config;
    private AppDbContext _context;

    public UserController(ILogger<UserController> logger, IConfiguration config, AppDbContext context)
    {
        _logger = logger;
        _config = config;
        _context = context;
    }
    // USERS ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="reg">User registration data (username, password, repeatpassword).</param>
    /// <response code="201">User Created.</response>
    /// <response code="400">Username already exists or missing elements.</response>
    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterUser reg)
    {
        if (string.IsNullOrWhiteSpace(reg.Username) || string.IsNullOrWhiteSpace(reg.Password) || string.IsNullOrWhiteSpace(reg.RepeatPassword))
            return BadRequest("All elements required.");

        if (_context.Users.Any(u => u.Username == reg.Username))
            return BadRequest("Username already exists.");

        if (reg.Password != reg.RepeatPassword)
            return BadRequest("Passwords don't match.");

        var hash = BCrypt.Net.BCrypt.HashPassword(reg.Password);

        var user = new User
        {
            Username = reg.Username,
            PasswordHash = hash,
            Role = "user"
        };

        _context.Users.Add(user);
        _context.SaveChanges();

        return Created();
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token.
    /// </summary>
    /// <param name="log">Login data (username and password).</param>
    /// <response code="200">User logged in.</response>
    /// <response code="400">Missing username or password.</response>
    /// <response code="401">Invalid username or password.</response>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginUser log)
    {
        if (string.IsNullOrWhiteSpace(log.Username) || string.IsNullOrWhiteSpace(log.Password))
            return BadRequest("Username and password required.");

        var user = _context.Users.SingleOrDefault(u => u.Username == log.Username);

        if (user == null || !BCrypt.Net.BCrypt.Verify(log.Password, user.PasswordHash))
            return Unauthorized("Invalid username or password.");

        var token = GenerateJwtToken(user);

        return Ok(new { token });
    }

    /// <summary>
    /// Creates a new JWT token from old one.
    /// </summary>
    /// <param name="token">Expired JWT token.</param>
    /// <response code="200">New token created.</response>
    /// <response code="400">Invalid token.</response>
    /// <response code="401">Token cannot be refreshed.</response>
    [HttpPost("refresh")]
    public IActionResult Refresh([FromBody] string token)
    {
        var jwtSettings = _config.GetSection("Jwt");
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);

        try
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwtSettings["Audience"],
                ValidateLifetime = false
            };

            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken validatedToken);

            if (validatedToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return BadRequest("Invalid token format.");
            }

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = principal.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
            {
                return BadRequest("Token missing required claims.");
            }

            var user = new User
            {
                Id = Guid.Parse(userId), 
                Username = username
            };

            var newToken = GenerateJwtToken(user);

            return Ok(new { token = newToken });
        }
        catch (Exception ex)
        {
            return Unauthorized("Could not refresh token: " + ex.Message);
        }
    }

    private string GenerateJwtToken(User user)
    {
        var jwt = _config.GetSection("Jwt");

        var claims = new[]
        {
            new Claim("Id", user.Id.ToString()),
            new Claim("Username", user.Username),
            new Claim("Role", "user") 
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwt["Key"])
        );

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: DateTime.Now.AddDays(int.Parse(jwt["ExpiresInDays"])),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Returns username of user with id "id".
    /// </summary>
    /// <param name="id">user id</param>
    /// <response code="200">Returns the username.</response>
    /// <response code="404">User with given id doesn't exist.</response>
    [HttpGet("{id:guid}/username")]
    public IActionResult Username(Guid id)
    {
        var user = _context.Users.Find(id);

        if (user == null)
        {
            return NotFound();
        }

        return Ok(user.Username);
    }

    /// <summary>
    /// Returns a list of [id, username] pairs.
    /// </summary>
    /// <param name="id">List of user ids.</param>
    /// <response code="200">Returns the list.</response>
    [HttpGet("username")]
    public IActionResult UsernameList([FromQuery] Guid[] id)
    {
        if (id == null || id.Length == 0)
        {
            return Ok(new List<object>());
        }

        var idList = id.ToList();

        var users = _context.Users
        .Where(u => idList.Contains(u.Id))
        .Select(u => new[] { u.Id.ToString(), u.Username }) 
        .ToList();

        return Ok(users);
    }

    /*
    // SCANS ---------------------------------------------------------------------------------------------
    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("/{guid:id}/scans")]
    public IActionResult AllScans(Guid id) 
    {
        return Ok();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("/{guid:id}/scans/count")]
    public IActionResult CountScans(Guid id)
    {
        return Ok();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="mountain_NFC"></param>
    /// <returns></returns>
    [HttpPost("/scans")]
    public IActionResult Scans(string mountain_NFC)
    {
        return Ok(); 
    }
    */




    /*
 
GET /users/{id}/scans
Vrne katere scanne je user z id naredil (id, id_gore, timestamp)

GET /users/{id}/scans/count
Vrne koliko scannov je naredil user z id 

POST /user/scans/ AUTH
Prejme v body {user_id: IZ JWT, mountain_NFC: }, pregleda kateri gori pripada
NFC in zapiše scan v tabelo SCANS, pod pogojem da uporabnik ni skeniral zadnjih 24h te gore
     */
}
