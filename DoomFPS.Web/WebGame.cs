using System.Drawing;
using DoomFPS.Application.Services;
using DoomFPS.Application.Interfaces;
using DoomFPS.Domain;
using DoomFPS.Domain.Entities;
using DoomFPS.Presentation;
using DoomFPS.Web.Platform;

namespace DoomFPS.Web;

// Versión web de GameForm: misma máquina de pantallas, mismas reglas y mismo
// HUD. Solo cambia la entrada (eventos del navegador), la salida (canvas vía
// IPlatform), el audio (Web Audio / Web Speech) y el guardado (localStorage).
public sealed class WebGame
{
    private enum Stage { Home, ProfilePicker, Continue, Difficulty, Training, Briefing, Play, Debrief, Campaign, Players, Admin, Cinematic }
    private enum PendingAction { None, StartOrContinue, Training, Admin, History, SwitchOnly }

    private readonly Func<CampaignService> createCampaign;
    private readonly IProfileRepository profiles;
    private readonly IPlatform platform;
    private CampaignService campaign;
    private readonly PixelRenderer renderer = new();
    private readonly Graphics graphics;
    private readonly HashSet<Keys> keys = new();
    private readonly HashSet<Keys> commandKeys = new();
    private readonly Dictionary<(int, bool), Font> fonts = new();
    private readonly WebSoundBank sound;
    private readonly WebVoice voice;

    private double lastTime = -1, accumulator, mouseTurn, mouseLook, nowSeconds, escapeGuardUntil;
    private Stage stage = Stage.Home;
    private bool showMap, showHelp, mouseCaptured, mouseFire, interact, reloadRequested;
    private int weaponSlot, wheelSteps;
    private int difficultyIndex = 1;
    private int trainingIndex;
    private LevelResult? lastResult;

    private PlayerProfile? currentProfile;
    private int continueIndex;
    private string homeNotice = "";
    private int homeNoticeFrames;

    // Sustituye al MessageBox del historial en escritorio.
    private string? historyTitle;
    private string[] historyLines = Array.Empty<string>();

    private PendingAction pendingAfterProfile = PendingAction.None;
    private List<PlayerProfile> profileList = new();
    private int profileIndex;
    private bool namingNewProfile;
    private string newProfileName = "";

    private int pauseIndex;
    private bool showSettings;
    private double mouseSensitivity = 1.0;
    private bool subtitlesEnabled = true;
    private static readonly string[] PauseLabels =
        { "REANUDAR", "CONFIGURACIÓN", "REINICIAR MISIÓN", "GUARDAR Y SALIR AL MENÚ" };

    private List<PlayerProfile> adminList = new();
    private int adminIndex;

    private List<string> cinematicLines = new();
    private int cinematicIndex;
    private int cinematicFrame;
    private Stage cinematicNextStage = Stage.Home;

    private static readonly Color Ink = Color.FromArgb(13, 18, 20);
    private static readonly Color Paper = Color.FromArgb(233, 230, 210);
    private static readonly Color Muted = Color.FromArgb(152, 163, 158);
    private static readonly Color Accent = Color.FromArgb(239, 143, 67);
    private static readonly Color Danger = Color.FromArgb(214, 78, 62);
    private static readonly Difficulty[] Difficulties =
        { Difficulty.Facil, Difficulty.Normal, Difficulty.Pesadilla };

    private GameService Game => campaign.Game;

    public WebGame(Func<CampaignService> createCampaign, IProfileRepository profiles, IPlatform platform)
    {
        this.createCampaign = createCampaign; this.profiles = profiles; this.platform = platform;
        graphics = new Graphics(platform);
        sound = new WebSoundBank(platform);
        voice = new WebVoice(platform);
        campaign = createCampaign();
    }

    // ------------------------------------------------------------ eventos del navegador

    // Un fotograma de requestAnimationFrame: avanza la simulación y dibuja.
    public void Frame(double nowMs, double mouseDx, double mouseDy)
    {
        nowSeconds = nowMs / 1000.0;
        if (mouseCaptured && stage == Stage.Play && Game.Status == GameStatus.Playing)
        {
            mouseTurn += mouseDx * 0.0025 * mouseSensitivity;
            mouseLook -= mouseDy * 0.0025 * mouseSensitivity;
        }
        TickGame();
        Paint();
    }

    // type: keydown/keyup/char/mousedown/mouseup/click/wheel/blur/pointerlock
    public void Event(string type, string arg, double value)
    {
        switch (type)
        {
            case "keydown":
                var key = KeyMap.FromCode(arg);
                if (key != Keys.None) OnKeyDown(key);
                break;
            case "keyup":
                var up = KeyMap.FromCode(arg);
                keys.Remove(up); commandKeys.Remove(up);
                break;
            case "char":
                if (arg.Length == 1) OnKeyPress(arg[0]);
                break;
            case "mousedown":
                if ((int)value != 0 || stage != Stage.Play || Game.Status != GameStatus.Playing) break;
                CaptureMouse(); mouseFire = true;
                break;
            case "mouseup":
                if ((int)value == 0) mouseFire = false;
                break;
            case "click":
                if ((int)value == 0) OnMouseClickUi(ParsePoint(arg));
                break;
            case "wheel":
                if (stage == Stage.Play && Game.Status == GameStatus.Playing) wheelSteps += Math.Sign(value);
                break;
            case "blur":
                Deactivate();
                break;
            case "pointerlock":
                bool locked = value > 0.5;
                if (!locked && mouseCaptured)
                {
                    // El navegador suelta el ratón con ESC: se trata como pausa y
                    // se ignora el ESC que pueda llegar justo después.
                    mouseCaptured = false;
                    if (stage == Stage.Play && Game.Status == GameStatus.Playing)
                    {
                        pauseIndex = 0; Game.SetPaused(true); ClearInput();
                        escapeGuardUntil = nowSeconds + 0.35;
                    }
                }
                else mouseCaptured = locked;
                break;
        }
    }

    private static PointF ParsePoint(string arg)
    {
        var parts = arg.Split(',');
        if (parts.Length != 2) return new PointF(-1, -1);
        float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
        float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
        return new PointF(x, y);
    }

    private void Deactivate()
    {
        if (stage == Stage.Play && Game.Status == GameStatus.Playing) Game.SetPaused(true);
        ClearInput(); commandKeys.Clear(); ReleaseMouse();
    }

    private void Close() => Notice("Para salir, cierra la pestaña del navegador. Tu progreso ya está guardado.");

    // ------------------------------------------------------------------ teclas

    private void OnKeyDown(Keys keyCode)
    {
        keys.Add(keyCode);

        // El historial (MessageBox en escritorio) se cierra con cualquier tecla.
        if (historyTitle is not null) { historyTitle = null; commandKeys.Add(keyCode); return; }
        if (keyCode == Keys.Escape && nowSeconds < escapeGuardUntil) return;

        // Escribiendo el nombre de un perfil nuevo: casi todas las teclas se
        // dejan pasar tal cual para que OnKeyPress reciba el carácter.
        if (stage == Stage.ProfilePicker && namingNewProfile)
        {
            if (keyCode == Keys.Enter) { commandKeys.Add(keyCode); ConfirmNewProfileName(); }
            else if (keyCode == Keys.Escape) { commandKeys.Add(keyCode); namingNewProfile = false; }
            else if (keyCode == Keys.Back)
            {
                if (newProfileName.Length > 0) newProfileName = newProfileName[..^1];
            }
            return;
        }

        if (!commandKeys.Add(keyCode)) return;

        if (keyCode is >= Keys.D1 and <= Keys.D6) weaponSlot = keyCode - Keys.D1 + 1;
        else if (keyCode is >= Keys.NumPad1 and <= Keys.NumPad6) weaponSlot = keyCode - Keys.NumPad1 + 1;

        switch (stage)
        {
            case Stage.Home:
                if (keyCode == Keys.Enter) ActivateHomePlay();
                else if (keyCode == Keys.F3) ActivateHomeTraining();
                else if (keyCode == Keys.F4) ActivateHomeHistory();
                else if (keyCode == Keys.F5) stage = Stage.Players;
                else if (keyCode == Keys.F6) ActivateHomeAdmin();
                else if (keyCode == Keys.F7) ActivateHomeSwitch();
                else if (keyCode == Keys.Escape) Close();
                break;

            case Stage.ProfilePicker:
                int rows = profileList.Count + 1;
                if (keyCode == Keys.Up) profileIndex = (profileIndex + rows - 1) % rows;
                else if (keyCode == Keys.Down) profileIndex = (profileIndex + 1) % rows;
                else if (keyCode == Keys.Enter) ActivateProfileRow();
                else if (keyCode == Keys.Escape) stage = Stage.Home;
                break;

            case Stage.Continue:
                if (keyCode is Keys.Up or Keys.Down) continueIndex = continueIndex == 0 ? 1 : 0;
                else if (keyCode == Keys.Enter) ActivateContinueOption();
                else if (keyCode == Keys.Escape) stage = Stage.Home;
                break;

            case Stage.Players:
                if (keyCode is Keys.Escape or Keys.Enter or Keys.F5) stage = Stage.Home;
                break;

            case Stage.Admin:
                if (keyCode == Keys.Up) adminIndex = adminList.Count == 0 ? 0 : (adminIndex + adminList.Count - 1) % adminList.Count;
                else if (keyCode == Keys.Down) adminIndex = adminList.Count == 0 ? 0 : (adminIndex + 1) % adminList.Count;
                else if (keyCode == Keys.Delete) DeleteSelectedProfile();
                else if (keyCode == Keys.R) ResetSelectedProfile();
                else if (keyCode is Keys.Escape or Keys.F6) stage = Stage.Home;
                break;

            case Stage.Training:
                if (keyCode is >= Keys.D1 and <= Keys.D7) trainingIndex = keyCode - Keys.D1;
                if (keyCode == Keys.Up) trainingIndex = (trainingIndex + 6) % 7;
                else if (keyCode == Keys.Down) trainingIndex = (trainingIndex + 1) % 7;
                else if (keyCode == Keys.Enter) ActivateTraining();
                else if (keyCode == Keys.Escape) stage = Stage.Home;
                break;
            case Stage.Difficulty:
                if (keyCode is Keys.Up or Keys.W)
                    difficultyIndex = (difficultyIndex + Difficulties.Length - 1) % Difficulties.Length;
                else if (keyCode is Keys.Down or Keys.S)
                    difficultyIndex = (difficultyIndex + 1) % Difficulties.Length;
                else if (keyCode == Keys.Enter) StartCampaign();
                else if (keyCode == Keys.Escape) stage = Stage.Home;
                break;

            case Stage.Cinematic:
                if (keyCode is Keys.Enter or Keys.Space) AdvanceCinematic();
                else if (keyCode == Keys.Escape) SkipCinematic();
                break;

            case Stage.Briefing:
                if (keyCode is Keys.Enter or Keys.Space) { stage = Stage.Play; ClearInput(); }
                else if (keyCode == Keys.Escape) stage = Stage.Home;
                break;

            case Stage.Debrief:
                if (keyCode is Keys.Enter or Keys.Space) NextLevel();
                break;

            case Stage.Campaign:
                if (keyCode is Keys.Enter or Keys.Space or Keys.Escape) stage = Stage.Home;
                break;

            case Stage.Play:
                HandlePlayKey(keyCode);
                break;
        }
    }

