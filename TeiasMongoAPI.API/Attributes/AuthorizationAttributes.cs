using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.API.Attributes
{
    /// <summary>
    /// Custom authorization attribute that checks for specific permissions
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequirePermissionAttribute : AuthorizeAttribute, IAuthorizationFilter
    {
        private readonly string _permission;

        public RequirePermissionAttribute(string permission)
        {
            _permission = permission;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedObjectResult(ApiResponse<object>.ErrorResponse("Unauthorized", new List<string> { "Authentication required" }));
                return;
            }

            var permissions = context.HttpContext.User.Claims
                .Where(c => c.Type == "permission")
                .Select(c => c.Value)
                .ToList();

            if (!permissions.Contains(_permission))
            {
                context.Result = new ForbiddenObjectResult(ApiResponse<object>.ErrorResponse("Forbidden", new List<string> { $"Permission '{_permission}' is required" }));
            }
        }
    }

    /// <summary>
    /// Custom authorization attribute that checks for specific roles
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequireRoleAttribute : AuthorizeAttribute, IAuthorizationFilter
    {
        private readonly string[] _roles;

        public RequireRoleAttribute(params string[] roles)
        {
            _roles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedObjectResult(ApiResponse<object>.ErrorResponse("Unauthorized", new List<string> { "Authentication required" }));
                return;
            }

            var userRoles = context.HttpContext.User.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            if (!_roles.Any(role => userRoles.Contains(role)))
            {
                var rolesString = string.Join(" or ", _roles);
                context.Result = new ForbiddenObjectResult(ApiResponse<object>.ErrorResponse("Forbidden", new List<string> { $"One of these roles is required: {rolesString}" }));
            }
        }
    }

    /// <summary>
    /// Custom result for forbidden (403) responses
    /// </summary>
    public class ForbiddenObjectResult : ObjectResult
    {
        public ForbiddenObjectResult(object value) : base(value)
        {
            StatusCode = 403;
        }
    }

    /// <summary>
    /// Combined authorization attribute for both roles and permissions
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequireAuthorizationAttribute : AuthorizeAttribute, IAuthorizationFilter
    {
        private readonly string[]? _roles;
        private readonly string[]? _permissions;
        private readonly bool _requireAll;

        public RequireAuthorizationAttribute(string[]? roles = null, string[]? permissions = null, bool requireAll = false)
        {
            _roles = roles;
            _permissions = permissions;
            _requireAll = requireAll;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedObjectResult(ApiResponse<object>.ErrorResponse("Unauthorized", new List<string> { "Authentication required" }));
                return;
            }

            var hasRole = false;
            var hasPermission = false;

            if (_roles != null && _roles.Length > 0)
            {
                var userRoles = context.HttpContext.User.Claims
                    .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                hasRole = _roles.Any(role => userRoles.Contains(role));
            }
            else
            {
                hasRole = true; // No role requirement
            }

            if (_permissions != null && _permissions.Length > 0)
            {
                var userPermissions = context.HttpContext.User.Claims
                    .Where(c => c.Type == "permission")
                    .Select(c => c.Value)
                    .ToList();

                hasPermission = _permissions.Any(permission => userPermissions.Contains(permission));
            }
            else
            {
                hasPermission = true; // No permission requirement
            }

            var authorized = _requireAll ? (hasRole && hasPermission) : (hasRole || hasPermission);

            if (!authorized)
            {
                var message = _requireAll ? "All specified roles and permissions are required" : "At least one of the specified roles or permissions is required";
                context.Result = new ForbiddenObjectResult(ApiResponse<object>.ErrorResponse("Forbidden", new List<string> { message }));
            }
        }
    }
}