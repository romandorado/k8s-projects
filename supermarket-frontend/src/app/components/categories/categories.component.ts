import { Component, input, output, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ShoppingService } from '../../services/shopping.service';
import { CATEGORIES } from '../../models/item.model';

@Component({
  selector: 'app-categories',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="card categories">
      <h2>Categorías</h2>
      <div class="category-grid">
        <button
          class="category-btn"
          [class.active]="selectedCategory() === 'all'"
          (click)="categorySelected.emit('all')"
        >
          <span class="category-icon">📋</span>
          <span class="category-name">Todos</span>
          <span class="category-count">{{ shoppingService.getCategoryCount('all') }}</span>
        </button>

        @for (cat of categories; track cat.id) {
          <button
            class="category-btn"
            [class.active]="selectedCategory() === cat.id"
            [style.--cat-color]="cat.color"
            (click)="categorySelected.emit(cat.id)"
          >
            <span class="category-icon">{{ cat.icon }}</span>
            <span class="category-name">{{ cat.name }}</span>
            <span class="category-count">{{ shoppingService.getCategoryCount(cat.id) }}</span>
          </button>
        }
      </div>
    </div>
  `,
  styles: [`
    .category-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px;
    }

    .category-btn {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
      padding: 12px 8px;
      background: var(--bg);
      border: 2px solid var(--border);
      border-radius: 10px;
      cursor: pointer;
      transition: all 0.2s;
    }

    .category-btn:hover {
      border-color: var(--cat-color, var(--primary));
      background: var(--surface-hover);
    }

    .category-btn.active {
      border-color: var(--cat-color, var(--primary));
      background: var(--surface-hover);
    }

    .category-icon {
      font-size: 1.4rem;
    }

    .category-name {
      font-size: 0.7rem;
      color: var(--text-muted);
      text-align: center;
    }

    .category-count {
      font-size: 0.75rem;
      color: var(--text-muted);
      background: var(--bg);
      padding: 2px 8px;
      border-radius: 10px;
    }
  `]
})
export class CategoriesComponent {
  selectedCategory = input.required<string>();
  categorySelected = output<string>();

  categories = CATEGORIES;

  constructor(public shoppingService: ShoppingService) {}
}
