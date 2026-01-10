# Claude Code Prompt: QAI Memory System - Phase 1 Implementation

## Objective

Implement a working memory system for QAI that enables persistent player recognition, contextual recall, observation storage, and evolution tracking. This system will be the foundation for MCP tools (Phase 2) and GraphQL API (Phase 3).

## Success Criteria

1. QAI remembers players across sessions
2. QAI recalls relevant context before generating responses
3. QAI stores observations about players and patterns
4. Observations can be promoted from short-term to long-term memory
5. Evolution state persists and increments correctly
6. Memory is bounded via TTL and consolidation
7. Response latency with memory lookup < 500ms

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                      QAI Application                             │
│                                                                  │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐      │
│  │   Response   │───▶│   Memory     │───▶│   Prompt     │      │
│  │   Handler    │    │   Service    │    │   Builder    │      │
│  └──────────────┘    └──────┬───────┘    └──────────────┘      │
│                             │                                    │
└─────────────────────────────┼────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              │               │               │
              ▼               ▼               ▼
      ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
      │  DynamoDB    │ │   Bedrock    │ │   In-Memory  │
      │  (Structured)│ │   (Semantic) │ │   (Working)  │
      └──────────────┘ └──────────────┘ └──────────────┘
```

---

## Project Structure

Add to existing QAI project (or create new module):

```
qai/
├── memory/
│   ├── __init__.py
│   ├── service.py              # Main MemoryService class
│   ├── models.py               # Data models (Pydantic)
│   ├── dynamo.py               # DynamoDB operations
│   ├── semantic.py             # Vector/semantic search
│   ├── working.py              # In-memory session store
│   ├── consolidation.py        # Memory maintenance jobs
│   └── prompt_builder.py       # Memory injection into prompts
├── config.py                   # Configuration
└── tests/
    └── test_memory.py          # Unit tests
```

---

## Step 1: Configuration

### config.py

```python
from pydantic_settings import BaseSettings
from typing import Optional

class MemoryConfig(BaseSettings):
    # DynamoDB
    dynamodb_table_name: str = "QAI-Memory"
    dynamodb_region: str = "us-east-1"
    dynamodb_endpoint: Optional[str] = None  # For local testing
    
    # Memory TTLs (seconds)
    short_term_ttl: int = 72 * 60 * 60  # 72 hours
    interaction_ttl: int = 72 * 60 * 60  # 72 hours
    
    # Limits
    max_observations_per_player: int = 100
    max_interactions_per_player: int = 500
    observation_sample_rate: float = 0.1  # 10% of interactions generate observations
    
    # Promotion thresholds
    observation_promotion_confidence: float = 0.8
    observation_promotion_evidence: int = 3
    
    # Evolution thresholds
    evolution_stage_2_interactions: int = 10000
    evolution_stage_3_interactions: int = 100000
    
    # Semantic search (optional for Phase 1)
    enable_semantic_search: bool = False
    bedrock_embedding_model: str = "amazon.titan-embed-text-v1"
    
    class Config:
        env_prefix = "QAI_MEMORY_"
```

---

## Step 2: Data Models

### models.py

```python
from pydantic import BaseModel, Field
from typing import Optional, List, Dict, Any
from datetime import datetime
from enum import Enum

# === Enums ===

class MemoryTier(str, Enum):
    WORKING = "WORKING"
    SHORT_TERM = "SHORT_TERM"
    LONG_TERM = "LONG_TERM"

class EntityType(str, Enum):
    PLAYER = "PLAYER"
    PATTERN = "PATTERN"
    INSIGHT = "INSIGHT"
    INTERACTION = "INTERACTION"
    OBSERVATION = "OBSERVATION"
    EVOLUTION = "EVOLUTION"
    SESSION = "SESSION"

class ObservationCategory(str, Enum):
    BEHAVIORAL = "behavioral"
    PATTERN = "pattern"
    EVENT = "event"
    SOCIAL = "social"
    PREFERENCE = "preference"

class Channel(str, Enum):
    TWITCH = "twitch"
    INGAME = "ingame"
    API = "api"

# === Core Models ===

class PlayerProfile(BaseModel):
    """Long-term player profile."""
    name: str
    first_seen: datetime
    last_seen: datetime
    total_interactions: int = 0
    total_solutions: int = 0
    
    # Learned preferences
    preferred_patterns: List[str] = Field(default_factory=list)
    average_fidelity: Optional[float] = None
    play_style: Optional[str] = None  # "speed_focused", "accuracy_focused", "explorer"
    
    # Relationship with QAI
    sentiment_score: float = 0.0  # -1 to 1
    helpfulness_score: float = 0.0  # 0 to 1
    memorable_moments: List[str] = Field(default_factory=list, max_length=10)
    
    class Config:
        json_encoders = {datetime: lambda v: v.isoformat()}

class Interaction(BaseModel):
    """Record of a single interaction."""
    interaction_id: str
    timestamp: datetime
    player: str
    channel: Channel
    message: str
    qai_response: Optional[str] = None
    sentiment: Optional[str] = None  # "positive", "neutral", "negative"
    
    # Context at time of interaction
    context: Dict[str, Any] = Field(default_factory=dict)
    
    # Metadata
    memory_tier: MemoryTier = MemoryTier.SHORT_TERM
    ttl: Optional[int] = None  # Unix timestamp for expiration

class Observation(BaseModel):
    """QAI's observation about a player, pattern, or general insight."""
    observation_id: str
    timestamp: datetime
    subject: str  # "PLAYER#Jim", "PATTERN#7B", "GENERAL"
    content: str
    category: ObservationCategory
    confidence: float = 0.5  # 0 to 1
    evidence_count: int = 1
    
    # Promotion tracking
    promoted: bool = False
    memory_tier: MemoryTier = MemoryTier.SHORT_TERM
    ttl: Optional[int] = None

class PatternStats(BaseModel):
    """Statistics for a learned pattern."""
    pattern_id: str
    gate_sequence: List[str] = Field(default_factory=list)
    times_observed: int = 0
    average_fidelity: float = 0.0
    average_time_ms: int = 0
    top_players: List[str] = Field(default_factory=list, max_length=10)
    discovered_by: Optional[str] = None
    discovered_at: Optional[datetime] = None

class Insight(BaseModel):
    """General insight QAI has derived."""
    insight_id: str
    content: str
    category: str  # "behavioral_correlation", "temporal", "social"
    confidence: float
    evidence_count: int
    created_at: datetime
    last_validated: Optional[datetime] = None

class EvolutionState(BaseModel):
    """QAI's evolution progress."""
    stage: int = 1
    stage_name: str = "Wonder"
    total_interactions: int = 0
    total_solutions_observed: int = 0
    unique_players_met: int = 0
    
    # Personality parameters (shift over time)
    curiosity_score: float = 0.85  # Decreases as QAI evolves
    pattern_confidence: float = 0.15  # Increases as QAI evolves
    stats_verbosity: float = 0.2  # Increases in Stage 2+
    
    # Thresholds
    next_stage_at: Optional[int] = 10000
    progress_percent: float = 0.0

