# Builds and runs the PromptVCS MCP server (mcp-server / PromptVcs.McpServer)
# for deployment on Render (or any Docker host). The CLI and Runner are not
# part of this image — they run on users' own machines.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, keyed only on the two .csproj files, so source-only edits
# don't invalidate Docker's restore layer cache.
COPY PromptVcs.Core/PromptVcs.Core.csproj PromptVcs.Core/
COPY mcp-server/PromptVcs.McpServer.csproj mcp-server/
RUN dotnet restore mcp-server/PromptVcs.McpServer.csproj

COPY PromptVcs.Core/ PromptVcs.Core/
COPY mcp-server/ mcp-server/
RUN dotnet publish mcp-server/PromptVcs.McpServer.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render injects PORT at runtime; Program.cs reads it directly, so no
# ENV/EXPOSE default is required here beyond documenting intent.
EXPOSE 8080

ENTRYPOINT ["dotnet", "PromptVcs.McpServer.dll"]
