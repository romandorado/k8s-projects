import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { User } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly API_URL = '/api/auth';
  private tokenSignal = signal<string | null>(localStorage.getItem('token'));
  private userSignal = signal<User | null>(null);

  token = this.tokenSignal.asReadonly();
  isAuthenticated = computed(() => !!this.tokenSignal());
  user = this.userSignal.asReadonly();

  constructor(private http: HttpClient, private router: Router) {
    if (this.tokenSignal()) {
      this.loadUser();
    }
  }

  login(email: string, password: string) {
    return this.http.post<{ token: string }>(`${this.API_URL}/login`, { email, password })
      .pipe(tap(res => {
        localStorage.setItem('token', res.token);
        this.tokenSignal.set(res.token);
        this.loadUser();
      }));
  }

  register(email: string, password: string, geminiApiKey: string) {
    return this.http.post<{ token: string }>(`${this.API_URL}/register`, { email, password, geminiApiKey })
      .pipe(tap(res => {
        localStorage.setItem('token', res.token);
        this.tokenSignal.set(res.token);
        this.loadUser();
      }));
  }

  logout() {
    localStorage.removeItem('token');
    this.tokenSignal.set(null);
    this.userSignal.set(null);
    this.router.navigate(['/login']);
  }

  loadUser() {
    this.http.get<User>(`${this.API_URL}/me`).subscribe({
      next: user => this.userSignal.set(user),
      error: () => this.logout()
    });
  }

  updateProfile(email?: string, geminiApiKey?: string) {
    return this.http.put<User>(`${this.API_URL}/me`, { email, geminiApiKey })
      .pipe(tap(() => this.loadUser()));
  }

  changePassword(currentPassword: string, newPassword: string) {
    return this.http.put(`${this.API_URL}/me/password`, { currentPassword, newPassword });
  }
}