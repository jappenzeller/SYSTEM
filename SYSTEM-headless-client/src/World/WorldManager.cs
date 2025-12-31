using SpacetimeDB;
using SpacetimeDB.ClientApi;
using SpacetimeDB.Types;
using SYSTEM.HeadlessClient.Connection;

namespace SYSTEM.HeadlessClient.World;

/// <summary>
/// Types of commands that can be queued in a plan.
/// </summary>
public enum PlanCommandType
{
    Walk,       // Walk in a direction for a distance
    Rotate,     // Rotate by radians
    Wait        // Wait for seconds
}

/// <summary>
/// A single command in a plan queue.
/// Timing is calculated automatically based on known constants.
/// </summary>
public class PlanCommand
{
    public PlanCommandType Type { get; set; }
    public float Forward { get; set; }      // -1 to 1 for Walk
    public float Right { get; set; }        // -1 to 1 for Walk
    public float Distance { get; set; }     // units for Walk
    public float Radians { get; set; }      // radians for Rotate
    public float Seconds { get; set; }      // seconds for Wait

    /// <summary>
    /// Calculate estimated duration based on WALK_SPEED constant
    /// </summary>
    public float GetEstimatedDuration(float walkSpeed)
    {
        return Type switch
        {
            PlanCommandType.Walk => Distance / walkSpeed,
            PlanCommandType.Rotate => 0.1f, // Rotation is instant, small buffer
            PlanCommandType.Wait => Seconds,
            _ => 0
        };
    }

    public override string ToString()
    {
        return Type switch
        {
            PlanCommandType.Walk => $"Walk(fwd={Forward:F1}, right={Right:F1}, dist={Distance:F1})",
            PlanCommandType.Rotate => $"Rotate({Radians:F2} rad)",
            PlanCommandType.Wait => $"Wait({Seconds:F1}s)",
            _ => "Unknown"
        };
    }
}

/// <summary>
/// Manages QAI position and movement on the sphere surface.
/// The world is a sphere with radius ~300 units.
/// </summary>
public class WorldManager
{
    private readonly SpacetimeConnection _connection;

    // World constants (matching Unity client and server)
    public const float WORLD_RADIUS = 300f;
    public const float SURFACE_OFFSET = 1f; // Players walk 1 unit above sphere surface
    public const float POSITION_UPDATE_INTERVAL = 0.1f; // seconds
    public const float WALK_SPEED = 10f; // units per second (matching player speed)

    // Current state
    private DbVector3 _position;
    private DbQuaternion _rotation;
    private float _lastUpdateTime;

    // Walking state (continuous movement like holding W)
    private float _walkForward;      // -1 to 1, like holding W/S
    private float _walkRight;        // -1 to 1, like holding A/D
    private float _walkDuration;     // remaining seconds to walk, or -1 for indefinite
    private float _walkDistance;     // remaining distance to walk, or -1 for indefinite
    private float _distanceTraveled; // distance traveled this walk session

    // Command queue for plan execution
    private readonly Queue<PlanCommand> _commandQueue = new();
    private float _waitRemaining;    // remaining wait time in seconds

    public bool IsWalking => _walkDuration != 0 || _walkDistance > 0;
    public float DistanceTraveled => _distanceTraveled;
    public bool IsExecutingPlan => _commandQueue.Count > 0 || IsWalking || _waitRemaining > 0;
    public int PlanCommandsRemaining => _commandQueue.Count;

    public DbVector3 Position => _position;
    public DbQuaternion Rotation => _rotation;

    public WorldManager(SpacetimeConnection connection)
    {
        _connection = connection;
        _position = new DbVector3(0, WORLD_RADIUS + SURFACE_OFFSET, 0); // Default: north pole at 301
        _rotation = new DbQuaternion(0, 0, 0, 1); // Identity quaternion
        _lastUpdateTime = 0;
    }

    /// <summary>
    /// Initialize reducer callbacks for position updates
    /// </summary>
    public void Initialize()
    {
        var conn = _connection.Conn;
        if (conn != null)
        {
            conn.Reducers.OnUpdatePlayerPosition += OnPositionUpdateResult;
        }
    }

    private void OnPositionUpdateResult(ReducerEventContext ctx, DbVector3 position, DbQuaternion rotation)
    {
        if (ctx.Event.Status is Status.Failed(var reason))
        {
            Console.WriteLine($"[World] Position update failed: {reason}");
        }
    }

