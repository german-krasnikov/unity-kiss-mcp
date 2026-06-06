using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests")]
[assembly: InternalsVisibleTo("UnityMCP.TestProject")]
// grants Chat.Tests access to JsonHelper parse helpers
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.Tests")]