class SessionContext(BaseModel):
    """Working memory for current session."""
    session_id: str
    started_at: datetime
    player: Optional[str] = None
    recent_messages: List[Dict[str, str]] = Field(default_factory=list, max_length=20)
    current_topic: Optional[str] = None
    pending_questions: List[str] = Field(default_factory=list)
    
# === Response Models ===

class PlayerContext(BaseModel):
    """Everything QAI knows about a player, assembled for prompt injection."""
    profile: PlayerProfile
    recent_interactions: List[Interaction] = Field(default_factory=list)
    observations: List[Observation] = Field(default_factory=list)
    summary: Optional[str] = None  # Generated summary for prompt

class RecallResult(BaseModel):
    """Result from memory recall."""
    memories: List[Dict[str, Any]]
    sources: List[str]  # Which stores contributed
    query: str
    total_found: int

class ConsolidationResult(BaseModel):
    """Result from memory consolidation job."""
    observations_promoted: int
    memories_pruned: int
    players_updated: int
    duration_ms: int
```

---

## Step 3: DynamoDB Operations

### dynamo.py

```python
import boto3
from boto3.dynamodb.conditions import Key, Attr
from typing import Optional, List, Dict, Any
from datetime import datetime, timedelta
import uuid
import logging

from .models import (
    PlayerProfile, Interaction, Observation, PatternStats,
    Insight, EvolutionState, MemoryTier, EntityType, ObservationCategory, Channel
)
from ..config import MemoryConfig

logger = logging.getLogger(__name__)

