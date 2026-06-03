using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Seam for swappable backends (Claude, Codex, Cursor — only Claude implemented in MVP).
    /// </summary>
    public interface IChatBackend
    {
        /// <summary>True while the child process is alive.</summary>
        bool IsRunning { get; }

        /// <summary>Session ID captured from the last system/init or result line.</summary>
        string SessionId { get; }

        /// <summary>Spawn the process for the first user turn.</summary>
        void Start();

        /// <summary>Write a user-turn envelope to the process stdin.</summary>
        void SendTurn(string turnJson);

        /// <summary>
        /// Drain buffered events produced since last call.
        /// Called every ~33 ms from the UI schedule.
        /// </summary>
        void DrainEvents(List<ChatEvent> output);

        /// <summary>Kill the child process.</summary>
        void Stop();
    }
}
