export interface ShoppingItem {
  id: number;
  name: string;
  quantity: number;
  category: string;
  price: number;
  checked: boolean;
}

export interface Category {
  id: string;
  name: string;
  icon: string;
  color: string;
}

export const CATEGORIES: Category[] = [
  { id: 'fruits', name: 'Frutas y Verduras', icon: '🥬', color: '#4ade80' },
  { id: 'dairy', name: 'Lácteos', icon: '🥛', color: '#60a5fa' },
  { id: 'meat', name: 'Carnes', icon: '🥩', color: '#f87171' },
  { id: 'bakery', name: 'Panadería', icon: '🍞', color: '#fbbf24' },
  { id: 'drinks', name: 'Bebidas', icon: '🥤', color: '#a78bfa' },
  { id: 'cleaning', name: 'Limpieza', icon: '🧹', color: '#2dd4bf' },
  { id: 'other', name: 'Otros', icon: '📦', color: '#9ca3af' }
];