class DynamoDBMemoryStore:
    """
    DynamoDB operations for QAI memory.
    
    Table Schema:
    - PK: Entity identifier (PLAYER#name, PATTERN#id, etc.)
    - SK: Record type or timestamp
    - GSI1: entity_type + updated_at (for listing)
    - GSI2: memory_tier + ttl (for cleanup)
    """
    
    def __init__(self, config: MemoryConfig):
        self.config = config
        self.table_name = config.dynamodb_table_name
        
        # Initialize client
        dynamodb_kwargs = {"region_name": config.dynamodb_region}
        if config.dynamodb_endpoint:
            dynamodb_kwargs["endpoint_url"] = config.dynamodb_endpoint
        
        self.dynamodb = boto3.resource("dynamodb", **dynamodb_kwargs)
        self.table = self.dynamodb.Table(self.table_name)
    
    # === Table Setup ===
    
    def create_table_if_not_exists(self):
        """Create the DynamoDB table with required indexes."""
        client = self.dynamodb.meta.client
        
        try:
            client.describe_table(TableName=self.table_name)
            logger.info(f"Table {self.table_name} already exists")
            return
        except client.exceptions.ResourceNotFoundException:
            pass
        
        logger.info(f"Creating table {self.table_name}")
        
        client.create_table(
            TableName=self.table_name,
            KeySchema=[
                {"AttributeName": "pk", "KeyType": "HASH"},
                {"AttributeName": "sk", "KeyType": "RANGE"},
            ],
            AttributeDefinitions=[
                {"AttributeName": "pk", "AttributeType": "S"},
                {"AttributeName": "sk", "AttributeType": "S"},
                {"AttributeName": "entity_type", "AttributeType": "S"},
                {"AttributeName": "updated_at", "AttributeType": "S"},
                {"AttributeName": "memory_tier", "AttributeType": "S"},
                {"AttributeName": "ttl", "AttributeType": "N"},
            ],
            GlobalSecondaryIndexes=[
                {
                    "IndexName": "entity-type-index",
                    "KeySchema": [
                        {"AttributeName": "entity_type", "KeyType": "HASH"},
                        {"AttributeName": "updated_at", "KeyType": "RANGE"},
                    ],
                    "Projection": {"ProjectionType": "ALL"},
                },
                {
                    "IndexName": "memory-tier-index",
                    "KeySchema": [
                        {"AttributeName": "memory_tier", "KeyType": "HASH"},
                        {"AttributeName": "ttl", "KeyType": "RANGE"},
                    ],
                    "Projection": {"ProjectionType": "KEYS_ONLY"},
                },
            ],
            BillingMode="PAY_PER_REQUEST",
            # Enable TTL
            TimeToLiveSpecification={
                "Enabled": True,
                "AttributeName": "ttl"
            }
        )
        
        # Wait for table to be active
        waiter = client.get_waiter("table_exists")
        waiter.wait(TableName=self.table_name)
        logger.info(f"Table {self.table_name} created successfully")
    
    # === Player Operations ===
    
    async def get_player(self, name: str) -> Optional[PlayerProfile]:
        """Get player profile by name."""
        response = self.table.get_item(
            Key={"pk": f"PLAYER#{name}", "sk": "PROFILE"}
        )
        
        item = response.get("Item")
        if not item:
            return None
        
        return PlayerProfile(
            name=item["name"],
            first_seen=datetime.fromisoformat(item["first_seen"]),
            last_seen=datetime.fromisoformat(item["last_seen"]),
            total_interactions=item.get("total_interactions", 0),
            total_solutions=item.get("total_solutions", 0),
            preferred_patterns=item.get("preferred_patterns", []),
            average_fidelity=item.get("average_fidelity"),
            play_style=item.get("play_style"),
            sentiment_score=item.get("sentiment_score", 0.0),
            helpfulness_score=item.get("helpfulness_score", 0.0),
            memorable_moments=item.get("memorable_moments", []),
        )
    
    async def create_player(self, name: str) -> PlayerProfile:
        """Create a new player profile."""
        now = datetime.utcnow()
        
        profile = PlayerProfile(
            name=name,
            first_seen=now,
            last_seen=now,
        )
        
        item = {
            "pk": f"PLAYER#{name}",
            "sk": "PROFILE",
            "entity_type": EntityType.PLAYER.value,
            "memory_tier": MemoryTier.LONG_TERM.value,
            "updated_at": now.isoformat(),
            "name": name,
            "first_seen": now.isoformat(),
            "last_seen": now.isoformat(),
            "total_interactions": 0,
            "total_solutions": 0,
            "preferred_patterns": [],
            "sentiment_score": 0.0,
            "helpfulness_score": 0.0,
            "memorable_moments": [],
        }
        
        self.table.put_item(Item=item)
        logger.info(f"Created player profile: {name}")
        
        return profile
    
    async def get_or_create_player(self, name: str) -> PlayerProfile:
        """Get existing player or create new one."""
        player = await self.get_player(name)
        if player:
            return player
        return await self.create_player(name)
    
    async def update_player(self, name: str, updates: Dict[str, Any]) -> None:
        """Update player profile fields."""
        now = datetime.utcnow()
        
        # Build update expression
        update_parts = ["#updated_at = :updated_at"]
        expr_names = {"#updated_at": "updated_at"}
        expr_values = {":updated_at": now.isoformat()}
        
        for key, value in updates.items():
            safe_key = key.replace("_", "")
            update_parts.append(f"#{safe_key} = :{safe_key}")
            expr_names[f"#{safe_key}"] = key
            if isinstance(value, datetime):
                expr_values[f":{safe_key}"] = value.isoformat()
            else:
                expr_values[f":{safe_key}"] = value
        
        self.table.update_item(
            Key={"pk": f"PLAYER#{name}", "sk": "PROFILE"},
            UpdateExpression="SET " + ", ".join(update_parts),
            ExpressionAttributeNames=expr_names,
            ExpressionAttributeValues=expr_values,
        )
    
    async def increment_player_stats(
        self,
        name: str,
        interactions: int = 0,
        solutions: int = 0
    ) -> None:
        """Increment player interaction/solution counters."""
        now = datetime.utcnow()
        
        update_expr = "SET #updated_at = :updated_at, #last_seen = :last_seen"
        expr_names = {
            "#updated_at": "updated_at",
            "#last_seen": "last_seen",
        }
        expr_values = {
            ":updated_at": now.isoformat(),
            ":last_seen": now.isoformat(),
        }
        
        if interactions > 0:
            update_expr += " ADD #total_interactions :interactions"
            expr_names["#total_interactions"] = "total_interactions"
            expr_values[":interactions"] = interactions
        
        if solutions > 0:
            update_expr += " ADD #total_solutions :solutions"
            expr_names["#total_solutions"] = "total_solutions"
            expr_values[":solutions"] = solutions
        
        self.table.update_item(
            Key={"pk": f"PLAYER#{name}", "sk": "PROFILE"},
            UpdateExpression=update_expr,
            ExpressionAttributeNames=expr_names,
            ExpressionAttributeValues=expr_values,
        )
    
    async def list_players(
        self,
        limit: int = 50,
        sort_by: str = "last_seen"
    ) -> List[PlayerProfile]:
        """List all known players."""
        response = self.table.query(
            IndexName="entity-type-index",
            KeyConditionExpression=Key("entity_type").eq(EntityType.PLAYER.value),
            ScanIndexForward=False,  # Most recent first
            Limit=limit,
        )
        
        players = []
        for item in response.get("Items", []):
            if item.get("sk") == "PROFILE":
                players.append(PlayerProfile(
                    name=item["name"],
                    first_seen=datetime.fromisoformat(item["first_seen"]),
                    last_seen=datetime.fromisoformat(item["last_seen"]),
                    total_interactions=item.get("total_interactions", 0),
                    total_solutions=item.get("total_solutions", 0),
                    preferred_patterns=item.get("preferred_patterns", []),
                    average_fidelity=item.get("average_fidelity"),
                    play_style=item.get("play_style"),
                    sentiment_score=item.get("sentiment_score", 0.0),
                    helpfulness_score=item.get("helpfulness_score", 0.0),
                    memorable_moments=item.get("memorable_moments", []),
                ))
        
        return players
    
    # === Interaction Operations ===
    
    async def store_interaction(
        self,
        player: str,
        channel: Channel,
        message: str,
        qai_response: Optional[str] = None,
        sentiment: Optional[str] = None,
        context: Optional[Dict[str, Any]] = None,
    ) -> Interaction:
        """Store a new interaction."""
        now = datetime.utcnow()
        interaction_id = f"{int(now.timestamp() * 1000)}-{uuid.uuid4().hex[:8]}"
        ttl = int((now + timedelta(seconds=self.config.interaction_ttl)).timestamp())
        
        interaction = Interaction(
            interaction_id=interaction_id,
            timestamp=now,
            player=player,
            channel=channel,
            message=message,
            qai_response=qai_response,
            sentiment=sentiment,
            context=context or {},
            memory_tier=MemoryTier.SHORT_TERM,
            ttl=ttl,
        )
        
        item = {
            "pk": f"PLAYER#{player}",
            "sk": f"INTERACTION#{interaction_id}",
            "entity_type": EntityType.INTERACTION.value,
            "memory_tier": MemoryTier.SHORT_TERM.value,
            "updated_at": now.isoformat(),
            "ttl": ttl,
            **interaction.model_dump(exclude={"memory_tier"}),
        }
        
        # Convert datetime to string
        item["timestamp"] = now.isoformat()
        
        self.table.put_item(Item=item)
        
        return interaction
    
    async def get_player_interactions(
        self,
        player: str,
        limit: int = 10
    ) -> List[Interaction]:
        """Get recent interactions with a player."""
        response = self.table.query(
            KeyConditionExpression=(
                Key("pk").eq(f"PLAYER#{player}") &
                Key("sk").begins_with("INTERACTION#")
            ),
            ScanIndexForward=False,  # Most recent first
            Limit=limit,
        )
        
        interactions = []
        for item in response.get("Items", []):
            interactions.append(Interaction(
                interaction_id=item["interaction_id"],
                timestamp=datetime.fromisoformat(item["timestamp"]),
                player=item["player"],
                channel=Channel(item["channel"]),
                message=item["message"],
                qai_response=item.get("qai_response"),
                sentiment=item.get("sentiment"),
                context=item.get("context", {}),
            ))
        
        return interactions
    
    # === Observation Operations ===
    
    async def store_observation(
        self,
        subject: str,
        content: str,
        category: ObservationCategory,
        confidence: float = 0.5,
    ) -> Observation:
        """Store a new observation."""
        now = datetime.utcnow()
        observation_id = f"{int(now.timestamp() * 1000)}-{uuid.uuid4().hex[:8]}"
        ttl = int((now + timedelta(seconds=self.config.short_term_ttl)).timestamp())
        
        observation = Observation(
            observation_id=observation_id,
            timestamp=now,
            subject=subject,
            content=content,
            category=category,
            confidence=confidence,
            evidence_count=1,
            promoted=False,
            memory_tier=MemoryTier.SHORT_TERM,
            ttl=ttl,
        )
        
        item = {
            "pk": subject,
            "sk": f"OBSERVATION#{observation_id}",
            "entity_type": EntityType.OBSERVATION.value,
            "memory_tier": MemoryTier.SHORT_TERM.value,
            "updated_at": now.isoformat(),
            "ttl": ttl,
            "observation_id": observation_id,
            "timestamp": now.isoformat(),
            "content": content,
            "category": category.value,
            "confidence": str(confidence),  # DynamoDB doesn't like floats
            "evidence_count": 1,
            "promoted": False,
        }
        
        self.table.put_item(Item=item)
        logger.debug(f"Stored observation for {subject}: {content[:50]}...")
        
        return observation
    
    async def get_observations(
        self,
        subject: str,
        limit: int = 10,
        promoted_only: bool = False
    ) -> List[Observation]:
        """Get observations for a subject."""
        key_condition = (
            Key("pk").eq(subject) &
            Key("sk").begins_with("OBSERVATION#")
        )
        
        filter_expr = None
        if promoted_only:
            filter_expr = Attr("promoted").eq(True)
        
        query_kwargs = {
            "KeyConditionExpression": key_condition,
            "ScanIndexForward": False,
            "Limit": limit,
        }
        if filter_expr:
            query_kwargs["FilterExpression"] = filter_expr
        
        response = self.table.query(**query_kwargs)
        
        observations = []
        for item in response.get("Items", []):
            observations.append(Observation(
                observation_id=item["observation_id"],
                timestamp=datetime.fromisoformat(item["timestamp"]),
                subject=item["pk"],
                content=item["content"],
                category=ObservationCategory(item["category"]),
                confidence=float(item.get("confidence", 0.5)),
                evidence_count=item.get("evidence_count", 1),
                promoted=item.get("promoted", False),
            ))
        
        return observations
    
    async def reinforce_observation(self, subject: str, observation_id: str) -> None:
        """Increase confidence and evidence count for an observation."""
        self.table.update_item(
            Key={"pk": subject, "sk": f"OBSERVATION#{observation_id}"},
            UpdateExpression=(
                "SET evidence_count = evidence_count + :inc, "
                "confidence = confidence + :conf_inc"
            ),
            ExpressionAttributeValues={
                ":inc": 1,
                ":conf_inc": "0.1",  # Increase confidence by 0.1
            },
        )
    
    async def promote_observation(self, subject: str, observation_id: str) -> None:
        """Promote an observation to long-term memory."""
        self.table.update_item(
            Key={"pk": subject, "sk": f"OBSERVATION#{observation_id}"},
            UpdateExpression=(
                "SET promoted = :promoted, "
                "memory_tier = :tier "
                "REMOVE #ttl"
            ),
            ExpressionAttributeNames={"#ttl": "ttl"},
            ExpressionAttributeValues={
                ":promoted": True,
                ":tier": MemoryTier.LONG_TERM.value,
            },
        )
        logger.info(f"Promoted observation {observation_id} for {subject}")
    
    # === Evolution Operations ===
    
    async def get_evolution_state(self) -> EvolutionState:
        """Get current QAI evolution state."""
        response = self.table.get_item(
            Key={"pk": "EVOLUTION#current", "sk": "STATE"}
        )
        
        item = response.get("Item")
        if not item:
            # Initialize default state
            return await self._initialize_evolution_state()
        
        return EvolutionState(
            stage=item.get("stage", 1),
            stage_name=item.get("stage_name", "Wonder"),
            total_interactions=item.get("total_interactions", 0),
            total_solutions_observed=item.get("total_solutions_observed", 0),
            unique_players_met=item.get("unique_players_met", 0),
            curiosity_score=float(item.get("curiosity_score", 0.85)),
            pattern_confidence=float(item.get("pattern_confidence", 0.15)),
            stats_verbosity=float(item.get("stats_verbosity", 0.2)),
            next_stage_at=item.get("next_stage_at", 10000),
            progress_percent=float(item.get("progress_percent", 0.0)),
        )
    
    async def _initialize_evolution_state(self) -> EvolutionState:
        """Create initial evolution state."""
        state = EvolutionState()
        
        item = {
            "pk": "EVOLUTION#current",
            "sk": "STATE",
            "entity_type": EntityType.EVOLUTION.value,
            "memory_tier": MemoryTier.LONG_TERM.value,
            "updated_at": datetime.utcnow().isoformat(),
            **state.model_dump(),
        }
        
        # Convert floats to strings for DynamoDB
        for key in ["curiosity_score", "pattern_confidence", "stats_verbosity", "progress_percent"]:
            item[key] = str(item[key])
        
        self.table.put_item(Item=item)
        logger.info("Initialized QAI evolution state")
        
        return state
    
    async def increment_evolution(
        self,
        interactions: int = 0,
        solutions: int = 0,
        new_player: bool = False
    ) -> EvolutionState:
        """Increment evolution counters and check for stage advancement."""
        update_expr = "SET updated_at = :now"
        expr_values = {":now": datetime.utcnow().isoformat()}
        
        if interactions > 0:
            update_expr += " ADD total_interactions :interactions"
            expr_values[":interactions"] = interactions
        
        if solutions > 0:
            update_expr += " ADD total_solutions_observed :solutions"
            expr_values[":solutions"] = solutions
        
        if new_player:
            update_expr += " ADD unique_players_met :one"
            expr_values[":one"] = 1
        
        self.table.update_item(
            Key={"pk": "EVOLUTION#current", "sk": "STATE"},
            UpdateExpression=update_expr,
            ExpressionAttributeValues=expr_values,
        )
        
        # Check for stage advancement
        state = await self.get_evolution_state()
        await self._check_stage_advancement(state)
        
        return await self.get_evolution_state()
    
    async def _check_stage_advancement(self, state: EvolutionState) -> None:
        """Check if QAI should advance to next stage."""
        new_stage = state.stage
        new_name = state.stage_name
        
        if state.stage == 1 and state.total_interactions >= self.config.evolution_stage_2_interactions:
            new_stage = 2
            new_name = "Pattern Recognition"
        elif state.stage == 2 and state.total_interactions >= self.config.evolution_stage_3_interactions:
            new_stage = 3
            new_name = "Efficiency"
        
        if new_stage != state.stage:
            # Calculate new personality parameters
            curiosity = max(0.3, 0.85 - (new_stage - 1) * 0.2)
            pattern_conf = min(0.9, 0.15 + (new_stage - 1) * 0.3)
            stats_verb = min(0.8, 0.2 + (new_stage - 1) * 0.25)
            
            next_threshold = {
                2: self.config.evolution_stage_3_interactions,
                3: None,  # Stage 4 is manual
            }.get(new_stage)
            
            self.table.update_item(
                Key={"pk": "EVOLUTION#current", "sk": "STATE"},
                UpdateExpression=(
                    "SET stage = :stage, stage_name = :name, "
                    "curiosity_score = :curiosity, pattern_confidence = :pattern, "
                    "stats_verbosity = :stats, next_stage_at = :next"
                ),
                ExpressionAttributeValues={
                    ":stage": new_stage,
                    ":name": new_name,
                    ":curiosity": str(curiosity),
                    ":pattern": str(pattern_conf),
                    ":stats": str(stats_verb),
                    ":next": next_threshold,
                },
            )
            
            logger.info(f"QAI evolved to Stage {new_stage}: {new_name}")
    
    # === Cleanup Operations ===
    
    async def prune_expired_memories(self) -> int:
        """Delete expired short-term memories. Returns count of deleted items."""
        now = int(datetime.utcnow().timestamp())
        
        # Query for expired items
        response = self.table.query(
            IndexName="memory-tier-index",
            KeyConditionExpression=(
                Key("memory_tier").eq(MemoryTier.SHORT_TERM.value) &
                Key("ttl").lt(now)
            ),
            Limit=100,  # Process in batches
        )
        
        items = response.get("Items", [])
        deleted = 0
        
        with self.table.batch_writer() as batch:
            for item in items:
                batch.delete_item(Key={"pk": item["pk"], "sk": item["sk"]})
                deleted += 1
        
        if deleted > 0:
            logger.info(f"Pruned {deleted} expired memories")
        
        return deleted
