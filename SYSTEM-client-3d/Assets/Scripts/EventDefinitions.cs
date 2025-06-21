using SpacetimeDB.Types;

// Energy Orb Events
public struct EnergyOrbCreatedEvent 
{ 
    public EnergyOrb Orb; 
}

public struct EnergyOrbUpdatedEvent 
{ 
    public EnergyOrb OldOrb; 
    public EnergyOrb NewOrb; 
}

public struct EnergyOrbDeletedEvent 
{ 
    public EnergyOrb Orb; 
}

// Energy Puddle Events
public struct EnergyPuddleCreatedEvent 
{ 
    public EnergyPuddle Puddle; 
}

public struct EnergyPuddleUpdatedEvent 
{ 
    public EnergyPuddle OldPuddle; 
    public EnergyPuddle NewPuddle; 
}

public struct EnergyPuddleDeletedEvent 
{ 
    public EnergyPuddle Puddle; 
}

// Player Events
public struct LocalPlayerSpawnedEvent 
{ 
    public Player Player; 
}

public struct RemotePlayerJoinedEvent 
{ 
    public Player Player; 
}

public struct RemotePlayerUpdatedEvent 
{ 
    public Player OldPlayer; 
    public Player NewPlayer; 
}

public struct RemotePlayerLeftEvent 
{ 
    public Player Player; 
}