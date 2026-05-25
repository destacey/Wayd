using Azure.Identity;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Integrations.MicrosoftGraph.Model;

namespace Wayd.Integrations.MicrosoftGraph;

public sealed class MicrosoftGraphService(ILogger<MicrosoftGraphService> logger) : IEntraEmployeeSource
{
    private static readonly string[] _selectOptions = ["id", "userPrincipalName", "userType", "accountEnabled", "givenName", "surname", "jobTitle", "department", "officeLocation", "mail", "manager", "employeeHireDate"];
    private const int MaxPageSize = 100; // graph api max page size is 999

    private readonly ILogger<MicrosoftGraphService> _logger = logger;

    public async Task<Result<IEnumerable<IExternalEmployee>>> GetEmployees(EntraConnectionCredentials credentials, CancellationToken cancellationToken)
    {
        try
        {
            var graphServiceClient = BuildClient(credentials);

            var members = string.IsNullOrWhiteSpace(credentials.AllUsersGroupObjectId)
                ? await GetEntraMembers(graphServiceClient, credentials.IncludeDisabledUsers, cancellationToken)
                : await GetGroupMembers(graphServiceClient, credentials.AllUsersGroupObjectId!, credentials.IncludeDisabledUsers, cancellationToken);

            if (members is null || members.Count == 0)
                return Result.Failure<IEnumerable<IExternalEmployee>>("No employees found in Entra via Microsoft Graph");

            _logger.LogInformation("Found {MemberCount} members in Entra tenant {TenantId} via Microsoft Graph", members.Count, credentials.TenantId);

            members = [.. members.Where(u => !string.IsNullOrWhiteSpace(u.Id) && !string.IsNullOrEmpty(u.GivenName) && !string.IsNullOrEmpty(u.Surname))];
            List<EntraEmployee> employees = new(members.Count);
            foreach (var user in members)
            {
                employees.Add(new EntraEmployee(user));
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
                requestConfiguration.QueryParameters.Expand = ["manager($select=id)"];
                requestConfiguration.QueryParameters.Select = _selectOptions;
                requestConfiguration.QueryParameters.Filter = filter;
                requestConfiguration.QueryParameters.Top = MaxPageSize;
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

    private async Task<List<User>> GetEntraMembers(GraphServiceClient graphServiceClient, bool includeDisabled, CancellationToken cancellationToken)
    {
        var filter = includeDisabled
            ? "usertype eq 'Member'"
            : "accountEnabled eq true and usertype eq 'Member'";

        //https://docs.microsoft.com/en-us/graph/aad-advanced-queries?tabs=csharp
        var members = await graphServiceClient.Users
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Expand = ["manager($select=id)"];
                requestConfiguration.QueryParameters.Select = _selectOptions;
                requestConfiguration.QueryParameters.Filter = filter;
                requestConfiguration.QueryParameters.Top = MaxPageSize;
            }, cancellationToken);

        List<User> users = [];
        if (members is null || members.Value is null)
        {
            _logger.LogWarning("GetEntraMembers:  No members found in Entra via Microsoft Graph");
            return users;
        }

        var pageIterator = PageIterator<User, UserCollectionResponse>
            .CreatePageIterator(
                graphServiceClient,
                members,
                (u) =>
                {
                    users.Add(u);
                    return true;
                });

        await pageIterator.IterateAsync(cancellationToken);

        return users;
    }
}
