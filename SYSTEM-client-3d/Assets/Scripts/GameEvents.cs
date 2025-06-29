using System;
using UnityEngine;
using SpacetimeDB.Types;

// Player-related events
public class LocalPlayerSpawnedEvent : EventArgs
{
    public Player Player { get; set; }
}

public class RemotePlayerJoinedEvent : EventArgs
{
    public Player Player { get; set; }
}

public class RemotePlayerLeftEvent : EventArgs
{
    public Player Player { get; set; }
}

public class RemotePlayerUpdatedEvent : EventArgs
{
    public Player OldPlayer { get; set; }
    public Player NewPlayer { get; set; }
}

// World Circuit events
public class WorldCircuitSpawnedEvent : EventArgs
{
    public WorldCircuit Circuit { get; set; }
}

public class WorldCircuitUpdatedEvent : EventArgs
{
    public WorldCircuit OldCircuit { get; set; }
    public WorldCircuit NewCircuit { get; set; }
}

public class WorldCircuitDespawnedEvent : EventArgs
{
    public WorldCircuit Circuit { get; set; }
}

// Connection events
public class ConnectionEstablishedEvent : EventArgs
{
    public SpacetimeDB.Identity Identity { get; set; }
    public string AuthToken { get; set; }
}

public class ConnectionLostEvent : EventArgs
{
    public Exception Error { get; set; }
    public string Reason { get; set; }
}

// World change events
public class WorldChangedEvent : EventArgs
{
    public WorldCoords OldWorld { get; set; }
    public WorldCoords NewWorld { get; set; }
}

// Wave Packet Mining events (for future use with the mining system)
public class WavePacketOrbSpawnedEvent : EventArgs
{
    public WavePacketOrb Orb { get; set; }
}

public class WavePacketOrbUpdatedEvent : EventArgs
{
    public WavePacketOrb OldOrb { get; set; }
    public WavePacketOrb NewOrb { get; set; }
}

public class WavePacketOrbDespawnedEvent : EventArgs
{
    public uint OrbId { get; set; }
}

public class WavePacketCapturedEvent : EventArgs
{
    public uint PlayerId { get; set; }
    public uint WavePacketId { get; set; }
    public WavePacketSignature Signature { get; set; }
}

public class MiningStartedEvent : EventArgs
{
    public uint PlayerId { get; set; }
    public uint OrbId { get; set; }
}

public class MiningStoppedEvent : EventArgs
{
    public uint PlayerId { get; set; }
}

// Crystal events
public class CrystalChosenEvent : EventArgs
{
    public uint PlayerId { get; set; }
    public CrystalType CrystalType { get; set; }
}

public class CrystalUpdatedEvent : EventArgs
{
    public PlayerCrystal OldCrystal { get; set; }
    public PlayerCrystal NewCrystal { get; set; }
}