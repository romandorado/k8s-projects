import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ChatSession, ChatMessage } from '../models/chat.model';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly API_URL = '/api/chat';

  constructor(private http: HttpClient) {}

  getSessions() { return this.http.get<ChatSession[]>(`${this.API_URL}/sessions`); }
  createSession(agentId?: string, teamId?: string) { return this.http.post<ChatSession>(`${this.API_URL}/sessions`, { agentId, teamId }); }
  getMessages(sessionId: string) { return this.http.get<ChatMessage[]>(`${this.API_URL}/sessions/${sessionId}/messages`); }
  sendMessage(sessionId: string, content: string) { return this.http.post<{ content: string }>(`${this.API_URL}/sessions/${sessionId}/messages`, { content }); }
  deleteSession(sessionId: string) { return this.http.delete(`${this.API_URL}/sessions/${sessionId}`); }
}