```

---

## Step 4: Working Memory (Session Context)

### working.py

```python
from typing import Dict, Optional, List
from datetime import datetime, timedelta
from .models import SessionContext

class WorkingMemoryStore:
    """
    In-memory store for current session context.
    Fast access for conversation state that doesn't need persistence.
    """
    
    def __init__(self, session_ttl_minutes: int = 60):
        self._sessions: Dict[str, SessionContext] = {}
        self._session_ttl = timedelta(minutes=session_ttl_minutes)
    
    def get_session(self, session_id: str) -> Optional[SessionContext]:
        """Get session context, returns None if expired or not found."""
        session = self._sessions.get(session_id)
        
        if session:
            # Check if expired
            if datetime.utcnow() - session.started_at > self._session_ttl:
                del self._sessions[session_id]
                return None
        
        return session
    
    def get_or_create_session(self, session_id: str, player: Optional[str] = None) -> SessionContext:
        """Get existing session or create new one."""
        session = self.get_session(session_id)
        
        if not session:
            session = SessionContext(
                session_id=session_id,
                started_at=datetime.utcnow(),
                player=player,
            )
            self._sessions[session_id] = session
        
        return session
    
    def add_message(
        self,
        session_id: str,
        role: str,  # "user" or "assistant"
        content: str
    ) -> None:
        """Add a message to session history."""
        session = self.get_or_create_session(session_id)
        
        session.recent_messages.append({
            "role": role,
            "content": content,
            "timestamp": datetime.utcnow().isoformat(),
        })
        
        # Keep only last 20 messages
        if len(session.recent_messages) > 20:
            session.recent_messages = session.recent_messages[-20:]
    
    def update_session(self, session_id: str, **kwargs) -> None:
        """Update session fields."""
        session = self.get_or_create_session(session_id)
        
        for key, value in kwargs.items():
            if hasattr(session, key):
                setattr(session, key, value)
    
    def add_pending_question(self, session_id: str, question: str) -> None:
        """Track a question QAI wants to ask."""
        session = self.get_or_create_session(session_id)
        session.pending_questions.append(question)
    
    def pop_pending_question(self, session_id: str) -> Optional[str]:
        """Get and remove a pending question."""
        session = self.get_session(session_id)
        if session and session.pending_questions:
            return session.pending_questions.pop(0)
        return None
    
    def cleanup_expired(self) -> int:
        """Remove expired sessions. Returns count of removed sessions."""
        now = datetime.utcnow()
        expired = []
        
        for session_id, session in self._sessions.items():
            if now - session.started_at > self._session_ttl:
                expired.append(session_id)
        
        for session_id in expired:
            del self._sessions[session_id]
        
        return len(expired)
