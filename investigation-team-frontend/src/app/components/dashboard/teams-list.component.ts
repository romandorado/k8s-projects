import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { TeamsService } from '../../services/teams.service';
import { AgentsService } from '../../services/agents.service';
import { Team } from '../../models/team.model';
import { Agent } from '../../models/agent.model';

@Component({
  selector: 'app-teams-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="header">
      <h2>Teams</h2>
      <button class="btn-primary" (click)="showForm = true">+ New Team</button>
    </div>

    <table *ngIf="teams.length > 0">
      <thead>
        <tr>
          <th>Name</th>
          <th>Description</th>
          <th>Members</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let team of teams">
          <td>{{ team.name }}</td>
          <td>{{ team.description }}</td>
          <td>{{ team.agentIds.length }}</td>
          <td>
            <button class="btn-secondary" (click)="edit(team)">Edit</button>
            <button class="btn-danger" (click)="delete(team.id)">Delete</button>
          </td>
        </tr>
      </tbody>
    </table>

    <p *ngIf="teams.length === 0 && !loading">No teams yet. Create one!</p>

    <!-- Modal -->
    <div class="modal-overlay" *ngIf="showForm" (click)="closeForm()">
      <div class="card modal" (click)="$event.stopPropagation()">
        <h3>{{ editing ? 'Edit Team' : 'New Team' }}</h3>
        <form (ngSubmit)="save()">
          <div class="form-group">
            <label>Name</label>
            <input [(ngModel)]="form.name" name="name" required>
          </div>
          <div class="form-group">
            <label>Description</label>
            <input [(ngModel)]="form.description" name="description">
          </div>
          <div class="form-group" *ngIf="editing">
            <label>Members</label>
            <div *ngFor="let agent of allAgents" class="checkbox">
              <input type="checkbox" [checked]="form.agentIds.includes(agent.id)" (change)="toggleAgent(agent.id)">
              {{ agent.name }}
            </div>
          </div>
          <div class="modal-actions">
            <button type="button" class="btn-secondary" (click)="closeForm()">Cancel</button>
            <button type="submit" class="btn-primary">Save</button>
          </div>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 12px; text-align: left; border-bottom: 1px solid #334155; }
    th { color: #94a3b8; }
    .modal-overlay { position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.7); display: flex; justify-content: center; align-items: center; }
    .modal { width: 400px; }
    .form-group { margin-bottom: 12px; }
    .form-group label { display: block; margin-bottom: 4px; font-size: 14px; }
    .checkbox { padding: 4px 0; }
    .modal-actions { display: flex; gap: 8px; justify-content: flex-end; margin-top: 16px; }
  `]
})
export class TeamsListComponent implements OnInit {
  teams: Team[] = [];
  allAgents: Agent[] = [];
  loading = true;
  showForm = false;
  editing: Team | null = null;
  form = { name: '', description: '', agentIds: [] as string[] };

  constructor(private teamsService: TeamsService, private agentsService: AgentsService) {}

  ngOnInit() {
    this.teamsService.getAll().subscribe({
      next: t => { this.teams = t; this.loading = false; },
      error: () => { this.loading = false; }
    });
    this.agentsService.getAll().subscribe({ next: a => this.allAgents = a, error: () => {} });
  }

  edit(team: Team) {
    this.editing = team;
    this.form = { name: team.name, description: team.description || '', agentIds: [...team.agentIds] };
    this.showForm = true;
  }

  save() {
    const team = { name: this.form.name, description: this.form.description };
    const req = this.editing ? this.teamsService.update(this.editing.id, team) : this.teamsService.create(team);
    req.subscribe({
      next: () => {
        if (this.editing) {
          const toAdd = this.form.agentIds.filter(id => !this.editing!.agentIds.includes(id));
          const toRemove = this.editing.agentIds.filter(id => !this.form.agentIds.includes(id));
          const mutations = [
            ...toAdd.map(id => this.teamsService.addAgent(this.editing!.id, id)),
            ...toRemove.map(id => this.teamsService.removeAgent(this.editing!.id, id))
          ];
          if (mutations.length > 0) {
            forkJoin(mutations).subscribe({
              next: () => { this.closeForm(); this.teamsService.getAll().subscribe(t => this.teams = t); },
              error: () => { this.closeForm(); this.teamsService.getAll().subscribe(t => this.teams = t); }
            });
          } else {
            this.closeForm();
            this.teamsService.getAll().subscribe(t => this.teams = t);
          }
        } else {
          this.closeForm();
          this.teamsService.getAll().subscribe(t => this.teams = t);
        }
      },
      error: () => {}
    });
  }

  delete(id: string) {
    if (confirm('Delete this team?')) {
      this.teamsService.delete(id).subscribe({ next: () => this.teamsService.getAll().subscribe(t => this.teams = t), error: () => {} });
    }
  }

  toggleAgent(agentId: string) {
    const idx = this.form.agentIds.indexOf(agentId);
    idx > -1 ? this.form.agentIds.splice(idx, 1) : this.form.agentIds.push(agentId);
  }

  closeForm() {
    this.showForm = false;
    this.editing = null;
    this.form = { name: '', description: '', agentIds: [] };
  }
}