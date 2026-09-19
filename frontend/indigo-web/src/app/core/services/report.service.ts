import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CONTEXTO_ERROR_LOCAL } from '../auth/error-handling.token';
import { ReportQuery, SalesReport } from '../models/report.model';

@Injectable({ providedIn: 'root' })
export class ReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/reports`;

  obtenerVentas(query: ReportQuery): Observable<SalesReport> {
    const params = new HttpParams().set('from', query.from).set('to', query.to);
    return this.http.get<SalesReport>(`${this.baseUrl}/sales`, {
      params,
      context: CONTEXTO_ERROR_LOCAL,
    });
  }
}