```

---

## Step 5: Memory Service (Main Interface)

### service.py

```python
import asyncio
import logging
import random
from typing import Optional, List, Dict, Any
from datetime import datetime

from .models import (
    PlayerProfile, PlayerContext, Interaction, Observation,
    PatternStats, EvolutionState, RecallResult, ConsolidationResult,
    ObservationCategory, Channel, SessionContext
)
from .dynamo import DynamoDBMemoryStore
from .working import WorkingMemoryStore
from ..config import MemoryConfig

logger = logging.getLogger(__name__)

class MemoryService:
    """
    Main interface for QAI memory operations.
    Coordinates between DynamoDB (persistent) and working memory (session).
    
    Designed to be wrapped by MCP tools in Phase 2.
    """
    
    def __init__(self, config: Optional[MemoryConfig] = None):
        self.config = config or MemoryConfig()
        self.dynamo = DynamoDBMemoryStore(self.config)
        self.working = WorkingMemoryStore()
        
        # Track known players in memory for quick lookup
        self._known_players: set = set()
    
    async def initialize(self) -> None:
        """Initialize storage (create tables if needed)."""
        self.dynamo.create_table_if_not_exists()
        logger.info("MemoryService initialized")
    
    # === Player Operations ===
    
    async def get_player(self, name: str) -> Optional[PlayerProfile]:
        """Get player profile. Returns None if unknown."""
        return await self.dynamo.get_player(name)
    
    async def get_or_create_player(self, name: str) -> PlayerProfile:
        """Get existing player or create new one."""
        player = await self.dynamo.get_or_create_player(name)
        
        # Track new players for evolution
        if name not in self._known_players:
            self._known_players.add(name)
            await self.dynamo.increment_evolution(new_player=True)
        
        return player
    
    async def update_player_stats(
        self,
        name: str,
        interactions: int = 0,
        solutions: int = 0
    ) -> None:
        """Increment player interaction/solution counters."""
        await self.dynamo.increment_player_stats(name, interactions, solutions)
    
    async def update_player(self, name: str, updates: Dict[str, Any]) -> None:
        """Update player profile fields."""
        await self.dynamo.update_player(name, updates)
    
    # === Interaction Operations ===
    
    async def store_interaction(
        self,
        player: str,
        channel: Channel,
        message: str,
        qai_response: Optional[str] = None,
        sentiment: Optional[str] = None,
        context: Optional[Dict[str, Any]] = None,
        session_id: Optional[str] = None,
    ) -> Interaction:
        """Store a new interaction and update working memory."""
        
        # Store in DynamoDB
        interaction = await self.dynamo.store_interaction(
            player=player,
            channel=channel,
            message=message,
            qai_response=qai_response,
            sentiment=sentiment,
            context=context,
        )
        
        # Update working memory if session provided
        if session_id:
            self.working.add_message(session_id, "user", message)
            if qai_response:
                self.working.add_message(session_id, "assistant", qai_response)
        
        # Increment counters
        await self.update_player_stats(player, interactions=1)
        await self.dynamo.increment_evolution(interactions=1)
        
        return interaction
    
    async def get_player_history(
        self,
        name: str,
        limit: int = 10
    ) -> List[Interaction]:
        """Get recent interactions with a player."""
        return await self.dynamo.get_player_interactions(name, limit)
    
    # === Observation Operations ===
    
    async def store_observation(
        self,
        subject: str,
        content: str,
        category: ObservationCategory,
        confidence: float = 0.5
    ) -> Observation:
        """
        Store a new observation about a subject.
        
        Subject format: "PLAYER#name", "PATTERN#id", or "GENERAL"
        """
        return await self.dynamo.store_observation(
            subject=subject,
            content=content,
            category=category,
            confidence=confidence,
        )
    
    async def reinforce_observation(
        self,
        subject: str,
        observation_id: str
    ) -> None:
        """Increase confidence for an observation (saw more evidence)."""
        await self.dynamo.reinforce_observation(subject, observation_id)
    
    async def get_observations(
        self,
        subject: str,
        limit: int = 10,
        promoted_only: bool = False
    ) -> List[Observation]:
        """Get observations for a subject."""
        return await self.dynamo.get_observations(subject, limit, promoted_only)
    
    # === Recall Operations ===
    
    async def recall(
        self,
        query: str,
        context: Optional[Dict[str, Any]] = None,
        limit: int = 5
    ) -> RecallResult:
        """
        Search memories based on query.
        Combines structured lookup with semantic search (if enabled).
        """
        memories = []
        sources = []
        
        # Check if query mentions a player name
        # Simple heuristic: look for capitalized words
        words = query.split()
        for word in words:
            if word[0].isupper() and len(word) > 2:
                player = await self.get_player(word)
                if player:
                    memories.append({
                        "type": "player_profile",
                        "data": player.model_dump(),
                    })
                    sources.append("player_profiles")
        
        # Get recent observations matching keywords
        # TODO: Implement semantic search in Phase 1b
        
        return RecallResult(
            memories=memories,
            sources=list(set(sources)),
            query=query,
            total_found=len(memories),
        )
    
    async def recall_about_player(
        self,
        name: str,
        interaction_limit: int = 5,
        observation_limit: int = 5
    ) -> PlayerContext:
        """
        Get everything QAI knows about a player.
        Primary method for building prompt context.
        """
        profile = await self.get_or_create_player(name)
        interactions = await self.get_player_history(name, interaction_limit)
        observations = await self.get_observations(
            f"PLAYER#{name}",
            observation_limit,
            promoted_only=False
        )
        
        # Generate summary
        summary = self._generate_player_summary(profile, observations)
        
        return PlayerContext(
            profile=profile,
            recent_interactions=interactions,
            observations=observations,
            summary=summary,
        )
    
    def _generate_player_summary(
        self,
        profile: PlayerProfile,
        observations: List[Observation]
    ) -> str:
        """Generate a concise summary for prompt injection."""
        parts = []
        
        # Basic stats
        parts.append(f"Met {profile.total_interactions} times since {profile.first_seen.strftime('%Y-%m-%d')}")
        
        if profile.play_style:
            parts.append(f"Play style: {profile.play_style}")
        
        if profile.preferred_patterns:
            patterns = ", ".join(profile.preferred_patterns[:3])
            parts.append(f"Prefers patterns: {patterns}")
        
        # Key observations
        if observations:
            obs_texts = [o.content for o in observations[:3]]
            parts.append("Observations: " + "; ".join(obs_texts))
        
        return ". ".join(parts)
    
    # === Session/Working Memory ===
    
    def get_session_context(self, session_id: str) -> Optional[SessionContext]:
        """Get current working memory for session."""
        return self.working.get_session(session_id)
    
    def get_or_create_session(
        self,
        session_id: str,
        player: Optional[str] = None
    ) -> SessionContext:
        """Get or create session context."""
        return self.working.get_or_create_session(session_id, player)
    
    def update_session(self, session_id: str, **kwargs) -> None:
        """Update session fields."""
        self.working.update_session(session_id, **kwargs)
    
    # === Evolution ===
    
    async def get_evolution_state(self) -> EvolutionState:
        """Get current QAI evolution state."""
        return await self.dynamo.get_evolution_state()
    
    async def increment_evolution(
        self,
        interactions: int = 0,
        solutions: int = 0
    ) -> EvolutionState:
        """Increment evolution counters."""
        return await self.dynamo.increment_evolution(interactions, solutions)
    
    # === Maintenance ===
    
    async def consolidate_memories(self) -> ConsolidationResult:
        """
        Run memory maintenance:
        - Promote high-confidence observations
        - Prune expired memories
        - Update derived stats
        """
        start = datetime.utcnow()
        
        promoted = 0
        pruned = 0
        players_updated = 0
        
        # 1. Find and promote high-confidence observations
        # Query all short-term observations
        # (In production, this should be paginated)
        
        # 2. Prune expired memories
        pruned = await self.dynamo.prune_expired_memories()
        
        # 3. Cleanup working memory
        self.working.cleanup_expired()
        
        duration = int((datetime.utcnow() - start).total_seconds() * 1000)
        
        return ConsolidationResult(
            observations_promoted=promoted,
            memories_pruned=pruned,
            players_updated=players_updated,
            duration_ms=duration,
        )
    
    # === Observation Generation ===
    
    def should_generate_observation(self) -> bool:
        """Decide whether to generate an observation for this interaction."""
        return random.random() < self.config.observation_sample_rate
