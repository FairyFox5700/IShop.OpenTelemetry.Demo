
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using UserManagementService.Models;

namespace UserManagementService.GraphQl
{
    public class Query
    {
        [Authorize]
        public async Task<ApplicationUser> GetUserByIdAsync(
            string id,
            [Service] UserManager<ApplicationUser> userManager)
        {
            return await userManager.FindByIdAsync(id);
        }

        [Authorize]
        public async Task<ApplicationUser> GetUserByUsernameAsync(
            string username,
            [Service] UserManager<ApplicationUser> userManager)
        {
            return await userManager.FindByNameAsync(username);
        }

        [Authorize]

        public IQueryable<ApplicationUser> GetAllUsers(
            [Service] UserManager<ApplicationUser> userManager)
        {
            return userManager.Users;
        }
    }
}
