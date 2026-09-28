# Despliegue de Sector 09

El proyecto se despliega de dos formas: **escritorio** (Windows Forms, carpeta `Juego_Listo`) y **web** (el mismo juego en el navegador, servido desde un contenedor Docker).

| | Escritorio | Web |
|---|---|---|
| Proyecto | `DoomFPS.Presentation` | `DoomFPS.Web` (nuevo) |
| Tecnología | Windows Forms, .NET 8 | Blazor WebAssembly (AOT), .NET 8 + nginx |
| Resultado | Carpeta `Juego_Listo` + `Jugar.bat` | Imagen Docker `sector09-web` en el puerto 8080 |
| Reutiliza | — | Domain, Application, Infrastructure y el renderizador (`PixelRenderer`, `Sprites`, `ShipVisuals`) sin cambios |

---

## 1. Escritorio (Juego_Listo)

Elige **una** de estas dos opciones:

**A) Con el SDK de .NET 8 instalado en Windows**

1. Abre `Compilar_y_jugar.bat`.
2. Compila en modo Release, deja los binarios en `Juego_Listo` y arranca el juego.
3. Desde ese momento, `Jugar.bat` abre el juego directamente.

**B) Sin el SDK, solo con Docker Desktop**

1. Abre `Compilar_escritorio_con_Docker.bat`
   (equivale a `docker build --target escritorio --output type=local,dest=Juego_Listo .`).
2. Cuando termine, abre `Jugar.bat`.

En ambos casos, el equipo donde se juega necesita el **.NET Desktop Runtime 8** (o posterior).

**Para entregarlo:** comprime la carpeta completa del proyecto (o solo `Jugar.bat` + `Juego_Listo`) y compártela.

---

## 2. Web en Docker

### Requisitos
- Docker Desktop (Windows/Mac) o Docker Engine (Linux).
- Internet durante la construcción para descargar las imágenes de .NET y nginx y los paquetes NuGet.

### En tu PC (Windows)
1. Abre `Web_Docker.bat`.
2. Equivale a `docker compose up -d --build`.
3. Abre **http://localhost:8080**.

La primera construcción tarda entre **5 y 15 minutos** porque compila el motor a WebAssembly nativo (AOT). Las siguientes usan la caché y son mucho más rápidas.

### Comandos manuales
```bash
docker build -t sector09-web .
docker run -d --name sector09 --restart unless-stopped -p 8080:80 sector09-web
```
En un servidor Linux: `./desplegar_web.sh 80` (el argumento es el puerto).

Para detenerlo: `docker compose down` o `docker rm -f sector09`.

### Publicarlo en internet
La imagen es un nginx con archivos estáticos, así que funciona en cualquier servicio que ejecute contenedores:

- **Un VPS** (DigitalOcean, AWS Lightsail, Azure VM…): copia la carpeta, ejecuta `./desplegar_web.sh 80` y apunta tu dominio a la IP.
- **Render / Railway / Fly.io / Google Cloud Run:** conecta el repositorio y elige el `Dockerfile` de la raíz. El contenedor escucha en el puerto **80**.
- **Docker Hub:** `docker tag sector09-web TU_USUARIO/sector09-web` y luego `docker push TU_USUARIO/sector09-web`.

> Pointer Lock (capturar el ratón) y el audio necesitan **HTTPS** en un dominio público; en `localhost` funcionan sin él. Los servicios anteriores dan HTTPS automáticamente. En un VPS, pon delante Caddy o un proxy con Let's Encrypt.

### Compilación rápida de prueba (sin AOT)
```bash
docker build --build-arg AOT=false -t sector09-web .
```
Compila en ~1 minuto, pero el juego va mucho más lento: sirve para comprobar menús, no para jugar.

### Ejecutar la web sin Docker (desarrollo)
```bash
dotnet run --project DoomFPS.Web
```
Se ejecuta en modo Debug, sin AOT, así que el juego va lento; úsalo solo para revisar cambios.

---

## 3. Qué cambia en la versión web

| Escritorio | Web | Motivo |
|---|---|---|
| Ctrl o clic para disparar | **Clic o F** para disparar | Ctrl+W cierra la pestaña del navegador |
| ESC libera el ratón y pausa | Igual (el navegador suelta el ratón y el juego se pausa) | Pointer Lock |
| Historial en un cuadro de diálogo de Windows | Panel dentro del juego | No hay MessageBox en el navegador |
| ESC en el menú cierra la ventana | Muestra un aviso | Una página no puede cerrar su pestaña |
| Perfiles en `%AppData%\Sector09\perfiles.json` | Perfiles en el `localStorage` del navegador | Cada navegador guarda sus propios perfiles |
| Voz de Windows (System.Speech) | Voz del navegador (Web Speech), en español si está disponible | |
| Efectos reemplazables en la carpeta `Sonidos` | Los mismos efectos sintéticos, sin reemplazo | Sin acceso a disco |

Las reglas, niveles, misiones, armas, enemigos y el renderizado son **el mismo código C#** que en escritorio.

### Estructura añadida
```
DoomFPS.Web/
  Program.cs              arranque de Blazor y enlace con JS
  GameBoot.cs             composición (igual que Program.cs de escritorio)
  WebGame.cs              GameForm adaptado: pantallas, HUD y entrada
  Platform/               canvas, teclado, audio, voz, localStorage
  wwwroot/                index.html, css y js/sector09.js
Dockerfile                etapas: escritorio | web (nginx)
docker-compose.yml
docker/nginx.conf
```
`DoomFPS.Web` no se añadió a `DoomFPS.sln`, para que `Compilar_y_jugar.bat`, Visual Studio y las pruebas sigan funcionando igual.

## 4. Verificación realizada
- El código web compila contra Domain/Application/Infrastructure y el generador de interop JS acepta todas las firmas.
- Una prueba automática sin ventana recorre 20 casos del flujo (perfil nuevo, dificultad, cinemática, briefing, partida con disparo, pausa al perder el ratón, configuración, guardar y salir, historial, recarga con perfil persistente, continuar partida y restricción del panel de administrador). Todos pasan.
- La capa JS se probó en Chromium con un fotograma real del juego: renderizado, HUD, mapa, coordenadas del clic, rueda, bloqueo de F5 y almacenamiento.
- El renderizado tarda unos 7 ms por fotograma en nativo. Con AOT, en el navegador debería ir a unos 60 fps en un PC normal.
- **No se pudo probar aquí** la construcción completa de la imagen Docker ni del ejecutable de Windows: el entorno de preparación no tenía acceso a NuGet ni a los registros de imágenes. Ejecútalas en tu equipo con los scripts anteriores.
