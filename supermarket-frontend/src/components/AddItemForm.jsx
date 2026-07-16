import { useState } from 'react'

function AddItemForm({ categories, onAdd }) {
  const [name, setName] = useState('')
  const [quantity, setQuantity] = useState(1)
  const [category, setCategory] = useState('other')
  const [price, setPrice] = useState('')

  const handleSubmit = (e) => {
    e.preventDefault()
    if (!name.trim()) return

    onAdd({
      name: name.trim(),
      quantity: parseInt(quantity) || 1,
      category,
      price: parseFloat(price) || 0
    })

    setName('')
    setQuantity(1)
    setCategory('other')
    setPrice('')
  }

  return (
    <div className="card add-item-form">
      <h2>Agregar Producto</h2>
      <form onSubmit={handleSubmit}>
        <div className="form-group">
          <input
            type="text"
            placeholder="Nombre del producto"
            value={name}
            onChange={(e) => setName(e.target.value)}
            className="input"
          />
        </div>

        <div className="form-row">
          <div className="form-group">
            <label>Cantidad</label>
            <input
              type="number"
              min="1"
              value={quantity}
              onChange={(e) => setQuantity(e.target.value)}
              className="input input-small"
            />
          </div>

          <div className="form-group">
            <label>Precio (€)</label>
            <input
              type="number"
              step="0.01"
              min="0"
              placeholder="0.00"
              value={price}
              onChange={(e) => setPrice(e.target.value)}
              className="input input-small"
            />
          </div>
        </div>

        <div className="form-group">
          <label>Categoría</label>
          <select
            value={category}
            onChange={(e) => setCategory(e.target.value)}
            className="input select"
          >
            {categories.map(cat => (
              <option key={cat.id} value={cat.id}>
                {cat.icon} {cat.name}
              </option>
            ))}
          </select>
        </div>

        <button type="submit" className="btn btn-primary">
          Agregar
        </button>
      </form>
    </div>
  )
}

export default AddItemForm
