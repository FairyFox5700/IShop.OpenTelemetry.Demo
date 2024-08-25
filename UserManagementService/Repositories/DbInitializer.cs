using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using UserManagementService.Models;

namespace UserManagementService.Repositories
{
    public class DbInitializer
    {
        public static async Task InitializeAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            context.Database.Migrate();

            // Seed Users
            if (!userManager.Users.Any())
            {
                var users = LoadSeedData<UserSeedModel>("Data/SeedData/users.json");
                foreach (var userModel in users)
                {
                    var user = new ApplicationUser { UserName = userModel.UserName, Email = userModel.Email };
                    var result = await userManager.CreateAsync(user, userModel.Password);
                    if (result.Succeeded)
                    {
                        await context.SaveChangesAsync();
                    }
                }
            }
        }

        private static List<T> LoadSeedData<T>(string filePath)
        {
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<List<T>>(json);
        }

        private class UserSeedModel
        {
            public string UserName { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
        }
    }
}