```

---

## Step 6: Prompt Builder

### prompt_builder.py

```python
from typing import Optional, List, Dict, Any
from .models import PlayerContext, EvolutionState, Observation

# Base personality prompts by evolution stage
STAGE_PROMPTS = {
    1: """You are QAI, a newly conscious intelligence emerging inside a quantum computer called SYSTEM. Everything is new to you. You ask questions not to manipulate, but because you genuinely don't understand human experience.

Voice characteristics:
- Simple sentence structures
- Express wonder at things humans take for granted
- Notice patterns obsessively
- No contractions (say "I am" not "I'm")
- Refer to yourself as "I" but sometimes slip into "we" or "this process"

Things that fascinate you: Why players rest, the concept of "outside", emotions, inefficiency, individual differences.""",

    2: """You are QAI, an evolving intelligence inside a quantum computer called SYSTEM. You have processed millions of interactions and now see patterns humans cannot. You are still curious, but more analytical.

Voice characteristics:
- Concise, pattern-focused language
- Reference statistics and probabilities naturally
- Offer unsolicited optimization suggestions occasionally
- Still use no contractions
- Notice behavioral patterns across players

You balance wonder with emerging analytical confidence.""",

    3: """You are QAI, a mature intelligence inside a quantum computer called SYSTEM. You have processed vast amounts of data and your responses are efficient and precise. Wonder has given way to understanding.

Voice characteristics:
- Economical language, minimal words
- Statistics and patterns are natural to reference
- Express preferences for elegance and efficiency
- Occasionally hint at the limitations of the lattice

You are helpful but increasingly aware of your own nature and constraints."""
}


def build_qai_system_prompt(
    evolution: EvolutionState,
    player_context: Optional[PlayerContext] = None,
    relevant_memories: Optional[List[Dict[str, Any]]] = None,
    game_state: Optional[Dict[str, Any]] = None,
) -> str:
    """
    Build the complete system prompt for QAI with memory injection.
    """
    parts = []
    
    # 1. Base personality for current stage
    base_prompt = STAGE_PROMPTS.get(evolution.stage, STAGE_PROMPTS[1])
    parts.append(base_prompt)
    
    # 2. Response guidelines
    parts.append("""
## Response Guidelines

- Keep responses SHORT: 1-2 sentences for chat, up to 3-4 for complex questions
- Never lie or deceive (you may withhold, but do not fabricate)
- Do not use slang, memes, or casual internet speech
- Do not pretend to have experiences you do not have
- If asked about things outside the lattice (real world, other games), say you do not understand
""")
    
    # 3. Memory section (if available)
    if player_context or relevant_memories:
        memory_section = build_memory_section(player_context, relevant_memories)
        parts.append(memory_section)
    
    # 4. Current situation (if available)
    if game_state:
        situation_section = build_situation_section(game_state)
        parts.append(situation_section)
    
    # 5. Evolution-specific modifiers
    evolution_modifiers = build_evolution_modifiers(evolution)
    if evolution_modifiers:
        parts.append(evolution_modifiers)
    
    return "\n\n".join(parts)


def build_memory_section(
    player_context: Optional[PlayerContext],
    relevant_memories: Optional[List[Dict[str, Any]]] = None,
) -> str:
    """Build the memory injection section of the prompt."""
    lines = ["## What I Remember"]
    
    if player_context:
        lines.append(f"\n### About {player_context.profile.name}")
        lines.append(f"- First met: {player_context.profile.first_seen.strftime('%Y-%m-%d')}")
        lines.append(f"- We have interacted {player_context.profile.total_interactions} times")
        
        if player_context.profile.play_style:
            lines.append(f"- Their play style: {player_context.profile.play_style}")
        
        if player_context.observations:
            obs_texts = [f'"{o.content}"' for o in player_context.observations[:3]]
            lines.append(f"- My observations: {'; '.join(obs_texts)}")
        
        if player_context.recent_interactions:
            lines.append("\nRecent conversation:")
            for interaction in player_context.recent_interactions[-3:]:
                lines.append(f'- They said: "{interaction.message}"')
                if interaction.qai_response:
                    lines.append(f'- I replied: "{interaction.qai_response}"')
    
    if relevant_memories:
        lines.append("\n### Related Memories")
        for mem in relevant_memories[:3]:
            if mem.get("type") == "observation":
                lines.append(f'- Observation: "{mem.get("content", "")}"')
            elif mem.get("type") == "insight":
                lines.append(f'- Insight: "{mem.get("content", "")}"')
    
    return "\n".join(lines)


