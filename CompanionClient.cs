using System;
using System.Collections;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace OutwardDynasty
{
    /// <summary>
    /// TCP newline-delimited JSON client to the Outward Dynasty Companion Authority.
    /// The Companion App is the authority: it arbitrates sync, host election, and can halt the game.
    /// </summary>
    public class CompanionClient : MonoBehaviour
    {
        public static CompanionClient Instance;

        [Header("Authority")]
        public string Host = "127.0.0.1";
        public int Port = 9876;

        public string DynastyId { get; private set; } = "";
        public string ClientId { get; private set; } = "";

        public bool Connected { get; private set; }
        public bool EverConnected { get; private set; }
        public bool AuthorityGranted { get; private set; }
        public bool LocalIsHost { get; private set; }

        public bool Halted { get; private set; }
        public string LastMessage { get; private set; } = "";

        private TcpClient _tcp;
        private NetworkStream _stream;
        private StreamReader _reader;
        private StreamWriter _writer;

        private Thread _rxThread;
        private volatile bool _stop;

        private readonly object _sendLock = new object();

        private Coroutine _heartbeatCoroutine;
        private bool _heartbeatIsHost;
        private float _heartbeatInterval = 2f;

        private DynastyCore _core;

        public void Initialize(DynastyCore core)
        {
            _core = core;
            if (string.IsNullOrEmpty(ClientId))
                ClientId = SystemInfo.deviceUniqueIdentifier;

            // Keep alive across scenes
            DontDestroyOnLoad(gameObject);

            // Attempt connect (non-fatal)
            EnsureConnected();
        }

        public void Initialize(string dynastyId)
        {
            DynastyId = dynastyId ?? "";
            if (string.IsNullOrEmpty(ClientId))
                ClientId = SystemInfo.deviceUniqueIdentifier;

            DontDestroyOnLoad(gameObject);
            EnsureConnected();
        }

        private void Awake()
        {
            Instance = this;
            if (string.IsNullOrEmpty(ClientId))
                ClientId = SystemInfo.deviceUniqueIdentifier;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            StopClient();
        }

        private void Update()
        {
            // If we lost connection, trigger grace countdown and try to reconnect occasionally.
            if (!Connected && !_stop)
            {
                // low-rate reconnect attempt
                if (Time.frameCount % 240 == 0) // ~ every few seconds
                    EnsureConnected();
            }
        }

        private void EnsureConnected()
        {
            if (Connected) return;

            try
            {
                StopClient();

                _tcp = new TcpClient();
                _tcp.NoDelay = true;
                _tcp.Connect(Host, Port);
                _stream = _tcp.GetStream();
                _reader = new StreamReader(_stream, Encoding.UTF8);
                _writer = new StreamWriter(_stream, new UTF8Encoding(false)) { AutoFlush = true };

                Connected = true;
                    EverConnected = true;
                AuthorityGranted = false;
                LocalIsHost = false;
                Halted = false;
                LastMessage = "";

                try { AuthorityFreezeManager.Instance?.NotifyConnected(); } catch { }

                _stop = false;
                _rxThread = new Thread(RxLoop) { IsBackground = true, Name = "Dynasty.CompanionClient.Rx" };
                _rxThread.Start();

                // Register/hello
                SendJson(BuildHello());
            }
            catch (Exception ex)
            {
                Connected = false;
                LastMessage = "Companion connect failed: " + ex.Message;
                try { AuthorityFreezeManager.Instance?.NotifyDisconnected(); } catch { }
            }
        }

        private void StopClient()
        {
            _stop = true;

            try { _rxThread?.Join(100); } catch { }
            _rxThread = null;

            try { _reader?.Dispose(); } catch { }
            try { _writer?.Dispose(); } catch { }
            try { _stream?.Dispose(); } catch { }
            try { _tcp?.Close(); } catch { }

            _reader = null;
            _writer = null;
            _stream = null;
            _tcp = null;

            if (Connected)
            {
                Connected = false;
                try { AuthorityFreezeManager.Instance?.NotifyDisconnected(); } catch { }
            }
        }

        // ------------------- Public API used by other systems -------------------


public bool RegisterHost(string dynastyId, string startLabel, string startScene, int echoes, out string status)
{
    status = null;
    DynastyId = dynastyId ?? "";
    if (!EnsureReady(out status)) return false;

    try
    {
        var msg = "{\"op\":\"register_host\",\"dynastyId\":\"" + Escape(DynastyId) +
                  "\",\"clientId\":\"" + Escape(ClientId) +
                  "\",\"startLabel\":\"" + Escape(startLabel ?? "") +
                  "\",\"startScene\":\"" + Escape(startScene ?? "") +
                  "\",\"echoes\":" + echoes.ToString() + "}";
        SendJson(msg);
        return true;
    }
    catch (Exception ex) { status = ex.Message; return false; }
}

public bool RegisterJoin(string dynastyId, out string status)
{
    status = null;
    DynastyId = dynastyId ?? "";
    if (!EnsureReady(out status)) return false;

    try
    {
        var msg = "{\"op\":\"register_join\",\"dynastyId\":\"" + Escape(DynastyId) +
                  "\",\"clientId\":\"" + Escape(ClientId) + "\"}";
        SendJson(msg);
        return true;
    }
    catch (Exception ex) { status = ex.Message; return false; }
}

public void StartHeartbeat(bool isHost)
{
    _heartbeatIsHost = isHost;

    if (_heartbeatCoroutine != null)
        StopCoroutine(_heartbeatCoroutine);

    _heartbeatCoroutine = StartCoroutine(HeartbeatLoop());
}

private IEnumerator HeartbeatLoop()
{
    while (true)
    {
        try
        {
            string status;
            if (EnsureReady(out status))
            {
                var msg = "{\"op\":\"heartbeat\",\"dynastyId\":\"" + Escape(DynastyId) +
                          "\",\"clientId\":\"" + Escape(ClientId) +
                          "\",\"isHost\":" + (_heartbeatIsHost ? "true" : "false") + "}";
                SendJson(msg);
            }
        }
        catch { }

        yield return new WaitForSeconds(_heartbeatInterval);
    }
}

        
public bool SendHistoryEvent(string historyJson, out string status)
{
    status = null;
    try
    {
        if (!Connected) { status = "Not connected."; return false; }
        if (string.IsNullOrEmpty(historyJson)) { status = "Empty history payload."; return false; }
        // Send as pre-serialized JSON string field to avoid dict parsing on this side.
        var line = "{"op":"history","dynastyId":"" + Escape(DynastyId) +
                   "","clientId":"" + Escape(ClientId) +
                   "","memberGuid":"" + Escape(DynastyIdentity.MemberGuid) +
                   "","historyJson":" + historyJson + "}";
        SendJson(line);
        return true;
    }
    catch (Exception ex) { status = ex.Message; return false; }
}

public bool PushSnapshot(string snapshotJson, out string status)
        {
            status = null;
            if (!EnsureReady(out status)) return false;

            try
            {
                var msg = "{\"op\":\"push_snapshot\",\"dynastyId\":\"" + Escape(DynastyId) +
                          "\",\"clientId\":\"" + Escape(ClientId) +
                          "\",\"snapshotJson\":" + (string.IsNullOrEmpty(snapshotJson) ? "\"\"" : snapshotJson) + "}";
                SendJson(msg);
                return true;
            }
            catch (Exception ex) { status = ex.Message; return false; }
        }

        public bool RequestResync(out string status)
        {
            status = null;
            if (!EnsureReady(out status)) return false;

            try
            {
                var msg = "{\"op\":\"resync_request\",\"dynastyId\":\"" + Escape(DynastyId) +
                          "\",\"clientId\":\"" + Escape(ClientId) + "\"}";
                SendJson(msg);
                return true;
            }
            catch (Exception ex) { status = ex.Message; return false; }
        }

        public bool StartHostElection(string reason, out string status)
        {
            status = null;
            if (!EnsureReady(out status)) return false;

            try
            {
                var msg = "{\"op\":\"host_election_start\",\"dynastyId\":\"" + Escape(DynastyId) +
                          "\",\"clientId\":\"" + Escape(ClientId) +
                          "\",\"reason\":\"" + Escape(reason ?? "") + "\"}";
                SendJson(msg);
                return true;
            }
            catch (Exception ex) { status = ex.Message; return false; }
        }

        public bool SendHostVote(string memberGuid, out string status)
        {
            status = null;
            if (!EnsureReady(out status)) return false;

            try
            {
                var msg = "{\"op\":\"host_vote\",\"dynastyId\":\"" + Escape(DynastyId) +
                          "\",\"clientId\":\"" + Escape(ClientId) +
                          "\",\"vote\":\"" + Escape(memberGuid ?? "") + "\"}";
                SendJson(msg);
                return true;
            }
            catch (Exception ex) { status = ex.Message; return false; }
        }

        // ------------------- Internals -------------------

        private bool EnsureReady(out string status)
        {
            status = null;
            if (!Connected) EnsureConnected();
            if (!Connected) { status = "Not connected to Companion."; return false; }

            // If DynastyId not known yet, still allow basic ops (some setups may set later)
            return true;
        }

        private void RxLoop()
        {
            try
            {
                while (!_stop && _reader != null)
                {
                    var line = _reader.ReadLine();
                    if (line == null) break;
                    line = line.Trim();
                    if (line.Length == 0) continue;

                    HandleIncoming(line);
                }
            }
            catch { /* treat as disconnect */ }

            // Disconnect
            Connected = false;
            try { AuthorityFreezeManager.Instance?.NotifyDisconnected(); } catch { }
        }

        private void HandleIncoming(string jsonLine)
        {
            // We keep parsing intentionally lightweight (no external JSON libs).
            // Companion is authoritative; we only need a few flags + a couple payload fields.

            try
            {
                // authority granted?
                if (jsonLine.Contains("\"authorityGranted\"") || jsonLine.Contains("\"authority\""))
                {
                    bool granted = GetBool(jsonLine, "authorityGranted", false);
                    if (jsonLine.Contains("\"authority\"") && !jsonLine.Contains("\"authorityGranted\""))
                        granted = GetBool(jsonLine, "authority", granted);

                    AuthorityGranted = granted;
                }

                // local host flag?
                if (jsonLine.Contains("\"localIsHost\"") || jsonLine.Contains("\"isHost\""))
                {
                    bool host = GetBool(jsonLine, "localIsHost", false);
                    if (jsonLine.Contains("\"isHost\"") && !jsonLine.Contains("\"localIsHost\""))
                        host = GetBool(jsonLine, "isHost", host);

                    LocalIsHost = host;
                }

                // halt?
                if (jsonLine.Contains("\"halted\"") || jsonLine.Contains("\"halt\""))
                {
                    bool h = GetBool(jsonLine, "halted", false);
                    if (jsonLine.Contains("\"halt\"") && !jsonLine.Contains("\"halted\""))
                        h = GetBool(jsonLine, "halt", h);

                    Halted = h;
                    var msg = GetString(jsonLine, "message", "");
                    if (!string.IsNullOrEmpty(msg)) LastMessage = msg;

                    if (Halted)
                        try { AuthorityFreezeManager.Instance?.BeginFreeze(string.IsNullOrEmpty(LastMessage) ? "Companion halted the game." : LastMessage); } catch { }
                }

                // host migration ack?
                if (jsonLine.Contains("migrationInProgress") || jsonLine.Contains("newHostClientId"))
                {
                    var ack = new HostMigrationAck
                    {
                        migrationInProgress = GetBool(jsonLine, "migrationInProgress", false),
                        newHostClientId = GetString(jsonLine, "newHostClientId", ""),
                        snapshotJson = ExtractJsonFieldRaw(jsonLine, "snapshotJson") // may be an object/string; keep raw
                    };
                    HostMigrationManager.OnCompanionAck(ack);
                }

                // resync payload: "snapshotJson"
                if (jsonLine.Contains("\"snapshotJson\"") && (jsonLine.Contains("resync") || jsonLine.Contains("snapshot")))
                {
                    // If this was a resync result and we are not host, try apply snapshot.
                    var raw = ExtractJsonFieldRaw(jsonLine, "snapshotJson");
                    if (!string.IsNullOrEmpty(raw) && DynastyCore.Instance != null)
                    {
                        DynastySnapshotManager.TryApplySnapshotJson(raw, DynastyCore.Instance, out var _);
                        DynastyLocalCommitStore.SaveLatest(raw);
                    }
                }
            }
            catch { }
        }

        [Serializable]
        private class HostMigrationAck
        {
            public bool migrationInProgress;
            public string newHostClientId;
            public string snapshotJson;
        }

        private string BuildHello()
        {
            return "{\"op\":\"hello\",\"dynastyId\":\"" + Escape(DynastyId) +
                   "\",\"clientId\":\"" + Escape(ClientId) + "\"}";
        }

        private void SendJson(string jsonLine)
        {
            if (_writer == null) return;
            lock (_sendLock)
            {
                _writer.WriteLine(jsonLine);
            }
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static bool GetBool(string json, string key, bool def)
        {
            try
            {
                int i = json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
                if (i < 0) return def;
                i = json.IndexOf(':', i);
                if (i < 0) return def;
                i++;
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i + 4 <= json.Length && string.Compare(json, i, "true", 0, 4, true) == 0) return true;
                if (i + 5 <= json.Length && string.Compare(json, i, "false", 0, 5, true) == 0) return false;
                return def;
            }
            catch { return def; }
        }

        private static string GetString(string json, string key, string def)
        {
            try
            {
                int i = json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
                if (i < 0) return def;
                i = json.IndexOf(':', i);
                if (i < 0) return def;
                i++;
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i >= json.Length || json[i] != '\"') return def;
                i++;
                var sb = new StringBuilder();
                while (i < json.Length)
                {
                    char c = json[i++];
                    if (c == '\\' && i < json.Length)
                    {
                        char n = json[i++];
                        if (n == '\"' || n == '\\' || n == '/') sb.Append(n);
                        else if (n == 'n') sb.Append('\n');
                        else if (n == 'r') sb.Append('\r');
                        else if (n == 't') sb.Append('\t');
                        else sb.Append(n);
                        continue;
                    }
                    if (c == '\"') break;
                    sb.Append(c);
                }
                return sb.ToString();
            }
            catch { return def; }
        }

        /// <summary>
        /// Tries to extract the raw JSON value for a field (string/object/array/number/bool/null).
        /// Returns the substring representing the value, without surrounding whitespace.
        /// </summary>
        private static string ExtractJsonFieldRaw(string json, string key)
        {
            try
            {
                int i = json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
                if (i < 0) return "";
                i = json.IndexOf(':', i);
                if (i < 0) return "";
                i++;
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i >= json.Length) return "";

                // If it's a string, return the quoted string content as a JSON string literal (with quotes)
                if (json[i] == '\"')
                {
                    int start = i;
                    i++;
                    bool esc = false;
                    while (i < json.Length)
                    {
                        char c = json[i++];
                        if (esc) { esc = false; continue; }
                        if (c == '\\') { esc = true; continue; }
                        if (c == '\"') break;
                    }
                    int end = i;
                    return json.Substring(start, end - start);
                }

                // If it's an object/array, balance braces/brackets
                if (json[i] == '{' || json[i] == '[')
                {
                    int start = i;
                    int depth = 0;
                    bool inStr = false;
                    bool esc = false;
                    while (i < json.Length)
                    {
                        char c = json[i++];
                        if (inStr)
                        {
                            if (esc) { esc = false; continue; }
                            if (c == '\\') { esc = true; continue; }
                            if (c == '\"') inStr = false;
                            continue;
                        }
                        if (c == '\"') { inStr = true; continue; }
                        if (c == '{' || c == '[') depth++;
                        if (c == '}' || c == ']') depth--;
                        if (depth == 0) break;
                    }
                    int end = i;
                    return json.Substring(start, end - start);
                }

                // primitive: read until comma or end or close brace
                int s = i;
                while (i < json.Length && json[i] != ',' && json[i] != '}' && json[i] != ']') i++;
                return json.Substring(s, i - s).Trim();
            }
            catch { return ""; }
        }
    }
}
