using HotChocolate.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UserManagementService.Models;

namespace UserManagementService.GraphQl
{
    public class Mutation
    {
        private readonly IConfiguration _configuration;

        public Mutation(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task<IdentityResult> RegisterUserAsync(
            RegisterUserInput input,
            [Service] UserManager<ApplicationUser> userManager)
        {
            var user = new ApplicationUser
            {
                UserName = input.Username,
                Email = input.Email
            };

            var result = await userManager.CreateAsync(user, input.Password);

            return result;
        }

        [Authorize]
        public async Task<IdentityResult> UpdateUserAsync(
            UpdateUserInput input,
            [Service] UserManager<ApplicationUser> userManager)
        {
            var user = await userManager.FindByIdAsync(input.Id);

            if (user == null)
            {
                return IdentityResult.Failed(new IdentityError { Description = "User not found" });
            }

            user.Email = input.Email;
            user.UserName = input.Username;

            return await userManager.UpdateAsync(user);
        }
        public async Task<LoginResult> LoginAsync(
           LoginInput input,
           [Service] UserManager<ApplicationUser> userManager,
           [Service] SignInManager<ApplicationUser> signInManager)
        {
            var user = await userManager.FindByNameAsync(input.Username);
            if (user == null)
            {
                return new LoginResult { Succeeded = false, Token = null, Errors = new[] { "Invalid username or password." } };
            }

            var result = await signInManager.CheckPasswordSignInAsync(user, input.Password, false);
            if (!result.Succeeded)
            {
                return new LoginResult { Succeeded = false, Token = null, Errors = new[] { "Invalid username or password." } };
            }

            var token = GenerateJwtToken(user);
            return new LoginResult { Succeeded = true, Token = token };
        }

        private string GenerateJwtToken(ApplicationUser user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            }),
                Expires = DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["ExpiresInMinutes"])),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
public record LoginInput(string Username, string Password);

public class LoginResult
{
    public bool Succeeded { get; set; }
    public string? Token { get; set; }
    public IEnumerable<string>? Errors { get; set; }
}
public record RegisterUserInput(string Username, string Email, string Password);
public record UpdateUserInput(string Id, string Username, string Email);
