using System.Collections.Generic;
using System.Security.Claims;

namespace Nop.Plugin.Api.Services
{
    using Models;

    public interface IClientService
    {
        IList<ClientApiModel> GetAllClients();
        void DeleteClient(int id);
        int InsertClient(ClientApiModel model);
        void UpdateClient(ClientApiModel model);
        ClientApiModel FindClientByIdAsync(int id);
        ClientApiModel FindClientByClientId(string clientId);
        bool UserHasRestrictedAccess(ClaimsPrincipal user);
    }
}