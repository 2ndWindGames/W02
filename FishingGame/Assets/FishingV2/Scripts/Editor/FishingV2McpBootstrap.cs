#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Fishing.V2.EditorTools
{
    /// <summary>
    /// Codex's Unity MCP server is configured for the local HTTP endpoint. Keep the package's
    /// local server and Unity bridge available when this project opens without requiring a GUI
    /// action in the MCP package window.
    /// </summary>
    [InitializeOnLoad]
    internal static class FishingV2McpBootstrap
    {
        private const string UseHttpTransportKey = "MCPForUnity.UseHttpTransport";
        private const string HttpTransportScopeKey = "MCPForUnity.HttpTransportScope";
        private const string HttpBaseUrlKey = "MCPForUnity.HttpUrl";
        private const string AutoStartOnLoadKey = "MCPForUnity.AutoStartOnLoad";
        private static bool _startInFlight;
        private static bool _bridgeConnected;
        private static double _lastAttemptAt = -100.0;

        static FishingV2McpBootstrap()
        {
            EditorApplication.delayCall += RetryStartFromEditorIdle;
            EditorApplication.update += RetryStartFromEditorIdle;
        }

        [InitializeOnLoadMethod]
        private static void EnsureRetryLoop()
        {
            EditorApplication.update -= RetryStartFromEditorIdle;
            EditorApplication.update += RetryStartFromEditorIdle;
        }

        private static void RetryStartFromEditorIdle()
        {
            if (_bridgeConnected || _startInFlight || EditorApplication.timeSinceStartup - _lastAttemptAt < 2.0)
            {
                return;
            }

            _lastAttemptAt = EditorApplication.timeSinceStartup;
            _startInFlight = true;
            _ = RetryStartAsync();
        }

        private static async Task RetryStartAsync()
        {
            try
            {
                await StartBridgeIfAvailableAsync();
            }
            finally
            {
                _startInFlight = false;
            }
        }

        private static async Task StartBridgeIfAvailableAsync()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            try
            {
                EditorPrefs.SetBool(UseHttpTransportKey, true);
                EditorPrefs.SetString(HttpTransportScopeKey, "local");
                EditorPrefs.SetString(HttpBaseUrlKey, "http://127.0.0.1:8080");
                // This project owns the retry loop below. Disable the package's separate
                // one-shot auto-start handler so two launchers do not race the same port.
                EditorPrefs.SetBool(AutoStartOnLoadKey, false);

                Type cacheType = Type.GetType(
                    "MCPForUnity.Editor.Services.EditorConfigurationCache, MCPForUnity.Editor");
                PropertyInfo cacheInstanceProperty = cacheType?.GetProperty(
                    "Instance",
                    BindingFlags.Public | BindingFlags.Static);
                object cache = cacheInstanceProperty?.GetValue(null, null);
                cache?.GetType().GetMethod("Refresh", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(cache, null);

                // The package's WebSocket receive loop registers project tools by posting to
                // TransportCommandDispatcher. Force its [InitializeOnLoad] static constructor
                // while this callback is still on Unity's editor thread so the dispatcher keeps
                // a valid UnitySynchronizationContext after a domain reload.
                Type dispatcherType = Type.GetType(
                    "MCPForUnity.Editor.Services.Transport.TransportCommandDispatcher, MCPForUnity.Editor");
                if (dispatcherType != null)
                {
                    RuntimeHelpers.RunClassConstructor(dispatcherType.TypeHandle);
                    Debug.Log("Fishing V2 MCP dispatcher initialized on Unity editor thread.");
                }

                Type endpointType = Type.GetType(
                    "MCPForUnity.Editor.Helpers.HttpEndpointUtility, MCPForUnity.Editor");
                MethodInfo getBaseUrlMethod = endpointType?.GetMethod(
                    "GetBaseUrl",
                    BindingFlags.Public | BindingFlags.Static);
                string resolvedBaseUrl = getBaseUrlMethod?.Invoke(null, null) as string;
                Debug.Log("Fishing V2 MCP resolved HTTP base URL: " + (resolvedBaseUrl ?? "<null>"));

                Type locatorType = Type.GetType("MCPForUnity.Editor.Services.MCPServiceLocator, MCPForUnity.Editor");
                PropertyInfo serverProperty = locatorType?.GetProperty(
                    "Server",
                    BindingFlags.Public | BindingFlags.Static);
                PropertyInfo bridgeProperty = locatorType?.GetProperty(
                    "Bridge",
                    BindingFlags.Public | BindingFlags.Static);
                object server = serverProperty?.GetValue(null, null);
                object bridge = bridgeProperty?.GetValue(null, null);
                if (server == null || bridge == null)
                {
                    return;
                }

                MethodInfo reachabilityMethod = server.GetType().GetMethod(
                    "IsLocalHttpServerReachable",
                    BindingFlags.Public | BindingFlags.Instance);
                MethodInfo serverStartMethod = server.GetType().GetMethod(
                    "StartLocalHttpServer",
                    BindingFlags.Public | BindingFlags.Instance);

                bool serverReachable = reachabilityMethod != null &&
                    (bool)reachabilityMethod.Invoke(server, null);
                if (!serverReachable && serverStartMethod != null)
                {
                    Debug.Log("Fishing V2 MCP starting local HTTP server for " + (resolvedBaseUrl ?? "<default>"));
                    serverStartMethod.Invoke(server, new object[] { true });
                }

                // The first uvx launch can take several seconds even when the process was
                // successfully created. Poll the actual endpoint instead of guessing a delay.
                for (int attempt = 0; attempt < 60 && !serverReachable; attempt++)
                {
                    await Task.Delay(500);
                    serverReachable = reachabilityMethod != null &&
                        (bool)reachabilityMethod.Invoke(server, null);
                }

                if (!serverReachable)
                {
                    Debug.LogWarning("Fishing V2 MCP local HTTP server did not become reachable.");
                    return;
                }

                Debug.Log("Fishing V2 MCP local HTTP server is reachable; starting Unity bridge.");

                MethodInfo bridgeStartMethod = bridge.GetType().GetMethod(
                    "StartAsync",
                    BindingFlags.Public | BindingFlags.Instance);
                if (bridgeStartMethod == null)
                {
                    return;
                }

                bool bridgeStarted = false;
                for (int attempt = 0; attempt < 30 && !bridgeStarted; attempt++)
                {
                    if (attempt > 0)
                    {
                        await Task.Delay(1000);
                    }

                    Task<bool> bridgeTask = (Task<bool>)bridgeStartMethod.Invoke(bridge, null);
                    bridgeStarted = await bridgeTask;
                }

                if (bridgeStarted)
                {
                    _bridgeConnected = true;
                    EditorApplication.update -= RetryStartFromEditorIdle;
                    Debug.Log("Fishing V2 MCP local HTTP bridge connected.");
                }
                else
                {
                    Debug.LogWarning("Fishing V2 MCP local HTTP bridge connection failed.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Fishing V2 MCP bridge auto-start failed: " + exception.Message);
            }
        }
    }
}
#endif
