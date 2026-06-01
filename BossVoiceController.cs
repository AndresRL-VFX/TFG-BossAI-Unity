using UnityEngine;
using UnityEngine.EventSystems;
using System.IO;
using System.Collections;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using UnityEngine.Networking;

public class BossVoiceController : MonoBehaviour
{
    public Animator bossAnimator;
    public GameObject panelProcesando;
    public AudioSource bossAudioSource;

    private AudioClip recordedClip;
    private string clipsFolder;
    private string pathVoz;
    private string pathAccion;
    private string pathTranscripcion;
    private string pathRespuestaAudio;

    private int puertoServidor = 65510;
    private Process pythonServerProcess;

    [Header("Debug UI")]
    public DebugUI debugUI;

    void Start()
    {
        clipsFolder = Path.Combine(Application.streamingAssetsPath, "Clips");

        UnityEngine.Debug.Log("[DEBUG] Carpeta de trabajo: " + clipsFolder);

        if (!Directory.Exists(clipsFolder))
            Directory.CreateDirectory(clipsFolder);

        pathVoz = Path.Combine(clipsFolder, "voz.wav");
        pathAccion = Path.Combine(clipsFolder, "boss_action.txt");
        pathTranscripcion = Path.Combine(clipsFolder, "voz_transcrita.txt");
        pathRespuestaAudio = Path.Combine(clipsFolder, "boss_voice.mp3");

        IniciarServidorSiNoExiste();
    }

    public void StartVoiceInteraction()
    {
        StartCoroutine(GrabarYProcesarVoz());
    }

    IEnumerator GrabarYProcesarVoz()
    {
        if (panelProcesando != null) panelProcesando.SetActive(true);

        if (debugUI != null) debugUI.ActualizarEstadoSistema("Iniciando grabación...", Color.yellow);

        if (Microphone.devices.Length == 0)
        {
            UnityEngine.Debug.LogError("Unity no detecta ningún micrófono.");
            if (debugUI != null) debugUI.ActualizarEstadoSistema("ERROR: No hay micrófono", Color.red);
            if (panelProcesando != null) panelProcesando.SetActive(false);
            yield break;
        }

        string micSeleccionado = Microphone.devices[0];
        UnityEngine.Debug.Log($"Micrófono detectado: {micSeleccionado}");

        int frecuenciaAUsar = 16000;
        recordedClip = Microphone.Start(micSeleccionado, false, 5, frecuenciaAUsar);

        float tiempoEsperaMicro = 0f;
        while (!(Microphone.GetPosition(micSeleccionado) > 0) && tiempoEsperaMicro < 2f)
        {
            tiempoEsperaMicro += Time.deltaTime;
            yield return null;
        }

        if (tiempoEsperaMicro >= 2f)
        {
            UnityEngine.Debug.LogError($"El micrófono {micSeleccionado} no arrancó.");
            if (debugUI != null) debugUI.ActualizarEstadoSistema("ERROR: Micrófono no arranca", Color.red);
            if (panelProcesando != null) panelProcesando.SetActive(false);
            yield break;
        }

        if (debugUI != null) debugUI.ActualizarEstadoSistema("Grabando voz (5s)...", Color.cyan);
        UnityEngine.Debug.Log("Grabando...");
        yield return new WaitForSeconds(5);

        Microphone.End(micSeleccionado);
        UnityEngine.Debug.Log($"Grabación finalizada. Muestras: {recordedClip.samples}");

        if (recordedClip == null || recordedClip.samples == 0)
        {
            UnityEngine.Debug.LogError("ERROR: 0 muestras grabadas.");
            if (debugUI != null) debugUI.ActualizarEstadoSistema("ERROR: Silencio detectado", Color.red);
            if (panelProcesando != null) panelProcesando.SetActive(false);
            yield break;
        }

        if (File.Exists(pathVoz)) File.Delete(pathVoz);
        SavWav.Save(pathVoz, recordedClip);

        float espera = 0f;
        while ((!File.Exists(pathVoz) || new FileInfo(pathVoz).Length == 0) && espera < 5f)
        {
            yield return new WaitForSeconds(0.1f);
            espera += 0.1f;
        }

        if (debugUI != null) debugUI.ActualizarEstadoSistema("Enviando audio al servidor...", Color.yellow);

        Task<string> task = EnviarTranscripcionAsync();
        while (!task.IsCompleted) yield return null;

        string respuesta = task.Result;

        if (respuesta != "OK")
        {
            UnityEngine.Debug.LogError("El servidor Python falló o no respondió OK. Respuesta: " + respuesta);
            if (debugUI != null) debugUI.ActualizarEstadoSistema("ERROR: Servidor Python no responde", Color.red);
            if (panelProcesando != null) panelProcesando.SetActive(false);
            yield break;
        }

        if (debugUI != null) debugUI.ActualizarEstadoSistema("Procesando respuesta del LLM...", Color.yellow);

        float tiempoMax = 3f;
        float tiempo = 0f;
        while (!File.Exists(pathTranscripcion) && tiempo < tiempoMax)
        {
            yield return new WaitForSeconds(0.1f);
            tiempo += 0.1f;
        }

        if (File.Exists(pathTranscripcion))
        {
            string textoCompleto = File.ReadAllText(pathTranscripcion).Trim();
            UnityEngine.Debug.Log("Whisper entendió: " + textoCompleto);
            if (debugUI != null) debugUI.ActualizarTranscripcion(textoCompleto);
        }

        if (File.Exists(pathAccion))
        {
            string accion = File.ReadAllText(pathAccion).Trim();
            UnityEngine.Debug.Log("El boss hará: " + accion);
            if (debugUI != null) debugUI.ActualizarAccion(accion);
            if (bossAnimator != null) bossAnimator.SetTrigger(accion);
            StartCoroutine(ReproducirVozBossIA());
        }

        if (debugUI != null) debugUI.ActualizarEstadoSistema("Listo. Pulsa de nuevo para hablar.", Color.green);
        if (panelProcesando != null) panelProcesando.SetActive(false);
    }

