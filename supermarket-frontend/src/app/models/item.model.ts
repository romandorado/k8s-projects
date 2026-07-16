export interface ShoppingItem {
  id: string;
  name: string;
  quantity: number;
  category: number;
  price: number;
  checked: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface Category {
  id: string;
  name: string;
  icon: string;
  color: string;
  value: number;
}

export const CATEGORIES: Category[] = [
  { id: 'fruits', name: 'Frutas y Verduras', icon: '🥬', color: '#4ade80', value: 0 },
  { id: 'dairy', name: 'Lácteos', icon: '🥛', color: '#60a5fa', value: 1 },
  { id: 'meat', name: 'Carnes', icon: '🥩', color: '#f87171', value: 2 },
  { id: 'bakery', name: 'Panadería', icon: '🍞', color: '#fbbf24', value: 3 },
  { id: 'drinks', name: 'Bebidas', icon: '🥤', color: '#a78bfa', value: 4 },
  { id: 'cleaning', name: 'Limpieza', icon: '🧹', color: '#2dd4bf', value: 5 },
  { id: 'other', name: 'Otros', icon: '📦', color: '#9ca3af', value: 6 }
];

export function getCategoryById(id: string): Category {
  return CATEGORIES.find(c => c.id === id) || CATEGORIES[CATEGORIES.length - 1];
}

export function getCategoryByValue(value: number): Category {
  return CATEGORIES.find(c => c.value === value) || CATEGORIES[CATEGORIES.length - 1];
}
