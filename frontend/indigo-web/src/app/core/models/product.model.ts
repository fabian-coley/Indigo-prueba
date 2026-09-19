export const CATEGORIAS_PRODUCTO = ['Electrónica', 'Hogar', 'Alimentos', 'Ropa', 'Otros'] as const;

export type CategoriaProducto = (typeof CATEGORIAS_PRODUCTO)[number];

export interface Product {
  id: string;
  nombre: string;
  precio: number;
  stock: number;
  categoria: CategoriaProducto;
  imagenUrl: string | null;
}

export interface ProductRequest {
  nombre: string;
  precio: number;
  stock: number;
  categoria: CategoriaProducto;
}

export interface ProductQuery {
  page: number;
  pageSize: number;
  search?: string;
  categoria?: CategoriaProducto;
}
