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
    public sealed partial class WorldCoords
    {
        [DataMember(Name = "x")]
        public int X;
        [DataMember(Name = "y")]
        public int Y;
        [DataMember(Name = "z")]
        public int Z;

        public WorldCoords(
            int X,
            int Y,
            int Z
        )
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
        }

        public WorldCoords()
        {
        }
    }
}
