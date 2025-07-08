using System;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;

// This partial class contains SpacetimeDB-specific functionality for GameManager
public partial class GameManager
{
    #region Public Reducer Calls

    /// <summary>
    /// Create a new player with the given name
    /// </summary>
    public static void CreatePlayer(string playerName)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot create player - not connected");
            return;
        }

        Debug.Log($"Calling CreatePlayer reducer with name: {playerName}");
        Conn.Reducers.CreatePlayer(playerName);
    }

    /// <summary>
    /// Choose a starting crystal for the player
    /// </summary>
    public static void ChooseCrystal(CrystalType crystalType)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot choose crystal - not connected");
            return;
        }

        Debug.Log($"Calling ChooseCrystal reducer with type: {crystalType}");
        Conn.Reducers.ChooseCrystal(crystalType);
    }

    /// <summary>
    /// Update player position in the world
    /// </summary>
    public static void UpdatePlayerPosition(DbVector3 position, DbQuaternion rotation)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot update position - not connected");
            return;
        }

        Conn.Reducers.UpdatePlayerPosition(position, rotation);
    }

    /// <summary>
    /// Travel to a different world
    /// </summary>
    public static void TravelToWorld(WorldCoords worldCoords)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot travel - not connected");
            return;
        }

        Debug.Log($"Traveling to world ({worldCoords.X}, {worldCoords.Y}, {worldCoords.Z})");
        Conn.Reducers.TravelToWorld(worldCoords);
    }

    #endregion

    #region Debug Commands

    /// <summary>
    /// Give the current player a debug crystal
    /// </summary>
    public static void DebugGiveCrystal(CrystalType crystalType)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot give crystal - not connected");
            return;
        }

        Debug.Log($"[DEBUG] Giving {crystalType} crystal to current player");
        Conn.Reducers.DebugGiveCrystal(crystalType);
    }

    /// <summary>
    /// Request debug mining status
    /// </summary>
    public static void DebugMiningStatus()
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot check mining status - not connected");
            return;
        }

        Debug.Log("[DEBUG] Requesting mining status");
        Conn.Reducers.DebugMiningStatus();
    }

    /// <summary>
    /// Request debug wave packet status
    /// </summary>
    public static void DebugWavePacketStatus()
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot check wave packet status - not connected");
            return;
        }

        Debug.Log("[DEBUG] Requesting wave packet status");
        Conn.Reducers.DebugWavePacketStatus();
    }

    #endregion

    #region Mining System

    /// <summary>
    /// Start mining a wave packet orb
    /// </summary>
    public static void StartMining(ulong orbId)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot start mining - not connected");
            return;
        }

        Debug.Log($"Starting to mine orb {orbId}");
        Conn.Reducers.StartMining(orbId);
    }

    /// <summary>
    /// Stop mining current orb
    /// </summary>
    public static void StopMining()
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot stop mining - not connected");
            return;
        }

        Debug.Log("Stopping mining");
        Conn.Reducers.StopMining();
    }

    /// <summary>
    /// Extract a wave packet from current mining session
    /// </summary>
    public static void ExtractWavePacket()
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot extract wave packet - not connected");
            return;
        }

        Conn.Reducers.ExtractWavePacket();
    }

    /// <summary>
    /// Capture a wave packet that has reached the player
    /// </summary>
    public static void CaptureWavePacket(ulong wavePacketId)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot capture wave packet - not connected");
            return;
        }

        Conn.Reducers.CaptureWavePacket(wavePacketId);
    }

    /// <summary>
    /// Collect an entire wave packet orb directly
    /// </summary>
    public static void CollectWavePacketOrb(ulong orbId)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot collect orb - not connected");
            return;
        }

        Debug.Log($"Collecting orb {orbId}");
        Conn.Reducers.CollectWavePacketOrb(orbId);
    }

    #endregion

    #region Authentication

    /// <summary>
    /// Register a new account
    /// </summary>
    public static void RegisterAccount(string username, string displayName, string pin)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot register - not connected");
            return;
        }

        Debug.Log($"Registering account: {username}");
        Conn.Reducers.RegisterAccount(username, displayName, pin);
    }

    /// <summary>
    /// Login with session support
    /// </summary>
    public static void LoginWithSession(string username, string pin, string deviceInfo)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot login - not connected");
            return;
        }

        Debug.Log($"Logging in with session: {username}");
        Conn.Reducers.LoginWithSession(username, pin, deviceInfo);
    }

    /// <summary>
    /// Restore a previous session
    /// </summary>
    public static void RestoreSession(string sessionToken)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot restore session - not connected");
            return;
        }

        Debug.Log("Restoring session");
        Conn.Reducers.RestoreSession(sessionToken);
    }

    /// <summary>
    /// Logout the current user
    /// </summary>
    public static void Logout()
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot logout - not connected");
            return;
        }

        Debug.Log("Logging out");
        Conn.Reducers.Logout();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Get the player's current crystal type
    /// </summary>
    public static CrystalType? GetPlayerCrystalType()
    {
        var player = GetLocalPlayer();
        if (player == null) return null;

        foreach (var crystal in Conn.Db.PlayerCrystal.Iter())
        {
            if (crystal.PlayerId == player.PlayerId)
            {
                return crystal.CrystalType;
            }
        }

        return null;
    }

    /// <summary>
    /// Get the player's wave packet storage
    /// </summary>
    public static WavePacketStorage[] GetPlayerStorage()
    {
        var player = GetLocalPlayer();
        if (player == null) return new WavePacketStorage[0];

        var storage = new System.Collections.Generic.List<WavePacketStorage>();
        
        foreach (var item in Conn.Db.WavePacketStorage.Iter())
        {
            if (item.OwnerType == "player" && item.OwnerId == player.PlayerId)
            {
                storage.Add(item);
            }
        }

        return storage.ToArray();
    }

    #endregion
}