import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConversationListComponent } from './conversation-list.component';
import { MessageThreadComponent } from './message-thread.component';
import { ChatService } from '../../services/chat.service';
import { AgentsService } from '../../services/agents.service';
import { TeamsService } from '../../services/teams.service';
import { ChatSession, ChatMessage } from '../../models/chat.model';
import { Agent, ROLE_EMOJIS } from '../../models/agent.model';
import { Team } from '../../models/team.model';

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule, ConversationListComponent, MessageThreadComponent],
  template: `
    <div class="chat-layout">
      <div class="sidebar">
        <app-conversation-list
          [sessions]="sessions"
          [activeId]="activeSessionId"
          (select)="selectSession($event)"
          (delete)="deleteSession($event)"
          (newChat)="showNewChat = true"
        />
      </div>
      <div class="chat-main">
        <div *ngIf="!activeSessionId && !showNewChat" class="empty-state">
          <h2>🔍 Investigation Team Chat</h2>
          <p>Select a conversation or start a new one</p>
        </div>

        <div *ngIf="showNewChat" class="new-chat">
          <h3>New Conversation</h3>
          <div class="options">
            <h4>Chat with Agent</h4>
            <div class="agent-grid">
              <button *ngFor="let agent of agents" class="agent-card" (click)="createSession(agent.id)">
                <span class="emoji">{{ roleEmojis[agent.role] }}</span>
                <span>{{ agent.name }}</span>
                <small>{{ roleNames[agent.role] }}</small>
              </button>
            </div>
            <h4>Chat with Team</h4>
            <div class="agent-grid">
              <button *ngFor="let team of teams" class="agent-card" (click)="createSession(undefined, team.id)">
                <span class="emoji">👥</span>
                <span>{{ team.name }}</span>
                <small>{{ team.agentIds.length }} members</small>
              </button>
            </div>
            <button class="btn-secondary" (click)="showNewChat = false">Cancel</button>
          </div>
        </div>

        <div *ngIf="activeSessionId && !showNewChat" class="active-chat">
          <app-message-thread [messages]="messages" [loading]="sending" />
          <div class="input-area">
            <textarea [(ngModel)]="newMessage" placeholder="Type your message..." (keydown.enter)="$event.preventDefault(); send()"></textarea>
            <button class="btn-primary" (click)="send()" [disabled]="!newMessage.trim() || sending">Send</button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .chat-layout { display: flex; height: calc(100vh - 40px); }
    .sidebar { width: 280px; border-right: 1px solid #334155; }
    .chat-main { flex: 1; display: flex; flex-direction: column; }
    .empty-state { display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100%; color: #64748b; }
    .new-chat { padding: 20px; }
    .options { margin-top: 16px; }
    .agent-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 12px; margin: 12px 0 24px; }
    .agent-card { display: flex; flex-direction: column; align-items: center; padding: 16px; background-color: #1e293b; border: 1px solid #334155; border-radius: 8px; cursor: pointer; }
    .agent-card:hover { border-color: #38bdf8; }
    .emoji { font-size: 24px; margin-bottom: 8px; }
    .active-chat { display: flex; flex-direction: column; flex: 1; }
    .input-area { display: flex; gap: 8px; padding: 16px; border-top: 1px solid #334155; }
    .input-area textarea { flex: 1; resize: none; height: 60px; }
    .input-area button { align-self: flex-end; }
  `]
})
export class ChatComponent implements OnInit {
  sessions: ChatSession[] = [];
  messages: ChatMessage[] = [];
  agents: Agent[] = [];
  teams: Team[] = [];
  activeSessionId: string | null = null;
  showNewChat = false;
  newMessage = '';
  sending = false;
  roleEmojis = ROLE_EMOJIS;
  roleNames = ['Investigador', 'Analista', 'Escritor', 'Coordinador', 'Revisor'];

  constructor(private chatService: ChatService, private agentsService: AgentsService, private teamsService: TeamsService) {}

  ngOnInit() {
    this.chatService.getSessions().subscribe({ next: s => this.sessions = s, error: () => {} });
    this.agentsService.getAll().subscribe({ next: a => this.agents = a, error: () => {} });
    this.teamsService.getAll().subscribe({ next: t => this.teams = t, error: () => {} });
  }

  selectSession(id: string) {
    this.activeSessionId = id;
    this.showNewChat = false;
    this.messages = [];
    this.chatService.getMessages(id).subscribe({ next: m => this.messages = m, error: () => {} });
  }

  createSession(agentId?: string, teamId?: string) {
    this.chatService.createSession(agentId, teamId).subscribe({
      next: session => {
        this.sessions.unshift(session);
        this.selectSession(session.id);
        this.showNewChat = false;
      },
      error: () => {}
    });
  }

  send() {
    if (!this.newMessage.trim() || !this.activeSessionId) return;
    this.sending = true;
    const msg = this.newMessage;
    this.newMessage = '';

    this.messages.push({ id: '', role: 'user', content: msg, createdAt: new Date().toISOString() });

    this.chatService.sendMessage(this.activeSessionId, msg).subscribe({
      next: res => {
        this.messages.push({ id: '', role: 'assistant', content: res.content, createdAt: new Date().toISOString() });
        this.sending = false;
      },
      error: () => {
        this.messages.push({ id: '', role: 'assistant', content: 'Error: No se pudo obtener respuesta.', createdAt: new Date().toISOString() });
        this.sending = false;
      }
    });
  }

  deleteSession(id: string) {
    if (!confirm('Delete this conversation?')) return;
    this.chatService.deleteSession(id).subscribe({
      next: () => {
        this.sessions = this.sessions.filter(s => s.id !== id);
        if (this.activeSessionId === id) {
          this.activeSessionId = null;
          this.messages = [];
        }
      },
      error: () => {}
    });
  }
}