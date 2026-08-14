import { Component, OnInit, OnDestroy, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, finalize, takeUntil } from 'rxjs';

import { StockService } from './services/stock.service';
import { InvoicingService } from './services/invoicing.service';
import { Product, Invoice } from './models/types';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  styleUrls: ['./app.scss']
})
export class App implements OnInit, OnDestroy {
  private stockService = inject(StockService);
  private invoicingService = inject(InvoicingService);
  private cdr = inject(ChangeDetectorRef);
  private destroy$ = new Subject<void>();

  // Telas: 'produtos' | 'notas' | 'novaNota' | 'notaDetail'
  screen: string = 'notas';

  // Dados reais das APIs
  products: Product[] = [];
  invoices: Invoice[] = [];
  selectedInvoice: Invoice | null = null;

  // Filtros e buscas
  productSearchQuery: string = '';
  catalogSearchQuery: string = '';
  invoiceFilter: string = 'Todas';

  // Carrinho da Nova Nota
  cartItems: { productCode: string; description: string; quantity: number }[] = [];

  // Estados de Operação / RxJS
  isPrinting: boolean = false;
  printErrorMsg: string | null = null;
  modalProductOpen: boolean = false;
  newProduct: Product = { code: '', description: '', quantityOnHand: 0 };
  toast: { type: 'success' | 'error'; message: string } | null = null;

  ngOnInit(): void {
    this.loadProducts();
    this.loadInvoices();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // --- CARREGAMENTO DE DADOS COM RXJS ---
  loadProducts(): void {
    this.stockService.getProducts()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.products = data;
          this.cdr.markForCheck();
        },
        error: () => this.showToast('error', 'Falha ao carregar produtos do Estoque.')
      });
  }

  loadInvoices(): void {
    this.invoicingService.getInvoices()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.invoices = data;
          this.cdr.markForCheck();
        },
        error: () => this.showToast('error', 'Falha ao carregar Notas Fiscais.')
      });
  }

  // --- GETTERS E FILTROS ---
  get openInvoicesCount(): number {
    return this.invoices.filter(i => i.status === 'Open').length;
  }

  get filteredProducts(): Product[] {
    const q = this.productSearchQuery.toLowerCase();
    return this.products.filter(p => p.description.toLowerCase().includes(q) || p.code.toLowerCase().includes(q));
  }

  get filteredCatalogProducts(): Product[] {
    const q = this.catalogSearchQuery.toLowerCase();
    return this.products.filter(p => p.description.toLowerCase().includes(q) || p.code.toLowerCase().includes(q));
  }

  get filteredInvoices(): Invoice[] {
    if (this.invoiceFilter === 'Todas') return this.invoices;
    return this.invoices.filter(i => i.status === this.invoiceFilter);
  }

  get totalCartQuantity(): number {
    return this.cartItems.reduce((acc, curr) => acc + curr.quantity, 0);
  }

  getSaldoClass(saldo: number): string {
    if (saldo <= 0) return 'zero';
    if (saldo <= 4) return 'low';
    return 'ok';
  }

  // --- AÇÕES DE PRODUTO ---
  openNewProductModal(): void {
    this.newProduct = { code: '', description: '', quantityOnHand: 0 };
    this.modalProductOpen = true;
    this.cdr.markForCheck();
  }

  saveNewProduct(): void {
    if (!this.newProduct.code.trim() || !this.newProduct.description.trim()) {
      this.showToast('error', 'Código e Descrição são obrigatórios.');
      return;
    }

    this.stockService.createProduct(this.newProduct)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (saved) => {
          this.products.push(saved);
          this.modalProductOpen = false;
          this.showToast('success', `Produto ${saved.code} cadastrado com sucesso!`);
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.showToast('error', err.error || 'Erro ao cadastrar produto.');
          this.cdr.markForCheck();
        }
      });
  }

  // --- AÇÕES DO CARRINHO DE NOVA NOTA ---
  addToCart(p: Product): void {
    const existing = this.cartItems.find(i => i.productCode === p.code);
    if (existing) {
      if (existing.quantity + 1 > p.quantityOnHand) {
        this.showToast('error', 'Quantidade solicitada excede o saldo em estoque.');
        return;
      }
      existing.quantity++;
    } else {
      this.cartItems.push({ productCode: p.code, description: p.description, quantity: 1 });
    }
    this.cdr.markForCheck();
  }

  incCart(code: string): void {
    const product = this.products.find(p => p.code === code);
    const item = this.cartItems.find(i => i.productCode === code);
    if (item && product && item.quantity < product.quantityOnHand) {
      item.quantity++;
      this.cdr.markForCheck();
    }
  }

  decCart(code: string): void {
    const item = this.cartItems.find(i => i.productCode === code);
    if (item) {
      item.quantity--;
      if (item.quantity <= 0) {
        this.removeCart(code);
      }
      this.cdr.markForCheck();
    }
  }

  removeCart(code: string): void {
    this.cartItems = this.cartItems.filter(i => i.productCode !== code);
    this.cdr.markForCheck();
  }

  saveInvoice(): void {
    if (this.cartItems.length === 0) return;

    const payload = this.cartItems.map(i => ({ productCode: i.productCode, quantity: i.quantity }));

    this.invoicingService.createInvoice(payload)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (created) => {
          this.invoices.unshift(created);
          this.cartItems = [];
          this.selectedInvoice = created;
          this.screen = 'notaDetail';
          this.showToast('success', `Nota Fiscal nº ${created.sequenceNumber} criada com status Aberta.`);
          this.cdr.markForCheck();
        },
        error: () => {
          this.showToast('error', 'Falha ao cadastrar a Nota Fiscal.');
          this.cdr.markForCheck();
        }
      });
  }

  // --- IMPRESSÃO / FECHAMENTO DA NOTA ---
  openInvoiceDetail(invoice: Invoice): void {
    this.selectedInvoice = invoice;
    this.printErrorMsg = null;
    this.isPrinting = false;
    this.screen = 'notaDetail';
    this.cdr.markForCheck();
  }

  printInvoice(invoice: Invoice): void {
    this.isPrinting = true;
    this.printErrorMsg = null;
    this.cdr.markForCheck();

    this.invoicingService.printInvoice(invoice.id)
      .pipe(
        finalize(() => {
          this.isPrinting = false;
          this.cdr.markForCheck();
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          invoice.status = 'Closed';
          if (this.selectedInvoice && this.selectedInvoice.id === invoice.id) {
            this.selectedInvoice.status = 'Closed';
          }
          this.showToast('success', `Nota nº ${invoice.sequenceNumber} impressa e Fechada!`);
          this.loadProducts(); // Atualiza saldos abatidos
          this.cdr.markForCheck();
        },
        error: (err) => {
          if (err.status === 503) {
            this.printErrorMsg = 'O serviço de Estoque está indisponível. A nota permaneceu Aberta e nenhum saldo foi alterado.';
          } else {
            this.printErrorMsg = err.error?.message || err.error || 'Erro na validação de saldo ao emitir nota.';
          }
          this.showToast('error', 'Não foi possível concluir a impressão.');
          this.cdr.markForCheck();
        }
      });
  }

  // --- TOAST HELPER ---
  showToast(type: 'success' | 'error', message: string): void {
    this.toast = { type, message };
    this.cdr.markForCheck();
    setTimeout(() => {
      this.toast = null;
      this.cdr.markForCheck();
    }, 4000);
  }
}