    IEnumerator ReproducirVozBossIA()
    {
        float timeout = 15f;
        float timer = 0f;
        while (!File.Exists(pathRespuestaAudio) || new FileInfo(pathRespuestaAudio).Length < 100)
        {
            if (timer > timeout) yield break;
            timer += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }
        yield return new WaitForSeconds(0.3f);

        string url = "file://" + pathRespuestaAudio + "?" + System.DateTime.Now.Ticks;
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                bossAudioSource.clip = DownloadHandlerAudioClip.GetContent(www);
                bossAudioSource.Play();
            }
            else
            {
                UnityEngine.Debug.LogError("Error cargando audio del boss: " + www.error);
            }
        }
    }

    private async Task<string> EnviarTranscripcionAsync()
    {
        try
        {
            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync("127.0.0.1", puertoServidor);
                NetworkStream stream = client.GetStream();

                // Solo "TRANSCRIBE" — Python usa SCRIPT_DIR que coincide con StreamingAssets/Clips/
                byte[] msg = Encoding.UTF8.GetBytes("TRANSCRIBE");
                await stream.WriteAsync(msg, 0, msg.Length);

                byte[] buffer = new byte[256];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                return response.Trim();
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error de conexión TCP: " + e.Message);
            return "ERROR";
        }
    }

    private void IniciarServidorSiNoExiste()
    {
        if (PuertoOcupado("127.0.0.1", puertoServidor))
        {
            UnityEngine.Debug.Log("[Servidor Python] Ya está corriendo.");
            return;
        }

        string scriptPath = Path.Combine(clipsFolder, "whisper_server.py");
        UnityEngine.Debug.Log("[Servidor Python] Arrancando desde: " + scriptPath);

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = @"C:\Users\iohht\AppData\Local\Programs\Python\Python310\python.exe",
                Arguments = $"\"{scriptPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            pythonServerProcess = Process.Start(psi);
            Task.Run(() =>
            {
                while (!pythonServerProcess.StandardOutput.EndOfStream)
                    UnityEngine.Debug.Log("[Servidor Python] " + pythonServerProcess.StandardOutput.ReadLine());
            });
            Task.Run(() =>
            {
                while (!pythonServerProcess.StandardError.EndOfStream)
                    UnityEngine.Debug.LogError("[Servidor Python ERROR] " + pythonServerProcess.StandardError.ReadLine());
            });
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("No se pudo arrancar el servidor Python: " + e.Message);
        }
    }

    private bool PuertoOcupado(string host, int puerto)
    {
        try
        {
            using (TcpClient client = new TcpClient())
            {
                var task = client.ConnectAsync(host, puerto);
                task.Wait(500);
                return task.IsCompletedSuccessfully;
            }
        }
        catch { return false; }
    }
}