    /// <summary>
    /// Initialize from player's current position
    /// </summary>
    public void InitializeFromPlayer(Player player)
    {
        _position = new DbVector3(player.Position.X, player.Position.Y, player.Position.Z);
        _rotation = new DbQuaternion(player.Rotation.X, player.Rotation.Y, player.Rotation.Z, player.Rotation.W);
        Console.WriteLine($"[World] Initialized at position ({_position.X:F1}, {_position.Y:F1}, {_position.Z:F1})");
    }

    /// <summary>
    /// Teleport to a specific position on the sphere surface.
    /// Position is automatically normalized to the correct radius.
    /// </summary>
    public void SetPosition(float x, float y, float z)
    {
        float magnitude = MathF.Sqrt(x * x + y * y + z * z);
        if (magnitude < 0.001f)
        {
            // Default to north pole if zero vector
            _position = new DbVector3(0, WORLD_RADIUS + SURFACE_OFFSET, 0);
        }
        else
        {
            // Normalize to sphere surface
            float surfaceRadius = WORLD_RADIUS + SURFACE_OFFSET;
            _position = new DbVector3(
                x / magnitude * surfaceRadius,
                y / magnitude * surfaceRadius,
                z / magnitude * surfaceRadius
            );
        }

        // Align rotation to new surface normal
        AlignToSurfaceNormal();
        Console.WriteLine($"[World] Teleported to ({_position.X:F1}, {_position.Y:F1}, {_position.Z:F1})");
    }

    /// <summary>
    /// Reset position to the north pole (starting position)
    /// </summary>
    public void ResetToStart()
    {
        _position = new DbVector3(0, WORLD_RADIUS + SURFACE_OFFSET, 0);
        _rotation = new DbQuaternion(0, 0, 0, 1);
        StopWalking();
        Console.WriteLine("[World] Reset to starting position (north pole)");
    }

    #region Continuous Walking (like holding WASD)

    /// <summary>
    /// Start walking in a direction for a specified duration.
    /// Like holding W for 5 seconds.
    /// </summary>
    /// <param name="forward">-1 to 1 (negative = backward)</param>
    /// <param name="right">-1 to 1 (negative = left)</param>
    /// <param name="durationSeconds">How long to walk, or -1 for indefinite</param>
    public void StartWalkingForDuration(float forward, float right, float durationSeconds)
    {
        _walkForward = Math.Clamp(forward, -1f, 1f);
        _walkRight = Math.Clamp(right, -1f, 1f);
        _walkDuration = durationSeconds;
        _walkDistance = -1; // not using distance mode
        _distanceTraveled = 0;
        Console.WriteLine($"[World] Started walking (fwd={_walkForward:F1}, right={_walkRight:F1}) for {durationSeconds:F1}s");
    }

    /// <summary>
    /// Start walking in a direction for a specified distance.
    /// Like holding W until you've moved 50 units.
    /// </summary>
    /// <param name="forward">-1 to 1 (negative = backward)</param>
    /// <param name="right">-1 to 1 (negative = left)</param>
    /// <param name="distanceUnits">How far to walk</param>
    public void StartWalkingForDistance(float forward, float right, float distanceUnits)
    {
        _walkForward = Math.Clamp(forward, -1f, 1f);
        _walkRight = Math.Clamp(right, -1f, 1f);
        _walkDuration = -1; // not using duration mode
        _walkDistance = distanceUnits;
        _distanceTraveled = 0;
        Console.WriteLine($"[World] Started walking (fwd={_walkForward:F1}, right={_walkRight:F1}) for {distanceUnits:F1} units");
    }

    /// <summary>
    /// Start walking indefinitely until StopWalking is called.
    /// Like holding W until you release the key.
    /// </summary>
    public void StartWalking(float forward, float right)
    {
        _walkForward = Math.Clamp(forward, -1f, 1f);
        _walkRight = Math.Clamp(right, -1f, 1f);
        _walkDuration = -1; // indefinite
        _walkDistance = -1;
        _distanceTraveled = 0;
        Console.WriteLine($"[World] Started walking (fwd={_walkForward:F1}, right={_walkRight:F1}) indefinitely");
    }

    /// <summary>
    /// Stop walking immediately.
    /// Like releasing W.
    /// </summary>
    public void StopWalking()
    {
        if (IsWalking)
        {
            Console.WriteLine($"[World] Stopped walking after {_distanceTraveled:F1} units");
        }
        _walkForward = 0;
        _walkRight = 0;
        _walkDuration = 0;
        _walkDistance = -1;
    }

