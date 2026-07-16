function Categories({ categories, selected, onSelect, items }) {
  const getCategoryCount = (categoryId) => {
    if (categoryId === 'all') return items.length
    return items.filter(item => item.category === categoryId).length
  }

  return (
    <div className="card categories">
      <h2>Categorías</h2>
      <div className="category-grid">
        <button
          className={`category-btn ${selected === 'all' ? 'active' : ''}`}
          onClick={() => onSelect('all')}
        >
          <span className="category-icon">📋</span>
          <span className="category-name">Todos</span>
          <span className="category-count">{items.length}</span>
        </button>

        {categories.map(cat => (
          <button
            key={cat.id}
            className={`category-btn ${selected === cat.id ? 'active' : ''}`}
            onClick={() => onSelect(cat.id)}
            style={{ '--cat-color': cat.color }}
          >
            <span className="category-icon">{cat.icon}</span>
            <span className="category-name">{cat.name}</span>
            <span className="category-count">{getCategoryCount(cat.id)}</span>
          </button>
        ))}
      </div>
    </div>
  )
}

export default Categories
