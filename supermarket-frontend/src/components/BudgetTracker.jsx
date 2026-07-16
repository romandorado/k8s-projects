import { useState } from 'react'

function BudgetTracker({ budget, setBudget, totalSpent }) {
  const [isEditing, setIsEditing] = useState(false)
  const [tempBudget, setTempBudget] = useState(budget.toString())

  const remaining = budget - totalSpent
  const percentage = budget > 0 ? (totalSpent / budget) * 100 : 0
  const isOverBudget = remaining < 0

  const handleSave = () => {
    setBudget(parseFloat(tempBudget) || 0)
    setIsEditing(false)
  }

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') handleSave()
    if (e.key === 'Escape') {
      setTempBudget(budget.toString())
      setIsEditing(false)
    }
  }

  return (
    <div className="card budget-tracker">
      <h2>Presupuesto</h2>

      <div className="budget-display">
        {isEditing ? (
          <div className="budget-edit">
            <input
              type="number"
              step="0.01"
              min="0"
              value={tempBudget}
              onChange={(e) => setTempBudget(e.target.value)}
              onKeyDown={handleKeyDown}
              onBlur={handleSave}
              className="input input-budget"
              autoFocus
            />
            <span className="currency">€</span>
          </div>
        ) : (
          <div
            className="budget-amount"
            onClick={() => setIsEditing(true)}
            title="Clic para editar"
          >
            {budget.toFixed(2)} €
          </div>
        )}
      </div>

      <div className="budget-stats">
        <div className="stat">
          <span className="stat-label">Gastado</span>
          <span className="stat-value spent">{totalSpent.toFixed(2)} €</span>
        </div>
        <div className="stat">
          <span className="stat-label">Restante</span>
          <span className={`stat-value ${isOverBudget ? 'over' : 'remaining'}`}>
            {remaining.toFixed(2)} €
          </span>
        </div>
      </div>

      {budget > 0 && (
        <div className="progress-bar">
          <div
            className={`progress-fill ${isOverBudget ? 'over' : ''}`}
            style={{ width: `${Math.min(percentage, 100)}%` }}
          />
          <span className="progress-text">{percentage.toFixed(0)}%</span>
        </div>
      )}

      {budget === 0 && (
        <p className="budget-hint">Clic en el monto para definir tu presupuesto</p>
      )}
    </div>
  )
}

export default BudgetTracker
