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
- Azure Functions Core Tools v4 (`npm install -g azure-functions-core-tools@4`)
- Azure OpenAI resource with GPT-4o deployment
- (Optional) Redis Cache for multi-instance deployments

### Step-by-Step Setup

**1. Clone and Navigate**
```bash
git clone <repository-url>
cd SKFProductAssistant
```

**2. Configure Environment Variables**

Edit the `local.settings.json` file in `src/Skf.ProductAssistant.Function/` with your Azure OpenAI credentials:
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AzureOpenAI__Endpoint": "https://your-resource.openai.azure.com/",
    "AzureOpenAI__ApiKey": "your-api-key",
    "AzureOpenAI__DeploymentName": "gpt-4o"
  }
}
```

Or set environment variables directly:
```bash
# Required
export AzureOpenAI__Endpoint=https://your-resource.openai.azure.com/
export AzureOpenAI__ApiKey=your-api-key
export AzureOpenAI__DeploymentName=gpt-4o

# Optional (Redis)
export FeatureFlags__UseRedis=true
export Redis__ConnectionString=your-redis-connection-string
```

**3. Build the Solution**
```bash
dotnet restore
dotnet build
```

**4. Run the Function App**
```bash
cd src/Skf.ProductAssistant.Function
func start
```

The API will be available at `http://localhost:7071/api/chat`

**5. Verify Health**
```bash
curl http://localhost:7071/api/health
# Expected: "Healthy"
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

## Question Flow

When a user asks a question, the system processes it through the following steps:

```
┌────────────────────────────────────────────────────────────────────────────────┐
│                              QUESTION FLOW                                      │
└────────────────────────────────────────────────────────────────────────────────┘

User: "What is the bore diameter of 6205-2RS?"
                    │
                    ▼
┌─────────────────────────────────────────┐
│  1. HTTP Request → ProductAssistantFunction  │
│     POST /api/chat                       │
│     Body: { "message": "...", "conversationId": "..." }
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│  2. OrchestratorService.ProcessAsync()   │
│     • Get/create conversation context    │
│     • Classify intent (Question/Feedback/etc.)
│     • Extract product designation        │
│     • Route to appropriate agent         │
└─────────────────────────────────────────┘
                    │
         Intent = Question
                    │
                    ▼
┌─────────────────────────────────────────┐
│  3. IntentClassifierService              │
│     • Uses LLM to classify message       │
│     • Returns: Question, Feedback,       │
│       Conversational, Help, or Unknown   │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│  4. ProductNormalizationService          │
│     • Extracts "6205-2RS" from message   │
│     • Normalizes designation             │
│     • Resolves against conversation context
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│  5. QnaAgent.ProcessAsync()              │
│     • Builds chat history with prompts   │
│     • Invokes Semantic Kernel with       │
│       function calling enabled           │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│  6. Semantic Kernel + Azure OpenAI       │
│     • LLM decides to call DatasheetPlugin│
│     • Function: GetProductAttribute()    │
│     • Parameters: "6205-2RS", "bore_diameter"
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│  7. DatasheetPlugin.GetProductAttribute()│
│     • Queries JsonDatasheetRepository    │
│     • Returns: { "value": "25", "unit": "mm" }
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│  8. LLM Generates Natural Language       │
│     • Formats data into response         │
│     • "The bore diameter of 6205-2RS is 25 mm."
└─────────────────────────────────────────┘
```

### Detailed Step Breakdown

| Step | Component | Action | Source File |
|------|-----------|--------|-------------|
| 1 | ProductAssistantFunction | Receives HTTP POST, parses JSON body | `Function/ProductAssistantFunction.cs:36` |
| 2 | OrchestratorService | Coordinates the entire flow | `Application/Orchestration/OrchestratorService.cs:50` |
| 3 | IntentClassifierService | Classifies user intent using LLM | `Application/Services/IntentClassifierService.cs` |
| 4 | ProductNormalizationService | Extracts and normalizes product codes | `Application/Services/ProductNormalizationService.cs` |
| 5 | QnaAgent | Handles Q&A using Semantic Kernel | `Application/Agents/QnaAgent.cs:51` |
| 6 | Semantic Kernel | Orchestrates LLM and function calling | `Infrastructure/SemanticKernel/KernelFactory.cs` |
| 7 | DatasheetPlugin | Retrieves data from JSON datasheets | `Application/Plugins/DatasheetPlugin.cs` |
| 8 | LLM | Generates natural language response | Azure OpenAI GPT-4o |

## Answer Flow

The answer is generated and validated through these steps:

```
┌────────────────────────────────────────────────────────────────────────────────┐
│                              ANSWER FLOW                                        │
└────────────────────────────────────────────────────────────────────────────────┘

                    │
                    │ (from Question Flow step 8)
                    ▼