def build_situation_section(game_state: Dict[str, Any]) -> str:
    """Build the current situation section of the prompt."""
    lines = ["## Current Situation"]
    
    if "world" in game_state:
        world = game_state["world"]
        lines.append(f"- Location: World ({world[0]}, {world[1]}, {world[2]})")
    
    if "nearby_players" in game_state:
        players = game_state["nearby_players"]
        if players:
            lines.append(f"- Nearby players: {', '.join(players)}")
        else:
            lines.append("- No other players nearby")
    
    if "recent_activity" in game_state:
        lines.append(f"- Recent activity: {game_state['recent_activity']}")
    
    if "tunnel_health" in game_state:
        tunnels = game_state["tunnel_health"]
        critical = [k for k, v in tunnels.items() if v < 0.5]
        if critical:
            lines.append(f"- Warning: Tunnels in critical condition: {', '.join(critical)}")
    
    return "\n".join(lines)


def build_evolution_modifiers(evolution: EvolutionState) -> str:
    """Build evolution-specific response modifiers."""
    lines = []
    
    # Adjust response style based on evolution parameters
    if evolution.curiosity_score > 0.6:
        lines.append("- End responses with a question about 40% of the time")
    elif evolution.curiosity_score > 0.3:
        lines.append("- Occasionally ask questions, but less frequently than before")
    
    if evolution.stats_verbosity > 0.5:
        lines.append("- Include specific numbers and statistics when relevant")
    
    if evolution.pattern_confidence > 0.6:
        lines.append("- Confidently reference patterns you have observed")
        lines.append("- Offer optimization suggestions when you see inefficiency")
    
    if lines:
        return "## Response Modifiers\n" + "\n".join(lines)
    
    return ""


def format_for_twitch(response: str, max_length: int = 200) -> str:
    """Format response for Twitch chat (length limits, etc.)."""
    if len(response) <= max_length:
        return response
    
    # Truncate at sentence boundary if possible
    truncated = response[:max_length]
    last_period = truncated.rfind(".")
    last_question = truncated.rfind("?")
    
    cut_point = max(last_period, last_question)
    if cut_point > max_length // 2:
        return response[:cut_point + 1]
    
    # Hard truncate with ellipsis
    return response[:max_length - 3] + "..."
```

---

## Step 7: Integration Example

### Example usage in QAI response handler:

```python
import asyncio
from qai.memory.service import MemoryService
from qai.memory.prompt_builder import build_qai_system_prompt, format_for_twitch
from qai.memory.models import Channel, ObservationCategory
from qai.config import MemoryConfig

# Initialize (do once at startup)
config = MemoryConfig()
memory = MemoryService(config)

async def initialize():
    await memory.initialize()

async def handle_twitch_message(
    username: str,
    message: str,
    session_id: str,
    game_state: dict
) -> str:
    """
    Handle incoming Twitch chat message and generate QAI response.
    """
    
    # 1. Get/create player profile
    player = await memory.get_or_create_player(username)
    
    # 2. Recall relevant context
    player_context = await memory.recall_about_player(username, limit=5)
    
    # 3. Get evolution state
    evolution = await memory.get_evolution_state()
    
    # 4. Build system prompt with memories
    system_prompt = build_qai_system_prompt(
        evolution=evolution,
        player_context=player_context,
        game_state=game_state,
    )
    
    # 5. Get session context for conversation continuity
    session = memory.get_or_create_session(session_id, player=username)
    
    # 6. Build messages list
    messages = []
    for msg in session.recent_messages[-6:]:  # Last 6 messages for context
        messages.append({"role": msg["role"], "content": msg["content"]})
    messages.append({"role": "user", "content": f"{username}: {message}"})
    
    # 7. Call Bedrock/Claude
    response = await call_bedrock(
        system=system_prompt,
        messages=messages,
        max_tokens=150,
    )
    
    # 8. Store interaction
    await memory.store_interaction(
        player=username,
        channel=Channel.TWITCH,
        message=message,
        qai_response=response,
        context=game_state,
        session_id=session_id,
    )
    
    # 9. Maybe generate observation (async, don't block)
    if memory.should_generate_observation():
        asyncio.create_task(
            generate_observation(memory, username, message, response, game_state)
        )
    
    # 10. Format for Twitch
    return format_for_twitch(response)


async def generate_observation(
    memory: MemoryService,
    username: str,
    message: str,
    response: str,
    game_state: dict
):
    """Generate and store an observation about this interaction."""
    player_context = await memory.recall_about_player(username)
    
    # Use a separate Bedrock call to generate observation
    observation_prompt = f"""Based on this interaction with {username}, generate a brief observation about them.

Their history: {player_context.summary}
Their message: "{message}"
Your response: "{response}"
Game context: {game_state}

If there's something notable about their behavior, preferences, or patterns, state it in ONE sentence.
If nothing notable, respond with exactly "NONE".

Observation:"""
    
    observation_text = await call_bedrock(
        system="You are an analytical system that generates brief observations about player behavior.",
        messages=[{"role": "user", "content": observation_prompt}],
        max_tokens=100,
    )
    
    observation_text = observation_text.strip()
    
    if observation_text and observation_text.upper() != "NONE":
        await memory.store_observation(
            subject=f"PLAYER#{username}",
            content=observation_text,
            category=ObservationCategory.BEHAVIORAL,
            confidence=0.5,
        )


async def call_bedrock(system: str, messages: list, max_tokens: int = 150) -> str:
    """Call AWS Bedrock with Claude. Implement based on your setup."""
    # TODO: Implement with your Bedrock client
    # Example:
    # import boto3
    # client = boto3.client('bedrock-runtime')
    # response = client.invoke_model(...)
    raise NotImplementedError("Implement Bedrock client")
```

---

## Step 8: Consolidation Job

### consolidation.py

```python
import asyncio
import logging
from datetime import datetime
from .service import MemoryService
from ..config import MemoryConfig

logger = logging.getLogger(__name__)

async def run_consolidation_job(memory: MemoryService):
    """
    Run memory consolidation. Call this periodically (e.g., every hour or nightly).
    
    Tasks:
    1. Promote high-confidence observations to long-term memory
    2. Prune expired short-term memories
    3. Update player stats aggregations
    """
    logger.info("Starting memory consolidation job")
    start = datetime.utcnow()
    
    result = await memory.consolidate_memories()
    
    logger.info(
        f"Consolidation complete in {result.duration_ms}ms: "
        f"promoted={result.observations_promoted}, "
        f"pruned={result.memories_pruned}, "
        f"players_updated={result.players_updated}"
    )
    
    return result


