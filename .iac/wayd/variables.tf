variable "location" {
  type        = string
  description = "The location/region where the resource group and most resources should exist."
  default     = "westus3"
}

variable "environment" {
  type        = string
  description = "Deployment environment name (e.g. dev, staging, prod). Used for tagging."
  default     = "dev"
}

variable "project" {
  type        = string
  description = "Project name used for tagging resources."
  default     = "wayd"
}

variable "sql_admin_pass" {
  type        = string
  description = "The password for the SQL Server administrator."
  sensitive   = true
}

variable "aad_api_client_secret" {
  type        = string
  description = "The client secret for the AAD API app registration."
  sensitive   = true
}

variable "app_reg_client_id" {
  type        = string
  description = "The client ID for the AAD client-side (frontend) app registration."
}

variable "api_app_reg_client_id" {
  type        = string
  description = "The client ID for the AAD API (backend) app registration."
}

variable "app_reg_api_scope" {
  type        = string
  description = "The full API scope URI for the AAD API app registration (e.g. api://<client-id>/access_as_user)."
}

variable "aad_tenant_id" {
  type        = string
  description = "The Azure AD tenant ID."
}

variable "allowed_entra_tenant_ids" {
  type        = list(string)
  description = "Entra tenant IDs allowed to authenticate via /api/auth/exchange. Defaults to the single app tenant; add more to onboard external orgs."
  default     = null
}

variable "entra_enabled" {
  type        = bool
  description = "Enable the Microsoft Entra token-exchange endpoint. Local-only deployments leave this false — the API boots without Entra config and the login page hides the Microsoft button."
  default     = false
}

variable "aad_domain" {
  type        = string
  description = "The Azure AD tenant primary domain (e.g. contoso.onmicrosoft.com)."
}

variable "sql_ad_admin_object_id" {
  type        = string
  description = "The Azure AD object ID of the group or user configured as the SQL Server AD administrator."
}

variable "local_jwt_secret" {
  type        = string
  description = "The secret key for signing local JWT tokens. Must be at least 32 characters."
  sensitive   = true
}

# IMPORTANT: data-encryption key — NOT a signing key.
# This key encrypts connector credentials (PATs, API keys) at rest in the
# Connections table. If you lose or rotate it, every encrypted connector secret
# becomes unrecoverable and customers must re-enter their credentials. Treat as
# write-once for the lifetime of the data.
# Generate once with: openssl rand -base64 32
variable "dataprotection_master_key" {
  type        = string
  description = "Base64-encoded 256-bit AES key for at-rest secret encryption (SecuritySettings:DataProtection:MasterKey). Distinct from local_jwt_secret per NIST SP 800-57 §5.2."
  sensitive   = true

  validation {
    # Base64 encoding of 32 raw bytes is always 44 chars (including the '=' pad).
    condition     = length(var.dataprotection_master_key) == 44
    error_message = "dataprotection_master_key must be base64-encoded 32 bytes (44 characters including padding). Generate with: openssl rand -base64 32."
  }
}

variable "local_jwt_token_expiration_minutes" {
  type        = number
  description = "The expiration time in minutes for local JWT tokens."
  default     = 60
}

variable "local_jwt_refresh_token_expiration_days" {
  type        = number
  description = "The expiration time in days for local JWT refresh tokens."
  default     = 7
}

variable "docker_tag" {
  type        = string
  description = "The tag for the Docker images."
  default     = "latest"
}

variable "docker_image_registry" {
  type        = string
  description = "The container registry host (e.g. 'docker.io/awaldow', 'ghcr.io/myorg', 'myregistry.azurecr.io'). Do not include a trailing slash."
  default     = "docker.io/awaldow"
}

variable "api_image_name" {
  type        = string
  description = "The repository name of the API image within the registry (e.g. 'moda-api')."
  default     = "moda-api"
}

variable "client_image_name" {
  type        = string
  description = "The repository name of the client image within the registry (e.g. 'moda-client')."
  default     = "moda-client"
}

variable "sql_ad_admin_login_username" {
  type        = string
  description = "The display name used for the Azure AD administrator on the SQL Server."
  default     = "Wayd SQL Admins"
}

variable "sql_admin_login" {
  type        = string
  description = "The SQL Server administrator login name (non-AD). Must not collide with reserved names or match common SQL keywords."
  default     = "waydadmin"
}

variable "allow_azure_services_sql_access" {
  type        = bool
  description = "Whether to create a SQL firewall rule allowing all Azure services (0.0.0.0-0.0.0.0) to connect. Useful for dev where the container apps need access; consider disabling for prod in favor of VNet integration."
  default     = true
}

variable "container_app_cpu" {
  type        = number
  description = "vCPU allocation per container app replica (0.25, 0.5, 0.75, 1.0, ...)."
  default     = 0.25
}

variable "container_app_memory" {
  type        = string
  description = "Memory allocation per container app replica (e.g. '0.5Gi', '1Gi', '2Gi'). Must be paired with a compatible CPU value per Azure Container Apps rules."
  default     = "0.5Gi"
}

variable "container_app_min_replicas" {
  type        = number
  description = "Minimum replicas per container app. Set to 0 for scale-to-zero (free when idle, cold starts on wake)."
  default     = 0
}

variable "container_app_max_replicas" {
  type        = number
  description = "Maximum replicas per container app."
  default     = 3
}

variable "sql_sku_name" {
  type        = string
  description = "SKU name for the Azure SQL database (e.g. 'GP_S_Gen5_1' serverless, 'GP_Gen5_2' provisioned, 'S0', 'P1')."
  default     = "GP_S_Gen5_1"
}

variable "sql_max_size_gb" {
  type        = number
  description = "Maximum size in GB for the Azure SQL database."
  default     = 10
}

variable "sql_min_capacity" {
  type        = number
  description = "Minimum vCores for serverless SQL databases. Ignored for non-serverless SKUs."
  default     = 0.5
}

variable "sql_auto_pause_delay_in_minutes" {
  type        = number
  description = "Time in minutes of inactivity before a serverless SQL database auto-pauses. -1 to disable, requires minutes >= 60 when enabled. Ignored for non-serverless SKUs."
  default     = 60
}

variable "log_analytics_sku" {
  type        = string
  description = "SKU name for the Log Analytics workspace."
  default     = "PerGB2018"
}

variable "log_analytics_retention_in_days" {
  type        = number
  description = "Retention period for Log Analytics workspace data, in days (30-730). Data beyond the 31-day free tier incurs additional cost."
  default     = 30
}

variable "client_url" {
  type        = string
  description = "Additional client URL to allow in CORS for the API (e.g. a custom domain). Leave empty if the container app's default hostname is the only allowed client."
  default     = ""
}

variable "signalr_name" {
  type        = string
  description = "Optional override for the Azure SignalR Service instance name. Leave empty to use the default 'sigr-<project>-<environment>'."
  default     = ""
}

variable "signalr_sku" {
  type        = string
  description = "The SKU name for the Azure SignalR Service (e.g. 'Free_F1', 'Standard_S1')."
  default     = "Free_F1"
}

variable "signalr_capacity" {
  type        = number
  description = "Capacity units for the Azure SignalR Service."
  default     = 1
}
