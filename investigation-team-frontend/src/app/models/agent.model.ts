export interface Agent {
  id: string;
  name: string;
  role: number;
  description?: string;
  skills: string[];
  status: number;
}

export const ROLE_NAMES = ['Investigador', 'Analista', 'Escritor', 'Coordinador', 'Revisor'];
export const ROLE_EMOJIS = ['🔍', '📊', '✍️', '🎯', '✅'];