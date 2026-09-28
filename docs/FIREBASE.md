> **Nota:** desde la actualización de perfiles locales, `GameForm` ya no usa
> `LoginDialog` ni `IPlayerAccountService`/Firebase para iniciar sesión: usa
> `ProfileDialog` e `IProfileRepository` (perfiles locales sin correo ni
> contraseña, guardados en un JSON en el equipo del jugador). Las clases de
> Firebase descritas abajo se conservan en `Infrastructure` como referencia y
> siguen compilando, pero `Program.cs` ya no las inyecta. Ver la sección
> "Perfiles locales, guardado y progreso" en el README.

# Firebase y el frontend de Sector 09

El frontend es Windows Forms, pero la comunicación con Firebase queda separada de la pantalla. `GameForm` depende de `IPlayerAccountService`; por eso no conoce URLs, tokens ni JSON. `Program` inyecta `FirebaseRestPlayerAccountService` desde Infrastructure.

El flujo es:

1. Al pulsar Enter o F3, `GameForm` abre `LoginDialog`.
2. El diálogo pide correo y contraseña. Registrar usa `accounts:signUp`; entrar usa `accounts:signInWithPassword`.
3. `FirebaseRestPlayerAccountService` llama por HTTPS a Firebase Authentication REST. Firebase devuelve `idToken` y `localId`; la contraseña nunca se guarda en el juego.
4. Al completar cada nivel, `GameForm` envía `ScoreEntry` a `scores/{userId}` en Realtime Database: nivel, puntos, segundos, bajas y fecha.
5. F4 consulta esa ruta y muestra el historial del jugador.

## Configuración para la demostración

En PowerShell, antes de ejecutar `Jugar.bat`:

```powershell
$env:SECTOR09_FIREBASE_API_KEY="tu_web_api_key"
$env:SECTOR09_FIREBASE_DATABASE_URL="https://tu-proyecto-default-rtdb.firebaseio.com"
.\Jugar.bat
```

En Firebase Console hay que habilitar Email/Password en Authentication y crear Realtime Database. Una regla inicial para una demostración puede exigir autenticación y limitar cada usuario a sus propios datos; revisar las reglas antes de publicar.

Si las variables no existen, el programa usa `LocalPlayerAccountService`. Ese modo permite revisar la navegación y conserva el historial solo mientras la aplicación está abierta. La etiqueta del login lo informa; al configurar las variables aparece “Firebase conectado”.

El ID del usuario funciona como partición de datos. Así cada jugador solo consulta y escribe `scores/{suUid}`. La sesión se mantiene en memoria durante la ejecución y se descarta al cerrar.
