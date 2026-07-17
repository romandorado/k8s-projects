import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./components/login/login.component').then(m => m.LoginComponent) },
  { path: 'register', loadComponent: () => import('./components/register/register.component').then(m => m.RegisterComponent) },
  {
    path: '',
    loadComponent: () => import('./components/dashboard/dashboard.component').then(m => m.DashboardComponent),
    canActivate: [authGuard],
    children: [
      { path: 'agents', loadComponent: () => import('./components/dashboard/agents-list.component').then(m => m.AgentsListComponent) },
      { path: 'teams', loadComponent: () => import('./components/dashboard/teams-list.component').then(m => m.TeamsListComponent) },
      { path: 'chat', loadComponent: () => import('./components/chat/chat.component').then(m => m.ChatComponent) },
      { path: 'profile', loadComponent: () => import('./components/profile/profile.component').then(m => m.ProfileComponent) },
      { path: '', redirectTo: 'agents', pathMatch: 'full' }
    ]
  },
  { path: '**', redirectTo: '' }
];