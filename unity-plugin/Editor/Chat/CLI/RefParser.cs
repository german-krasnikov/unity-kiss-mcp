// Parse a raw [kind:ref] reference into ChipData (Path, InstanceID, DisplayName).
// Inverse of ChipContextResolver.FormatChipRef.
// Hierarchy: "/Root/Child #-33506" -> Path="/Root/Child", ID=-33506, Display="Child".
// Asset:     "Assets/Scripts/Foo.cs" -> Path=same, ID=0, Display="Foo.cs".
namespace UnityMCP.Editor.Chat
{
    internal static class RefParser
    {
        internal static ChipData Parse(string kindKey, string rawRef)
        {
            var path       = rawRef;
            int instanceId = 0;

            // Strip " #id" suffix (hierarchy refs). LastIndexOf handles nested # correctly.
            int hashIdx = rawRef.LastIndexOf(" #");
            if (hashIdx >= 0 && int.TryParse(rawRef.Substring(hashIdx + 2), out var id))
            {
                path       = rawRef.Substring(0, hashIdx);
                instanceId = id;
            }

            // Leaf name: after last '/'
            var leaf      = path;
            int lastSlash = path.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash < path.Length - 1)
                leaf = path.Substring(lastSlash + 1);

            return new ChipData(kindKey, path, leaf, instanceId);
        }
    }
}
