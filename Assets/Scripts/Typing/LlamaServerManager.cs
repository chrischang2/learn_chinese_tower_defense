using UnityEngine;
using System.Diagnostics;
using System.IO;
using Debug = UnityEngine.Debug;

/// <summary>
/// Manages the local llama-server.exe process lifecycle.
/// Starts the server on Awake, shuts it down on application quit.
/// Exposes the base URL for other scripts to query.
/// </summary>
public class LlamaServerManager : MonoBehaviour
{
    public static LlamaServerManager Instance { get; private set; }

    // ── Configuration ────────────────────────────────────────────────

    [Header("Paths")]
    [Tooltip("Folder containing llama-server.exe")]
    public string serverFolder = @"C:\Users\chris\Downloads\chatbots\llama-b8182-bin-win-cpu-x64";

    [Tooltip("GGUF model filename (inside serverFolder)")]
    public string modelFilename = "llama3.1_8b_chinese_chat_q4_k_m.gguf";

    [Header("Server Settings")]
    [Tooltip("Port for the HTTP API")]
    public int port = 8081;

    [Tooltip("Context length (tokens). 2048 is enough for word lists.")]
    public int contextLength = 2048;

    [Tooltip("Number of CPU threads to use (0 = auto)")]
    public int threads = 0;

    // ── Public state ─────────────────────────────────────────────────

    /// <summary>Base URL for API requests, e.g. http://localhost:8081</summary>
    public string BaseUrl => $"http://127.0.0.1:{port}";

    /// <summary>True once the server process has been launched (may still be loading the model).</summary>
    public bool IsLaunched { get; private set; }

    /// <summary>True once the server responded to a health check.</summary>
    public bool IsReady { get; private set; }

    // ── Internals ────────────────────────────────────────────────────

    private Process serverProcess;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        StartServer();
    }

    void OnApplicationQuit()
    {
        StopServer();
    }

    void OnDestroy()
    {
        StopServer();
    }

    // ── Server lifecycle ─────────────────────────────────────────────

    private void StartServer()
    {
        string exePath = Path.Combine(serverFolder, "llama-server.exe");
        string modelPath = Path.Combine(serverFolder, modelFilename);

        if (!File.Exists(exePath))
        {
            Debug.LogError($"[LlamaServer] llama-server.exe not found at: {exePath}");
            return;
        }
        if (!File.Exists(modelPath))
        {
            Debug.LogError($"[LlamaServer] Model file not found at: {modelPath}");
            return;
        }

        string args = $"-m \"{modelPath}\" --port {port} -c {contextLength}";
        if (threads > 0)
            args += $" -t {threads}";

        // Disable chat template auto-detection issues — use chatml
        args += " --chat-template chatml";

        Debug.Log($"[LlamaServer] Starting: {exePath} {args}");

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = args,
            WorkingDirectory = serverFolder,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        serverProcess = new Process { StartInfo = psi };
        serverProcess.OutputDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Debug.Log($"[LlamaServer] {e.Data}");
        };
        serverProcess.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Debug.Log($"[LlamaServer] {e.Data}");
        };

        serverProcess.Start();
        serverProcess.BeginOutputReadLine();
        serverProcess.BeginErrorReadLine();

        IsLaunched = true;
        Debug.Log($"[LlamaServer] Process started (PID {serverProcess.Id}). Waiting for model load...");

        // Start polling for readiness
        StartCoroutine(PollUntilReady());
    }

    private System.Collections.IEnumerator PollUntilReady()
    {
        float timeout = 120f; // model loading can take a while on CPU
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            using (var req = UnityEngine.Networking.UnityWebRequest.Get($"{BaseUrl}/health"))
            {
                req.timeout = 3;
                yield return req.SendWebRequest();

                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    IsReady = true;
                    Debug.Log("[LlamaServer] Server is ready!");
                    yield break;
                }
            }

            yield return new WaitForSeconds(2f);
            elapsed += 2f;
        }

        Debug.LogWarning("[LlamaServer] Timed out waiting for server to become ready.");
    }

    private void StopServer()
    {
        if (serverProcess != null && !serverProcess.HasExited)
        {
            Debug.Log("[LlamaServer] Shutting down server...");
            try
            {
                serverProcess.Kill();
                serverProcess.WaitForExit(3000);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LlamaServer] Error stopping server: {ex.Message}");
            }
            finally
            {
                serverProcess.Dispose();
                serverProcess = null;
            }
        }
        IsLaunched = false;
        IsReady = false;
    }
}
