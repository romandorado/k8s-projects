import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ShoppingService } from '../../services/shopping.service';

@Component({
  selector: 'app-budget-tracker',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="card budget-tracker">
      <h2>Presupuesto</h2>

      <div class="budget-display">
        @if (isEditing()) {
          <div class="budget-edit">
            <input
              type="number"
              step="0.01"
              min="0"
              [ngModel]="tempBudget()"
              (ngModelChange)="tempBudget.set($event)"
              (keydown.enter)="onSaveBudget()"
              (keydown.escape)="onCancelEdit()"
              (blur)="onSaveBudget()"
              class="input input-budget"
              autofocus
            />
            <span class="currency">€</span>
          </div>
        } @else {
          <div
            class="budget-amount"
            (click)="onStartEdit()"
            title="Clic para editar"
          >
            {{ shoppingService.budget().toFixed(2) }} €
          </div>
        }
      </div>

      <div class="budget-stats">
        <div class="stat">
          <span class="stat-label">Gastado</span>
          <span class="stat-value spent">{{ shoppingService.totalSpent().toFixed(2) }} €</span>
        </div>
        <div class="stat">
          <span class="stat-label">Restante</span>
          <span class="stat-value" [class.over]="isOverBudget()" [class.remaining]="!isOverBudget()">
            {{ shoppingService.remaining().toFixed(2) }} €
          </span>
        </div>
      </div>

      @if (shoppingService.budget() > 0) {
        <div class="progress-bar">
          <div
            class="progress-fill"
            [class.over]="isOverBudget()"
            [style.width.%]="percentage()"
          ></div>
          <span class="progress-text">{{ percentage().toFixed(0) }}%</span>
        </div>
      }

      @if (shoppingService.budget() === 0) {
        <p class="budget-hint">Clic en el monto para definir tu presupuesto</p>
      }
    </div>
  `,
  styles: [`
    .budget-display {
      text-align: center;
      margin-bottom: 20px;
    }

    .budget-amount {
      font-size: 2.5rem;
      font-weight: 700;
      cursor: pointer;
      transition: color 0.2s;
    }

    .budget-amount:hover {
      color: var(--primary);
    }

    .budget-edit {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 5px;
    }

    .input-budget {
      width: 150px;
      text-align: right;
      font-size: 2rem;
      font-weight: 700;
    }

    .currency {
      font-size: 1.5rem;
      color: var(--text-muted);
    }

    .budget-stats {
      display: flex;
      justify-content: space-between;
      margin-bottom: 20px;
    }

    .stat {
      text-align: center;
    }

    .stat-label {
      display: block;
      font-size: 0.8rem;
      color: var(--text-muted);
      margin-bottom: 4px;
    }

    .stat-value {
      font-size: 1.1rem;
      font-weight: 600;
    }

    .stat-value.spent {
      color: var(--warning);
    }

    .stat-value.remaining {
      color: var(--success);
    }

    .stat-value.over {
      color: var(--danger);
    }

    .progress-bar {
      position: relative;
      height: 24px;
      background: var(--bg);
      border-radius: 12px;
      overflow: hidden;
    }

    .progress-fill {
      height: 100%;
      background: var(--primary);
      border-radius: 12px;
      transition: width 0.3s;
    }

    .progress-fill.over {
      background: var(--danger);
    }

    .progress-text {
      position: absolute;
      top: 50%;
      left: 50%;
      transform: translate(-50%, -50%);
      font-size: 0.8rem;
      font-weight: 600;
    }

    .budget-hint {
      text-align: center;
      font-size: 0.85rem;
      color: var(--text-muted);
      margin-top: 10px;
    }

    .input {
      width: 100%;
      padding: 10px 12px;
      background: var(--bg);
      border: 1px solid var(--border);
      border-radius: 8px;
      color: var(--text);
      font-size: 0.95rem;
      transition: border-color 0.2s;
    }

    .input:focus {
      outline: none;
      border-color: var(--primary);
    }
  `]
})
export class BudgetTrackerComponent {
  isEditing = signal(false);
  tempBudget = signal(0);

  isOverBudget = computed(() => this.shoppingService.remaining() < 0);

  percentage = computed(() => {
    const budget = this.shoppingService.budget();
    const spent = this.shoppingService.totalSpent();
    return budget > 0 ? (spent / budget) * 100 : 0;
  });

  constructor(public shoppingService: ShoppingService) {}

  onStartEdit(): void {
    this.tempBudget.set(this.shoppingService.budget());
    this.isEditing.set(true);
  }

  onSaveBudget(): void {
    this.shoppingService.setBudget(this.tempBudget() || 0);
    this.isEditing.set(false);
  }

  onCancelEdit(): void {
    this.isEditing.set(false);
  }
}
