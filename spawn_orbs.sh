#!/bin/bash
# Spawn 20 mixed orbs in a circle around superstringman's position
# Position: (-19.918457, 299.8741, -16.808578)

BASE_X=-19.9
BASE_Y=299.9
BASE_Z=-16.8
RADIUS=10

for i in {1..20}; do
    # Calculate angle for circular distribution
    ANGLE=$(echo "scale=4; 3.14159 * 2 * $i / 20" | bc)
    
    # Calculate x and z offsets
    X_OFFSET=$(echo "scale=4; $RADIUS * c($ANGLE)" | bc -l)
    Z_OFFSET=$(echo "scale=4; $RADIUS * s($ANGLE)" | bc -l)
    
    # Calculate final position
    X=$(echo "scale=2; $BASE_X + $X_OFFSET" | bc)
    Z=$(echo "scale=2; $BASE_Z + $Z_OFFSET" | bc)
    
    echo "Spawning orb $i at ($X, $BASE_Y, $Z)"
    spacetime call system --server local spawn_test_orb -- $X $BASE_Y $Z
    sleep 0.2
done

echo "Spawned 20 orbs in a circle around the player!"
