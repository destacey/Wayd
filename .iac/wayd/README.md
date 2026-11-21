# Wayd Infrastructure

Terraform configuration for deploying Wayd to Azure.

The deployed stack is:

- **Resource Group** — container for all resources
- **Azure SQL Server + Database** (serverless, scale-to-zero compatible)
- **Container App Environment + Log Analytics** workspace
- **Container Apps** — API (ASP.NET Core) and client (Next.js), both behind HTTPS ingress
- **Azure SignalR Service** — for real-time features

The configuration is designed to be reusable across organizations and environments by overriding variables. The only file you need to edit directly is the `cloud {}` block in [main.tf](./main.tf) to point at your own Terraform Cloud organization — see [Terraform Cloud](#terraform-cloud) below. Everything else is driven by variables.

## Prerequisites

Before `terraform apply` can succeed you need:

### Terraform Cloud

A [Terraform Cloud](https://app.terraform.io) organization and workspace.

The [`cloud {}` block in `main.tf`](./main.tf) currently points at `organization = "wayd"` with workspace tag `wayd`. **Terraform does not allow variables in the `cloud {}` block**, so if you are deploying under a different organization you must either:

- Change those two literals in `main.tf` to match your organization and tag, or
- Use a different backend (e.g. `azurerm` backend with a storage account) by editing the `terraform {}` block.

### Azure subscription

An Azure subscription with permission to create resource groups, SQL, container apps, log analytics, SignalR, and static web apps. The following Azure resource providers must be registered in the subscription: `Microsoft.App`, `Microsoft.Sql`, `Microsoft.OperationalInsights`, `Microsoft.SignalRService`, `Microsoft.Web`.

### Azure AD app registrations

Two app registrations must exist in your AAD tenant before `terraform apply`:

1. **Client (frontend SPA)** — public client with redirect URIs for the deployed frontend hostname. Collect: client ID.
2. **API (backend)** — exposes an API scope (typically `access_as_user`). Has a client secret. Collect: client ID, tenant ID, primary domain, API scope URI (`api://<client-id>/<scope>`), client secret.

### Azure AD SQL admin group

An AAD group that will be set as the Azure AD administrator on the SQL Server. Members of this group can connect to SQL with their AAD identity (no password). Collect: group display name and object ID.

### Azure service principal (for Terraform)

The Terraform Cloud workspace needs Azure credentials to provision resources. Create a service principal with `Contributor` role on the subscription (or tighter — scope it to the resource group after first apply). Collect: client ID, client secret, subscription ID, tenant ID.

### Azure credential for the deploy job

The workflow's **Deploy** job runs EF Core migrations from a GitHub-hosted runner. Those runners can run on AWS as well as Azure, so the `Allow Azure Services` firewall rule does not let them in. The job signs in to Azure and adds a firewall rule for its own public IP (`gh-run-<run id>-<attempt>`). It removes the rule when the job ends, whether the job passed, failed or was cancelled.

The sign-in uses OIDC (workload identity federation), so GitHub stores no client secret. Set it up once per environment, after the first `terraform apply` has created the SQL server:

```bash
# App registration + service principal for the deploy job
APP_ID=$(az ad app create --display-name "wayd-dev-github-deploy" --query appId -o tsv)
az ad sp create --id "$APP_ID"

# Trust the workflow's tokens for pushes to main
az ad app federated-credential create --id "$APP_ID" --parameters '{
  "name": "github-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<owner>/<repo>:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'

# Least privilege: manage the SQL server (firewall rules included), nothing else
SQL_ID=$(az sql server show -g rg-wayd-dev -n sql-wayd-dev --query id -o tsv)
az role assignment create --assignee "$APP_ID" --role "SQL Server Contributor" --scope "$SQL_ID"
```

Then add these **repository secrets** in GitHub (Settings → Secrets and variables → Actions):

| Secret | Value |
|---|---|
| `AZURE_CLIENT_ID` | `$APP_ID` from above |
| `AZURE_TENANT_ID` | Your Entra tenant ID |
| `AZURE_SUBSCRIPTION_ID` | The subscription holding the SQL server |

Notes:

- In Git Bash on Windows, prefix the last two commands with `MSYS_NO_PATHCONV=1`, or MSYS rewrites the `/subscriptions/...` resource ID into a Windows path. Assigning the role needs Owner or User Access Administrator on the subscription.
- The token's subject must match a federated credential exactly. A `workflow_dispatch` run from a branch other than `main` gets `repo:<owner>/<repo>:ref:refs/heads/<branch>`, so add a credential for that branch to run the deploy from there.
- No secret expires, so nothing needs rotating. To revoke access, delete the federated credential or the role assignment. To recreate the identity, run the commands again and update `AZURE_CLIENT_ID`.
- A run whose runner dies before the cleanup step can leave a `gh-run-*` rule behind. Look for them with `az sql server firewall-rule list -g rg-wayd-dev -s sql-wayd-dev -o table` and delete any whose run has finished.

### Container images

Docker images for the API and client must be published to a registry accessible by the Container App. The default config points at `ghcr.io/destacey/wayd-api` and `ghcr.io/destacey/wayd-client` — override `docker_image_registry`, `api_image_name`, and `client_image_name` to point at your own images.

## Configuration

Required and optional variables are declared in [variables.tf](./variables.tf). The GitHub Actions workflow passes `sql_admin_pass`, `local_jwt_secret`, `dataprotection_master_key`, and `docker_tag` as `-var` flags on every run — you set the rest on the Terraform Cloud workspace.

### Required variables (no default — must be set)

Set these as **Terraform variables** in the TFC workspace:

| Variable                 | Example value | Notes                             |
| ------------------------ | ------------- | --------------------------------- |
| `sql_ad_admin_object_id` | `<guid>`      | AAD group object ID for SQL admin |

Set these as **Terraform variables (sensitive)** in the TFC workspace (or pass them via `-var` flags as the workflow does):

| Variable                    | Notes                                                                                                                                                                                                                                            |
| --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `sql_admin_pass`            | Azure SQL Server admin password                                                                                                                                                                                                                  |
| `local_jwt_secret`          | 32+ character random string used to sign local JWTs                                                                                                                                                                                              |
| `dataprotection_master_key` | Base64-encoded 32 bytes (44 chars) used to encrypt connector credentials at rest. **Effectively write-once** — rotating or losing it makes every stored connector secret unrecoverable. Generate with `openssl rand -base64 32`; store securely. |

Set these as **Environment variables** in the TFC workspace (typically via a shared variable set so multiple workspaces can inherit them):

| Variable | Notes |
|---|---|
| `ARM_CLIENT_ID` | Terraform service principal client ID |
| `ARM_CLIENT_SECRET` | Terraform service principal secret (sensitive) |
| `ARM_SUBSCRIPTION_ID` | Target Azure subscription |
| `ARM_TENANT_ID` | Target AAD tenant |

### Optional variables (defaults provided)

Override these in the TFC workspace only if the defaults don't fit:

| Variable | Default | Notes |
|---|---|---|
| `project` | `wayd` | Used in resource naming and tags |
| `environment` | `dev` | Used in resource naming and tags |
| `location` | `westus3` | Primary Azure region |
| `docker_image_registry` | `ghcr.io/destacey` | Container image registry host |
| `api_image_name` | `wayd-api` | API image repository name |
| `client_image_name` | `wayd-client` | Client image repository name |
| `docker_tag` | `latest` | Image tag (workflow overrides with commit SHA) |
| `container_app_cpu` | `0.25` | vCPU per replica |
| `container_app_memory` | `0.5Gi` | Memory per replica |
| `container_app_min_replicas` | `0` | `0` enables scale-to-zero |
| `container_app_max_replicas` | `3` | |
| `sql_sku_name` | `GP_S_Gen5_1` | Serverless; change to `GP_Gen5_2` for provisioned |
| `sql_max_size_gb` | `10` | |
| `sql_min_capacity` | `0.5` | Serverless only |
| `sql_auto_pause_delay_in_minutes` | `60` | Set `-1` to disable auto-pause |
| `sql_admin_login` | `waydadmin` | SQL Server admin login name |
| `sql_ad_admin_login_username` | `Wayd SQL Admins` | Display name for AAD admin |
| `allow_azure_services_sql_access` | `true` | Creates a `0.0.0.0-0.0.0.0` firewall rule for Azure services access. Disable for prod in favor of VNet integration. |
| `signalr_sku` | `Free_F1` | |
| `signalr_capacity` | `1` | |
| `log_analytics_sku` | `PerGB2018` | |
| `log_analytics_retention_in_days` | `30` | 30 days = free tier; longer incurs cost |
| `client_url` | `""` | Optional additional CORS origin |
| `local_jwt_token_expiration_minutes` | `60` | |
| `local_jwt_refresh_token_expiration_days` | `7` | |

## Resource naming

Most Azure resources are named using the pattern `<prefix>-${project}-${environment}`:

| Resource | Default name (with defaults) | Override via |
|---|---|---|
| Resource group | `rg-wayd-dev` | `project`, `environment` |
| SQL Server | `sql-wayd-dev` | `project`, `environment` |
| SQL Database | `sqldb-wayd-dev` | `project`, `environment` |
| Container App Environment | `cae-wayd-dev` | `project`, `environment` |
| Log Analytics | `la-wayd-dev` | `project`, `environment` |
| Container App (API) | `wayd-api-dev` | `project`, `environment` |
| Container App (client) | `wayd-client-dev` | `project`, `environment` |
| SignalR | `sigr-wayd-dev` | `project`, `environment`, or `signalr_name` to override directly |

## Deploying

The GitHub Actions workflow ([.github/workflows/docker.yml](../../.github/workflows/docker.yml)) handles the full deploy:

1. Build + push container images to GHCR (tagged with the commit SHA)
2. `terraform apply` against the TFC workspace, passing `docker_tag=sha-<commit>`
3. Open the SQL firewall to the runner's IP, run EF Core migrations against the deployed SQL database, then close it again (see [Azure credential for the deploy job](#azure-credential-for-the-deploy-job))

Triggering:

- Automatic on push to `main`
- Manual via **Actions → Docker → Run workflow**

### First-time deploy (new environment)

For a brand-new environment:

1. Create the TFC workspace (tag `wayd`) and set the required variables above
2. Create the two AAD app registrations and collect their IDs
3. Create the AAD SQL admin group and collect its object ID
4. Configure the Terraform service principal on the TFC workspace
5. Trigger the workflow via `workflow_dispatch` against your branch to verify auth
6. Merge to `main` to run the first automatic apply. Its **Deploy** job fails until the deploy credential exists.
7. Create the [deploy job's Azure credential](#azure-credential-for-the-deploy-job) and re-run the Deploy job

### Local development against TF

You typically don't need to run Terraform locally — the workflow handles everything. For local validation:

```bash
cd .iac/wayd
terraform init                  # authenticates to TFC, requires TF_TOKEN env var
terraform validate              # syntax + types
terraform fmt -check -diff      # formatting
terraform plan                  # dry-run; requires variables set on the workspace
```

## Cost estimate

With defaults on `dev` and idle usage, expect **$5–$40/month** for a single environment:

- SQL serverless auto-pauses after 60 min idle (free when paused; ~$0.52/vCore-hour active)
- Container apps scale to zero (free when idle)
- Log Analytics free tier covers first 5 GB/month ingested
- SignalR Free_F1 is always-free
- Resource group itself incurs no charge

Provisioned SQL SKUs, longer Log Analytics retention, or `min_replicas >= 1` will increase the baseline cost.

## Related

- [GitHub Actions workflow](../../.github/workflows/docker.yml) — build + deploy pipeline
- [Migration guide](../../docs/contributing/migrating-from-moda.mdx) — dev onboarding after the Moda → Wayd rename
- [Terraform CLI](https://developer.hashicorp.com/terraform/install)
- [`azurerm` provider docs](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs)