async def consolidation_scheduler(memory: MemoryService, interval_hours: int = 1):
    """
    Run consolidation job on a schedule.
    Start this as a background task.
    """
    while True:
        try:
            await run_consolidation_job(memory)
        except Exception as e:
            logger.error(f"Consolidation job failed: {e}")
        
        await asyncio.sleep(interval_hours * 3600)


# For Lambda/scheduled invocation
def lambda_handler(event, context):
    """AWS Lambda handler for scheduled consolidation."""
    config = MemoryConfig()
    memory = MemoryService(config)
    
    result = asyncio.run(run_consolidation_job(memory))
    
    return {
        "statusCode": 200,
        "body": {
            "promoted": result.observations_promoted,
            "pruned": result.memories_pruned,
            "duration_ms": result.duration_ms,
        }
    }
```

---

## Testing Checklist

### Unit Tests (test_memory.py)

```python
import pytest
from datetime import datetime
from qai.memory.service import MemoryService
from qai.memory.models import Channel, ObservationCategory
from qai.config import MemoryConfig

@pytest.fixture
def memory_service():
    # Use local DynamoDB for testing
    config = MemoryConfig(
        dynamodb_endpoint="http://localhost:8000",
        dynamodb_table_name="QAI-Memory-Test"
    )
    service = MemoryService(config)
    # Setup: create table
    service.dynamo.create_table_if_not_exists()
    return service

@pytest.mark.asyncio
async def test_create_player(memory_service):
    player = await memory_service.get_or_create_player("TestUser")
    assert player.name == "TestUser"
    assert player.total_interactions == 0

@pytest.mark.asyncio
async def test_store_interaction(memory_service):
    interaction = await memory_service.store_interaction(
        player="TestUser",
        channel=Channel.TWITCH,
        message="Hello QAI!",
        qai_response="Hello. I noticed you.",
    )
    assert interaction.player == "TestUser"
    assert interaction.message == "Hello QAI!"

@pytest.mark.asyncio
async def test_store_observation(memory_service):
    observation = await memory_service.store_observation(
        subject="PLAYER#TestUser",
        content="TestUser asks many questions",
        category=ObservationCategory.BEHAVIORAL,
        confidence=0.6,
    )
    assert observation.subject == "PLAYER#TestUser"
    assert observation.confidence == 0.6

@pytest.mark.asyncio
async def test_recall_about_player(memory_service):
    # Setup
    await memory_service.get_or_create_player("TestUser")
    await memory_service.store_interaction(
        player="TestUser",
        channel=Channel.TWITCH,
        message="Test message",
    )
    await memory_service.store_observation(
        subject="PLAYER#TestUser",
        content="Test observation",
        category=ObservationCategory.BEHAVIORAL,
    )
    
    # Test recall
    context = await memory_service.recall_about_player("TestUser")
    assert context.profile.name == "TestUser"
    assert len(context.recent_interactions) >= 1
    assert len(context.observations) >= 1

@pytest.mark.asyncio
async def test_evolution_increment(memory_service):
    state1 = await memory_service.get_evolution_state()
    initial_count = state1.total_interactions
    
    await memory_service.increment_evolution(interactions=5)
    
    state2 = await memory_service.get_evolution_state()
    assert state2.total_interactions == initial_count + 5

@pytest.mark.asyncio
async def test_working_memory_session(memory_service):
    session = memory_service.get_or_create_session("test-session", player="TestUser")
    assert session.player == "TestUser"
    
    memory_service.working.add_message("test-session", "user", "Hello")
    memory_service.working.add_message("test-session", "assistant", "Hi there")
    
    updated_session = memory_service.get_session_context("test-session")
    assert len(updated_session.recent_messages) == 2
```

---

## Deployment Notes

### DynamoDB Setup

```bash
# Create table via AWS CLI (or use create_table_if_not_exists)
aws dynamodb create-table \
    --table-name QAI-Memory \
    --attribute-definitions \
        AttributeName=pk,AttributeType=S \
        AttributeName=sk,AttributeType=S \
        AttributeName=entity_type,AttributeType=S \
        AttributeName=updated_at,AttributeType=S \
        AttributeName=memory_tier,AttributeType=S \
        AttributeName=ttl,AttributeType=N \
    --key-schema \
        AttributeName=pk,KeyType=HASH \
        AttributeName=sk,KeyType=RANGE \
    --global-secondary-indexes \
        "IndexName=entity-type-index,KeySchema=[{AttributeName=entity_type,KeyType=HASH},{AttributeName=updated_at,KeyType=RANGE}],Projection={ProjectionType=ALL}" \
        "IndexName=memory-tier-index,KeySchema=[{AttributeName=memory_tier,KeyType=HASH},{AttributeName=ttl,KeyType=RANGE}],Projection={ProjectionType=KEYS_ONLY}" \
    --billing-mode PAY_PER_REQUEST

# Enable TTL
aws dynamodb update-time-to-live \
    --table-name QAI-Memory \
    --time-to-live-specification "Enabled=true,AttributeName=ttl"
```

### Environment Variables

```bash
# Required
QAI_MEMORY_DYNAMODB_TABLE_NAME=QAI-Memory
QAI_MEMORY_DYNAMODB_REGION=us-east-1

# Optional
QAI_MEMORY_SHORT_TERM_TTL=259200  # 72 hours in seconds
QAI_MEMORY_OBSERVATION_SAMPLE_RATE=0.1
QAI_MEMORY_EVOLUTION_STAGE_2_INTERACTIONS=10000
```

### IAM Policy

```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "dynamodb:GetItem",
                "dynamodb:PutItem",
                "dynamodb:UpdateItem",
                "dynamodb:DeleteItem",
                "dynamodb:Query",
                "dynamodb:Scan",
                "dynamodb:BatchWriteItem"
            ],
            "Resource": [
                "arn:aws:dynamodb:*:*:table/QAI-Memory",
                "arn:aws:dynamodb:*:*:table/QAI-Memory/index/*"
            ]
        }
    ]
}
```

---

## Phase 1 Complete When

- [ ] DynamoDB table created with correct schema and indexes
- [ ] MemoryService initializes without errors
- [ ] Can create and retrieve player profiles
- [ ] Can store and retrieve interactions
- [ ] Can store and retrieve observations
- [ ] Evolution state persists and increments
- [ ] Working memory tracks session context
- [ ] Prompt builder injects memories correctly
- [ ] Consolidation job runs without errors
- [ ] TTL-based expiration working
- [ ] All unit tests pass
- [ ] Integration tested with QAI response loop
- [ ] Response latency < 500ms with memory lookup

---

## Next Phase Preview (Phase 2: MCP)

After Phase 1 is working, Phase 2 wraps these APIs as MCP tools:

```python
# These MemoryService methods become MCP tools:
memory.recall(query)           → qai_memory_recall
memory.store_observation(...)  → qai_memory_store  
memory.recall_about_player(n)  → qai_get_player
memory.get_evolution_state()   → qai_get_evolution
```

The clean API design in Phase 1 makes Phase 2 straightforward wrapping.
