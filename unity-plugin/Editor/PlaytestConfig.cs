using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor
{
    [CreateAssetMenu(menuName = "MCP/Playtest Config")]
    public class PlaytestConfig : ScriptableObject
    {
        [Header("Character Movement")]
        public string characterPath = "";
        public string moveComponent = "";
        public string moveMethod = "";
        public string isMovingField = "IsMoving";

        [Header("Movement Arrival")]
        public string arrivalOp = "==";
        public string arrivalValue = "False";

        [Header("Time Scale")]
        public string timeScaleClass = "";
        public string timeScaleProperty = "";

        [Header("CTA")]
        public string ctaPath;

        [Header("Aliases")]
        public List<QueryAlias> aliases = new();

        public QueryAlias FindAlias(string name) => aliases.Find(a => a.alias == name);
    }

    [Serializable]
    public class QueryAlias
    {
        public string alias;
        public string path;
        public string component;
        public string field;
    }
}
