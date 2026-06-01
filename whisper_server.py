import socket
import os
import threading
import whisper
import sys
import time
from openai import OpenAI

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

client = OpenAI(api_key="")

print("[INFO] Cargando Whisper...")
model = whisper.load_model("base", device="cpu")

def handle_client(conn, addr):
    try:
        data = conn.recv(1024).decode('utf-8').strip()

        if not data.startswith("TRANSCRIBE"):
            conn.sendall("FAIL".encode("utf-8"))
            return

        # ── Extraer carpeta de trabajo enviada por Unity ──────────────────
        partes = data.split("|")
        if len(partes) > 1:
            carpeta = partes[1]
        else:
            carpeta = SCRIPT_DIR   # fallback: carpeta del propio script

        AUDIO_FILENAME         = os.path.join(carpeta, "voz.wav")
        TRANSCRIPTION_FILENAME = os.path.join(carpeta, "voz_transcrita.txt")
        ACTION_FILENAME        = os.path.join(carpeta, "boss_action.txt")
        BOSS_VOICE_FILENAME    = os.path.join(carpeta, "boss_voice.mp3")

        print(f"[INFO] Carpeta de trabajo: {carpeta}")
        print(f"[INFO] Leyendo audio de:   {AUDIO_FILENAME}")

        # ── Transcripción con Whisper ──────────────────────────────────────
        result = model.transcribe(AUDIO_FILENAME, language="es")
        texto_usuario = result["text"].strip() or "..."
        with open(TRANSCRIPTION_FILENAME, "w", encoding="utf-8") as f:
            f.write(texto_usuario)

        # ── Clasificación de intención (PROMPT V6) ─────────────────────────
        prompt_accion = f"""Eres el clasificador de intención de un jefe final en un videojuego de acción. Analizas frases del jugador en español (incluyendo argot, palabrotas y expresiones coloquiales) y decides la acción que ejecutará el jefe.

ACCIONES VÁLIDAS:
VerticalAtack | HorizontalAtack | Block | Shoot | Heal | Action

GUÍA DE CLASIFICACIÓN:
- VerticalAtack: rabia pura, gritos, insultos directos al jefe. El jugador pierde los nervios.
- HorizontalAtack: provocación, chulería, retos. El jugador se cree superior y desafía.
- Block: el jugador declara su intención de atacar físicamente. Hay un verbo de ataque hacia el jefe.
- Shoot: ironía, sarcasmo, comentarios distantes. El jugador desprecia sin atacar.
- Heal: súplicas, miedo, sumisión, peticiones de piedad.
- Action: mensaje sin carga emocional clara, saludos, preguntas neutras o frases sin sentido.

EJEMPLOS POSITIVOS (intención clara):
"Eres un puto mierda" -> VerticalAtack
"Cabrón, te odio" -> VerticalAtack
"A ver si tienes huevos de pelear" -> HorizontalAtack
"Venga, atrévete cobarde" -> HorizontalAtack
"Voy a partirte la cara" -> Block
"Te voy a destrozar con mi espada" -> Block
"Vaya, qué miedo das con esa pinta" -> Shoot
"Bonito vestido, ¿lo eligió tu madre?" -> Shoot
"Por favor, no me hagas daño" -> Heal
"Déjame vivir, te lo suplico" -> Heal
"Hola, qué tal" -> Action
"No sé qué decir" -> Action

CASOS LÍMITE:
- Si la frase mezcla súplica con insulto, prioriza Heal solo si la súplica es dominante. Si no, VerticalAtack.
- Si la frase es sarcástica pero contiene una amenaza física, prioriza Block.
- Si la frase es muy corta (una sola palabra), interpreta según el tono: "¡Imbécil!" -> VerticalAtack, "Hola" -> Action.
- Si Whisper devuelve algo ininteligible o muy raro, devuelve Action.

REGLAS DE SALIDA:
1. Responde con UNA SOLA PALABRA, exactamente igual que en la lista de acciones válidas.
2. Sin explicaciones, sin comillas, sin puntuación.
3. Respeta las mayúsculas y minúsculas exactas (VerticalAtack, no verticalatack).

Frase del jugador: '{texto_usuario}'

Acción:"""

        res_accion = client.chat.completions.create(
            model="gpt-4o-mini",
            messages=[{"role": "user", "content": prompt_accion}],
            temperature=0.3
        )
        accion = res_accion.choices[0].message.content.strip()
        print(f"[INFO] Frase: '{texto_usuario}' -> Acción: '{accion}'")

        # ── Respuesta épica del boss ───────────────────────────────────────
        res_frase = client.chat.completions.create(
            model="gpt-4o-mini",
            messages=[{"role": "user", "content": f"Eres un jefe final. Vas a hacer {accion} porque el jugador dijo '{texto_usuario}'. Di algo corto y épico (max 8 palabras)."}],
            temperature=0.7
        )
        texto_boss = res_frase.choices[0].message.content.strip()

        # ── TTS: voz del boss ──────────────────────────────────────────────
        if os.path.exists(BOSS_VOICE_FILENAME):
            os.remove(BOSS_VOICE_FILENAME)

        tts_response = client.audio.speech.create(
            model="tts-1",
            voice="onyx",
            input=texto_boss
        )
        tts_response.stream_to_file(BOSS_VOICE_FILENAME)

        # ── Escribir acción para que Unity la lea ─────────────────────────
        with open(ACTION_FILENAME, "w", encoding="utf-8") as f:
            f.write(accion)

        time.sleep(0.5)
        conn.sendall("OK".encode("utf-8"))

    except Exception as e:
        print(f"[ERROR]: {e}")
        conn.sendall("FAIL".encode("utf-8"))
    finally:
        conn.close()

def start_server():
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server.bind(("127.0.0.1", 65510))
    server.listen(5)
    print("[INFO] Servidor V6 listo en puerto 65510...")
    while True:
        conn, addr = server.accept()
        threading.Thread(target=handle_client, args=(conn, addr)).start()

if __name__ == "__main__":
    start_server()