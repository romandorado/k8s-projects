import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="profile-container">
      <h2>Profile</h2>

      <div class="card">
        <h3>Account Info</h3>
        <form (ngSubmit)="updateProfile()">
          <div class="form-group">
            <label>Email</label>
            <input type="email" [(ngModel)]="email" name="email">
          </div>
          <div class="form-group">
            <label>Gemini API Key</label>
            <input type="password" [(ngModel)]="geminiApiKey" name="geminiApiKey">
          </div>
          <div class="success" *ngIf="profileSuccess">{{ profileSuccess }}</div>
          <div class="error" *ngIf="profileError">{{ profileError }}</div>
          <button type="submit" class="btn-primary">Update Profile</button>
        </form>
      </div>

      <div class="card">
        <h3>Change Password</h3>
        <form (ngSubmit)="changePassword()">
          <div class="form-group">
            <label>Current Password</label>
            <input type="password" [(ngModel)]="currentPassword" name="currentPassword">
          </div>
          <div class="form-group">
            <label>New Password</label>
            <input type="password" [(ngModel)]="newPassword" name="newPassword">
          </div>
          <div class="success" *ngIf="passwordSuccess">{{ passwordSuccess }}</div>
          <div class="error" *ngIf="passwordError">{{ passwordError }}</div>
          <button type="submit" class="btn-primary">Change Password</button>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .profile-container { max-width: 500px; }
    .card { margin-bottom: 20px; }
    .form-group { margin-bottom: 12px; }
    .form-group label { display: block; margin-bottom: 4px; font-size: 14px; }
  `]
})
export class ProfileComponent implements OnInit {
  email = '';
  geminiApiKey = '';
  currentPassword = '';
  newPassword = '';
  profileSuccess = '';
  profileError = '';
  passwordSuccess = '';
  passwordError = '';

  constructor(private auth: AuthService) {}

  ngOnInit() {
    const user = this.auth.user();
    if (user) {
      this.email = user.email;
    }
  }

  updateProfile() {
    this.profileSuccess = '';
    this.profileError = '';
    this.auth.updateProfile(this.email, this.geminiApiKey || undefined).subscribe({
      next: () => this.profileSuccess = 'Profile updated!',
      error: (err) => this.profileError = err.error || 'Error updating profile'
    });
  }

  changePassword() {
    this.passwordSuccess = '';
    this.passwordError = '';
    this.auth.changePassword(this.currentPassword, this.newPassword).subscribe({
      next: () => { this.passwordSuccess = 'Password changed!'; this.currentPassword = ''; this.newPassword = ''; },
      error: (err) => this.passwordError = err.error || 'Error changing password'
    });
  }
}