using System;
using System.Threading;
using System.Threading.Tasks;
using ExileCore;

namespace ExportGlobals;

/// <summary>
/// Listens for build completion events and triggers plugin reload.
/// Lifecycle: Start -> Wait for signal -> Trigger reload -> Get destroyed during reload -> Recreate on next plugin init
/// </summary>
public class AutoReloadListener : IDisposable
{
    private const string EventName = "ExileCoreReloadPlugins";

    private readonly GameController _gameController;
    private readonly Action<string> _logMessage;
    private readonly Action<string> _logError;

    private EventWaitHandle _reloadEvent;
    private CancellationTokenSource _cts;
    private Task _listenerTask;

    public AutoReloadListener(GameController gameController, Action<string> logMessage, Action<string> logError)
    {
        _gameController = gameController;
        _logMessage = logMessage;
        _logError = logError;
    }

    public void Start()
    {
        if (_listenerTask != null)
            return;

        try
        {
            // Create or open the named event
            _reloadEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            _cts = new CancellationTokenSource();

            // Start background listener
            _listenerTask = Task.Run(() => ListenerLoop(_cts.Token));

            _logMessage($"Auto-reload listener started (event: {EventName})");
        }
        catch (Exception ex)
        {
            _logError($"Failed to start auto-reload listener: {ex.Message}");
        }
    }

    private void ListenerLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Wait for signal with timeout to allow cancellation check
                var signaled = _reloadEvent.WaitOne(500);

                if (signaled && !ct.IsCancellationRequested)
                {
                    _logMessage("Reload signal received - triggering plugin reload");

                    // Trigger plugin reload - this will cause this listener to be destroyed and recreated
                    _gameController?.Settings?.CoreSettings?.ReloadPlugins?.OnPressed?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _logError($"Error in reload listener: {ex.Message}");
                Thread.Sleep(1000); // Avoid tight loop on error
            }
        }
    }

    public void Dispose()
    {
        try
        {
            _cts?.Cancel();
            _reloadEvent?.Set(); // Wake up the waiting thread
            _listenerTask?.Wait(1000);

            _reloadEvent?.Dispose();
            _cts?.Dispose();

            _logMessage("Auto-reload listener stopped");
        }
        catch (Exception ex)
        {
            _logError?.Invoke($"Error stopping auto-reload listener: {ex.Message}");
        }
    }
}
