import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { toObservable } from '@angular/core/rxjs-interop';
import { switchMap, startWith } from 'rxjs/operators';
import { of } from 'rxjs';
import { ShoppingItem, getCategoryById } from '../models/item.model';

@Injectable({
  providedIn: 'root'
})
export class ShoppingService {
  private http = inject(HttpClient);
  private apiUrl = '/api';

  items = signal<ShoppingItem[]>([]);
  budget = signal<number>(0);
  currentListId = signal<string | null>(null);

  totalSpent = computed(() =>
    this.items()
      .filter(item => item.checked)
      .reduce((sum, item) => sum + (item.price || 0), 0)
  );

  remaining = computed(() => this.budget() - this.totalSpent());

  constructor() {
    this.loadLists();
  }

  loadLists(): void {
    this.http.get<any[]>(`${this.apiUrl}/lists`).subscribe({
      next: (lists) => {
        if (lists.length > 0) {
          const list = lists[0];
          this.currentListId.set(list.id);
          this.budget.set(list.budget);
          this.items.set(list.items || []);
        } else {
          this.createList('Mi lista de compra', 0);
        }
      },
      error: (err) => console.error('Error loading lists:', err)
    });
  }

  createList(name: string, budget: number): void {
    this.http.post<any>(`${this.apiUrl}/lists`, { name, budget }).subscribe({
      next: (list) => {
        this.currentListId.set(list.id);
        this.budget.set(budget);
        this.items.set([]);
      },
      error: (err) => console.error('Error creating list:', err)
    });
  }

  addItem(item: Omit<ShoppingItem, 'id' | 'checked' | 'createdAt' | 'updatedAt'>): void {
    const listId = this.currentListId();
    const url = listId
      ? `${this.apiUrl}/items?listId=${listId}`
      : `${this.apiUrl}/items`;

    this.http.post<ShoppingItem>(url, item).subscribe({
      next: (newItem) => {
        this.items.update(items => [...items, newItem]);
      },
      error: (err) => console.error('Error adding item:', err)
    });
  }

  removeItem(id: string): void {
    this.http.delete(`${this.apiUrl}/items/${id}`).subscribe({
      next: () => {
        this.items.update(items => items.filter(item => item.id !== id));
      },
      error: (err) => console.error('Error removing item:', err)
    });
  }

  toggleItem(id: string): void {
    this.http.patch<{ id: string; checked: boolean }>(`${this.apiUrl}/items/${id}/toggle`, {}).subscribe({
      next: (result) => {
        this.items.update(items =>
          items.map(item =>
            item.id === id ? { ...item, checked: result.checked } : item
          )
        );
      },
      error: (err) => console.error('Error toggling item:', err)
    });
  }

  clearChecked(): void {
    const listId = this.currentListId();
    const url = listId
      ? `${this.apiUrl}/items/completed?listId=${listId}`
      : `${this.apiUrl}/items/completed`;

    this.http.delete<{ deleted: number }>(url).subscribe({
      next: () => {
        this.items.update(items => items.filter(item => !item.checked));
      },
      error: (err) => console.error('Error clearing checked:', err)
    });
  }

  setBudget(amount: number): void {
    const listId = this.currentListId();
    if (listId) {
      this.http.put(`${this.apiUrl}/lists/${listId}`, {
        name: 'Mi lista de compra',
        budget: amount
      }).subscribe({
        next: () => {
          this.budget.set(amount);
        },
        error: (err) => console.error('Error setting budget:', err)
      });
    }
  }

  getItemsByCategory(categoryId: string): ShoppingItem[] {
    if (categoryId === 'all') return this.items();
    const cat = getCategoryById(categoryId);
    return this.items().filter(item => item.category === cat.value);
  }

  getCategoryCount(categoryId: string): number {
    if (categoryId === 'all') return this.items().length;
    const cat = getCategoryById(categoryId);
    return this.items().filter(item => item.category === cat.value).length;
  }
}
