FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5150

ENV ASPNETCORE_URLS=http://+:5150

# Creates a non-root user with an explicit UID and adds permission to access the /app folder
# For more info, please refer to https://aka.ms/vscode-docker-dotnet-configure-containers
# RUN adduser -u 5678 --disabled-password --gecos "" appuser && chown -R appuser /app
# USER appuser

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Copy central package management files
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
# Copy all .csproj files to preserve project structure for restore
COPY ["Wayd.Web/src/Wayd.Web.Api/Wayd.Web.Api.csproj", "Wayd.Web/src/Wayd.Web.Api/"]
RUN dotnet restore "Wayd.Web/src/Wayd.Web.Api/Wayd.Web.Api.csproj"
COPY . .
WORKDIR "/src/Wayd.Web/src/Wayd.Web.Api"
# The Wolverine handler tree (Internal/Generated/WolverineHandlers) is git-ignored and generated once by the
# build-and-test-api CI job, then downloaded into the build context before this image is built (see
# .github/workflows/docker.yml). It is compiled in by the build below so the image runs TypeLoadMode.Static
# with no runtime Roslyn. Fail loudly if it is absent — otherwise the image would build fine and only break at
# first message dispatch (missing handlers). A local `docker build` without the CI step will hit this on
# purpose; generate the tree first (see Wayd.Web.Api.csproj) or build the image through the pipeline.
RUN test -n "$(ls -A Internal/Generated/WolverineHandlers/*.cs 2>/dev/null)" \
    || (echo 'ERROR: Wolverine handler tree missing. It must be generated (codegen write) and present in the build context before building this image. See .github/workflows/docker.yml.' && exit 1)
RUN dotnet build "Wayd.Web.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Wayd.Web.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Wayd.Web.Api.dll"]
