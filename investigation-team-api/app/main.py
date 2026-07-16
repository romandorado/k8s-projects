from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import Optional, List
from enum import Enum
import uuid
from datetime import datetime

app = FastAPI(
    title="InvestigationTeam API",
    description="API para gestionar equipos de investigación con agentes",
    version="1.0.0"
)

# Modelos de datos
class AgentRole(str, Enum):
    RESEARCHER = "researcher"
    ANALYST = "analyst"
    WRITER = "writer"
    COORDINATOR = "coordinator"
    REVIEWER = "reviewer"

class AgentStatus(str, Enum):
    ACTIVE = "active"
    INACTIVE = "inactive"
    BUSY = "busy"

class AgentBase(BaseModel):
    name: str
    role: AgentRole
    description: Optional[str] = None
    skills: List[str] = []

class AgentCreate(AgentBase):
    pass

class AgentUpdate(BaseModel):
    name: Optional[str] = None
    role: Optional[AgentRole] = None
    description: Optional[str] = None
    skills: Optional[List[str]] = None
    status: Optional[AgentStatus] = None

class Agent(AgentBase):
    id: str
    status: AgentStatus = AgentStatus.ACTIVE
    created_at: datetime
    updated_at: datetime

class Team(BaseModel):
    id: str
    name: str
    description: Optional[str] = None
    agents: List[str] = []
    created_at: datetime

# Base de datos en memoria (luego se conectará a PostgreSQL)
agents_db: dict[str, Agent] = {}
teams_db: dict[str, Team] = {}

# Endpoints de Agentes
@app.get("/agents", response_model=List[Agent])
def list_agents():
    """Listar todos los agentes"""
    return list(agents_db.values())

@app.get("/agents/{agent_id}", response_model=Agent)
def get_agent(agent_id: str):
    """Obtener un agente por ID"""
    if agent_id not in agents_db:
        raise HTTPException(status_code=404, detail="Agente no encontrado")
    return agents_db[agent_id]

@app.post("/agents", response_model=Agent, status_code=201)
def create_agent(agent: AgentCreate):
    """Crear un nuevo agente"""
    agent_id = str(uuid.uuid4())
    now = datetime.now()
    new_agent = Agent(
        id=agent_id,
        **agent.dict(),
        created_at=now,
        updated_at=now
    )
    agents_db[agent_id] = new_agent
    return new_agent

@app.put("/agents/{agent_id}", response_model=Agent)
def update_agent(agent_id: str, agent_update: AgentUpdate):
    """Actualizar un agente"""
    if agent_id not in agents_db:
        raise HTTPException(status_code=404, detail="Agente no encontrado")
    
    stored_agent = agents_db[agent_id]
    update_data = agent_update.dict(exclude_unset=True)
    
    updated_agent = stored_agent.copy(update=update_data)
    updated_agent.updated_at = datetime.now()
    agents_db[agent_id] = updated_agent
    return updated_agent

@app.delete("/agents/{agent_id}", status_code=204)
def delete_agent(agent_id: str):
    """Eliminar un agente"""
    if agent_id not in agents_db:
        raise HTTPException(status_code=404, detail="Agente no encontrado")
    del agents_db[agent_id]

# Endpoints de Teams
@app.get("/teams", response_model=List[Team])
def list_teams():
    """Listar todos los equipos"""
    return list(teams_db.values())

@app.get("/teams/{team_id}", response_model=Team)
def get_team(team_id: str):
    """Obtener un equipo por ID"""
    if team_id not in teams_db:
        raise HTTPException(status_code=404, detail="Equipo no encontrado")
    return teams_db[team_id]

@app.post("/teams", response_model=Team, status_code=201)
def create_team(name: str, description: Optional[str] = None):
    """Crear un nuevo equipo"""
    team_id = str(uuid.uuid4())
    new_team = Team(
        id=team_id,
        name=name,
        description=description,
        created_at=datetime.now()
    )
    teams_db[team_id] = new_team
    return new_team

@app.post("/teams/{team_id}/agents/{agent_id}")
def add_agent_to_team(team_id: str, agent_id: str):
    """Agregar un agente a un equipo"""
    if team_id not in teams_db:
        raise HTTPException(status_code=404, detail="Equipo no encontrado")
    if agent_id not in agents_db:
        raise HTTPException(status_code=404, detail="Agente no encontrado")
    
    team = teams_db[team_id]
    if agent_id not in team.agents:
        team.agents.append(agent_id)
    return {"message": "Agente agregado al equipo"}

@app.delete("/teams/{team_id}/agents/{agent_id}")
def remove_agent_from_team(team_id: str, agent_id: str):
    """Remover un agente de un equipo"""
    if team_id not in teams_db:
        raise HTTPException(status_code=404, detail="Equipo no encontrado")
    
    team = teams_db[team_id]
    if agent_id in team.agents:
        team.agents.remove(agent_id)
    return {"message": "Agente removido del equipo"}

# Endpoint de salud
@app.get("/health")
def health_check():
    """Health check endpoint"""
    return {"status": "healthy", "timestamp": datetime.now()}

@app.get("/")
def root():
    """Root endpoint"""
    return {
        "name": "InvestigationTeam API",
        "version": "1.0.0",
        "docs": "/docs"
    }
