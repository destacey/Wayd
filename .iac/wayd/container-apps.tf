locals {
  sql_conn_string = "Server=tcp:${azurerm_mssql_server.wayd_sql_server.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.wayd_db.name};Persist Security Info=False;User ID=${var.sql_admin_login};Password=${var.sql_admin_pass};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
}

resource "azurerm_container_app_environment" "wayd_cae" {
  name                       = "cae-${local.name_stem}"
  resource_group_name        = azurerm_resource_group.wayd_dev_rg.name
  location                   = azurerm_resource_group.wayd_dev_rg.location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.wayd.id

  tags = local.common_tags
}

resource "azurerm_log_analytics_workspace" "wayd" {
  location            = azurerm_resource_group.wayd_dev_rg.location
  name                = "la-${local.name_stem}"
  resource_group_name = azurerm_resource_group.wayd_dev_rg.name
  sku                 = var.log_analytics_sku
  retention_in_days   = var.log_analytics_retention_in_days

  tags = local.common_tags
}

resource "azurerm_container_app" "wayd_frontend" {
  name                         = "${var.project}-client-${var.environment}"
  container_app_environment_id = azurerm_container_app_environment.wayd_cae.id
  resource_group_name          = azurerm_resource_group.wayd_dev_rg.name
  revision_mode                = "Single"

  ingress {
    external_enabled = true
    target_port      = 3000

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  identity {
    type = "SystemAssigned"
  }

  template {
    min_replicas = var.container_app_min_replicas
    max_replicas = var.container_app_max_replicas

    container {
      name   = "${var.project}-client"
      image  = "${var.docker_image_registry}/${var.client_image_name}:${var.docker_tag}"
      cpu    = var.container_app_cpu
      memory = var.container_app_memory

      readiness_probe {
        port                    = 3000
        transport               = "HTTP"
        path                    = "/api/health"
        timeout                 = 3
        failure_count_threshold = 5
        interval_seconds        = 30
      }

      startup_probe {
        port                    = 3000
        transport               = "HTTP"
        path                    = "/api/health"
        timeout                 = 2
        failure_count_threshold = 5
        interval_seconds        = 10
      }

      env {
        name  = "NEXT_PUBLIC_API_BASE_URL"
        value = "https://${azurerm_container_app.wayd_backend.ingress.0.fqdn}"
      }
    }
  }

  tags = local.common_tags
}

resource "azurerm_container_app" "wayd_backend" {
  name                         = "${var.project}-api-${var.environment}"
  container_app_environment_id = azurerm_container_app_environment.wayd_cae.id
  resource_group_name          = azurerm_resource_group.wayd_dev_rg.name
  revision_mode                = "Single"

  ingress {
    external_enabled = true
    target_port      = 8080

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  secret {
    name  = "sql-conn-string"
    value = local.sql_conn_string
  }

  secret {
    name  = "signalr-conn-string"
    value = azurerm_signalr_service.wayd_signalr.primary_connection_string
  }

  secret {
    name  = "local-jwt-secret"
    value = var.local_jwt_secret
  }

  # WARNING: data-encryption key for connector credentials at rest.
  # Losing or rotating this value renders every stored PAT/API key unrecoverable.
  # See variables.tf for the full caveat.
  secret {
    name  = "dataprotection-master-key"
    value = var.dataprotection_master_key
  }

  identity {
    type = "SystemAssigned"
  }

  template {
    min_replicas = var.container_app_min_replicas
    max_replicas = var.container_app_max_replicas

    container {
      name   = "${var.project}-api"
      image  = "${var.docker_image_registry}/${var.api_image_name}:${var.docker_tag}"
      cpu    = var.container_app_cpu
      memory = var.container_app_memory

      readiness_probe {
        port                    = 8080
        transport               = "HTTP"
        path                    = "/startup"
        timeout                 = 3
        failure_count_threshold = 5
        interval_seconds        = 30
        initial_delay           = 60
      }

      startup_probe {
        port                    = 8080
        transport               = "HTTP"
        path                    = "/startup"
        timeout                 = 2
        failure_count_threshold = 5
        interval_seconds        = 10
        initial_delay           = 60
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }

      env {
        name        = "DatabaseSettings__ConnectionString"
        secret_name = "sql-conn-string"
      }

      env {
        name        = "HangfireSettings__Storage__ConnectionString"
        secret_name = "sql-conn-string"
      }

      env {
        name        = "Azure__SignalR__ConnectionString"
        secret_name = "signalr-conn-string"
      }

      env {
        name        = "SecuritySettings__LocalJwt__Secret"
        secret_name = "local-jwt-secret"
      }

      env {
        name        = "SecuritySettings__DataProtection__MasterKey"
        secret_name = "dataprotection-master-key"
      }

      # Issuer and Audience come from security.json baked into the image.
      # They're JWT claim identifiers — not environment-specific config — and
      # making them per-env variables invited the kind of defaults-drift bug
      # that the URI rename caught.

      env {
        name  = "SecuritySettings__LocalJwt__TokenExpirationInMinutes"
        value = tostring(var.local_jwt_token_expiration_minutes)
      }

      env {
        name  = "SecuritySettings__LocalJwt__RefreshTokenExpirationInDays"
        value = tostring(var.local_jwt_refresh_token_expiration_days)
      }

      env {
        name = "CorsSettings__WebClient"
        # Frontend FQDN computed from the name pattern (matches wayd_frontend definition
        # above) to avoid a wayd_backend -> wayd_frontend -> wayd_backend cycle.
        value = var.client_url != "" ? "https://${var.project}-client-${var.environment}.${azurerm_container_app_environment.wayd_cae.default_domain};${var.client_url}" : "https://${var.project}-client-${var.environment}.${azurerm_container_app_environment.wayd_cae.default_domain}"
      }
    }
  }

  tags = local.common_tags
}
