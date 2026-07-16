function ShoppingList({ items, onToggle, onRemove, onClearChecked, categories }) {
  const getCategoryInfo = (categoryId) => {
    return categories.find(c => c.id === categoryId) || categories[categories.length - 1]
  }

  const groupedItems = items.reduce((groups, item) => {
    const cat = item.category || 'other'
    if (!groups[cat]) groups[cat] = []
    groups[cat].push(item)
    return groups
  }, {})

  if (items.length === 0) {
    return (
      <div className="card shopping-list empty">
        <div className="empty-state">
          <span className="empty-icon">🛒</span>
          <p>Tu lista está vacía</p>
          <p className="empty-hint">Agrega productos desde el formulario</p>
        </div>
      </div>
    )
  }

  const checkedCount = items.filter(i => i.checked).length

  return (
    <div className="card shopping-list">
      <div className="list-header">
        <h2>Lista de Compra</h2>
        {checkedCount > 0 && (
          <button className="btn btn-danger btn-small" onClick={onClearChecked}>
            Limpiar ({checkedCount})
          </button>
        )}
      </div>

      {Object.entries(groupedItems).map(([catId, catItems]) => {
        const cat = getCategoryInfo(catId)
        return (
          <div key={catId} className="category-group">
            <h3 className="category-title" style={{ borderBottomColor: cat.color }}>
              {cat.icon} {cat.name}
            </h3>
            <ul className="item-list">
              {catItems.map(item => (
                <li key={item.id} className={`item ${item.checked ? 'checked' : ''}`}>
                  <label className="item-label">
                    <input
                      type="checkbox"
                      checked={item.checked}
                      onChange={() => onToggle(item.id)}
                    />
                    <span className="item-name">{item.name}</span>
                    {item.quantity > 1 && (
                      <span className="item-quantity">x{item.quantity}</span>
                    )}
                    {item.price > 0 && (
                      <span className="item-price">{item.price.toFixed(2)}€</span>
                    )}
                  </label>
                  <button
                    className="btn-icon"
                    onClick={() => onRemove(item.id)}
                    title="Eliminar"
                  >
                    ✕
                  </button>
                </li>
              ))}
            </ul>
          </div>
        )
      })}
    </div>
  )
}

export default ShoppingList