    // Carácter escrito mientras se crea un perfil nuevo (letras, números,
    // espacio y algunos signos). Enter/Esc/Retroceso los maneja OnKeyDown.
    private void OnKeyPress(char c)
    {
        if (stage != Stage.ProfilePicker || !namingNewProfile) return;
        if (char.IsControl(c)) return;
        if (newProfileName.Length < 24 && (char.IsLetterOrDigit(c) || c is ' ' or '_' or '-' or '.'))
            newProfileName += c;
    }

    private void HandlePlayKey(Keys key)
    {
        if (Game.Status == GameStatus.Paused && showSettings) { HandleSettingsKey(key); return; }

        if (key == Keys.Enter)
        {
            if (Game.Status == GameStatus.Lost) RetryLevel();
            else if (Game.Status == GameStatus.Paused)
            {
                if (showHelp) { showHelp = false; Game.SetPaused(false); ClearInput(); }
                else ActivatePauseOption();
            }
        }
        else if (key == Keys.Escape)
        {
            showHelp = false; showSettings = false;
            bool wasPlaying = Game.Status == GameStatus.Playing;
            if (wasPlaying) pauseIndex = 0;
            Game.SetPaused(wasPlaying);
            ReleaseMouse(); ClearInput();
        }
        else if (Game.Status == GameStatus.Paused && !showHelp && key == Keys.Up) pauseIndex = (pauseIndex + 3) % 4;
        else if (Game.Status == GameStatus.Paused && !showHelp && key == Keys.Down) pauseIndex = (pauseIndex + 1) % 4;
        else if (key == Keys.E) interact = true;
        else if (key == Keys.R && Game.Status == GameStatus.Playing) reloadRequested = true;
        else if (key == Keys.R && Game.Status != GameStatus.Playing) RetryLevel();
        else if (key == Keys.M) showMap = !showMap;
        else if (key == Keys.F1 && Game.Status is GameStatus.Playing or GameStatus.Paused)
        { showHelp = !showHelp; Game.SetPaused(showHelp); ReleaseMouse(); ClearInput(); }
        else if (key == Keys.F2) { sound.Muted = !sound.Muted; voice.Muted = sound.Muted; }
        else if (key == Keys.Q) wheelSteps--;
        else if (key == Keys.Tab) wheelSteps++;
    }

    // Menú de pausa: ↑ ↓ eligen, ENTER confirma la fila resaltada.
    private void ActivatePauseOption()
    {
        switch (pauseIndex)
        {
            case 0: showHelp = false; Game.SetPaused(false); ClearInput(); break;
            case 1: showSettings = true; break;
            case 2: RetryLevel(); break;
            default: SaveAndExitToMenu(); break;
        }
    }

    // Sub-pantalla de configuración abierta desde la pausa.
    private void HandleSettingsKey(Keys key)
    {
        if (key is Keys.Escape or Keys.Enter) { showSettings = false; return; }
        if (key == Keys.Left) mouseSensitivity = Math.Max(0.3, Math.Round(mouseSensitivity - 0.1, 1));
        else if (key == Keys.Right) mouseSensitivity = Math.Min(2.5, Math.Round(mouseSensitivity + 0.1, 1));
        else if (key is Keys.Up or Keys.Down) subtitlesEnabled = !subtitlesEnabled;
        else if (key == Keys.F2) { sound.Muted = !sound.Muted; voice.Muted = sound.Muted; }
    }

    private void ActivateContinueOption()
    {
        if (continueIndex == 0 && currentProfile?.Save is { } save)
        {
            campaign = createCampaign();
            campaign.Resume(save);
            lastResult = null; stage = Stage.Briefing; ResetRound();
        }
        else
        {
            if (currentProfile is not null) { currentProfile.Save = null; profiles.Save(currentProfile); }
            stage = Stage.Difficulty;
        }
    }

    private void ActivateTraining()
    {
        campaign = createCampaign(); campaign.StartTraining(trainingIndex, Difficulty.Facil);
        lastResult = null; stage = Stage.Briefing; ResetRound();
    }

    // ------------------------------------------------------------- flujo de campaña y perfiles

    // Acciones del menú principal: si no hay perfil activo, primero se abre
    // el selector de perfil (pantalla propia) y se retoma la acción al
    // volver, en vez de bloquear con un cuadro de diálogo.
    private void ActivateHomePlay()
    {
        if (currentProfile is not null)
        {
            stage = currentProfile.Save is not null ? Stage.Continue : Stage.Difficulty;
            if (stage == Stage.Continue) continueIndex = 0;
        }
        else { pendingAfterProfile = PendingAction.StartOrContinue; OpenProfilePicker(); }
    }

    private void ActivateHomeTraining()
    {
        if (currentProfile is not null)
        {
            if (currentProfile.CanSelectAnyLevel) stage = Stage.Training;
            else Notice("Completa la campaña una vez para desbloquear la selección de nivel.");
        }
        else { pendingAfterProfile = PendingAction.Training; OpenProfilePicker(); }
    }

    private void ActivateHomeHistory()
    {
        if (currentProfile is not null) ShowHistory();
        else { pendingAfterProfile = PendingAction.History; OpenProfilePicker(); }
    }

    private void ActivateHomeAdmin()
    {
        if (currentProfile is not null)
        {
            if (currentProfile.Role == ProfileRole.Administrador)
            { adminList = profiles.All.ToList(); adminIndex = 0; stage = Stage.Admin; }
            else Notice("El panel de administrador es solo para la cuenta administradora.");
        }
        else { pendingAfterProfile = PendingAction.Admin; OpenProfilePicker(); }
    }

    private void ActivateHomeSwitch()
    {
        pendingAfterProfile = PendingAction.SwitchOnly;
        OpenProfilePicker();
    }

    private void OpenProfilePicker()
    {
        RefreshProfileList();
        stage = Stage.ProfilePicker;
    }

