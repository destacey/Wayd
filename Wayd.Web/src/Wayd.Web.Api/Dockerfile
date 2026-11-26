FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5150

ENV ASPNETCORE_URLS=http://+:5150

# Creates a non-root user with an explicit UID and adds permission to access the /app folder
# For more info, please refer to https://aka.ms/vscode-docker-dotnet-configure-containers
# RUN adduser -u 5678 --disabled-password --gecos "" appuser && chown -R appuser /app
# USER appuser

# The image CI publishes: the build context is the API as CI already published it (the build-api action), with
# the handler tree it tested compiled in, so this compiles nothing. Built with `--target prebuilt`; the stages
# after it build the same image from source, for docker compose and local builds.
FROM base AS prebuilt
COPY . .
ENTRYPOINT ["dotnet", "Wayd.Web.Api.dll"]

# Everything restore reads, in its place in the tree, and nothing else. This stage reruns on any change, but
# the COPY --from below is keyed on the content it copies, so the restore layer after it is reused until a
# project file, package version or NuGet setting changes. A new kind of restore input belongs in this list and
# in the build-compose-images job's file list in .github/workflows/docker.yml.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore-inputs
WORKDIR /src
COPY . .
RUN find . -type f ! \( -name '*.csproj' -o -name 'Directory.Build.props' -o -name 'Directory.Build.targets' \
        -o -name 'Directory.Packages.props' -o -name 'global.json' -o -iname 'nuget.config' \) -delete \
    && find . -type d -empty -delete

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY --from=restore-inputs /src .
RUN dotnet restore "Wayd.Web/src/Wayd.Web.Api/Wayd.Web.Api.csproj"
COPY . .
WORKDIR "/src/Wayd.Web/src/Wayd.Web.Api"
# The Wolverine handler tree (Internal/Generated/WolverineHandlers) is git-ignored, so a build from source
# needs it generated (`codegen write`, see Wayd.Web.Api.csproj) in the build context first. It is compiled in by
# the build below so the image runs TypeLoadMode.Static with no runtime Roslyn. Fail loudly if it is absent —
# otherwise the image would build fine and only break at first message dispatch (missing handlers).
RUN test -n "$(ls -A Internal/Generated/WolverineHandlers/*.cs 2>/dev/null)" \
    || (echo 'ERROR: Wolverine handler tree missing. It must be generated (codegen write) and present in the build context before building this image. See .github/actions/build-api/action.yml.' && exit 1)
# Without WAYD_STATIC_HANDLERS, Wayd.Web.Api.csproj leaves the tree out of the compile, and the image would ship
# no handlers.
ENV WAYD_STATIC_HANDLERS=true
RUN dotnet build "Wayd.Web.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Wayd.Web.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Wayd.Web.Api.dll"]
