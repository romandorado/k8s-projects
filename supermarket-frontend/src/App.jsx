import { useState, useEffect } from 'react'
import ShoppingList from './components/ShoppingList'
import AddItemForm from './components/AddItemForm'
import Categories from './components/Categories'
import BudgetTracker from './components/BudgetTracker'

const CATEGORIES = [
  { id: 'fruits', name: 'Frutas y Verduras', icon: '🥬', color: '#4ade80' },
  { id: 'dairy', name: 'Lácteos', icon: '🥛', color: '#60a5fa' },
  { id: 'meat', name: 'Carnes', icon: '🥩', color: '#f87171' },
  { id: 'bakery', name: 'Panadería', icon: '🍞', color: '#fbbf24' },
  { id: 'drinks', name: 'Bebidas', icon: '🥤', color: '#a78bfa' },
  { id: 'cleaning', name: 'Limpieza', icon: '🧹', color: '#2dd4bf' },
  { id: 'other', name: 'Otros', icon: '📦', color: '#9ca3af' }
]

function App() {
  const [items, setItems] = useState(() => {
    const saved = localStorage.getItem('supermarket-items')
    return saved ? JSON.parse(saved) : []
  })
  const [budget, setBudget] = useState(() => {
    const saved = localStorage.getItem('supermarket-budget')
    return saved ? parseFloat(saved) : 0
  })
  const [selectedCategory, setSelectedCategory] = useState('all')

  useEffect(() => {
    localStorage.setItem('supermarket-items', JSON.stringify(items))
  }, [items])

  useEffect(() => {
    localStorage.setItem('supermarket-budget', budget.toString())
  }, [budget])

  const addItem = (item) => {
    setItems([...items, { ...item, id: Date.now(), checked: false }])
  }

  const removeItem = (id) => {
    setItems(items.filter(item => item.id !== id))
  }

  const toggleItem = (id) => {
    setItems(items.map(item =>
      item.id === id ? { ...item, checked: !item.checked } : item
    ))
  }

  const clearChecked = () => {
    setItems(items.filter(item => !item.checked))
  }

  const totalSpent = items
    .filter(item => item.checked)
    .reduce((sum, item) => sum + (item.price || 0), 0)

  const filteredItems = selectedCategory === 'all'
    ? items
    : items.filter(item => item.category === selectedCategory)

  return (
    <div className="app">
      <header className="header">
        <h1>🛒 Supermarket Manager</h1>
        <p className="subtitle">Organiza tus compras del supermercado</p>
      </header>

      <main className="main">
        <div className="left-panel">
          <AddItemForm categories={CATEGORIES} onAdd={addItem} />
          <Categories
            categories={CATEGORIES}
            selected={selectedCategory}
            onSelect={setSelectedCategory}
            items={items}
          />
        </div>

        <div className="center-panel">
          <ShoppingList
            items={filteredItems}
            onToggle={toggleItem}
            onRemove={removeItem}
            onClearChecked={clearChecked}
            categories={CATEGORIES}
          />
        </div>

        <div className="right-panel">
          <BudgetTracker
            budget={budget}
            setBudget={setBudget}
            totalSpent={totalSpent}
          />
        </div>
      </main>
    </div>
  )
}

export default App
