import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="dashboard-layout">
      <aside class="sidebar">
        <div class="logo">🔍 Investigation Team</div>
        <nav>
          <a routerLink="/agents" routerLinkActive="active">Agents</a>
          <a routerLink="/teams" routerLinkActive="active">Teams</a>
          <a routerLink="/chat" routerLinkActive="active">Chat</a>
          <a routerLink="/profile" routerLinkActive="active">Profile</a>
        </nav>
        <button class="btn-secondary logout" (click)="auth.logout()">Logout</button>
      </aside>
      <main class="content">
        <router-outlet />
      </main>
    </div>
  `,
  styles: [`
    .dashboard-layout { display: flex; min-height: 100vh; }
    .sidebar { width: 240px; background-color: #1e293b; padding: 20px; display: flex; flex-direction: column; }
    .logo { font-size: 18px; font-weight: bold; margin-bottom: 30px; }
    nav { flex: 1; }
    nav a { display: block; padding: 10px 12px; border-radius: 6px; margin-bottom: 4px; color: #94a3b8; }
    nav a:hover { background-color: #334155; color: #e2e8f0; }
    nav a.active { background-color: #38bdf8; color: #0f172a; }
    .logout { margin-top: auto; width: 100%; }
    .content { flex: 1; padding: 20px; }
  `]
})
export class DashboardComponent {
  constructor(public auth: AuthService) {}
}