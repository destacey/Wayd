using Azure.Identity;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Wayd.Common.Application.Interfaces;
using Wayd.Integrations.MicrosoftGraph.Model;

namespace Wayd.Integrations.MicrosoftGraph;

public sealed class MicrosoftGraphService : IEntraEmployeeSource
{
    private static readonly string[] _selectOptions = new string[] { "id", "userPrincipalName", "userType", "accountEnabled", "givenName", "surname", "jobTitle", "department", "officeLocation", "mail", "manager", "employeeHireDate" };
    private const int _maxPageSize = 100; // graph api max page size is 999

    private readonly ILogger<MicrosoftGraphService> _logger;

    public MicrosoftGraphService(ILogger<MicrosoftGraphService> logger)
    {
        _logger = logger;
    }

    public async Task<Result<IEnumerable<IExternalEmployee>>> GetEmployees(EntraConnectionCredentials credentials, CancellationToken cancellationToken)
    {
        try
        {
            var graphServiceClient = BuildClient(credentials);

            var users = string.IsNullOrWhiteSpace(credentials.AllUsersGroupObjectId)
                ? await GetActiveDirectoryUsers(graphServiceClient, credentials.IncludeDisabledUsers, cancellationToken)
                : await GetGroupMembers(graphServiceClient, credentials.AllUsersGroupObjectId!, credentials.IncludeDisabledUsers, cancellationToken);

            if (users is null || users.Count == 0)
                return Result.Failure<IEnumerable<IExternalEmployee>>("No employees found in Entra via Microsoft Graph");

            _logger.LogInformation("Found {UserCount} users in Entra tenant {TenantId} via Microsoft Graph", users.Count, credentials.TenantId);

            users = [.. users.Where(u => !string.IsNullOrWhiteSpace(u.Id) && !string.IsNullOrEmpty(u.GivenName) && !string.IsNullOrEmpty(u.Surname))];
            List<AzureAdEmployee> employees = new(users.Count);
            foreach (var user in users)
            {
                employees.Add(new AzureAdEmployee(user));
            }

            _logger.LogInformation("Returning {EmployeeCount} employees from Entra tenant {TenantId} via Microsoft Graph", employees.Count, credentials.TenantId);
            return Result.Success((IEnumerable<IExternalEmployee>)employees);
        }
        catch (Exception ex)
        {
            string message = $"Error getting employees from Entra tenant {credentials.TenantId} via Microsoft Graph";
            _logger.LogError(ex, "{Message}", message);
            return Result.Failure<IEnumerable<IExternalEmployee>>(message);
        }
    }

    private static GraphServiceClient BuildClient(EntraConnectionCredentials credentials)
    {
        var credential = new ClientSecretCredential(credentials.TenantId, credentials.ClientId, credentials.ClientSecret);
        return new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
    }

    private async Task<List<User>> GetGroupMembers(GraphServiceClient graphServiceClient, string groupId, bool includeDisabled, CancellationToken cancellationToken)
    {
        var filter = includeDisabled
            ? "usertype eq 'Member'"
            : "accountEnabled eq true and usertype eq 'Member'";

        var members = await graphServiceClient.Groups[groupId]
            .TransitiveMembers
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Expand = new string[] { "manager($select=id)" };
                requestConfiguration.QueryParameters.Select = _selectOptions;
                requestConfiguration.QueryParameters.Filter = filter;
                requestConfiguration.QueryParameters.Top = _maxPageSize;
            }, cancellationToken);

        List<User> users = [];
        if (members is null || members.Value is null)
        {
            _logger.LogWarning("GetGroupMembers:  No members found for group {GroupId} in Entra via Microsoft Graph", groupId);
            return users;
        }

        var pageIterator = PageIterator<DirectoryObject, DirectoryObjectCollectionResponse>
            .CreatePageIterator(
                graphServiceClient,
                members,
                (d) =>
                {
                    if (d is User user) users.Add(user);
                    return true;
                });

        await pageIterator.IterateAsync(cancellationToken);

        return users;
    }

    private async Task<List<User>> GetActiveDirectoryUsers(GraphServiceClient graphServiceClient, bool includeDisabled, CancellationToken cancellationToken)
    {
        var filter = includeDisabled
            ? "usertype eq 'Member'"
            : "accountEnabled eq true and usertype eq 'Member'";

        //https://docs.microsoft.com/en-us/graph/aad-advanced-queries?tabs=csharp
        var adUsers = await graphServiceClient.Users
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Expand = new string[] { "manager($select=id)" };
                requestConfiguration.QueryParameters.Select = _selectOptions;
                requestConfiguration.QueryParameters.Filter = filter;
                requestConfiguration.QueryParameters.Top = _maxPageSize;
            }, cancellationToken);

        List<User> users = [];
        if (adUsers is null || adUsers.Value is null)
        {
            _logger.LogWarning("GetActiveDirectoryUsers:  No users found in Entra via Microsoft Graph");
            return users;
        }

        var pageIterator = PageIterator<User, UserCollectionResponse>
            .CreatePageIterator(
                graphServiceClient,
                adUsers,
                (u) =>
                {
                    users.Add(u);
                    return true;
                });

        await pageIterator.IterateAsync(cancellationToken);

        return users;
    }
}
