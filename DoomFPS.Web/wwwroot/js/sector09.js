// Sector 09 · capa de navegador.
// Recibe del C# (WebAssembly) el fotograma 640×360 del raycaster y una lista de
// órdenes de dibujo del HUD, y le devuelve teclado, ratón y el reloj.
// Lienzo lógico: 960×640 (igual que la ventana de escritorio).

const W = 960, H = 640, FW = 640, FH = 360;
const FIELD = '\u001f', RECORD = '\u001e';

let canvas, ctx, frameCanvas, frameCtx, frameImage, frameBytes, frameU32;
let mouseDX = 0, mouseDY = 0, started = false;
let audio = null, master = null, muted = false;
const rawSounds = new Map(), buffers = new Map();
let speechQueued = 0, spanishVoice = null;

// Teclas cuyo comportamiento por defecto del navegador se anula (desplazar la
// página, buscar, recargar, ayuda, tabulador...).
const BLOCKED = new Set(['Space', 'Tab', 'Backspace', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight',
  'PageUp', 'PageDown', 'Home', 'F1', 'F2', 'F3', 'F4', 'F5', 'F6', 'F7', 'Enter', 'NumpadEnter', 'Delete']);

export function init(onFrame, onEvent) {
  canvas = document.getElementById('screen');
  ctx = canvas.getContext('2d', { alpha: false });
  frameCanvas = document.createElement('canvas');
  frameCanvas.width = FW; frameCanvas.height = FH;
  frameCtx = frameCanvas.getContext('2d', { alpha: false });
  frameImage = frameCtx.createImageData(FW, FH);
  frameBytes = new Uint8Array(frameImage.data.buffer);
  frameU32 = new Uint32Array(frameImage.data.buffer);

  resize();
  window.addEventListener('resize', resize);

  window.addEventListener('keydown', e => {
    if (e.ctrlKey || e.metaKey || e.altKey) return; // deja libres los atajos del navegador
    if (BLOCKED.has(e.code) || e.code.startsWith('Key') || e.code.startsWith('Digit')) e.preventDefault();
    unlockAudio();
    onEvent('keydown', e.code, e.repeat ? 1 : 0);
    if (e.key && e.key.length === 1) onEvent('char', e.key, 0);
  });
  window.addEventListener('keyup', e => { onEvent('keyup', e.code, 0); });

  canvas.addEventListener('mousedown', e => { e.preventDefault(); unlockAudio(); onEvent('mousedown', toVirtual(e), e.button); });
  window.addEventListener('mouseup', e => onEvent('mouseup', '', e.button));
  canvas.addEventListener('click', e => onEvent('click', toVirtual(e), e.button));
  canvas.addEventListener('contextmenu', e => e.preventDefault());
  canvas.addEventListener('wheel', e => { e.preventDefault(); onEvent('wheel', '', -Math.sign(e.deltaY)); }, { passive: false });
  window.addEventListener('mousemove', e => {
    if (document.pointerLockElement === canvas) { mouseDX += e.movementX; mouseDY += e.movementY; }
  });
  document.addEventListener('pointerlockchange', () =>
    onEvent('pointerlock', '', document.pointerLockElement === canvas ? 1 : 0));
  window.addEventListener('blur', () => onEvent('blur', '', 0));
  document.addEventListener('visibilitychange', () => { if (document.hidden) onEvent('blur', '', 0); });

  const loop = t => {
    const dx = mouseDX, dy = mouseDY; mouseDX = 0; mouseDY = 0;
    onFrame(t, dx, dy);
    requestAnimationFrame(loop);
  };
  if (!started) { started = true; requestAnimationFrame(loop); }
}

export function ready() {
  const loading = document.getElementById('loading');
  if (loading) loading.remove();
  canvas.focus();
}

// Ajusta el canvas a la ventana manteniendo la proporción 3:2 y nitidez en HiDPI.
function resize() {
  const dpr = Math.max(1, Math.min(window.devicePixelRatio || 1, 2));
  const scale = Math.min(window.innerWidth / W, window.innerHeight / H);
  const cssW = Math.floor(W * scale), cssH = Math.floor(H * scale);
  canvas.style.width = cssW + 'px'; canvas.style.height = cssH + 'px';
  canvas.width = Math.round(cssW * dpr); canvas.height = Math.round(cssH * dpr);
  ctx.setTransform(canvas.width / W, 0, 0, canvas.height / H, 0, 0);
}

function toVirtual(e) {
  const r = canvas.getBoundingClientRect();
  const x = (e.clientX - r.left) / r.width * W, y = (e.clientY - r.top) / r.height * H;
  return x.toFixed(1) + ',' + y.toFixed(1);
}