    /// <summary>
    /// Get current walking status for API
    /// </summary>
    public object GetWalkingStatus()
    {
        return new
        {
            isWalking = IsWalking,
            forward = _walkForward,
            right = _walkRight,
            distanceTraveled = _distanceTraveled,
            remainingDuration = _walkDuration > 0 ? _walkDuration : (float?)null,
            remainingDistance = _walkDistance > 0 ? _walkDistance : (float?)null
        };
    }

    #endregion

    #region Plan Execution (command queue)

    /// <summary>
    /// Queue a plan of commands to execute sequentially.
    /// Timing is calculated automatically based on WALK_SPEED.
    /// </summary>
    public void QueuePlan(IEnumerable<PlanCommand> commands)
    {
        // Clear any existing plan
        _commandQueue.Clear();
        _waitRemaining = 0;
        StopWalking();

        foreach (var cmd in commands)
        {
            _commandQueue.Enqueue(cmd);
        }

        Console.WriteLine($"[World] Plan queued with {_commandQueue.Count} commands");

        // Calculate total estimated time
        float totalTime = 0;
        foreach (var cmd in _commandQueue)
        {
            totalTime += cmd.GetEstimatedDuration(WALK_SPEED);
        }
        Console.WriteLine($"[World] Estimated plan duration: {totalTime:F1} seconds");

        // Start executing first command
        StartNextCommand();
    }

    /// <summary>
    /// Start executing the next command in the queue
    /// </summary>
    private void StartNextCommand()
    {
        if (_commandQueue.Count == 0)
        {
            Console.WriteLine("[World] Plan execution complete");
            return;
        }

        var cmd = _commandQueue.Dequeue();
        Console.WriteLine($"[World] Executing: {cmd}");

        switch (cmd.Type)
        {
            case PlanCommandType.Walk:
                // Calculate direction magnitude to normalize walk
                float dirMag = MathF.Sqrt(cmd.Forward * cmd.Forward + cmd.Right * cmd.Right);
                if (dirMag > 0.001f)
                {
                    // Normalize direction so we walk at consistent speed
                    float normForward = cmd.Forward / dirMag;
                    float normRight = cmd.Right / dirMag;
                    StartWalkingForDistance(normForward, normRight, cmd.Distance);
                }
                break;

            case PlanCommandType.Rotate:
                // Convert radians to degrees and rotate
                float degrees = cmd.Radians * 180f / MathF.PI;
                Rotate(degrees);
                SendPositionUpdate();
                // Small delay after rotation, then next command
                _waitRemaining = 0.1f;
                break;

            case PlanCommandType.Wait:
                _waitRemaining = cmd.Seconds;
                break;
        }
    }

    /// <summary>
    /// Process plan queue - called each frame
    /// </summary>
    private void ProcessPlan(float deltaTime)
    {
        // If waiting, count down
        if (_waitRemaining > 0)
        {
            _waitRemaining -= deltaTime;
            if (_waitRemaining <= 0)
            {
                _waitRemaining = 0;
                StartNextCommand();
            }
            return;
        }

        // If not walking anymore and have more commands, start next
        if (!IsWalking && _commandQueue.Count > 0)
        {
            StartNextCommand();
        }
    }

    /// <summary>
    /// Get current plan execution status
    /// </summary>
    public object GetPlanStatus()
    {
        return new
        {
            isExecutingPlan = IsExecutingPlan,
            commandsRemaining = _commandQueue.Count,
            waitRemaining = _waitRemaining > 0 ? _waitRemaining : (float?)null,
            walking = IsWalking ? GetWalkingStatus() : null
        };
    }

    /// <summary>
    /// Cancel the current plan
    /// </summary>
    public void CancelPlan()
    {
        _commandQueue.Clear();
        _waitRemaining = 0;
        StopWalking();
        Console.WriteLine("[World] Plan cancelled");
    }

    #endregion

