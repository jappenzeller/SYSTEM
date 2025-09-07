-- Post-deployment verification queries for SYSTEM SpacetimeDB
-- These queries verify the database structure and basic functionality
-- Each query is separated by GO statements for batch execution

-- Check core tables exist and have correct structure
SELECT COUNT(*) as player_table_exists FROM sqlite_master WHERE type='table' AND name='Player';
GO

SELECT COUNT(*) as world_table_exists FROM sqlite_master WHERE type='table' AND name='World';
GO

SELECT COUNT(*) as orb_table_exists FROM sqlite_master WHERE type='table' AND name='Orb';
GO

SELECT COUNT(*) as crystal_table_exists FROM sqlite_master WHERE type='table' AND name='Crystal';
GO

SELECT COUNT(*) as wave_packet_table_exists FROM sqlite_master WHERE type='table' AND name='WavePacket';
GO

-- Verify Player table columns
SELECT sql FROM sqlite_master WHERE type='table' AND name='Player';
GO

-- Check for essential indexes (performance critical)
SELECT name, sql FROM sqlite_master WHERE type='index' AND tbl_name='Player';
GO

SELECT name, sql FROM sqlite_master WHERE type='index' AND tbl_name='Orb';
GO

-- Verify default world exists
SELECT COUNT(*) as default_world_count FROM World WHERE name = 'Default';
GO

-- Check if any test data exists
SELECT 
    (SELECT COUNT(*) FROM Player) as player_count,
    (SELECT COUNT(*) FROM World) as world_count,
    (SELECT COUNT(*) FROM Orb) as orb_count,
    (SELECT COUNT(*) FROM Crystal) as crystal_count,
    (SELECT COUNT(*) FROM WavePacket) as wave_packet_count;
GO

-- Verify LoggedOutPlayer table for disconnect handling
SELECT COUNT(*) as logged_out_player_table FROM sqlite_master WHERE type='table' AND name='LoggedOutPlayer';
GO

-- Check reducer functions are registered (via system tables if available)
SELECT COUNT(*) as reducer_count FROM sqlite_master WHERE type='table' AND name LIKE '%reducer%';
GO

-- Test basic data integrity
SELECT 
    CASE 
        WHEN COUNT(*) > 0 THEN 'FAIL: Orphaned crystals found'
        ELSE 'PASS: No orphaned crystals'
    END as crystal_integrity_check
FROM Crystal c
WHERE NOT EXISTS (SELECT 1 FROM Player p WHERE p.id = c.player_id);
GO

-- Check for any active wave packets without valid orbs
SELECT 
    CASE 
        WHEN COUNT(*) > 0 THEN 'FAIL: Orphaned wave packets found'
        ELSE 'PASS: No orphaned wave packets'
    END as wave_packet_integrity_check
FROM WavePacket w
WHERE NOT EXISTS (SELECT 1 FROM Orb o WHERE o.id = w.orb_id);
GO

-- Verify world boundaries are reasonable
SELECT 
    id,
    name,
    CASE 
        WHEN radius <= 0 THEN 'FAIL: Invalid radius'
        WHEN radius > 10000 THEN 'WARNING: Very large world'
        ELSE 'PASS'
    END as world_size_check,
    radius
FROM World;
GO

-- Check for duplicate player names (should be unique)
SELECT 
    username,
    COUNT(*) as duplicate_count
FROM Player
GROUP BY username
HAVING COUNT(*) > 1;
GO

-- Verify orb frequency distribution
SELECT 
    frequency,
    COUNT(*) as orb_count,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM Orb), 2) as percentage
FROM Orb
GROUP BY frequency
ORDER BY frequency;
GO

-- Check player position sanity (not at origin or extreme values)
SELECT 
    COUNT(*) as players_at_origin
FROM Player
WHERE position_x = 0 AND position_y = 0 AND position_z = 0;
GO

-- Verify timestamp columns are being set
SELECT 
    CASE 
        WHEN COUNT(*) > 0 THEN 'WARNING: Players without timestamps'
        ELSE 'PASS: All players have timestamps'
    END as timestamp_check
FROM Player
WHERE created_at IS NULL OR last_login IS NULL;
GO

-- Check for any stuck mining states
SELECT 
    p.username,
    p.is_mining,
    p.mining_target,
    CASE 
        WHEN p.is_mining = 1 AND p.mining_target IS NULL THEN 'ERROR: Mining without target'
        WHEN p.is_mining = 0 AND p.mining_target IS NOT NULL THEN 'WARNING: Has target but not mining'
        ELSE 'OK'
    END as mining_state_check
FROM Player p
WHERE p.is_mining = 1 OR p.mining_target IS NOT NULL;
GO

-- Performance check: Table sizes
SELECT 
    'Player' as table_name,
    COUNT(*) as row_count,
    CASE 
        WHEN COUNT(*) > 10000 THEN 'WARNING: Large table'
        ELSE 'OK'
    END as size_check
FROM Player
UNION ALL
SELECT 
    'WavePacket' as table_name,
    COUNT(*) as row_count,
    CASE 
        WHEN COUNT(*) > 100000 THEN 'WARNING: Large table - consider cleanup'
        ELSE 'OK'
    END as size_check
FROM WavePacket
UNION ALL
SELECT 
    'LoggedOutPlayer' as table_name,
    COUNT(*) as row_count,
    CASE 
        WHEN COUNT(*) > 50000 THEN 'WARNING: Large history - consider archiving'
        ELSE 'OK'
    END as size_check
FROM LoggedOutPlayer;
GO

-- Final verification summary
SELECT 
    'Deployment Verification Complete' as status,
    datetime('now') as verification_time,
    (SELECT COUNT(*) FROM sqlite_master WHERE type='table') as total_tables,
    (SELECT COUNT(*) FROM sqlite_master WHERE type='index') as total_indexes;
GO