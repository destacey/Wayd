dotnet ef database update `
    --project ./Wayd.Infrastructure/src/Wayd.Infrastructure.Migrators.MSSQL/Wayd.Infrastructure.Migrators.MSSQL.csproj `
    --startup-project ./Wayd.Web/src/Wayd.Web.Api/Wayd.Web.Api.csproj `
    --connection "Server=localhost,1433;Database=wayd;User Id=sa;Password=Wayd_Dev_P@ssw0rd!;TrustServerCertificate=true"