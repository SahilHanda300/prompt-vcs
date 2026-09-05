# Builds and runs PromptVcs.Web (the landing/marketing site) for deployment
# on Render as its own Web Service, separate from mcp-server's image (see
# the root Dockerfile). Standalone/thin, no PromptVcs.Core dependency —
# matches the CLI's pattern. Build context is the repo root (Render default),
# so COPY paths below are relative to the repo root, not this folder.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PromptVcs.Web/PromptVcs.Web.csproj PromptVcs.Web/
RUN dotnet restore PromptVcs.Web/PromptVcs.Web.csproj

COPY PromptVcs.Web/ PromptVcs.Web/
RUN dotnet publish PromptVcs.Web/PromptVcs.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Same inotify-exhaustion fix as mcp-server's Dockerfile — see its comment.
ENV DOTNET_hostBuilder__reloadConfigOnChange=false

# Render injects PORT at runtime; Program.cs reads it directly.
EXPOSE 8080

ENTRYPOINT ["dotnet", "PromptVcs.Web.dll"]
