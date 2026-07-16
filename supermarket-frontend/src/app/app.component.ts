import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AddItemFormComponent } from './components/add-item-form/add-item-form.component';
import { ShoppingListComponent } from './components/shopping-list/shopping-list.component';
import { CategoriesComponent } from './components/categories/categories.component';
import { BudgetTrackerComponent } from './components/budget-tracker/budget-tracker.component';
import { ShoppingService } from './services/shopping.service';
import { CATEGORIES } from './models/item.model';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    AddItemFormComponent,
    ShoppingListComponent,
    CategoriesComponent,
    BudgetTrackerComponent
  ],
  template: `
    <div class="app">
      <header class="header">
        <h1>🛒 Supermarket Manager</h1>
        <p class="subtitle">Organiza tus compras del supermercado</p>
      </header>

      <main class="main">
        <div class="left-panel">
          <app-add-item-form />
          <app-categories
            [selectedCategory]="selectedCategory()"
            (categorySelected)="selectedCategory.set($event)"
          />
        </div>

        <div class="center-panel">
          <app-shopping-list
            [selectedCategory]="selectedCategory()"
          />
        </div>

        <div class="right-panel">
          <app-budget-tracker />
        </div>
      </main>
    </div>
  `,
  styles: [`
    .app {
      max-width: 1400px;
      margin: 0 auto;
      padding: 20px;
    }

    .header {
      text-align: center;
      margin-bottom: 30px;
    }

    .header h1 {
      font-size: 2.5rem;
      font-weight: 700;
    }

    .subtitle {
      color: var(--text-muted);
      margin-top: 5px;
    }

    .main {
      display: grid;
      grid-template-columns: 300px 1fr 250px;
      gap: 20px;
    }

    @media (max-width: 1024px) {
      .main {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class AppComponent {
  categories = CATEGORIES;
  selectedCategory = signal<string>('all');

  constructor(public shoppingService: ShoppingService) {}
}
