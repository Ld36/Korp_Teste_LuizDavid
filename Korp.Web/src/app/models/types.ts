export interface Product {
  id?: number;
  code: string;
  description: string;
  quantityOnHand: number;
}

export interface InvoiceItem {
  id?: number;
  productCode: string;
  quantity: number;
}

export interface Invoice {
  id: number;
  sequenceNumber: number;
  status: 'Open' | 'Closed' | string;
  createdAt: string;
  items: InvoiceItem[];
}