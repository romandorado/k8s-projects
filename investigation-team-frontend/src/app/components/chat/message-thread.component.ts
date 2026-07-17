import { Component, Input, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ChatMessage } from '../../models/chat.model';

@Component({
  selector: 'app-message-thread',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="thread" #thread>
      <div *ngFor="let msg of messages" class="message" [class.user]="msg.role === 'user'" [class.assistant]="msg.role === 'assistant'">
        <div class="bubble">{{ msg.content }}</div>
      </div>
      <div *ngIf="loading" class="message assistant">
        <div class="bubble typing">Typing...</div>
      </div>
    </div>
  `,
  styles: [`
    .thread { flex: 1; overflow-y: auto; padding: 20px; display: flex; flex-direction: column; gap: 12px; }
    .message { display: flex; }
    .message.user { justify-content: flex-end; }
    .message.assistant { justify-content: flex-start; }
    .bubble { max-width: 70%; padding: 12px 16px; border-radius: 12px; line-height: 1.5; }
    .message.user .bubble { background-color: #38bdf8; color: #0f172a; }
    .message.assistant .bubble { background-color: #334155; color: #e2e8f0; }
    .typing { font-style: italic; color: #94a3b8; }
  `]
})
export class MessageThreadComponent implements AfterViewChecked {
  @Input() messages: ChatMessage[] = [];
  @Input() loading = false;
  @ViewChild('thread') private thread!: ElementRef;

  private shouldScroll = false;

  ngAfterViewChecked() {
    if (this.shouldScroll) {
      this.scrollToBottom();
      this.shouldScroll = false;
    }
  }

  ngOnChanges() {
    this.shouldScroll = true;
  }

  private scrollToBottom(): void {
    try {
      this.thread.nativeElement.scrollTop = this.thread.nativeElement.scrollHeight;
    } catch {}
  }
}