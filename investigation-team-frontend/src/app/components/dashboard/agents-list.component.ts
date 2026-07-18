import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AgentsService } from '../../services/agents.service';
import { Agent, ROLE_NAMES } from '../../models/agent.model';

@Component({
  selector: 'app-agents-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="header">
      <h2>Agents</h2>
      <button class="btn-primary" (click)="showForm = true">+ New Agent</button>
    </div>

    <table *ngIf="agents.length > 0">
      <thead>
        <tr>
          <th>Name</th>
          <th>Role</th>
          <th>Status</th>
          <th>Skills</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let agent of agents">
          <td>{{ agent.name }}</td>
          <td>{{ roleNames[agent.role] }}</td>
          <td>{{ agent.status === 0 ? 'Active' : agent.status === 1 ? 'Inactive' : 'Busy' }}</td>
          <td>{{ agent.skills.join(', ') }}</td>
          <td>
            <button class="btn-secondary" (click)="edit(agent)">Edit</button>
            <button class="btn-danger" (click)="delete(agent.id)">Delete</button>
          </td>
        </tr>
      </tbody>
    </table>

    <p *ngIf="agents.length === 0 && !loading">No agents yet. Create one!</p>

    <!-- Modal -->
    <div class="modal-overlay" *ngIf="showForm" (click)="closeForm()">
      <div class="card modal" (click)="$event.stopPropagation()">
        <h3>{{ editing ? 'Edit Agent' : 'New Agent' }}</h3>
        <form (ngSubmit)="save()">
          <div class="form-group">
            <label>Name</label>
            <input [(ngModel)]="form.name" name="name" required>
          </div>
          <div class="form-group">
            <label>Role</label>
            <select [(ngModel)]="form.role" name="role">
              <option *ngFor="let r of roleNames; let i = index" [value]="i">{{ r }}</option>
            </select>
          </div>
          <div class="form-group">
            <label>Description</label>
            <input [(ngModel)]="form.description" name="description">
          </div>
          <div class="form-group">
            <label>Skills (comma-separated)</label>
            <input [(ngModel)]="form.skillsText" name="skills">
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
    .modal-actions { display: flex; gap: 8px; justify-content: flex-end; margin-top: 16px; }
  `]
})
export class AgentsListComponent implements OnInit {
  agents: Agent[] = [];
  roleNames = ROLE_NAMES;
  loading = true;
  showForm = false;
  editing: Agent | null = null;
  form = { name: '', role: 0, description: '', skillsText: '' };

  constructor(private agentsService: AgentsService) {}

  ngOnInit() { this.load(); }

  load() {
    this.agentsService.getAll().subscribe({
      next: agents => { this.agents = agents; this.loading = false; },
      error: () => this.loading = false
    });
  }

  edit(agent: Agent) {
    this.editing = agent;
    this.form = { name: agent.name, role: agent.role, description: agent.description || '', skillsText: agent.skills.join(', ') };
    this.showForm = true;
  }

  save() {
    const agent = { name: this.form.name, role: +this.form.role, description: this.form.description, skills: this.form.skillsText.split(',').map(s => s.trim()).filter(Boolean) };
    const req = this.editing ? this.agentsService.update(this.editing.id, agent) : this.agentsService.create(agent);
    req.subscribe({ next: () => { this.closeForm(); this.load(); }, error: (err) => { alert(err.error?.message || 'Error saving agent'); } });
  }

  delete(id: string) {
    if (confirm('Delete this agent?')) {
      this.agentsService.delete(id).subscribe({ next: () => this.load(), error: (err) => alert(err.error?.message || 'Error deleting agent') });
    }
  }

  closeForm() {
    this.showForm = false;
    this.editing = null;
    this.form = { name: '', role: 0, description: '', skillsText: '' };
  }
}