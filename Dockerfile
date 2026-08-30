# Builds and runs the PromptVCS MCP server (mcp-server / PromptVcs.McpServer)
# for deployment on Render (or any Docker host). The Runner is not part of
# this image — it runs on users' own machines. The CLI IS included: the
# server's /terminal page spawns it as a local child process (see
# mcp-server/Services/TerminalSessionManager.cs) to drive a browser-based
# terminal, talking back to this same server over localhost.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, keyed only on the .csproj files, so source-only edits
# don't invalidate Docker's restore layer cache.
COPY PromptVcs.Core/PromptVcs.Core.csproj PromptVcs.Core/
COPY mcp-server/PromptVcs.McpServer.csproj mcp-server/
COPY PromptVcs.Cli/PromptVcs.Cli.csproj PromptVcs.Cli/
RUN dotnet restore mcp-server/PromptVcs.McpServer.csproj \
 && dotnet restore PromptVcs.Cli/PromptVcs.Cli.csproj

COPY PromptVcs.Core/ PromptVcs.Core/
COPY mcp-server/ mcp-server/
COPY PromptVcs.Cli/ PromptVcs.Cli/
RUN dotnet publish mcp-server/PromptVcs.McpServer.csproj -c Release -o /app/publish --no-restore \
 && dotnet publish PromptVcs.Cli/PromptVcs.Cli.csproj -c Release -o /app/cli-publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/cli-publish ./cli

# ASP.NET Core's default config setup watches appsettings.json for changes
# via a FileSystemWatcher (inotify on Linux) so it can hot-reload — useless
# in a container that gets rebuilt on every deploy anyway, and some hosts
# (Render included) cap inotify instances low enough that just starting up
# throws an unhandled IOException before the app ever binds a port.
ENV DOTNET_hostBuilder__reloadConfigOnChange=false

# Render injects PORT at runtime; Program.cs reads it directly, so no
# ENV/EXPOSE default is required here beyond documenting intent.
EXPOSE 8080

ENTRYPOINT ["dotnet", "PromptVcs.McpServer.dll"]
