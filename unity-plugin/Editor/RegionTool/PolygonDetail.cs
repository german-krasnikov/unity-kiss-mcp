namespace UnityMCP.Editor.RegionTool
{
    internal enum PolygonDetailLevel { Minimal, Normal, Detailed, Full }

    internal static class PolygonDetailConfig
    {
        /// <summary>RDP epsilon in world units. Larger = more aggressive simplification. 0 = skip.</summary>
        public static float Epsilon(PolygonDetailLevel level) => level switch
        {
            PolygonDetailLevel.Minimal  => 4.0f,
            PolygonDetailLevel.Normal   => 1.0f,
            PolygonDetailLevel.Detailed => 0.3f,
            PolygonDetailLevel.Full     => 0.0f,
            _                           => 1.0f,
        };

        /// <summary>Circle approximation vertex count per detail level.</summary>
        public static int CircleVertices(PolygonDetailLevel level) => level switch
        {
            PolygonDetailLevel.Minimal  => 8,
            PolygonDetailLevel.Normal   => 16,
            PolygonDetailLevel.Detailed => 32,
            PolygonDetailLevel.Full     => 64,
            _                           => 16,
        };

        /// <summary>Approximate vertex count targets for display only.</summary>
        public static (int min, int max) VertexTarget(PolygonDetailLevel level) => level switch
        {
            PolygonDetailLevel.Minimal  => (4, 8),
            PolygonDetailLevel.Normal   => (12, 24),
            PolygonDetailLevel.Detailed => (32, 64),
            PolygonDetailLevel.Full     => (0, 256),
            _                           => (12, 24),
        };

        /// <summary>Estimated tokens per vertex: "1234.56,789.01;" ≈ 15 chars ≈ 4 tokens.</summary>
        public const int TokensPerVertex = 4;

        /// <summary>Vertex count above which a warning is shown in the overlay.</summary>
        public const int WarnVertexCount = 128;
    }
}
