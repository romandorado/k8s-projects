import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Team } from '../models/team.model';

@Injectable({ providedIn: 'root' })
export class TeamsService {
  private readonly API_URL = '/api/teams';

  constructor(private http: HttpClient) {}

  getAll() { return this.http.get<Team[]>(this.API_URL); }
  getById(id: string) { return this.http.get<Team>(`${this.API_URL}/${id}`); }
  create(team: Partial<Team>) { return this.http.post<Team>(this.API_URL, team); }
  update(id: string, team: Partial<Team>) { return this.http.put(`${this.API_URL}/${id}`, team); }
  delete(id: string) { return this.http.delete(`${this.API_URL}/${id}`); }
  addAgent(teamId: string, agentId: string) { return this.http.post(`${this.API_URL}/${teamId}/agents/${agentId}`, {}); }
  removeAgent(teamId: string, agentId: string) { return this.http.delete(`${this.API_URL}/${teamId}/agents/${agentId}`); }
}