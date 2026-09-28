# syntax=docker/dockerfile:1
# Sector 09 · imagen Docker
#
#   Web (por defecto):   docker build -t sector09-web .
#                        docker run -d -p 8080:80 --name sector09 sector09-web
#   Escritorio Windows:  docker build --target escritorio --output type=local,dest=Juego_Listo .
#                        (deja los binarios en Juego_Listo sin instalar el SDK de .NET)
#
# AOT=true compila el motor a WebAssembly nativo (mucho más fluido, compilación
# más lenta). Para una compilación rápida de prueba: --build-arg AOT=false

# ---------------------------------------------------------------- escritorio
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-desktop
WORKDIR /src
COPY . .
RUN dotnet publish DoomFPS.Presentation/DoomFPS.Presentation.csproj -c Release -o /out \
      -p:EnableWindowsTargeting=true -p:UseAppHost=false --nologo

FROM scratch AS escritorio
COPY --from=build-desktop /out /

# ---------------------------------------------------------------- web: compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-web
RUN apt-get update \
 && apt-get install -y --no-install-recommends python3 \
 && rm -rf /var/lib/apt/lists/*
RUN dotnet workload install wasm-tools
WORKDIR /src
# Primero solo los proyectos, para que la restauración quede en caché.
COPY DoomFPS.Domain/DoomFPS.Domain.csproj DoomFPS.Domain/
COPY DoomFPS.Application/DoomFPS.Application.csproj DoomFPS.Application/
COPY DoomFPS.Infrastructure/DoomFPS.Infrastructure.csproj DoomFPS.Infrastructure/
COPY DoomFPS.Web/DoomFPS.Web.csproj DoomFPS.Web/
RUN dotnet restore DoomFPS.Web/DoomFPS.Web.csproj
COPY . .
ARG AOT=true
RUN dotnet publish DoomFPS.Web/DoomFPS.Web.csproj -c Release -o /out -p:Aot=${AOT} --nologo

# ---------------------------------------------------------------- web: servidor
FROM nginx:1.27-alpine AS web
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build-web /out/wwwroot /usr/share/nginx/html
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://127.0.0.1/ >/dev/null || exit 1
