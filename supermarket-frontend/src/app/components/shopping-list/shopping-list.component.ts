import { Component, input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ShoppingService } from '../../services/shopping.service';
import { CATEGORIES, ShoppingItem } from '../../models/item.model';

@Component({
  selector: 'app-shopping-list',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (filteredItems().length === 0) {
      <div class="card shopping-list empty">
        <div class="empty-state">
          <span class="empty-icon">🛒</span>
          <p>Tu lista está vacía</p>
          <p class="empty-hint">Agrega productos desde el formulario</p>
        </div>
      </div>
    } @else {
      <div class="card shopping-list">
        <div class="list-header">
          <h2>Lista de Compra</h2>
          @if (checkedCount() > 0) {
            <button class="btn btn-danger btn-small" (click)="onClearChecked()">
              Limpiar ({{ checkedCount() }})
            </button>
          }
        </div>

        @for (group of groupedItems(); track group.categoryId) {
          <div class="category-group">
            <h3 class="category-title" [style.borderBottomColor]="group.color">
              {{ group.icon }} {{ group.name }}
            </h3>
            <ul class="item-list">
              @for (item of group.items; track item.id) {
                <li class="item" [class.checked]="item.checked">
                  <label class="item-label">
                    <input
                      type="checkbox"
                      [checked]="item.checked"
                      (change)="onToggle(item.id)"
                    />
                    <span class="item-name">{{ item.name }}</span>
                    @if (item.quantity > 1) {
                      <span class="item-quantity">x{{ item.quantity }}</span>
                    }
                    @if (item.price > 0) {
                      <span class="item-price">{{ item.price.toFixed(2) }}€</span>
                    }
                  </label>
                  <button
                    class="btn-icon"
                    (click)="onRemove(item.id)"
                    title="Eliminar"
                  >
                    ✕
                  </button>
                </li>
              }
            </ul>
          </div>
        }
      </div>
    }
  `,
  styles: [`
    .shopping-list.empty {
      min-height: 300px;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .empty-state {
      text-align: center;
      color: var(--text-muted);
    }

    .empty-icon {
      font-size: 4rem;
      display: block;
      margin-bottom: 15px;
    }

    .empty-hint {
      font-size: 0.85rem;
      margin-top: 5px;
    }

    .list-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 15px;
    }

    .list-header h2 {
      margin-bottom: 0;
    }

    .category-group {
      margin-bottom: 20px;
    }

    .category-title {
      font-size: 0.9rem;
      font-weight: 600;
      color: var(--text-muted);
      padding-bottom: 8px;
      border-bottom: 2px solid;
      margin-bottom: 10px;
    }

    .item-list {
      list-style: none;
    }

    .item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 10px 12px;
      background: var(--bg);
      border-radius: 8px;
      margin-bottom: 6px;
      transition: opacity 0.2s;
    }

    .item.checked {
      opacity: 0.5;
    }

    .item.checked .item-name {
      text-decoration: line-through;
    }

    .item-label {
      display: flex;
      align-items: center;
      gap: 10px;
      cursor: pointer;
      flex: 1;
    }

    .item-label input[type="checkbox"] {
      width: 18px;
      height: 18px;
      cursor: pointer;
      accent-color: var(--primary);
    }

    .item-name {
      font-weight: 500;
    }

    .item-quantity {
      font-size: 0.8rem;
      color: var(--text-muted);
      background: var(--surface);
      padding: 2px 6px;
      border-radius: 4px;
    }

    .item-price {
      font-size: 0.85rem;
      color: var(--success);
      font-weight: 500;
    }

    .btn-icon {
      background: none;
      border: none;
      color: var(--text-muted);
      cursor: pointer;
      padding: 4px 8px;
      border-radius: 4px;
      font-size: 0.9rem;
    }

    .btn-icon:hover {
      background: var(--danger);
      color: white;
    }

    .btn-danger {
      background: var(--danger);
      color: white;
    }

    .btn-danger:hover {
      background: var(--danger-hover);
    }

    .btn-small {
      width: auto;
      padding: 6px 12px;
      font-size: 0.8rem;
    }
  `]
})
export class ShoppingListComponent {
  selectedCategory = input.required<string>();

  constructor(private shoppingService: ShoppingService) {}

  filteredItems = computed(() => {
    return this.shoppingService.getItemsByCategory(this.selectedCategory());
  });

  checkedCount = computed(() => {
    return this.shoppingService.items().filter(i => i.checked).length;
  });

  groupedItems = computed(() => {
    const items = this.filteredItems();
    const groups: { categoryId: string; name: string; icon: string; color: string; items: ShoppingItem[] }[] = [];

    const grouped = items.reduce((acc, item) => {
      const cat = item.category || 'other';
      if (!acc[cat]) acc[cat] = [];
      acc[cat].push(item);
      return acc;
    }, {} as Record<string, ShoppingItem[]>);

    for (const [catId, catItems] of Object.entries(grouped)) {
      const cat = CATEGORIES.find(c => c.id === catId) || CATEGORIES[CATEGORIES.length - 1];
      groups.push({
        categoryId: catId,
        name: cat.name,
        icon: cat.icon,
        color: cat.color,
        items: catItems
      });
    }

    return groups;
  });

  onToggle(id: number): void {
    this.shoppingService.toggleItem(id);
  }

  onRemove(id: number): void {
    this.shoppingService.removeItem(id);
  }

  onClearChecked(): void {
    this.shoppingService.clearChecked();
  }
}
