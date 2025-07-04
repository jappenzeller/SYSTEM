// THIS FILE IS AUTOMATICALLY GENERATED BY SPACETIMEDB. EDITS TO THIS FILE
// WILL NOT BE SAVED. MODIFY TABLES IN YOUR MODULE SOURCE CODE INSTEAD.

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SpacetimeDB.Types
{
    [SpacetimeDB.Type]
    [DataContract]
    public sealed partial class World
    {
        [DataMember(Name = "world_coords")]
        public WorldCoords WorldCoords;
        [DataMember(Name = "world_name")]
        public string WorldName;
        [DataMember(Name = "world_type")]
        public string WorldType;
        [DataMember(Name = "shell_level")]
        public byte ShellLevel;

        public World(
            WorldCoords WorldCoords,
            string WorldName,
            string WorldType,
            byte ShellLevel
        )
        {
            this.WorldCoords = WorldCoords;
            this.WorldName = WorldName;
            this.WorldType = WorldType;
            this.ShellLevel = ShellLevel;
        }

        public World()
        {
            this.WorldCoords = new();
            this.WorldName = "";
            this.WorldType = "";
        }
    }
}
