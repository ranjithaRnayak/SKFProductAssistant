# SKF Product Assistant

An AI-powered product information assistant built with Microsoft Semantic Kernel and Azure OpenAI. The system uses an agentic architecture with function calling to answer questions about SKF bearings from local JSON datasheets.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Azure Function (HTTP)                        │
│                    POST /api/chat                                │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    OrchestratorService                           │
│  • Intent Classification (Question/Feedback/Conversational)      │
│  • Agent Routing                                                 │
│  • Conversation State Management                                 │
└─────────────────────────────────────────────────────────────────┘
                    │                       │
                    ▼                       ▼
        ┌───────────────────┐    ┌───────────────────┐
        │     QnaAgent      │    │   FeedbackAgent   │
        │  (SK + Function   │    │  (Stores user     │
        │   Calling)        │    │   corrections)    │
        └───────────────────┘    └───────────────────┘
                    │
                    ▼
        ┌───────────────────┐
        │  DatasheetPlugin  │ ← Function calling (NO hallucination)
        │  CachePlugin      │
        │  StatePlugin      │
        └───────────────────┘
                    │
                    ▼
        ┌───────────────────┐
        │  JSON Datasheets  │  ← Single source of truth
        │  (bearings.json)  │
        └───────────────────┘
```

## Quick Start

### Prerequisites
- .NET 8.0 SDK
- Azure OpenAI resource with GPT-4o deployment
- (Optional) Redis Cache for multi-instance deployments

### Environment Variables

```bash
# Required
AzureOpenAI__Endpoint=https://your-resource.openai.azure.com/
AzureOpenAI__ApiKey=your-api-key
AzureOpenAI__DeploymentName=gpt-4o

# Optional (Redis)
FeatureFlags__UseRedis=true
Redis__ConnectionString=your-redis-connection-string
```

### Running Locally

```bash
cd src/Skf.ProductAssistant.Function
func start
```

### Testing the API

```bash
# Basic question
curl -X POST http://localhost:7071/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "What is the width of 6205?"}'

# Follow-up (uses conversation context)
curl -X POST http://localhost:7071/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "And what about its diameter?", "conversationId": "conv_123"}'

# Feedback
curl -X POST http://localhost:7071/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "That width is wrong - it should be 15mm", "conversationId": "conv_123"}'
```

## Key Features

### Hallucination Prevention
- Agents use **function calling only** to access data
- All responses grounded in JSON datasheet content
- `HallucinationGuard` validates responses before returning
- Explicit abstention when data not found ("I don't have that information")

### Conversation State
- Multi-turn conversations with context preservation
- Pronoun resolution ("What's its weight?" → refers to current product)
- Follow-up questions without repeating product designation
- State stored in Redis (production) or in-memory (development)

### Caching
- Optional Redis cache for repeated queries
- In-memory cache for single-instance deployments
- Configurable via `FeatureFlags.UseRedis`

### Feedback Capture
- Users can report incorrect information
- Feedback linked to conversation context
- Stored for review and datasheet updates

## Project Structure

```
src/
├── Skf.ProductAssistant.Domain/          # Pure business entities
├── Skf.ProductAssistant.Application/     # Business logic, agents, plugins
├── Skf.ProductAssistant.Infrastructure/  # Repositories, configuration
└── Skf.ProductAssistant.Function/        # Azure Function endpoint
```

## Configuration

All configuration via `appsettings.json` or environment variables:

| Setting | Description | Default |
|---------|-------------|---------|
| `FeatureFlags:UseRedis` | Use Redis for cache/state | `false` |
| `FeatureFlags:StrictHallucinationPrevention` | Validate all responses | `true` |
| `FeatureFlags:EnableConversationState` | Track conversation context | `true` |
| `FeatureFlags:EnableFeedbackCapture` | Store user feedback | `true` |
| `Datasheet:FilePaths` | JSON datasheet file paths | `["bearings_type1.json", "bearings_type2.json"]` |

## Example Interactions

| User Input | Response |
|------------|----------|
| "What is the width of 6205?" | "The width of the 6205 bearing is 15 mm." |
| "And what about its diameter?" | "The bore diameter of the 6205 bearing is 25 mm." |
| "Diameter for 9999?" | "I couldn't find product 9999 in my database." |
| "That width is wrong" | "Thanks for your feedback. I've recorded your correction." |

## Design Decisions

1. **Clean Architecture**: Domain → Application → Infrastructure → Function layers
2. **SOLID Principles**: Single responsibility, dependency injection throughout
3. **IOptions Pattern**: All configuration externalized, no hardcoding
4. **Function Calling**: Agents must use plugins to access data (prevents hallucination)
5. **Repository Pattern**: Swappable implementations (InMemory, Redis, JSON)
