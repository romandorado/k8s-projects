import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="register-container">
      <div class="card register-card">
        <h1>🔍 Investigation Team</h1>
        <p>Crea tu cuenta</p>

        <form (ngSubmit)="onSubmit()">
          <div class="form-group">
            <label>Email</label>
            <input type="email" [(ngModel)]="email" name="email" required>
          </div>

          <div class="form-group">
            <label>Password</label>
            <input type="password" [(ngModel)]="password" name="password" required minlength="6">
          </div>

          <div class="form-group">
            <label>Groq API Key</label>
            <input type="password" [(ngModel)]="geminiApiKey" name="geminiApiKey" required>
            <small>Obtén tu key en <a href="https://console.groq.com/keys" target="_blank">Groq Console</a></small>
          </div>

          <div class="error" *ngIf="error()">{{ error() }}</div>

          <button type="submit" class="btn-primary full-width" [disabled]="loading()">
            {{ loading() ? 'Creando...' : 'Crear Cuenta' }}
          </button>
        </form>

        <p class="link">¿Ya tienes cuenta? <a routerLink="/login">Inicia sesión</a></p>
      </div>
    </div>
  `,
  styles: [`
    .register-container { display: flex; justify-content: center; align-items: center; min-height: 100vh; }
    .register-card { width: 100%; max-width: 400px; text-align: center; }
    h1 { margin-bottom: 8px; }
    .form-group { margin-bottom: 16px; text-align: left; }
    .form-group label { display: block; margin-bottom: 4px; font-size: 14px; }
    .form-group small { display: block; margin-top: 4px; font-size: 12px; color: #94a3b8; }
    .full-width { width: 100%; margin-top: 16px; padding: 12px; }
    .link { margin-top: 16px; font-size: 14px; }
  `]
})
export class RegisterComponent {
  email = '';
  password = '';
  geminiApiKey = '';
  loading = signal(false);
  error = signal('');

  constructor(private auth: AuthService, private router: Router) {}

  onSubmit() {
    this.loading.set(true);
    this.error.set('');
    this.auth.register(this.email, this.password, this.geminiApiKey).subscribe({
      next: () => this.router.navigate(['/']),
      error: (err) => {
        try {
          const body = err.error;
          if (body?.errors) {
            const messages = Object.values(body.errors).flat().join('. ');
            this.error.set(messages);
          } else {
            this.error.set(typeof body === 'string' ? body : body?.message || 'Error al crear cuenta');
          }
        } catch { this.error.set('Error al crear cuenta'); }
        this.loading.set(false);
      }
    });
  }
}
