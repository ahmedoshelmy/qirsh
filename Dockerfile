# One Dockerfile for the three .NET services: docker build --build-arg SERVICE=Ledger .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG SERVICE
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/
COPY seed/ seed/
RUN --mount=type=cache,id=qirsh-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/${SERVICE}/Qirsh.${SERVICE}.Api/Qirsh.${SERVICE}.Api.csproj -c Release -o /app \
      -p:UseAppHost=false -nodeReuse:false -p:UseSharedCompilation=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG SERVICE
WORKDIR /app
COPY --from=build /app ./
ENV DOTNET_gcServer=0 \
    DOTNET_GCHeapHardLimitPercent=0x4B \
    ASSEMBLY=Qirsh.${SERVICE}.Api.dll
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec dotnet \"$ASSEMBLY\""]