    private void RefreshProfileList()
    {
        profileList = profiles.All.ToList();
        profileIndex = 0;
        if (profiles.LastUsedName is not null)
        {
            int idx = profileList.FindIndex(p => string.Equals(p.Name, profiles.LastUsedName, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) profileIndex = idx;
        }
        namingNewProfile = false; newProfileName = "";
    }

    // La última fila de la lista siempre es "crear perfil nuevo".
    private void ActivateProfileRow()
    {
        if (profileIndex == profileList.Count) { namingNewProfile = true; newProfileName = ""; return; }
        if (profileIndex < 0 || profileIndex >= profileList.Count) return;
        OnProfileChosen(profiles.CreateOrGet(profileList[profileIndex].Name));
    }

    private void ConfirmNewProfileName()
    {
        var name = newProfileName.Trim();
        if (name.Length == 0) return;
        OnProfileChosen(profiles.CreateOrGet(name));
    }

    private void OnProfileChosen(PlayerProfile profile)
    {
        currentProfile = profile;
        profiles.SetLastUsed(profile.Name);
        namingNewProfile = false;
        var pending = pendingAfterProfile;
        pendingAfterProfile = PendingAction.None;
        switch (pending)
        {
            case PendingAction.Training:
                if (currentProfile.CanSelectAnyLevel) stage = Stage.Training;
                else { stage = Stage.Home; Notice("Completa la campaña una vez para desbloquear la selección de nivel."); }
                break;
            case PendingAction.Admin:
                if (currentProfile.Role == ProfileRole.Administrador)
                { adminList = profiles.All.ToList(); adminIndex = 0; stage = Stage.Admin; }
                else { stage = Stage.Home; Notice("El panel de administrador es solo para la cuenta administradora."); }
                break;
            case PendingAction.History:
                stage = Stage.Home; ShowHistory();
                break;
            case PendingAction.SwitchOnly:
                stage = Stage.Home;
                break;
            default:
                stage = currentProfile.Save is not null ? Stage.Continue : Stage.Difficulty;
                if (stage == Stage.Continue) continueIndex = 0;
                break;
        }
    }

    private void Notice(string text) { homeNotice = text; homeNoticeFrames = 220; }

    // Solo las mejoras reales (arma nueva, armadura, megaesfera, mochila) se
    // anuncian con voz; recoger balas o cartuchos a cada paso sería agotador.
    private static bool IsUpgradeMessage(string message) => !string.IsNullOrEmpty(message) &&
        (message.StartsWith('¡') || message.Contains("armadura", StringComparison.OrdinalIgnoreCase) ||
         message.Contains("MEGAESFERA", StringComparison.OrdinalIgnoreCase) ||
         message.Contains("Mochila", StringComparison.OrdinalIgnoreCase));

    private static string CleanForSpeech(string text) => text.Replace('¡', ' ').Replace('!', '.').Replace('·', ',');

    // --------------------------------------------------------- cinemáticas

    private void BeginCinematic(IReadOnlyList<string> lines, Stage next)
    {
        cinematicLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        cinematicNextStage = next;
        if (cinematicLines.Count == 0) { stage = next; return; }
        cinematicIndex = 0; cinematicFrame = 0;
        stage = Stage.Cinematic;
        ClearInput();
    }

    private void AdvanceCinematic()
    {
        cinematicIndex++; cinematicFrame = 0;
        if (cinematicIndex >= cinematicLines.Count) { stage = cinematicNextStage; ClearInput(); }
    }

    private void SkipCinematic() { stage = cinematicNextStage; ClearInput(); }

    // Presenta lo que ya cuentan la campaña (Debrief de cada misión y sus
    // frases de briefing), en vez de inventar texto nuevo: la historia que se
    // narra es la misma, solo con una puesta en escena más cinematográfica.
    private static IReadOnlyList<string> OpeningCinematicLines(LevelInfo first) => new[]
    {
        "El reactor de la Sector 09 ha fallado. La nave cae sin control hacia el planeta.",
        "Los sistemas centrales están mudos. No hay señal de la tripulación.",
        "Alguien debe llegar al puente de mando antes del impacto — ese alguien eres tú.",
        $"MISIÓN 1 · {first.Name}"
    };

    private static IReadOnlyList<string> TransitionCinematicLines(string recap, LevelInfo next) => new[]
    {
        recap,
        $"Sector siguiente: {next.Name}.",
        next.Briefing.Count > 0 ? next.Briefing[0] : next.Subtitle
    };

    private void ShowHistory()
    {
        var p = currentProfile;
        if (p is null) return;
        string text = $"Rol: {(p.Role == ProfileRole.Administrador ? "Administrador" : "Jugador")}\n" +
            $"Campañas completadas: {p.CampaignsCompleted}\n" +
            $"Mejor puntuación: {p.BestScore}\n" +
            $"Mejor misión alcanzada: {p.BestLevelReached}\n" +
            $"Tiempo total jugado: {TimeSpan.FromSeconds(p.TotalSecondsPlayed):hh\\:mm\\:ss}\n" +
            $"Partida guardada: {(p.Save is null ? "no" : $"misión {p.Save.LevelIndex + 1}")}";
        historyTitle = $"HISTORIAL · {p.Name}";
        historyLines = text.Split('\n');
    }

    private void DeleteSelectedProfile()
    {
        if (adminIndex < 0 || adminIndex >= adminList.Count) return;
        var target = adminList[adminIndex];
        if (currentProfile is not null && string.Equals(target.Name, currentProfile.Name, StringComparison.OrdinalIgnoreCase))
        { Notice("No puedes eliminar el perfil con el que iniciaste sesión."); return; }
        profiles.Delete(target.Name);
        adminList = profiles.All.ToList();
        if (adminIndex >= adminList.Count) adminIndex = Math.Max(0, adminList.Count - 1);
    }

    private void ResetSelectedProfile()
    {
        if (adminIndex < 0 || adminIndex >= adminList.Count) return;
        var target = adminList[adminIndex];
        target.Save = null; target.CampaignsCompleted = 0; target.BestLevelReached = 0; target.BestScore = 0;
        profiles.Save(target);
        adminList = profiles.All.ToList();
    }

    private void StartCampaign()
    {
        campaign = createCampaign();
        campaign.StartNewCampaign(Difficulties[difficultyIndex]);
        lastResult = null;
        ResetRound();
        SaveProgress();
        BeginCinematic(OpeningCinematicLines(campaign.Info), Stage.Briefing);
    }

    private void RetryLevel()
    {
        campaign.RetryLevel();
        stage = Stage.Briefing;
        ResetRound();
    }

    private void NextLevel()
    {
        string recap = campaign.Info.Debrief;
        if (campaign.AdvanceToNextLevel())
        {
            ResetRound(); SaveProgress();
            BeginCinematic(TransitionCinematicLines(recap, campaign.Info), Stage.Briefing);
        }
        else { stage = Stage.Campaign; OnCampaignFinished(); }
    }

    // Guarda el punto de control actual (misión y equipo) en el perfil local
    // para poder reanudar la partida más tarde.
    private void SaveProgress()
    {
        if (currentProfile is null || campaign.TrainingMode || campaign.CampaignFinished) return;
        try
        {
            currentProfile.Save = campaign.Snapshot();
            currentProfile.LastPlayedAt = DateTimeOffset.UtcNow;
            currentProfile.BestScore = Math.Max(currentProfile.BestScore, campaign.TotalScore + Game.Score);
            currentProfile.BestLevelReached = Math.Max(currentProfile.BestLevelReached, campaign.Info.Number);
            profiles.Save(currentProfile);
        }
        catch { /* guardar es en el mejor esfuerzo */ }
    }

    private void OnCampaignFinished()
    {
        if (currentProfile is null || campaign.TrainingMode) return;
        currentProfile.CampaignsCompleted++;
        currentProfile.Save = null;
        currentProfile.BestScore = Math.Max(currentProfile.BestScore, campaign.TotalScore);
        currentProfile.BestLevelReached = Math.Max(currentProfile.BestLevelReached, campaign.LevelCount);
        currentProfile.TotalSecondsPlayed += campaign.TotalSeconds;
        currentProfile.LastPlayedAt = DateTimeOffset.UtcNow;
        profiles.Save(currentProfile);
    }

    private void SaveAndExitToMenu()
    {
        SaveProgress();
        ReleaseMouse(); ClearInput();
        showHelp = false; showSettings = false; pauseIndex = 0;
        stage = Stage.Home;
    }

    private void ResetRound()
    {
        showHelp = false; showMap = false; showSettings = false; pauseIndex = 0; accumulator = 0;
        ClearInput(); ReleaseMouse();
    }

    private void ClearInput()
    {
        keys.Clear(); mouseFire = false; interact = false; reloadRequested = false;
        mouseTurn = 0; mouseLook = 0; weaponSlot = 0; wheelSteps = 0;
    }

    private void CaptureMouse()
    {
        if (mouseCaptured) return;
        // Pointer Lock: el navegador confirma con el evento "pointerlock".
        mouseCaptured = true; platform.LockPointer();
    }

    private void ReleaseMouse()
    {
        if (!mouseCaptured) return;
        mouseCaptured = false; platform.UnlockPointer();
    }

    // --------------------------------------------------------------- ratón (menús)

    // p ya llega en coordenadas del lienzo virtual de 960×640 (lo calcula JS).
    private void OnMouseClickUi(PointF p)
    {
        if (historyTitle is not null) { historyTitle = null; return; }
        switch (stage)
        {
            case Stage.Home: HandleHomeClick(p); break;
            case Stage.ProfilePicker: HandleProfilePickerClick(p); break;
            case Stage.Continue: HandleContinueClick(p); break;
            case Stage.Difficulty: HandleDifficultyClick(p); break;
            case Stage.Training: HandleTrainingClick(p); break;
            case Stage.Players: stage = Stage.Home; break;
            case Stage.Admin: HandleAdminClick(p); break;
            case Stage.Cinematic: AdvanceCinematic(); break;
            case Stage.Briefing: stage = Stage.Play; ClearInput(); break;
            case Stage.Debrief: NextLevel(); break;
            case Stage.Campaign: stage = Stage.Home; break;
            case Stage.Play: HandlePlayClick(p); break;
        }
    }

    private void HandleHomeClick(PointF p)
    {
        if (HomePlayRect.Contains(p)) { ActivateHomePlay(); return; }
        for (int i = 0; i < HomeChips.Length; i++)
        {
            if (!HomeChips[i].Rect.Contains(p)) continue;
            switch (i)
            {
                case 0: ActivateHomeTraining(); break;
                case 1: ActivateHomeHistory(); break;
                case 2: stage = Stage.Players; break;
                case 3: ActivateHomeAdmin(); break;
                case 4: ActivateHomeSwitch(); break;
                default: Close(); break;
            }
            return;
        }
    }

    private void HandleProfilePickerClick(PointF p)
    {
        if (namingNewProfile)
        {
            if (NamingConfirmRect.Contains(p)) ConfirmNewProfileName();
            else if (NamingCancelRect.Contains(p)) namingNewProfile = false;
            return;
        }
        int total = Math.Min(profileList.Count, MaxVisibleProfiles) + 1;
        for (int i = 0; i < total; i++)
        {
            if (!ProfileRowRect(i).Contains(p)) continue;
            profileIndex = i;
            ActivateProfileRow();
            return;
        }
    }

    private void HandleContinueClick(PointF p)
    {
        for (int i = 0; i < 2; i++)
        {
            if (!ContinueRowRect(i).Contains(p)) continue;
            continueIndex = i;
            ActivateContinueOption();
            return;
        }
    }

    private void HandleDifficultyClick(PointF p)
    {
        for (int i = 0; i < Difficulties.Length; i++)
        {
            if (!DifficultyRowRect(i).Contains(p)) continue;
            difficultyIndex = i;
            StartCampaign();
            return;
        }
    }

    private void HandleTrainingClick(PointF p)
    {
        for (int i = 0; i < 7; i++)
        {
            if (!TrainingRowRect(i).Contains(p)) continue;
            trainingIndex = i;
            ActivateTraining();
            return;
        }
    }

    private void HandleAdminClick(PointF p)
    {
        for (int i = 0; i < adminList.Count && i < 12; i++)
        {
            if (!AdminRowRect(i).Contains(p)) continue;
            adminIndex = i;
            return;
        }
    }

    private void HandlePlayClick(PointF p)
    {
        if (Game.Status == GameStatus.Paused)
        {
            if (showHelp) { showHelp = false; return; }
            if (showSettings) { HandleSettingsClick(p); return; }
            for (int i = 0; i < PauseLabels.Length; i++)
            {
                if (!PauseRowRect(i).Contains(p)) continue;
                pauseIndex = i;
                ActivatePauseOption();
                return;
            }
        }
        else if (Game.Status == GameStatus.Lost)
        {
            RetryLevel();
        }
    }

    private void HandleSettingsClick(PointF p)
    {
        for (int i = 0; i < 4; i++)
        {
            var row = SettingsRowRect(i);
            if (!row.Contains(p)) continue;
            switch (i)
            {
                case 0:
                    if (p.X < row.X + row.Width / 2) mouseSensitivity = Math.Max(0.3, Math.Round(mouseSensitivity - 0.1, 1));
                    else mouseSensitivity = Math.Min(2.5, Math.Round(mouseSensitivity + 0.1, 1));
                    break;
                case 1: subtitlesEnabled = !subtitlesEnabled; break;
                case 2: sound.Muted = !sound.Muted; voice.Muted = sound.Muted; break;
                default: showSettings = false; break;
            }
            return;
        }
    }

    // -------------------------------------------------------------------- reloj

    private void TickGame()
    {
        double now = nowSeconds;
        double elapsed = lastTime < 0 ? 0 : Math.Clamp(now - lastTime, 0, 0.1); lastTime = now;
        if (homeNoticeFrames > 0) homeNoticeFrames--;
        if (stage == Stage.Cinematic) cinematicFrame++;
        if (stage == Stage.Play && Game.Status == GameStatus.Playing)
        {
            accumulator += elapsed;
            while (accumulator >= 1d / GameService.FramesPerSecond)
            {
                bool Down(Keys key) => keys.Contains(key);
                double turn = (Down(Keys.Right) ? 1 : 0) - (Down(Keys.Left) ? 1 : 0);
                var input = new GameInput(
                    (Down(Keys.W) || Down(Keys.Up) ? 1 : 0) - (Down(Keys.S) || Down(Keys.Down) ? 1 : 0),
                    (Down(Keys.D) ? 1 : 0) - (Down(Keys.A) ? 1 : 0),
                    turn * 2.3 / 60 + mouseTurn,
                    Down(Keys.F) || mouseFire,
                    interact, Down(Keys.ShiftKey),
                    weaponSlot, wheelSteps > 0, wheelSteps < 0,
                    mouseLook + ((Down(Keys.PageUp) ? 1 : 0) - (Down(Keys.PageDown) ? 1 : 0)) * 1.5 / 60, Down(Keys.Home),
                    reloadRequested, Down(Keys.Space));
                mouseTurn = 0; mouseLook = 0; interact = false; weaponSlot = 0; wheelSteps = 0; reloadRequested = false;
                Game.Step(input);
                sound.BeginFrame();
                foreach (var evt in Game.LastEvents)
                {
                    if (evt.Kind == "story") { voice.Say(evt.Message); continue; }
                    sound.Play(evt.Kind);
                    if (evt.Kind == "pickup" && IsUpgradeMessage(evt.Message)) voice.Say(CleanForSpeech(evt.Message));
                }
                accumulator -= 1d / 60;
                if (Game.Status != GameStatus.Playing) { OnRoundEnded(); break; }
            }
        }
        else accumulator = 0;
    }

    private void OnRoundEnded()
    {
        ReleaseMouse(); ClearInput(); accumulator = 0;
        if (Game.Status != GameStatus.Won) return;
        lastResult = campaign.CompleteLevel();
        sound.Play("level");
        stage = campaign.IsFinalLevel ? Stage.Campaign : Stage.Debrief;
    }

    // ------------------------------------------------------------------- dibujo

    // El fotograma 640×360 del raycaster se escala a 960×540 en JS; encima se
    // ejecutan las órdenes del HUD acumuladas en Graphics.
    private void Paint()
    {
        renderer.Render(Game);
        var g = graphics;
        g.Begin();

        DrawHud(g);
        if (stage is Stage.Play or Stage.Debrief or Stage.Campaign) DrawPlayOverlay(g);
        switch (stage)
        {
            case Stage.Home: DrawHome(g); break;
            case Stage.ProfilePicker: DrawProfilePicker(g); break;
            case Stage.Continue: DrawContinue(g); break;
            case Stage.Players: DrawPlayers(g); break;
            case Stage.Admin: DrawAdmin(g); break;
            case Stage.Difficulty: DrawDifficulty(g); break;
            case Stage.Training: DrawTraining(g); break;
            case Stage.Cinematic: DrawCinematic(g); break;
            case Stage.Briefing: DrawBriefing(g); break;
            case Stage.Debrief: DrawDebrief(g); break;
            case Stage.Campaign: DrawCampaignEnd(g); break;
            default:
                if (Game.Status == GameStatus.Paused) DrawPause(g);
                else if (Game.Status == GameStatus.Lost) DrawDefeat(g);
                break;
        }
        if (historyTitle is not null) DrawHistory(g);
        platform.Present(renderer.Pixels, g.Flush());
    }

    private void DrawHistory(Graphics g)
    {
        Fill(g, Color.FromArgb(200, 4, 6, 7), 0, 0, 960, 640);
        Fill(g, Color.FromArgb(245, 20, 28, 30), 230, 150, 500, 300);
        Fill(g, Accent, 230, 150, 500, 4);
        TextAt(g, historyTitle ?? "", 256, 172, 18, Paper, true);
        for (int i = 0; i < historyLines.Length; i++) TextAt(g, historyLines[i], 256, 216 + i * 30, 15, Paper);
        Center(g, "Cualquier tecla o clic · cerrar", 412, 12, Accent, true);
    }

    private void DrawPlayOverlay(Graphics g)
    {
        var game = Game;
        Fill(g, Color.FromArgb(165, 10, 16, 17), 0, 0, 960, 47);
        TextAt(g, $"MISIÓN {game.Level.Number}/{campaign.LevelCount}", 22, 12, 15, Accent, true);
        TextAt(g, game.Level.Name, 152, 15, 12, Muted, true);
        TextAt(g, game.Objective, 420, 15, 11, Paper, true);
        if (game.InSafeZone) TextAt(g, "ZONA SEGURA", 700, 15, 12, Color.FromArgb(140, 220, 180), true);
        TextAt(g, "M · MAPA", 866, 15, 12, Muted);

        using var pen = new Pen(game.ShotFlashFrames > 0 ? Accent : Paper, 2);
        g.DrawLine(pen, 469, 270, 475, 270); g.DrawLine(pen, 485, 270, 491, 270);
        g.DrawLine(pen, 480, 259, 480, 265); g.DrawLine(pen, 480, 275, 480, 281);

        var boss = game.World.Enemies.FirstOrDefault(en => en.IsAlive && en.Kind == EnemyKind.Ciberdemonio);
        if (boss is not null)
        {
            Fill(g, Color.FromArgb(190, 10, 16, 17), 250, 56, 460, 26);
            Fill(g, Color.FromArgb(48, 26, 26), 258, 68, 444, 8);
            Fill(g, Danger, 258, 68, (float)(444 * boss.HealthFraction), 8);
            TextAt(g, game.Mission?.ShieldActive == true ? "CIBERDEMONIO · ESCUDO ACTIVO" : "CIBERDEMONIO · VULNERABLE", 258, 57, 11, Paper, true);
        }

        if (subtitlesEnabled && game.SubtitleFrames > 0)
        {
            string line = string.IsNullOrEmpty(game.SubtitleSpeaker) ? game.Subtitle : $"{game.SubtitleSpeaker}: {game.Subtitle}";
            Fill(g, Color.FromArgb(205, 8, 12, 14), 90, 445, 780, 32);
            Center(g, line, 453, 14, Color.FromArgb(255, 226, 170), true);
        }
        if (game.MessageFrames > 0)
        {
            Fill(g, Color.FromArgb(185, 10, 16, 17), 120, 487, 720, 34);
            Center(g, game.Message, 492, 14, Paper);
        }
        if (game.Mission is { } mission)
        {
            Fill(g, Color.FromArgb(215, 10, 21, 25), 18, 88, 510, 58);
            TextAt(g, mission.Detail, 30, 96, 12, Paper, true);
            Fill(g, Color.FromArgb(43, 62, 65), 30, 128, 480, 5);
            Fill(g, Accent, 30, 128, (float)(480 * mission.Progress), 5);
            var pending = mission.Nodes.FirstOrDefault(n => !n.Activated);
            if (pending is not null && mission.Plan.Kind != MissionKind.Escort)
                TextAt(g, pending.Point.Name + " · E al acercarte · M para localizar", 30, 114, 10, Muted);
        }
        if (showMap) DrawMap(g);
    }

    private void DrawHud(Graphics g)
    {
        var game = Game;
        var player = game.Player;
        Fill(g, Ink, 0, 540, 960, 100);
        Fill(g, Accent, 0, 540, 960, 3);

        Color healthColor = player.Health < 35 ? Danger : player.Health > 100 ? Color.FromArgb(120, 208, 236) : Paper;
        TextAt(g, "SALUD", 24, 547, 10, Muted, true);
        TextAt(g, player.Health.ToString("000"), 22, 558, 28, healthColor, true);
        Fill(g, Color.FromArgb(48, 56, 52), 24, 594, 108, 4);
        Fill(g, healthColor, 24, 594, 1.08f * Math.Min(100, player.Health), 4);

        TextAt(g, "ARMADURA", 150, 547, 10, Muted, true);
        TextAt(g, player.Armor.ToString("000"), 148, 558, 28,
            player.Armor > 0 ? Color.FromArgb(120, 206, 150) : Muted, true);
        Fill(g, Color.FromArgb(48, 56, 52), 150, 594, 108, 4);
        Fill(g, Color.FromArgb(120, 206, 150), 150, 594, 1.08f * Math.Min(100, player.Armor), 4);

        var weapon = player.CurrentWeapon;
        TextAt(g, weapon.ShortName, 276, 547, 10, Accent, true);
        if (weapon.MagazineSize > 0)
        {
            int reserve = player.AmmoOf(weapon.Ammo);
            Color ammoColor = weapon.Loaded == 0 ? Danger : Accent;
            TextAt(g, $"{weapon.Loaded}/{reserve}", 274, 558, 26, ammoColor, true);
            TextAt(g, game.IsReloading ? "RECARGANDO…" : "R · RECARGAR", 276, 594, 10, game.IsReloading ? Accent : Muted);
            if (game.IsReloading)
            {
                float pct = 1f - (float)game.ReloadFramesLeft / Math.Max(1, game.ReloadDurationFrames);
                Fill(g, Color.FromArgb(48, 56, 52), 276, 618, 108, 4);
                Fill(g, Accent, 276, 618, 108 * pct, 4);
            }
        }
        else
        {
            int ammo = player.AmmoOf(weapon.Ammo);
            TextAt(g, ammo.ToString("000"), 274, 558, 28, ammo == 0 ? Danger : Accent, true);
            TextAt(g, AmmoName(weapon.Ammo), 276, 594, 10, Muted);
        }

        TextAt(g, "ARMAS", 412, 547, 10, Muted, true);
        for (int slot = 1; slot <= 6; slot++)
        {
            var owned = player.Weapons.FirstOrDefault(w => w.Slot == slot);
            bool current = owned is not null && owned.Kind == weapon.Kind;
            float x = 412 + (slot - 1) * 32;
            Fill(g, current ? Accent : owned is null ? Color.FromArgb(32, 38, 38) : Color.FromArgb(58, 68, 66),
                x, 560, 27, 26);
            TextAt(g, slot.ToString(), x + 9, 564, 15, current ? Ink : owned is null ? Muted : Paper, true);
        }
        TextAt(g, "1..6 / RUEDA", 412, 594, 10, Muted);

        if (game.Mission is { } mission)
        {
            TextAt(g, "OBJETIVO", 618, 547, 10, Muted, true);
            TextAt(g, $"{mission.Progress * 100:0}%", 616, 560, 24, Accent, true);
            TextAt(g, game.CanExit ? "SALIDA LISTA" : "EN CURSO", 618, 594, 10, Muted);
        }
        else
        {
            TextAt(g, "ACCESO", 618, 547, 10, Muted, true);
            DrawKey(g, 618, player.Inventory.Contains("Tarjeta Roja"), Color.IndianRed, "R");
            DrawKey(g, 652, player.Inventory.Contains("Tarjeta Azul"), Color.SteelBlue, "A");
            DrawKey(g, 686, player.Inventory.Contains("Tarjeta Amarilla"), Color.Goldenrod, "M");
        }

        TextAt(g, "BAJAS", 734, 547, 10, Muted, true);
        TextAt(g, $"{Math.Min(game.DefeatedEnemies, game.TotalEnemies)}/{game.TotalEnemies}", 732, 560, 22, Paper, true);
        TextAt(g, game.Mission is null ? $"META {game.RequiredKills}" : "COMBATE", 734, 594, 10, Muted);

        // Con objetivo cronometrado (fuga que sella, reactor que se separa,
        // impacto contra el planeta) se muestra la cuenta atrás real de la
        // misión en vez del tiempo total jugado, y en rojo cuando queda poco.
        var missionState = game.Mission;
        bool countdown = missionState is { Timed: true, Failed: false };
        int secondsLeft = countdown ? missionState!.RemainingSeconds : (int)(game.ElapsedFrames / 60);
        var time = TimeSpan.FromSeconds(secondsLeft);
        bool urgent = countdown && secondsLeft <= 30;
        Color timeColor = urgent ? Danger : Paper;
        TextAt(g, countdown ? "CUENTA ATRÁS" : "TIEMPO", 838, 547, 10, urgent ? Danger : Muted, true);
        TextAt(g, $"{(int)time.TotalMinutes:00}:{time.Seconds:00}", 836, 560, 22, timeColor, true);
        TextAt(g, sound.Muted ? "F2 SONIDO OFF" : "F1 · ESC · F2", 838, 594, 10, Muted);
    }

    private void DrawKey(Graphics g, float x, bool owned, Color color, string letter)
    {
        Fill(g, owned ? color : Color.FromArgb(38, 44, 46), x, 560, 30, 26);
        TextAt(g, letter, x + 10, 564, 14, owned ? Ink : Muted, true);
    }

    private static string AmmoName(AmmoKind kind) => kind switch
    {
        AmmoKind.Bullets => "BALAS",
        AmmoKind.Cells => "CELDAS",
        AmmoKind.Rockets => "COHETES",
        _ => "CARTUCHOS"
    };

    // ------------------------------------------------------------------ pantallas

    private static readonly RectangleF HomePlayRect = new(65, 330, 319, 49);
    private static readonly (string Label, RectangleF Rect)[] HomeChips =
    {
        ("F3 · PRÁCTICA", new RectangleF(65, 505, 150, 30)),
        ("F4 · HISTORIAL", new RectangleF(225, 505, 150, 30)),
        ("F5 · JUGADORES", new RectangleF(385, 505, 150, 30)),
        ("F6 · ADMINISTRADOR", new RectangleF(65, 541, 150, 30)),
        ("F7 · CAMBIAR USUARIO", new RectangleF(225, 541, 150, 30)),
        ("ESC · SALIR", new RectangleF(385, 541, 150, 30)),
    };

    private void DrawHome(Graphics g)
    {
        Fill(g, Color.FromArgb(226, 8, 14, 15), 0, 0, 960, 640);
        TextAt(g, "FPS EN C#  /  SIETE MISIONES · UNA HISTORIA", 64, 49, 12, Accent, true);
        TextAt(g, "SECTOR 09", 56, 74, 59, Paper, true);
        string sessionLine = currentProfile is null
            ? "SIN PERFIL · ENTER PARA ELEGIR USUARIO"
            : $"PERFIL · {currentProfile.Name}" + (currentProfile.Role == ProfileRole.Administrador ? "  ·  ADMINISTRADOR" : "");
        TextAt(g, sessionLine, 63, 182, 16, Accent, true);
        if (currentProfile?.Save is not null)
            TextAt(g, $"Partida guardada: misión {currentProfile.Save.LevelIndex + 1} · ENTER para continuar", 64, 206, 13, Color.FromArgb(140, 220, 180));
        TextAt(g, "El reactor falla. La nave cae hacia el planeta.", 65, 244, 17, Paper);
        TextAt(g, "Recupera el mando. Evita la explosión y el impacto.", 65, 269, 17, Paper);
        Fill(g, Accent, HomePlayRect.X, HomePlayRect.Y, HomePlayRect.Width, HomePlayRect.Height);
        TextAt(g, "ENTER  /  CLIC · JUGAR", 86, 344, 15, Ink, true);

        for (int i = 0; i < HomeChips.Length; i++)
        {
            var (label, rect) = HomeChips[i];
            bool locked = i == 0 && currentProfile is not null && !currentProfile.CanSelectAnyLevel;
            Fill(g, locked ? Color.FromArgb(46, 30, 28) : Color.FromArgb(34, 46, 45), rect.X, rect.Y, rect.Width, rect.Height);
            TextAt(g, label, rect.X + 12, rect.Y + 9, 11, locked ? Muted : Accent, true);
        }
        if (homeNoticeFrames > 0)
        {
            Fill(g, Color.FromArgb(210, 40, 20, 18), 65, 580, 700, 26);
            TextAt(g, homeNotice, 78, 585, 12, Color.FromArgb(255, 200, 190), true);
        }

        Fill(g, Color.FromArgb(190, 29, 37, 36), 580, 73, 317, 430);
        TextAt(g, "CONTROLES", 606, 98, 15, Accent, true);
        string[] rows =
        {
            "W A S D     Moverse", "←  →        Girar", "CLIC / F    Disparar",
            "ESPACIO     Saltar", "1 ... 6     Cambiar de arma", "RUEDA/Q/TAB Arma anterior/siguiente",
            "R           Recargar", "E           Interactuar", "SHIFT       Correr",
            "M           Mapa", "ESC         Pausa"
        };
        for (int i = 0; i < rows.Length; i++) TextAt(g, rows[i], 606, 141 + i * 27, 13, Paper);
        TextAt(g, "Ratón: mirar en ambas direcciones", 606, 425, 12, Muted);
        TextAt(g, "RePág / AvPág: mirar · Inicio: centrar", 606, 449, 12, Muted);
    }

    private const int MaxVisibleProfiles = 6;
    private static RectangleF ProfileRowRect(int i) => new(64, 168 + i * 58, 830, 50);
    private static readonly RectangleF NamingConfirmRect = new(64, 268, 180, 36);
    private static readonly RectangleF NamingCancelRect = new(258, 268, 180, 36);

    private void DrawProfilePicker(Graphics g)
    {
        Fill(g, Color.FromArgb(236, 8, 14, 15), 0, 0, 960, 640);
        TextAt(g, "SECTOR 09", 64, 42, 16, Muted, true);
        TextAt(g, pendingAfterProfile == PendingAction.SwitchOnly ? "CAMBIAR DE USUARIO" : "¿QUIÉN JUEGA?", 64, 64, 32, Paper, true);
        TextAt(g, "Perfil local de este equipo · sin correo, sin contraseña, solo un nombre.", 66, 112, 13, Muted);

        if (namingNewProfile)
        {
            Fill(g, Color.FromArgb(28, 36, 36), 64, 168, 830, 90);
            TextAt(g, "NUEVO PERFIL", 88, 182, 12, Accent, true);
            bool cursorOn = (Environment.TickCount / 500) % 2 == 0;
            TextAt(g, newProfileName + (cursorOn ? "_" : ""), 88, 206, 22, Paper, true);
            Fill(g, Accent, NamingConfirmRect.X, NamingConfirmRect.Y, NamingConfirmRect.Width, NamingConfirmRect.Height);
            TextAt(g, "ENTER · CREAR", NamingConfirmRect.X + 30, NamingConfirmRect.Y + 10, 13, Ink, true);
            Fill(g, Color.FromArgb(44, 52, 50), NamingCancelRect.X, NamingCancelRect.Y, NamingCancelRect.Width, NamingCancelRect.Height);
            TextAt(g, "ESC · CANCELAR", NamingCancelRect.X + 26, NamingCancelRect.Y + 10, 13, Paper, true);
            Center(g, "Escribe el nombre (máximo 24 caracteres)", 340, 13, Muted);
            return;
        }

        int visible = Math.Min(profileList.Count, MaxVisibleProfiles);
        if (profileList.Count == 0)
            TextAt(g, "Todavía no hay perfiles en este equipo. Crea el primero abajo.", 64, 150, 13, Muted);
        for (int i = 0; i < visible; i++)
        {
            var p = profileList[i]; bool selected = i == profileIndex;
            var row = ProfileRowRect(i);
            Fill(g, selected ? Accent : Color.FromArgb(28, 36, 36), row.X, row.Y, row.Width, row.Height);
            TextAt(g, p.Name, row.X + 24, row.Y + 14, 18, selected ? Ink : Paper, true);
            if (p.Role == ProfileRole.Administrador)
                TextAt(g, "ADMINISTRADOR", row.X + 640, row.Y + 17, 11, selected ? Ink : Accent, true);
        }
        if (profileList.Count > MaxVisibleProfiles)
            TextAt(g, $"… y {profileList.Count - MaxVisibleProfiles} más", 64, ProfileRowRect(visible).Y + 4, 12, Muted);
        {
            var row = ProfileRowRect(visible);
            bool selected = profileIndex == profileList.Count;
            Fill(g, selected ? Accent : Color.FromArgb(22, 30, 32), row.X, row.Y, row.Width, row.Height);
            TextAt(g, "+  CREAR PERFIL NUEVO", row.X + 24, row.Y + 14, 18, selected ? Ink : Accent, true);
        }
        Center(g, "↑ ↓ o clic · elegir      ENTER o clic · confirmar      ESC · volver", 596, 14, Muted);
    }

    private static RectangleF ContinueRowRect(int i) => new(64, 210 + i * 80, 830, 62);

    private void DrawContinue(Graphics g)
    {
        Fill(g, Color.FromArgb(236, 8, 14, 15), 0, 0, 960, 640);
        TextAt(g, "BIENVENIDO DE VUELTA", 64, 66, 30, Paper, true);
        TextAt(g, currentProfile?.Name ?? "", 66, 112, 16, Accent, true);
        var save = currentProfile?.Save;
        if (save is not null)
            TextAt(g, $"Misión {save.LevelIndex + 1} de {campaign.LevelCount} · {save.Results.Count} completadas · guardado {save.SavedAt.LocalDateTime:g}",
                66, 144, 13, Muted);

        string[] options = { "REANUDAR PARTIDA GUARDADA", "EMPEZAR UNA CAMPAÑA NUEVA" };
        for (int i = 0; i < options.Length; i++)
        {
            bool selected = i == continueIndex;
            var row = ContinueRowRect(i);
            Fill(g, selected ? Accent : Color.FromArgb(28, 36, 36), row.X, row.Y, row.Width, row.Height);
            TextAt(g, options[i], row.X + 24, row.Y + 20, 20, selected ? Ink : Paper, true);
        }
        Center(g, "↑ ↓ o clic · elegir     ENTER o clic · confirmar     ESC · volver", 500, 15, Accent, true);
        TextAt(g, "Empezar de nuevo reemplaza la partida guardada de este perfil.", 64, 540, 12, Muted);
    }

    private void DrawPlayers(Graphics g)
    {
        Fill(g, Color.FromArgb(236, 8, 14, 15), 0, 0, 960, 640);
        TextAt(g, "JUGADORES", 64, 52, 32, Paper, true);
        TextAt(g, "Perfiles locales creados en este equipo, ordenados por última partida.", 66, 96, 13, Muted);
        TextAt(g, "USUARIO", 64, 134, 12, Accent, true);
        TextAt(g, "ROL", 350, 134, 12, Accent, true);
        TextAt(g, "CAMPAÑAS", 460, 134, 12, Accent, true);
        TextAt(g, "MEJOR PUNTOS", 600, 134, 12, Accent, true);
        TextAt(g, "ÚLTIMA PARTIDA", 760, 134, 12, Accent, true);
        Fill(g, Color.FromArgb(60, 72, 70), 64, 154, 830, 1);
        var all = profiles.All;
        if (all.Count == 0) TextAt(g, "Todavía no hay perfiles. Pulsa ENTER en el menú para crear el primero.", 64, 180, 14, Muted);
        for (int i = 0; i < all.Count && i < 14; i++)
        {
            var p = all[i]; float y = 166 + i * 30;
            bool isYou = currentProfile is not null && string.Equals(p.Name, currentProfile.Name, StringComparison.OrdinalIgnoreCase);
            Color c = isYou ? Accent : Paper;
            TextAt(g, p.Name, 64, y, 14, c, isYou);
            TextAt(g, p.Role == ProfileRole.Administrador ? "ADMIN" : "JUGADOR", 350, y, 13, c);
            TextAt(g, p.CampaignsCompleted.ToString(), 460, y, 13, c);
            TextAt(g, p.BestScore.ToString(), 600, y, 13, c);
            TextAt(g, p.LastPlayedAt == default ? "—" : p.LastPlayedAt.LocalDateTime.ToString("dd/MM HH:mm"), 760, y, 13, c);
        }
        Center(g, "ENTER / clic / ESC · volver al menú", 600, 16, Accent, true);
    }

    private static RectangleF AdminRowRect(int i) => new(64, 150 + i * 34, 830, 28);

    private void DrawAdmin(Graphics g)
    {
        Fill(g, Color.FromArgb(236, 10, 14, 20), 0, 0, 960, 640);
        TextAt(g, "PANEL DE ADMINISTRADOR", 64, 52, 28, Paper, true);
        TextAt(g, $"Sesión: {currentProfile?.Name}", 66, 94, 13, Accent, true);
        TextAt(g, "↑ ↓ o clic elegir perfil     R reiniciar su progreso     SUPR eliminarlo     ESC / F6 volver", 66, 116, 12, Muted);
        if (adminList.Count == 0) TextAt(g, "No hay perfiles todavía.", 64, 160, 14, Muted);
        for (int i = 0; i < adminList.Count && i < 12; i++)
        {
            var p = adminList[i]; bool selected = i == adminIndex;
            var row = AdminRowRect(i);
            Fill(g, selected ? Accent : Color.FromArgb(26, 34, 38), row.X, row.Y, row.Width, row.Height);
            Color c = selected ? Ink : Paper;
            TextAt(g, p.Name + (p.Role == ProfileRole.Administrador ? "  (ADMIN)" : ""), row.X + 14, row.Y + 6, 13, c, true);
            TextAt(g, $"Campañas: {p.CampaignsCompleted}   Mejor puntos: {p.BestScore}   Guardado: {(p.Save is null ? "no" : "sí")}",
                row.X + 356, row.Y + 6, 12, c);
        }
        Center(g, "Reiniciar borra el progreso y la partida guardada de ese perfil.", 560, 12, Muted);
    }

    private static RectangleF DifficultyRowRect(int i) => new(64, 170 + i * 96, 830, 80);

    private void DrawDifficulty(Graphics g)
    {
        Fill(g, Color.FromArgb(233, 8, 14, 15), 0, 0, 960, 640);
        TextAt(g, "SELECCIONA LA DIFICULTAD", 64, 70, 30, Paper, true);
        TextAt(g, "Se aplica a toda la campaña. ↑ ↓ o clic para elegir, ENTER o clic para empezar.", 65, 116, 14, Muted);
        for (int i = 0; i < Difficulties.Length; i++)
        {
            var settings = DifficultySettings.For(Difficulties[i]);
            bool selected = i == difficultyIndex;
            var row = DifficultyRowRect(i);
            Fill(g, selected ? Accent : Color.FromArgb(28, 36, 36), row.X, row.Y, row.Width, row.Height);
            TextAt(g, settings.Name, row.X + 24, row.Y + 12, 24, selected ? Ink : Paper, true);
            TextAt(g, settings.Description, row.X + 24, row.Y + 46, 13, selected ? Color.FromArgb(58, 38, 20) : Muted);
            TextAt(g, $"VIDA ENEMIGA x{settings.EnemyHealth:0.00}   DAÑO x{settings.EnemyDamage:0.00}   " +
                      $"SUMINISTROS x{settings.SupplyFactor:0.00}", row.X + 496, row.Y + 12, 12,
                selected ? Color.FromArgb(58, 38, 20) : Muted, true);
        }
        Center(g, "ENTER o clic · comenzar la campaña      ESC · volver", 490, 16, Accent, true);
    }

    private static RectangleF TrainingRowRect(int i) => new(64, 104 + i * 57, 830, 47);

    private void DrawTraining(Graphics g)
    {
        Fill(g, Ink, 0, 0, 960, 640);
        TextAt(g, "PRÁCTICA · ELIGE UNA MISIÓN", 64, 45, 28, Paper, true);
        string[] titles = { "Hangar · comunicaciones", "Energía · red eléctrica", "Bodega · escolta del dron", "Refrigeración · sella la fuga", "Observatorio · transmite la ruta", "Reactor · separación y evacuación", "Puente · evita el impacto" };
        for (int i = 0; i < titles.Length; i++)
        {
            bool selected = i == trainingIndex;
            var row = TrainingRowRect(i);
            Fill(g, selected ? Accent : Color.FromArgb(27, 37, 40), row.X, row.Y, row.Width, row.Height);
            TextAt(g, $"{i + 1}   {titles[i]}", row.X + 24, row.Y + 12, 17, selected ? Ink : Paper, true);
        }
        TextAt(g, "1–7 / flechas / clic: elegir · ENTER o clic: jugar · ESC: volver", 64, 538, 16, Accent);
        TextAt(g, "Dificultad Recluta y equipo inicial. El daño y los objetivos siguen activos.", 64, 580, 13, Muted);
    }

    // Escena entre misiones: barras de cine, una frase a la vez con fundido de
    // entrada. Usa el mismo texto que ya narra la campaña (Debrief y Briefing),
    // solo presentado como una cinemática en vez de una lista de párrafos.
    private void DrawCinematic(Graphics g)
    {
        Fill(g, Color.FromArgb(255, 4, 6, 7), 0, 0, 960, 640);
        Fill(g, Color.Black, 0, 0, 960, 76);
        Fill(g, Color.Black, 0, 564, 960, 76);
        Fill(g, Accent, 0, 76, 960, 2);
        Fill(g, Accent, 0, 562, 960, 2);
        TextAt(g, "SECTOR 09 · LA HISTORIA HASTA AHORA", 64, 30, 13, Muted, true);

        if (cinematicIndex < cinematicLines.Count)
        {
            var font = FontFor(22, true);
            var lines = WrapText(g, cinematicLines[cinematicIndex], font, 760f);
            double t = Math.Min(1.0, cinematicFrame / 30.0);
            int alpha = (int)(255 * t);
            const float lineHeight = 32;
            float startY = 320 - lines.Count * lineHeight / 2;
            foreach (var line in lines)
            {
                float width = g.MeasureString(line, font).Width;
                using var brush = new SolidBrush(Color.FromArgb(alpha, Paper));
                g.DrawString(line, font, brush, (960 - width) / 2, startY);
                startY += lineHeight;
            }
        }
        Center(g, $"{Math.Min(cinematicIndex + 1, cinematicLines.Count)} / {cinematicLines.Count}", 496, 12, Muted);
        Center(g, "ENTER o clic · continuar      ESC · saltar", 520, 14, Accent, true);
    }

    private static List<string> WrapText(Graphics g, string text, Font font, float maxWidth)
    {
        var lines = new List<string>();
        var current = "";
        foreach (var word in text.Split(' '))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (g.MeasureString(candidate, font).Width > maxWidth && current.Length > 0)
            { lines.Add(current); current = word; }
            else current = candidate;
        }
        if (current.Length > 0) lines.Add(current);
        return lines;
    }

