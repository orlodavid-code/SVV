using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SVV.Services
{
    public interface IUserService
    {
        int GetCurrentUserId();
        string GetCurrentUserEmail();
        int GetCurrentUserRoleId();
        string GetCurrentUserName();
        bool IsAdmin();
        bool IsGerente();
        bool IsInRole(string roleName);
    }

    public class UserService : IUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int GetCurrentUserId()
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userId, out int id) ? id : 0;
        }

        public string GetCurrentUserEmail()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        }

        public string GetCurrentUserName()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        }

        public int GetCurrentUserRoleId()
        {
            var roleId = _httpContextAccessor.HttpContext?.User?.FindFirstValue("RolId");
            return int.TryParse(roleId, out int id) ? id : 0;
        }

        public bool IsAdmin()
        {
            var roleId = GetCurrentUserRoleId();
            return roleId == 1 || roleId == 6;
        }

        public bool IsGerente()
        {
            var roleId = GetCurrentUserRoleId();
            return roleId == 2 || roleId == 5;
        }

        public bool IsInRole(string roleName)
        {
            return _httpContextAccessor.HttpContext?.User?.IsInRole(roleName) ?? false;
        }
    }
}