    /// <summary>
    /// Move along the sphere surface in a given direction.
    /// Direction is in local space (forward = positive Z, right = positive X).
    /// </summary>
    public void Move(float forwardAmount, float rightAmount)
    {
        if (Math.Abs(forwardAmount) < 0.001f && Math.Abs(rightAmount) < 0.001f)
            return;

        // Get surface normal (position normalized = "up" on sphere)
        float magnitude = MathF.Sqrt(_position.X * _position.X + _position.Y * _position.Y + _position.Z * _position.Z);
        if (magnitude < 0.001f) magnitude = WORLD_RADIUS;

        float upX = _position.X / magnitude;
        float upY = _position.Y / magnitude;
        float upZ = _position.Z / magnitude;

        // Get forward direction from rotation (simplified - assuming Y-up world)
        // The quaternion forward is typically (0, 0, 1) rotated by the quaternion
        var forward = RotateVector(0, 0, 1, _rotation);

        // Project forward onto sphere surface (remove the up component)
        float dot = forward.x * upX + forward.y * upY + forward.z * upZ;
        float tangentFwdX = forward.x - dot * upX;
        float tangentFwdY = forward.y - dot * upY;
        float tangentFwdZ = forward.z - dot * upZ;

        // Normalize tangent forward
        float tangentMag = MathF.Sqrt(tangentFwdX * tangentFwdX + tangentFwdY * tangentFwdY + tangentFwdZ * tangentFwdZ);
        if (tangentMag > 0.001f)
        {
            tangentFwdX /= tangentMag;
            tangentFwdY /= tangentMag;
            tangentFwdZ /= tangentMag;
        }

        // Calculate right as cross product of up and forward
        float rightX = upY * tangentFwdZ - upZ * tangentFwdY;
        float rightY = upZ * tangentFwdX - upX * tangentFwdZ;
        float rightZ = upX * tangentFwdY - upY * tangentFwdX;

        // Calculate movement vector on tangent plane
        float moveX = tangentFwdX * forwardAmount + rightX * rightAmount;
        float moveY = tangentFwdY * forwardAmount + rightY * rightAmount;
        float moveZ = tangentFwdZ * forwardAmount + rightZ * rightAmount;

        // Apply movement
        float newX = _position.X + moveX;
        float newY = _position.Y + moveY;
        float newZ = _position.Z + moveZ;

        // Project back onto sphere surface (at 301 radius, not 300)
        float newMag = MathF.Sqrt(newX * newX + newY * newY + newZ * newZ);
        if (newMag > 0.001f)
        {
            float surfaceRadius = WORLD_RADIUS + SURFACE_OFFSET;
            _position.X = newX / newMag * surfaceRadius;
            _position.Y = newY / newMag * surfaceRadius;
            _position.Z = newZ / newMag * surfaceRadius;
        }

        // Align rotation to new surface normal
        AlignToSurfaceNormal();
    }

    /// <summary>
    /// Rotate (yaw) around the surface normal
    /// </summary>
    public void Rotate(float yawDegrees)
    {
        if (Math.Abs(yawDegrees) < 0.001f)
            return;

        // Get up vector (surface normal)
        float magnitude = MathF.Sqrt(_position.X * _position.X + _position.Y * _position.Y + _position.Z * _position.Z);
        if (magnitude < 0.001f) magnitude = WORLD_RADIUS;

        float upX = _position.X / magnitude;
        float upY = _position.Y / magnitude;
        float upZ = _position.Z / magnitude;

        // Create rotation quaternion around up axis
        float halfAngle = yawDegrees * MathF.PI / 360f; // degrees to radians, halved
        float sinHalf = MathF.Sin(halfAngle);
        float cosHalf = MathF.Cos(halfAngle);

        var yawQuat = new DbQuaternion(
            upX * sinHalf,
            upY * sinHalf,
            upZ * sinHalf,
            cosHalf
        );

        // Multiply quaternions: yawQuat * _rotation
        _rotation = MultiplyQuaternions(yawQuat, _rotation);
        NormalizeQuaternion(ref _rotation);
    }

    /// <summary>
    /// Update walking and send position to server.
    /// Called each frame (~60fps).
    /// </summary>
    /// <param name="currentTime">Current time in seconds since start</param>
    /// <param name="deltaTime">Time since last update in seconds</param>
    public void Update(float currentTime, float deltaTime)
    {
        // Process continuous walking
        if (IsWalking)
        {
            ProcessWalking(deltaTime);
        }

        // Process plan queue (waits and command transitions)
        if (IsExecutingPlan)
        {
            ProcessPlan(deltaTime);
        }

        // Send position update to server at fixed interval
        if (currentTime - _lastUpdateTime >= POSITION_UPDATE_INTERVAL)
        {
            SendPositionUpdate();
            _lastUpdateTime = currentTime;
        }
    }