    private void DrawBriefing(Graphics g)
    {
        var info = campaign.Info;
        Fill(g, Color.FromArgb(238, 8, 14, 15), 0, 0, 960, 640);
        TextAt(g, $"MISIÓN {info.Number} DE {campaign.LevelCount}", 64, 66, 13, Accent, true);
        TextAt(g, info.Name, 64, 88, 38, Paper, true);
        TextAt(g, info.Subtitle, 66, 138, 16, Muted);
        Fill(g, Accent, 64, 172, 120, 3);
        for (int i = 0; i < info.Briefing.Count; i++)
            TextAt(g, info.Briefing[i], 64, 200 + i * 32, 16, Paper);

        Fill(g, Color.FromArgb(28, 36, 36), 64, 330, 830, 96);
        TextAt(g, "EQUIPO ACTUAL", 88, 344, 12, Accent, true);
        var player = Game.Player;
        TextAt(g, $"SALUD {player.Health}    ARMADURA {player.Armor}    " +
                  $"BALAS {player.AmmoOf(AmmoKind.Bullets)}    CARTUCHOS {player.AmmoOf(AmmoKind.Shells)}    " +
                  $"CELDAS {player.AmmoOf(AmmoKind.Cells)}    COHETES {player.AmmoOf(AmmoKind.Rockets)}",
            88, 366, 15, Paper, true);
        TextAt(g, "ARMAS: " + string.Join("   ", player.Weapons.Select(w => w.Slot + " " + w.ShortName)),
            88, 394, 13, Muted);

        TextAt(g, $"AMENAZAS: {Game.TotalEnemies}    ·    COMPLETA EL OBJETIVO PARA AVANZAR" +
                  $"    ·    DIFICULTAD: {campaign.Difficulty.Name}", 64, 448, 14, Muted, true);
        Center(g, "ENTER o clic · desplegar en una zona segura", 496, 20, Accent, true);
    }

