import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ShoppingService } from '../../services/shopping.service';
import { CATEGORIES } from '../../models/item.model';

@Component({
  selector: 'app-add-item-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="card add-item-form">
      <h2>Agregar Producto</h2>
      <form (ngSubmit)="onSubmit()">
        <div class="form-group">
          <input
            type="text"
            placeholder="Nombre del producto"
            [(ngModel)]="name"
            name="name"
            class="input"
          />
        </div>

        <div class="form-row">
          <div class="form-group">
            <label>Cantidad</label>
            <input
              type="number"
              min="1"
              [(ngModel)]="quantity"
              name="quantity"
              class="input input-small"
            />
          </div>

          <div class="form-group">
            <label>Precio (€)</label>
            <input
              type="number"
              step="0.01"
              min="0"
              placeholder="0.00"
              [(ngModel)]="price"
              name="price"
              class="input input-small"
            />
          </div>
        </div>

        <div class="form-group">
          <label>Categoría</label>
          <select
            [(ngModel)]="categoryValue"
            name="category"
            class="input select"
          >
            @for (cat of categories; track cat.id) {
              <option [value]="cat.value">{{ cat.icon }} {{ cat.name }}</option>
            }
          </select>
        </div>

        <button type="submit" class="btn btn-primary">
          Agregar
        </button>
      </form>
    </div>
  `,
  styles: [`
    .form-group {
      margin-bottom: 12px;
    }

    .form-group label {
      display: block;
      font-size: 0.85rem;
      color: var(--text-muted);
      margin-bottom: 5px;
    }

    .form-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 12px;
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

    .input::placeholder {
      color: var(--text-muted);
    }

    .select {
      cursor: pointer;
    }

    .btn {
      width: 100%;
      padding: 10px 16px;
      border: none;
      border-radius: 8px;
      font-size: 0.95rem;
      font-weight: 500;
      cursor: pointer;
      transition: background 0.2s;
    }

    .btn-primary {
      background: var(--primary);
      color: white;
    }

    .btn-primary:hover {
      background: var(--primary-hover);
    }
  `]
})
export class AddItemFormComponent {
  categories = CATEGORIES;
  name = '';
  quantity = 1;
  categoryValue = 1;
  price = '';

  constructor(private shoppingService: ShoppingService) {}

  onSubmit(): void {
    if (!this.name.trim()) return;

    this.shoppingService.addItem({
      name: this.name.trim(),
      quantity: parseInt(this.quantity.toString()) || 1,
      category: parseInt(this.categoryValue.toString()),
      price: parseFloat(this.price) || 0
    });

    this.name = '';
    this.quantity = 1;
    this.categoryValue = 1;
    this.price = '';
  }
}
