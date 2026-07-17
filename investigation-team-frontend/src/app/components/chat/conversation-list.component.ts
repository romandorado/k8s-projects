import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ChatSession } from '../../models/chat.model';

@Component({
  selector: 'app-conversation-list',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="conversation-list">
      <div class="list-header">
        <h3>Conversations</h3>
        <button class="btn-primary" (click)="newChat.emit()">+ New</button>
      </div>
      <div class="conversations">
        <div *ngFor="let session of sessions" class="conversation-item" [class.active]="session.id === activeId" (click)="select.emit(session.id)">
          <span class="title">{{ session.title }}</span>
          <button class="btn-icon" (click)="delete.emit(session.id); $event.stopPropagation()">×</button>
        </div>
        <p *ngIf="sessions.length === 0" class="empty">No conversations yet</p>
      </div>
    </div>
  `,
  styles: [`
    .conversation-list { height: 100%; display: flex; flex-direction: column; }
    .list-header { display: flex; justify-content: space-between; align-items: center; padding: 12px; border-bottom: 1px solid #334155; }
    .conversations { flex: 1; overflow-y: auto; }
    .conversation-item { display: flex; justify-content: space-between; align-items: center; padding: 12px; cursor: pointer; border-bottom: 1px solid #1e293b; }
    .conversation-item:hover { background-color: #334155; }
    .conversation-item.active { background-color: #38bdf8; color: #0f172a; }
    .title { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .btn-icon { background: none; color: inherit; padding: 4px 8px; font-size: 16px; }
    .empty { padding: 20px; text-align: center; color: #64748b; }
  `]
})
export class ConversationListComponent {
  @Input() sessions: ChatSession[] = [];
  @Input() activeId: string | null = null;
  @Output() select = new EventEmitter<string>();
  @Output() delete = new EventEmitter<string>();
  @Output() newChat = new EventEmitter<void>();
}