    private void DrawDebrief(Graphics g)
    {
        Fill(g, Color.FromArgb(236, 8, 14, 15), 0, 0, 960, 640);
        var result = lastResult;
        TextAt(g, "MISIÓN COMPLETADA", 64, 74, 34, Paper, true);
        TextAt(g, campaign.Info.Debrief, 64, 126, 17, Accent);

        if (result is not null)
        {
            Fill(g, Color.FromArgb(28, 36, 36), 64, 186, 830, 120);
            string[] labels = { "BAJAS", "OBJETOS", "TIEMPO", "PUNTOS" };
            string[] values =
            {
                $"{result.Kills}/{result.TotalEnemies}",
                $"{result.ItemsFound}/{result.ItemsTotal}",
                $"{result.Seconds / 60:00}:{result.Seconds % 60:00}",
                result.Score.ToString()
            };
            for (int i = 0; i < labels.Length; i++)
            {
                float x = 96 + i * 205;
                TextAt(g, labels[i], x, 206, 12, Muted, true);
                TextAt(g, values[i], x, 228, 34, Paper, true);
            }
            TextAt(g, $"Tiempo previsto de la misión: {campaign.Info.ParSeconds / 60:00}:{campaign.Info.ParSeconds % 60:00}",
                96, 278, 12, Muted);
        }

        TextAt(g, "PROGRESO DE LA CAMPAÑA", 64, 330, 12, Accent, true);
        for (int i = 0; i < campaign.LevelCount; i++)
        {
            bool done = campaign.Results.Any(r => r.LevelIndex == i);
            Fill(g, done ? Accent : Color.FromArgb(34, 42, 42), 64 + i * 62, 352, 54, 12);
        }
        TextAt(g, $"PUNTUACIÓN TOTAL {campaign.TotalScore}    ·    BAJAS TOTALES {campaign.TotalKills}",
            64, 386, 15, Paper, true);
        Center(g, "ENTER o clic · siguiente misión (guardado automáticamente)", 496, 20, Accent, true);
    }

