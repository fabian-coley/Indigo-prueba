export interface DailySummary {
  fecha: string;
  ventas: number;
  items: number;
  total: number;
}

export interface SalesReport {
  desde: string;
  hasta: string;
  totalGeneral: number;
  totalVentas: number;
  totalItems: number;
  porDia: DailySummary[];
}

export interface ReportQuery {
  from: string;
  to: string;
}
