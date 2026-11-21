output "sql_server_connection_string" {
  value     = local.sql_conn_string
  sensitive = true
}

# The deploy job opens the SQL firewall to its runner's IP by name, so it needs these two.
output "resource_group_name" {
  value = azurerm_resource_group.wayd_dev_rg.name
}

output "sql_server_name" {
  value = azurerm_mssql_server.wayd_sql_server.name
}

output "api_hostname" {
  value = azurerm_container_app.wayd_backend.ingress.0.fqdn
}

output "client_hostname" {
  value = azurerm_container_app.wayd_frontend.ingress.0.fqdn
}

output "signalr_hostname" {
  value = azurerm_signalr_service.wayd_signalr.hostname
}
