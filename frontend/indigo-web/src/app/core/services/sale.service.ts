import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CONTEXTO_ERROR_LOCAL } from '../auth/error-handling.token';
import { PagedResult } from '../models/paged-result.model';
import { Sale, SaleDetail, SaleQuery, SaleRequest } from '../models/sale.model';

@Injectable({ providedIn: 'root' })
export class SaleService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/sales`;

  listar(query: SaleQuery): Observable<PagedResult<Sale>> {
    const params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    return this.http.get<PagedResult<Sale>>(this.baseUrl, {
      params,
      context: CONTEXTO_ERROR_LOCAL,
    });
  }

  obtener(id: string): Observable<SaleDetail> {
    return this.http.get<SaleDetail>(`${this.baseUrl}/${id}`, { context: CONTEXTO_ERROR_LOCAL });
  }

  crear(request: SaleRequest): Observable<SaleDetail> {
    return this.http.post<SaleDetail>(this.baseUrl, request, { context: CONTEXTO_ERROR_LOCAL });
  }
}
