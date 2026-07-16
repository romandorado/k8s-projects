# InvestigationTeam Frontend - Design Spec

**Date:** 2026-07-16
**Status:** Draft

## Overview

Web frontend for the InvestigationTeam API that provides:
- User authentication (JWT) with per-user Gemini API keys
- CRUD dashboard for agents and teams
- AI-powered chat with agents (individual and team modes)
- Chat history stored in PostgreSQL

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     KUBERNETES CLUSTER                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   NAMESPACE: investigation-team (EXISTING)                       │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │  InvestigationTeam API (CRUD agentes/equipos) :32444     │  │
│   │  PostgreSQL (datos agentes)                              │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│   NAMESPACE: investigation-team-frontend (NUEVO)                 │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │  Angular Frontend (Nginx)          → Service: 30081      │  │
│   └──────────────────────────────────────────────────────────┘  │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │  .NET Chat Backend                → Service: 32445      │  │
│   └──────────────────────────────────────────────────────────┘  │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │  PostgreSQL (chat DB)             → Service: 5432        │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Components

### 1. Angular Frontend

**Technology:** Angular 22 + Nginx Alpine
**Port:** 30081 (LoadBalancer)

**Features:**

#### Login/Register
- Login form: email + password
- Register form: email + password + Gemini API key
- JWT token stored in localStorage
- Redirect to dashboard after login

#### Dashboard
- Sidebar: navigation (Agents, Teams, Chat, Profile)
- Agents list: table with name, role, status, skills, actions (edit/delete)
- Create/Edit agent modal: name, role dropdown, description, skills (comma-separated)
- Teams list: table with name, description, members count, actions
- Create/Edit team modal: name, description, member selection (checkboxes)

#### Chat
- Left panel: list of conversations (agent or team)
- Right panel: message thread
- Input: text area + send button
- Message types: user (blue), agent response (gray), system (yellow)
- Agent avatar based on role (emoji: 🔍 Researcher, 📊 Analyst, ✍️ Writer, 🎯 Coordinator, ✅ Reviewer)
- Team chat: shows which agent is responding

#### Profile
- View/edit email
- View/update Gemini API key (masked)
- Change password

**Components:**
- `app.component` — layout with router-outlet
- `auth/login-component` — login form
- `auth/register-component` — register form
- `dashboard/dashboard-component` — main layout
- `dashboard/agents-list-component` — agents table
- `dashboard/teams-list-component` — teams table
- `chat/chat-component` — chat interface
- `chat/conversation-list-component` — sidebar conversations
- `chat/message-thread-component` — message display
- `profile/profile-component` — user profile

**Services:**
- `auth.service` — login, register, logout, token management
- `agents.service` — CRUD agents (via chat backend proxy)
- `teams.service` — CRUD teams (via chat backend proxy)
- `chat.service` — send messages, get history
- `user.service` — profile management

### 2. .NET Chat Backend

**Technology:** .NET 10 + Google Gemini SDK
**Port:** 32445 (LoadBalancer)

**Endpoints:**

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/auth/register` | Register new user |
| POST | `/api/auth/login` | Login, return JWT |
| GET | `/api/auth/me` | Get current user |
| PUT | `/api/auth/me` | Update profile |
| PUT | `/api/auth/me/password` | Change password |
| GET | `/api/agents` | Proxy to InvestigationTeam API |
| POST | `/api/agents` | Proxy to InvestigationTeam API |
| PUT | `/api/agents/{id}` | Proxy to InvestigationTeam API |
| DELETE | `/api/agents/{id}` | Proxy to InvestigationTeam API |
| GET | `/api/teams` | Proxy to InvestigationTeam API |
| POST | `/api/teams` | Proxy to InvestigationTeam API |
| PUT | `/api/teams/{id}` | Proxy to InvestigationTeam API |
| DELETE | `/api/teams/{id}` | Proxy to InvestigationTeam API |
| POST | `/api/teams/{id}/agents/{agentId}` | Add agent to team |
| DELETE | `/api/teams/{id}/agents/{agentId}` | Remove agent from team |
| GET | `/api/chat/sessions` | List user's chat sessions |
| POST | `/api/chat/sessions` | Create new chat session |
| GET | `/api/chat/sessions/{id}/messages` | Get messages |
| POST | `/api/chat/sessions/{id}/messages` | Send message, get AI response |
| DELETE | `/api/chat/sessions/{id}` | Delete session |

**Chat Flow:**
1. User sends message with session_id
2. Backend loads session (agent_id or team_id)
3. Backend loads agent/team details from InvestigationTeam API (via K8s service: `http://investigation-team-api.investigation-team.svc.cluster.local`)
4. Backend loads message history from PostgreSQL
5. Backend constructs prompt:
   - System: "Eres [AgentName], un [Role] con habilidades en [Skills]. Tu descripción: [Description]"
   - Context: last N messages (max 20)
   - User message
6. Backend calls Google Gemini API with user's API key
7. Backend saves both messages to PostgreSQL
8. Backend returns AI response

**Team Chat Behavior:**
- When chatting with a team, the backend selects the agent whose role best matches the user's message
- Agent selection: analyze message keywords against agent roles and skills
- Response shows which agent is speaking: "[AgentName] (Role): response"
- If no clear match, the Coordinator agent responds

**Models:**
- `User` — id, email, password_hash, gemini_key, created_at, updated_at
- `ChatSession` — id, user_id, agent_id?, team_id?, title, created_at, updated_at
- `ChatMessage` — id, session_id, role (user/assistant), content, created_at

**Dependencies:**
- Google.Apis.GenerativeLanguage (Gemini SDK) — NuGet package: `Google.Apis.GenerativeLanguage`
- Npgsql + Entity Framework Core
- Microsoft.AspNetCore.Authentication.JwtBearer

### 3. PostgreSQL (Chat DB)

**Database:** `investigation_team_chat`
**Tables:**
- `users` — id, email, password_hash, gemini_key, created_at, updated_at
- `chat_sessions` — id, user_id, agent_id, team_id, title, created_at, updated_at
- `chat_messages` — id, session_id, role, content, created_at

**Secrets:**
- `chat-db-secret` — connection string, JWT key

## Data Flow

```
┌─────────────┐     ┌─────────────────┐     ┌──────────────────────┐
│   Angular   │────▶│  Chat Backend   │────▶│ InvestigationTeam API │
│   Frontend  │     │  (.NET + Gemini)│     │      (.NET)          │
└─────────────┘     └─────────────────┘     └──────────────────────┘
                           │
                           ▼
                    ┌─────────────────┐
                    │   PostgreSQL    │
                    │   (chat DB)     │
                    └─────────────────┘
```

## Error Handling

- **401 Unauthorized** — redirect to login
- **403 Forbidden** — show error toast
- **404 Not Found** — show error toast
- **500 Server Error** — show error toast, log to console
- **Gemini API error** — show "AI no disponible" message, save error in chat
- **Network error** — show "Sin conexión" message

## Testing

- Unit tests for services (Angular)
- Unit tests for controllers (Chat Backend)
- Integration tests for chat flow
- E2E tests for login + chat

## Deployment

- K3s with `imagePullPolicy: IfNotPresent`
- Docker images built locally and imported via `docker save | k3s ctr images import -`
- Socat forwards for Windows access
- Namespace: `investigation-team-frontend`

## Security Notes

- Gemini API keys stored encrypted in PostgreSQL (or plain for MVP)
- JWT secret stored in K8s Secret
- CORS configured for frontend origin only
- Rate limiting on chat endpoints (optional)
- **REMEMBER:** Disable Swagger for production deployment
