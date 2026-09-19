import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CONTEXTO_ERROR_LOCAL } from '../auth/error-handling.token';
import { PagedResult } from '../models/paged-result.model';
import { Product, ProductQuery, ProductRequest } from '../models/product.model';

export interface ImagenSubida {
  imagenUrl: string;
}

@Injectable({ providedIn: 'root' })
export class ProductService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/products`;

  listar(query: ProductQuery): Observable<PagedResult<Product>> {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    if (query.search) {
      params = params.set('search', query.search);
    }
    if (query.categoria) {
      params = params.set('categoria', query.categoria);
    }
    return this.http.get<PagedResult<Product>>(this.baseUrl, {
      params,
      context: CONTEXTO_ERROR_LOCAL,
    });
  }

  obtener(id: string): Observable<Product> {
    return this.http.get<Product>(`${this.baseUrl}/${id}`, { context: CONTEXTO_ERROR_LOCAL });
  }

  crear(request: ProductRequest): Observable<Product> {
    return this.http.post<Product>(this.baseUrl, request, { context: CONTEXTO_ERROR_LOCAL });
  }

  actualizar(id: string, request: ProductRequest): Observable<Product> {
    return this.http.put<Product>(`${this.baseUrl}/${id}`, request, { context: CONTEXTO_ERROR_LOCAL });
  }

  eliminar(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`, { context: CONTEXTO_ERROR_LOCAL });
  }

  subirImagen(id: string, archivo: File): Observable<ImagenSubida> {
    const body = new FormData();
    body.append('file', archivo);
    return this.http.post<ImagenSubida>(`${this.baseUrl}/${id}/imagen`, body, {
      context: CONTEXTO_ERROR_LOCAL,
    });
  }
}
