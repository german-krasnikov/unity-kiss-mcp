// Enums for tool approval flow. Pure — no UnityEngine/UnityEditor deps.
namespace UnityMCP.Editor.Chat
{
    public enum ApprovalDecision { Allow, Deny, AllowSession, AlwaysAllow }

    public enum RiskLevel { Low, Medium, High }

    public enum PermissionMode { Trust, Allowlist, Interactive }
}