export function present(view, commands) {
  // Píxeles ARGB de .NET (bytes B,G,R,A) → RGBA de ImageData.
  view.copyTo(frameBytes);
  const px = frameU32;
  for (let i = 0; i < px.length; i++) {
    const p = px[i];
    px[i] = 0xff000000 | (p & 0x0000ff00) | ((p & 0xff) << 16) | ((p >>> 16) & 0xff);
  }
  frameCtx.putImageData(frameImage, 0, 0);

  ctx.fillStyle = '#000';
  ctx.fillRect(0, 0, W, H);
  ctx.imageSmoothingEnabled = false;
  ctx.drawImage(frameCanvas, 0, 0, 960, 540);
  ctx.imageSmoothingEnabled = true;
  ctx.textBaseline = 'top';

  if (!commands) return;
  const list = commands.split(RECORD);
  for (let i = 0; i < list.length; i++) {
    const f = list[i].split(FIELD);
    switch (f[0]) {
      case 'R': ctx.fillStyle = f[5]; ctx.fillRect(+f[1], +f[2], +f[3], +f[4]); break;
      case 'T': ctx.font = f[3]; ctx.fillStyle = f[4]; ctx.fillText(f.slice(5).join(' '), +f[1], +f[2]); break;
      case 'L':
        ctx.strokeStyle = f[6]; ctx.lineWidth = +f[5];
        ctx.beginPath(); ctx.moveTo(+f[1], +f[2]); ctx.lineTo(+f[3], +f[4]); ctx.stroke(); break;
      case 'E':
        ctx.fillStyle = f[5]; ctx.beginPath();
        ctx.ellipse(+f[1] + f[3] / 2, +f[2] + f[4] / 2, f[3] / 2, f[4] / 2, 0, 0, Math.PI * 2); ctx.fill(); break;
    }
  }
}

export function measure(font, text) {
  ctx.font = font;
  return ctx.measureText(text).width;
}

// ------------------------------------------------------------------ audio
export function loadSound(name, view) { rawSounds.set(name, view.slice()); }

// Los navegadores solo permiten sonar tras un gesto del usuario.
function unlockAudio() {
  if (!audio) {
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;
    audio = new AC();
    master = audio.createGain(); master.gain.value = 0.9; master.connect(audio.destination);
    for (const [name, bytes] of rawSounds) {
      const copy = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
      audio.decodeAudioData(copy).then(buf => buffers.set(name, buf)).catch(() => {});
    }
  }
  if (audio.state === 'suspended') audio.resume();
}

export function playSound(name) {
  if (muted || !audio) return;
  const buf = buffers.get(name);
  if (!buf) return;
  const src = audio.createBufferSource();
  src.buffer = buf; src.connect(master); src.start();
}

export function setMuted(m) {
  muted = !!m;
  if (muted && 'speechSynthesis' in window) { speechSynthesis.cancel(); speechQueued = 0; }
}

// Voz de la IA de la nave: Web Speech, en español y con voz femenina si existe.
function pickVoice() {
  if (spanishVoice || !('speechSynthesis' in window)) return spanishVoice;
  const voices = speechSynthesis.getVoices().filter(v => v.lang && v.lang.toLowerCase().startsWith('es'));
  spanishVoice = voices.find(v => /female|mujer|helena|laura|monica|paulina|sabina|elvira|dalia|google español/i.test(v.name)) || voices[0] || null;
  return spanishVoice;
}

export function speak(text) {
  if (muted || !('speechSynthesis' in window)) return;
  // Como en escritorio: si se acumulan avisos, se conservan solo los recientes.
  if (speechQueued >= 2) { speechSynthesis.cancel(); speechQueued = 0; }
  const u = new SpeechSynthesisUtterance(text);
  u.lang = 'es-ES'; u.rate = 1.03; u.pitch = 1.15;
  const v = pickVoice(); if (v) u.voice = v;
  u.onend = u.onerror = () => { speechQueued = Math.max(0, speechQueued - 1); };
  speechQueued++;
  speechSynthesis.speak(u);
}

// ------------------------------------------------------------------ almacenamiento y ratón
export function storageGet(key) { try { return localStorage.getItem(key); } catch { return null; } }
export function storageSet(key, value) { try { localStorage.setItem(key, value); } catch { } }

export function lockPointer() {
  try { const r = canvas.requestPointerLock(); if (r && r.catch) r.catch(() => {}); } catch { }
}
export function unlockPointer() {
  try { if (document.pointerLockElement) document.exitPointerLock(); } catch { }
}