    private void DrawCampaignEnd(Graphics g)
    {
        Fill(g, Color.FromArgb(242, 8, 14, 15), 0, 0, 960, 640);
        Center(g, campaign.TrainingMode ? "PRÁCTICA COMPLETADA" : "NAVE A SALVO", 68, 40, Paper, true);
        Center(g, campaign.Info.Debrief, 124, 17, Accent);
        Center(g, campaign.TrainingMode ? "Práctica terminada. Elige otra misión desde F3." : "La nave cambia de rumbo. El planeta está a salvo.", 154, 14, Muted);

        TextAt(g, "RESUMEN POR MISIÓN", 128, 196, 12, Accent, true);
        TextAt(g, "MISIÓN", 128, 218, 12, Muted, true);
        TextAt(g, "BAJAS", 500, 218, 12, Muted, true);
        TextAt(g, "TIEMPO", 610, 218, 12, Muted, true);
        TextAt(g, "PUNTOS", 726, 218, 12, Muted, true);
        var results = campaign.Results;
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            float y = 240 + i * 26;
            TextAt(g, $"{r.LevelIndex + 1}. {r.LevelName}", 128, y, 13, Paper);
            TextAt(g, $"{r.Kills}/{r.TotalEnemies}", 500, y, 13, Paper);
            TextAt(g, $"{r.Seconds / 60:00}:{r.Seconds % 60:00}", 610, y, 13, Paper);
            TextAt(g, r.Score.ToString(), 726, y, 13, Paper);
        }
        int total = campaign.TotalSeconds;
        Center(g, $"TOTAL   ·   {campaign.TotalKills} BAJAS   ·   {total / 60:00}:{total % 60:00}   ·   " +
                  $"{campaign.TotalScore} PUNTOS   ·   {campaign.Difficulty.Name}", 452, 17, Accent, true);
        Center(g, "ENTER o clic · volver al menú", 496, 18, Paper, true);
    }

    private static RectangleF PauseRowRect(int i) => new(260, 190 + i * 56, 440, 44);
    private static RectangleF SettingsRowRect(int i) => new(230, 195 + i * 52, 500, 40);

    private void DrawPause(Graphics g)
    {
        Fill(g, Color.FromArgb(232, 8, 12, 14), 0, 0, 960, 640);
        Fill(g, Color.FromArgb(232, 15, 22, 24), 210, 90, 540, 460);
        Fill(g, Accent, 210, 90, 540, 4);
        Center(g, showHelp ? "CONTROLES" : showSettings ? "CONFIGURACIÓN" : "MISIÓN EN PAUSA", 112, 28, Paper, true);
        Center(g, $"{campaign.Info.Name}  ·  misión {campaign.Info.Number} de {campaign.LevelCount}", 150, 14, Accent);

        if (showHelp)
        {
            Center(g, "WASD · mover    |    Flechas ← → · girar    |    Shift · correr", 200, 15, Paper);
            Center(g, "Clic / F · disparar    |    Espacio · saltar    |    E · interactuar    |    R · recargar", 230, 15, Paper);
            Center(g, "1 a 6 · elegir arma    |    Rueda, Q o Tab · cambiar de arma", 260, 15, Paper);
            Center(g, "Ratón: mirar libremente · RePág/AvPág: vertical · Inicio: centrar", 290, 15, Paper);
            Center(g, "M · mapa    |    F2 · sonido", 320, 15, Paper);
            Center(g, "F1 / ESC / clic · volver", 400, 16, Accent, true);
        }
        else if (showSettings)
        {
            string[] labels =
            {
                $"SENSIBILIDAD DEL RATÓN      {mouseSensitivity:0.0}x    ( ← / →  o clic izq./der. )",
                $"SUBTÍTULOS      {(subtitlesEnabled ? "ACTIVADOS" : "DESACTIVADOS")}",
                $"SONIDO      {(sound.Muted ? "SILENCIADO" : "ACTIVADO")}",
                "◀  VOLVER AL MENÚ DE PAUSA"
            };
            for (int i = 0; i < labels.Length; i++)
            {
                var row = SettingsRowRect(i);
                Fill(g, i == 3 ? Color.FromArgb(46, 40, 30) : Color.FromArgb(32, 40, 42), row.X, row.Y, row.Width, row.Height);
                TextAt(g, labels[i], row.X + 16, row.Y + 11, 13, i == 3 ? Accent : Paper, i == 3);
            }
            Center(g, $"Dificultad de la campaña:  {campaign.Difficulty.Name}", 412, 13, Muted);
            Center(g, "Cada fila también responde al clic del ratón", 436, 12, Muted);
        }
        else
        {
            for (int i = 0; i < PauseLabels.Length; i++)
            {
                bool selected = i == pauseIndex;
                var row = PauseRowRect(i);
                Fill(g, selected ? Accent : Color.FromArgb(32, 40, 42), row.X, row.Y, row.Width, row.Height);
                TextAt(g, PauseLabels[i], row.X + 24, row.Y + 13, 16, selected ? Ink : Paper, true);
            }
            Center(g, "↑ ↓ o clic · elegir     ENTER o clic · confirmar     ESC · reanudar", 432, 14, Muted);
            Center(g, "F1 · controles      La partida también se pausa al cambiar de ventana.", 458, 12, Muted);
        }
    }

    private void DrawDefeat(Graphics g)
    {
        Fill(g, Color.FromArgb(232, 22, 8, 8), 0, 0, 960, 640);
        Center(g, "MISIÓN FALLIDA", 120, 40, Danger, true);
        Center(g, Game.Mission?.Failed == true ? Game.Mission?.Plan.Kind == MissionKind.BossShield ? "IMPACTO. No recuperaste el timón a tiempo." : "EXPLOSIÓN. No estabilizaste el módulo a tiempo." : "Tu salud llegó a cero. El sector sigue comprometido.", 186, 17, Paper);
        Center(g, $"Caíste en la misión {campaign.Info.Number}: {campaign.Info.Name}", 218, 15, Muted);
        Center(g, $"BAJAS  {Math.Min(Game.DefeatedEnemies, Game.TotalEnemies)}/{Game.TotalEnemies}" +
                  $"     ·     TIEMPO  {Game.ElapsedFrames / 60}s     ·     PUNTOS  {Game.Score}", 268, 17, Accent, true);
        Center(g, "ENTER, R o clic · repetir la misión con el equipo con el que entraste", 344, 17, Paper, true);
        Center(g, "ESC · pausa    ·    La campaña no vuelve a empezar desde cero.", 380, 13, Muted);
    }

    private void DrawMap(Graphics g)
    {
        var game = Game;
        int size = Math.Max(6, Math.Min(14, 330 / Math.Max(game.World.Width, game.World.Height)));
        int width = game.World.Width * size, height = game.World.Height * size;
        int ox = 940 - width, oy = 66;
        Fill(g, Color.FromArgb(240, 12, 18, 19), ox - 12, oy - 12, width + 24, height + 62);
        for (int y = 0; y < game.World.Height; y++)
            for (int x = 0; x < game.World.Width; x++)
            {
                char tile = game.World.Tile(x, y);
                Color c = game.World.IsSolid(x, y) ? Color.FromArgb(72, 81, 79) : Color.FromArgb(24, 32, 32);
                if (tile == 'R') c = game.World.IsSolid(x, y) ? Color.IndianRed : Color.FromArgb(68, 38, 36);
                if (tile == 'B') c = game.World.IsSolid(x, y) ? Color.SteelBlue : Color.FromArgb(29, 50, 69);
                if (tile == 'Y') c = game.World.IsSolid(x, y) ? Color.Goldenrod : Color.FromArgb(66, 58, 26);
                if (tile == 'W') c = Color.FromArgb(72, 155, 191);
                if (tile == 'X') c = game.CanExit ? Color.MediumSeaGreen : Color.IndianRed;
                Fill(g, c, ox + x * size, oy + y * size, size - 1, size - 1);
            }
        foreach (var item in game.World.Pickups)
        {
            if (item.Collected) continue;
            Color c = item.Kind switch
            {
                PickupKind.RedKey => Color.Salmon,
                PickupKind.BlueKey => Color.DeepSkyBlue,
                PickupKind.YellowKey => Color.Gold,
                PickupKind.Health or PickupKind.MegaHealth => Color.White,
                PickupKind.Armor => Color.MediumSeaGreen,
                _ => item.IsWeapon ? Color.Orange : Color.DarkKhaki
            };
            float s = item.IsWeapon || item.Kind is PickupKind.RedKey or PickupKind.BlueKey or PickupKind.YellowKey ? 6 : 4;
            Fill(g, c, ox + (float)item.X * size - s / 2, oy + (float)item.Y * size - s / 2, s, s);
        }
        foreach (var enemy in game.World.Enemies)
        {
            if (!enemy.IsAlive) continue;
            float s = enemy.Kind == EnemyKind.Ciberdemonio ? 10 : 5;
            Fill(g, enemy.Kind == EnemyKind.Ciberdemonio ? Color.Magenta : Color.OrangeRed,
                ox + (float)enemy.X * size - s / 2, oy + (float)enemy.Y * size - s / 2, s, s);
        }
        if (game.Mission is { } mission)
        {
            foreach (var hazard in mission.Plan.Hazards)
                Fill(g, mission.ElectricityOn ? Color.OrangeRed : Color.DeepSkyBlue,
                    ox + (float)hazard.X * size - 2, oy + (float)hazard.Y * size - 2, 4, 4);
            for (int i = 0; i < mission.Nodes.Count; i++)
            {
                var node = mission.Nodes[i];
                double x = mission.Plan.Kind == MissionKind.Escort ? mission.DroneX : node.Point.X;
                double y = mission.Plan.Kind == MissionKind.Escort ? mission.DroneY : node.Point.Y;
                Fill(g, node.Activated ? Color.SeaGreen : Color.Cyan, ox + (float)x * size - 5, oy + (float)y * size - 5, 10, 10);
                TextAt(g, (i + 1).ToString(), ox + (float)x * size - 3, oy + (float)y * size - 7, 10, Color.Black, true);
            }
        }
        float px = ox + (float)game.Player.X * size, py = oy + (float)game.Player.Y * size;
        using var brush = new SolidBrush(Paper); g.FillEllipse(brush, px - 4, py - 4, 8, 8);
        using var pen = new Pen(Paper, 2);
        g.DrawLine(pen, px, py, px + (float)Math.Cos(game.Player.Angle) * 13, py + (float)Math.Sin(game.Player.Angle) * 13);
        TextAt(g, "● TÚ   ■ ROJO: ENEMIGO   ■ CIAN: OBJETIVO", ox, oy + height + 8, 10, Paper);
        TextAt(g, "M · CERRAR  /  EL JUEGO CONTINÚA", ox, oy + height + 26, 10, Accent);
    }

    // ------------------------------------------------------------------ utilidades

    private Font FontFor(int size, bool bold)
    {
        if (!fonts.TryGetValue((size, bold), out var font))
        {
            font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            fonts.Add((size, bold), font);
        }
        return font;
    }

    private void TextAt(Graphics g, string text, float x, float y, int size, Color color, bool bold = false)
    { using var brush = new SolidBrush(color); g.DrawString(text, FontFor(size, bold), brush, x, y); }

    private void Center(Graphics g, string text, float y, int size, Color color, bool bold = false)
    { float width = g.MeasureString(text, FontFor(size, bold)).Width; TextAt(g, text, (960 - width) / 2, y, size, color, bold); }

    private static void Fill(Graphics g, Color color, float x, float y, float width, float height)
    { using var brush = new SolidBrush(color); g.FillRectangle(brush, x, y, width, height); }
}
