import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="login-container">
      <div class="card login-card">
        <h1>🔍 Investigation Team</h1>
        <p>Inicia sesión para continuar</p>

        <form (ngSubmit)="onSubmit()">
          <div class="form-group">
            <label>Email</label>
            <input type="email" [(ngModel)]="email" name="email" required>
          </div>

          <div class="form-group">
            <label>Password</label>
            <input type="password" [(ngModel)]="password" name="password" required>
          </div>

          <div class="error" *ngIf="error">{{ error }}</div>

          <button type="submit" class="btn-primary full-width" [disabled]="loading">
            {{ loading ? 'Ingresando...' : 'Iniciar Sesión' }}
          </button>
        </form>

        <p class="link">¿No tienes cuenta? <a routerLink="/register">Regístrate</a></p>
      </div>
    </div>
  `,
  styles: [`
    .login-container { display: flex; justify-content: center; align-items: center; min-height: 100vh; }
    .login-card { width: 100%; max-width: 400px; text-align: center; }
    h1 { margin-bottom: 8px; }
    .form-group { margin-bottom: 16px; text-align: left; }
    .form-group label { display: block; margin-bottom: 4px; font-size: 14px; }
    .full-width { width: 100%; margin-top: 16px; padding: 12px; }
    .link { margin-top: 16px; font-size: 14px; }
  `]
})
export class LoginComponent {
  email = '';
  password = '';
  loading = false;
  error = '';

  constructor(private auth: AuthService, private router: Router) {}

  onSubmit() {
    this.loading = true;
    this.error = '';
    this.auth.login(this.email, this.password).subscribe({
      next: () => this.router.navigate(['/']),
      error: (err) => {
        this.error = err.error || 'Error al iniciar sesión';
        this.loading = false;
      }
    });
  }
}