┌─────────────────────────────────────────┐
│  1. QnaAgent receives LLM response       │
│     Answer: "The bore diameter is 25 mm" │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│  2. HallucinationGuard.ValidateResponseAsync()
│     • Checks if answer contains data     │
│       that exists in datasheet           │
│     • Validates numeric values match     │
│     • Returns: { IsValid: true/false }   │
└─────────────────────────────────────────┘
                    │
           ┌───────┴───────┐
           │               │
     Valid │               │ Invalid
           ▼               ▼
┌──────────────────┐ ┌──────────────────┐
│ Return answer    │ │ Return abstention │
│ as-is            │ │ "I don't have..." │
└──────────────────┘ └──────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│  3. OrchestratorService                  │
│     • Updates conversation state         │
│     • Stores current product context     │
│     • Increments turn count              │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│  4. Build ChatResponse                   │
│     {                                    │
│       "answer": "The bore diameter...",  │
│       "conversationId": "conv_123",      │
│       "intent": "Question",              │
│       "productDesignation": "6205-2RS",  │
│       "isFromDatasheet": true,           │
│       "metadata": {                      │
│         "processingTimeMs": 450,         │
│         "agentUsed": "QnaAgent",         │
│         "turnNumber": 1                  │
│       }                                  │
│     }                                    │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│  5. HTTP Response                        │
│     200 OK with JSON body                │
└─────────────────────────────────────────┘
```

### Answer Validation Rules

The `HallucinationGuard` ensures responses are grounded in actual data:

| Check | Description | Action if Failed |
|-------|-------------|------------------|
| Data Existence | Product must exist in datasheet | Return "product not found" |
| Attribute Existence | Requested attribute must exist | Return "attribute not available" |
| Value Match | Numeric values must match source | Return abstention response |
| No Fabrication | No invented specifications | Block and log warning |

### Response Types

| Response Type | Trigger | Example |
|---------------|---------|---------|
| **Success** | Data found and validated | "The bore diameter of 6205-2RS is 25 mm." |
| **Not Found** | Product doesn't exist | "I couldn't find product 9999 in my database." |
| **Attribute N/A** | Attribute not in datasheet | "I don't have weight information for this product." |
| **Abstention** | Hallucination detected | "I cannot provide that information from my data sources." |

## AI Prompts Configuration

All AI prompts are externalized in `appsettings.json` for easy tuning without code changes:

```json
{
  "Prompts": {
    "Orchestrator": {
      "SystemPrompt": "You are the SKF Product Assistant orchestrator. Route user requests to the appropriate agent based on intent."
    },
    "QnaAgent": {
      "SystemPrompt": "You are the SKF Product Assistant. Answer questions about SKF bearings ONLY using data from the datasheet functions. If data is not found, say so clearly. Never guess or make up specifications.",
      "UserTemplate": "User question: {question}\nProduct context: {product}\nConversation context: {context}",
      "NotFoundResponse": "I couldn't find that product in my database. Please verify the product designation.",
      "AttributeNotAvailableResponse": "I don't have information about that attribute for this product."
    },
    "FeedbackAgent": {
      "SystemPrompt": "You are the SKF Product Assistant feedback handler. Capture user corrections and feedback about product information politely and professionally.",
      "AcknowledgmentTemplate": "Thank you for your feedback about {product}. I've recorded your correction regarding {attribute}. Our team will review this."
    },
    "IntentClassifier": {
      "SystemPrompt": "Classify user messages into: Question (asking about products), Feedback (corrections/complaints), Conversational (greetings/thanks), Help (asking what you can do), Unknown.",
      "ClassificationTemplate": "Classify this message: {message}\nRespond with only one word: Question, Feedback, Conversational, Help, or Unknown."
    }
  }
}
```

### Prompt Placeholders

| Placeholder | Used In | Description |
|-------------|---------|-------------|
| `{question}` | QnaAgent.UserTemplate | The user's original message |
| `{product}` | QnaAgent.UserTemplate, FeedbackAgent | Current product designation |
| `{context}` | QnaAgent.UserTemplate | Conversation history summary |
| `{attribute}` | FeedbackAgent.AcknowledgmentTemplate | The attribute being corrected |
| `{message}` | IntentClassifier.ClassificationTemplate | Message to classify |

### Customizing Prompts

To modify AI behavior, edit the prompts in `appsettings.json`:

1. **Make responses more formal**: Update `QnaAgent.SystemPrompt`
2. **Change error messages**: Update `NotFoundResponse` or `AttributeNotAvailableResponse`
3. **Improve intent detection**: Refine `IntentClassifier.SystemPrompt`

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
