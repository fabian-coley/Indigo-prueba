export interface SaleItem {
  productoId: string;
  productoNombre: string;
  cantidad: number;
  precioUnitario: number;
  subtotal: number;
}

export interface Sale {
  id: string;
  fecha: string;
  total: number;
  cantidadItems: number;
  usuarioEmail: string;
}

export interface SaleDetail extends Sale {
  items: SaleItem[];
}

export interface SaleItemRequest {
  productoId: string;
  cantidad: number;
}

export interface SaleRequest {
  items: SaleItemRequest[];
}

export interface SaleQuery {
  page: number;
  pageSize: number;
}