    /// <summary>
    /// Legacy Update without deltaTime (for backward compatibility)
    /// </summary>
    public void Update(float currentTime)
    {
        Update(currentTime, 0.016f); // Assume ~60fps
    }

    /// <summary>
    /// Process one frame of walking movement
    /// </summary>
    private void ProcessWalking(float deltaTime)
    {
        if (!IsWalking) return;

        // Calculate movement for this frame
        float moveSpeed = WALK_SPEED * deltaTime;
        float forwardMove = _walkForward * moveSpeed;
        float rightMove = _walkRight * moveSpeed;

        // Apply movement
        Move(forwardMove, rightMove);

        // Track distance traveled
        float frameDistance = MathF.Sqrt(forwardMove * forwardMove + rightMove * rightMove);
        _distanceTraveled += frameDistance;

        // Update duration mode
        if (_walkDuration > 0)
        {
            _walkDuration -= deltaTime;
            if (_walkDuration <= 0)
            {
                Console.WriteLine($"[World] Walk duration complete, traveled {_distanceTraveled:F1} units");
                StopWalking();
            }
        }

        // Update distance mode
        if (_walkDistance > 0)
        {
            _walkDistance -= frameDistance;
            if (_walkDistance <= 0)
            {
                Console.WriteLine($"[World] Walk distance complete, traveled {_distanceTraveled:F1} units");
                StopWalking();
            }
        }
    }

