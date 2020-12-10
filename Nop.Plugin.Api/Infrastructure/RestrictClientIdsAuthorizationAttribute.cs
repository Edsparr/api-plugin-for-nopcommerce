using System.Linq;
using System.Security.Claims;
using IdentityServer4.EntityFramework.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core.Infrastructure;
using Nop.Plugin.Api.Domain;
using Nop.Plugin.Api.Services;

namespace Nop.Plugin.Api.Infrastructure
{
    public class RestrictClientIdsAuthorizationAttribute : TypeFilterAttribute
    {
        public RestrictClientIdsAuthorizationAttribute() : base(typeof(ClaimRequirementFilter))
        {
        }
    }

    public class ClaimRequirementFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var clientService = EngineContext.Current.Resolve<IClientService>();
            var httpContextAccessor = EngineContext.Current.Resolve<IHttpContextAccessor>();
            
            if (clientService.UserHasRestrictedAccess(httpContextAccessor.HttpContext.User))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}