import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Invoice } from '../models/types';

@Injectable({
  providedIn: 'root'
})
export class InvoicingService {
  private http = inject(HttpClient);
  private readonly baseUrl = 'http://localhost:5106/api/invoices';

  getInvoices(): Observable<Invoice[]> {
    return this.http.get<Invoice[]>(this.baseUrl);
  }

  createInvoice(items: { productCode: string; quantity: number }[]): Observable<Invoice> {
    return this.http.post<Invoice>(this.baseUrl, { items });
  }

  printInvoice(id: number): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/${id}/print`, {});
  }
}