    /// <summary>
    /// Force send position update to server
    /// </summary>
    public void SendPositionUpdate()
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        conn.Reducers.UpdatePlayerPosition(_position, _rotation);
    }

    /// <summary>
    /// Calculate distance between two positions on the sphere surface
    /// </summary>
    public static float Distance(DbVector3 a, DbVector3 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// Calculate direction from current position to target
    /// </summary>
    public (float forward, float right) GetDirectionTo(DbVector3 target)
    {
        // Vector from current to target
        float dx = target.X - _position.X;
        float dy = target.Y - _position.Y;
        float dz = target.Z - _position.Z;

        // Get up vector
        float magnitude = MathF.Sqrt(_position.X * _position.X + _position.Y * _position.Y + _position.Z * _position.Z);
        float upX = _position.X / magnitude;
        float upY = _position.Y / magnitude;
        float upZ = _position.Z / magnitude;

        // Project direction onto tangent plane
        float dot = dx * upX + dy * upY + dz * upZ;
        float tangentX = dx - dot * upX;
        float tangentY = dy - dot * upY;
        float tangentZ = dz - dot * upZ;

        // Get forward direction from rotation
        var forward = RotateVector(0, 0, 1, _rotation);

        // Project forward onto tangent plane
        dot = forward.x * upX + forward.y * upY + forward.z * upZ;
        float fwdTangentX = forward.x - dot * upX;
        float fwdTangentY = forward.y - dot * upY;
        float fwdTangentZ = forward.z - dot * upZ;
        float fwdMag = MathF.Sqrt(fwdTangentX * fwdTangentX + fwdTangentY * fwdTangentY + fwdTangentZ * fwdTangentZ);
        if (fwdMag > 0.001f)
        {
            fwdTangentX /= fwdMag;
            fwdTangentY /= fwdMag;
            fwdTangentZ /= fwdMag;
        }

        // Calculate right direction
        float rightX = upY * fwdTangentZ - upZ * fwdTangentY;
        float rightY = upZ * fwdTangentX - upX * fwdTangentZ;
        float rightZ = upX * fwdTangentY - upY * fwdTangentX;

        // Dot products give forward and right components
        float forwardComponent = tangentX * fwdTangentX + tangentY * fwdTangentY + tangentZ * fwdTangentZ;
        float rightComponent = tangentX * rightX + tangentY * rightY + tangentZ * rightZ;

        return (forwardComponent, rightComponent);
    }

    #region Math Helpers

    private static (float x, float y, float z) RotateVector(float vx, float vy, float vz, DbQuaternion q)
    {
        // Quaternion rotation: q * v * q^-1
        float qx = q.X, qy = q.Y, qz = q.Z, qw = q.W;

        // q * v (treating v as quaternion with w=0)
        float tx = qw * vx + qy * vz - qz * vy;
        float ty = qw * vy + qz * vx - qx * vz;
        float tz = qw * vz + qx * vy - qy * vx;
        float tw = -(qx * vx + qy * vy + qz * vz);

        // result * q^-1 (conjugate for unit quaternion)
        float rx = tx * qw + tw * (-qx) + ty * (-qz) - tz * (-qy);
        float ry = ty * qw + tw * (-qy) + tz * (-qx) - tx * (-qz);
        float rz = tz * qw + tw * (-qz) + tx * (-qy) - ty * (-qx);

        return (rx, ry, rz);
    }

    private static DbQuaternion MultiplyQuaternions(DbQuaternion a, DbQuaternion b)
    {
        return new DbQuaternion(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z
        );
    }

    private static void NormalizeQuaternion(ref DbQuaternion q)
    {
        float mag = MathF.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
        if (mag > 0.001f)
        {
            q.X /= mag;
            q.Y /= mag;
            q.Z /= mag;
            q.W /= mag;
        }
    }

    /// <summary>
    /// Convert rotation matrix to quaternion.
    /// Matrix columns are: right (X), up (Y), forward (Z).
    /// </summary>
    private static DbQuaternion MatrixToQuaternion(
        float m00, float m01, float m02,
        float m10, float m11, float m12,
        float m20, float m21, float m22)
    {
        float trace = m00 + m11 + m22;
        float x, y, z, w;

        if (trace > 0)
        {
            float s = 0.5f / MathF.Sqrt(trace + 1.0f);
            w = 0.25f / s;
            x = (m21 - m12) * s;
            y = (m02 - m20) * s;
            z = (m10 - m01) * s;
        }
        else if (m00 > m11 && m00 > m22)
        {
            float s = 2.0f * MathF.Sqrt(1.0f + m00 - m11 - m22);
            w = (m21 - m12) / s;
            x = 0.25f * s;
            y = (m01 + m10) / s;
            z = (m02 + m20) / s;
        }
        else if (m11 > m22)
        {
            float s = 2.0f * MathF.Sqrt(1.0f + m11 - m00 - m22);
            w = (m02 - m20) / s;
            x = (m01 + m10) / s;
            y = 0.25f * s;
            z = (m12 + m21) / s;
        }
        else
        {
            float s = 2.0f * MathF.Sqrt(1.0f + m22 - m00 - m11);
            w = (m10 - m01) / s;
            x = (m02 + m20) / s;
            y = (m12 + m21) / s;
            z = 0.25f * s;
        }

        return new DbQuaternion(x, y, z, w);
    }

    /// <summary>
    /// Align player rotation so "up" points along surface normal.
    /// Equivalent to Unity's Quaternion.LookRotation(forward, up).
    /// </summary>
    private void AlignToSurfaceNormal()
    {
        // Calculate surface normal (up vector)
        float mag = MathF.Sqrt(_position.X * _position.X + _position.Y * _position.Y + _position.Z * _position.Z);
        if (mag < 0.001f) return;

        float upX = _position.X / mag;
        float upY = _position.Y / mag;
        float upZ = _position.Z / mag;

        // Get current forward from rotation
        var forward = RotateVector(0, 0, 1, _rotation);

        // Project forward onto tangent plane (remove up component)
        float dot = forward.x * upX + forward.y * upY + forward.z * upZ;
        float fwdX = forward.x - dot * upX;
        float fwdY = forward.y - dot * upY;
        float fwdZ = forward.z - dot * upZ;

        // Normalize projected forward
        float fwdMag = MathF.Sqrt(fwdX * fwdX + fwdY * fwdY + fwdZ * fwdZ);
        if (fwdMag < 0.001f)
        {
            // Forward is parallel to up, pick arbitrary tangent
            fwdX = -upY; fwdY = upX; fwdZ = 0;
            fwdMag = MathF.Sqrt(fwdX * fwdX + fwdY * fwdY + fwdZ * fwdZ);
            if (fwdMag < 0.001f) { fwdX = 0; fwdY = -upZ; fwdZ = upY; }
            fwdMag = MathF.Sqrt(fwdX * fwdX + fwdY * fwdY + fwdZ * fwdZ);
        }
        fwdX /= fwdMag; fwdY /= fwdMag; fwdZ /= fwdMag;

        // Calculate right = up × forward
        float rightX = upY * fwdZ - upZ * fwdY;
        float rightY = upZ * fwdX - upX * fwdZ;
        float rightZ = upX * fwdY - upY * fwdX;

        // Build rotation matrix and convert to quaternion
        // Matrix: [right, up, forward] columns
        _rotation = MatrixToQuaternion(
            rightX, upX, fwdX,
            rightY, upY, fwdY,
            rightZ, upZ, fwdZ
        );
    }

    #endregion
}
