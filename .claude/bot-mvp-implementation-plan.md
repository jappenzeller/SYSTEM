# Bot MVP Implementation Plan

## Overview
MVP: Basic bot that can enter world, move, mine + social bot for Twitch integration

## Phase 1: Headless Client Foundation (Week 1)
**Goal**: Basic headless connection to SpacetimeDB

**Steps**:
1. Create `HeadlessGameManager.cs` - pure C# SpacetimeDB connection
2. Test local connection to existing server
3. Implement basic player creation and login
4. **Verification**: Console app connects and creates player

**Test Command**:
```bash
dotnet run -- --server "http://localhost:3000" --module "system" --username "TestBot1"
```

## Phase 2: Basic Bot Actions (Week 1-2)
**Goal**: Move and mine functionality

**Steps**:
1. Add `BasicBotController.cs` with move/mine commands
2. Implement position updates via existing reducers
3. Add wave packet detection and mining logic
4. **Verification**: Bot spawns, moves to wave packet, mines successfully

**Test Commands**:
```bash
# Test movement
dotnet run -- --action move --x 10 --y 0 --z 5

# Test mining
dotnet run -- --action mine --target-frequency 440
```

## Phase 3: Console Interface (Week 2)
**Goal**: Manual bot control for testing

**Steps**:
1. Create `BotConsole.cs` with command parser
2. Add real-time game state display
3. Implement manual command input
4. **Verification**: Interactive console controls bot in real-time

**Console Commands**:
```
> connect localhost:3000 system
> create-player "ManualBot"
> move 10 0 5
> mine 440
> status
```

## Phase 4: Twitch Integration Foundation (Week 2-3)
**Goal**: Basic Twitch chat connection

**Steps**:
1. Create `TwitchChatClient.cs` using TwitchLib
2. Connect to Twitch channel
3. Parse basic commands from chat
4. **Verification**: Bot responds to `!hello` in Twitch chat

**Twitch Commands**:
```
!hello -> "Bot online and ready!"
!status -> "Currently at world (1,0,0), mining frequency 440Hz"
```

## Phase 5: Social Bot Commands (Week 3)
**Goal**: Twitch controls game bot

**Steps**:
1. Link Twitch commands to bot actions
2. Add command cooldowns and permissions
3. Implement viewer feedback in chat
4. **Verification**: Twitch viewers can control bot movement and mining

**Social Commands**:
```
!move 5 0 3 -> Bot moves to coordinates
!mine 880 -> Bot starts mining 880Hz frequency
!explore -> Bot moves to random location
```

## Phase 6: In-Game Social Features (Week 3-4)
**Goal**: Bot communicates with players in-game

**Steps**:
1. Add chat system integration or proximity detection
2. Implement bot responses to nearby players
3. Add Twitch viewer -> in-game player messaging
4. **Verification**: Bot greets players and relays Twitch messages

## Phase 7: AWS Deployment Prep (Week 4)
**Goal**: Containerized bot ready for cloud

**Steps**:
1. Create Dockerfile for headless client
2. Add environment configuration
3. Test container locally with Docker
4. **Verification**: Docker container runs bot successfully

**Docker Test**:
```bash
docker build -t system-bot .
docker run -e SERVER_URL="https://maincloud.spacetimedb.com" -e MODULE_NAME="system-test" system-bot
```

## Phase 8: Basic AWS Deployment (Week 4-5)
**Goal**: Single bot running on AWS

**Steps**:
1. Create ECS task definition
2. Deploy to ECS Fargate
3. Add CloudWatch logging
4. **Verification**: Bot runs 24/7 on AWS, visible in game

## Phase 9: Twitch Integration on AWS (Week 5)
**Goal**: Cloud-hosted social bot

**Steps**:
1. Add Twitch credentials to AWS Secrets Manager
2. Deploy Twitch-enabled bot to ECS
3. Test end-to-end: Twitch chat -> AWS bot -> game action
4. **Verification**: Twitch viewers control AWS-hosted bot

## Phase 10: Monitoring & Scaling (Week 5-6)
**Goal**: Production-ready bot system

**Steps**:
1. Add CloudWatch metrics and alarms
2. Implement auto-restart on failures
3. Add bot status dashboard
4. **Verification**: System recovers from failures automatically

## Verification Checkpoints

**Week 1**: Headless client connects, creates player, moves
**Week 2**: Bot mines, console control works
**Week 3**: Twitch commands control bot
**Week 4**: In-game communication, Docker ready
**Week 5**: AWS deployment, cloud Twitch integration
**Week 6**: Production monitoring

## File Structure
```
SYSTEM-headless-client/
├── src/
│   ├── HeadlessGameManager.cs
│   ├── BasicBotController.cs
│   ├── TwitchChatClient.cs
│   └── BotConsole.cs
├── Dockerfile
├── docker-compose.yml
└── aws/
    ├── task-definition.json
    └── deploy.sh
```

## Status
- [ ] Phase 1: Headless Foundation
- [ ] Phase 2: Basic Actions
- [ ] Phase 3: Console Interface
- [ ] Phase 4: Twitch Foundation
- [ ] Phase 5: Social Commands
- [ ] Phase 6: In-Game Social
- [ ] Phase 7: AWS Prep
- [ ] Phase 8: AWS Deploy
- [ ] Phase 9: Cloud Twitch
- [ ] Phase 10: Production Ready