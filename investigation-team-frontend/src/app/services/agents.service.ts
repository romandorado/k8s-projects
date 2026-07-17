import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Agent } from '../models/agent.model';

@Injectable({ providedIn: 'root' })
export class AgentsService {
  private readonly API_URL = '/api/agents';

  constructor(private http: HttpClient) {}

  getAll() { return this.http.get<Agent[]>(this.API_URL); }
  getById(id: string) { return this.http.get<Agent>(`${this.API_URL}/${id}`); }
  create(agent: Partial<Agent>) { return this.http.post<Agent>(this.API_URL, agent); }
  update(id: string, agent: Partial<Agent>) { return this.http.put(`${this.API_URL}/${id}`, agent); }
  delete(id: string) { return this.http.delete(`${this.API_URL}/${id}`); }
}