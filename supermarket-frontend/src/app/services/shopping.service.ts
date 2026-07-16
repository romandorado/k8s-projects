import { Injectable, signal, computed } from '@angular/core';
import { ShoppingItem } from '../models/item.model';

@Injectable({
  providedIn: 'root'
})
export class ShoppingService {
  private readonly ITEMS_KEY = 'supermarket-items';
  private readonly BUDGET_KEY = 'supermarket-budget';

  items = signal<ShoppingItem[]>(this.loadItems());
  budget = signal<number>(this.loadBudget());

  totalSpent = computed(() =>
    this.items()
      .filter(item => item.checked)
      .reduce((sum, item) => sum + (item.price || 0), 0)
  );

  remaining = computed(() => this.budget() - this.totalSpent());

  private loadItems(): ShoppingItem[] {
    if (typeof localStorage !== 'undefined') {
      const saved = localStorage.getItem(this.ITEMS_KEY);
      return saved ? JSON.parse(saved) : [];
    }
    return [];
  }

  private loadBudget(): number {
    if (typeof localStorage !== 'undefined') {
      const saved = localStorage.getItem(this.BUDGET_KEY);
      return saved ? parseFloat(saved) : 0;
    }
    return 0;
  }

  private saveItems(): void {
    localStorage.setItem(this.ITEMS_KEY, JSON.stringify(this.items()));
  }

  private saveBudget(): void {
    localStorage.setItem(this.BUDGET_KEY, this.budget().toString());
  }

  addItem(item: Omit<ShoppingItem, 'id' | 'checked'>): void {
    const newItem: ShoppingItem = {
      ...item,
      id: Date.now(),
      checked: false
    };
    this.items.update(items => [...items, newItem]);
    this.saveItems();
  }

  removeItem(id: number): void {
    this.items.update(items => items.filter(item => item.id !== id));
    this.saveItems();
  }

  toggleItem(id: number): void {
    this.items.update(items =>
      items.map(item =>
        item.id === id ? { ...item, checked: !item.checked } : item
      )
    );
    this.saveItems();
  }

  clearChecked(): void {
    this.items.update(items => items.filter(item => !item.checked));
    this.saveItems();
  }

  setBudget(amount: number): void {
    this.budget.set(amount);
    this.saveBudget();
  }

  getItemsByCategory(categoryId: string): ShoppingItem[] {
    if (categoryId === 'all') return this.items();
    return this.items().filter(item => item.category === categoryId);
  }

  getCategoryCount(categoryId: string): number {
    if (categoryId === 'all') return this.items().length;
    return this.items().filter(item => item.category === categoryId).length;
  }
}
